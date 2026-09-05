using System.Security.Claims;
using Paghe.Persistence.Ai;

namespace Paghe.Api;

public sealed record AggiornaImpostazioneCronologiaAurora(bool Attiva);

/// <summary>CRUD del workspace recente Aurora; contenuti e titoli non sono mai memorizzati nel browser.</summary>
public static class AuroraConversazioniEndpoints
{
    public static IEndpointRouteBuilder MapAuroraConversazioni(this IEndpointRouteBuilder app)
    {
        var gruppo = app.MapGroup("/api/ai/conversazioni")
            .RequireAuthorization()
            .RequireRateLimiting("sensibili");

        gruppo.MapGet("/impostazioni", async (
            AuroraConversazioniService servizio, ClaimsPrincipal user, HttpContext http,
            CancellationToken ct) =>
        {
            NoStore(http);
            return Results.Ok(await servizio.ImpostazioneAsync(
                Tenant(user), Utente(user), ct));
        });

        gruppo.MapPut("/impostazioni", async (
            AggiornaImpostazioneCronologiaAurora richiesta,
            AuroraConversazioniService servizio, ClaimsPrincipal user, HttpContext http,
            CancellationToken ct) =>
        {
            NoStore(http);
            return Results.Ok(await servizio.ImpostaAsync(
                Tenant(user), Utente(user), richiesta.Attiva, ct));
        });

        gruppo.MapDelete("/impostazioni", async (
            AuroraConversazioniService servizio, ClaimsPrincipal user, HttpContext http,
            CancellationToken ct) =>
        {
            NoStore(http);
            return Results.Ok(await servizio.ImpostaAsync(
                Tenant(user), Utente(user), false, ct));
        });

        gruppo.MapPost("/", async (
            CreaConversazioneAurora richiesta, AuroraConversazioniService servizio,
            ClaimsPrincipal user, HttpContext http, CancellationToken ct) =>
        {
            NoStore(http);
            try
            {
                var creata = await servizio.CreaAsync(Tenant(user), Utente(user), richiesta, ct);
                return Results.Created($"/api/ai/conversazioni/{creata.Id:D}", creata);
            }
            catch (CronologiaAuroraDisattivataException ex)
            {
                return Results.Conflict(new { errore = ex.Message });
            }
            catch (LimiteAuroraRaggiuntoException ex)
            {
                return Results.Conflict(new { errore = ex.Message });
            }
            catch (ConversazioneAuroraConflittoException ex)
            {
                return Results.Conflict(new { errore = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { errore = ex.Message });
            }
        });

        gruppo.MapGet("/", async (
            bool? archiviate, AuroraConversazioniService servizio,
            ClaimsPrincipal user, HttpContext http, CancellationToken ct) =>
        {
            NoStore(http);
            return Results.Ok(await servizio.ElencoAsync(
                Tenant(user), Utente(user), archiviate == true, ct));
        });

        gruppo.MapGet("/{id:guid}", async (
            Guid id, AuroraConversazioniService servizio, ClaimsPrincipal user,
            HttpContext http, CancellationToken ct) =>
        {
            NoStore(http);
            var dettaglio = await servizio.DettaglioAsync(Tenant(user), Utente(user), id, ct);
            return dettaglio is null
                ? Results.NotFound(new { errore = "Conversazione non trovata." })
                : Results.Ok(dettaglio);
        });

        gruppo.MapPatch("/{id:guid}", async (
            Guid id, ModificaConversazioneAurora richiesta,
            AuroraConversazioniService servizio, ClaimsPrincipal user,
            HttpContext http, CancellationToken ct) =>
        {
            NoStore(http);
            try
            {
                var modificata = await servizio.ModificaAsync(
                    Tenant(user), Utente(user), id, richiesta, ct);
                return modificata is null
                    ? Results.NotFound(new { errore = "Conversazione non trovata." })
                    : Results.Ok(modificata);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { errore = ex.Message });
            }
        });

        gruppo.MapDelete("/{id:guid}", async (
            Guid id, AuroraConversazioniService servizio, ClaimsPrincipal user,
            HttpContext http, CancellationToken ct) =>
        {
            NoStore(http);
            return await servizio.EliminaAsync(Tenant(user), Utente(user), id, ct)
                ? Results.NoContent()
                : Results.NotFound(new { errore = "Conversazione non trovata." });
        });

        return app;
    }

    private static void NoStore(HttpContext http) =>
        http.Response.Headers.CacheControl = "no-store";

    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;

    private static Guid Utente(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : Guid.Empty;
}
