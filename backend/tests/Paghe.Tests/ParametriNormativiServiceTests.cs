using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Calcolo;
using Paghe.Core.Domain;
using Paghe.Core.Parametri;
using Paghe.Persistence;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Verifica che i parametri normativi caricati dal database coincidano con i dati di
/// esempio e, soprattutto, che il calcolo del cedolino con i parametri letti dal DB sia
/// identico a quello ottenuto con i parametri cablati (Seed).
/// </summary>
public sealed class ParametriNormativiServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ParametriNormativiServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
        new ParametriNormativiService(db).ImportaSeedSeVuotoAsync().GetAwaiter().GetResult();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task CaricaParametri_DalDb_CoincideConIlSeed()
    {
        await using var db = NuovoContesto();
        var p = await new ParametriNormativiService(db).CaricaParametriAsync(2026);

        Assert.Equal(3, p.ScaglioniIrpef.Count);
        Assert.Equal(0.0919m, p.AliquoteInps[Qualifica.Impiegato].Dipendente);
        Assert.Equal(0.0123m, p.AddizionaleRegionale["Lombardia"]);
        Assert.Equal(0.0080m, p.AddizionaleComunale["F205"]);
        Assert.Equal(168m, p.DivisoreOrario);
        Assert.Equal(13.5m, p.TfrDivisore);
    }

    [Fact]
    public async Task CaricaCcnl_DalDb_MinimiCorretti()
    {
        await using var db = NuovoContesto();
        var ccnl = await new ParametriNormativiService(db).CaricaCcnlAsync("COMMERCIO_TERZIARIO", 2026);

        Assert.Equal(1330.66m, ccnl.Livello("3").MinimoTabellare);
        Assert.Equal(8, ccnl.Livelli.Count);
    }

    [Fact]
    public async Task Calcolo_ConParametriDalDb_IdenticoAlSeed()
    {
        await using var db = NuovoContesto();
        var svc = new ParametriNormativiService(db);
        var parametri = await svc.CaricaParametriAsync(2026);
        var ccnl = await svc.CaricaCcnlAsync("COMMERCIO_TERZIARIO", 2026);

        var motore = new MotoreCalcoloCedolino(parametri, ccnl);
        var cedolino = motore.Calcola(
            new RapportoLavoro
            {
                CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = Qualifica.Impiegato,
                NumeroMensilita = 14, // senza residenza fiscale: confronta il calcolo base DB vs Seed
            },
            new Cartellino { Anno = 2026, Mese = 6 });

        // Stesso risultato del calcolo col Seed (senza addizionali): netto 1100,59.
        Assert.Equal(1340.99m, cedolino.TotaleCompetenze);
        Assert.Equal(1100.59m, cedolino.NettoInBusta);
    }

    [Fact]
    public async Task CaricaParametri_AnnoNonPresente_LanciaEccezione()
    {
        await using var db = NuovoContesto();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ParametriNormativiService(db).CaricaParametriAsync(1999));
    }

    [Fact]
    public async Task ImportaSeed_Idempotente_NonDuplica()
    {
        await using (var db = NuovoContesto())
            await new ParametriNormativiService(db).ImportaSeedSeVuotoAsync(); // seconda chiamata

        await using var check = NuovoContesto();
        Assert.Equal(1, await check.ParametriAnno.CountAsync());
        Assert.Equal(3, await check.IrpefScaglioni.CountAsync());
    }

    public void Dispose() => _connection.Dispose();
}
