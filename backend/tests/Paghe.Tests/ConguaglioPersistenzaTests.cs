using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Calcolo;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test del conguaglio fiscale annuale agganciato ai cedolini archiviati: aggrega imponibile e
/// IRPEF trattenuta dell'anno e ricalcola il dovuto. Verifica che il servizio produca lo stesso
/// risultato del motore <see cref="ConguaglioFiscale"/> applicato ai dati aggregati.
/// </summary>
public sealed class ConguaglioPersistenzaTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ConguaglioPersistenzaTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
        new ParametriNormativiService(db).ImportaSeedSeVuotoAsync().GetAwaiter().GetResult();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    private async Task<(Guid tenant, Guid rapportoId)> CreaRapportoAsync()
    {
        var tenant = Guid.NewGuid();
        await using var db = NuovoContesto();
        var az = await new AziendeService(db).CreaAsync(tenant, new AziendaEntity
        {
            RagioneSociale = "Alfa Srl", PartitaIva = "01234567890",
            Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
        });
        var rapporto = await new AnagraficheLavoroService(db).CreaDipendenteERapportoAsync(
            tenant, az.Id,
            new DipendenteEntity { CodiceFiscale = "RSSMRA80A01F205X", Nome = "Mario", Cognome = "Rossi" },
            new RapportoLavoroEntity
            {
                CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = "Impiegato",
                NumeroMensilita = 14, Regione = "Lombardia", CodiceCatastaleComune = "F205",
                DataAssunzione = new DateOnly(2020, 1, 1),
            });
        return (tenant, rapporto.Id);
    }

    [Fact]
    public async Task ConguaglioAnnuale_AggregaICedoliniArchiviati()
    {
        var (tenant, rapportoId) = await CreaRapportoAsync();

        // Archivia tre mesi.
        await using (var db = NuovoContesto())
        {
            var svc = new CedoliniService(db, new ParametriNormativiService(db));
            foreach (var mese in new[] { 1, 2, 3 })
                await svc.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino { Anno = 2026, Mese = mese });
        }

        await using (var db = NuovoContesto())
        {
            var svc = new CedoliniService(db, new ParametriNormativiService(db));
            var r = await svc.CalcolaConguaglioAnnualeAsync(tenant, rapportoId, 2026);

            // Ricostruisce l'atteso dai cedolini archiviati con lo stesso motore pubblico.
            var cedolini = await db.Cedolini
                .Where(c => c.TenantId == tenant && c.RapportoLavoroId == rapportoId && c.Anno == 2026)
                .ToListAsync();
            Assert.Equal(3, cedolini.Count);

            decimal imponibile = cedolini.Sum(c => c.ImponibileIrpef);
            decimal trattenuta = cedolini.Sum(c => c.IrpefNetta);
            var parametri = await new ParametriNormativiService(db).CaricaParametriAsync(2026);
            decimal detrazioni = CalcoloIrpef.DetrazioneLavoroDipendenteAnnua(imponibile, 365);
            var atteso = ConguaglioFiscale.Calcola(imponibile, trattenuta, detrazioni, parametri.ScaglioniIrpef);

            Assert.Equal(atteso.IrpefLordaAnnua, r.IrpefLordaAnnua);
            Assert.Equal(atteso.IrpefNettaAnnua, r.IrpefNettaAnnua);
            Assert.Equal(atteso.Conguaglio, r.Conguaglio);
            Assert.Equal(atteso.ADebito, r.ADebito);
        }
    }

    [Fact]
    public async Task ArchiviaDicembre_ApplicaIlConguaglioInBusta()
    {
        var (tenant, rapportoId) = await CreaRapportoAsync();

        await using (var db = NuovoContesto())
        {
            var svc = new CedoliniService(db, new ParametriNormativiService(db));
            for (int mese = 1; mese <= 12; mese++)
                await svc.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino { Anno = 2026, Mese = mese });
        }

        await using (var db = NuovoContesto())
        {
            var cedolini = await db.Cedolini.Include(c => c.Voci)
                .Where(c => c.TenantId == tenant && c.RapportoLavoroId == rapportoId && c.Anno == 2026)
                .ToListAsync();
            var gennaio = cedolini.Single(c => c.Mese == 1);
            var dicembre = cedolini.Single(c => c.Mese == 12);

            // Atteso: mesi tutti uguali (nessuna variabile) → presuntiva dicembre = presuntiva gennaio.
            decimal imponibileAnnuo = cedolini.Sum(c => c.ImponibileIrpef);
            decimal presuntivaAnnua = gennaio.IrpefNetta * 12m;
            var parametri = await new ParametriNormativiService(db).CaricaParametriAsync(2026);
            decimal detrazioni = CalcoloIrpef.DetrazioneLavoroDipendenteAnnua(imponibileAnnuo, 365);
            var atteso = ConguaglioFiscale.Calcola(
                imponibileAnnuo, presuntivaAnnua, detrazioni, parametri.ScaglioniIrpef);

            Assert.Equal(atteso.Conguaglio, dicembre.ConguaglioIrpef);
            Assert.NotEqual(0m, dicembre.ConguaglioIrpef); // gli arrotondamenti mensili lasciano un residuo
            Assert.Contains(dicembre.Voci, v => v.Codice == "CONGUAGLIO_IRPEF");
            Assert.Equal(gennaio.IrpefNetta + atteso.Conguaglio, dicembre.IrpefNetta);
            Assert.Equal(gennaio.NettoInBusta - atteso.Conguaglio, dicembre.NettoInBusta);

            // Mese non-dicembre: nessun conguaglio.
            Assert.Equal(0m, gennaio.ConguaglioIrpef);
            Assert.DoesNotContain(gennaio.Voci, v => v.Codice == "CONGUAGLIO_IRPEF");

            // Il PDF (ricalcolo dal motore) riapplica il conguaglio archiviato: stesso netto.
            var svc = new CedoliniService(db, new ParametriNormativiService(db));
            var perPdf = await svc.RicalcolaPerPdfAsync(tenant, dicembre.Id);
            Assert.NotNull(perPdf);
            Assert.Contains(perPdf!.Cedolino.Righe, r => r.Codice == "CONGUAGLIO_IRPEF");
            Assert.Equal(dicembre.NettoInBusta, perPdf.Cedolino.NettoInBusta);

            // Dopo dicembre il conguaglio annuale residuo è zero: anno già sistemato.
            var residuo = await svc.CalcolaConguaglioAnnualeAsync(tenant, rapportoId, 2026);
            Assert.Equal(0m, residuo.Conguaglio);
        }
    }

    [Fact]
    public async Task ConguaglioAnnuale_SenzaCedolini_Lancia()
    {
        var (tenant, rapportoId) = await CreaRapportoAsync();
        await using var db = NuovoContesto();
        var svc = new CedoliniService(db, new ParametriNormativiService(db));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CalcolaConguaglioAnnualeAsync(tenant, rapportoId, 2026));
    }

    public void Dispose() => _connection.Dispose();
}
