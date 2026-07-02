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
            if (!nome.EndsWith(".pdf")
                && !(file.ContentType ?? "").Contains("pdf", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { errore = "Formato non supportato: caricare il PDF dell'UNILAV." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            string testo = EstrattoreTestoPdf.EstraiTesto(ms.ToArray());
            if (string.IsNullOrWhiteSpace(testo))
                return Results.BadRequest(new
                {
                    errore = "Il PDF non contiene testo leggibile (probabile scansione/immagine). " +
                             "Serve un UNILAV in PDF testuale, oppure usa l'import da XML.",
                });

            try
            {
                // Azienda scelta dall'utente (opzionale): se assente, il backend trova/crea il datore del PDF.
                Guid? aziendaId = Guid.TryParse(form["aziendaId"], out var aid) ? aid : null;
                var esito = await new ImportUnilavService(db)
                    .ImportaDaTestoAsync(Tenant(user), testo, form["ccnlDefault"], form["livelloDefault"], aziendaId);

                var d = esito.Rapporto.Dipendente!;
                return Results.Ok(new
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
                        dataAssunzione = esito.Rapporto.DataAssunzione.ToString("yyyy-MM-dd"),
                        lavoratoreGiaPresente = esito.LavoratoreEsistente,
                    },
                    avvertenze = esito.Avvertenze,
                });
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
}
