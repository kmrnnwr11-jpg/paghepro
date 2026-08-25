using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Cedolini;

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

        // Controllo del calendario prima di elaborare: le regole stanno solo qui, nel dominio,
        // così la schermata non ne tiene una copia che col tempo direbbe cose diverse.
        app.MapPost("/api/rapporti/{rapportoId:guid}/cartellino/verifica", async (
            Guid rapportoId, Cartellino cartellino, ClaimsPrincipal user,
            PagheDbContext db, CancellationToken ct) =>
        {
            var tenantId = Tenant(user);
            var rapporto = await db.Rapporti.FirstOrDefaultAsync(
                r => r.Id == rapportoId && r.TenantId == tenantId, ct);
            if (rapporto is null)
                return Results.NotFound(new { errore = "Rapporto di lavoro non trovato." });

            decimal? riportoPrimaSettimana = await RiportoOreSettimanaliService.CalcolaAsync(
                db, tenantId, rapportoId, cartellino.Anno, cartellino.Mese,
                rapporto.DataAssunzione, ct);
            var problemi = CalendarioCartellino.Verifica(
                cartellino.Anno, cartellino.Mese, cartellino.Giornate,
                rapporto.OreSettimanali / 5m,
                oreLavoratePregressePrimaSettimana: riportoPrimaSettimana);

            return Results.Ok(new
            {
                problemi,
                elaborabile = !problemi.Any(p => p.Bloccante),
            });
        }).RequireAuthorization();

        // Salva soltanto la bozza del calendario. Non calcola il cedolino e rifiuta il mese
        // quando un cedolino esiste già; ID+versione impediscono sovrascritture silenziose.
        app.MapPut("/api/rapporti/{rapportoId:guid}/cartellino-bozza", async (
            Guid rapportoId, SalvaCartellinoBozza richiesta, ClaimsPrincipal user,
            CartellinoBozzaService servizio, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await servizio.SalvaAsync(
                    Tenant(user), rapportoId, richiesta, ct));
            }
            catch (ConflittoCartellinoException ex)
            {
                return Results.Conflict(new { errore = ex.Message });
            }
            catch (RapportoCartellinoNonTrovatoException ex)
            {
                return Results.NotFound(new { errore = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { errore = ex.Message });
            }
        }).RequireAuthorization().RequireRateLimiting("sensibili");

        return app;
    }
}
