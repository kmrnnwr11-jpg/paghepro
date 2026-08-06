using System.Security.Claims;
using Paghe.Documenti;
using Paghe.Persistence;

namespace Paghe.Api;

/// <summary>
/// Endpoint: l'utente carica il <b>PDF UNILAV</b>, il sistema ne legge i dati e registra in automatico
/// il <b>datore di lavoro</b> (azienda), il <b>lavoratore</b> e il <b>rapporto</b>. Isolato per tenant.
/// Mappato come metodo di estensione per ridurre le modifiche a Program.cs.
/// </summary>
public static class UnilavPdfEndpoints
{
    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;

    /// <summary>Dimensione massima accettata per il PDF caricato (un UNILAV sta ampiamente sotto).</summary>
    private const long DimensioneMassima = 10 * 1024 * 1024; // 10 MB

    public static IEndpointRouteBuilder MapImportUnilavPdf(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/unilav/pdf", async (HttpRequest req, ClaimsPrincipal user, PagheDbContext db) =>
        {
            if (!req.HasFormContentType)
                return Results.BadRequest(new { errore = "Caricare il PDF come multipart/form-data (campo 'file')." });

            var form = await req.ReadFormAsync();
            var file = form.Files["file"] ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { errore = "Allegare il file PDF UNILAV nel campo 'file'." });
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
                    errore = "Formato non supportato: caricare il PDF dell'UNILAV oppure una foto/scansione (JPG, PNG).",
                });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            string testo;
            if (èImmagine)
            {
                // Foto/scansione → OCR locale con Tesseract (italiano), come per la visura.
                if (!OcrTesseract.Disponibile)
                    return Results.Json(new
                    {
                        errore = "Lettura da foto non disponibile: manca il motore OCR. " +
                                 "Installa Tesseract (macOS: 'brew install tesseract tesseract-lang').",
                    }, statusCode: 501);
                testo = OcrTesseract.EstraiTesto(ms.ToArray(), "ita");
            }
            else
            {
                testo = EstrattoreTestoPdf.EstraiTesto(ms.ToArray());
            }

            if (string.IsNullOrWhiteSpace(testo))
                return Results.BadRequest(new
                {
                    errore = "Documento non leggibile. Se è un PDF scansionato, caricalo come foto/immagine " +
                             "(JPG/PNG) per usare l'OCR; se è una foto, riprova con un'immagine più nitida.",
                });

            try
            {
                // Azienda scelta dall'utente (opzionale): se assente, il backend trova/crea il datore del PDF.
                Guid? aziendaId = Guid.TryParse(form["aziendaId"], out var aid) ? aid : null;
                var esito = await new ImportUnilavService(db)
                    .ImportaDaTestoAsync(Tenant(user), testo, form["ccnlDefault"], form["livelloDefault"], aziendaId);
                return Results.Ok(Risposta(esito));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { errore = ex.Message });
            }
        })
        .RequireAuthorization()
        .DisableAntiforgery();

        return app;
    }

    /// <summary>Testo dell'UNILAV incollato dall'utente (copiato dalla comunicazione o da un riepilogo).</summary>
    public sealed record ImportUnilavTestoRequest(
        string Testo, Guid? AziendaId, string? CcnlDefault, string? LivelloDefault);

    /// <summary>Lunghezza massima del testo incollato: un UNILAV sta ampiamente sotto.</summary>
    private const int CaratteriMassimi = 200_000;

    public static IEndpointRouteBuilder MapImportUnilavTesto(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/unilav/testo", async (
            ImportUnilavTestoRequest req, ClaimsPrincipal user, PagheDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Testo))
                return Results.BadRequest(new { errore = "Incollare il testo della comunicazione UNILAV." });
            if (req.Testo.Length > CaratteriMassimi)
                return Results.BadRequest(new { errore = "Testo troppo lungo: incollare solo la comunicazione." });

            try
            {
                var esito = await new ImportUnilavService(db).ImportaDaTestoAsync(
                    Tenant(user), req.Testo, req.CcnlDefault, req.LivelloDefault, req.AziendaId);
                return Results.Ok(Risposta(esito));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { errore = ex.Message });
            }
        })
        .RequireAuthorization();

        return app;
    }

    /// <summary>Risposta comune ai due ingressi (PDF/foto e testo incollato).</summary>
    private static object Risposta(EsitoImportUnilav esito)
    {
        var d = esito.Rapporto.Dipendente!;
        return new
        {
            azienda = new
            {
                esito.Azienda.Id,
                esito.Azienda.RagioneSociale,
                esito.Azienda.PartitaIva,
                esito.Azienda.CodiceFiscale,
                creata = esito.AziendaCreata,
            },
            rapporto = new
            {
                esito.Rapporto.Id,
                dipendente = new { d.Id, d.Nome, d.Cognome, d.CodiceFiscale },
                esito.Rapporto.CodiceCcnl,
                esito.Rapporto.Livello,
                esito.Rapporto.Qualifica,
                esito.Rapporto.TipoContratto,
                esito.Rapporto.PartTime,
                esito.Rapporto.PercentualePartTime,
                esito.Rapporto.OreSettimanali,
                dataAssunzione = esito.Rapporto.DataAssunzione.ToString("yyyy-MM-dd"),
                dataCessazione = esito.Rapporto.DataCessazione?.ToString("yyyy-MM-dd"),
                lavoratoreGiaPresente = esito.LavoratoreEsistente,
            },
            avvertenze = esito.Avvertenze,
            daCompletare = esito.DaCompletare.Select(c => new
            {
                c.Campo, c.Dove, c.RiferimentoId, c.Perche, c.DoveTrovarlo,
            }),
        };
    }
}
