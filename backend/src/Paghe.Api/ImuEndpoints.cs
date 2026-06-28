using System.Security.Claims;
using Paghe.Persistence;

namespace Paghe.Api;

public sealed record AggiungiImmobileRequest(
    string Descrizione, string CategoriaCatastale, decimal RenditaCatastale,
    decimal PercentualePossesso, int MesiPossesso, decimal AliquotaPerMille, bool AbitazionePrincipale);

/// <summary>
/// Endpoint per anagrafica immobili e bozza IMU (Fase 6 dichiarazioni), isolato per tenant.
/// Mappato come metodo di estensione per ridurre le modifiche a Program.cs.
/// </summary>
public static class ImuEndpoints
{
    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;

    public static IEndpointRouteBuilder MapImu(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/aziende/{aziendaId:guid}/immobili", async (
            Guid aziendaId, ClaimsPrincipal user, ImuService svc) =>
            Results.Ok(await svc.ElencoAsync(Tenant(user), aziendaId)))
            .RequireAuthorization();

        app.MapPost("/api/aziende/{aziendaId:guid}/immobili", async (
            Guid aziendaId, AggiungiImmobileRequest req, ClaimsPrincipal user, ImuService svc) =>
        {
            var e = await svc.AggiungiAsync(Tenant(user), aziendaId, new ImmobileInput(
                req.Descrizione, req.CategoriaCatastale, req.RenditaCatastale,
                req.PercentualePossesso <= 0 ? 100m : req.PercentualePossesso,
                req.MesiPossesso <= 0 ? 12 : req.MesiPossesso,
                req.AliquotaPerMille <= 0 ? 8.6m : req.AliquotaPerMille,
                req.AbitazionePrincipale));
            return Results.Created($"/api/immobili/{e.Id}", new { e.Id });
        }).RequireAuthorization();

        app.MapDelete("/api/immobili/{id:guid}", async (Guid id, ClaimsPrincipal user, ImuService svc) =>
            await svc.EliminaAsync(Tenant(user), id) ? Results.NoContent() : Results.NotFound())
            .RequireAuthorization();

        app.MapGet("/api/aziende/{aziendaId:guid}/imu/{anno:int}", async (
            Guid aziendaId, int anno, ClaimsPrincipal user, ImuService svc) =>
            Results.Ok(await svc.CalcolaAsync(Tenant(user), aziendaId, anno)))
            .RequireAuthorization();

        return app;
    }
}
