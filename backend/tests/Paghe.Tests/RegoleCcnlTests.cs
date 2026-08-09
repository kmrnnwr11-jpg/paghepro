using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Calcolo;
using Paghe.Core.Domain;
using Paghe.Core.Parametri;
using Paghe.Persistence;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Divisori e maggiorazioni dello straordinario cambiano da contratto a contratto. Erano scritti
/// dentro il motore con il commento «es. CCNL Commercio»: la logistica pagava lo straordinario al
/// 15% invece del 30%, l'agricoltura calcolava la paga oraria su 168 invece che su 169.
/// </summary>
public sealed class RegoleCcnlTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public RegoleCcnlTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();

    private static RapportoLavoro Operaio(string ccnl) => new()
    {
        CodiceCcnl = ccnl, Livello = "3", Qualifica = Qualifica.Operaio,
        NumeroMensilita = 14, Regione = "Lombardia", CodiceCatastaleComune = "F205",
    };

    private static Cartellino ConStraordinario(decimal ore) =>
        new() { Anno = 2026, Mese = 3, OreStraordinario = ore };

    private static decimal ImportoStraordinario(Cedolino c) =>
        c.Righe.Single(r => r.Codice == "STRAORD").Importo;

    // ---- Il motore usa le regole del contratto ----

    [Fact]
    public void SenzaRegoleDelContratto_ValgonoIDefaultDellAnno()
    {
        var parametri = Seed.Parametri2026();
        var regole = parametri.Regole("CONTRATTO_SENZA_TABELLA");

        Assert.Equal(parametri.DivisoreOrario, regole.DivisoreOrario);
        Assert.Equal(parametri.MaggiorazioneStraordinario, regole.MaggiorazioneStraordinario);
        Assert.Equal(26m, regole.DivisoreGiornaliero);
        Assert.Equal(2.1667m, regole.GiorniFerieMese);
    }

    [Fact]
    public void ConLeRegoleDelContratto_LoStraordinarioCambia()
    {
        var senza = Seed.Parametri2026();
        var conRegole = new ParametriNormativi
        {
            Anno = senza.Anno,
            ScaglioniIrpef = senza.ScaglioniIrpef,
            AliquoteInps = senza.AliquoteInps,
            AddizionaleRegionale = senza.AddizionaleRegionale,
            AddizionaleComunale = senza.AddizionaleComunale,
            RegoleCcnl = new Dictionary<string, RegoleCcnl>(StringComparer.OrdinalIgnoreCase)
            {
                // Logistica: straordinario diurno al 30%, verificato su busta reale.
                ["TRASPORTI_LOGISTICA"] = new(168m, 26m, 0.30m, 0.50m, 0.65m, 2.1667m, 6m),
            },
        };
        var ccnl = Seed.CcnlTrasportiLogistica2026();

        var conDefault = new MotoreCalcoloCedolino(senza, ccnl)
            .Calcola(Operaio("TRASPORTI_LOGISTICA"), ConStraordinario(10m));
        var conContratto = new MotoreCalcoloCedolino(conRegole, ccnl)
            .Calcola(Operaio("TRASPORTI_LOGISTICA"), ConStraordinario(10m));

        // Stesse ore, stessa paga base: cambia solo la maggiorazione, 15% → 30%.
        decimal atteso = Arrotondamento.Euro(
            ImportoStraordinario(conDefault) / 1.15m * 1.30m);
        Assert.Equal(atteso, ImportoStraordinario(conContratto), precision: 1);
        Assert.True(ImportoStraordinario(conContratto) > ImportoStraordinario(conDefault));
    }

    [Fact]
    public void IlDivisoreOrarioDelContratto_CambiaLaPagaOraria()
    {
        var parametri = Seed.Parametri2026();
        var conAgricoltura = new ParametriNormativi
        {
            Anno = parametri.Anno,
            ScaglioniIrpef = parametri.ScaglioniIrpef,
            AliquoteInps = parametri.AliquoteInps,
            AddizionaleRegionale = parametri.AddizionaleRegionale,
            AddizionaleComunale = parametri.AddizionaleComunale,
            RegoleCcnl = new Dictionary<string, RegoleCcnl>(StringComparer.OrdinalIgnoreCase)
            {
                // CCNL operai agricoli, art. 50: paga oraria = mensile / 169.
                ["AGRICOLTURA_OPERAI"] = new(169m, 26m, 0.25m, 0m, 0.40m, 2.1667m, 6m),
            },
        };
        var ccnl = Seed.CcnlAgricolturaOperai2026();

        var cedolino = new MotoreCalcoloCedolino(conAgricoltura, ccnl)
            .Calcola(Operaio("AGRICOLTURA_OPERAI"), ConStraordinario(1m));

        decimal minimo = ccnl.Livello("3").MinimoTabellare;
        decimal attesa = Arrotondamento.Euro(minimo / 169m * 1.25m);
        Assert.Equal(attesa, ImportoStraordinario(cedolino));
    }

    // ---- Il dataset ufficiale arriva fino al database ----

    [Fact]
    public void IlDatasetPortaLeRegoleDeiContrattiConosciuti()
    {
        var regole = RegoleCcnlDataset.Carica();

        var agricoltura = Assert.Single(regole, r => r.CcnlCodice == "AGRICOLTURA_OPERAI" && r.Anno == 2026);
        Assert.Equal(169m, agricoltura.DivisoreOrario);
        Assert.Equal(0.25m, agricoltura.MaggiorazioneStraordinario);

        var logistica = Assert.Single(regole, r => r.CcnlCodice == "TRASPORTI_LOGISTICA" && r.Anno == 2026);
        Assert.Equal(0.30m, logistica.MaggiorazioneStraordinario);

        // Quello che il dataset non dice resta nullo: lo colma il default dell'anno.
        Assert.Null(agricoltura.DivisoreGiornaliero);
        Assert.Null(agricoltura.GiorniFerieMese);
        Assert.All(regole, r => Assert.False(string.IsNullOrWhiteSpace(r.Fonte)));
    }

    [Fact]
    public async Task DopoLImport_IlMotoreLeggeLeRegoleDalDatabase()
    {
        await using var db = NuovoContesto();
        var servizio = new ParametriNormativiService(db);
        await servizio.ImportaSeedSeVuotoAsync();
        await RegoleCcnlDataset.ImportaAsync(db, RegoleCcnlDataset.Carica());

        var parametri = await servizio.CaricaParametriAsync(2026);

        Assert.Equal(169m, parametri.Regole("AGRICOLTURA_OPERAI").DivisoreOrario);
        Assert.Equal(0.30m, parametri.Regole("TRASPORTI_LOGISTICA").MaggiorazioneStraordinario);
        // Il Commercio non è nel dataset: continua con i valori generali dell'anno.
        Assert.Equal(parametri.DivisoreOrario, parametri.Regole("COMMERCIO_TERZIARIO").DivisoreOrario);
    }

    [Fact]
    public async Task LImportSiPuoRipetere_SenzaDuplicare()
    {
        await using var db = NuovoContesto();
        await new ParametriNormativiService(db).ImportaSeedSeVuotoAsync();

        await RegoleCcnlDataset.ImportaAsync(db, RegoleCcnlDataset.Carica());
        await RegoleCcnlDataset.ImportaAsync(db, RegoleCcnlDataset.Carica());

        Assert.Single(await db.RegoleCcnl
            .Where(r => r.CcnlCodice == "AGRICOLTURA_OPERAI" && r.Anno == 2026)
            .ToListAsync());
    }
}
