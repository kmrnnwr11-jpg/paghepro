using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>Test degli avvisi di pagamento clienti: debiti, promemoria e incasso.</summary>
public sealed class PersistenzaAvvisiPagamentoTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public PersistenzaAvvisiPagamentoTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto()
    {
        var options = new DbContextOptionsBuilder<PagheDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new PagheDbContext(options);
    }

    private async Task<Guid> CreaAziendaAsync(Guid tenant, string nome = "Alfa Srl")
    {
        await using var db = NuovoContesto();
        var azienda = await new AziendeService(db).CreaAsync(tenant, new AziendaEntity
        {
            RagioneSociale = nome,
            PartitaIva = "01234567890",
            Indirizzo = "Via Roma 1",
            Cap = "20100",
            Comune = "Milano",
            Provincia = "MI",
        });
        return azienda.Id;
    }

    private static AvvisoPagamentoInput Input(Guid aziendaId) => new(
        AziendaId: aziendaId,
        Anno: 2026,
        Mese: 6,
        Importo: 180m,
        DataScadenza: new DateOnly(2026, 7, 10),
        Descrizione: "Compenso commercialista",
        DataEmissione: new DateOnly(2026, 6, 1),
        Iban: "IT60X0542811101000000123456");

    [Fact]
    public async Task Crea_E_Rilegge_AvvisoPagamento()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);

        Guid avvisoId;
        await using (var db = NuovoContesto())
        {
            var avviso = await new AvvisiPagamentoService(db).CreaAsync(tenant, Input(aziendaId));
            avvisoId = avviso.Id;
        }

        await using (var db = NuovoContesto())
        {
            var avviso = await new AvvisiPagamentoService(db).GetAsync(tenant, avvisoId);
            Assert.NotNull(avviso);
            Assert.Equal(2026, avviso!.Anno);
            Assert.Equal(6, avviso.Mese);
            Assert.Equal(180m, avviso.Importo);
            Assert.Equal(AvvisiPagamentoService.DaPagare, avviso.Stato);
            Assert.Equal("Alfa Srl", avviso.Azienda?.RagioneSociale);
        }
    }

    [Fact]
    public async Task Duplicato_StessoClienteMese_LanciaEccezione()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);

        await using var db = NuovoContesto();
        var svc = new AvvisiPagamentoService(db);
        await svc.CreaAsync(tenant, Input(aziendaId));

        await Assert.ThrowsAsync<ArgumentException>(() => svc.CreaAsync(tenant, Input(aziendaId)));
    }

    [Fact]
    public async Task MensilePerTutti_CreaDebitiESaltaQuelliGiaPresenti()
    {
        var tenant = Guid.NewGuid();
        await CreaAziendaAsync(tenant, "Alfa Srl");
        await CreaAziendaAsync(tenant, "Beta Srl");

        await using var db = NuovoContesto();
        var svc = new AvvisiPagamentoService(db);
        var input = new AvvisiPagamentoMeseInput(
            Anno: 2026,
            Mese: 6,
            Importo: 180m,
            DataScadenza: new DateOnly(2026, 7, 10),
            Descrizione: "Canone mensile",
            DataEmissione: new DateOnly(2026, 6, 1));

        var primi = await svc.CreaMensilePerTuttiAsync(tenant, input);
        var secondi = await svc.CreaMensilePerTuttiAsync(tenant, input);

        Assert.Equal(2, primi.Count);
        Assert.Empty(secondi);
        Assert.Equal(2, (await svc.ElencoAsync(tenant)).Count);
    }

    [Fact]
    public async Task Promemoria_E_Pagamento_ChiudonoLaProcedura()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant, "Beta Srl");
        var oggi = DateOnly.FromDateTime(DateTime.UtcNow);

        Guid avvisoId;
        await using (var db = NuovoContesto())
        {
            var avviso = await new AvvisiPagamentoService(db).CreaAsync(tenant, new AvvisoPagamentoInput(
                AziendaId: aziendaId,
                Anno: oggi.Year,
                Mese: oggi.Month,
                Importo: 200m,
                DataScadenza: oggi,
                Descrizione: "Compenso mese corrente",
                DataEmissione: oggi));
            avvisoId = avviso.Id;
        }

        await using (var db = NuovoContesto())
        {
            var svc = new AvvisiPagamentoService(db);
            Assert.Single(await svc.PromemoriaDaFareAsync(tenant, giorniAnticipo: 0));

            var sollecitato = await svc.RegistraPromemoriaAsync(tenant, avvisoId);
            Assert.Equal(AvvisiPagamentoService.Sollecitato, sollecitato.Stato);
            Assert.Equal(1, sollecitato.NumeroPromemoria);
            Assert.Contains("Beta Srl", AvvisiPagamentoService.TestoPromemoria(sollecitato));

            var pagato = await svc.CambiaStatoAsync(tenant, avvisoId, new StatoAvvisoPagamentoInput(
                AvvisiPagamentoService.Pagato,
                PagatoIl: oggi,
                RiferimentoPagamento: "CRO123"));
            Assert.Equal(AvvisiPagamentoService.Pagato, pagato.Stato);
            Assert.Equal(oggi, pagato.PagatoIl);
            Assert.Empty(await svc.PromemoriaDaFareAsync(tenant, giorniAnticipo: 0));
        }
    }

    [Fact]
    public async Task IsolamentoTenant_NonLeggeDebitiAltrui()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenantA);

        Guid avvisoId;
        await using (var db = NuovoContesto())
            avvisoId = (await new AvvisiPagamentoService(db).CreaAsync(tenantA, Input(aziendaId))).Id;

        await using (var db = NuovoContesto())
        {
            var svc = new AvvisiPagamentoService(db);
            Assert.Null(await svc.GetAsync(tenantB, avvisoId));
            Assert.Empty(await svc.ElencoAsync(tenantB));
        }
    }

    public void Dispose() => _connection.Dispose();
}
