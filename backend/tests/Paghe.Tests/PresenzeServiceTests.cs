using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Entities;

namespace Paghe.Tests;

public sealed class PresenzeServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public PresenzeServiceTests()
    {
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task Costruisci_RileggeTutteLeCausali_EIsolaIlTenant()
    {
        var tenant = Guid.NewGuid();
        var estraneo = Guid.NewGuid();
        var aziendaId = Guid.NewGuid();
        var dipendenteId = Guid.NewGuid();
        var rapportoId = Guid.NewGuid();

        await using (var db = NuovoContesto())
        {
            db.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId, TenantId = tenant, RagioneSociale = "Alfa Srl",
                Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
            });
            db.Dipendenti.Add(new DipendenteEntity
            {
                Id = dipendenteId, TenantId = tenant, Cognome = "Rossi", Nome = "Mario",
                CodiceFiscale = "RSSMRA80A01F205X",
            });
            db.Rapporti.Add(new RapportoLavoroEntity
            {
                Id = rapportoId, TenantId = tenant, AziendaId = aziendaId,
                DipendenteId = dipendenteId, CodiceCcnl = "COMMERCIO_TERZIARIO",
                Livello = "3", Qualifica = "Impiegato",
            });
            db.Cartellini.Add(new CartellinoEntity
            {
                Id = Guid.NewGuid(), TenantId = tenant, RapportoLavoroId = rapportoId,
                Anno = 2026, Mese = 7,
                OreStraordinario = 6m, OreStraordinarioNotturno = 2m,
                OreStraordinarioFestivo = 1m, GiorniFerie = 3m,
                OrePermessi = 4m, OreExFestivita = 2m,
                GiorniMalattia = 5m, NumeroEventoMalattiaAnno = 2,
                GiorniInfortunio = 1m, OreAssenzaNonRetribuita = 8m,
            });
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            var riga = Assert.Single((await new PresenzeService(db)
                .CostruisciAsync(tenant, aziendaId, 2026, 7)).Righe);
            Assert.Equal("Rossi Mario", riga.Nominativo);
            Assert.Equal(6m, riga.OreStraordinario);
            Assert.Equal(2m, riga.OreStraordinarioNotturno);
            Assert.Equal(1m, riga.OreStraordinarioFestivo);
            Assert.Equal(3m, riga.GiorniFerie);
            Assert.Equal(4m, riga.OrePermessi);
            Assert.Equal(2m, riga.OreExFestivita);
            Assert.Equal(5m, riga.GiorniMalattia);
            Assert.Equal(2, riga.NumeroEventoMalattiaAnno);
            Assert.Equal(1m, riga.GiorniInfortunio);
            Assert.Equal(8m, riga.OreAssenzaNonRetribuita);

            var vistaEstranea = await new PresenzeService(db)
                .CostruisciAsync(estraneo, aziendaId, 2026, 7);
            Assert.Empty(vistaEstranea.Righe);
        }
    }

    [Fact]
    public async Task Costruisci_EsponeIResiduiDallUltimoCedolinoPrecedenteAlMese()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = Guid.NewGuid();
        var dipendenteId = Guid.NewGuid();
        var rapportoId = Guid.NewGuid();
        var cedolinoAprileId = Guid.NewGuid();
        var cedolinoMaggioId = Guid.NewGuid();

        await using (var db = NuovoContesto())
        {
            db.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId, TenantId = tenant, RagioneSociale = "Alfa Srl",
                Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
            });
            db.Dipendenti.Add(new DipendenteEntity
            {
                Id = dipendenteId, TenantId = tenant, Cognome = "Rossi", Nome = "Mario",
                CodiceFiscale = "RSSMRA80A01F205X",
            });
            db.Rapporti.Add(new RapportoLavoroEntity
            {
                Id = rapportoId, TenantId = tenant, AziendaId = aziendaId,
                DipendenteId = dipendenteId, CodiceCcnl = "COMMERCIO_TERZIARIO",
                Livello = "3", Qualifica = "Impiegato",
            });
            db.Cedolini.AddRange(
                new CedolinoEntity
                {
                    Id = cedolinoAprileId, TenantId = tenant, AziendaId = aziendaId,
                    RapportoLavoroId = rapportoId, Anno = 2026, Mese = 4,
                    FerieResidue = 8.67m, PermessiResidui = 24m,
                    Stato = nameof(StatoCedolino.Bozza), Versione = 1,
                },
                new CedolinoEntity
                {
                    Id = cedolinoMaggioId, TenantId = tenant, AziendaId = aziendaId,
                    RapportoLavoroId = rapportoId, Anno = 2026, Mese = 5,
                    FerieResidue = 10.83m, PermessiResidui = 30m,
                    Stato = nameof(StatoCedolino.Bozza), Versione = 1,
                });
            await db.SaveChangesAsync();
            await db.ClassificaPreWorkflowAsync(tenant, cedolinoAprileId);
            await db.ClassificaPreWorkflowAsync(tenant, cedolinoMaggioId);
        }

        await using (var db = NuovoContesto())
        {
            var servizio = new PresenzeService(db);

            // Giugno vede i saldi del cedolino più recente (maggio), non quello di aprile.
            var giugno = Assert.Single((await servizio.CostruisciAsync(tenant, aziendaId, 2026, 6)).Righe);
            Assert.Equal(10.83m, giugno.FerieResidue);
            Assert.Equal(30m, giugno.PermessiResidui);
            Assert.Equal(2026, giugno.SaldiAnno);
            Assert.Equal(5, giugno.SaldiMese);

            // Aprile non ha cedolini precedenti: nessun numero inventato.
            var aprile = Assert.Single((await servizio.CostruisciAsync(tenant, aziendaId, 2026, 4)).Righe);
            Assert.Null(aprile.FerieResidue);
            Assert.Null(aprile.PermessiResidui);
            Assert.Null(aprile.SaldiAnno);
            Assert.Null(aprile.SaldiMese);
        }
    }

    public void Dispose() => _connection.Dispose();
}
