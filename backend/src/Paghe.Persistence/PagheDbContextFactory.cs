using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Paghe.Persistence;

/// <summary>
/// Factory usata SOLO dagli strumenti EF Core (<c>dotnet ef</c>) a design-time, per generare e
/// applicare le migrazioni del database senza avviare l'intera applicazione. A runtime l'app usa
/// la propria configurazione (provider e stringa di connessione dall'host).
///
/// Stringa di connessione di design-time: variabile d'ambiente <c>PAGHE_DB</c> se presente,
/// altrimenti un file SQLite locale dedicato (non tocca il DB di sviluppo dell'app).
/// </summary>
public sealed class PagheDbContextFactory : IDesignTimeDbContextFactory<PagheDbContext>
{
    public PagheDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("PAGHE_DB") ?? "Data Source=paghe-design.db";

        var options = new DbContextOptionsBuilder<PagheDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new PagheDbContext(options);
    }
}
