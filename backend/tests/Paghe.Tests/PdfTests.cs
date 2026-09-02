using System.Text;
using Paghe.Core.Calcolo;
using Paghe.Core.Domain;
using Paghe.Core.Fatturazione;
using Paghe.Core.Parametri;
using Paghe.Documenti;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using Xunit;

namespace Paghe.Tests;

public class PdfTests
{
    public static IEnumerable<object[]> TuttiGliStili =>
        CatalogoStiliCedolino.Tutti.Select(voce => new object[] { voce.Stile });

    public static IEnumerable<object[]> TuttiGliStiliEFont =>
        from voce in CatalogoStiliCedolino.Tutti
        from font in new[]
        {
            FontDocumenti.Predefinito,
            FontDocumenti.Famiglia("carlito"),
            FontDocumenti.Famiglia("plex"),
        }
        select new object[] { voce.Stile, font };

    private static bool IsPdf(byte[] bytes) =>
        bytes.Length > 4 && Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-";

    [Theory]
    [MemberData(nameof(TuttiGliStiliEFont))]
    public void CedolinoPdf_OgniStileEFontUsaA4EIlNumeroDiPagineDellaSuaFamiglia(
        StileCedolino stile, string fontFamily)
    {
        var motore = new MotoreCalcoloCedolino(Seed.Parametri2026(), Seed.CcnlCommercio2026());
        var cedolino = motore.Calcola(
            new RapportoLavoro
            {
                CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = Qualifica.Impiegato,
                Regione = "Lombardia", CodiceCatastaleComune = "F205",
            },
            new Cartellino { Anno = 2026, Mese = 7 });

        byte[] pdf = CedolinoPdf.Genera(
            cedolino,
            new IntestazioneCedolino
            {
                Azienda = "Alfa Srl", Dipendente = "Rossi Mario", CodiceCnel = "H011",
            },
            cartellino: null,
            stile: stile,
            fontFamily: fontFamily);

        string? directoryFormati = Environment.GetEnvironmentVariable("PAGHE_PDF_FORMATI_DIR");
        if (!string.IsNullOrWhiteSpace(directoryFormati))
        {
            Directory.CreateDirectory(directoryFormati);
            File.WriteAllBytes(
                Path.Combine(directoryFormati,
                    $"formato-{CatalogoStiliCedolino.Chiave(stile)}-{FontDocumenti.Chiave(fontFamily)}.pdf"),
                pdf);
        }

        using var documento = PdfDocument.Open(pdf);
        int pagineAttese = CatalogoStiliCedolino.Descrivi(stile).Famiglia switch
        {
            FamigliaStileCedolino.Classico => 1,
            FamigliaStileCedolino.Modulare => 1,
            FamigliaStileCedolino.Editoriale => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(stile)),
        };
        Assert.Equal(pagineAttese, documento.NumberOfPages);
        Assert.All(documento.GetPages(), pagina => Assert.Equal(PageSize.A4, pagina.Size));
    }

    [Fact]
    public void CedolinoPdf_ProduceUnPdfValido()
    {
        var motore = new MotoreCalcoloCedolino(Seed.Parametri2026(), Seed.CcnlCommercio2026());
        var cedolino = motore.Calcola(
            new RapportoLavoro
            {
                CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = Qualifica.Impiegato,
                Regione = "Lombardia", CodiceCatastaleComune = "F205",
            },
            new Cartellino { Anno = 2026, Mese = 6 });

        byte[] pdf = CedolinoPdf.Genera(cedolino, "Alfa Srl", "Mario Rossi");

        Assert.True(IsPdf(pdf), "Il file generato non inizia con l'header %PDF-.");
        Assert.True(pdf.Length > 1000, "Il PDF è troppo piccolo per essere valido.");
    }

    [Fact]
    public void CedolinoPdf_ClassicoConPresenzeEAssenze_ProduceUnPdfValido()
    {
        var cartellino = new Cartellino
        {
            Anno = 2026,
            Mese = 7,
            GiorniFerie = 2m,
            OrePermessi = 4m,
            OreExFestivita = 2m,
            GiorniMalattia = 5m,
            NumeroEventoMalattiaAnno = 2,
            GiorniInfortunio = 3m,
            OreAssenzaNonRetribuita = 8m,
            OreStraordinario = 6m,
            OreStraordinarioNotturno = 2m,
            OreStraordinarioFestivo = 2m,
        };
        var motore = new MotoreCalcoloCedolino(Seed.Parametri2026(), Seed.CcnlCommercio2026());
        var cedolino = motore.Calcola(
            new RapportoLavoro
            {
                CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = Qualifica.Impiegato,
                Regione = "Lombardia", CodiceCatastaleComune = "F205",
            },
            cartellino);

        byte[] pdf = CedolinoPdf.Genera(cedolino, new IntestazioneCedolino
        {
            Azienda = "Officina Nord S.r.l.",
            AziendaIndirizzo = "Via Melchiorre Gioia 12",
            AziendaComune = "20124 Milano (MI)",
            PartitaIva = "12345678903",
            CodiceFiscaleAzienda = "12345678903",
            MatricolaInps = "1234567890/01",
            PatInail = "12345678/01",
            Dipendente = "Giulia Bianchi",
            CodiceFiscaleDipendente = "BNCGLI93B41F205X",
            Matricola = "000123",
            IbanDipendente = "IT60X0542811101000000123456",
            DataAssunzione = "01/09/2022",
            Ccnl = "CCNL Terziario, Distribuzione e Servizi",
            CodiceCnel = "H011",
            Livello = "3",
            Qualifica = "Impiegata",
            TipoContratto = "Tempo indeterminato",
            Mensilita = 14,
            Orario = "Tempo pieno · 40 ore settimanali",
        }, cartellino, stile: StileCedolino.Classico);

        // Opt-in per rigenerare il fac-simile revisionabile senza introdurre scritture nei test normali.
        var percorsoAnteprima = Environment.GetEnvironmentVariable("PAGHE_PDF_OUTPUT");
        if (!string.IsNullOrWhiteSpace(percorsoAnteprima))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(percorsoAnteprima)!);
            File.WriteAllBytes(percorsoAnteprima, pdf);
        }

        Assert.True(IsPdf(pdf));
        Assert.True(pdf.Length > 3_000, "Il riepilogo completo non risulta nel documento PDF.");

        string testo = EstrattoreTestoPdf.EstraiTesto(pdf);
        Assert.Contains("PROSPETTO PAGA DI SIMULAZIONE – NON COSTITUISCE LUL", testo);
        Assert.Contains("H011", testo);
        // Griglia classica: fasce anagrafica, ratei e quadratura contributiva/fiscale.
        Assert.Contains("COGNOME E NOME", testo);
        Assert.Contains("CCNL APPLICATO", testo);
        Assert.Contains("RATEI E ISTITUTI", testo);
        Assert.Contains("Malattia", testo);
        Assert.Contains("IMPON. CONTR. SOC.", testo);
        Assert.Contains("IMPONIBILE IRPEF", testo);
        Assert.Contains("NETTO SIMULATO", testo);
        Assert.Contains("BOZZA / NON CERTIFICATO", testo);
        Assert.DoesNotContain("Firma per quietanza", testo);
        Assert.DoesNotContain("RETRIBUZIONE UTILE TFR", testo);
        Assert.DoesNotContain("Come leggere questo cedolino", testo);

        using var documento = PdfDocument.Open(pdf);
        var pagine = documento.GetPages().ToList();
        Assert.InRange(pagine.Count, 1, 2);
        Assert.All(pagine, pagina =>
            Assert.Contains("PROSPETTO PAGA DI SIMULAZIONE – NON COSTITUISCE LUL", pagina.Text));
    }

    [Fact]
    public void CedolinoPdf_MascheraIbanENonEsponeQuelloCompleto()
    {
        const string iban = "IT60X0542811101000000123456";
        var cedolino = new Cedolino
        {
            Anno = 2026,
            Mese = 7,
            TotaleCompetenze = 1_500m,
            TotaleTrattenute = 300m,
            NettoInBusta = 1_200m,
        };

        byte[] pdf = CedolinoPdf.Genera(cedolino, new IntestazioneCedolino
        {
            Azienda = "Alfa Srl",
            Dipendente = "Rossi Mario",
            IbanDipendente = iban,
            CodiceCnel = "H011",
        });

        string testo = EstrattoreTestoPdf.EstraiTesto(pdf);
        Assert.DoesNotContain(iban, testo);
        Assert.Contains("3456", testo);
        Assert.Contains("H011", testo);
        Assert.Equal("IT60 **** **** **** **** 3456", CedolinoPdfPresentazione.MascheraIban(iban));
    }

    [Fact]
    public void CedolinoPdf_CompetenzaNegativaRestaNellaColonnaCompetenze()
    {
        var cedolino = new Cedolino { Anno = 2026, Mese = 7 };
        cedolino.Righe.Add(new CedolinoRiga
        {
            Codice = "STORNO",
            Descrizione = "Storno competenza precedente",
            Tipo = TipoVoce.Competenza,
            Importo = -12.34m,
        });
        cedolino.Righe.Add(new CedolinoRiga
        {
            Codice = "TRATT",
            Descrizione = "Trattenuta ordinaria",
            Tipo = TipoVoce.Trattenuta,
            Importo = -8.50m,
        });

        var dati = CedolinoPdfPresentazione.Crea(
            cedolino,
            new IntestazioneCedolino { Azienda = "Alfa Srl", Dipendente = "Rossi Mario" },
            null);

        var storno = Assert.Single(dati.Righe, riga => riga.Codice == "STORNO");
        Assert.Equal(-12.34m, storno.Competenza);
        Assert.Null(storno.Trattenuta);

        var trattenuta = Assert.Single(dati.Righe, riga => riga.Codice == "TRATT");
        Assert.Null(trattenuta.Competenza);
        Assert.Equal(8.50m, trattenuta.Trattenuta);
    }

    [Fact]
    public void CedolinoPdf_PreservaLeRigheInformativeAImportoZero()
    {
        var cedolino = new Cedolino { Anno = 2026, Mese = 7 };
        cedolino.Righe.Add(new CedolinoRiga
        {
            Codice = "FERIE",
            Descrizione = "Ferie godute",
            Tipo = TipoVoce.Competenza,
            Quantita = 2m,
            Base = 51.58m,
            Importo = 0m,
        });

        var dati = CedolinoPdfPresentazione.Crea(
            cedolino,
            new IntestazioneCedolino { Azienda = "Alfa Srl", Dipendente = "Rossi Mario" },
            null);

        var ferie = Assert.Single(dati.Righe);
        Assert.Equal("FERIE", ferie.Codice);
        Assert.Equal(2m, ferie.Quantita);
        Assert.Equal(51.58m, ferie.Base);
        Assert.Equal(0m, ferie.Competenza);
        Assert.Null(ferie.Trattenuta);
    }

    [Fact]
    public void CedolinoPdf_SenzaCartellinoNonInventaPresenze()
    {
        var cedolino = new Cedolino
        {
            Anno = 2026,
            Mese = 7,
            TotaleCompetenze = 1_000m,
            TotaleTrattenute = 200m,
            NettoInBusta = 800m,
        };

        byte[] pdf = CedolinoPdf.Genera(cedolino, "Alfa Srl", "Rossi Mario");
        string testo = EstrattoreTestoPdf.EstraiTesto(pdf);

        Assert.Contains("Dati presenze non disponibili in questo documento", testo);
        Assert.Contains("CALENDARIO GIORNALIERO NON DISPONIBILE", testo);
        Assert.Contains("ANF / assegni familiari", testo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Dato non disponibile in questo documento", testo,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CedolinoPdf_ClassicoScenarioDensoNonPerdeLeVoci()
    {
        var cedolino = new Cedolino
        {
            Anno = 2026,
            Mese = 7,
            TotaleCompetenze = 5_000m,
            TotaleTrattenute = 1_000m,
            NettoInBusta = 4_000m,
        };
        for (int indice = 1; indice <= 40; indice++)
        {
            cedolino.Righe.Add(new CedolinoRiga
            {
                Codice = $"V{indice:00}",
                Descrizione = $"Voce retributiva di prova numero {indice} con descrizione estesa",
                Tipo = indice % 4 == 0 ? TipoVoce.Trattenuta : TipoVoce.Competenza,
                Quantita = indice,
                Base = 10m,
                Importo = indice % 4 == 0 ? -10m : 10m,
            });
        }

        byte[] pdf = CedolinoPdf.Genera(
            cedolino,
            new IntestazioneCedolino
            {
                Azienda = "Azienda con ragione sociale particolarmente lunga Srl",
                Dipendente = "Rossi Mario",
            },
            cartellino: null,
            stile: StileCedolino.Classico);
        var percorsoAnteprimaDensa = Environment.GetEnvironmentVariable("PAGHE_PDF_DENSO_OUTPUT");
        if (!string.IsNullOrWhiteSpace(percorsoAnteprimaDensa))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(percorsoAnteprimaDensa)!);
            File.WriteAllBytes(percorsoAnteprimaDensa, pdf);
        }

        string testo = EstrattoreTestoPdf.EstraiTesto(pdf);
        using var documento = PdfDocument.Open(pdf);

        Assert.InRange(documento.NumberOfPages, 2, 3);
        foreach (int indice in Enumerable.Range(1, 40))
            Assert.Contains($"V{indice:00}", testo);
        Assert.All(documento.GetPages(), pagina =>
            Assert.Contains("PROSPETTO PAGA DI SIMULAZIONE – NON COSTITUISCE LUL", pagina.Text));
        Assert.DoesNotContain("Come leggere questo cedolino", testo);
    }

    [Fact]
    public void CedolinoPdf_ClassicoCalendario31Giorni_RestaNelCorpoDelCedolino()
    {
        var giornate = Enumerable.Range(1, 31)
            .Select(giorno =>
            {
                var data = new DateOnly(2026, 7, giorno);
                bool riposo = data.DayOfWeek == DayOfWeek.Sunday;
                return new GiornataCartellino
                {
                    Data = data,
                    Causale = riposo ? CausaleGiornata.RiposoSettimanale : CausaleGiornata.Lavorata,
                    OreOrdinarie = riposo ? 0m : 8m,
                    OreStraordinario = giorno == 15 ? 2m : 0m,
                };
            })
            .ToList();
        var cartellino = new Cartellino
        {
            Anno = 2026,
            Mese = 7,
            Giornate = giornate,
            OreStraordinario = 2m,
            StatoAssegniFamiliari = StatoAssegniFamiliari.NonSpettanti,
        };
        var cedolino = new Cedolino
        {
            Anno = 2026,
            Mese = 7,
            TotaleCompetenze = 1_500m,
            TotaleTrattenute = 300m,
            NettoInBusta = 1_200m,
        };

        byte[] pdf = CedolinoPdf.Genera(cedolino, new IntestazioneCedolino
        {
            Azienda = "Alfa Srl",
            Dipendente = "Rossi Mario",
            CodiceCnel = "H011",
        }, cartellino, stile: StileCedolino.Classico);

        var percorsoAnteprima = Environment.GetEnvironmentVariable("PAGHE_PDF_CALENDARIO_OUTPUT");
        if (!string.IsNullOrWhiteSpace(percorsoAnteprima))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(percorsoAnteprima)!);
            File.WriteAllBytes(percorsoAnteprima, pdf);
        }

        string testo = EstrattoreTestoPdf.EstraiTesto(pdf);
        using var documento = PdfDocument.Open(pdf);

        // Il calendario sta nella colonna a lato delle voci: niente allegato separato.
        Assert.Equal(1, documento.NumberOfPages);
        Assert.Contains("SIGLA", testo);
        Assert.Contains("LEGENDA SIGLE", testo);
        Assert.Contains("31", documento.GetPage(1).Text);
        Assert.DoesNotContain("CALENDARIO GIORNALIERO NON DISPONIBILE", testo);
        Assert.Contains("ANF / assegni familiari: non spettanti nel mese", testo);
    }

    [Fact]
    public void CedolinoPdf_AncheSeChiusoRestaUnaSimulazioneNonCertificata()
    {
        var cedolino = new Cedolino
        {
            Anno = 2026, Mese = 7,
            TotaleCompetenze = 1_500m, TotaleTrattenute = 300m, NettoInBusta = 1_200m,
        };

        byte[] pdf = CedolinoPdf.Genera(
            cedolino,
            new IntestazioneCedolino { Azienda = "Alfa Srl", Dipendente = "Rossi Mario" },
            cartellino: null,
            new ContestoEmissioneCedolino(
                "Chiusa", ValidazioneRegistrata: "Validazione registrata: Dott.ssa Maria Verdi"));

        string testo = EstrattoreTestoPdf.EstraiTesto(pdf);
        Assert.Contains("Validazione registrata: Dott.ssa Maria Verdi", testo);
        Assert.Contains("PROSPETTO PAGA DI SIMULAZIONE – NON COSTITUISCE LUL", testo);
        Assert.Contains("BOZZA / NON CERTIFICATO", testo);
        Assert.Contains("Stato workflow: chiusa", testo, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(TuttiGliStili))]
    public void CedolinoPdf_NonSiQualificaComeLibroUnicoDelLavoro(StileCedolino stile)
    {
        var cedolino = new Cedolino
        {
            Anno = 2026,
            Mese = 7,
            TotaleCompetenze = 1_500m,
            TotaleTrattenute = 300m,
            NettoInBusta = 1_200m,
        };
        var cartellino = new Cartellino
        {
            Anno = 2026,
            Mese = 7,
            StatoAssegniFamiliari = StatoAssegniFamiliari.NonSpettanti,
            Giornate =
            [
                new GiornataCartellino
                {
                    Data = new DateOnly(2026, 7, 1),
                    Causale = CausaleGiornata.Lavorata,
                    OreOrdinarie = 8m,
                },
            ],
        };

        byte[] pdf = CedolinoPdf.Genera(
            cedolino,
            new IntestazioneCedolino
            {
                Azienda = "Alfa Srl",
                Dipendente = "Rossi Mario",
                CodiceCnel = "H011",
            },
            cartellino,
            stile: stile);

        string testo = EstrattoreTestoPdf.EstraiTesto(pdf).ToUpperInvariant();
        Assert.Contains("PROSPETTO PAGA DI SIMULAZIONE – NON COSTITUISCE LUL", testo);
        Assert.Contains("BOZZA / NON CERTIFICATO", testo);
        Assert.DoesNotContain("LIBRO UNICO DEL LAVORO - PROSPETTO PAGA", testo);
        Assert.DoesNotContain("D.L. 112/2008 ART. 39", testo);
    }

    [Theory]
    [MemberData(nameof(TuttiGliStili))]
    public void CedolinoPdf_MantieneTitoloEAvvisoInOgniStatoWorkflow(StileCedolino stile)
    {
        string[] stati = ["Bozza", "PreWorkflow", "Controllata", "Validata", "Chiusa"];
        var cedolino = new Cedolino
        {
            Anno = 2026, Mese = 7,
            TotaleCompetenze = 1_500m, TotaleTrattenute = 300m, NettoInBusta = 1_200m,
        };

        foreach (string stato in stati)
        {
            byte[] pdf = CedolinoPdf.Genera(
                cedolino,
                new IntestazioneCedolino { Azienda = "Alfa Srl", Dipendente = "Rossi Mario" },
                cartellino: null,
                emissione: new ContestoEmissioneCedolino(stato),
                stile: stile);

            string testo = EstrattoreTestoPdf.EstraiTesto(pdf).ToUpperInvariant();
            Assert.Contains("PROSPETTO PAGA DI SIMULAZIONE – NON COSTITUISCE LUL", testo);
            Assert.Contains("BOZZA / NON CERTIFICATO", testo);
        }
    }

    [Theory]
    [MemberData(nameof(TuttiGliStili))]
    public void CedolinoPdf_EsponeTariffeSaldiCostiERigheFigurativeSenzaPrecisioneNascosta(
        StileCedolino stile)
    {
        var cedolino = new Cedolino
        {
            Anno = 2026,
            Mese = 7,
            RetribuzioneFissa = 2_015.15m,
            TariffaOraria = 11.994940476m,
            TariffaGiornaliera = 77.50576923m,
            TotaleCompetenze = 2_015.15m,
            TotaleTrattenute = 500m,
            NettoInBusta = 1_515.15m,
            QuotaTfr = 164.16m,
            PrestazioniRecuperabili = 123.45m,
            CostoAzienda = 2_700.10m,
            CostoMedioAnnualizzato = 3_000.20m,
            FerieMaturate = 15.17m,
            FerieGodute = 2m,
            FerieResidue = 13.17m,
            RolSaldoPrecedente = 4m,
            RolMaturati = 18.67m,
            RolGoduti = 3m,
            RolResidui = 19.67m,
            ExFestivitaSaldoPrecedente = 2m,
            ExFestivitaMaturate = 18.66m,
            ExFestivitaGodute = 1m,
            ExFestivitaResidue = 19.66m,
        };
        cedolino.Righe.AddRange(
        [
            new CedolinoRiga
            {
                Codice = "PAGA_BASE", Descrizione = "Paga base", Tipo = TipoVoce.Competenza,
                Base = 1_453.94m, Importo = 1_453.94m,
            },
            new CedolinoRiga
            {
                Codice = "CONTINGENZA", Descrizione = "Contingenza (EDR incluso)",
                Tipo = TipoVoce.Competenza, Base = 527.90m, Importo = 527.90m,
            },
            new CedolinoRiga
            {
                Codice = "SCATTI", Descrizione = "Scatto di anzianità", Tipo = TipoVoce.Competenza,
                Base = 21.95m, Importo = 21.95m,
            },
            new CedolinoRiga
            {
                Codice = "TFR_PAGA_BASE", Descrizione = "Quota TFR su paga base",
                Tipo = TipoVoce.Figurativa, Base = 1_453.94m, Importo = 118.54m,
            },
            new CedolinoRiga
            {
                Codice = "TFR_CONTINGENZA", Descrizione = "Quota TFR su contingenza",
                Tipo = TipoVoce.Figurativa, Base = 527.90m, Importo = 43.04m,
            },
            new CedolinoRiga
            {
                Codice = "FONDO_SAN_DATORE", Descrizione = "Fondo Est c/azienda",
                Tipo = TipoVoce.Figurativa, Importo = 13m,
            },
            new CedolinoRiga
            {
                Codice = "ENTE_BIL_DATORE", Descrizione = "Ente bilaterale c/azienda",
                Tipo = TipoVoce.Figurativa, Base = 1_981.84m, Importo = 1.98m,
            },
            new CedolinoRiga
            {
                Codice = "PREST_RECUPERABILI", Descrizione = "Prestazioni anticipate recuperabili",
                Tipo = TipoVoce.Figurativa, Importo = -123.45m,
            },
            new CedolinoRiga
            {
                Codice = "QUATTORDICESIMA_SEPARATA",
                Descrizione = "Quattordicesima da documentare con prospetto separato",
                Tipo = TipoVoce.Figurativa, Importo = 0m,
            },
            new CedolinoRiga
            {
                Codice = "INPS_DATORE", Descrizione = "Contributi INPS c/datore",
                Tipo = TipoVoce.Figurativa, Base = 2_015.15m, Importo = 500m,
            },
        ]);

        byte[] pdf = CedolinoPdf.Genera(
            cedolino,
            new IntestazioneCedolino
            {
                Azienda = "Officina Nord S.r.l.",
                Dipendente = "Giulia Bianchi",
                CodiceCnel = "H011",
                DataNascita = "03/04/1985",
                ComuneResidenza = "Milano (MI)",
                DataAssunzione = "01/10/2020",
                DataCessazione = "31/07/2026",
            },
            cartellino: null,
            new ContestoEmissioneCedolino("Chiusa"),
            stile,
            progressivi: new ProgressiviAnnui(
                MesiCompresi: 7,
                ImponibileInps: 12_345.67m,
                ImponibileIrpef: 10_234.56m,
                IrpefNetta: 2_345.67m,
                Detrazioni: 1_234.56m));

        string? directoryAnteprime = Environment.GetEnvironmentVariable("PAGHE_PDF_VARIANTI_DIR");
        if (!string.IsNullOrWhiteSpace(directoryAnteprime))
        {
            Directory.CreateDirectory(directoryAnteprime);
            File.WriteAllBytes(
                Path.Combine(directoryAnteprime, $"cedolino-{CatalogoStiliCedolino.Chiave(stile)}.pdf"),
                pdf);
        }

        string testo = EstrattoreTestoPdf.EstraiTesto(pdf).ToUpperInvariant();
        Assert.Contains("PROSPETTO PAGA DI SIMULAZIONE – NON COSTITUISCE LUL", testo);
        Assert.Contains("BOZZA / NON CERTIFICATO", testo);
        Assert.Contains("03/04/1985", testo);
        Assert.Contains("MILANO (MI)", testo);
        Assert.Contains("01/10/2020", testo);
        Assert.Contains("31/07/2026", testo);
        Assert.Contains("RETRIBUZIONE FISSA", testo);
        Assert.Contains("2.015,1500", testo);
        Assert.Contains("TARIFFA ORARIA", testo);
        Assert.Contains("11,9949", testo);
        Assert.Contains("TARIFFA GIORNALIERA", testo);
        Assert.Contains("77,5058", testo);
        Assert.Contains("FERIE (GIORNI)", testo);
        Assert.Contains("ROL (ORE)", testo);
        Assert.Contains("EX FESTIVITÀ (ORE)", testo);
        Assert.Contains("SALDO PRECEDENTE FERIE", testo);
        Assert.Contains("COSTO PARZIALE STIMATO DEL MESE", testo);
        Assert.Contains("COSTO MEDIO PARZIALE STIMATO ANNUO/12", testo);
        Assert.Contains("TFR_PAGA_BASE", testo);
        Assert.Contains("TFR_CONTINGENZA", testo);
        Assert.Contains("FONDO_SAN_DATORE", testo);
        Assert.Contains("ENTE_BIL_DATORE", testo);
        Assert.Contains("PREST_RECUPERABILI", testo);
        Assert.Contains("QUATTORDICESIMA_SEPARATA", testo);
        Assert.DoesNotContain("INPS_DATORE", testo);
        Assert.Contains("12.345,67", testo);
        Assert.Contains("10.234,56", testo);
        Assert.Contains("2.345,67", testo);
        Assert.Contains("1.234,56", testo);
    }

    [Fact]
    public void CedolinoPdf_ConLogoAziendale_ProduceUnPdfValido()
    {
        // PNG 1x1 valido: basta per verificare che la testata accetti e renda il logo.
        byte[] logoPng = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

        var motore = new MotoreCalcoloCedolino(Seed.Parametri2026(), Seed.CcnlCommercio2026());
        var cedolino = motore.Calcola(
            new RapportoLavoro
            {
                CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = Qualifica.Impiegato,
                Regione = "Lombardia", CodiceCatastaleComune = "F205",
            },
            new Cartellino { Anno = 2026, Mese = 6 });

        byte[] pdf = CedolinoPdf.Genera(cedolino, new IntestazioneCedolino
        {
            Azienda = "Alfa Srl",
            Logo = logoPng,
            Dipendente = "Rossi Mario",
        });

        Assert.True(IsPdf(pdf));
        Assert.Contains("ALFA SRL", EstrattoreTestoPdf.EstraiTesto(pdf));
    }

    [Fact]
    public void FatturaPdf_ProduceUnPdfValido()
    {
        var fattura = new Fattura
        {
            Numero = "1/2026", Data = new DateOnly(2026, 6, 26),
            Cedente = new DatiAnagrafici
            {
                Denominazione = "Alfa Srl", PartitaIva = "01234567897",
                Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
            },
            Cessionario = new DatiAnagrafici
            {
                Denominazione = "Beta Spa", PartitaIva = "09876543217",
                Indirizzo = "Via Po 2", Cap = "10100", Comune = "Torino", Provincia = "TO",
            },
            Righe =
            {
                new RigaFattura { Descrizione = "Consulenza", PrezzoUnitario = 100m, AliquotaIva = 22m },
                new RigaFattura { Descrizione = "Materiali", Quantita = 2m, PrezzoUnitario = 50m, AliquotaIva = 10m },
            },
        };

        byte[] pdf = FatturaPdf.Genera(fattura);

        Assert.True(IsPdf(pdf), "Il file generato non inizia con l'header %PDF-.");
        Assert.True(pdf.Length > 1000, "Il PDF è troppo piccolo per essere valido.");
    }
}
