using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Paghe.Documenti;

/// <summary>
/// Estrae il testo da un PDF <b>testuale</b> (con layer di testo). Usa PdfPig (Apache-2.0) con
/// estrazione in ordine di lettura, così etichette e valori restano vicini per il parsing.
/// Per i PDF <b>scansionati</b> (sola immagine) il testo è vuoto: chi chiama deve avvisare l'utente.
/// </summary>
public static class EstrattoreTestoPdf
{
    public static string EstraiTesto(byte[] pdf)
    {
        if (pdf is null || pdf.Length == 0)
            return "";

        using var doc = PdfDocument.Open(pdf);
        var sb = new StringBuilder();
        foreach (var pagina in doc.GetPages())
            sb.AppendLine(ContentOrderTextExtractor.GetText(pagina));
        return sb.ToString();
    }
}
