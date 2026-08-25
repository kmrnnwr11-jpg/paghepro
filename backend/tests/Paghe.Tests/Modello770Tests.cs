using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test della bozza Modello 770 per azienda: aggrega le ritenute dei cedolini per percipiente e
/// produce l'export CSV (bozza rivedibile, non il file ufficiale).
/// </summary>
public sealed class Modello770Tests : IDisposable
{
    private readonly SqliteConnection _connection;

    public Modello770Tests()
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
    public async Task Bozza770_AggregaRitenutePerPercipiente_EdExportCsv()
    {
        var tenant = Guid.NewGuid();
        Guid aziendaId, rapportoId;

        await using (var db = NuovoContesto())
        {
            var az = await new AziendeService(db).CreaAsync(tenant, new AziendaEntity
            {
                RagioneSociale = "Alfa Srl", PartitaIva = "01234561007",
                Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
            });
            aziendaId = az.Id;
            var rapporto = await new AnagraficheLavoroService(db).CreaDipendenteERapportoAsync(
                tenant, aziendaId,
                new DipendenteEntity { CodiceFiscale = "RSSMRA80A01F205X", Nome = "Mario", Cognome = "Rossi" },
                new RapportoLavoroEntity
                {
                    CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = "Impiegato",
                    NumeroMensilita = 14, Regione = "Lombardia", CodiceCatastaleComune = "F205",
                    DataAssunzione = new DateOnly(2020, 1, 1),
                });
            rapportoId = rapporto.Id;
        }

        await using (var db = NuovoContesto())
        {
            var svc = new CedoliniService(db, new ParametriNormativiService(db));
            await svc.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 1 });
            await svc.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 2 });
            foreach (var cedolino in db.Cedolini.Local)
                await db.ClassificaPreWorkflowAsync(tenant, cedolino.Id);
        }

        await using (var db = NuovoContesto())
        {
            var bozza = await new Modello770Service(db).CostruisciAsync(tenant, aziendaId, 2026);

            Assert.Equal(1, bozza.NumeroPercipienti);
            Assert.Equal(2, bozza.NumeroCedolini);
            var p = Assert.Single(bozza.Percipienti);
            Assert.Equal("RSSMRA80A01F205X", p.CodiceFiscale);
            Assert.Equal(bozza.TotaleRitenuteIrpef, p.RitenuteIrpef); // unico percipiente = totale

            var csv = Modello770Service.EsportaCsv(bozza);
            Assert.StartsWith("Nominativo;CodiceFiscale;RitenuteIrpef;", csv);
            Assert.Contains("RSSMRA80A01F205X", csv);
            Assert.Equal(2, csv.TrimEnd().Split('\n').Length); // header + 1 riga
        }
    }

    public void Dispose() => _connection.Dispose();
}
