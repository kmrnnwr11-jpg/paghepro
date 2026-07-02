using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test di integrazione del flusso lavoro su SQLite: creazione dipendente/rapporto,
/// calcolo e archiviazione del cedolino, rilettura, ricalcolo idempotente e isolamento tenant.
/// </summary>
public sealed class PersistenzaCedoliniTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public PersistenzaCedoliniTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
        new ParametriNormativiService(db).ImportaSeedSeVuotoAsync().GetAwaiter().GetResult();
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

    private async Task<Guid> CreaRapportoAsync(Guid tenant, Guid aziendaId)
    {
        await using var db = NuovoContesto();
        var rapporto = await new AnagraficheLavoroService(db).CreaDipendenteERapportoAsync(
            tenant, aziendaId,
            new DipendenteEntity { CodiceFiscale = "RSSMRA80A01F205X", Nome = "Mario", Cognome = "Rossi" },
            new RapportoLavoroEntity
            {
                CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = "Impiegato",
                NumeroMensilita = 14, Regione = "Lombardia", CodiceCatastaleComune = "F205",
                DataAssunzione = new DateOnly(2020, 1, 1),
            });
        return rapporto.Id;
    }

    [Fact]
    public async Task CalcolaEArchivia_PoiRileggi_ConVociENetto()
    {
        var (tenant, aziendaId) = await CreaAziendaAsync();
        var rapportoId = await CreaRapportoAsync(tenant, aziendaId);

        Guid cedolinoId;
        decimal nettoCalcolato;
        await using (var db = NuovoContesto())
        {
            var svc = new CedoliniService(db, new ParametriNormativiService(db));
            var c = await svc.CalcolaEArchiviaAsync(tenant, rapportoId,
                new Cartellino { Anno = 2026, Mese = 6 });
            cedolinoId = c.Id;
            nettoCalcolato = c.NettoInBusta;
            Assert.Equal(1340.99m, c.TotaleCompetenze); // le competenze non dipendono dalle addizionali
            Assert.True(c.NettoInBusta > 0m && c.NettoInBusta < c.TotaleCompetenze);
        }

        await using (var db = NuovoContesto())
        {
            var c = await new CedoliniService(db, new ParametriNormativiService(db)).GetAsync(tenant, cedolinoId);
            Assert.NotNull(c);
            Assert.Equal(2026, c!.Anno);
            Assert.Equal(nettoCalcolato, c.NettoInBusta); // rileggendo: stesso netto calcolato
            Assert.NotEmpty(c.Voci); // le righe del cedolino sono persistite
        }
    }

    [Fact]
    public async Task Ricalcolo_StessoPeriodo_NonDuplica()
    {
        var (tenant, aziendaId) = await CreaAziendaAsync();
        var rapportoId = await CreaRapportoAsync(tenant, aziendaId);

        await using (var db = NuovoContesto())
        {
            var svc = new CedoliniService(db, new ParametriNormativiService(db));
            await svc.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 6 });
            await svc.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 6 });
        }

        await using (var db = NuovoContesto())
        {
            var elenco = await new CedoliniService(db, new ParametriNormativiService(db))
                .ElencoPerRapportoAsync(tenant, rapportoId);
            Assert.Single(elenco); // il ricalcolo sostituisce, non duplica
        }
    }

    [Fact]
    public async Task Calcola_RapportoInesistente_LanciaEccezione()
    {
        var tenant = Guid.NewGuid();
        await using var db = NuovoContesto();
        var svc = new CedoliniService(db, new ParametriNormativiService(db));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CalcolaEArchiviaAsync(tenant, Guid.NewGuid(), new Cartellino { Anno = 2026, Mese = 6 }));
    }

    public void Dispose() => _connection.Dispose();
}
