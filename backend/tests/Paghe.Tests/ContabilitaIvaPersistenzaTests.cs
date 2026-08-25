using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Contabilita;
using Paghe.Core.Fatturazione;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

public sealed class ContabilitaIvaPersistenzaTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ContabilitaIvaPersistenzaTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task SalvaSpesa_CalcolaIvaDetraibileETotale()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);

        await using var db = NuovoContesto();
        var spesa = await new ContabilitaIvaService(db).SalvaSpesaAsync(tenant, aziendaId, new SpesaInput(
            Data: new DateOnly(2026, 7, 11),
            Descrizione: "Software",
            ContoCosto: "6400",
            Imponibile: 50m,
            AliquotaIva: 22m,
            PercentualeDetraibilita: 50m));

        Assert.Equal(11m, spesa.Iva);
        Assert.Equal(5.50m, spesa.IvaDetraibile);
        Assert.Equal(61m, spesa.Totale);
    }

    [Fact]
    public async Task CreaLiquidazione_UsaFattureESpesePersistite()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenant);

        await using (var db = NuovoContesto())
        {
            await new FattureService(db).SalvaAsync(tenant, aziendaId, new FatturaInput(
                Data: new DateOnly(2026, 7, 10),
                Cessionario: Cliente(),
                Righe: new[] { new RigaFattura { Descrizione = "Servizi", PrezzoUnitario = 100m, AliquotaIva = 22m } }));

            await new ContabilitaIvaService(db).SalvaSpesaAsync(tenant, aziendaId, new SpesaInput(
                Data: new DateOnly(2026, 7, 11),
                Descrizione: "Software",
                ContoCosto: "6400",
                Imponibile: 50m,
                AliquotaIva: 22m));
        }

        await using (var db = NuovoContesto())
        {
            var liquidazione = await new ContabilitaIvaService(db).CreaLiquidazioneAsync(
                tenant,
                aziendaId,
                new LiquidazioneIvaInput(
                    Dal: new DateOnly(2026, 7, 1),
                    Al: new DateOnly(2026, 7, 31),
                    Periodicita: PeriodicitaIva.Mensile));

            Assert.Equal(22m, liquidazione.IvaVendite);
            Assert.Equal(11m, liquidazione.IvaAcquistiDetraibile);
            Assert.Equal(11m, liquidazione.IvaDaVersare);
            Assert.Equal(2, liquidazione.Righe.Count);
        }
    }

    [Fact]
    public async Task SpeseELiquidazioni_SonoIsolatePerTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var aziendaId = await CreaAziendaAsync(tenantA);

        Guid spesaId;
        Guid liquidazioneId;
        await using (var db = NuovoContesto())
        {
            var svc = new ContabilitaIvaService(db);
            spesaId = (await svc.SalvaSpesaAsync(tenantA, aziendaId, new SpesaInput(
                Data: new DateOnly(2026, 7, 11),
                Descrizione: "Software",
                ContoCosto: "6400",
                Imponibile: 50m))).Id;

            liquidazioneId = (await svc.CreaLiquidazioneAsync(tenantA, aziendaId, new LiquidazioneIvaInput(
                Dal: new DateOnly(2026, 7, 1),
                Al: new DateOnly(2026, 7, 31),
                Periodicita: PeriodicitaIva.Mensile))).Id;
        }

        await using (var db = NuovoContesto())
        {
            var svc = new ContabilitaIvaService(db);
            Assert.NotNull(await svc.GetSpesaAsync(tenantA, spesaId));
            Assert.Null(await svc.GetSpesaAsync(tenantB, spesaId));
            Assert.NotNull(await svc.GetLiquidazioneAsync(tenantA, liquidazioneId));
            Assert.Null(await svc.GetLiquidazioneAsync(tenantB, liquidazioneId));
        }
    }

    private async Task<Guid> CreaAziendaAsync(Guid tenant)
    {
        await using var db = NuovoContesto();
        var azienda = await new AziendeService(db).CreaAsync(tenant, new AziendaEntity
        {
            RagioneSociale = "Alfa Srl",
            PartitaIva = "01234561007",
            Indirizzo = "Via Roma 1",
            Cap = "20100",
            Comune = "Milano",
            Provincia = "MI",
        });
        return azienda.Id;
    }

    private static DatiAnagrafici Cliente() => new()
    {
        Denominazione = "Beta Spa",
        PartitaIva = "09876541005",
        Indirizzo = "Via Po 2",
        Cap = "10100",
        Comune = "Torino",
        Provincia = "TO",
    };

    public void Dispose() => _connection.Dispose();
}
