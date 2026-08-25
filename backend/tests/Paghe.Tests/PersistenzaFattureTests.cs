using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Fatturazione;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test di integrazione della persistenza su SQLite in-memory (database SQL reale).
/// Verificano salvataggio/rilettura, numerazione automatica e isolamento per tenant.
/// </summary>
public sealed class PersistenzaFattureTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public PersistenzaFattureTests()
    {
        // SQLite in-memory: il DB vive finché la connessione resta aperta.
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

    private static DatiAnagrafici Cliente() => new()
    {
        Denominazione = "Beta Spa", PartitaIva = "09876541005",
        Indirizzo = "Via Po 2", Cap = "10100", Comune = "Torino", Provincia = "TO",
    };

    private async Task<Guid> CreaAziendaAsync(Guid tenant)
    {
        await using var db = NuovoContesto();
        var svc = new AziendeService(db);
        var azienda = await svc.CreaAsync(tenant, new AziendaEntity
        {
            RagioneSociale = "Alfa Srl", PartitaIva = "01234561007",
            Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
        });
        return azienda.Id;
    }

    private static FatturaInput Input() => new(
        Data: new DateOnly(2026, 6, 26),
        Cessionario: Cliente(),
        Righe: new[]
        {
            new RigaFattura { Descrizione = "Consulenza", PrezzoUnitario = 100m, AliquotaIva = 22m },
            new RigaFattura { Descrizione = "Materiali", Quantita = 2m, PrezzoUnitario = 50m, AliquotaIva = 10m },
        });

    [Fact]
    public async Task Salva_E_Rileggi_FatturaConTotaliERighe()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);

        Guid fatturaId;
        await using (var db = NuovoContesto())
        {
            var saved = await new FattureService(db).SalvaAsync(tenant, aziendaId, Input());
            fatturaId = saved.Id;
        }

        await using (var db = NuovoContesto())
        {
            var f = await new FattureService(db).GetAsync(tenant, fatturaId);
            Assert.NotNull(f);
            Assert.Equal("1/2026", f!.Numero);
            Assert.Equal(2, f.Righe.Count);
            // 100 + (2*50) = 200 imponibile; IVA 22 + 10 = 32; totale 232
            Assert.Equal(200m, f.TotaleImponibile);
            Assert.Equal(32m, f.TotaleImposta);
            Assert.Equal(232m, f.TotaleDocumento);
            Assert.Equal("Beta Spa", f.CessionarioDenominazione);
        }
    }

    [Fact]
    public async Task Numerazione_Automatica_Progressiva_PerAziendaEAnno()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);

        await using var db = NuovoContesto();
        var svc = new FattureService(db);

        var f1 = await svc.SalvaAsync(tenant, aziendaId, Input());
        var f2 = await svc.SalvaAsync(tenant, aziendaId, Input());

        Assert.Equal("1/2026", f1.Numero);
        Assert.Equal("2/2026", f2.Numero);
    }

    [Fact]
    public async Task Isolamento_Tenant_NonSiVedonoLeFattureAltrui()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var aziendaA = await CreaAziendaAsync(tenantA);

        Guid fatturaId;
        await using (var db = NuovoContesto())
            fatturaId = (await new FattureService(db).SalvaAsync(tenantA, aziendaA, Input())).Id;

        await using (var db = NuovoContesto())
        {
            // Il tenant B non deve vedere la fattura del tenant A.
            var vistaDaB = await new FattureService(db).GetAsync(tenantB, fatturaId);
            Assert.Null(vistaDaB);
            // Il tenant A la vede.
            var vistaDaA = await new FattureService(db).GetAsync(tenantA, fatturaId);
            Assert.NotNull(vistaDaA);
        }
    }

    [Fact]
    public async Task Salva_AziendaInesistente_LanciaEccezione()
    {
        var tenant = Guid.NewGuid();
        await using var db = NuovoContesto();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new FattureService(db).SalvaAsync(tenant, Guid.NewGuid(), Input()));
    }

    public void Dispose() => _connection.Dispose();
}
