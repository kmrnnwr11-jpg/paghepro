using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Cedolini;
using Paghe.Persistence.Entities;

namespace Paghe.Tests;

/// <summary>
/// Verifica che adempimenti e progressivi usino lo storico pre-workflow senza confonderlo
/// con bozze o stati intermedi e senza oltrepassare il confine del tenant.
/// </summary>
public sealed class CedolinoStatiQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public CedolinoStatiQueryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>()
            .UseSqlite(_connection)
            .Options);

    [Fact]
    public async Task UtilizzabiliAValle_IncludonoChiusaEPreWorkflow_EscludonoGliAltriStati()
    {
        var tenant = Guid.NewGuid();
        var altroTenant = Guid.NewGuid();
        var aziendaId = Guid.NewGuid();
        await using (var db = NuovoContesto())
        {
            await InserisciConStatiAsync(db,
                (Cedolino(tenant, aziendaId, 1, 100m), StatoCedolino.PreWorkflow),
                (Cedolino(tenant, aziendaId, 2, 200m), StatoCedolino.Chiusa),
                (Cedolino(tenant, aziendaId, 3, 400m), StatoCedolino.Bozza),
                (Cedolino(tenant, aziendaId, 4, 800m), StatoCedolino.Controllata),
                (Cedolino(tenant, aziendaId, 5, 1_600m), StatoCedolino.Validata),
                (Cedolino(altroTenant, aziendaId, 6, 3_200m), StatoCedolino.PreWorkflow));
        }

        await using var verifica = NuovoContesto();
        var stati = await verifica.Cedolini
            .UtilizzabiliAValle()
            .Where(c => c.TenantId == tenant && c.AziendaId == aziendaId)
            .OrderBy(c => c.Mese)
            .Select(c => c.Stato)
            .ToListAsync();

        Assert.Equal(
            [nameof(StatoCedolino.PreWorkflow), nameof(StatoCedolino.Chiusa)],
            stati);
    }

    [Fact]
    public async Task Inail_SommaChiusaEPreWorkflow_MaNonBozzaONeAltroTenant()
    {
        var tenant = Guid.NewGuid();
        var altroTenant = Guid.NewGuid();
        var aziendaId = Guid.NewGuid();
        await using (var db = NuovoContesto())
        {
            db.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId,
                TenantId = tenant,
                RagioneSociale = "Storico Paghe Srl",
            });
            await InserisciConStatiAsync(db,
                (Cedolino(tenant, aziendaId, 1, 10_000m), StatoCedolino.PreWorkflow),
                (Cedolino(tenant, aziendaId, 2, 20_000m), StatoCedolino.Chiusa),
                (Cedolino(tenant, aziendaId, 3, 40_000m), StatoCedolino.Bozza),
                (Cedolino(tenant, aziendaId, 4, 80_000m), StatoCedolino.Validata),
                (Cedolino(altroTenant, aziendaId, 5, 160_000m), StatoCedolino.PreWorkflow));

            await new InailService(db).SalvaPosizioneAsync(
                tenant, aziendaId, 2026, "PAT-TEST", "0722", 10m);
        }

        await using var verifica = NuovoContesto();
        var risultato = await new InailService(verifica)
            .CalcolaAutoliquidazioneAsync(tenant, aziendaId, 2026);

        Assert.Equal(2, risultato.NumeroCedolini);
        Assert.Equal(30_000m, risultato.RetribuzioniAnno);
        Assert.Equal(300m, risultato.Regolazione);
    }

    private static CedolinoEntity Cedolino(
        Guid tenantId,
        Guid aziendaId,
        int mese,
        decimal imponibileInps) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AziendaId = aziendaId,
            RapportoLavoroId = Guid.NewGuid(),
            Anno = 2026,
            Mese = mese,
            Stato = nameof(StatoCedolino.Bozza),
            Versione = 1,
            ImponibileInps = imponibileInps,
            CalcolatoIl = new DateTime(2026, mese, 28, 12, 0, 0, DateTimeKind.Utc),
        };

    private static async Task InserisciConStatiAsync(
        PagheDbContext db,
        params (CedolinoEntity Cedolino, StatoCedolino Stato)[] righe)
    {
        db.Cedolini.AddRange(righe.Select(r => r.Cedolino));
        await db.SaveChangesAsync();
        foreach (var (cedolino, stato) in righe.Where(r => r.Stato != StatoCedolino.Bozza))
        {
            var versione = stato switch
            {
                StatoCedolino.PreWorkflow => 1,
                StatoCedolino.Controllata => 2,
                StatoCedolino.Validata => 3,
                StatoCedolino.Chiusa => 4,
                _ => 1,
            };
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE Cedolini SET Stato = {stato.ToString()}, Versione = {versione}
                WHERE TenantId = {cedolino.TenantId} AND Id = {cedolino.Id}
                """);
        }
    }

    public void Dispose() => _connection.Dispose();
}
