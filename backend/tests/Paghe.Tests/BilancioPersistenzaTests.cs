using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Contabilita;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

public sealed class BilancioPersistenzaTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public BilancioPersistenzaTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task CreaDaEsempio_ArchiviaBilancioQuadratoConRighe()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);

        await using var db = NuovoContesto();
        var b = await new BilancioService(db).CreaDaEsempioAsync(tenant, aziendaId, 2025);

        Assert.Equal(2025, b.Anno);
        Assert.Equal(StatoBilancio.Bozza.ToString(), b.Stato);
        Assert.Equal(20000m, b.UtilePerditaEsercizio);   // dal seed quadrato
        Assert.Equal(b.TotaleAttivo, b.TotalePassivo);
        Assert.True(b.Quadra);
        Assert.Equal(SeedPianoConti.SaldiEsempio().Count, b.Righe.Count);
        Assert.Contains("UTILE/(PERDITA)", b.Bozza);
    }

    [Fact]
    public async Task PrecompilaDaContabilita_PortaRicaviInValoreDellaProduzione()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);

        await using (var db = NuovoContesto())
        {
            await new ContabilitaIvaService(db).SalvaSpesaAsync(tenant, aziendaId, new SpesaInput(
                Data: new DateOnly(2025, 3, 10), Descrizione: "Software", ContoCosto: "6400", Imponibile: 1000m));
        }

        await using (var db = NuovoContesto())
        {
            var voci = await new BilancioService(db).PrecompilaDaContabilitaAsync(tenant, aziendaId, 2025);
            var costi = Assert.Single(voci);
            Assert.Equal(MacroVoce.CostiDellaProduzione, costi.Voce);
            Assert.Equal(1000m, costi.Importo);
        }
    }

    [Fact]
    public async Task CambiaStato_RispettaLaMacchinaAStati()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);

        await using var db = NuovoContesto();
        var svc = new BilancioService(db);
        var b = await svc.CreaDaEsempioAsync(tenant, aziendaId, 2025);

        var approvato = await svc.CambiaStatoAsync(tenant, b.Id, StatoBilancio.Approvato);
        Assert.Equal(StatoBilancio.Approvato.ToString(), approvato.Stato);

        var depositato = await svc.CambiaStatoAsync(tenant, b.Id, StatoBilancio.Depositato);
        Assert.Equal(StatoBilancio.Depositato.ToString(), depositato.Stato);

        // Da Depositato non sono ammesse ulteriori transizioni.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CambiaStatoAsync(tenant, b.Id, StatoBilancio.Bozza));
    }

    [Fact]
    public async Task Bilanci_SonoIsolatiPerTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenantA);

        Guid bilancioId;
        await using (var db = NuovoContesto())
            bilancioId = (await new BilancioService(db).CreaDaEsempioAsync(tenantA, aziendaId, 2025)).Id;

        await using (var db = NuovoContesto())
        {
            var svc = new BilancioService(db);
            Assert.NotNull(await svc.GetAsync(tenantA, bilancioId));
            Assert.Null(await svc.GetAsync(tenantB, bilancioId));
        }
    }

    private async Task<Guid> CreaAziendaAsync(Guid tenant)
    {
        await using var db = NuovoContesto();
        var azienda = await new AziendeService(db).CreaAsync(tenant, new AziendaEntity
        {
            RagioneSociale = "Alfa Srls",
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
