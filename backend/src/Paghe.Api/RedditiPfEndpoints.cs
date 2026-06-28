using System.Security.Claims;
using Paghe.Persistence;

namespace Paghe.Api;

/// <summary>Richiesta di salvataggio degli input manuali della dichiarazione Redditi PF.</summary>
public sealed record SalvaRedditiPfRequest(
    decimal AltriRedditi, decimal OneriDeducibili, decimal OneriDetraibili19, decimal Acconti);

/// <summary>
/// Endpoint per la bozza Redditi PF / 730 (Fase 4 dichiarazioni), isolato per tenant.
/// Mappato come metodo di estensione per ridurre le modifiche a Program.cs.
/// </summary>
public static class RedditiPfEndpoints
{
    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;

    public static IEndpointRouteBuilder MapRedditiPf(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/rapporti/{rapportoId:guid}/redditi-pf/{anno:int}", async (
            Guid rapportoId, int anno, ClaimsPrincipal user, RedditiPfService svc) =>
        {
            var bozza = await svc.CostruisciBozzaAsync(Tenant(user), rapportoId, anno);
            return bozza is null
                ? Results.NotFound(new { errore = "Rapporto non trovato per il tenant indicato." })
                : Results.Ok(bozza);
        }).RequireAuthorization();

        app.MapPut("/api/rapporti/{rapportoId:guid}/redditi-pf/{anno:int}", async (
            Guid rapportoId, int anno, SalvaRedditiPfRequest req, ClaimsPrincipal user, RedditiPfService svc) =>
        {
            var bozza = await svc.SalvaInputAsync(Tenant(user), rapportoId, anno,
                new RedditiPfInput(req.AltriRedditi, req.OneriDeducibili, req.OneriDetraibili19, req.Acconti));
            return Results.Ok(bozza);
        }).RequireAuthorization();

        return app;
    }
}
