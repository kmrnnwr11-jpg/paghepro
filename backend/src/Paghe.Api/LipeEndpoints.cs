using System.Security.Claims;
using Paghe.Persistence;

namespace Paghe.Api;

/// <summary>
/// Endpoint per la bozza LIPE (Fase 3 dichiarazioni), isolato per tenant.
/// Mappato come metodo di estensione per ridurre le modifiche a Program.cs.
/// </summary>
public static class LipeEndpoints
{
    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;

    public static IEndpointRouteBuilder MapLipe(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/aziende/{aziendaId:guid}/lipe/{anno:int}/{trimestre:int}", async (
            Guid aziendaId, int anno, int trimestre, ClaimsPrincipal user, LipeService svc) =>
        {
            var bozza = await svc.CostruisciAsync(Tenant(user), aziendaId, anno, trimestre);
            return Results.Ok(bozza);
        }).RequireAuthorization();

        app.MapGet("/api/aziende/{aziendaId:guid}/iva-annuale/{anno:int}", async (
            Guid aziendaId, int anno, ClaimsPrincipal user, LipeService svc) =>
        {
            var bozza = await svc.CostruisciAnnualeAsync(Tenant(user), aziendaId, anno);
            return Results.Ok(bozza);
        }).RequireAuthorization();

        return app;
    }
}
