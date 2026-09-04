using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Il dataset dimostrativo passa dagli stessi servizi (e dagli stessi controlli formali su
/// P.IVA e codice fiscale) dei dati reali: se un checksum è sbagliato, l'endpoint demo
/// fallisce a metà e l'archivio resta incompleto.
/// </summary>
public sealed class DemoDatasetTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public DemoDatasetTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
        new ParametriNormativiService(db).ImportaSeedSeVuotoAsync().GetAwaiter().GetResult();
    }

    public void Dispose() => _connection.Dispose();

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task CreaSeMancante_CreaCinqueAziendeConUnDipendente_EResta_Idempotente()
    {
        var tenant = Guid.NewGuid();
        await using var db = NuovoContesto();
        var svc = new DemoDatasetService(db, new AziendeService(db), new AnagraficheLavoroService(db));

        var esito = await svc.CreaSeMancanteAsync(tenant);
        Assert.Equal(5, esito.Aziende);
        Assert.Equal(5, esito.Dipendenti);

        var ripetuto = await svc.CreaSeMancanteAsync(tenant);
        Assert.Equal(5, ripetuto.Aziende);
        Assert.Equal(5, ripetuto.Dipendenti);

        var altroTenant = Guid.NewGuid();
        Assert.False(await db.Aziende.AnyAsync(a => a.TenantId == altroTenant));
    }
}
