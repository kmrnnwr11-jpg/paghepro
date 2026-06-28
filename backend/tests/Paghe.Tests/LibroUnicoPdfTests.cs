using System.Text;
using Paghe.Core.Calcolo;
using Paghe.Core.Domain;
using Paghe.Core.Parametri;
using Paghe.Documenti;
using Xunit;

namespace Paghe.Tests;

/// <summary>Test della generazione del Libro Unico del Lavoro (LUL) in PDF.</summary>
public sealed class LibroUnicoPdfTests
{
    private static bool IsPdf(byte[] bytes) =>
        bytes.Length > 4 && Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-";

    [Fact]
    public void LulPdf_ProduceUnPdfValido()
    {
        var cartellino = new Cartellino
        {
            Anno = 2026, Mese = 6, GiorniFerie = 2m, OrePermessi = 4m, OreStraordinario = 8m,
        };
        var cedolino = new MotoreCalcoloCedolino(Seed.Parametri2026(), Seed.CcnlCommercio2026())
            .Calcola(
                new RapportoLavoro
                {
                    CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = Qualifica.Impiegato,
                    Regione = "Lombardia", CodiceCatastaleComune = "F205",
                },
                cartellino);

        byte[] pdf = LibroUnicoPdf.Genera(cartellino, cedolino, "Alfa Srl", "Mario Rossi", "RSSMRA80A01F205X");

        Assert.True(IsPdf(pdf), "Il file generato non inizia con l'header %PDF-.");
        Assert.True(pdf.Length > 1000, "Il PDF è troppo piccolo per essere valido.");
    }
}
