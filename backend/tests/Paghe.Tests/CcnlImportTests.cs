using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Xunit;

namespace Paghe.Tests;

/// <summary>Test dell'import in blocco dei CCNL (caricamento tabelle reali da dati esterni).</summary>
public sealed class CcnlImportTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public CcnlImportTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = Ctx();
        db.Database.EnsureCreated();
    }

    private PagheDbContext Ctx() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task Importa_CaricaUnCcnlEloRendeUsabile()
    {
        var input = new List<CcnlInput>
        {
            new("METALMECCANICI_ARTIGIANATO", "CCNL Metalmeccanici Artigianato", 2026, new()
            {
                new("1", "Primo livello", 1300m, 0m, 20m),
                new("2", "Secondo livello", 1450m, 0m, 20m),
            }),
        };

        await using (var db = Ctx())
        {
            var (ccnl, livelli) = await new CcnlImportService(db).ImportaAsync(input);
            Assert.Equal(1, ccnl);
            Assert.Equal(2, livelli);
        }

        await using (var db = Ctx())
        {
            var c = await new ParametriNormativiService(db).CaricaCcnlAsync("METALMECCANICI_ARTIGIANATO", 2026);
            Assert.Equal(2, c.Livelli.Count);
            Assert.Equal(1450m, c.Livello("2").MinimoTabellare);
        }
    }

    [Fact]
    public async Task Importa_Aggiorna_SostituisceILivelliDellAnno()
    {
        await using (var db = Ctx())
            await new CcnlImportService(db).ImportaAsync(new List<CcnlInput>
            {
                new("X", "X v1", 2026, new() { new("1", "a", 1000m, 0m, 0m) }),
            });

        await using (var db = Ctx())
            await new CcnlImportService(db).ImportaAsync(new List<CcnlInput>
            {
                new("X", "X v2", 2026, new() { new("1", "a", 1100m, 0m, 0m), new("2", "b", 1200m, 0m, 0m) }),
            });

        await using (var db = Ctx())
        {
            var c = await new ParametriNormativiService(db).CaricaCcnlAsync("X", 2026);
            Assert.Equal(2, c.Livelli.Count);
            Assert.Equal(1100m, c.Livello("1").MinimoTabellare); // valore aggiornato, non duplicato
        }
    }

    public void Dispose() => _connection.Dispose();
}
