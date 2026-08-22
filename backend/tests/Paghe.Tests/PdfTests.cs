using System.Text;
using Paghe.Core.Calcolo;
using Paghe.Core.Domain;
using Paghe.Core.Fatturazione;
using Paghe.Core.Parametri;
using Paghe.Documenti;
using UglyToad.PdfPig;
using Xunit;

namespace Paghe.Tests;

public class PdfTests
{
    private static bool IsPdf(byte[] bytes) =>
        bytes.Length > 4 && Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-";

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
    public void CedolinoPdf_ConPresenzeEAssenze_ProduceUnPdfValido()
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
            PartitaIva = "12345678901",
            CodiceFiscaleAzienda = "12345678901",
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
        }, cartellino);

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
        Assert.Contains("CEDOLINO PAGA", testo);
        Assert.Contains("H011", testo);
        // Griglia classica: fasce anagrafica, ratei e quadratura contributiva/fiscale.
        Assert.Contains("COGNOME E NOME", testo);
        Assert.Contains("CCNL APPLICATO", testo);
        Assert.Contains("RATEI E ISTITUTI", testo);
        Assert.Contains("Malattia", testo);
        Assert.Contains("IMPON. CONTR. SOC.", testo);
        Assert.Contains("IMPONIBILE IRPEF", testo);
        Assert.Contains("NETTO BUSTA", testo);
        Assert.Contains("BOZZA DA VALIDARE", testo);
        Assert.DoesNotContain("Firma per quietanza", testo);
        Assert.DoesNotContain("RETRIBUZIONE UTILE TFR", testo);
        Assert.DoesNotContain("Come leggere questo cedolino", testo);

        using var documento = PdfDocument.Open(pdf);
        var pagine = documento.GetPages().ToList();
        Assert.Single(pagine);
        Assert.Contains("CEDOLINO PAGA", pagine[0].Text);
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
        Assert.Contains("ANF / assegni familiari: dato non disponibile in questo documento", testo);
    }

    [Fact]
    public void CedolinoPdf_ScenarioDensoNonPerdeLeVoci()
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

        byte[] pdf = CedolinoPdf.Genera(cedolino, "Azienda con ragione sociale particolarmente lunga Srl", "Rossi Mario");
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
        Assert.All(documento.GetPages(), pagina => Assert.Contains("CEDOLINO PAGA", pagina.Text));
        Assert.DoesNotContain("Come leggere questo cedolino", testo);
    }

    [Fact]
    public void CedolinoPdf_Calendario31Giorni_RestaNelCorpoDelCedolino()
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
        }, cartellino);

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
    public void CedolinoPdf_RiportaLaValidazioneRegistrataEChiudeLoStato()
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
        Assert.Contains("CEDOLINO CHIUSO", testo);
        Assert.DoesNotContain("BOZZA DA VALIDARE", testo);
    }

    [Theory]
    [InlineData(StileCedolino.Classico)]
    [InlineData(StileCedolino.Colori)]
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
        Assert.Contains("NON COSTITUISCE LIBRO UNICO DEL LAVORO", testo);
        Assert.DoesNotContain("LIBRO UNICO DEL LAVORO - PROSPETTO PAGA", testo);
        Assert.DoesNotContain("D.L. 112/2008 ART. 39", testo);
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
                Denominazione = "Alfa Srl", PartitaIva = "01234567890",
                Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
            },
            Cessionario = new DatiAnagrafici
            {
                Denominazione = "Beta Spa", PartitaIva = "09876543210",
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
