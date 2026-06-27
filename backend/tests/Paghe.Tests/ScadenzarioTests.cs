using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

public sealed class ScadenzarioTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ScadenzarioTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task RegistraDurc_SenzaScadenza_CalcolaCentoVentiGiorni()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);

        await using var db = NuovoContesto();
        var svc = new ScadenzarioService(db);
        var durc = await svc.RegistraDurcAsync(tenant, new DurcInput(
            aziendaId, "PROT-2026-1", new DateOnly(2026, 1, 1)));

        Assert.Equal(new DateOnly(2026, 1, 1).AddDays(ScadenzarioService.GiorniValiditaDurc), durc.DataScadenza);
    }

    [Fact]
    public async Task RegistraDurc_AziendaInesistente_Errore()
    {
        var tenant = Guid.NewGuid();
        await using var db = NuovoContesto();
        await Assert.ThrowsAsync<ArgumentException>(() => new ScadenzarioService(db).RegistraDurcAsync(
            tenant, new DurcInput(Guid.NewGuid(), "X", new DateOnly(2026, 1, 1))));
    }

    [Fact]
    public async Task Rigenera_DaDurc_CreaPromemoriaConMessaggio()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);

        await using (var db = NuovoContesto())
            await new ScadenzarioService(db).RegistraDurcAsync(tenant, new DurcInput(
                aziendaId, "PROT-7", new DateOnly(2026, 3, 1), new DateOnly(2026, 6, 30)));

        await using (var db = NuovoContesto())
        {
            var elenco = await new ScadenzarioService(db).ElencoAsync(tenant);
            var p = Assert.Single(elenco);
            Assert.Equal(TipoPromemoria.Durc, p.Tipo);
            Assert.Equal(new DateOnly(2026, 6, 30), p.DataScadenza);
            Assert.Equal(StatoPromemoria.Aperto, p.Stato);
            Assert.Contains("PROT-7", p.Messaggio);
            Assert.Contains("Alfa Srl", p.Messaggio);
        }
    }

    [Fact]
    public async Task Rigenera_Idempotente_NonDuplica()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);
        await using (var db = NuovoContesto())
            await new ScadenzarioService(db).RegistraDurcAsync(tenant, new DurcInput(
                aziendaId, "PROT-1", new DateOnly(2026, 1, 1)));

        await using (var db = NuovoContesto())
        {
            var svc = new ScadenzarioService(db);
            Assert.Equal(1, await svc.RigeneraAsync(tenant));   // crea
            Assert.Equal(0, await svc.RigeneraAsync(tenant));   // nessun cambiamento
        }

        await using (var db = NuovoContesto())
            Assert.Single(await new ScadenzarioService(db).ElencoAsync(tenant, rigenera: false));
    }

    [Fact]
    public async Task Rigenera_PreservaStatoDecisoDallUtente()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);
        await using (var db = NuovoContesto())
            await new ScadenzarioService(db).RegistraDurcAsync(tenant, new DurcInput(
                aziendaId, "PROT-1", new DateOnly(2026, 1, 1)));

        Guid promemoriaId;
        await using (var db = NuovoContesto())
        {
            var svc = new ScadenzarioService(db);
            promemoriaId = (await svc.ElencoAsync(tenant)).Single().Id;
            await svc.CambiaStatoAsync(tenant, promemoriaId, StatoPromemoria.Completato);
        }

        await using (var db = NuovoContesto())
        {
            var svc = new ScadenzarioService(db);
            await svc.RigeneraAsync(tenant);
            var p = await svc.GetAsync(tenant, promemoriaId);
            Assert.Equal(StatoPromemoria.Completato, p!.Stato);
        }
    }

    [Fact]
    public async Task Rigenera_DaF24_CreaPromemoriaVersamento()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);

        await using (var db = NuovoContesto())
        {
            db.PraticheF24.Add(new PraticaF24Entity
            {
                Id = Guid.NewGuid(),
                TenantId = tenant,
                AziendaId = aziendaId,
                Anno = 2026,
                Mese = 5,
                DataVersamento = new DateOnly(2026, 6, 16),
                SaldoFinale = 1234.56m,
                Stato = "Generata",
                CreataIl = DateTime.UtcNow,
                UltimoAggiornamento = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            var p = Assert.Single(await new ScadenzarioService(db).ElencoAsync(tenant));
            Assert.Equal(TipoPromemoria.F24, p.Tipo);
            Assert.Equal(new DateOnly(2026, 6, 16), p.DataScadenza);
            Assert.Contains("05/2026", p.Messaggio);
        }
    }

    [Fact]
    public async Task CambiaStato_AInviato_RegistraData_EFinaleBlocca()
    {
        var tenant = Guid.NewGuid();
        await using var db = NuovoContesto();
        var svc = new ScadenzarioService(db);
        var p = await svc.CreaManualeAsync(tenant, new PromemoriaManualeInput(
            "Scadenza generica", new DateOnly(2026, 12, 31)));

        var inviato = await svc.CambiaStatoAsync(tenant, p.Id, StatoPromemoria.Inviato);
        Assert.NotNull(inviato.InviatoIl);

        await svc.CambiaStatoAsync(tenant, p.Id, StatoPromemoria.Annullato);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CambiaStatoAsync(tenant, p.Id, StatoPromemoria.Aperto));
    }

    [Fact]
    public async Task CambiaStato_StatoNonValido_Errore()
    {
        var tenant = Guid.NewGuid();
        await using var db = NuovoContesto();
        var svc = new ScadenzarioService(db);
        var p = await svc.CreaManualeAsync(tenant, new PromemoriaManualeInput("X", new DateOnly(2026, 12, 31)));
        await Assert.ThrowsAsync<ArgumentException>(() => svc.CambiaStatoAsync(tenant, p.Id, "Boh"));
    }

    [Fact]
    public async Task Promemoria_IsolatiPerTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenantA);
        await using (var db = NuovoContesto())
            await new ScadenzarioService(db).RegistraDurcAsync(tenantA, new DurcInput(
                aziendaId, "PROT-1", new DateOnly(2026, 1, 1)));

        await using (var db = NuovoContesto())
        {
            var svc = new ScadenzarioService(db);
            Assert.Single(await svc.ElencoAsync(tenantA));
            Assert.Empty(await svc.ElencoAsync(tenantB));
        }
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
