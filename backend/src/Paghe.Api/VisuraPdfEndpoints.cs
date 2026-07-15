using System.Security.Claims;
using Paghe.Core.Ai;
using Paghe.Core.PartiteIva;
using Paghe.Documenti;
using Paghe.Persistence;

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
        app.MapPost("/api/aziende/da-visura", async (
            HttpRequest req, IEstrattoreAiDocumenti ai, AiOptions aiOptions,
            ConsumoAiService consumi, ClaimsPrincipal user) =>
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

            // La vista AI (se configurata) legge le foto direttamente: l'OCR locale resta
            // il primo tentativo gratuito, ma la sua assenza non è più bloccante.
            bool vistaDisponibile = èImmagine && ai.Attivo && ai.VedeImmagini;

            string testo;
            if (èImmagine)
            {
                // Foto/scansione → OCR locale con Tesseract (italiano), nessuna API esterna.
                if (!OcrTesseract.Disponibile && !vistaDisponibile)
                    return Results.Json(new
                    {
                        errore = "Lettura da foto non disponibile: manca il motore OCR. " +
                                 "Installa Tesseract (macOS: 'brew install tesseract tesseract-lang').",
                    }, statusCode: 501);
                testo = OcrTesseract.Disponibile ? OcrTesseract.EstraiTesto(dati, "ita") : "";
            }
            else
            {
                testo = EstrattoreTestoPdf.EstraiTesto(dati);
            }

            if (string.IsNullOrWhiteSpace(testo) && !vistaDisponibile)
                return Results.BadRequest(new
                {
                    errore = "Documento non leggibile. Se è un PDF scansionato, caricalo come foto/immagine " +
                             "(JPG/PNG) per usare l'OCR; se è una foto, riprova con un'immagine più nitida.",
                });

            // 1) Parser deterministico (vince sempre); 2) AI solo sui campi rimasti vuoti,
            // se configurata E se lo studio è dentro il limite mensile del canone.
            // Sulle foto la vista AI legge direttamente l'immagine (vede anche il layout);
            // sui PDF testuali l'AI lavora sul testo. L'AI propone: la conferma resta all'utente.
            var d = VisuraCameraleParser.Estrai(testo);
            IReadOnlyList<string> campiDallAi = [];
            bool limiteRaggiunto = false;
            var mancanti = CompletamentoVisuraAi.CampiMancanti(d);
            if (ai.Attivo && mancanti.Count > 0
                && (vistaDisponibile || !string.IsNullOrWhiteSpace(testo)))
            {
                var tenantId = Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var t)
                    ? t : Guid.Empty;
                if (await consumi.ProvaConsumaAsync(tenantId, aiOptions.LimiteMensileChiamate))
                {
                    try
                    {
                        var mime = string.IsNullOrWhiteSpace(file.ContentType)
                            ? "image/jpeg" : file.ContentType;
                        var proposta = vistaDisponibile
                            ? await ai.EstraiDaImmagineAsync("visura camerale", dati, mime, mancanti)
                            : await ai.EstraiAsync("visura camerale", testo, mancanti);
                        if (proposta is not null)
                            (d, campiDallAi) = CompletamentoVisuraAi.Completa(
                                d, proposta, aiOptions.SogliaConfidenza);
                    }
                    catch (Exception)
                    {
                        // L'AI non deve mai bloccare il flusso: si resta sull'estrazione euristica.
                    }
                }
                else
                {
                    limiteRaggiunto = true;
                }
            }

            string nota = campiDallAi.Count > 0
                ? "Dati estratti dalla visura. I campi "
                  + string.Join(", ", campiDallAi)
                  + " sono proposti dall'AI: controllali con particolare attenzione prima di salvare."
                : limiteRaggiunto
                    ? "Dati estratti con il solo lettore automatico di base: il limite mensile "
                      + "di letture AI del piano è stato raggiunto. Controlla e completa i campi vuoti."
                    : "Dati estratti automaticamente dalla visura: controllali e correggili prima di salvare.";

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
                nota,
            });
        })
        .RequireAuthorization()
        .DisableAntiforgery();

        return app;
    }
}
