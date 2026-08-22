using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Paghe.Persistence;

namespace Paghe.Migrazioni.Postgres;

/// <summary>Factory EF di design: genera il modello senza collegarsi al database di produzione.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<PagheDbContext>
{
    public PagheDbContext CreateDbContext(string[] args)
    {
        var opzioni = new DbContextOptionsBuilder<PagheDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=paghepro_design;Username=design;Password=design",
                sql => sql.MigrationsAssembly("Paghe.Migrazioni.Postgres"))
            .Options;
        return new PagheDbContext(opzioni);
    }
}
