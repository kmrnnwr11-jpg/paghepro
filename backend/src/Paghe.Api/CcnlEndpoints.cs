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

        return app;
    }
}
