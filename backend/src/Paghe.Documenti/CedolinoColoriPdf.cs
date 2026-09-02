using System.Globalization;
using Paghe.Core.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Paghe.Documenti;

/// <summary>
/// Cedolino A4 nello stile "Colori": sezioni numerate con testata blu, riquadro di sintesi in
/// alto e calendario del mese su una fascia orizzontale. Stessi dati dello stile classico,
/// impaginati per essere letti anche dal dipendente. Non esegue calcoli normativi e lascia
/// vuote le caselle che il software non alimenta, senza sostituirle con zeri.
/// </summary>
internal sealed class CedolinoColoriPdf
{
    private readonly TemaCedolinoPdf _tema;
    private readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    private string Blu => _tema.Accento;
    private string BluChiaro => _tema.AccentoChiaro;
    private string Testo => _tema.Inchiostro;
    private string Etichetta => _tema.Secondario;
    private string Bordo => _tema.BordoTenue;
    private string Fondo => _tema.Fondo;
    private string FondoGiorno => _tema.FondoAlternato;
    private string Avviso => _tema.Avviso;
    private string Bianco => _tema.Bianco;

    private CedolinoColoriPdf(TemaCedolinoPdf tema) => _tema = tema;

    internal static byte[] Genera(CedolinoPresentazione dati, TemaCedolinoPdf tema) =>
        new CedolinoColoriPdf(tema).GeneraInterno(dati);

    private byte[] GeneraInterno(CedolinoPresentazione dati) =>
        Document.Create(documento => documento.Page(pagina => Pagina(pagina, dati))).GeneratePdf();

    private void Pagina(PageDescriptor pagina, CedolinoPresentazione dati)
    {
        pagina.Size(PageSizes.A4);
        pagina.MarginHorizontal(0.9f, Unit.Centimetre);
        pagina.MarginTop(0.65f, Unit.Centimetre);
        pagina.MarginBottom(0.65f, Unit.Centimetre);
        pagina.PageColor(Bianco);
        pagina.DefaultTextStyle(stile => stile.FontFamily(dati.FontFamily).FontSize(6.8f)
            // IBM Plex ha metriche verticali più generose: una line-height controllata evita
            // che il solo blocco firme scivoli su un secondo foglio senza ridurre i caratteri.
            .LineHeight(string.Equals(dati.FontFamily, "IBM Plex Sans", StringComparison.Ordinal)
                ? 0.9f : 1f)
            .FontColor(Testo));

        pagina.Content().Column(colonna =>
        {
            colonna.Spacing(2);
            colonna.Item().Element(c => Testata(c, dati));
            colonna.Item().Element(c => DatiLavoratore(c, dati.Intestazione));
            colonna.Item().Element(c => InBreve(c, dati));
            colonna.Item().Element(c => ElementiRetribuzione(c, dati));
            colonna.Item().Element(c => SviluppoVoci(c, dati));
            colonna.Item().ShowEntire().Element(c => TreQuadri(c, dati.Cedolino, dati.Progressivi));
            colonna.Item().ShowEntire().Element(c => CostiStimati(c, dati.Cedolino));
            if (dati.RigheFigurativeRilevanti.Count > 0)
                colonna.Item().ShowEntire().Element(c => DettaglioFigurativo(c, dati.RigheFigurativeRilevanti));
            colonna.Item().ShowEntire().Element(c => FeriePermessi(c, dati.Cedolino));
            colonna.Item().ShowEntire().Element(c => Calendario(c, dati.Cartellino));
            colonna.Item().ShowEntire().Element(c => BarraNetto(c, dati.Cedolino));
            colonna.Item().ShowEntire().Element(c => Pagamento(c, dati));
        });
        pagina.Footer().Element(c => Piede(c, dati));
    }

    // ---- Testata ------------------------------------------------------------------------

    private void Testata(IContainer contenitore, CedolinoPresentazione dati)
    {
        var intestazione = dati.Intestazione;
        contenitore.Column(testata =>
        {
            testata.Item().Background(Avviso).PaddingVertical(3).PaddingHorizontal(6).Row(avviso =>
            {
                avviso.RelativeItem().Text(CedolinoPdfPresentazione.TitoloSimulazione)
                    .FontSize(9).SemiBold().FontColor(Bianco);
                avviso.AutoItem().PaddingLeft(8).Text(CedolinoPdfPresentazione.AvvisoNonCertificato)
                    .FontSize(9).SemiBold().FontColor(Bianco);
            });

            testata.Item().PaddingTop(3).Border(0.8f).BorderColor(Blu).Row(riga =>
            {
                riga.RelativeItem().BorderRight(0.8f).BorderColor(Blu)
                    .PaddingVertical(4).PaddingHorizontal(5).Column(sinistra =>
                    {
                        sinistra.Item().Row(testa =>
                        {
                            if (intestazione.Logo is { Length: > 0 } logo)
                                testa.ConstantItem(52).Height(34).PaddingRight(7).Image(logo).FitArea();
                            testa.RelativeItem().Column(nome =>
                            {
                                nome.Item().Text(Valore(intestazione.Azienda).ToUpperInvariant())
                                    .FontSize(11.5f).SemiBold().FontColor(Testo);
                                nome.Item().PaddingTop(1.5f).Text(UnisciRiga(
                                        intestazione.AziendaIndirizzo, intestazione.AziendaComune))
                                    .FontSize(6.2f).FontColor(Etichetta);
                            });
                        });
                        sinistra.Item().PaddingTop(3.5f).Row(dettagli =>
                        {
                            MiniCampo(dettagli, "Codice fiscale", intestazione.CodiceFiscaleAzienda);
                            MiniCampo(dettagli, "Partita IVA", intestazione.PartitaIva);
                            MiniCampo(dettagli, "Matricola INPS", intestazione.MatricolaInps);
                            MiniCampo(dettagli, "P.A.T. INAIL", intestazione.PatInail);
                        });
                    });

                riga.ConstantItem(175).PaddingVertical(4).PaddingHorizontal(5).Column(destra =>
                {
                    destra.Item().Text("PROSPETTO RETRIBUTIVO MENSILE")
                        .FontSize(7.2f).SemiBold().FontColor(Testo).LetterSpacing(0.08f);
                    destra.Item().PaddingTop(2).BorderTop(1.4f).BorderColor(Blu);
                    destra.Item().PaddingTop(3).Row(box =>
                    {
                        box.RelativeItem().Border(0.7f).BorderColor(Blu).PaddingVertical(2).PaddingHorizontal(4)
                            .Column(periodo =>
                            {
                                periodo.Item().Text("PERIODO DI PAGA")
                                    .FontSize(5.4f).SemiBold().FontColor(Etichetta);
                                periodo.Item().Text(Capitalizza(dati.Periodo).ToUpperInvariant())
                                    .FontSize(10.5f).SemiBold().FontColor(Testo);
                            });
                        box.ConstantItem(4);
                        box.ConstantItem(62).Border(0.7f).BorderColor(Blu).PaddingVertical(2).PaddingHorizontal(4)
                            .Column(numero =>
                            {
                                numero.Item().Text("CEDOLINO N.")
                                    .FontSize(5.4f).SemiBold().FontColor(Etichetta);
                                numero.Item().Text(Valore(dati.Emissione.RiferimentoDocumento))
                                    .FontSize(7.5f).SemiBold().FontColor(Testo);
                            });
                    });
                });
            });
        });
    }

    private void MiniCampo(RowDescriptor riga, string etichetta, string? valore) =>
        riga.RelativeItem().Column(colonna =>
        {
            colonna.Item().Text(etichetta.ToUpperInvariant())
                .FontSize(5.4f).SemiBold().FontColor(Etichetta);
            colonna.Item().Text(Valore(valore)).FontSize(6.6f).FontColor(Testo);
        });

    // ---- Sezioni ------------------------------------------------------------------------

    private void TitoloSezione(IContainer contenitore, string numero, string titolo) =>
        contenitore.Background(Blu).PaddingVertical(2.5f).PaddingHorizontal(5).Text(testo =>
        {
            testo.Span($"{numero}   ").FontSize(6f).FontColor(BluChiaro);
            testo.Span(titolo.ToUpperInvariant()).FontSize(6.2f).SemiBold().FontColor(Bianco)
                .LetterSpacing(0.1f);
        });

    private void DatiLavoratore(IContainer contenitore, IntestazioneCedolino intestazione)
    {
        var campi = new (string Etichetta, string? Valore, bool Evidenza)[]
        {
            ("Cognome e nome", intestazione.Dipendente, true),
            ("Codice fiscale", intestazione.CodiceFiscaleDipendente, false),
            ("Matricola", intestazione.Matricola, false),
            ("Data assunzione", intestazione.DataAssunzione, false),
            ("Data cessazione", intestazione.DataCessazione, false),
            ("Data di nascita", intestazione.DataNascita, false),
            ("Comune di residenza", intestazione.ComuneResidenza, false),
            ("CCNL applicato", intestazione.Ccnl, false),
            ("Codice CNEL", string.IsNullOrWhiteSpace(intestazione.CodiceCnel)
                ? "NON CONFIGURATO" : intestazione.CodiceCnel, false),
            ("Qualifica", intestazione.Qualifica, false),
            ("Livello", intestazione.Livello, false),
            ("Tipo contratto", intestazione.TipoContratto, false),
            ("Orario", intestazione.Orario, false),
            ("Mensilità", intestazione.Mensilita?.ToString(It), false),
        };

        contenitore.Border(0.8f).BorderColor(Blu).Column(colonna =>
        {
            colonna.Item().Element(c => TitoloSezione(c, "02", "Dati del lavoratore"));
            for (int inizio = 0; inizio < campi.Length; inizio += 5)
            {
                var fascia = campi.Skip(inizio).Take(5).ToList();
                var cella = colonna.Item();
                if (inizio > 0) cella = cella.BorderTop(0.5f).BorderColor(Bordo);
                cella.Row(riga =>
                {
                    for (int i = 0; i < 5; i++)
                    {
                        var box = riga.RelativeItem();
                        if (i < 4) box = box.BorderRight(0.5f).BorderColor(Bordo);
                        if (i >= fascia.Count) { box.Height(1); continue; }
                        var campo = fascia[i];
                        box.PaddingVertical(2.5f).PaddingHorizontal(5).Column(dato =>
                        {
                            dato.Item().Text(campo.Etichetta.ToUpperInvariant())
                                .FontSize(5.4f).SemiBold().FontColor(Etichetta);
                            dato.Item().Text(Valore(campo.Valore))
                                .FontSize(7.4f).SemiBold().FontColor(Testo);
                        });
                    }
                });
            }
        });
    }

    private void InBreve(IContainer contenitore, CedolinoPresentazione dati)
    {
        var cedolino = dati.Cedolino;
        var voci = new (string Titolo, string Valore, string Nota)[]
        {
            ("Netto simulato", Importo(cedolino.NettoInBusta), "non definitivo"),
            ("Ferie che ti restano", Numero(cedolino.FerieResidue), "giorni"),
            ("ROL residui", Numero(cedolino.RolResidui), "ore"),
            ("Giorni lavorati", GiorniLavorati(dati.Cartellino) ?? "n.d.",
                OreLavorate(dati.Cartellino) is { } ore ? $"{ore} ore ordinarie" : "calendario non compilato"),
            ("Trattenute totali", Importo(cedolino.TotaleTrattenute), "contributi, IRPEF e addizionali"),
        };

        contenitore.Border(0.8f).BorderColor(Blu).Background(Fondo).Row(riga =>
        {
            for (int i = 0; i < voci.Length; i++)
            {
                var box = riga.RelativeItem();
                if (i < voci.Length - 1) box = box.BorderRight(0.5f).BorderColor(Bordo);
                var voce = voci[i];
                box.PaddingVertical(3.5f).PaddingHorizontal(5).Column(colonna =>
                {
                    colonna.Item().Text(voce.Titolo.ToUpperInvariant())
                        .FontSize(5.4f).SemiBold().FontColor(Etichetta);
                    colonna.Item().PaddingTop(1).Text(voce.Valore)
                        .FontSize(11.5f).SemiBold().FontColor(Blu);
                    colonna.Item().Text(voce.Nota).FontSize(5.6f).FontColor(Etichetta);
                });
            }
        });
    }

    private void ElementiRetribuzione(IContainer contenitore, CedolinoPresentazione dati)
    {
        var righe = dati.Righe;
        decimal? pagaBase = CompetenzaVoce(righe, "PAGA_BASE");
        decimal? contingenza = CompetenzaVoce(righe, "CONTINGENZA");
        decimal? terzoElemento = CompetenzaVoce(righe, "TERZO_EL");
        decimal? edr = CompetenzaVoce(righe, "EDR");
        decimal? superminimo = CompetenzaVoce(righe, "SUPERMIN");
        decimal? scatti = CompetenzaVoce(righe, "SCATTI");
        var componenti = new (string Etichetta, string Valore, bool Evidenza)[]
        {
            ("Paga base", Importo(pagaBase), false),
            ("Contingenza", Importo(contingenza), false),
            ("Terzo elem.", Importo(terzoElemento), false),
            ("E.D.R.", Importo(edr), false),
            ("Superminimo", Importo(superminimo), false),
            ("Scatti anz.", Importo(scatti), false),
        };
        var tariffe = new (string Etichetta, string Valore, bool Evidenza)[]
        {
            ("Retribuzione fissa", Importo4(dati.Cedolino.RetribuzioneFissa), true),
            ("Tariffa oraria", Importo4(dati.Cedolino.TariffaOraria), false),
            ("Tariffa giornaliera", Importo4(dati.Cedolino.TariffaGiornaliera), false),
        };

        contenitore.Border(0.8f).BorderColor(Blu).Column(colonna =>
        {
            colonna.Item().Element(c => TitoloSezione(c, "03", "Elementi della retribuzione"));
            colonna.Item().Row(riga =>
            {
                for (int i = 0; i < componenti.Length; i++)
                {
                    var box = riga.RelativeItem();
                    if (i < componenti.Length - 1) box = box.BorderRight(0.5f).BorderColor(Bordo);
                    if (componenti[i].Evidenza) box = box.Background(Fondo);
                    box.PaddingVertical(2.5f).PaddingHorizontal(5).Column(dato =>
                    {
                        dato.Item().Text(componenti[i].Etichetta.ToUpperInvariant())
                            .FontSize(5.4f).SemiBold().FontColor(Etichetta);
                        dato.Item().AlignRight().Text(componenti[i].Valore)
                            .FontSize(7.6f).SemiBold().FontColor(Testo);
                    });
                }
            });
            colonna.Item().BorderTop(0.5f).BorderColor(Bordo).Row(riga =>
            {
                for (int i = 0; i < tariffe.Length; i++)
                {
                    var box = riga.RelativeItem();
                    if (i < tariffe.Length - 1) box = box.BorderRight(0.5f).BorderColor(Bordo);
                    if (tariffe[i].Evidenza) box = box.Background(Fondo);
                    box.PaddingVertical(2.5f).PaddingHorizontal(5).Row(dato =>
                    {
                        dato.RelativeItem().Text(tariffe[i].Etichetta.ToUpperInvariant())
                            .FontSize(5.8f).SemiBold().FontColor(Etichetta);
                        dato.AutoItem().PaddingLeft(6).Text(tariffe[i].Valore)
                            .FontSize(8).SemiBold().FontColor(Testo);
                    });
                }
            });
        });
    }

    private void SviluppoVoci(IContainer contenitore, CedolinoPresentazione dati)
    {
        var righe = dati.Righe;
        contenitore.Border(0.8f).BorderColor(Blu).Column(colonna =>
        {
            colonna.Item().Element(c => TitoloSezione(c, "04", "Sviluppo delle voci"));
            colonna.Item().Table(tabella =>
            {
                tabella.ColumnsDefinition(colonne =>
                {
                    colonne.RelativeColumn(1.4f);
                    colonne.RelativeColumn(4.6f);
                    colonne.RelativeColumn(1.1f);
                    colonne.RelativeColumn(1.3f);
                    colonne.RelativeColumn(1.5f);
                    colonne.RelativeColumn(1.5f);
                });
                tabella.Header(header =>
                {
                    Intestazione(header, "Voce");
                    Intestazione(header, "Descrizione");
                    Intestazione(header, "Ore / GG", destra: true);
                    Intestazione(header, "Base", destra: true);
                    Intestazione(header, "Competenze", destra: true);
                    Intestazione(header, "Trattenute", destra: true, ultimo: true);
                });

                if (righe.Count == 0)
                    tabella.Cell().ColumnSpan(6).BorderBottom(0.4f).BorderColor(Bordo).Padding(8)
                        .Text("Nessuna voce presente nel mese.").FontColor(Etichetta);

                for (int indice = 0; indice < righe.Count; indice++)
                {
                    var voce = righe[indice];
                    string sfondo = indice % 2 == 1 ? Fondo : Bianco;
                    Cella(tabella, voce.Codice, sfondo, piccolo: true);
                    Cella(tabella, voce.Descrizione, sfondo);
                    Cella(tabella, Numero(voce.Quantita), sfondo, destra: true);
                    Cella(tabella, Importo4Breve(voce.Base), sfondo, destra: true);
                    Cella(tabella, ImportoEconomico(voce.Competenza), sfondo, destra: true,
                        colore: voce.Competenza < 0m ? Avviso : Testo);
                    Cella(tabella, ImportoEconomico(voce.Trattenuta), sfondo, destra: true, ultimo: true);
                }

                tabella.Cell().ColumnSpan(4).BorderTop(1.1f).BorderColor(Blu)
                    .PaddingVertical(3).PaddingHorizontal(5)
                    .Text("TOTALI DI COLONNA").FontSize(5.6f).SemiBold().FontColor(Blu);
                tabella.Cell().BorderTop(1.1f).BorderColor(Blu).PaddingVertical(3).PaddingHorizontal(5)
                    .AlignRight().Text(Importo(dati.Cedolino.TotaleCompetenze))
                    .FontSize(7.6f).SemiBold().FontColor(Testo);
                tabella.Cell().BorderTop(1.1f).BorderColor(Blu).PaddingVertical(3).PaddingHorizontal(5)
                    .AlignRight().Text(Importo(dati.Cedolino.TotaleTrattenute))
                    .FontSize(7.6f).SemiBold().FontColor(Testo);
            });
        });
    }

    private void Intestazione(
        TableCellDescriptor header, string testo, bool destra = false, bool ultimo = false)
    {
        var cella = header.Cell().BorderBottom(1.1f).BorderColor(Blu);
        if (!ultimo) cella = cella.BorderRight(0.5f).BorderColor(Bordo);
        var interno = cella.PaddingVertical(2.5f).PaddingHorizontal(5);
        if (destra) interno = interno.AlignRight();
        interno.Text(testo.ToUpperInvariant()).FontSize(5.4f).SemiBold().FontColor(Blu);
    }

    private void Cella(
        TableDescriptor tabella, string testo, string sfondo,
        bool destra = false, bool piccolo = false, bool ultimo = false, string? colore = null)
    {
        var cella = tabella.Cell().Background(sfondo).BorderBottom(0.4f).BorderColor(Bordo);
        if (!ultimo) cella = cella.BorderRight(0.4f).BorderColor(Bordo);
        var interno = cella.PaddingVertical(2f).PaddingHorizontal(5);
        if (destra) interno = interno.AlignRight();
        interno.Text(testo).FontSize(piccolo ? 6f : 6.7f).FontColor(colore ?? Testo);
    }

    private void TreQuadri(
        IContainer contenitore, Cedolino cedolino, ProgressiviAnnui? progressivi) =>
        contenitore.Row(riga =>
        {
            riga.RelativeItem().Element(c => Quadro(c, "05", "Dati previdenziali INPS", new[]
            {
                ("Imponibile previdenziale", Importo(cedolino.ImponibileInps), false),
                ("Contributi c/dipendente", Importo(cedolino.ContributiDipendente), true),
                ("Contributi c/ditta *", Importo(cedolino.ContributiDatore), false),
                ("Somme esenti", Importo(cedolino.TotaleEsenti), false),
                // Vuota quando i progressivi non sono stati calcolati: non è uno zero.
                ("Imponibile progressivo anno", progressivi is { } prev
                    ? Importo(prev.ImponibileInps)
                    : "", false),
            }));
            riga.ConstantItem(4);
            riga.RelativeItem().Element(c => Quadro(c, "06", "Dati fiscali", new[]
            {
                ("Imponibile IRPEF", Importo(cedolino.ImponibileIrpef), false),
                ("IRPEF lorda", Importo(cedolino.IrpefLorda), false),
                ("Detrazione lavoro dip.", Importo(cedolino.DetrazioniLavoroDipendente), false),
                ("Ulteriore detraz. cuneo", Importo(cedolino.UlterioreDetrazioneCuneo), false),
                ("IRPEF netta trattenuta", Importo(cedolino.IrpefNetta), true),
                ("Addizionali reg. + com.", Importo(cedolino.AddizionaleRegionale + cedolino.AddizionaleComunale), false),
            }));
            riga.ConstantItem(4);
            riga.RelativeItem().Element(c => Quadro(c, "07", "TFR e progressivi", new[]
            {
                ("Quota TFR del mese *", Importo(cedolino.QuotaTfr), false),
                ("Premio agevolato", Importo(cedolino.PremioRisultatoAgevolato), false),
                ("Imposta sostitutiva", Importo(cedolino.ImpostaSostitutivaPremio), false),
                // I tre layout espongono lo stesso patrimonio informativo: la variante grafica
                // non deve nascondere dati utili al controllo di conguagli e detrazioni.
                ("Mesi compresi", progressivi is { } mesi
                    ? mesi.MesiCompresi.ToString("N0", It)
                    : "", false),
                ("Imponibile IRPEF progr.", progressivi is { } fiscale
                    ? Importo(fiscale.ImponibileIrpef)
                    : "", false),
                ("IRPEF netta progr.", progressivi is { } imposta
                    ? Importo(imposta.IrpefNetta)
                    : "", true),
                ("Detrazioni progr.", progressivi is { } detrazioni
                    ? Importo(detrazioni.Detrazioni)
                    : "", false),
            }));
        });

    private void CostiStimati(IContainer contenitore, Cedolino cedolino)
    {
        contenitore.Border(0.8f).BorderColor(Blu).Column(colonna =>
        {
            colonna.Item().Element(c => TitoloSezione(c, "07A", "Costi parziali stimati"));
            colonna.Item().Row(riga =>
            {
                RigaCosto(riga, "COSTO PARZIALE STIMATO DEL MESE", Importo(cedolino.CostoAzienda));
                RigaCosto(riga, "COSTO MEDIO PARZIALE STIMATO ANNUO/12",
                    cedolino.CostoMedioAnnualizzato > 0m
                        ? Importo(cedolino.CostoMedioAnnualizzato)
                        : "", ultimo: true);
            });
        });
    }

    private void RigaCosto(
        RowDescriptor riga, string etichetta, string valore, bool ultimo = false)
    {
        var cella = riga.RelativeItem();
        if (!ultimo) cella = cella.BorderRight(0.5f).BorderColor(Bordo);
        cella.PaddingVertical(3).PaddingHorizontal(6).Row(dato =>
        {
            dato.RelativeItem().Text(etichetta).FontSize(5.8f).SemiBold().FontColor(Etichetta);
            dato.AutoItem().PaddingLeft(8).Text(valore).FontSize(7.2f).SemiBold().FontColor(Testo);
        });
    }

    private void Quadro(
        IContainer contenitore, string numero, string titolo,
        IReadOnlyList<(string Etichetta, string Valore, bool Evidenza)> valori) =>
        contenitore.Border(0.8f).BorderColor(Blu).Column(colonna =>
        {
            colonna.Item().Element(c => TitoloSezione(c, numero, titolo));
            for (int i = 0; i < valori.Count; i++)
            {
                var cella = colonna.Item();
                if (i % 2 == 1) cella = cella.Background(Fondo);
                cella.BorderBottom(0.4f).BorderColor(Bordo)
                    .PaddingVertical(1.8f).PaddingHorizontal(5).Row(riga =>
                    {
                        var testo = riga.RelativeItem().Text(valori[i].Etichetta)
                            .FontSize(6.4f).FontColor(valori[i].Evidenza ? Testo : Etichetta);
                        if (valori[i].Evidenza) testo.SemiBold();
                        var importo = riga.AutoItem().PaddingLeft(6).Text(valori[i].Valore)
                            .FontSize(6.8f).FontColor(Testo);
                        if (valori[i].Evidenza) importo.SemiBold();
                    });
            }
        });

    private void FeriePermessi(IContainer contenitore, Cedolino cedolino) =>
        contenitore.Border(0.8f).BorderColor(Blu).Column(colonna =>
        {
            colonna.Item().Element(c => TitoloSezione(c, "08", "Ferie e permessi — quanto ti resta"));
            colonna.Item().BorderBottom(1.1f).BorderColor(Blu).Row(riga =>
            {
                TestataRateo(riga, 2.4f, "Istituto");
                TestataRateo(riga, 1f, "Residuo anno prec.", destra: true);
                TestataRateo(riga, 1f, "Maturato quest'anno", destra: true);
                TestataRateo(riga, 1f, "Già usato", destra: true);
                TestataRateo(riga, 1f, "Ti restano", destra: true, ultimo: true);
            });
            RigaRateo(colonna, "Ferie (giorni)", saldoPrecedente: null,
                cedolino.FerieMaturate, cedolino.FerieGodute, cedolino.FerieResidue, sfondo: false);
            RigaRateo(colonna, "ROL (ore)", cedolino.RolSaldoPrecedente,
                cedolino.RolMaturati, cedolino.RolGoduti, cedolino.RolResidui, sfondo: true);
            RigaRateo(colonna, "Ex festività (ore)", cedolino.ExFestivitaSaldoPrecedente,
                cedolino.ExFestivitaMaturate, cedolino.ExFestivitaGodute,
                cedolino.ExFestivitaResidue, sfondo: false);
            colonna.Item().Background(Fondo).PaddingVertical(2).PaddingHorizontal(5)
                .Text("Saldo precedente ferie: dato non disponibile nel modello del cedolino.")
                .FontSize(5.4f).FontColor(Etichetta);
        });

    private void TestataRateo(
        RowDescriptor riga, float peso, string testo, bool destra = false, bool ultimo = false)
    {
        var cella = riga.RelativeItem(peso);
        if (!ultimo) cella = cella.BorderRight(0.5f).BorderColor(Bordo);
        var interno = cella.PaddingVertical(2f).PaddingHorizontal(5);
        if (destra) interno = interno.AlignRight();
        interno.Text(testo.ToUpperInvariant()).FontSize(5.4f).SemiBold().FontColor(Blu);
    }

    private void RigaRateo(
        ColumnDescriptor colonna, string istituto,
        decimal? saldoPrecedente, decimal maturato, decimal goduto, decimal residuo, bool sfondo)
    {
        var cella = colonna.Item();
        if (sfondo) cella = cella.Background(Fondo);
        cella.BorderBottom(0.4f).BorderColor(Bordo).Row(riga =>
        {
            ValoreRateo(riga, 2.4f, istituto);
            ValoreRateo(riga, 1f, Numero(saldoPrecedente), destra: true);
            ValoreRateo(riga, 1f, Numero(maturato), destra: true);
            ValoreRateo(riga, 1f, Numero(goduto), destra: true);
            ValoreRateo(riga, 1f, Numero(residuo), destra: true, evidenza: true, ultimo: true);
        });
    }

    private void DettaglioFigurativo(
        IContainer contenitore, IReadOnlyList<RigaFigurativaPresentata> righe)
    {
        contenitore.Border(0.8f).BorderColor(Blu).Column(colonna =>
        {
            colonna.Item().Element(c => TitoloSezione(
                c, "07B", "Dettaglio figurativo TFR, fondi e prestazioni recuperabili"));
            colonna.Item().Table(tabella =>
            {
                tabella.ColumnsDefinition(colonne =>
                {
                    colonne.RelativeColumn(2.2f);
                    colonne.RelativeColumn(4.1f);
                    colonne.RelativeColumn(1.4f);
                    colonne.RelativeColumn(1.4f);
                });
                tabella.Header(header =>
                {
                    Intestazione(header, "Voce");
                    Intestazione(header, "Descrizione");
                    Intestazione(header, "Base", destra: true);
                    Intestazione(header, "Importo", destra: true, ultimo: true);
                });
                for (int indice = 0; indice < righe.Count; indice++)
                {
                    var voce = righe[indice];
                    string sfondo = indice % 2 == 1 ? Fondo : Bianco;
                    Cella(tabella, voce.Codice, sfondo, piccolo: true);
                    Cella(tabella, voce.Descrizione, sfondo);
                    Cella(tabella, Importo4(voce.Base), sfondo, destra: true);
                    Cella(tabella, Importo(voce.Importo), sfondo, destra: true, ultimo: true);
                }
            });
        });
    }

    private void ValoreRateo(
        RowDescriptor riga, float peso, string testo,
        bool destra = false, bool evidenza = false, bool ultimo = false)
    {
        var cella = riga.RelativeItem(peso);
        if (!ultimo) cella = cella.BorderRight(0.4f).BorderColor(Bordo);
        var interno = cella.PaddingVertical(2.2f).PaddingHorizontal(5);
        if (destra) interno = interno.AlignRight();
        var span = interno.Text(testo).FontSize(6.7f).FontColor(Testo);
        if (evidenza) span.SemiBold();
    }

    private void Calendario(IContainer contenitore, Cartellino? cartellino)
    {
        bool disponibile = cartellino is { Giornate.Count: > 0 };
        contenitore.Border(0.8f).BorderColor(Blu).Column(colonna =>
        {
            colonna.Item().Background(Blu).PaddingVertical(2.5f).PaddingHorizontal(5).Row(titolo =>
            {
                titolo.RelativeItem().Text(testo =>
                {
                    testo.Span("09   ").FontSize(6f).FontColor(BluChiaro);
                    testo.Span("CALENDARIO PRESENZE").FontSize(6.2f).SemiBold().FontColor(Bianco);
                });
                titolo.AutoItem().AlignMiddle().Text(
                        "L lavorato · R riposo · F festività · E ferie · M malattia · " +
                        "I infortunio · P permesso ROL · X ex festività · A assenza n.r.")
                    .FontSize(5.2f).FontColor(BluChiaro);
            });

            if (!disponibile)
            {
                colonna.Item().Background(Fondo).Padding(6)
                    .Text("CALENDARIO GIORNALIERO NON DISPONIBILE - il cartellino non è stato " +
                          "compilato giorno per giorno.")
                    .FontSize(6.2f).SemiBold().FontColor(Avviso);
                return;
            }

            var perGiorno = cartellino!.Giornate.ToDictionary(giorno => giorno.Data.Day);
            int giorniDelMese = DateTime.DaysInMonth(cartellino.Anno, cartellino.Mese);
            colonna.Item().Table(tabella =>
            {
                tabella.ColumnsDefinition(colonne =>
                {
                    for (int giorno = 1; giorno <= giorniDelMese; giorno++)
                        colonne.RelativeColumn();
                });
                for (int giorno = 1; giorno <= giorniDelMese; giorno++)
                    tabella.Cell().Background(Fondo).BorderRight(0.4f).BorderBottom(0.4f)
                        .BorderColor(Bordo).PaddingVertical(1.2f).AlignCenter()
                        .Text(giorno.ToString(It)).FontSize(4.8f).FontColor(Etichetta);
                for (int giorno = 1; giorno <= giorniDelMese; giorno++)
                {
                    bool trovato = perGiorno.TryGetValue(giorno, out var dato);
                    string sigla = trovato ? Sigla(dato!.Causale) : "";
                    string sfondo = !trovato ? Bianco
                        : dato!.Causale == CausaleGiornata.Lavorata ? Bianco
                        : dato.Causale == CausaleGiornata.RiposoSettimanale ? Fondo
                        : FondoGiorno;
                    tabella.Cell().Background(sfondo).BorderRight(0.4f).BorderColor(Bordo)
                        .PaddingVertical(2.2f).AlignCenter()
                        .Text(sigla).FontSize(6.6f).SemiBold().FontColor(Testo);
                }
            });

            colonna.Item().BorderTop(0.5f).BorderColor(Bordo).Row(riepilogo =>
            {
                RiepilogoVoce(riepilogo, "Giorni lavorati", cartellino.GiornateLavorate.ToString(It));
                RiepilogoVoce(riepilogo, "Ore lavorate",
                    Numero(cartellino.Giornate.Sum(giorno => giorno.OreOrdinarie)));
                RiepilogoVoce(riepilogo, "Straordinari",
                    Numero(cartellino.Giornate.Sum(giorno => giorno.TotaleStraordinario)));
                RiepilogoVoce(riepilogo, "Giorni ferie", Numero(cartellino.GiorniFerie));
                RiepilogoVoce(riepilogo, "Ore permessi", Numero(cartellino.OrePermessi));
                RiepilogoVoce(riepilogo, "Assenze n.r. (ore)",
                    Numero(cartellino.OreAssenzaNonRetribuita), ultimo: true);
            });
        });
    }

    private void RiepilogoVoce(RowDescriptor riga, string etichetta, string valore, bool ultimo = false)
    {
        var cella = riga.RelativeItem();
        if (!ultimo) cella = cella.BorderRight(0.4f).BorderColor(Bordo);
        cella.PaddingVertical(2.5f).PaddingHorizontal(5).Row(dato =>
        {
            dato.RelativeItem().Text(etichetta.ToUpperInvariant())
                .FontSize(5.4f).SemiBold().FontColor(Etichetta);
            dato.AutoItem().Text(valore).FontSize(6.8f).SemiBold().FontColor(Testo);
        });
    }

    private void BarraNetto(IContainer contenitore, Cedolino cedolino) =>
        contenitore.Border(0.8f).BorderColor(Blu).Row(riga =>
        {
            TotaleBox(riga, "Totale competenze", Importo(cedolino.TotaleCompetenze));
            TotaleBox(riga, "Totale trattenute", Importo(cedolino.TotaleTrattenute));
            TotaleBox(riga, "Somme esenti", Importo(cedolino.TotaleEsenti));
            riga.ConstantItem(175).Background(Blu).PaddingVertical(4).PaddingHorizontal(7)
                .Row(netto =>
                {
                    netto.RelativeItem().AlignMiddle().Text("NETTO\nSIMULATO")
                        .FontSize(6.2f).SemiBold().FontColor(Bianco).LetterSpacing(0.1f);
                    netto.AutoItem().AlignMiddle().Text(Importo(cedolino.NettoInBusta))
                        .FontSize(16f).SemiBold().FontColor(Bianco);
                });
        });

    private void TotaleBox(RowDescriptor riga, string etichetta, string valore) =>
        riga.RelativeItem().BorderRight(0.5f).BorderColor(Bordo)
            .PaddingVertical(3.5f).PaddingHorizontal(6).Column(colonna =>
            {
                colonna.Item().Text(etichetta.ToUpperInvariant())
                    .FontSize(5.4f).SemiBold().FontColor(Etichetta);
                colonna.Item().AlignRight().Text(valore).FontSize(9.5f).SemiBold().FontColor(Testo);
            });

    private void Pagamento(IContainer contenitore, CedolinoPresentazione dati) =>
        contenitore.Border(0.8f).BorderColor(Blu).Column(colonna =>
        {
            colonna.Item().Row(riga =>
            {
                CampoPagamento(riga, "IBAN accredito", dati.IbanMascherato);
                CampoPagamento(riga, "ANF / assegni familiari", EtichettaAnf(dati.Cartellino));
                CampoPagamento(riga, "Presenze", TestoDisponibilitaPresenze(dati.Cartellino), ultimo: true);
            });
            colonna.Item().BorderTop(0.5f).BorderColor(Bordo)
                .PaddingVertical(1).PaddingHorizontal(5).Column(note =>
                {
                    if (!string.IsNullOrWhiteSpace(dati.Emissione.ValidazioneRegistrata))
                        note.Item().Text(dati.Emissione.ValidazioneRegistrata!)
                            .FontSize(5.8f).SemiBold().FontColor(Testo);
                    note.Item().Text(
                            "La firma attesta la sola ricezione del prospetto e non costituisce prova " +
                            "dell'avvenuto pagamento. Data o prova del pagamento: non disponibile nel documento.")
                        .FontSize(5.6f).FontColor(Etichetta);
                    note.Item().Text(
                            "* Dato informativo a carico del datore o accantonato: non riduce il netto. " +
                            "Le caselle senza valore non sono azzerate: il dato non è stato calcolato.")
                        .FontSize(5.6f).FontColor(Etichetta);
                });
            colonna.Item().BorderTop(0.5f).BorderColor(Bordo).Row(riga =>
            {
                Firma(riga, "Firma del lavoratore");
                Firma(riga, "Firma del datore di lavoro", dati.Emissione.FirmaSiglaTimbro, ultimo: true);
            });
        });

    private void CampoPagamento(RowDescriptor riga, string etichetta, string? valore, bool ultimo = false)
    {
        var cella = riga.RelativeItem();
        if (!ultimo) cella = cella.BorderRight(0.5f).BorderColor(Bordo);
        cella.PaddingVertical(1.5f).PaddingHorizontal(5).Column(colonna =>
        {
            colonna.Item().Text(etichetta.ToUpperInvariant())
                .FontSize(5.4f).SemiBold().FontColor(Etichetta);
            colonna.Item().Text(Valore(valore)).FontSize(6.8f).FontColor(Testo);
        });
    }

    private void Firma(RowDescriptor riga, string etichetta, byte[]? immagine = null, bool ultimo = false)
    {
        var cella = riga.RelativeItem();
        if (!ultimo) cella = cella.BorderRight(0.5f).BorderColor(Bordo);
        cella.PaddingVertical(1.5f).PaddingHorizontal(5).Column(colonna =>
        {
            if (immagine is { Length: > 0 })
                colonna.Item().Height(20).Image(immagine).FitArea();
            else
                colonna.Item().PaddingTop(10).BorderBottom(0.5f).BorderColor(Etichetta);
            colonna.Item().PaddingTop(1).Text(etichetta.ToUpperInvariant())
                .FontSize(5.4f).SemiBold().FontColor(Etichetta);
        });
    }

    private void Piede(IContainer contenitore, CedolinoPresentazione dati) =>
        contenitore.PaddingTop(3).BorderTop(0.5f).BorderColor(Bordo).Row(riga =>
        {
            riga.RelativeItem().Text(NotaStato(dati.Emissione.Stato))
                .FontSize(5.3f).SemiBold().FontColor(Avviso);
            if (!string.IsNullOrWhiteSpace(dati.DicituraStudio))
                riga.RelativeItem().AlignCenter().Text(dati.DicituraStudio)
                    .FontSize(5.3f).FontColor(Etichetta);
            riga.ConstantItem(70).AlignRight().Text(testo =>
            {
                testo.DefaultTextStyle(stile => stile.FontSize(5.4f).FontColor(Etichetta));
                testo.Span("Pagina ");
                testo.CurrentPageNumber();
                testo.Span(" / ");
                testo.TotalPages();
            });
        });

    private string NotaStato(string? stato) => stato?.Trim().ToLowerInvariant() switch
    {
        "preworkflow" => "BOZZA / NON CERTIFICATO - Ricostruzione da archivio pre-workflow non definitiva.",
        "validata" => "BOZZA / NON CERTIFICATO - Stato workflow: validazione registrata.",
        "chiusa" => "BOZZA / NON CERTIFICATO - Stato workflow: chiusa; consegna e pagamento restano separati.",
        "controllata" => "BOZZA / NON CERTIFICATO - Stato workflow: dati congelati, validazione da registrare.",
        _ => "BOZZA / NON CERTIFICATO - Documento di simulazione da sottoporre al professionista.",
    };

    // ---- Lettura dei dati e formattazione ------------------------------------------------

    private decimal? CompetenzaVoce(IReadOnlyList<RigaCedolinoPresentata> righe, string codice) =>
        righe.FirstOrDefault(riga => riga.Codice == codice)?.Competenza;

    private decimal? BaseVoce(IReadOnlyList<RigaCedolinoPresentata> righe, params string[] codici) =>
        righe.FirstOrDefault(riga => codici.Contains(riga.Codice) && riga.Base is not null)?.Base;

    private string? GiorniLavorati(Cartellino? cartellino) =>
        cartellino is { Giornate.Count: > 0 } ? cartellino.GiornateLavorate.ToString(It) : null;

    private string? OreLavorate(Cartellino? cartellino) =>
        cartellino is { Giornate.Count: > 0 }
            ? Numero(cartellino.Giornate.Sum(giorno => giorno.OreOrdinarie))
            : null;

    private string Sigla(CausaleGiornata causale) => causale switch
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

    private string TestoDisponibilitaPresenze(Cartellino? cartellino) => cartellino switch
    {
        null => "Dati presenze non disponibili in questo documento",
        { Giornate.Count: > 0 } => "Calendario giornaliero riportato nella sezione 09",
        // Con tutte le variabili a zero il cartellino a totali non dice nulla su ore e giorni.
        _ when SenzaVariabiliMensili(cartellino) =>
            "Nessuna variabile mensile registrata: ore e giorni lavorati non disponibili",
        _ => "Cartellino acquisito a totali mensili",
    };

    private bool SenzaVariabiliMensili(Cartellino c) =>
        c.OreStraordinario == 0m && c.OreStraordinarioNotturno == 0m
        && c.OreStraordinarioFestivo == 0m && c.GiorniFerie == 0m && c.OrePermessi == 0m
        && c.OreExFestivita == 0m && c.GiorniMalattia == 0m && c.GiorniInfortunio == 0m
        && c.OreAssenzaNonRetribuita == 0m;

    private string EtichettaAnf(Cartellino? cartellino) => cartellino?.StatoAssegniFamiliari switch
    {
        StatoAssegniFamiliari.NonSpettanti => "Non spettanti nel mese",
        StatoAssegniFamiliari.Spettanti => Importo(cartellino!.ImportoAssegniFamiliari),
        null => "Dato non disponibile in questo documento",
        _ => "Da verificare prima della chiusura",
    };

    private string Importo(decimal valore) => valore.ToString("N2", It);
    private string Importo(decimal? valore) => valore is decimal v ? Importo(v) : "";
    private string Importo4(decimal? valore) => valore is decimal v ? v.ToString("N4", It) : "";
    private string Importo4Breve(decimal? valore) => Importo4(valore);
    private string ImportoEconomico(decimal? valore) => valore is decimal v && v != 0m
        ? v.ToString("N2", It)
        : "";
    private string Numero(decimal valore) => valore.ToString("0.##", It);
    private string Numero(decimal? valore) => valore is decimal v ? Numero(v) : "";
    private string Valore(string? valore) => string.IsNullOrWhiteSpace(valore) ? "" : valore;
    private string Capitalizza(string valore) => string.IsNullOrWhiteSpace(valore)
        ? valore
        : char.ToUpper(valore[0], It) + valore[1..];

    private string UnisciRiga(params string?[] valori)
    {
        string testo = string.Join(" · ", valori.Where(v => !string.IsNullOrWhiteSpace(v)));
        return string.IsNullOrWhiteSpace(testo) ? "" : testo;
    }
}
