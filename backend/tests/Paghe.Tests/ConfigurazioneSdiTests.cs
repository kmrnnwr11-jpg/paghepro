using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Xunit;

namespace Paghe.Tests;

public sealed class ConfigurazioneSdiTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ConfigurazioneSdiTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task Salva_CreaEPoiAggiorna_UnaSolaConfigPerTenant()
    {
        var tenant = Guid.NewGuid();

        await using (var db = NuovoContesto())
        {
            await new ConfigurazioneSdiService(db).SalvaAsync(tenant, new ConfigurazioneSdiInput(
                Modalita: "Rest", Provider: "Acube", BaseUrl: "https://api.acube.it/", ApiKey: "chiave-123"));
        }

        await using (var db = NuovoContesto())
        {
            await new ConfigurazioneSdiService(db).SalvaAsync(tenant, new ConfigurazioneSdiInput(
                Modalita: "Rest", Provider: "Aruba", BaseUrl: "https://api.aruba.it/"));
        }

        await using (var db = NuovoContesto())
        {
            Assert.Equal(1, await db.ConfigurazioniSdi.CountAsync(c => c.TenantId == tenant));
            var c = await new ConfigurazioneSdiService(db).GetAsync(tenant);
            Assert.NotNull(c);
            Assert.Equal("Aruba", c!.Provider);
            // ApiKey non re-inviata (null) → conservata
            Assert.Equal("chiave-123", c.ApiKey);
        }
    }

    [Fact]
    public async Task ApiKey_StringaVuota_Cancella_NullMantiene()
    {
        var tenant = Guid.NewGuid();
        await using (var db = NuovoContesto())
            await new ConfigurazioneSdiService(db).SalvaAsync(tenant, new ConfigurazioneSdiInput(
                Modalita: "Rest", ApiKey: "segreto"));

        await using (var db = NuovoContesto())
            await new ConfigurazioneSdiService(db).SalvaAsync(tenant, new ConfigurazioneSdiInput(
                Modalita: "Rest", ApiKey: ""));   // svuota esplicitamente

        await using (var db = NuovoContesto())
            Assert.Null((await new ConfigurazioneSdiService(db).GetAsync(tenant))!.ApiKey);
    }

    [Fact]
    public async Task Configurazione_IsolataPerTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using (var db = NuovoContesto())
            await new ConfigurazioneSdiService(db).SalvaAsync(tenantA, new ConfigurazioneSdiInput(Modalita: "Simulato"));

        await using (var db = NuovoContesto())
        {
            Assert.NotNull(await new ConfigurazioneSdiService(db).GetAsync(tenantA));
            Assert.Null(await new ConfigurazioneSdiService(db).GetAsync(tenantB));
        }
    }

    public void Dispose() => _connection.Dispose();
}
