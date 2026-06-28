using System.Security.Claims;
using Paghe.Persistence;

namespace Paghe.Api;

/// <summary>
/// Endpoint per il cruscotto presenze del mese (scheda Presenze reale), isolato per tenant.
/// Mappato come metodo di estensione per ridurre le modifiche a Program.cs.
/// </summary>
public static class PresenzeEndpoints
{
    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;

    public static IEndpointRouteBuilder MapPresenze(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/aziende/{aziendaId:guid}/presenze/{anno:int}/{mese:int}", async (
            Guid aziendaId, int anno, int mese, ClaimsPrincipal user, PresenzeService svc) =>
        {
            var board = await svc.CostruisciAsync(Tenant(user), aziendaId, anno, mese);
            return Results.Ok(board);
        }).RequireAuthorization();

        return app;
    }
}
