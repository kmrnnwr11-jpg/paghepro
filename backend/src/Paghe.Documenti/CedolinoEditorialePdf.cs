using System.Globalization;
using Paghe.Core.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Paghe.Documenti;

/// <summary>
/// Cedolino editoriale vettoriale in tre sezioni: prospetto, dettagli del mese e guida.
/// Le pagine possono aumentare quando le voci sono numerose: nessun dato viene compresso
/// o troncato per rispettare artificialmente il formato standard di tre pagine.
/// </summary>
internal static class CedolinoEditorialePdf
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    private const string Carta = "#F7F5F0";
    private const string Inchiostro = "#17233B";
    private const string Grafite = "#4F5663";
    private const string Filetto = "#C9CDD3";
    private const string Rame = "#B6653C";
    private const string RameChiaro = "#F4E7DE";
    private const string Bianco = "#FFFFFF";

    internal static byte[] Genera(CedolinoPresentazione dati) =>
        Document.Create(documento =>
        {
            documento.Page(pagina => PaginaProspetto(pagina, dati));
            documento.Page(pagina => PaginaDettagli(pagina, dati));
            documento.Page(pagina => PaginaGuida(pagina, dati));
        }).GeneratePdf();

    private static void PaginaProspetto(PageDescriptor pagina, CedolinoPresentazione dati)
    {
        ConfiguraPagina(pagina);
        pagina.Content().Column(colonna =>
        {
            colonna.Spacing(7);
            colonna.Item().Element(c => TestataPrincipale(c, dati));
            colonna.Item().Element(c => NettoPrincipale(c, dati.Cedolino));
            colonna.Item().Element(c => AnagraficaERapporto(c, dati));
            colonna.Item().Element(c => TabellaVoci(c, dati.Righe.Where(ÈRigaEconomica).ToList()));
            colonna.Item().ShowEntire().Element(c => Totali(c, dati.Cedolino));
            colonna.Item().ShowEntire().Element(c => Firme(c, dati));
        });
        pagina.Footer().Element(c => PiedePagina(c, dati));
    }

    private static void PaginaDettagli(PageDescriptor pagina, CedolinoPresentazione dati)
    {
        ConfiguraPagina(pagina);
        pagina.Content().Column(colonna =>
        {
            colonna.Spacing(14);
            colonna.Item().Element(c => TestataCompatta(c, dati, "Dettagli informativi del mese"));
            colonna.Item().Background(RameChiaro).Padding(10).Text(
                    MessaggioAllegato(dati.Emissione))
                .FontSize(8.5f).SemiBold().FontColor(Inchiostro);
            colonna.Item().Element(c => RiepiloghiTecnici(c, dati.Cedolino));
            colonna.Item().Element(c => FerieEPermessi(c, dati));
            colonna.Item().Element(c => VociInformativeSenzaImporto(c, dati.Righe));
            colonna.Item().Element(c => PresenzeDelMese(c, dati.Cartellino));
            colonna.Item().Element(c => VariabiliDelMese(c, dati));
            colonna.Item().Element(c => CalendarioGiornaliero(c, dati.Cartellino));
            colonna.Item().Element(c => StatoDati(c, dati));
        });
        pagina.Footer().Element(c => PiedePagina(c, dati));
    }

    private static void PaginaGuida(PageDescriptor pagina, CedolinoPresentazione dati)
    {
        ConfiguraPagina(pagina);
        pagina.DefaultTextStyle(testo => testo.FontSize(9.5f).FontColor(Grafite));
        pagina.Content().Column(colonna =>
        {
            colonna.Spacing(10);
            colonna.Item().Element(c => TestataCompatta(c, dati, "Come leggere questo cedolino"));
            colonna.Item().Text(
                    "Questa pagina spiega con parole semplici i dati del prospetto. " +
                    "Gli importi di riferimento restano quelli delle pagine precedenti.")
                .FontSize(10).FontColor(Grafite);
            colonna.Item().Element(c => FormulaNetto(c, dati.Cedolino));
            colonna.Item().Element(c => SpiegazioneFerie(c, dati));
            colonna.Item().Element(c => SpiegazioneExtra(c, dati));
            colonna.Item().Element(c => VociCheFormanoIlNetto(c, dati));
            colonna.Item().Element(Glossario);
            colonna.Item().BorderTop(0.7f).BorderColor(Filetto).PaddingTop(10).Text(
                    "Hai un dubbio su una voce? Indica il mese e il codice della voce " +
                    "all’ufficio paghe o al consulente del lavoro.")
                .FontSize(9.5f).SemiBold().FontColor(Inchiostro);
        });
        pagina.Footer().Element(c => PiedePagina(c, dati));
    }

    private static void ConfiguraPagina(PageDescriptor pagina)
    {
        pagina.Size(PageSizes.A4);
        pagina.MarginHorizontal(1.6f, Unit.Centimetre);
        pagina.MarginTop(1.35f, Unit.Centimetre);
        pagina.MarginBottom(1.15f, Unit.Centimetre);
        pagina.PageColor(Carta);
        pagina.DefaultTextStyle(testo => testo.FontFamily("Lato").FontSize(8.5f).FontColor(Inchiostro));
    }

    private static void TestataPrincipale(IContainer contenitore, CedolinoPresentazione dati)
    {
        contenitore.Row(riga =>
        {
            riga.RelativeItem(5.2f).Row(sinistra =>
            {
                if (dati.Intestazione.Logo is { Length: > 0 })
                    sinistra.ConstantItem(54).Height(44).PaddingRight(10)
                        .Image(dati.Intestazione.Logo).FitArea();

                sinistra.RelativeItem().Column(azienda =>
                {
                    azienda.Item().Text(dati.Intestazione.Azienda.ToUpperInvariant())
                        .FontSize(13).SemiBold().FontColor(Inchiostro);
                    azienda.Item().PaddingTop(5).Width(34).BorderBottom(1.4f).BorderColor(Rame);
                    azienda.Item().PaddingTop(7).Text(Valore(dati.Intestazione.AziendaIndirizzo))
                        .FontSize(8).FontColor(Grafite);
                    azienda.Item().Text(Valore(dati.Intestazione.AziendaComune))
                        .FontSize(8).FontColor(Grafite);
                    azienda.Item().PaddingTop(5).Text(TestoFiscaleAzienda(dati.Intestazione))
                        .FontSize(7.5f).FontColor(Grafite);
                    azienda.Item().Text(
                            $"Posizione INPS {Valore(dati.Intestazione.MatricolaInps)}   ·   " +
                            $"PAT INAIL {Valore(dati.Intestazione.PatInail)}")
                        .FontSize(7.5f).FontColor(Grafite);
                });
            });

            riga.RelativeItem(4.8f).AlignRight().Column(documento =>
            {
                documento.Item().AlignRight().Text("PROSPETTO PAGA")
                    .FontSize(21).SemiBold().FontColor(Inchiostro);
                documento.Item().PaddingTop(3).BorderTop(0.7f).BorderColor(Filetto);
                documento.Item().PaddingTop(8).AlignRight().Text(Capitalize(dati.Periodo))
                    .FontSize(17).SemiBold().FontColor(Inchiostro);
                documento.Item().PaddingTop(7).AlignRight().Row(stato =>
                {
                    stato.RelativeItem();
                    stato.AutoItem().Background(Rame).PaddingHorizontal(8).PaddingVertical(4)
                        .Text(EtichettaStato(dati.Emissione.Stato))
                        .FontSize(7.5f).SemiBold().FontColor(Bianco);
                });
            });
        });
    }

    private static void NettoPrincipale(IContainer contenitore, Cedolino cedolino)
    {
        contenitore.BorderTop(0.7f).BorderBottom(0.7f).BorderColor(Filetto)
            .PaddingVertical(14).Row(riga =>
            {
                riga.RelativeItem(5.5f).Column(netto =>
                {
                    netto.Item().Text("Netto da corrispondere")
                        .FontSize(10).SemiBold().FontColor(Inchiostro);
                    netto.Item().Text(Euro(cedolino.NettoInBusta))
                        .FontSize(29).SemiBold().FontColor(Inchiostro);
                });
                riga.RelativeItem(2.25f).BorderLeft(0.6f).BorderColor(Filetto).PaddingLeft(14)
                    .Column(competenze =>
                    {
                        competenze.Item().Text("Competenze").FontSize(8).FontColor(Grafite);
                        competenze.Item().Text(Euro(cedolino.TotaleCompetenze))
                            .FontSize(14).SemiBold().FontColor(Inchiostro);
                    });
                riga.RelativeItem(2.25f).BorderLeft(0.6f).BorderColor(Filetto).PaddingLeft(14)
                    .Column(trattenute =>
                    {
                        trattenute.Item().Text("Trattenute").FontSize(8).FontColor(Grafite);
                        trattenute.Item().Text(Euro(cedolino.TotaleTrattenute))
                            .FontSize(14).SemiBold().FontColor(Inchiostro);
                    });
            });
    }

    private static void AnagraficaERapporto(IContainer contenitore, CedolinoPresentazione dati)
    {
        contenitore.Row(riga =>
        {
            riga.RelativeItem().PaddingRight(18).Column(sinistra =>
            {
                CampoEditoriale(sinistra, "Dipendente", dati.Intestazione.Dipendente, evidenza: true);
                CampoEditoriale(sinistra, "Matricola", dati.Intestazione.Matricola);
                CampoEditoriale(sinistra, "Codice fiscale", dati.Intestazione.CodiceFiscaleDipendente);
                CampoEditoriale(sinistra, "Data assunzione", dati.Intestazione.DataAssunzione);
                CampoEditoriale(sinistra, "Riferimento pagamento", dati.IbanMascherato);
            });
            riga.RelativeItem().BorderLeft(0.6f).BorderColor(Filetto).PaddingLeft(18).Column(destra =>
            {
                CampoEditoriale(destra, "CCNL applicato", dati.Intestazione.Ccnl, evidenza: true);
                CampoEditoriale(destra, "Codice alfanumerico unico CNEL",
                    string.IsNullOrWhiteSpace(dati.Intestazione.CodiceCnel)
                        ? "NON CONFIGURATO - DOCUMENTO NON VALIDABILE"
                        : dati.Intestazione.CodiceCnel);
                CampoEditoriale(destra, "Qualifica e livello",
                    Unisci(dati.Intestazione.Qualifica, dati.Intestazione.Livello));
                CampoEditoriale(destra, "Tipo di contratto", dati.Intestazione.TipoContratto);
                CampoEditoriale(destra, "Orario", dati.Intestazione.Orario);
                CampoEditoriale(destra, "Mensilità previste",
                    dati.Intestazione.Mensilita?.ToString(It));
            });
        });
    }

    private static void TabellaVoci(IContainer contenitore, IReadOnlyList<RigaCedolinoPresentata> righe)
    {
        contenitore.Column(colonna =>
        {
            colonna.Item().Element(c => TitoloSezione(c, "Voci del mese"));
            colonna.Item().Table(tabella =>
            {
                tabella.ColumnsDefinition(colonne =>
                {
                    colonne.RelativeColumn(1.55f);
                    colonne.RelativeColumn(3.35f);
                    colonne.RelativeColumn(1.1f);
                    colonne.RelativeColumn(1.3f);
                    colonne.RelativeColumn(1.35f);
                    colonne.RelativeColumn(1.35f);
                });

                tabella.Header(intestazione =>
                {
                    intestazione.Cell().Element(CellaTitolo).Text("VOCE");
                    intestazione.Cell().Element(CellaTitolo).Text("DESCRIZIONE");
                    intestazione.Cell().Element(CellaTitolo).AlignRight().Text("QUANTITÀ");
                    intestazione.Cell().Element(CellaTitolo).AlignRight().Text("BASE");
                    intestazione.Cell().Element(CellaTitolo).AlignRight().Text("COMPETENZE");
                    intestazione.Cell().Element(CellaTitolo).AlignRight().Text("TRATTENUTE");
                });

                if (righe.Count == 0)
                {
                    tabella.Cell().ColumnSpan(6).PaddingVertical(10)
                        .Text("Nessuna voce retributiva presente nel mese.")
                        .FontColor(Grafite);
                    return;
                }

                foreach (var riga in righe)
                {
                    tabella.Cell().Element(CellaVoce).Text(riga.Codice).FontSize(6.7f);
                    tabella.Cell().Element(CellaVoce).Text(riga.Descrizione);
                    tabella.Cell().Element(CellaVoce).AlignRight().Text(Numero(riga.Quantita));
                    tabella.Cell().Element(CellaVoce).AlignRight().Text(Importo(riga.Base));
                    tabella.Cell().Element(CellaVoce).AlignRight().Text(Importo(riga.Competenza));
                    tabella.Cell().Element(CellaVoce).AlignRight().Text(Importo(riga.Trattenuta));
                }
            });
        });
    }

    private static void Totali(IContainer contenitore, Cedolino cedolino)
    {
        contenitore.BorderTop(1).BorderColor(Inchiostro).PaddingTop(8).Row(riga =>
        {
            riga.RelativeItem(4.8f).Text(
                    "Il netto è il risultato del cedolino; il pagamento è un’operazione separata.")
                .FontSize(7.5f).FontColor(Grafite);
            TotaleEditoriale(riga, "TOTALE COMPETENZE", cedolino.TotaleCompetenze);
            TotaleEditoriale(riga, "TOTALE TRATTENUTE", cedolino.TotaleTrattenute);
            riga.RelativeItem(1.7f).Background(Inchiostro).Padding(8).Column(netto =>
            {
                netto.Item().Text("NETTO").FontSize(7).SemiBold().FontColor(Bianco);
                netto.Item().AlignRight().Text(Euro(cedolino.NettoInBusta))
                    .FontSize(11.5f).SemiBold().FontColor(Bianco);
            });
        });
    }

    private static void Firme(IContainer contenitore, CedolinoPresentazione dati)
    {
        contenitore.PaddingTop(8).Row(riga =>
        {
            riga.RelativeItem().PaddingRight(18).Column(firma =>
            {
                firma.Item().Text(
                        "Firma del lavoratore per ricevuta del prospetto - " +
                        "non costituisce prova dell’avvenuto pagamento")
                    .FontSize(7.5f).FontColor(Grafite);
                firma.Item().PaddingTop(16).BorderBottom(0.6f).BorderColor(Inchiostro);
            });
            riga.RelativeItem().PaddingLeft(18).Column(datore =>
            {
                datore.Item().Text(dati.Emissione.FirmaSiglaTimbro is { Length: > 0 }
                        ? "Firma / sigla / timbro del datore - immagine caricata"
                        : "Firma / sigla / timbro del datore - da apporre")
                    .FontSize(7.5f).FontColor(Grafite);
                if (dati.Emissione.FirmaSiglaTimbro is { Length: > 0 } firma)
                    datore.Item().PaddingTop(4).Height(34).Image(firma).FitArea();
                else
                    datore.Item().PaddingTop(16).BorderBottom(0.6f).BorderColor(Inchiostro);
                if (!string.IsNullOrWhiteSpace(dati.Emissione.RiferimentoDocumento))
                    datore.Item().PaddingTop(3).Text(dati.Emissione.RiferimentoDocumento!)
                        .FontSize(6.5f).FontColor(Grafite);
                if (!string.IsNullOrWhiteSpace(dati.Emissione.ValidazioneRegistrata))
                    datore.Item().PaddingTop(2).Text(dati.Emissione.ValidazioneRegistrata!)
                        .FontSize(6.5f).SemiBold().FontColor(Inchiostro);
            });
        });
    }

    private static void TestataCompatta(
        IContainer contenitore,
        CedolinoPresentazione dati,
        string titolo)
    {
        contenitore.BorderBottom(0.8f).BorderColor(Filetto).PaddingBottom(9).Row(riga =>
        {
            riga.RelativeItem().Column(sinistra =>
            {
                sinistra.Item().Text(dati.Intestazione.Azienda.ToUpperInvariant())
                    .FontSize(8).SemiBold().FontColor(Inchiostro);
                sinistra.Item().Text($"{dati.Intestazione.Dipendente} · {Capitalize(dati.Periodo)}")
                    .FontSize(8).FontColor(Grafite);
            });
            riga.RelativeItem().AlignRight().Column(destra =>
            {
                destra.Item().AlignRight().Text(titolo).FontSize(18).SemiBold().FontColor(Inchiostro);
                destra.Item().AlignRight().Text(EtichettaStato(dati.Emissione.Stato))
                    .FontSize(7.5f).SemiBold().FontColor(Rame);
            });
        });
    }

    private static void RiepiloghiTecnici(IContainer contenitore, Cedolino cedolino)
    {
        contenitore.Column(colonna =>
        {
            colonna.Item().Element(c => TitoloSezione(c, "Previdenza, fisco e dati informativi"));
            colonna.Item().Row(riga =>
            {
                riga.RelativeItem().PaddingRight(12).Column(c =>
                {
                    RigaDato(c, "Imponibile contributivo", Euro(cedolino.ImponibileInps));
                    RigaDato(c, "Contributi dipendente", Euro(cedolino.ContributiDipendente));
                    RigaDato(c, "Contributi a carico del datore (*)", Euro(cedolino.ContributiDatore));
                });
                riga.RelativeItem().BorderLeft(0.6f).BorderColor(Filetto).PaddingHorizontal(12).Column(c =>
                {
                    RigaDato(c, "Imponibile fiscale", Euro(cedolino.ImponibileIrpef));
                    RigaDato(c, "IRPEF lorda", Euro(cedolino.IrpefLorda));
                    RigaDato(c, "Detrazioni lavoro dipendente", Euro(cedolino.DetrazioniLavoroDipendente));
                    RigaDato(c, "IRPEF netta", Euro(cedolino.IrpefNetta));
                    RigaDato(c, "Addizionale regionale", Euro(cedolino.AddizionaleRegionale));
                    RigaDato(c, "Addizionale comunale", Euro(cedolino.AddizionaleComunale));
                });
                riga.RelativeItem().BorderLeft(0.6f).BorderColor(Filetto).PaddingLeft(12).Column(c =>
                {
                    RigaDato(c, "Quota TFR del mese (*)", Euro(cedolino.QuotaTfr));
                    RigaDato(c, "Costo azienda - dato informativo (*)", Euro(cedolino.CostoAzienda));
                    RigaDato(c, "Somme esenti del mese", Euro(cedolino.TotaleEsenti));
                    RigaDato(c, "Premio di risultato agevolato", Euro(cedolino.PremioRisultatoAgevolato));
                    RigaDato(c, "Imposta sostitutiva premio", Euro(cedolino.ImpostaSostitutivaPremio));
                });
            });
            colonna.Item().PaddingTop(7).Text(
                    "(*) Valore informativo a carico del datore o accantonato: non riduce il netto del lavoratore.")
                .FontSize(7.5f).FontColor(Grafite);
        });
    }

    private static void FerieEPermessi(IContainer contenitore, CedolinoPresentazione dati)
    {
        contenitore.Column(colonna =>
        {
            colonna.Item().Element(c => TitoloSezione(c, "Ferie e ROL / ex festività"));
            colonna.Item().Row(riga =>
            {
                riga.RelativeItem().PaddingRight(16).Column(c =>
                {
                    c.Item().Text("FERIE").FontSize(8).SemiBold().FontColor(Inchiostro);
                    RigaDato(c, "Maturate nel calcolo da gennaio", $"{Numero(dati.Cedolino.FerieMaturate)} giorni");
                    RigaDato(c, "Godute da gennaio", $"{Numero(dati.Cedolino.FerieGodute)} giorni");
                    RigaDato(c, "Saldo disponibile calcolato", $"{Numero(dati.Cedolino.FerieResidue)} giorni");
                });
                riga.RelativeItem().BorderLeft(0.6f).BorderColor(Filetto).PaddingLeft(16).Column(c =>
                {
                    c.Item().Text("ROL + EX FESTIVITÀ").FontSize(8).SemiBold().FontColor(Inchiostro);
                    RigaDato(c, "Maturati nel calcolo da gennaio", $"{Numero(dati.Cedolino.PermessiMaturati)} ore");
                    RigaDato(c, "Goduti da gennaio", $"{Numero(dati.Cedolino.PermessiGoduti)} ore");
                    RigaDato(c, "Saldo disponibile calcolato", $"{Numero(dati.Cedolino.PermessiResidui)} ore");
                });
            });
            colonna.Item().PaddingTop(6).Text(
                    "Il saldo include il riporto iniziale del rapporto e gli utilizzi dei mesi precedenti chiusi.")
                .FontSize(7.5f).FontColor(Grafite);
        });
    }

    private static void PresenzeDelMese(IContainer contenitore, Cartellino? cartellino)
    {
        contenitore.Column(colonna =>
        {
            colonna.Item().Element(c => TitoloSezione(c, "Presenze e assenze del mese"));
            if (cartellino is null)
            {
                colonna.Item().Text("Dati presenze non disponibili in questo documento.")
                    .FontSize(8.5f).FontColor(Grafite);
                return;
            }

            colonna.Item().Row(riga =>
            {
                MiniDato(riga, "Giornate lavorate", cartellino.Giornate.Count == 0
                    ? "Dato non disponibile"
                    : cartellino.GiornateLavorate.ToString(It));
                MiniDato(riga, "Ferie", $"{Numero(cartellino.GiorniFerie)} gg");
                MiniDato(riga, "Permessi", $"{Numero(cartellino.OrePermessi)} h");
                MiniDato(riga, "Ex festività", $"{Numero(cartellino.OreExFestivita)} h");
                MiniDato(riga, "Malattia", $"{Numero(cartellino.GiorniMalattia)} gg");
                MiniDato(riga, "Infortunio", $"{Numero(cartellino.GiorniInfortunio)} gg");
                MiniDato(riga, "Assenza non retribuita", $"{Numero(cartellino.OreAssenzaNonRetribuita)} h");
            });
            colonna.Item().PaddingTop(7).Row(riga =>
            {
                MiniDato(riga, "Straordinario feriale", $"{Numero(cartellino.OreStraordinario)} h");
                MiniDato(riga, "Straordinario notturno", $"{Numero(cartellino.OreStraordinarioNotturno)} h");
                MiniDato(riga, "Straordinario festivo", $"{Numero(cartellino.OreStraordinarioFestivo)} h");
                riga.RelativeItem();
            });
        });
    }

    private static void VociInformativeSenzaImporto(
        IContainer contenitore,
        IReadOnlyList<RigaCedolinoPresentata> righe)
    {
        var informative = righe.Where(riga => !ÈRigaEconomica(riga)).ToList();
        if (informative.Count == 0) return;

        contenitore.Column(colonna =>
        {
            colonna.Item().Element(c => TitoloSezione(c, "Voci informative senza importo"));
            colonna.Item().Table(tabella =>
            {
                tabella.ColumnsDefinition(colonne =>
                {
                    colonne.RelativeColumn(1.4f);
                    colonne.RelativeColumn(4.6f);
                    colonne.RelativeColumn(1.5f);
                    colonne.RelativeColumn(1.5f);
                });
                tabella.Header(header =>
                {
                    header.Cell().Element(CellaTitolo).Text("VOCE");
                    header.Cell().Element(CellaTitolo).Text("DESCRIZIONE");
                    header.Cell().Element(CellaTitolo).AlignRight().Text("QUANTITÀ");
                    header.Cell().Element(CellaTitolo).AlignRight().Text("BASE");
                });
                foreach (var riga in informative)
                {
                    tabella.Cell().Element(CellaCalendario).Text(riga.Codice).FontSize(6.7f);
                    tabella.Cell().Element(CellaCalendario).Text(riga.Descrizione);
                    tabella.Cell().Element(CellaCalendario).AlignRight().Text(Numero(riga.Quantita));
                    tabella.Cell().Element(CellaCalendario).AlignRight().Text(Importo(riga.Base));
                }
            });
        });
    }

    private static void VariabiliDelMese(IContainer contenitore, CedolinoPresentazione dati)
    {
        var cartellino = dati.Cartellino;
        if (cartellino is null) return;

        var righe = new List<(string Etichetta, string Valore)>();
        if (cartellino.GiorniTrasfertaItalia > 0m)
            righe.Add(("Trasferte in Italia", $"{Numero(cartellino.GiorniTrasfertaItalia)} giorni"));
        if (cartellino.GiorniTrasfertaEstero > 0m)
            righe.Add(("Trasferte all’estero", $"{Numero(cartellino.GiorniTrasfertaEstero)} giorni"));
        if (cartellino.RimborsiSpeseDocumentate > 0m)
            righe.Add(("Rimborsi documentati", Euro(cartellino.RimborsiSpeseDocumentate)));
        if (cartellino.FringeBenefit > 0m)
            righe.Add(("Fringe benefit", Euro(cartellino.FringeBenefit)));
        if (cartellino.GiorniBuonoPasto > 0)
            righe.Add(("Buoni pasto", $"{cartellino.GiorniBuonoPasto} × {Euro(cartellino.ValoreBuonoPasto)}"));
        if (cartellino.PremioRisultato > 0m)
            righe.Add(("Premio di risultato", Euro(cartellino.PremioRisultato)));

        contenitore.Column(colonna =>
        {
            colonna.Item().Element(c => TitoloSezione(c, "Elementi aggiuntivi del mese"));
            if (righe.Count == 0)
            {
                colonna.Item().Text("Nessun elemento aggiuntivo presente nel mese.")
                    .FontSize(8.5f).FontColor(Grafite);
                return;
            }

            foreach (var (etichetta, valore) in righe)
                colonna.Item().Element(c => RigaDatoOrizzontale(c, etichetta, valore));
        });
    }

    private static void CalendarioGiornaliero(IContainer contenitore, Cartellino? cartellino)
    {
        contenitore.Column(colonna =>
        {
            colonna.Item().Element(c => TitoloSezione(c, "Calendario giornaliero"));
            if (cartellino is null || cartellino.Giornate.Count == 0)
            {
                colonna.Item().Text(
                        "CALENDARIO GIORNALIERO NON DISPONIBILE - QUESTO ALLEGATO NON COSTITUISCE " +
                        "LIBRO UNICO DEL LAVORO.")
                    .FontSize(8.5f).SemiBold().FontColor(Rame);
                return;
            }

            colonna.Item().Table(tabella =>
            {
                tabella.ColumnsDefinition(colonne =>
                {
                    colonne.RelativeColumn(1.15f);
                    colonne.RelativeColumn(2.3f);
                    colonne.RelativeColumn();
                    colonne.RelativeColumn();
                    colonne.RelativeColumn();
                    colonne.RelativeColumn();
                });
                tabella.Header(header =>
                {
                    header.Cell().Element(CellaTitolo).Text("GIORNO");
                    header.Cell().Element(CellaTitolo).Text("CAUSALE");
                    header.Cell().Element(CellaTitolo).AlignRight().Text("ORD.");
                    header.Cell().Element(CellaTitolo).AlignRight().Text("PERM./EX");
                    header.Cell().Element(CellaTitolo).AlignRight().Text("STRAORD.");
                    header.Cell().Element(CellaTitolo).AlignRight().Text("ASS. N/R");
                });
                foreach (var giorno in cartellino.Giornate.OrderBy(g => g.Data))
                {
                    tabella.Cell().Element(CellaCalendario).Text(giorno.Data.ToString("dd/MM", It));
                    tabella.Cell().Element(CellaCalendario).Text(Descrivi(giorno.Causale));
                    tabella.Cell().Element(CellaCalendario).AlignRight().Text(Numero(giorno.OreOrdinarie));
                    tabella.Cell().Element(CellaCalendario).AlignRight()
                        .Text(Numero(giorno.OrePermessi + giorno.OreExFestivita));
                    tabella.Cell().Element(CellaCalendario).AlignRight().Text(Numero(giorno.TotaleStraordinario));
                    tabella.Cell().Element(CellaCalendario).AlignRight().Text(Numero(giorno.OreAssenzaNonRetribuita));
                }
            });
        });
    }

    private static void StatoDati(IContainer contenitore, CedolinoPresentazione dati)
    {
        contenitore.BorderTop(0.7f).BorderColor(Filetto).PaddingTop(9).Column(colonna =>
        {
            colonna.Item().Text("Disponibilità dei dati").FontSize(9).SemiBold().FontColor(Inchiostro);
            colonna.Item().PaddingTop(4).Text("Progressivo anno: dato non disponibile in questo documento.")
                .FontSize(8).FontColor(Grafite);
            colonna.Item().Text(TestoAssegniFamiliari(dati.Cartellino))
                .FontSize(8).FontColor(Grafite);
            colonna.Item().Text("Data o prova del pagamento: dato non disponibile in questo documento.")
                .FontSize(8).FontColor(Grafite);
            colonna.Item().Text(string.IsNullOrWhiteSpace(dati.IbanMascherato)
                    ? "Riferimento per il pagamento: dato non disponibile in questo documento."
                    : $"Riferimento per il pagamento: {dati.IbanMascherato}.")
                .FontSize(8).FontColor(Grafite);
        });
    }

    private static void FormulaNetto(IContainer contenitore, Cedolino cedolino)
    {
        contenitore.Background(Inchiostro).Padding(16).Column(colonna =>
        {
            colonna.Item().Text("Il risultato del mese")
                .FontSize(12).SemiBold().FontColor(Bianco);
            colonna.Item().PaddingTop(8).Row(riga =>
            {
                FormulaParte(riga, "Competenze", cedolino.TotaleCompetenze, "+");
                FormulaParte(riga, "Trattenute", cedolino.TotaleTrattenute, "−");
                FormulaParte(riga, "Netto", cedolino.NettoInBusta, "=");
            });
            colonna.Item().PaddingTop(10).Text(
                    "Il netto è l’importo finale calcolato nel cedolino. La ricevuta del prospetto " +
                    "non prova che il pagamento sia già avvenuto.")
                .FontSize(9).FontColor("#E6E8EC");
        });
    }

    private static void SpiegazioneFerie(IContainer contenitore, CedolinoPresentazione dati)
    {
        contenitore.Column(colonna =>
        {
            colonna.Item().Element(c => TitoloSezione(c, "Ferie e ROL / ex festività"));
            colonna.Item().Row(riga =>
            {
                riga.RelativeItem().PaddingRight(14).Column(c =>
                {
                    c.Item().Text("Ferie").FontSize(10).SemiBold().FontColor(Inchiostro);
                    c.Item().Text($"Maturate nel calcolo da gennaio: {Numero(dati.Cedolino.FerieMaturate)} giorni");
                    c.Item().Text($"Usate da gennaio: {Numero(dati.Cedolino.FerieGodute)} giorni");
                    c.Item().Text($"Saldo disponibile calcolato: {Numero(dati.Cedolino.FerieResidue)} giorni");
                });
                riga.RelativeItem().BorderLeft(0.6f).BorderColor(Filetto).PaddingLeft(14).Column(c =>
                {
                    c.Item().Text("Permessi ROL / ex festività").FontSize(10).SemiBold().FontColor(Inchiostro);
                    c.Item().Text($"Maturati nel calcolo da gennaio: {Numero(dati.Cedolino.PermessiMaturati)} ore");
                    c.Item().Text($"Usati da gennaio: {Numero(dati.Cedolino.PermessiGoduti)} ore");
                    c.Item().Text($"Saldo disponibile calcolato: {Numero(dati.Cedolino.PermessiResidui)} ore");
                });
            });
            colonna.Item().PaddingTop(6).Text(
                    "Il saldo comprende il riporto iniziale inserito nella scheda e i mesi precedenti chiusi.")
                .FontSize(8.5f).FontColor(Grafite);
        });
    }

    private static void SpiegazioneExtra(IContainer contenitore, CedolinoPresentazione dati)
    {
        contenitore.Column(colonna =>
        {
            colonna.Item().Element(c => TitoloSezione(c, "Ore e somme extra"));
            if (dati.Cartellino is null)
            {
                colonna.Item().Text("Dati su ore e somme extra non disponibili in questo documento.");
                return;
            }

            decimal straordinario = dati.Cartellino.OreStraordinario
                + dati.Cartellino.OreStraordinarioNotturno
                + dati.Cartellino.OreStraordinarioFestivo;
            var vociStraordinario = dati.Righe
                .Where(r => r.Competenza.HasValue
                    && r.Descrizione.Contains("straordin", StringComparison.OrdinalIgnoreCase))
                .Sum(r => r.Competenza ?? 0m);

            colonna.Item().Text(straordinario > 0m
                ? $"Straordinario: {Numero(straordinario)} ore · {Euro(vociStraordinario)} riconosciuti nel mese."
                : "Nessuna ora di straordinario presente nel mese.");
            colonna.Item().Text(dati.Cartellino.PremioRisultato > 0m
                ? $"Premio di risultato: {Euro(dati.Cartellino.PremioRisultato)} indicati nel mese."
                : "Nessun premio di risultato presente tra le voci del mese.");
        });
    }

    private static void VociCheFormanoIlNetto(IContainer contenitore, CedolinoPresentazione dati)
    {
        contenitore.Column(colonna =>
        {
            colonna.Item().Element(c => TitoloSezione(c, "Le voci che formano il netto"));
            colonna.Item().Row(riga =>
            {
                riga.RelativeItem().PaddingRight(14).Column(c =>
                {
                    c.Item().Text("Competenze e rettifiche").FontSize(10).SemiBold().FontColor(Inchiostro);
                    foreach (var voce in dati.Righe.Where(r => r.Competenza is not null and not 0m))
                    {
                        decimal valore = voce.Competenza!.Value;
                        string segno = valore < 0m ? "−" : "+";
                        c.Item().PaddingTop(3).Text($"{segno} {voce.Descrizione} - {Euro(Math.Abs(valore))}");
                    }
                });
                riga.RelativeItem().BorderLeft(0.6f).BorderColor(Filetto).PaddingLeft(14).Column(c =>
                {
                    c.Item().Text("Somme trattenute").FontSize(10).SemiBold().FontColor(Inchiostro);
                    foreach (var voce in dati.Righe.Where(r => r.Trattenuta is not null and not 0m))
                        c.Item().PaddingTop(3).Text($"− {voce.Descrizione} - {Euro(voce.Trattenuta!.Value)}");
                });
            });
        });
    }

    private static void Glossario(IContainer contenitore)
    {
        contenitore.Column(colonna =>
        {
            colonna.Item().Element(c => TitoloSezione(c, "Parole utili"));
            colonna.Item().Row(riga =>
            {
                riga.RelativeItem().PaddingRight(14).Column(c =>
                {
                    Definizione(c, "Competenze e rettifiche",
                        "importi che aumentano o diminuiscono la retribuzione del mese.");
                    Definizione(c, "Trattenute", "contributi e imposte sottratti.");
                    Definizione(c, "Storno",
                        "importo che annulla una quota già compresa nella paga per sostituirla con la voce corretta, " +
                        "per esempio per malattia o infortunio.");
                    Definizione(c, "Base di calcolo", "valore usato per un conteggio; non è un pagamento aggiuntivo.");
                    Definizione(c, "Imponibile contributivo", "parte della retribuzione su cui si calcolano i contributi.");
                });
                riga.RelativeItem().BorderLeft(0.6f).BorderColor(Filetto).PaddingLeft(14).Column(c =>
                {
                    Definizione(c, "Imponibile fiscale", "parte della retribuzione su cui si calcolano le imposte.");
                    Definizione(c, "Detrazioni", "riduzioni che abbassano l’imposta.");
                    Definizione(c, "TFR", "quota maturata per il fine rapporto; non entra nel netto del mese.");
                    Definizione(c, "Costo azienda", "dato del datore; non è una somma pagata al dipendente.");
                });
            });
        });
    }

    private static void PiedePagina(IContainer contenitore, CedolinoPresentazione dati)
    {
        contenitore.PaddingTop(7).BorderTop(0.6f).BorderColor(Filetto).Row(riga =>
        {
            riga.RelativeItem().Text(
                    NotaStato(dati.Emissione))
                .FontSize(7).SemiBold().FontColor(Rame);
            riga.ConstantItem(80).AlignRight().Text(testo =>
            {
                testo.DefaultTextStyle(stile => stile.FontSize(7).FontColor(Grafite));
                testo.Span("Pagina ");
                testo.CurrentPageNumber();
                testo.Span(" di ");
                testo.TotalPages();
            });
        });
    }

    private static string EtichettaStato(string? stato) => stato?.Trim().ToLowerInvariant() switch
    {
        "preworkflow" => "ARCHIVIO PRE-WORKFLOW",
        "controllata" => "DATI CONGELATI - DA VALIDARE",
        "validata" => "VALIDAZIONE REGISTRATA",
        "chiusa" => "CHIUSO - VALIDAZIONE REGISTRATA",
        _ => "BOZZA DA VALIDARE",
    };

    private static string NotaStato(ContestoEmissioneCedolino emissione) =>
        emissione.Stato?.Trim().ToLowerInvariant() switch
        {
            "preworkflow" => "RICOSTRUZIONE DA ARCHIVIO PRE-WORKFLOW NON DEFINITIVA - importi archiviati, dati accessori e impaginazione correnti; nessuna attestazione retroattiva.",
            "validata" => "VALIDAZIONE REGISTRATA - Documento da chiudere; non equivale a conservazione a norma.",
            "chiusa" => "CEDOLINO CHIUSO - Ristampa dello snapshot; consegna e pagamento sono operazioni separate.",
            "controllata" => "DATI CONGELATI - Controllo dichiarato dall'operatore; validazione ancora da registrare.",
            _ => "BOZZA DA VALIDARE - Documento non certificato: sottoporre al controllo del professionista.",
        };

    private static string MessaggioAllegato(ContestoEmissioneCedolino emissione)
    {
        const string premessa = "ALLEGATO INFORMATIVO - NON COSTITUISCE LIBRO UNICO DEL LAVORO. ";
        return emissione.Stato?.Trim().ToLowerInvariant() switch
        {
            "preworkflow" => premessa
                + "RICOSTRUZIONE DA ARCHIVIO PRE-WORKFLOW NON DEFINITIVA - importi archiviati, dati accessori e impaginazione correnti; nessuna attestazione retroattiva.",
            "chiusa" => premessa + (string.IsNullOrWhiteSpace(emissione.ValidazioneRegistrata)
                ? "Documento chiuso sullo snapshot validato. Consegna, pagamento e conservazione a norma restano separati."
                : $"{emissione.ValidazioneRegistrata}. Documento chiuso sullo snapshot; consegna, pagamento e conservazione a norma restano separati."),
            "validata" => premessa + "La validazione è stata registrata; il documento non è ancora chiuso.",
            "controllata" => premessa + "I dati sono congelati dopo il controllo dichiarato dall'operatore e attendono la validazione.",
            _ => premessa + "I dati devono essere controllati e validati dal professionista.",
        };
    }

    private static void CampoEditoriale(
        ColumnDescriptor colonna,
        string etichetta,
        string? valore,
        bool evidenza = false)
    {
        colonna.Item().PaddingBottom(4).Row(riga =>
        {
            riga.RelativeItem(1.55f).Text(etichetta.ToUpperInvariant())
                .FontSize(6.8f).SemiBold().FontColor(Grafite);
            riga.RelativeItem(2.45f).PaddingLeft(5).Text(Valore(valore))
                .FontSize(evidenza ? 9.2f : 8.2f).SemiBold().FontColor(Inchiostro);
        });
    }

    private static void RigaDato(ColumnDescriptor colonna, string etichetta, string valore)
    {
        colonna.Item().PaddingVertical(2).Row(riga =>
        {
            riga.RelativeItem(1.7f).Text(etichetta).FontSize(7.6f).FontColor(Grafite);
            riga.RelativeItem().AlignRight().Text(valore).FontSize(8).SemiBold().FontColor(Inchiostro);
        });
    }

    private static void RigaDatoOrizzontale(IContainer contenitore, string etichetta, string valore)
    {
        contenitore.BorderBottom(0.45f).BorderColor(Filetto).PaddingVertical(4).Row(riga =>
        {
            riga.RelativeItem().Text(etichetta).FontSize(8).FontColor(Grafite);
            riga.RelativeItem().AlignRight().Text(valore).FontSize(8.5f).SemiBold().FontColor(Inchiostro);
        });
    }

    private static void MiniDato(RowDescriptor riga, string etichetta, string valore)
    {
        riga.RelativeItem().PaddingRight(8).Column(c =>
        {
            c.Item().Text(etichetta.ToUpperInvariant()).FontSize(6.7f).SemiBold().FontColor(Grafite);
            c.Item().PaddingTop(2).Text(valore).FontSize(8.3f).SemiBold().FontColor(Inchiostro);
        });
    }

    private static void TotaleEditoriale(RowDescriptor riga, string etichetta, decimal valore)
    {
        riga.RelativeItem(1.65f).PaddingHorizontal(8).Column(c =>
        {
            c.Item().Text(etichetta).FontSize(6.6f).SemiBold().FontColor(Grafite);
            c.Item().AlignRight().Text(Euro(valore)).FontSize(10).SemiBold().FontColor(Inchiostro);
        });
    }

    private static void FormulaParte(RowDescriptor riga, string etichetta, decimal valore, string simbolo)
    {
        riga.RelativeItem().Row(c =>
        {
            c.AutoItem().PaddingRight(8).AlignMiddle().Text(simbolo)
                .FontSize(17).SemiBold().FontColor(RameChiaro);
            c.RelativeItem().Column(dato =>
            {
                dato.Item().Text(etichetta).FontSize(8).FontColor("#C8CED8");
                dato.Item().Text(Euro(valore)).FontSize(15).SemiBold().FontColor(Bianco);
            });
        });
    }

    private static void Definizione(ColumnDescriptor colonna, string termine, string spiegazione)
    {
        colonna.Item().PaddingBottom(5).Text(testo =>
        {
            testo.Span($"{termine}: ").SemiBold().FontColor(Inchiostro);
            testo.Span(spiegazione).FontColor(Grafite);
        });
    }

    private static void TitoloSezione(IContainer contenitore, string titolo) =>
        contenitore.PaddingBottom(6).BorderBottom(0.7f).BorderColor(Filetto)
            .Text(titolo).FontSize(10.5f).SemiBold().FontColor(Inchiostro);

    private static IContainer CellaTitolo(IContainer contenitore) =>
        contenitore.BorderBottom(0.8f).BorderColor(Inchiostro).PaddingVertical(5).PaddingHorizontal(3)
            .DefaultTextStyle(testo => testo.FontSize(7).SemiBold().FontColor(Inchiostro));

    private static IContainer CellaVoce(IContainer contenitore) =>
        contenitore.BorderBottom(0.4f).BorderColor(Filetto).PaddingVertical(3).PaddingHorizontal(3)
            .DefaultTextStyle(testo => testo.FontSize(8.1f).FontColor(Inchiostro));

    private static IContainer CellaCalendario(IContainer contenitore) =>
        contenitore.BorderBottom(0.35f).BorderColor(Filetto).PaddingVertical(2.2f).PaddingHorizontal(3)
            .DefaultTextStyle(testo => testo.FontSize(7.5f).FontColor(Inchiostro));

    private static string TestoFiscaleAzienda(IntestazioneCedolino dati)
    {
        var parti = new List<string>();
        if (!string.IsNullOrWhiteSpace(dati.PartitaIva)) parti.Add($"P.IVA {dati.PartitaIva}");
        if (!string.IsNullOrWhiteSpace(dati.CodiceFiscaleAzienda)) parti.Add($"C.F. {dati.CodiceFiscaleAzienda}");
        return parti.Count == 0 ? "P.IVA / C.F. non disponibili" : string.Join("   ·   ", parti);
    }

    private static string Descrivi(CausaleGiornata causale) => causale switch
    {
        CausaleGiornata.Ferie => "Ferie",
        CausaleGiornata.Malattia => "Malattia",
        CausaleGiornata.Infortunio => "Infortunio",
        CausaleGiornata.PermessoRetribuito => "Permesso retribuito",
        CausaleGiornata.ExFestivita => "Ex festività",
        CausaleGiornata.AssenzaNonRetribuita => "Assenza non retribuita",
        CausaleGiornata.RiposoSettimanale => "Riposo settimanale",
        CausaleGiornata.Festivita => "Festività",
        _ => "Lavorata",
    };

    private static string TestoAssegniFamiliari(Cartellino? cartellino) =>
        cartellino?.StatoAssegniFamiliari switch
        {
            StatoAssegniFamiliari.NonSpettanti => "ANF / assegni familiari: non spettanti nel mese.",
            StatoAssegniFamiliari.Spettanti =>
                $"ANF / assegni familiari: {Euro(cartellino.ImportoAssegniFamiliari)}.",
            null => "ANF / assegni familiari: dato non disponibile in questo documento.",
            _ => "ANF / assegni familiari: da verificare prima della chiusura.",
        };

    private static string Euro(decimal valore) => $"€ {valore.ToString("N2", It)}";
    private static bool ÈRigaEconomica(RigaCedolinoPresentata riga) =>
        riga.Competenza is not null and not 0m || riga.Trattenuta is not null and not 0m;
    private static string Importo(decimal? valore) => valore is decimal v ? v.ToString("N2", It) : "";
    private static string Numero(decimal valore) => valore.ToString("0.##", It);
    private static string Numero(decimal? valore) => valore is decimal v ? Numero(v) : "";
    private static string Valore(string? valore) => string.IsNullOrWhiteSpace(valore) ? "-" : valore;
    private static string Cap(string valore) => string.IsNullOrEmpty(valore)
        ? valore
        : char.ToUpper(valore[0], It) + valore[1..];
    private static string Capitalize(string valore) => Cap(valore);
    private static string Unisci(string? primo, string? secondo)
    {
        var valori = new[] { primo, secondo }.Where(v => !string.IsNullOrWhiteSpace(v));
        string unito = string.Join(" · ", valori);
        return string.IsNullOrWhiteSpace(unito) ? "-" : unito;
    }
}
