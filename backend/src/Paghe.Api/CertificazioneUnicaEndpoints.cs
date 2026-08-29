using System.Security.Claims;
using Paghe.Documenti;
using Paghe.Persistence;

namespace Paghe.Api;

/// <summary>
/// Endpoint per la bozza di Certificazione Unica (Fase 1 dichiarazioni), isolato per tenant.
/// Mappato come metodo di estensione per ridurre le modifiche a Program.cs.
/// </summary>
public static class CertificazioneUnicaEndpoints
{
    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;

    public static IEndpointRouteBuilder MapCertificazioneUnica(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/rapporti/{rapportoId:guid}/cu/{anno:int}", async (
            Guid rapportoId, int anno, ClaimsPrincipal user, CertificazioneUnicaService svc) =>
        {
            var bozza = await svc.CostruisciAsync(Tenant(user), rapportoId, anno);
            return bozza is null
                ? Results.NotFound(new { errore = "Rapporto non trovato per il tenant indicato." })
                : Results.Ok(bozza);
        }).RequireAuthorization();

        // PDF della CU sul modello ufficiale AdE (sintetico): anagrafiche, parte B fiscale,
        // previdenza INPS e annotazioni compilate dai cedolini archiviati dell'anno.
        app.MapGet("/api/rapporti/{rapportoId:guid}/cu/{anno:int}/pdf", async (
            Guid rapportoId, int anno, ClaimsPrincipal user, CertificazioneUnicaService svc) =>
        {
            try
            {
                if (anno != CuPdfUfficiale.AnnoRedditiSupportato)
                    return Results.BadRequest(new
                    {
                        errore = $"Il PDF ufficiale CU 2026 è disponibile soltanto per i redditi {CuPdfUfficiale.AnnoRedditiSupportato}. "
                            + $"Il modello ufficiale per i redditi {anno} non è disponibile.",
                    });
                var cu = await svc.CostruisciModelloAsync(Tenant(user), rapportoId, anno);
                if (cu is null)
                    return Results.NotFound(new { errore = "Rapporto non trovato per il tenant indicato." });
                var errori = cu.Valida();
                if (errori.Count > 0)
                    return Results.BadRequest(new { errori });
                byte[] pdf = CuPdfUfficiale.Compila(cu);
                string cognome = string.Concat(cu.Percipiente.Cognome.Split(Path.GetInvalidFileNameChars()))
                    .Replace(' ', '-');
                return Results.File(pdf, "application/pdf", $"CU-{anno}-{cognome}.pdf");
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { errore = ex.Message });
            }
        }).RequireAuthorization();

        // Tutte le CU dei dipendenti di un'azienda per l'anno.
        app.MapGet("/api/aziende/{aziendaId:guid}/cu/{anno:int}", async (
            Guid aziendaId, int anno, ClaimsPrincipal user, CertificazioneUnicaService svc) =>
            Results.Ok(await svc.CostruisciPerAziendaAsync(Tenant(user), aziendaId, anno)))
            .RequireAuthorization();

        // File telematico ufficiale CUR26 (tutte le CU dell'azienda per l'anno) con gli avvisi
        // sui dati mancanti. Il file va passato dal controllo ufficiale AdE prima dell'invio.
        app.MapGet("/api/aziende/{aziendaId:guid}/cu/{anno:int}/telematico", async (
            Guid aziendaId, int anno, ClaimsPrincipal user, CertificazioneUnicaService svc) =>
        {
            try
            {
                var esito = await svc.CostruisciTracciatoTelematicoAsync(
                    Tenant(user), aziendaId, anno, DateOnly.FromDateTime(DateTime.Today));
                return esito is null
                    ? Results.NotFound(new { errore = "Azienda non trovata per il tenant indicato." })
                    : Results.Ok(esito);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { errore = ex.Message });
            }
        }).RequireAuthorization();

        // Export CSV (bozza rivedibile) delle CU dell'azienda per l'anno.
        app.MapGet("/api/aziende/{aziendaId:guid}/cu/{anno:int}/export.csv", async (
            Guid aziendaId, int anno, ClaimsPrincipal user, CertificazioneUnicaService svc) =>
        {
            var bozze = await svc.CostruisciPerAziendaAsync(Tenant(user), aziendaId, anno);
            return Results.Text(CertificazioneUnicaService.EsportaCsv(bozze), "text/csv");
        }).RequireAuthorization();

        return app;
    }
}
