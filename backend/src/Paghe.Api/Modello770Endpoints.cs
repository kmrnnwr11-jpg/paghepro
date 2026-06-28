using System.Security.Claims;
using Paghe.Persistence;

namespace Paghe.Api;

/// <summary>
/// Endpoint per la bozza Modello 770 (Fase 2 dichiarazioni), isolato per tenant.
/// Mappato come metodo di estensione per ridurre le modifiche a Program.cs.
/// </summary>
public static class Modello770Endpoints
{
    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;

    public static IEndpointRouteBuilder MapModello770(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/aziende/{aziendaId:guid}/modello770/{anno:int}", async (
            Guid aziendaId, int anno, ClaimsPrincipal user, Modello770Service svc) =>
        {
            var bozza = await svc.CostruisciAsync(Tenant(user), aziendaId, anno);
            return Results.Ok(bozza);
        }).RequireAuthorization();

        return app;
    }
}
