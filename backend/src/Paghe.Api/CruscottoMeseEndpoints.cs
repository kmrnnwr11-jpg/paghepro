using System.Security.Claims;
using Paghe.Persistence;

namespace Paghe.Api;

/// <summary>
/// Cruscotto del mese multi-azienda (semafori dello studio): sola lettura, tenant dal token.
/// Mappato come metodo di estensione per ridurre le modifiche a Program.cs.
/// </summary>
public static class CruscottoMeseEndpoints
{
    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;

    public static IEndpointRouteBuilder MapCruscottoMese(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/cruscotto-mese/{anno:int}/{mese:int}", async (
            int anno, int mese, ClaimsPrincipal user,
            CruscottoMeseService servizio, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await servizio.CostruisciAsync(Tenant(user), anno, mese, ct));
            }
            catch (ArgumentException e)
            {
                return Results.BadRequest(new { errore = e.Message });
            }
        }).RequireAuthorization();

        // «Elabora i pronti»: i rapporti col cartellino salvato e senza cedolino passano
        // uno per uno dal percorso di elaborazione verificato; gli esiti tornano per riga.
        app.MapPost("/api/aziende/{aziendaId:guid}/elabora-pronti/{anno:int}/{mese:int}", async (
            Guid aziendaId, int anno, int mese, ClaimsPrincipal user,
            ElaborazioneMassivaService servizio, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await servizio.ElaboraProntiAsync(
                    Tenant(user), aziendaId, anno, mese, ct));
            }
            catch (ArgumentException e)
            {
                return Results.BadRequest(new { errore = e.Message });
            }
        }).RequireAuthorization();

        return app;
    }
}
