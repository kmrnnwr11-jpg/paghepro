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
        app.MapPost("/api/ccnl/importa", async (List<CcnlInput> input, PagheDbContext db) =>
        {
            var (ccnl, livelli) = await new CcnlImportService(db).ImportaAsync(input);
            return Results.Ok(new { ccnlCaricati = ccnl, livelliCaricati = livelli });
        }).RequireAuthorization();

        return app;
    }
}
