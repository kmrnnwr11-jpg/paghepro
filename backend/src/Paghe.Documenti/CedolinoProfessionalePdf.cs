using System.Globalization;
using Paghe.Core.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Paghe.Documenti;

/// <summary>
/// Cedolino A4 nel formato classico dell'ufficio paghe: testata ditta, griglia anagrafica a
/// fasce, blocco della retribuzione contrattuale, corpo voci affiancato dal calendario del mese
/// e fasce di quadratura (contributi, IRPEF, netto, ratei, statistici, progressivi).
/// Non esegue calcoli normativi: presenta esclusivamente i valori ricevuti dal dominio e lascia
/// vuote le caselle che il software non alimenta, senza sostituirle con zeri.
/// </summary>
internal static class CedolinoProfessionalePdf
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    private const string Inchiostro = "#000000";
    private const string Testo = "#111111";
    private const string Secondario = "#4A4A4A";
    private const string Bordo = "#000000";
    private const string BordoTenue = "#8C8C8C";
    private const string Fondo = "#E9E9E9";
    private const string FondoRiga = "#F5F5F5";
    private const string Avviso = "#9A3412";
    private const string Bianco = "#FFFFFF";

    private const float LarghezzaCalendario = 104f;

    internal static byte[] Genera(CedolinoPresentazione dati) =>
        Document.Create(documento => documento.Page(pagina => PaginaCedolino(pagina, dati)))
            .GeneratePdf();

    private static void PaginaCedolino(PageDescriptor pagina, CedolinoPresentazione dati)
    {
        pagina.Size(PageSizes.A4);
        pagina.MarginHorizontal(0.85f, Unit.Centimetre);
        pagina.MarginTop(0.7f, Unit.Centimetre);
        pagina.MarginBottom(0.7f, Unit.Centimetre);
        pagina.PageColor(Bianco);
        pagina.DefaultTextStyle(stile => stile.FontFamily("Lato").FontSize(6.6f).FontColor(Testo));

        pagina.Header().Element(c => TestataDitta(c, dati));
        pagina.Content().PaddingTop(4).Column(colonna =>
        {
            colonna.Spacing(2);
            colonna.Item().Element(c => GrigliaAnagrafica(c, dati));
            colonna.Item().Element(c => BloccoRetribuzione(c, dati));
            colonna.Item().Element(c => CorpoVoci(c, dati));
            colonna.Item().ShowEntire().Element(c => FasciaContributi(c, dati.Cedolino));
            colonna.Item().ShowEntire().Element(c => FasciaFiscale(c, dati.Cedolino));
            colonna.Item().ShowEntire().Element(c => FasciaNetto(c, dati.Cedolino));
            colonna.Item().ShowEntire().Element(c => FasciaRatei(c, dati.Cedolino));
            if (dati.RigheFigurativeRilevanti.Count > 0)
                colonna.Item().ShowEntire().Element(c => DettaglioFigurativo(c, dati.RigheFigurativeRilevanti));
            colonna.Item().ShowEntire().Element(c => FasciaStatistici(c, dati));
            colonna.Item().ShowEntire().Element(c => FasciaDetrazioni(c, dati.Cedolino));
            colonna.Item().ShowEntire().Element(c => FasciaProgressivi(c, dati.Progressivi));
            colonna.Item().ShowEntire().Element(c => PagamentoEFirme(c, dati));
        });
        pagina.Footer().Element(c => Piede(c, dati));
    }

    // ---- Testata ------------------------------------------------------------------------

    private static void TestataDitta(IContainer contenitore, CedolinoPresentazione dati)
    {
        contenitore.Column(testata =>
        {
            testata.Item().Background(Avviso).PaddingVertical(3).PaddingHorizontal(5).Row(avviso =>
            {
                avviso.RelativeItem().Text(CedolinoPdfPresentazione.TitoloSimulazione)
                    .FontSize(8.8f).SemiBold().FontColor(Bianco);
                avviso.AutoItem().PaddingLeft(8).Text(CedolinoPdfPresentazione.AvvisoNonCertificato)
                    .FontSize(8.8f).SemiBold().FontColor(Bianco);
            });

            testata.Item().PaddingTop(3).BorderBottom(1.2f).BorderColor(Bordo).PaddingBottom(4).Row(riga =>
            {
                riga.RelativeItem(6).Column(ditta =>
                {
                    ditta.Item().Text("Ditta").FontSize(6.2f).SemiBold().FontColor(Secondario);
                    ditta.Item().PaddingLeft(10).Column(dati2 =>
                    {
                        dati2.Item().Text(Valore(dati.Intestazione.Azienda).ToUpperInvariant())
                            .FontSize(9.5f).SemiBold().FontColor(Inchiostro);
                        dati2.Item().Text(UnisciRiga(
                                dati.Intestazione.AziendaIndirizzo, dati.Intestazione.AziendaComune))
                            .FontSize(6.4f).FontColor(Secondario);
                        dati2.Item().Text(TestoFiscaleAzienda(dati.Intestazione))
                            .FontSize(6.4f).FontColor(Secondario);
                    });
                });

                riga.RelativeItem(4).AlignRight().Column(documento =>
                {
                    if (dati.Intestazione.Logo is { Length: > 0 } logo)
                        documento.Item().AlignRight().Height(26).Image(logo).FitArea();
                    documento.Item().AlignRight().Text("PROSPETTO RETRIBUTIVO MENSILE")
                        .FontSize(7.5f).SemiBold().FontColor(Secondario);
                    documento.Item().AlignRight().Text(
                            string.IsNullOrWhiteSpace(dati.Emissione.RiferimentoDocumento)
                                ? Capitalizza(dati.Periodo)
                                : $"{Capitalizza(dati.Periodo)} - Foglio n. {dati.Emissione.RiferimentoDocumento}")
                        .FontSize(7.4f).SemiBold().FontColor(Inchiostro);
                });
            });
        });
    }

    // ---- Griglia anagrafica -------------------------------------------------------------

    private static void GrigliaAnagrafica(IContainer contenitore, CedolinoPresentazione dati)
    {
        var intestazione = dati.Intestazione;
        contenitore.Border(0.8f).BorderColor(Bordo).Column(colonna =>
        {
            colonna.Item().Row(riga =>
            {
                Casella(riga, 2.4f, "MESE RETRIBUTIVO", Capitalizza(dati.Periodo).ToUpperInvariant(), evidenza: true);
                Casella(riga, 1.9f, "MATR. INPS AZIENDA", intestazione.MatricolaInps);
                Casella(riga, 1.8f, "POSIZIONE INAIL / P.A.T.", intestazione.PatInail);
                Casella(riga, 0.9f, "CODICE", intestazione.Matricola);
                Casella(riga, 3.2f, "COGNOME E NOME", intestazione.Dipendente, evidenza: true);
                Casella(riga, 1.1f, "DATA ASSUNZ.", intestazione.DataAssunzione);
                Casella(riga, 1.1f, "DATA CESSAZ.", intestazione.DataCessazione, ultimo: true);
            });
            colonna.Item().BorderTop(0.5f).BorderColor(BordoTenue).Row(riga =>
            {
                Casella(riga, 2.2f, "CODICE FISCALE", intestazione.CodiceFiscaleDipendente);
                Casella(riga, 2.6f, "COMUNE DI RESIDENZA / DOMICILIO", intestazione.ComuneResidenza);
                Casella(riga, 1.1f, "DATA DI NASC.", intestazione.DataNascita);
                Casella(riga, 3.3f, "SITUAZIONE ANF", EtichettaAnf(dati.Cartellino), ultimo: true);
            });
            colonna.Item().BorderTop(0.5f).BorderColor(BordoTenue).Row(riga =>
            {
                Casella(riga, 1.8f, "QUALIFICA", intestazione.Qualifica);
                Casella(riga, 0.8f, "LIVELLO", intestazione.Livello);
                Casella(riga, 3.0f, "CCNL APPLICATO", intestazione.Ccnl);
                Casella(riga, 1.0f, "COD. CNEL", string.IsNullOrWhiteSpace(intestazione.CodiceCnel)
                    ? "NON CONFIGURATO"
                    : intestazione.CodiceCnel);
                Casella(riga, 2.0f, "TIPO CONTRATTO", intestazione.TipoContratto);
                Casella(riga, 1.6f, "ORARIO", intestazione.Orario);
                Casella(riga, 0.7f, "MENSILITÀ", intestazione.Mensilita?.ToString(It), ultimo: true);
            });
        });
    }

    private static void Casella(
        RowDescriptor riga,
        float peso,
        string etichetta,
        string? valore,
        bool evidenza = false,
        bool ultimo = false)
    {
        var cella = riga.RelativeItem(peso);
        if (!ultimo) cella = cella.BorderRight(0.5f).BorderColor(BordoTenue);
        cella.PaddingVertical(2.5f).PaddingHorizontal(3.5f).Column(colonna =>
        {
            colonna.Item().Text(etichetta).FontSize(4.7f).SemiBold().FontColor(Secondario);
            colonna.Item().PaddingTop(0.8f).Text(Valore(valore))
                .FontSize(evidenza ? 7.6f : 6.8f).SemiBold().FontColor(Inchiostro);
        });
    }

    // ---- Retribuzione contrattuale ------------------------------------------------------

    private static void BloccoRetribuzione(IContainer contenitore, CedolinoPresentazione dati)
    {
        var righe = dati.Righe;
        decimal? pagaBase = CompetenzaVoce(righe, "PAGA_BASE");
        // Contingenza (EDR storico incluso) e terzo elemento provinciale: voci fisse distinte
        // in busta dopo la revisione del consulente; il totale mensile le comprende.
        decimal? contingenza = CompetenzaVoce(righe, "CONTINGENZA");
        decimal? terzoElemento = CompetenzaVoce(righe, "TERZO_EL");
        decimal? edr = CompetenzaVoce(righe, "EDR");
        decimal? superminimo = CompetenzaVoce(righe, "SUPERMIN");
        decimal? scatti = CompetenzaVoce(righe, "SCATTI");
        contenitore.Border(0.8f).BorderColor(Bordo).Row(riga =>
        {
            CasellaNumerica(riga, 1.2f, "PAGA BASE", Importo(pagaBase));
            CasellaNumerica(riga, 1.1f, "CONTINGENZA", Importo(contingenza));
            CasellaNumerica(riga, 1f, "TERZO ELEM.", Importo(terzoElemento));
            CasellaNumerica(riga, 1f, "E.D.R.", Importo(edr));
            CasellaNumerica(riga, 1.1f, "SUPERMINIMO", Importo(superminimo));
            CasellaNumerica(riga, 1.1f, "SCATTI ANZ.", Importo(scatti));
            CasellaNumerica(riga, 1.25f, "RETRIBUZIONE FISSA",
                Importo4(dati.Cedolino.RetribuzioneFissa), evidenza: true);
            CasellaNumerica(riga, 1.25f, "TARIFFA ORARIA",
                Importo4(dati.Cedolino.TariffaOraria));
            CasellaNumerica(riga, 1.25f, "TARIFFA GIORNALIERA",
                Importo4(dati.Cedolino.TariffaGiornaliera), ultimo: true);
        });
    }

    private static void CasellaNumerica(
        RowDescriptor riga,
        float peso,
        string etichetta,
        string valore,
        bool evidenza = false,
        bool ultimo = false)
    {
        var cella = riga.RelativeItem(peso);
        if (!ultimo) cella = cella.BorderRight(0.5f).BorderColor(BordoTenue);
        if (evidenza) cella = cella.Background(Fondo);
        cella.PaddingVertical(2.5f).PaddingHorizontal(3.5f).Column(colonna =>
        {
            colonna.Item().Text(etichetta).FontSize(4.7f).SemiBold().FontColor(Secondario);
            colonna.Item().PaddingTop(0.8f).AlignRight().Text(valore)
                .FontSize(7f).SemiBold().FontColor(Inchiostro);
        });
    }

    // ---- Corpo voci + calendario --------------------------------------------------------

    private static void CorpoVoci(IContainer contenitore, CedolinoPresentazione dati)
    {
        contenitore.Row(riga =>
        {
            riga.RelativeItem().Element(c => TabellaVoci(c, dati));
            riga.ConstantItem(LarghezzaCalendario).PaddingLeft(2)
                .Element(c => StrisciaCalendario(c, dati.Cartellino));
        });
    }

    private static void TabellaVoci(IContainer contenitore, CedolinoPresentazione dati)
    {
        var righe = dati.Righe;
        contenitore.Border(0.8f).BorderColor(Bordo).Table(tabella =>
        {
            tabella.ColumnsDefinition(colonne =>
            {
                colonne.RelativeColumn(1.65f);  // codice: evita di spezzare QUATTORDICESIMA
                colonne.RelativeColumn(3.85f);  // descrizione
                colonne.RelativeColumn(1.1f);   // ore / giorni
                colonne.RelativeColumn(1.25f);  // base
                colonne.RelativeColumn(1.4f);   // competenze
                colonne.RelativeColumn(1.4f);   // trattenute
            });

            tabella.Header(header =>
            {
                IntestazioneVoci(header, "CODICE");
                IntestazioneVoci(header, "DESCRIZIONE");
                IntestazioneVoci(header, "ORE / GIORNI", destra: true);
                IntestazioneVoci(header, "BASE", destra: true);
                IntestazioneVoci(header, "COMPETENZE", destra: true, evidenza: true);
                IntestazioneVoci(header, "TRATTENUTE", destra: true, ultimo: true);
            });

            if (righe.Count == 0)
                tabella.Cell().ColumnSpan(6).BorderBottom(0.35f).BorderColor(BordoTenue).Padding(8)
                    .Text("Nessuna voce presente nel mese.").FontColor(Secondario);

            for (int indice = 0; indice < righe.Count; indice++)
            {
                var voce = righe[indice];
                string sfondo = indice % 2 == 0 ? Bianco : FondoRiga;
                CellaVoce(tabella, voce.Codice, sfondo, piccolo: true);
                CellaVoce(tabella, voce.Descrizione, sfondo);
                CellaVoce(tabella, Numero(voce.Quantita), sfondo, destra: true);
                CellaVoce(tabella, Importo4Breve(voce.Base), sfondo, destra: true);
                CellaVoce(tabella, ImportoEconomico(voce.Competenza), sfondo, destra: true,
                    evidenza: true, colore: voce.Competenza < 0m ? Avviso : Inchiostro);
                CellaVoce(tabella, ImportoEconomico(voce.Trattenuta), sfondo, destra: true, ultimo: true);
            }

            tabella.Cell().ColumnSpan(4).BorderTop(1f).BorderColor(Bordo)
                .PaddingVertical(3).PaddingHorizontal(4)
                .Text("TOTALI DI COLONNA").FontSize(5.2f).SemiBold().FontColor(Inchiostro);
            tabella.Cell().BorderTop(1f).BorderLeft(0.5f).BorderColor(Bordo).Background(Fondo)
                .PaddingVertical(3).PaddingHorizontal(4).AlignRight()
                .Text(Importo(dati.Cedolino.TotaleCompetenze)).FontSize(7.2f).SemiBold().FontColor(Inchiostro);
            tabella.Cell().BorderTop(1f).BorderLeft(0.5f).BorderColor(Bordo)
                .PaddingVertical(3).PaddingHorizontal(4).AlignRight()
                .Text(Importo(dati.Cedolino.TotaleTrattenute)).FontSize(7.2f).SemiBold().FontColor(Inchiostro);
        });
    }

    private static void IntestazioneVoci(
        TableCellDescriptor header,
        string testo,
        bool destra = false,
        bool evidenza = false,
        bool ultimo = false)
    {
        var cella = header.Cell().Background(evidenza ? Fondo : Bianco)
            .BorderBottom(0.9f).BorderColor(Bordo);
        if (!ultimo) cella = cella.BorderRight(0.5f).BorderColor(BordoTenue);
        var interno = cella.PaddingVertical(2.5f).PaddingHorizontal(3.5f);
        if (destra) interno = interno.AlignRight();
        interno.Text(testo).FontSize(4.9f).SemiBold().FontColor(Inchiostro);
    }

    private static void CellaVoce(
        TableDescriptor tabella,
        string testo,
        string sfondo,
        bool destra = false,
        bool piccolo = false,
        bool evidenza = false,
        bool ultimo = false,
        string? colore = null)
    {
        var cella = tabella.Cell().Background(evidenza ? Fondo : sfondo)
            .BorderBottom(0.35f).BorderColor(BordoTenue);
        if (!ultimo) cella = cella.BorderRight(0.4f).BorderColor(BordoTenue);
        var interno = cella.PaddingVertical(2f).PaddingHorizontal(3.5f);
        if (destra) interno = interno.AlignRight();
        interno.Text(testo).FontSize(piccolo ? 5.9f : 6.5f).FontColor(colore ?? Testo);
    }

    private static void StrisciaCalendario(IContainer contenitore, Cartellino? cartellino)
    {
        bool disponibile = cartellino is { Giornate.Count: > 0 };
        if (!disponibile)
        {
            // Un calendario assente non viene simulato con 31 righe vuote: l'assenza del dato
            // resta evidente e il prospetto non suggerisce presenze mai acquisite.
            contenitore.Border(0.8f).BorderColor(Bordo).Column(colonna =>
            {
                colonna.Item().Background(Fondo).PaddingVertical(2.5f).PaddingHorizontal(3)
                    .Text("CALENDARIO GIORNALIERO").FontSize(4.8f).SemiBold().FontColor(Inchiostro);
                colonna.Item().Padding(5).Text("NON DISPONIBILE")
                    .FontSize(5.5f).SemiBold().FontColor(Avviso);
            });
            return;
        }

        var perGiorno = disponibile
            ? cartellino!.Giornate.ToDictionary(giorno => giorno.Data.Day)
            : new Dictionary<int, GiornataCartellino>();
        int giorniDelMese = cartellino is null
            ? 31
            : DateTime.DaysInMonth(cartellino.Anno, cartellino.Mese);

        contenitore.Border(0.8f).BorderColor(Bordo).Column(colonna =>
        {
            colonna.Item().Table(tabella =>
            {
                tabella.ColumnsDefinition(colonne =>
                {
                    colonne.RelativeColumn(0.8f);
                    colonne.RelativeColumn(1.3f);
                    colonne.RelativeColumn(0.9f);
                });
                tabella.Header(header =>
                {
                    IntestazioneCalendario(header, "GG");
                    IntestazioneCalendario(header, "ORE LAV.", destra: true);
                    IntestazioneCalendario(header, "SIGLA", ultimo: true);
                });

                for (int giorno = 1; giorno <= giorniDelMese; giorno++)
                {
                    bool trovato = perGiorno.TryGetValue(giorno, out var dato);
                    string sfondo = giorno % 2 == 0 ? FondoRiga : Bianco;
                    CellaCalendario(tabella, giorno.ToString(It), sfondo, colore: Secondario);
                    CellaCalendario(tabella, trovato ? OreGiornata(dato!) : "-", sfondo, destra: true);
                    CellaCalendario(tabella, trovato ? Sigla(dato!.Causale) : "", sfondo, ultimo: true);
                }
            });
        });
    }

    private static void IntestazioneCalendario(
        TableCellDescriptor header, string testo, bool destra = false, bool ultimo = false)
    {
        var cella = header.Cell().Background(Fondo).BorderBottom(0.9f).BorderColor(Bordo);
        if (!ultimo) cella = cella.BorderRight(0.4f).BorderColor(BordoTenue);
        var interno = cella.PaddingVertical(2.5f).PaddingHorizontal(2.5f);
        if (destra) interno = interno.AlignRight();
        interno.Text(testo).FontSize(4.7f).SemiBold().FontColor(Inchiostro);
    }

    private static void CellaCalendario(
        TableDescriptor tabella,
        string testo,
        string sfondo,
        bool destra = false,
        bool ultimo = false,
        string? colore = null)
    {
        var cella = tabella.Cell().Background(sfondo).BorderBottom(0.3f).BorderColor(BordoTenue);
        if (!ultimo) cella = cella.BorderRight(0.4f).BorderColor(BordoTenue);
        var interno = cella.PaddingVertical(1.4f).PaddingHorizontal(2.5f);
        if (destra) interno = interno.AlignRight();
        interno.Text(testo).FontSize(5.6f).FontColor(colore ?? Inchiostro);
    }

    // ---- Fasce di quadratura ------------------------------------------------------------

    private static void FasciaContributi(IContainer contenitore, Cedolino cedolino) =>
        contenitore.Border(0.8f).BorderColor(Bordo).Row(riga =>
        {
            CasellaNumerica(riga, 1.2f, "TOTALE LORDO", Importo(cedolino.TotaleCompetenze));
            CasellaNumerica(riga, 1.3f, "IMPON. CONTR. SOC.", Importo(cedolino.ImponibileInps));
            CasellaNumerica(riga, 1.4f, "CONTRIB. C/DIPENDENTE", Importo(cedolino.ContributiDipendente), evidenza: true);
            CasellaNumerica(riga, 1.4f, "CONTRIB. C/DITTA *", Importo(cedolino.ContributiDatore));
            CasellaNumerica(riga, 1.2f, "SOMME ESENTI", Importo(cedolino.TotaleEsenti), ultimo: true);
        });

    private static void FasciaFiscale(IContainer contenitore, Cedolino cedolino) =>
        contenitore.Border(0.8f).BorderColor(Bordo).Row(riga =>
        {
            CasellaNumerica(riga, 1.3f, "IMPONIBILE IRPEF", Importo(cedolino.ImponibileIrpef));
            CasellaNumerica(riga, 1.1f, "IRPEF LORDA", Importo(cedolino.IrpefLorda));
            CasellaNumerica(riga, 1.3f, "TOT. DETRAZIONI",
                Importo(cedolino.DetrazioniLavoroDipendente + cedolino.UlterioreDetrazioneCuneo));
            CasellaNumerica(riga, 1.2f, "IRPEF NETTA", Importo(cedolino.IrpefNetta), evidenza: true);
            CasellaNumerica(riga, 1.3f, "PREMIO AGEVOLATO", Importo(cedolino.PremioRisultatoAgevolato));
            CasellaNumerica(riga, 1.3f, "IMPOSTA SOSTITUTIVA", Importo(cedolino.ImpostaSostitutivaPremio), ultimo: true);
        });

    private static void FasciaNetto(IContainer contenitore, Cedolino cedolino) =>
        contenitore.Border(0.8f).BorderColor(Bordo).Row(riga =>
        {
            CasellaNumerica(riga, 1.2f, "IRPEF ERARIALE", Importo(cedolino.IrpefNetta));
            CasellaNumerica(riga, 1.2f, "ADDIZ. REGIONALE", Importo(cedolino.AddizionaleRegionale));
            CasellaNumerica(riga, 1.2f, "ADDIZ. COMUNALE", Importo(cedolino.AddizionaleComunale));
            CasellaNumerica(riga, 1.2f, "TOT. TRATTENUTE", Importo(cedolino.TotaleTrattenute));
            riga.RelativeItem(1.9f).Background(Fondo).PaddingVertical(3).PaddingHorizontal(5)
                .Column(netto =>
                {
                    netto.Item().Text("NETTO SIMULATO").FontSize(5.2f).SemiBold().FontColor(Inchiostro);
                    netto.Item().AlignRight().Text(Importo(cedolino.NettoInBusta))
                        .FontSize(13f).SemiBold().FontColor(Inchiostro);
                });
        });

    private static void FasciaRatei(IContainer contenitore, Cedolino cedolino) =>
        contenitore.Border(0.8f).BorderColor(Bordo).Column(colonna =>
        {
            colonna.Item().Background(Fondo).Row(riga =>
            {
                EtichettaColonna(riga, 2.2f, "RATEI E ISTITUTI");
                EtichettaColonna(riga, 1f, "A.P.", destra: true);
                EtichettaColonna(riga, 1f, "MATURATO", destra: true);
                EtichettaColonna(riga, 1f, "GODUTO", destra: true);
                EtichettaColonna(riga, 1f, "RESIDUO", destra: true, ultimo: true);
            });
            RigaRateo(colonna, "FERIE (giorni)", saldoPrecedente: null,
                cedolino.FerieMaturate, cedolino.FerieGodute, cedolino.FerieResidue);
            RigaRateo(colonna, "ROL (ore)", cedolino.RolSaldoPrecedente,
                cedolino.RolMaturati, cedolino.RolGoduti, cedolino.RolResidui);
            RigaRateo(colonna, "EX FESTIVITÀ (ore)", cedolino.ExFestivitaSaldoPrecedente,
                cedolino.ExFestivitaMaturate, cedolino.ExFestivitaGodute, cedolino.ExFestivitaResidue);
            colonna.Item().BorderTop(0.4f).BorderColor(BordoTenue).PaddingVertical(2)
                .PaddingHorizontal(3.5f).Text(
                    "Saldo precedente ferie: dato non disponibile nel modello del cedolino.")
                .FontSize(5.2f).FontColor(Secondario);
        });

    private static void RigaRateo(
        ColumnDescriptor colonna, string istituto,
        decimal? saldoPrecedente, decimal maturato, decimal goduto, decimal residuo) =>
        colonna.Item().BorderTop(0.4f).BorderColor(BordoTenue).Row(riga =>
        {
            ValoreColonna(riga, 2.2f, istituto);
            ValoreColonna(riga, 1f, Numero(saldoPrecedente), destra: true);
            ValoreColonna(riga, 1f, Numero(maturato), destra: true);
            ValoreColonna(riga, 1f, Numero(goduto), destra: true);
            ValoreColonna(riga, 1f, Numero(residuo), destra: true, evidenza: true, ultimo: true);
        });

    private static void FasciaStatistici(IContainer contenitore, CedolinoPresentazione dati) =>
        contenitore.Border(0.8f).BorderColor(Bordo).Row(riga =>
        {
            CasellaNumerica(riga, 1f, "GG. LAVORATI", GiorniLavorati(dati.Cartellino) ?? "");
            CasellaNumerica(riga, 1f, "ORE LAVORATE", OreLavorate(dati.Cartellino) ?? "");
            CasellaNumerica(riga, 1.4f, "IMPONIBILE INPS", Importo(dati.Cedolino.ImponibileInps));
            CasellaNumerica(riga, 1.2f, "TFR MESE *", Importo(dati.Cedolino.QuotaTfr));
            CasellaNumerica(riga, 1.5f, "COSTO PARZIALE STIMATO DEL MESE",
                Importo(dati.Cedolino.CostoAzienda));
            // Costo medio annualizzato (retribuzione annua con 13ª/14ª + contributi + TFR, /12):
            // è un numero diverso dal costo del mese. Vuoto sui cedolini archiviati prima del dato.
            CasellaNumerica(riga, 1.7f, "COSTO MEDIO PARZIALE STIMATO ANNUO/12",
                dati.Cedolino.CostoMedioAnnualizzato > 0m
                    ? Importo(dati.Cedolino.CostoMedioAnnualizzato)
                    : "", ultimo: true);
        });

    private static void DettaglioFigurativo(
        IContainer contenitore, IReadOnlyList<RigaFigurativaPresentata> righe)
    {
        contenitore.Border(0.8f).BorderColor(Bordo).Table(tabella =>
        {
            tabella.ColumnsDefinition(colonne =>
            {
                colonne.RelativeColumn(1.7f);
                colonne.RelativeColumn(4.5f);
                colonne.RelativeColumn(1.4f);
                colonne.RelativeColumn(1.4f);
            });
            tabella.Header(header =>
            {
                header.Cell().ColumnSpan(4).Background(Fondo).PaddingVertical(2.5f).PaddingHorizontal(3.5f)
                    .Text("DETTAGLIO FIGURATIVO TFR / FONDI / PRESTAZIONI RECUPERABILI")
                    .FontSize(4.9f).SemiBold().FontColor(Inchiostro);
                IntestazioneVoci(header, "CODICE");
                IntestazioneVoci(header, "DESCRIZIONE");
                IntestazioneVoci(header, "BASE", destra: true);
                IntestazioneVoci(header, "IMPORTO", destra: true, ultimo: true);
            });
            for (int indice = 0; indice < righe.Count; indice++)
            {
                var voce = righe[indice];
                string sfondo = indice % 2 == 0 ? Bianco : FondoRiga;
                CellaVoce(tabella, voce.Codice, sfondo, piccolo: true);
                CellaVoce(tabella, voce.Descrizione, sfondo);
                CellaVoce(tabella, Importo4(voce.Base), sfondo, destra: true);
                CellaVoce(tabella, Importo(voce.Importo), sfondo, destra: true, ultimo: true);
            }
        });
    }

    private static void FasciaDetrazioni(IContainer contenitore, Cedolino cedolino) =>
        contenitore.Border(0.8f).BorderColor(Bordo).Row(riga =>
        {
            CasellaNumerica(riga, 1.3f, "DETR. LAVORO DIP.", Importo(cedolino.DetrazioniLavoroDipendente));
            CasellaNumerica(riga, 1f, "DETR. CONIUGE", "");
            CasellaNumerica(riga, 1f, "DETR. FIGLI", "");
            CasellaNumerica(riga, 1.2f, "DETR. ALTRI CARICHI", "");
            CasellaNumerica(riga, 1f, "ONERI DEDUCIBILI", "");
            // Ulteriore detrazione cuneo (L. 207/2024): casella distinta dalla detrazione da
            // lavoro dipendente, non vanno sommate insieme (revisione consulente 25/08/2026).
            CasellaNumerica(riga, 1.2f, "ULTERIORE DETRAZ.",
                cedolino.UlterioreDetrazioneCuneo > 0m
                    ? Importo(cedolino.UlterioreDetrazioneCuneo)
                    : "", ultimo: true);
        });

    private static void FasciaProgressivi(IContainer contenitore, ProgressiviAnnui? progressivi)
    {
        // Senza il dato la fascia resta onesta: il valore non è stato calcolato, non è zero.
        if (progressivi is null)
        {
            contenitore.Border(0.8f).BorderColor(Bordo).Background(FondoRiga)
                .PaddingVertical(3).PaddingHorizontal(4).Column(colonna =>
                {
                    colonna.Item().Text("PROGRESSIVI ANNUI").FontSize(4.9f).SemiBold().FontColor(Inchiostro);
                    colonna.Item().Text(
                            "Imponibile previdenziale, imponibile IRPEF, IRPEF pagata e detrazioni progressive: " +
                            "non disponibili in questo documento. Le caselle senza valore non sono azzerate: " +
                            "il dato non è stato calcolato.")
                        .FontSize(5.6f).FontColor(Secondario);
                });
            return;
        }

        // Somme dei mesi elaborati dell'anno fino a questo (compreso), lette dall'archivio.
        contenitore.Border(0.8f).BorderColor(Bordo).Row(riga =>
        {
            CasellaNumerica(riga, 1.4f, "IMPONIB. PREV. PROGR.", Importo(progressivi.ImponibileInps));
            CasellaNumerica(riga, 1.4f, "IMPONIB. IRPEF PROGR.", Importo(progressivi.ImponibileIrpef));
            CasellaNumerica(riga, 1.3f, "IRPEF PAGATA PROGR.", Importo(progressivi.IrpefNetta));
            CasellaNumerica(riga, 1.3f, "DETRAZIONI PROGR.", Importo(progressivi.Detrazioni));
            CasellaNumerica(riga, 1.2f, "MESI ELABORATI", progressivi.MesiCompresi.ToString(It), ultimo: true);
        });
    }

    private static void EtichettaColonna(
        RowDescriptor riga, float peso, string testo, bool destra = false, bool ultimo = false)
    {
        var cella = riga.RelativeItem(peso);
        if (!ultimo) cella = cella.BorderRight(0.4f).BorderColor(BordoTenue);
        var interno = cella.PaddingVertical(2f).PaddingHorizontal(3.5f);
        if (destra) interno = interno.AlignRight();
        interno.Text(testo).FontSize(4.9f).SemiBold().FontColor(Inchiostro);
    }

    private static void ValoreColonna(
        RowDescriptor riga, float peso, string testo,
        bool destra = false, bool evidenza = false, bool ultimo = false)
    {
        var cella = riga.RelativeItem(peso);
        if (!ultimo) cella = cella.BorderRight(0.4f).BorderColor(BordoTenue);
        if (evidenza) cella = cella.Background(Fondo);
        var interno = cella.PaddingVertical(2.2f).PaddingHorizontal(3.5f);
        if (destra) interno = interno.AlignRight();
        var span = interno.Text(testo).FontSize(6.5f).FontColor(Inchiostro);
        if (evidenza) span.SemiBold();
    }

    // ---- Pagamento, firme, legenda ------------------------------------------------------

    private static void PagamentoEFirme(IContainer contenitore, CedolinoPresentazione dati)
    {
        contenitore.Border(0.8f).BorderColor(Bordo).Column(colonna =>
        {
            colonna.Item().Row(riga =>
            {
                Casella(riga, 2.4f, "BANCA ACCREDITO", dati.IbanMascherato);
                Casella(riga, 1.6f, "ANF", EtichettaAnf(dati.Cartellino));
                Casella(riga, 1.4f, "CEDOLINO N.", dati.Emissione.RiferimentoDocumento, ultimo: true);
            });
            colonna.Item().BorderTop(0.5f).BorderColor(BordoTenue).Row(riga =>
            {
                riga.RelativeItem(1.7f).BorderRight(0.5f).BorderColor(BordoTenue)
                    .PaddingVertical(2.5f).PaddingHorizontal(3.5f).Column(legenda =>
                    {
                        legenda.Item().Text("LEGENDA SIGLE").FontSize(4.7f).SemiBold().FontColor(Secondario);
                        legenda.Item().Text(
                                "L lavorato · R riposo · F festività · E ferie · M malattia · " +
                                "I infortunio · P permesso ROL · X ex festività · A assenza non retribuita")
                            .FontSize(5.4f).FontColor(Secondario);
                    });
                SpazioFirma(riga, "FIRMA DEL LAVORATORE");
                SpazioFirma(riga, "FIRMA DEL DATORE DI LAVORO", dati.Emissione.FirmaSiglaTimbro, ultimo: true);
            });
            colonna.Item().BorderTop(0.5f).BorderColor(BordoTenue)
                .PaddingVertical(2).PaddingHorizontal(3.5f).Column(note =>
                {
                    if (!string.IsNullOrWhiteSpace(dati.Emissione.ValidazioneRegistrata))
                        note.Item().Text(dati.Emissione.ValidazioneRegistrata!)
                            .FontSize(5.6f).SemiBold().FontColor(Inchiostro);
                    note.Item().Text(TestoDisponibilitaPresenze(dati.Cartellino))
                        .FontSize(5.4f).FontColor(Secondario);
                    if (dati.Cartellino is null or { Giornate.Count: 0 })
                        note.Item().Text(
                                "CALENDARIO GIORNALIERO NON DISPONIBILE - il cartellino non è stato " +
                                "compilato giorno per giorno.")
                            .FontSize(5.4f).SemiBold().FontColor(Avviso);
                    note.Item().Text(EtichettaAnfEstesa(dati.Cartellino))
                        .FontSize(5.4f).FontColor(Secondario);
                    note.Item().Text(
                            "La firma attesta la sola ricezione del prospetto e non costituisce prova " +
                            "dell'avvenuto pagamento. Data o prova del pagamento: non disponibile nel documento.")
                        .FontSize(5.4f).FontColor(Secondario);
                    note.Item().Text(
                            "* Dato informativo a carico del datore o accantonato: non riduce il netto del dipendente.")
                        .FontSize(5.4f).FontColor(Secondario);
                });
        });
    }

    private static void SpazioFirma(
        RowDescriptor riga, string etichetta, byte[]? immagine = null, bool ultimo = false)
    {
        var cella = riga.RelativeItem();
        if (!ultimo) cella = cella.BorderRight(0.5f).BorderColor(BordoTenue);
        cella.PaddingVertical(2.5f).PaddingHorizontal(3.5f).Column(colonna =>
        {
            colonna.Item().Text(etichetta).FontSize(4.7f).SemiBold().FontColor(Secondario);
            if (immagine is { Length: > 0 })
                colonna.Item().PaddingTop(1).Height(20).Image(immagine).FitArea();
            else
                colonna.Item().PaddingTop(14).BorderBottom(0.5f).BorderColor(BordoTenue);
        });
    }

    private static void Piede(IContainer contenitore, CedolinoPresentazione dati)
    {
        contenitore.BorderTop(0.5f).BorderColor(BordoTenue).PaddingTop(3).Row(riga =>
        {
            riga.RelativeItem().Text(NotaStato(dati.Emissione.Stato))
                .FontSize(5.3f).SemiBold().FontColor(Avviso);
            riga.ConstantItem(70).AlignRight().Text(testo =>
            {
                testo.DefaultTextStyle(stile => stile.FontSize(5.4f).FontColor(Secondario));
                testo.Span("Pagina ");
                testo.CurrentPageNumber();
                testo.Span(" / ");
                testo.TotalPages();
            });
        });
    }

    // ---- Lettura dei dati ---------------------------------------------------------------

    private static decimal? CompetenzaVoce(IReadOnlyList<RigaCedolinoPresentata> righe, string codice) =>
        righe.FirstOrDefault(riga => riga.Codice == codice)?.Competenza;

    private static decimal? BaseVoce(IReadOnlyList<RigaCedolinoPresentata> righe, params string[] codici) =>
        righe.FirstOrDefault(riga => codici.Contains(riga.Codice) && riga.Base is not null)?.Base;

    private static string? GiorniLavorati(Cartellino? cartellino) =>
        cartellino is { Giornate.Count: > 0 } ? cartellino.GiornateLavorate.ToString(It) : null;

    private static string? OreLavorate(Cartellino? cartellino) =>
        cartellino is { Giornate.Count: > 0 }
            ? Numero(cartellino.Giornate.Sum(giorno => giorno.OreOrdinarie))
            : null;

    private static string OreGiornata(GiornataCartellino giorno)
    {
        decimal ore = giorno.OreOrdinarie + giorno.TotaleStraordinario;
        return ore == 0m ? "-" : Numero(ore);
    }

    private static string Sigla(CausaleGiornata causale) => causale switch
    {
        CausaleGiornata.Ferie => "E",
        CausaleGiornata.Malattia => "M",
        CausaleGiornata.Infortunio => "I",
        CausaleGiornata.PermessoRetribuito => "P",
        CausaleGiornata.ExFestivita => "X",
        CausaleGiornata.AssenzaNonRetribuita => "A",
        CausaleGiornata.RiposoSettimanale => "R",
        CausaleGiornata.Festivita => "F",
        _ => "L",
    };

    private static string TestoDisponibilitaPresenze(Cartellino? cartellino) => cartellino switch
    {
        null => "Dati presenze non disponibili in questo documento",
        { Giornate.Count: > 0 } => "Calendario giornaliero riportato a lato",
        // Un cartellino a totali con tutte le variabili a zero non dice nulla su ore e
        // giorni: dichiarare «acquisito» accanto a caselle vuote era contraddittorio.
        _ when SenzaVariabiliMensili(cartellino) =>
            "Nessuna variabile mensile registrata: ore e giorni lavorati non disponibili",
        _ => "Cartellino acquisito a totali mensili",
    };

    private static bool SenzaVariabiliMensili(Cartellino c) =>
        c.OreStraordinario == 0m && c.OreStraordinarioNotturno == 0m
        && c.OreStraordinarioFestivo == 0m && c.GiorniFerie == 0m && c.OrePermessi == 0m
        && c.OreExFestivita == 0m && c.GiorniMalattia == 0m && c.GiorniInfortunio == 0m
        && c.OreAssenzaNonRetribuita == 0m;

    private static string EtichettaAnf(Cartellino? cartellino) => cartellino?.StatoAssegniFamiliari switch
    {
        StatoAssegniFamiliari.NonSpettanti => "Non spettanti nel mese",
        StatoAssegniFamiliari.Spettanti => Importo(cartellino!.ImportoAssegniFamiliari),
        null => "Dato non disponibile",
        _ => "Da verificare",
    };

    private static string EtichettaAnfEstesa(Cartellino? cartellino) => cartellino?.StatoAssegniFamiliari switch
    {
        StatoAssegniFamiliari.NonSpettanti => "ANF / assegni familiari: non spettanti nel mese.",
        StatoAssegniFamiliari.Spettanti =>
            $"ANF / assegni familiari: {Importo(cartellino!.ImportoAssegniFamiliari)}.",
        null => "ANF / assegni familiari: dato non disponibile in questo documento.",
        _ => "ANF / assegni familiari: da verificare prima della chiusura.",
    };

    private static string NotaStato(string? stato) => stato?.Trim().ToLowerInvariant() switch
    {
        "preworkflow" => "BOZZA / NON CERTIFICATO - Ricostruzione da archivio pre-workflow non definitiva.",
        "validata" => "BOZZA / NON CERTIFICATO - Stato workflow: validazione registrata.",
        "chiusa" => "BOZZA / NON CERTIFICATO - Stato workflow: chiusa; consegna e pagamento restano separati.",
        "controllata" => "BOZZA / NON CERTIFICATO - Stato workflow: dati congelati, validazione da registrare.",
        _ => "BOZZA / NON CERTIFICATO - Documento di simulazione da sottoporre al professionista.",
    };

    private static string TestoFiscaleAzienda(IntestazioneCedolino dati)
    {
        var parti = new List<string>();
        if (!string.IsNullOrWhiteSpace(dati.CodiceFiscaleAzienda)) parti.Add($"Cod. fiscale: {dati.CodiceFiscaleAzienda}");
        if (!string.IsNullOrWhiteSpace(dati.PartitaIva)) parti.Add($"P.IVA {dati.PartitaIva}");
        return parti.Count == 0 ? "Dati fiscali azienda non disponibili" : string.Join(" · ", parti);
    }

    // ---- Formattazione ------------------------------------------------------------------

    private static string Importo(decimal valore) => valore.ToString("N2", It);
    private static string Importo(decimal? valore) => valore is decimal v ? Importo(v) : "";
    private static string Importo4(decimal? valore) => valore is decimal v ? v.ToString("N4", It) : "";
    private static string Importo4Breve(decimal? valore) => Importo4(valore);
    private static string ImportoEconomico(decimal? valore) => valore is decimal v && v != 0m
        ? v.ToString("N2", It)
        : "";
    private static string Numero(decimal valore) => valore.ToString("0.##", It);
    private static string Numero(decimal? valore) => valore is decimal v ? Numero(v) : "";
    private static string Valore(string? valore) => string.IsNullOrWhiteSpace(valore) ? "" : valore;
    private static string Capitalizza(string valore) => string.IsNullOrWhiteSpace(valore)
        ? valore
        : char.ToUpper(valore[0], It) + valore[1..];

    private static string UnisciRiga(params string?[] valori)
    {
        string testo = string.Join(" · ", valori.Where(v => !string.IsNullOrWhiteSpace(v)));
        return string.IsNullOrWhiteSpace(testo) ? "" : testo;
    }
}
