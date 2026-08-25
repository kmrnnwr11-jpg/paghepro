using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Contabilita;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test della bozza di dichiarazione IVA annuale: aggrega le liquidazioni periodiche dell'anno e
/// produce l'export CSV (bozza rivedibile, non il file ufficiale).
/// </summary>
public sealed class IvaDichiarazioneAnnualeTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public IvaDichiarazioneAnnualeTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    private async Task<Guid> CreaAziendaAsync(Guid tenant)
    {
        await using var db = NuovoContesto();
        var az = await new AziendeService(db).CreaAsync(tenant, new AziendaEntity
        {
            RagioneSociale = "Alfa Srl", PartitaIva = "01234567897",
            Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
        });
        return az.Id;
    }

    [Fact]
    public async Task IvaAnnuale_AggregaLeLiquidazioniDellAnno()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);

        LiquidazioneIvaEntity liq;
        await using (var db = NuovoContesto())
        {
            var svc = new ContabilitaIvaService(db);
            await svc.SalvaSpesaAsync(tenant, aziendaId, new SpesaInput(
                new DateOnly(2026, 1, 10), "Acquisto materiali", "06.05", 1000m));
            liq = await svc.CreaLiquidazioneAsync(tenant, aziendaId, new LiquidazioneIvaInput(
                new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), PeriodicitaIva.Mensile));
        }

        await using (var db = NuovoContesto())
        {
            var svc = new ContabilitaIvaService(db);
            var b = await svc.CostruisciDichiarazioneIvaAnnualeAsync(tenant, aziendaId, 2026);

            Assert.Equal(1, b.NumeroLiquidazioni);
            Assert.Equal(liq.IvaVendite, b.TotaleIvaVendite);
            Assert.Equal(liq.IvaAcquistiDetraibile, b.TotaleIvaAcquistiDetraibile);
            Assert.Equal(liq.IvaDaVersare, b.TotaleIvaDaVersare);
            Assert.Equal(liq.IvaVendite - liq.IvaAcquistiDetraibile, b.SaldoAnnuale);

            var csv = ContabilitaIvaService.EsportaCsvDichiarazioneIva(b);
            Assert.StartsWith("Anno;NumeroLiquidazioni;IvaVendite;", csv);
            Assert.Contains("2026", csv);
        }
    }

    [Fact]
    public async Task IvaAnnuale_AnnoSenzaLiquidazioni_TuttoAZero()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);

        await using var db = NuovoContesto();
        var b = await new ContabilitaIvaService(db)
            .CostruisciDichiarazioneIvaAnnualeAsync(tenant, aziendaId, 2026);

        Assert.Equal(0, b.NumeroLiquidazioni);
        Assert.Equal(0m, b.SaldoAnnuale);
    }

    public void Dispose() => _connection.Dispose();
}
