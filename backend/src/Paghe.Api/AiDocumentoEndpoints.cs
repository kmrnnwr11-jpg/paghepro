using System.Security.Claims;
using System.Text;
using Paghe.Core.Ai;
using Paghe.Documenti;
using Paghe.Persistence;

namespace Paghe.Api;

/// <summary>
/// Legge un documento allegato alla chat di Aurora e ne restituisce il <b>testo</b>, che la SPA
/// invia poi come contesto selezionato del turno successivo. Il file non viene conservato:
/// resta in memoria per il tempo della lettura, esattamente come il testo selezionato a mano.
/// </summary>
public static class AiDocumentoEndpoints
{
    /// <summary>Dimensione massima del singolo allegato.</summary>
    private const long DimensioneMassima = 10 * 1024 * 1024; // 10 MB

    private static readonly string[] EstensioniTesto =
        [".txt", ".csv", ".xml", ".json", ".md", ".log"];

    public static IEndpointRouteBuilder MapAiDocumento(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/ai/documento", async (
            HttpRequest req, IEstrattoreAiDocumenti ai, AiOptions aiOptions,
            ConsumoAiService consumi, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (!req.HasFormContentType)
                return Results.BadRequest(new
                {
                    errore = "Allegare il documento come multipart/form-data (campo 'file').",
                });

            var form = await req.ReadFormAsync(ct);
            var file = form.Files["file"] ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { errore = "Nessun documento allegato." });
            if (file.Length > DimensioneMassima)
                return Results.BadRequest(new { errore = "Documento troppo grande: il limite è 10 MB." });

            var nome = (file.FileName ?? "documento").Trim();
            var nomeMinuscolo = nome.ToLowerInvariant();
            var contentType = file.ContentType ?? "";
            bool èImmagine = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                || nomeMinuscolo.EndsWith(".jpg") || nomeMinuscolo.EndsWith(".jpeg")
                || nomeMinuscolo.EndsWith(".png") || nomeMinuscolo.EndsWith(".webp")
                || nomeMinuscolo.EndsWith(".tif") || nomeMinuscolo.EndsWith(".tiff");
            bool èPdf = nomeMinuscolo.EndsWith(".pdf")
                || contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase);
            bool èTesto = EstensioniTesto.Any(e => nomeMinuscolo.EndsWith(e, StringComparison.Ordinal))
                || contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);

            if (!èImmagine && !èPdf && !èTesto)
                return Results.BadRequest(new
                {
                    errore = "Tipo di documento non supportato: PDF, foto (JPG/PNG) oppure testo "
                           + "(txt, csv, xml, json).",
                });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var dati = ms.ToArray();

            string testo;
            if (èPdf)
            {
                testo = EstrattoreTestoPdf.EstraiTesto(dati);
            }
            else if (èTesto)
            {
                testo = Encoding.UTF8.GetString(dati);
            }
            else
            {
                // Foto/scansione: prima l'OCR locale (gratuito), poi la vista del modello se
                // configurata. Il campo "testo_integrale" chiede la trascrizione fedele: il
                // contratto del seam resta quello dell'estrazione, nessun canale nuovo.
                testo = OcrTesseract.Disponibile ? OcrTesseract.EstraiTesto(dati, "ita") : "";
                if (string.IsNullOrWhiteSpace(testo) && ai.Attivo && ai.VedeImmagini)
                {
                    var tenantId = Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var t)
                        ? t : Guid.Empty;
                    if (await consumi.ProvaConsumaAsync(tenantId, aiOptions.LimiteMensileChiamate, ct))
                    {
                        var mime = string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType;
                        var proposta = await ai.EstraiDaImmagineAsync(
                            "documento fotografato", dati, mime, ["testo_integrale"], ct);
                        testo = proposta?.Campi
                            .FirstOrDefault(c => c.Nome.Contains("testo", StringComparison.OrdinalIgnoreCase))
                            ?.Valore ?? "";
                    }
                }
            }

            testo = (testo ?? "").Replace("\0", "", StringComparison.Ordinal).Trim();
            if (testo.Length == 0)
                return Results.BadRequest(new
                {
                    errore = èImmagine
                        ? "Foto non leggibile: riprova con un'immagine più nitida, oppure allega il PDF."
                        : "Documento non leggibile: se è un PDF scansionato, allegalo come foto (JPG/PNG).",
                });

            if (LocalSecretBoundaryFilter.DeveBloccare(testo))
                return Results.BadRequest(new
                {
                    errore = "Il documento contiene qualcosa che sembra una password o una chiave "
                           + "segreta: non viene inviato all'assistente.",
                });

            bool troncato = testo.Length > AiChatEndpoints.MassimoCaratteriSelezione;
            if (troncato) testo = testo[..AiChatEndpoints.MassimoCaratteriSelezione];

            return Results.Ok(new { nomeFile = nome, testo, troncato });
        })
        .RequireAuthorization()
        .RequireRateLimiting("sensibili")
        .DisableAntiforgery();

        return app;
    }
}
