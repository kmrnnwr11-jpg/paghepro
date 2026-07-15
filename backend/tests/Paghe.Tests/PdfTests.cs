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
            Azienda = "Alfa Srl",
            Dipendente = "Rossi Mario",
            CodiceFiscaleDipendente = "RSSMRA80A01F205X",
            Ccnl = "CCNL Commercio e Terziario",
            Livello = "3",
            Qualifica = "Impiegato",
        }, cartellino);

        Assert.True(IsPdf(pdf));
        Assert.True(pdf.Length > 3_000, "Il riepilogo completo non risulta nel documento PDF.");

        string testo = EstrattoreTestoPdf.EstraiTesto(pdf);
        Assert.Contains("MESE DI RETRIBUZIONE", testo);
        Assert.Contains("INQUADRAMENTO E DATI CONTRATTUALI", testo);
        Assert.Contains("FERIE (GIORNI)", testo);
        Assert.Contains("MALATTIA", testo);
        Assert.Contains("DATI FISCALI DEL MESE", testo);
        Assert.Contains("NETTO IN BUSTA", testo);
        Assert.Contains("BOZZA DA VALIDARE", testo);

        using var documento = PdfDocument.Open(pdf);
        Assert.Single(documento.GetPages());
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
