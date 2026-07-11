using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Verifica che il dataset ufficiale dei minimi CCNL incorporato in Paghe.Persistence
/// (docs/dati/minimi-ccnl-*.json, fonti CNEL) sovrascriva i valori d'esempio del Seed,
/// come fa l'avvio dell'API: seed → import ufficiale → il motore legge i minimi reali.
/// </summary>
public sealed class CcnlDatasetUfficialeTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public CcnlDatasetUfficialeTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
        new ParametriNormativiService(db).ImportaSeedSeVuotoAsync().GetAwaiter().GetResult();
        new CcnlImportService(db).ImportaAsync(CcnlDatasetUfficiale.Carica()).GetAwaiter().GetResult();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public void Carica_LeggeIlDatasetIncorporato()
    {
        var dataset = CcnlDatasetUfficiale.Carica();

        Assert.Contains(dataset, c => c.Codice == "COMMERCIO_TERZIARIO" && c.Anno == 2026);
        Assert.True(dataset.Count >= 10);
    }

    [Fact]
    public async Task DopoImport_IMinimiSonoQuelliUfficiali_NonQuelliDelSeed()
    {
        await using var db = NuovoContesto();
        var ccnl = await new ParametriNormativiService(db).CaricaCcnlAsync("COMMERCIO_TERZIARIO", 2026);

        // Valori del dataset ufficiale (CNEL CCNL 242377), non più 1800,94 / 1330,66 del Seed.
        Assert.Equal(2504.06m, ccnl.Livello("1").MinimoTabellare);
        Assert.Equal(1981.84m, ccnl.Livello("3").MinimoTabellare);
    }

    [Fact]
    public async Task LivelliSenzaMinimoTabellare_NonVengonoImportati()
    {
        await using var db = NuovoContesto();

        // Il 6J della logistica non è riportato nella tabella minimi CNEL: 23 livelli nel file, 22 importati.
        var livelli = await db.CcnlLivelli
            .Where(l => l.CcnlCodice == "TRASPORTI_LOGISTICA" && l.Anno == 2026).ToListAsync();
        Assert.Equal(22, livelli.Count);
        Assert.DoesNotContain(livelli, l => l.Livello == "6J");
    }

    [Fact]
    public async Task Import_Idempotente_NonDuplicaILivelli()
    {
        await using (var db = NuovoContesto())
            await new CcnlImportService(db).ImportaAsync(CcnlDatasetUfficiale.Carica()); // seconda volta

        await using var check = NuovoContesto();
        var livelli = await check.CcnlLivelli
            .Where(l => l.CcnlCodice == "COMMERCIO_TERZIARIO" && l.Anno == 2026).CountAsync();
        Assert.Equal(8, livelli);
    }

    public void Dispose() => _connection.Dispose();
}
