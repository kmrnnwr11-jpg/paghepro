using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test della Certificazione Unica per azienda: una CU per ogni rapporto con cedolini archiviati
/// nell'anno (esclusi quelli senza), più l'export CSV (bozza rivedibile).
/// </summary>
public sealed class CertificazioneUnicaTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public CertificazioneUnicaTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
        new ParametriNormativiService(db).ImportaSeedSeVuotoAsync().GetAwaiter().GetResult();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    private async Task<Guid> CreaRapportoAsync(Guid tenant, Guid aziendaId, string cf, string cognome)
    {
        await using var db = NuovoContesto();
        var rapporto = await new AnagraficheLavoroService(db).CreaDipendenteERapportoAsync(
            tenant, aziendaId,
            new DipendenteEntity { CodiceFiscale = cf, Nome = "Mario", Cognome = cognome },
            new RapportoLavoroEntity
            {
                CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = "Impiegato",
                NumeroMensilita = 14, Regione = "Lombardia", CodiceCatastaleComune = "F205",
                DataAssunzione = new DateOnly(2020, 1, 1),
            });
        return rapporto.Id;
    }

    [Fact]
    public async Task CuPerAzienda_SoloRapportiConCedolini_EdExportCsv()
    {
        var tenant = Guid.NewGuid();
        Guid aziendaId;
        await using (var db = NuovoContesto())
        {
            var az = await new AziendeService(db).CreaAsync(tenant, new AziendaEntity
            {
                RagioneSociale = "Alfa Srl", PartitaIva = "01234561007",
                Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
            });
            aziendaId = az.Id;
        }

        var conCedolini = await CreaRapportoAsync(tenant, aziendaId, "RSSMRA80A01F205X", "Rossi");
        await CreaRapportoAsync(tenant, aziendaId, "VRDLGI85A01F205M", "Verdi"); // senza cedolini

        await using (var db = NuovoContesto())
        {
            var svc = new CedoliniService(db, new ParametriNormativiService(db));
            await svc.CalcolaEArchiviaAsync(tenant, conCedolini, new Cartellino { Anno = 2026, Mese = 1 });
            await svc.CalcolaEArchiviaAsync(tenant, conCedolini, new Cartellino { Anno = 2026, Mese = 2 });
            foreach (var cedolino in db.Cedolini.Local)
                await db.ClassificaPreWorkflowAsync(tenant, cedolino.Id);
        }

        await using (var db = NuovoContesto())
        {
            var svc = new CertificazioneUnicaService(db);
            var bozze = await svc.CostruisciPerAziendaAsync(tenant, aziendaId, 2026);

            var cu = Assert.Single(bozze); // solo il rapporto con cedolini
            Assert.Equal("RSSMRA80A01F205X", cu.CodiceFiscale);
            Assert.Equal(2, cu.NumeroCedolini);

            var csv = CertificazioneUnicaService.EsportaCsv(bozze);
            Assert.StartsWith("CodiceFiscale;Nominativo;Anno;", csv);
            Assert.Contains("RSSMRA80A01F205X", csv);
            Assert.Equal(2, csv.TrimEnd().Split('\n').Length); // header + 1 riga dati
        }
    }

    public void Dispose() => _connection.Dispose();
}
