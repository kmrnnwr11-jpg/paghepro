using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Paghe.Persistence.Gdpr;
using Xunit;

namespace Paghe.Tests;

/// <summary>Il limite mensile resta esatto anche con richieste simultanee su DbContext distinti.</summary>
public sealed class ConsumoAiConcorrenzaTests
{
    [Fact]
    public async Task PrimaRigaConcorrente_LimiteSette_ConsenteEsattamenteSetteChiamate()
    {
        var percorso = PercorsoTemporaneo();
        try
        {
            var opzioni = await PreparaAsync(percorso);
            var tenant = Guid.NewGuid();

            var esiti = await Task.WhenAll(Enumerable.Range(0, 32)
                .Select(_ => ConsumaAsync(opzioni, tenant, limite: 7)));

            Assert.Equal(7, esiti.Count(x => x));
            await using var verifica = new PagheDbContext(opzioni);
            var riga = await verifica.ConsumiAi.AsNoTracking().SingleAsync(c => c.TenantId == tenant);
            Assert.Equal(7, riga.Chiamate);
            Assert.Equal(7, await verifica.AuditLog.AsNoTracking().CountAsync(a =>
                a.TenantId == tenant && a.Entita == "ConsumiAi"));
        }
        finally
        {
            Elimina(percorso);
        }
    }

    [Fact]
    public async Task RigaAlLimiteMenoUno_Concorrenza_AmmetteUnSoloIncremento()
    {
        var percorso = PercorsoTemporaneo();
        try
        {
            var opzioni = await PreparaAsync(percorso);
            var tenant = Guid.NewGuid();
            for (var i = 0; i < 6; i++)
                Assert.True(await ConsumaAsync(opzioni, tenant, limite: 7));

            var esiti = await Task.WhenAll(Enumerable.Range(0, 24)
                .Select(_ => ConsumaAsync(opzioni, tenant, limite: 7)));

            Assert.Equal(1, esiti.Count(x => x));
            await using var verifica = new PagheDbContext(opzioni);
            Assert.Equal(7, await verifica.ConsumiAi.AsNoTracking()
                .Where(c => c.TenantId == tenant)
                .Select(c => c.Chiamate)
                .SingleAsync());
        }
        finally
        {
            Elimina(percorso);
        }
    }

    [Fact]
    public async Task IllimitatoEDueTenant_NessunLostUpdateENessunaInterferenza()
    {
        var percorso = PercorsoTemporaneo();
        try
        {
            var opzioni = await PreparaAsync(percorso);
            var tenantA = Guid.NewGuid();
            var tenantB = Guid.NewGuid();
            var lavori = Enumerable.Range(0, 20)
                .SelectMany(_ => new[]
                {
                    ConsumaAsync(opzioni, tenantA, limite: 0),
                    ConsumaAsync(opzioni, tenantB, limite: 0),
                });

            var esiti = await Task.WhenAll(lavori);

            Assert.All(esiti, Assert.True);
            await using var verifica = new PagheDbContext(opzioni);
            var consumi = await verifica.ConsumiAi.AsNoTracking()
                .ToDictionaryAsync(c => c.TenantId, c => c.Chiamate);
            Assert.Equal(20, consumi[tenantA]);
            Assert.Equal(20, consumi[tenantB]);
        }
        finally
        {
            Elimina(percorso);
        }
    }

    [Fact]
    public async Task TenantVuotoODiversoDalContestoAudit_FalliscePrimaDellaQuery()
    {
        var percorso = PercorsoTemporaneo();
        try
        {
            var opzioni = await PreparaAsync(percorso);
            await using var db = new PagheDbContext(opzioni);
            var servizio = new ConsumoAiService(db);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                servizio.ProvaConsumaAsync(Guid.Empty, 10));

            using var _ = AmbientContestoAudit.Imposta(Guid.NewGuid(), Guid.NewGuid());
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                servizio.ProvaConsumaAsync(Guid.NewGuid(), 10));
            Assert.Empty(await db.ConsumiAi.AsNoTracking().ToListAsync());
        }
        finally
        {
            Elimina(percorso);
        }
    }

    private static async Task<DbContextOptions<PagheDbContext>> PreparaAsync(string percorso)
    {
        var opzioni = new DbContextOptionsBuilder<PagheDbContext>()
            .UseSqlite($"Data Source={percorso};Default Timeout=30;Pooling=False")
            .Options;
        await using var db = new PagheDbContext(opzioni);
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        return opzioni;
    }

    private static async Task<bool> ConsumaAsync(
        DbContextOptions<PagheDbContext> opzioni, Guid tenant, int limite)
    {
        await using var db = new PagheDbContext(opzioni);
        return await new ConsumoAiService(db).ProvaConsumaAsync(tenant, limite);
    }

    private static string PercorsoTemporaneo() =>
        Path.Combine(Path.GetTempPath(), $"paghe-consumo-ai-{Guid.NewGuid():N}.db");

    private static void Elimina(string percorso)
    {
        foreach (var file in new[] { percorso, percorso + "-wal", percorso + "-shm" })
            if (File.Exists(file)) File.Delete(file);
    }
}
