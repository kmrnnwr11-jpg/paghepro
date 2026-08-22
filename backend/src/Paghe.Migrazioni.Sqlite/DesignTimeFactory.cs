using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Paghe.Persistence;

namespace Paghe.Migrazioni.Sqlite;

/// <summary>Factory EF di design: evita di avviare API, seed e database reale durante lo scaffold.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<PagheDbContext>
{
    public PagheDbContext CreateDbContext(string[] args)
    {
        var opzioni = new DbContextOptionsBuilder<PagheDbContext>()
            .UseSqlite("Data Source=/tmp/paghepro-ef-design.sqlite",
                sql => sql.MigrationsAssembly("Paghe.Migrazioni.Sqlite"))
            .Options;
        return new PagheDbContext(opzioni);
    }
}
