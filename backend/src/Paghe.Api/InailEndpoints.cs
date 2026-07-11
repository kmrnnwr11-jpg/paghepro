using System.Security.Claims;
using Paghe.Persistence;

namespace Paghe.Api;

public sealed record SalvaPosizioneInailRequest(
    int Anno, string Pat, string VoceTariffa, decimal TassoPerMille);

/// <summary>
/// Endpoint per la posizione INAIL (PAT + tasso) e la bozza di autoliquidazione, isolati per
/// tenant. Mappato come metodo di estensione per ridurre le modifiche a Program.cs.
/// </summary>
public static class InailEndpoints
{
    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;

    public static IEndpointRouteBuilder MapInail(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/aziende/{aziendaId:guid}/inail/posizione", async (
            Guid aziendaId, int anno, ClaimsPrincipal user, InailService svc) =>
        {
            var p = await svc.GetPosizioneAsync(Tenant(user), aziendaId, anno);
            return p is null
                ? Results.NotFound(new { errore = $"Posizione INAIL non registrata per l'anno {anno}." })
                : Results.Ok(new { p.Anno, p.Pat, p.VoceTariffa, p.TassoPerMille });
        }).RequireAuthorization();

        app.MapPost("/api/aziende/{aziendaId:guid}/inail/posizione", async (
            Guid aziendaId, SalvaPosizioneInailRequest req, ClaimsPrincipal user, InailService svc) =>
        {
            try
            {
                var p = await svc.SalvaPosizioneAsync(
                    Tenant(user), aziendaId, req.Anno, req.Pat, req.VoceTariffa, req.TassoPerMille);
                return Results.Ok(new { p.Anno, p.Pat, p.VoceTariffa, p.TassoPerMille });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { errore = ex.Message });
            }
        }).RequireAuthorization();

        app.MapGet("/api/aziende/{aziendaId:guid}/inail/autoliquidazione", async (
            Guid aziendaId, int anno, decimal? rataVersata, ClaimsPrincipal user, InailService svc) =>
        {
            try
            {
                return Results.Ok(await svc.CalcolaAutoliquidazioneAsync(
                    Tenant(user), aziendaId, anno, rataVersata ?? 0m));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { errore = ex.Message });
            }
        }).RequireAuthorization();

        return app;
    }
}
