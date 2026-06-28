using System.Security.Claims;
using Paghe.Persistence;

namespace Paghe.Api;

/// <summary>
/// Endpoint per la bozza IRAP (Fase 5 dichiarazioni), isolato per tenant.
/// Mappato come metodo di estensione per ridurre le modifiche a Program.cs.
/// </summary>
public static class IrapEndpoints
{
    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;

    public static IEndpointRouteBuilder MapIrap(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/aziende/{aziendaId:guid}/irap/{anno:int}", async (
            Guid aziendaId, int anno, ClaimsPrincipal user, IrapService svc) =>
            Results.Ok(await svc.CostruisciAsync(Tenant(user), aziendaId, anno)))
            .RequireAuthorization();

        return app;
    }
}
