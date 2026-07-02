using Paghe.Core.PartiteIva;
using Paghe.Documenti;

namespace Paghe.Api;

/// <summary>
/// Endpoint: l'utente carica la <b>visura camerale</b> come <b>PDF testuale</b> oppure come
/// <b>foto/scansione</b> (JPG/PNG → letta via OCR Tesseract locale). Il sistema estrae i dati
/// anagrafici dell'azienda e li restituisce per la <b>conferma</b> (il frontend precompila il form).
/// Non crea nulla da solo: i dati estratti vanno sempre controllati.
/// </summary>
public static class VisuraPdfEndpoints
{
    /// <summary>Dimensione massima accettata per il file caricato (una visura sta ampiamente sotto).</summary>
    private const long DimensioneMassima = 10 * 1024 * 1024; // 10 MB

    public static IEndpointRouteBuilder MapImportVisuraPdf(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/aziende/da-visura", async (HttpRequest req) =>
        {
            if (!req.HasFormContentType)
                return Results.BadRequest(new { errore = "Caricare il PDF come multipart/form-data (campo 'file')." });

            var form = await req.ReadFormAsync();
            var file = form.Files["file"] ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { errore = "Allegare il PDF della visura nel campo 'file'." });
            if (file.Length > DimensioneMassima)
                return Results.BadRequest(new { errore = "File troppo grande: il limite è 10 MB." });

            var nome = (file.FileName ?? "").ToLowerInvariant();
            var èImmagine = (file.ContentType ?? "").StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                || nome.EndsWith(".jpg") || nome.EndsWith(".jpeg") || nome.EndsWith(".png")
                || nome.EndsWith(".webp") || nome.EndsWith(".tif") || nome.EndsWith(".tiff");
            var èPdf = nome.EndsWith(".pdf")
                || (file.ContentType ?? "").Contains("pdf", StringComparison.OrdinalIgnoreCase);
            if (!èImmagine && !èPdf)
                return Results.BadRequest(new
                {
                    errore = "Formato non supportato: caricare un PDF oppure una foto/scansione (JPG, PNG).",
                });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var dati = ms.ToArray();

            string testo;
            if (èImmagine)
            {
                // Foto/scansione → OCR locale con Tesseract (italiano), nessuna API esterna.
                if (!OcrTesseract.Disponibile)
                    return Results.Json(new
                    {
                        errore = "Lettura da foto non disponibile: manca il motore OCR. " +
                                 "Installa Tesseract (macOS: 'brew install tesseract tesseract-lang').",
                    }, statusCode: 501);
                testo = OcrTesseract.EstraiTesto(dati, "ita");
            }
            else
            {
                testo = EstrattoreTestoPdf.EstraiTesto(dati);
            }

            if (string.IsNullOrWhiteSpace(testo))
                return Results.BadRequest(new
                {
                    errore = "Documento non leggibile. Se è un PDF scansionato, caricalo come foto/immagine " +
                             "(JPG/PNG) per usare l'OCR; se è una foto, riprova con un'immagine più nitida.",
                });

            var d = VisuraCameraleParser.Estrai(testo);
            return Results.Ok(new
            {
                d.RagioneSociale,
                d.PartitaIva,
                d.CodiceFiscale,
                d.Indirizzo,
                d.Cap,
                d.Comune,
                d.Provincia,
                d.CodiceAteco,
                d.LegaleRappresentante,
                nota = "Dati estratti automaticamente dalla visura: controllali e correggili prima di salvare.",
            });
        })
        .RequireAuthorization()
        .DisableAntiforgery();

        return app;
    }
}
