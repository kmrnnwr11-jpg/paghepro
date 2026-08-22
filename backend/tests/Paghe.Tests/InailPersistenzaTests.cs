using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test della posizione INAIL (upsert per azienda+anno) e della bozza di autoliquidazione
/// calcolata sui cedolini archiviati dell'anno. Isolamento tenant compreso.
/// </summary>
public sealed class InailPersistenzaTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public InailPersistenzaTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    private async Task<(Guid tenant, Guid aziendaId)> CreaAziendaAsync()
    {
        var tenant = Guid.NewGuid();
        await using var db = NuovoContesto();
        var az = await new AziendeService(db).CreaAsync(tenant, new AziendaEntity
        {
            RagioneSociale = "Alfa Srl", PartitaIva = "01234567890",
            Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
        });
        return (tenant, az.Id);
    }

    private async Task InserisciCedolinoAsync(Guid tenant, Guid aziendaId, int anno, int mese, decimal imponibileInps)
    {
        await using var db = NuovoContesto();
        var cedolino = new CedolinoEntity
        {
            Id = Guid.NewGuid(), TenantId = tenant, AziendaId = aziendaId,
            RapportoLavoroId = Guid.NewGuid(), Anno = anno, Mese = mese,
            ImponibileInps = imponibileInps, CalcolatoIl = DateTime.UtcNow,
            Stato = nameof(StatoCedolino.Bozza), Versione = 1,
        };
        db.Cedolini.Add(cedolino);
        await db.SaveChangesAsync();
        await db.ClassificaPreWorkflowAsync(tenant, cedolino.Id);
    }

    [Fact]
    public async Task SalvaPosizione_EUpsertPerAziendaEAnno()
    {
        var (tenant, aziendaId) = await CreaAziendaAsync();

        await using var db = NuovoContesto();
        var svc = new InailService(db);
        await svc.SalvaPosizioneAsync(tenant, aziendaId, 2026, "12345678/90", "0722", 12.5m);
        await svc.SalvaPosizioneAsync(tenant, aziendaId, 2026, "12345678/90", "0722", 14m); // aggiorna

        Assert.Equal(1, await db.PosizioniInail.CountAsync());
        var p = await svc.GetPosizioneAsync(tenant, aziendaId, 2026);
        Assert.Equal(14m, p!.TassoPerMille);

        // Tenant diverso non vede la posizione.
        Assert.Null(await svc.GetPosizioneAsync(Guid.NewGuid(), aziendaId, 2026));
    }

    [Fact]
    public async Task Autoliquidazione_SommaLeRetribuzioniDeiCedoliniDellAnno()
    {
        var (tenant, aziendaId) = await CreaAziendaAsync();
        await InserisciCedolinoAsync(tenant, aziendaId, 2026, 1, 30_000m);
        await InserisciCedolinoAsync(tenant, aziendaId, 2026, 2, 70_000m);
        await InserisciCedolinoAsync(tenant, aziendaId, 2025, 12, 999m); // altro anno: escluso

        await using var db = NuovoContesto();
        var svc = new InailService(db);
        await svc.SalvaPosizioneAsync(tenant, aziendaId, 2026, "12345678/90", "0722", 12.5m);
        var bozza = await svc.CalcolaAutoliquidazioneAsync(tenant, aziendaId, 2026, rataGiaVersata: 1000m);

        Assert.Equal(100_000m, bozza.RetribuzioniAnno);
        Assert.Equal(2, bozza.NumeroCedolini);
        Assert.Equal(1250m, bozza.Regolazione);           // 100.000 × 12,5‰
        Assert.Equal(1500m, bozza.TotaleDaVersare);       // 1250 − 1000 + 1250
        Assert.Contains("INAIL", bozza.Nota);
    }

    [Fact]
    public async Task Autoliquidazione_SenzaPosizioneOCedolini_Lancia()
    {
        var (tenant, aziendaId) = await CreaAziendaAsync();

        await using var db = NuovoContesto();
        var svc = new InailService(db);

        // Senza posizione salvata.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CalcolaAutoliquidazioneAsync(tenant, aziendaId, 2026));

        // Con posizione ma senza cedolini archiviati.
        await svc.SalvaPosizioneAsync(tenant, aziendaId, 2026, "12345678/90", "0722", 12.5m);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CalcolaAutoliquidazioneAsync(tenant, aziendaId, 2026));
    }

    [Fact]
    public async Task SalvaPosizione_AziendaDiAltroTenant_Lancia()
    {
        var (_, aziendaId) = await CreaAziendaAsync();

        await using var db = NuovoContesto();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new InailService(db).SalvaPosizioneAsync(Guid.NewGuid(), aziendaId, 2026, "1", "0722", 10m));
    }

    public void Dispose() => _connection.Dispose();
}
