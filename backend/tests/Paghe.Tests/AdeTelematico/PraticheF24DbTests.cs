using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico;
using Paghe.Persistence;
using Paghe.Persistence.Entities;

namespace Paghe.Tests.AdeTelematico;

/// <summary>
/// Integrazione su SQLite: archiviazione della pratica F24 generata dai cedolini, lettura,
/// transizioni di stato ed export su cartella.
/// </summary>
public sealed class PraticheF24DbTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _aziendaId = Guid.NewGuid();

    public PraticheF24DbTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
        SeedPaghe(db);
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    private PraticheF24Service Servizio(PagheDbContext db) =>
        new(db, new CedoliniService(db, new ParametriNormativiService(db)));

    [Fact]
    public async Task Genera_archivia_e_rileggi()
    {
        Guid id;
        await using (var db = NuovoContesto())
        {
            var p = await Servizio(db).GeneraEArchiviaDelMeseAsync(_tenant, _aziendaId, 2026, 6, "1234567890", "7100");
            id = p.Id;
            Assert.Equal(nameof(StatoPraticaTelematica.Generata), p.Stato);
            Assert.True(p.Valido);
            Assert.Equal(1550m, p.SaldoFinale);
            Assert.Contains("BOZZA NON UFFICIALE", p.Bozza);
        }

        await using (var db = NuovoContesto())
        {
            var p = await Servizio(db).GetAsync(_tenant, id);
            Assert.NotNull(p);
            Assert.Contains("1001", p!.ModelloJson);   // il codice tributo è serializzato nel JSON

            var elenco = await Servizio(db).ElencoAsync(_tenant, _aziendaId);
            Assert.Single(elenco);
        }
    }

    [Fact]
    public async Task Transizione_di_stato_valida_e_invalida()
    {
        Guid id;
        await using (var db = NuovoContesto())
            id = (await Servizio(db).GeneraEArchiviaDelMeseAsync(_tenant, _aziendaId, 2026, 6, "1234567890")).Id;

        await using (var db = NuovoContesto())
        {
            var p = await Servizio(db).CambiaStatoAsync(_tenant, id, StatoPraticaTelematica.ControllataOk);
            Assert.Equal(nameof(StatoPraticaTelematica.ControllataOk), p.Stato);
        }

        await using (var db = NuovoContesto())
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Servizio(db).CambiaStatoAsync(_tenant, id, StatoPraticaTelematica.RicevutaAcquisita));
    }

    [Fact]
    public async Task Esporta_scrive_il_file()
    {
        Guid id;
        await using (var db = NuovoContesto())
            id = (await Servizio(db).GeneraEArchiviaDelMeseAsync(_tenant, _aziendaId, 2026, 6, "1234567890")).Id;

        string baseDir = Path.Combine(Path.GetTempPath(), "paghe-export-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            string file;
            await using (var db = NuovoContesto())
                file = await Servizio(db).EsportaAsync(_tenant, id, baseDir);

            Assert.True(File.Exists(file));
            Assert.Contains(Path.Combine("agenzia-entrate", "da-controllare"), file);
            // L'export scrive il tracciato telematico ufficiale (record A/M/V/Z, codice fornitura F24A0).
            string contenuto = await File.ReadAllTextAsync(file);
            Assert.StartsWith("A", contenuto);
            Assert.Contains("F24A0", contenuto);
        }
        finally
        {
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true);
        }
    }

    private void SeedPaghe(PagheDbContext db)
    {
        var dip1 = Guid.NewGuid();
        var dip2 = Guid.NewGuid();
        var rap1 = Guid.NewGuid();
        var rap2 = Guid.NewGuid();

        db.Aziende.Add(new AziendaEntity
        {
            Id = _aziendaId, TenantId = _tenant, RagioneSociale = "Acme Srl",
            PartitaIva = "01234567897", CodiceFiscale = "01234567897",
            Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
        });
        db.Dipendenti.AddRange(
            new DipendenteEntity { Id = dip1, TenantId = _tenant, CodiceFiscale = "RSSMRA85T10A562S", Nome = "Mario", Cognome = "Rossi" },
            new DipendenteEntity { Id = dip2, TenantId = _tenant, CodiceFiscale = "VRDLGI80A01F205X", Nome = "Luigi", Cognome = "Verdi" });
        db.Rapporti.AddRange(Rapporto(rap1, dip1), Rapporto(rap2, dip2));
        db.Cedolini.AddRange(
            Cedolino(rap1, irpef: 300m, addReg: 20m, addCom: 10m, inpsDip: 150m, inpsDat: 450m),
            Cedolino(rap2, irpef: 200m, addReg: 15m, addCom: 5m, inpsDip: 100m, inpsDat: 300m));
        db.SaveChanges();
    }

    private RapportoLavoroEntity Rapporto(Guid id, Guid dipendenteId) => new()
    {
        Id = id, TenantId = _tenant, AziendaId = _aziendaId, DipendenteId = dipendenteId,
        CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = "Impiegato",
        Regione = "12", CodiceCatastaleComune = "F205", DataAssunzione = new DateOnly(2020, 1, 1),
    };

    private CedolinoEntity Cedolino(
        Guid rapportoId, decimal irpef, decimal addReg, decimal addCom, decimal inpsDip, decimal inpsDat) => new()
    {
        Id = Guid.NewGuid(), TenantId = _tenant, AziendaId = _aziendaId, RapportoLavoroId = rapportoId,
        Anno = 2026, Mese = 6,
        IrpefNetta = irpef, AddizionaleRegionale = addReg, AddizionaleComunale = addCom,
        ContributiDipendente = inpsDip, ContributiDatore = inpsDat, CalcolatoIl = DateTime.UtcNow,
    };

    public void Dispose() => _connection.Dispose();
}
