using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;

namespace Paghe.Api;

/// <summary>Elenco dei CCNL configurati nel motore (per i menu a tendina del frontend).</summary>
public static class CcnlEndpoints
{
    public static IEndpointRouteBuilder MapCcnl(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/ccnl", async (PagheDbContext db) =>
            Results.Ok(await db.Ccnl
                .OrderBy(c => c.Descrizione)
                .Select(c => new { c.Codice, c.Descrizione })
                .ToListAsync()))
            .RequireAuthorization();

        // Carica/aggiorna in blocco le tabelle CCNL (minimi tabellari reali) da un file JSON.
        // Cosi' "tutti i CCNL" = caricare i dati ufficiali, senza toccare il codice.
        // Le tabelle CCNL sono CONDIVISE tra tutti i tenant: la scrittura è riservata
        // al super-admin di piattaforma, non a un normale studio autenticato.
        app.MapPost("/api/ccnl/importa", async (List<CcnlInput> input, ClaimsPrincipal user, PagheDbContext db) =>
        {
            if (!user.IsInRole(AuthService.RuoloSuperAdmin)
                && user.FindFirst(ClaimTypes.Role)?.Value != AuthService.RuoloSuperAdmin)
                return Results.Json(
                    new { errore = "Operazione riservata all'amministratore della piattaforma." },
                    statusCode: StatusCodes.Status403Forbidden);
            var (ccnl, livelli) = await new CcnlImportService(db).ImportaAsync(input);
            return Results.Ok(new { ccnlCaricati = ccnl, livelliCaricati = livelli });
        }).RequireAuthorization();

        return app;
    }
}
