using System.Text;
using Paghe.Core.Calcolo;
using Paghe.Core.Domain;
using Paghe.Core.Fatturazione;
using Paghe.Core.Parametri;
using Paghe.Documenti;
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
