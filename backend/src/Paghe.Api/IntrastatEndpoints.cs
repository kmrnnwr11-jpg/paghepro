using System.Security.Claims;
using Paghe.Persistence;

namespace Paghe.Api;

/// <summary>
/// Endpoint per la bozza Intrastat (Fase 6 dichiarazioni), isolato per tenant.
/// Mappato come metodo di estensione per ridurre le modifiche a Program.cs.
/// </summary>
public static class IntrastatEndpoints
{
    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;

    public static IEndpointRouteBuilder MapIntrastat(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/aziende/{aziendaId:guid}/intrastat/{anno:int}", async (
            Guid aziendaId, int anno, ClaimsPrincipal user, IntrastatService svc) =>
            Results.Ok(await svc.CostruisciAsync(Tenant(user), aziendaId, anno)))
            .RequireAuthorization();

        return app;
    }
}
