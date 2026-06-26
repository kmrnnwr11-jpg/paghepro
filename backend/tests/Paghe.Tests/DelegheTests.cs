using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

public sealed class DelegheTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public DelegheTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task Crea_DelegaAttiva_RisultaVerificabile()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);

        await using var db = NuovoContesto();
        var svc = new DelegheService(db);
        await svc.CreaAsync(tenant, new DelegaInput(
            aziendaId, "FatturaElettronica", new DateOnly(2026, 1, 1)));

        Assert.True(await svc.EsisteDelegaAttivaAsync(tenant, aziendaId, "FatturaElettronica", new DateOnly(2026, 6, 1)));
        Assert.False(await svc.EsisteDelegaAttivaAsync(tenant, aziendaId, "ComUnica", new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public async Task Revoca_DisattivaLaDelega()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);

        Guid delegaId;
        await using (var db = NuovoContesto())
            delegaId = (await new DelegheService(db).CreaAsync(tenant, new DelegaInput(
                aziendaId, "FatturaElettronica", new DateOnly(2026, 1, 1)))).Id;

        await using (var db = NuovoContesto())
            await new DelegheService(db).RevocaAsync(tenant, delegaId);

        await using (var db = NuovoContesto())
            Assert.False(await new DelegheService(db).EsisteDelegaAttivaAsync(
                tenant, aziendaId, "FatturaElettronica", new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public async Task DataFineAnterioreAInizio_Errore()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);

        await using var db = NuovoContesto();
        await Assert.ThrowsAsync<ArgumentException>(() => new DelegheService(db).CreaAsync(tenant, new DelegaInput(
            aziendaId, "FatturaElettronica", new DateOnly(2026, 6, 1), new DateOnly(2026, 1, 1))));
    }

    [Fact]
    public async Task Deleghe_SonoIsolatePerTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenantA);

        Guid delegaId;
        await using (var db = NuovoContesto())
            delegaId = (await new DelegheService(db).CreaAsync(tenantA, new DelegaInput(
                aziendaId, "FatturaElettronica", new DateOnly(2026, 1, 1)))).Id;

        await using (var db = NuovoContesto())
        {
            var svc = new DelegheService(db);
            Assert.NotNull(await svc.GetAsync(tenantA, delegaId));
            Assert.Null(await svc.GetAsync(tenantB, delegaId));
        }
    }

    [Fact]
  public async Task Elenco_OrdinaSenzaErrori()
  {
    // Regressione SQLite: ordinamento per timestamp (CreataIl) deve essere DateTime, non DateTimeOffset.
    var tenant = Guid.NewGuid();
    var aziendaId = await CreaAziendaAsync(tenant);

    await using (var db = NuovoContesto())
      await new DelegheService(db).CreaAsync(tenant, new DelegaInput(aziendaId, "FatturaElettronica", new DateOnly(2026, 1, 1)));

    await using (var db = NuovoContesto())
      Assert.Single(await new DelegheService(db).ElencoAsync(tenant));
  }

  private async Task<Guid> CreaAziendaAsync(Guid tenant)
    {
        await using var db = NuovoContesto();
        var azienda = await new AziendeService(db).CreaAsync(tenant, new AziendaEntity
        {
            RagioneSociale = "Alfa Srl",
            PartitaIva = "01234567890",
            Indirizzo = "Via Roma 1",
            Cap = "20100",
            Comune = "Milano",
            Provincia = "MI",
        });
        return azienda.Id;
    }

    public void Dispose() => _connection.Dispose();
}
