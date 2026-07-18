using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Pratiche di autoliquidazione INAIL: calcolo dai cedolini archiviato come pratica con stato,
/// transizioni di stato validate, bozza leggibile e isolamento per tenant.
/// </summary>
public sealed class PraticheInailTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public PraticheInailTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    private async Task<(Guid tenant, Guid aziendaId)> CreaAziendaConCedoliniAsync(decimal retribuzioni)
    {
        var tenant = Guid.NewGuid();
        Guid aziendaId;
        await using (var db = NuovoContesto())
        {
            var az = await new AziendeService(db).CreaAsync(tenant, new AziendaEntity
            {
                RagioneSociale = "Alfa Srl", PartitaIva = "01234567890",
                Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
            });
            aziendaId = az.Id;
            db.Cedolini.Add(new CedolinoEntity
            {
                Id = Guid.NewGuid(), TenantId = tenant, AziendaId = aziendaId,
                RapportoLavoroId = Guid.NewGuid(), Anno = 2026, Mese = 1,
                ImponibileInps = retribuzioni, CalcolatoIl = DateTime.UtcNow,
            });
            await new InailService(db).SalvaPosizioneAsync(tenant, aziendaId, 2026, "12345678/90", "0722", 12.5m);
            await db.SaveChangesAsync();
        }
        return (tenant, aziendaId);
    }

    [Fact]
    public async Task CalcolaEArchivia_SalvaLaPraticaConTotaliEBozza()
    {
        var (tenant, aziendaId) = await CreaAziendaConCedoliniAsync(100_000m);

        await using var db = NuovoContesto();
        var svc = new PraticheInailService(db, new InailService(db));
        var p = await svc.CalcolaEArchiviaAsync(tenant, aziendaId, 2026, rataGiaVersata: 1000m);

        Assert.Equal(100_000m, p.RetribuzioniAnno);
        Assert.Equal(1250m, p.Regolazione);          // 100.000 × 12,5‰
        Assert.Equal(1500m, p.TotaleDaVersare);      // 1250 − 1000 + 1250
        Assert.Equal(StatoPraticaTelematica.Generata.ToString(), p.Stato);
        Assert.Contains(BozzaLeggibileInailWriter.IntestazioneBozza, p.Bozza);
        Assert.Contains("TOTALE DA VERSARE: 1500.00", p.Bozza);
        Assert.Equal(1, await db.PraticheInail.CountAsync());
    }

    [Fact]
    public async Task Elenco_OrdinatoPerAnnoEDataDiscendente_IsolatoPerTenant()
    {
        var (tenant, aziendaId) = await CreaAziendaConCedoliniAsync(50_000m);
        var altroTenant = Guid.NewGuid();

        await using (var db = NuovoContesto())
            await new PraticheInailService(db, new InailService(db)).CalcolaEArchiviaAsync(tenant, aziendaId, 2026);

        await using (var db = NuovoContesto())
        {
            var elenco = await new PraticheInailService(db, new InailService(db)).ElencoAsync(tenant, aziendaId);
            Assert.Single(elenco);

            var elencoAltroTenant = await new PraticheInailService(db, new InailService(db)).ElencoAsync(altroTenant);
            Assert.Empty(elencoAltroTenant);
        }
    }

    [Fact]
    public async Task CambiaStato_TransizioneAmmessa_Passa_NonAmmessa_Lancia()
    {
        var (tenant, aziendaId) = await CreaAziendaConCedoliniAsync(50_000m);
        Guid praticaId;
        await using (var db = NuovoContesto())
            praticaId = (await new PraticheInailService(db, new InailService(db))
                .CalcolaEArchiviaAsync(tenant, aziendaId, 2026)).Id;

        await using (var db = NuovoContesto())
        {
            var svc = new PraticheInailService(db, new InailService(db));
            var p = await svc.CambiaStatoAsync(tenant, praticaId, StatoPraticaTelematica.ControllataOk);
            Assert.Equal(StatoPraticaTelematica.ControllataOk.ToString(), p.Stato);

            // Da ControllataOk non si può saltare direttamente a RicevutaAcquisita.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.CambiaStatoAsync(tenant, praticaId, StatoPraticaTelematica.RicevutaAcquisita));
        }
    }

    [Fact]
    public async Task Get_DiUnAltroTenant_NonTrovata()
    {
        var (tenant, aziendaId) = await CreaAziendaConCedoliniAsync(50_000m);
        Guid praticaId;
        await using (var db = NuovoContesto())
            praticaId = (await new PraticheInailService(db, new InailService(db))
                .CalcolaEArchiviaAsync(tenant, aziendaId, 2026)).Id;

        await using (var db = NuovoContesto())
        {
            var svc = new PraticheInailService(db, new InailService(db));
            Assert.Null(await svc.GetAsync(Guid.NewGuid(), praticaId));
            Assert.NotNull(await svc.GetAsync(tenant, praticaId));
        }
    }

    public void Dispose() => _connection.Dispose();
}
