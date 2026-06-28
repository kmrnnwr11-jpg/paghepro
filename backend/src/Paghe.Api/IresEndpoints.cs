using System.Security.Claims;
using Paghe.Persistence;

namespace Paghe.Api;

/// <summary>
/// Endpoint per la bozza IRES (Fase 5 dichiarazioni), isolato per tenant.
/// Mappato come metodo di estensione per ridurre le modifiche a Program.cs.
/// </summary>
public static class IresEndpoints
{
    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;

    public static IEndpointRouteBuilder MapIres(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/aziende/{aziendaId:guid}/ires/{anno:int}", async (
            Guid aziendaId, int anno, decimal? variazioni, ClaimsPrincipal user, IresService svc) =>
            Results.Ok(await svc.CostruisciAsync(Tenant(user), aziendaId, anno, variazioni ?? 0m)))
            .RequireAuthorization();

        return app;
    }
}
