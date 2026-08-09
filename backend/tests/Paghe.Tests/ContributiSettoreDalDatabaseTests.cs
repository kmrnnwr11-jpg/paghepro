using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Calcolo;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// I due dati contributivi che stanno sull'azienda — soglia dei 15 dipendenti (0,30% CIG) e fondi
/// di settore (FIS, FSBS) — devono arrivare dal database fino al motore. Prima esistevano solo nel
/// dominio: i golden test su buste reali passavano, ma in produzione ogni cedolino usciva con la
/// quota INPS a carico del dipendente sottostimata, e quindi un netto più alto del dovuto.
/// </summary>
public sealed class ContributiSettoreDalDatabaseTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ContributiSettoreDalDatabaseTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
        new ParametriNormativiService(db).ImportaSeedSeVuotoAsync().GetAwaiter().GetResult();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();

    /// <summary>Azienda + dipendente pronti al calcolo; restituisce tenant e rapporto.</summary>
    private async Task<(Guid Tenant, Guid RapportoId)> PreparaAsync(
        bool oltreQuindici, decimal fondiSettore)
    {
        var tenant = Guid.NewGuid();
        await using var db = NuovoContesto();
        var azienda = await new AziendeService(db).CreaAsync(tenant, new AziendaEntity
        {
            RagioneSociale = "Pulizie Beta Srl", PartitaIva = "01234567890",
            Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
            OltreQuindiciDipendenti = oltreQuindici,
            ContributiFondiSettore = fondiSettore,
        });
        var rapporto = await new AnagraficheLavoroService(db).CreaDipendenteERapportoAsync(
            tenant, azienda.Id,
            new DipendenteEntity { CodiceFiscale = "RSSMRA80A01F205X", Nome = "Mario", Cognome = "Rossi" },
            new RapportoLavoroEntity
            {
                CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = "Impiegato",
                NumeroMensilita = 14, Regione = "Lombardia", CodiceCatastaleComune = "F205",
                DataAssunzione = new DateOnly(2020, 1, 1), TipoContratto = "Tempo indeterminato",
            });
        return (tenant, rapporto.Id);
    }

    private async Task<CedolinoEntity> ArchiviaAsync(Guid tenant, Guid rapportoId)
    {
        await using var db = NuovoContesto();
        var svc = new CedoliniService(db, new ParametriNormativiService(db),
            new OrologioFisso(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero)));
        return await svc.CalcolaEArchiviaAsync(
            tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 5 });
    }

    [Fact]
    public async Task AziendaOltreQuindiciDipendenti_IlCedolinoArchiviatoApplicaLo030DiCig()
    {
        var (tenant, rapportoId) = await PreparaAsync(oltreQuindici: true, fondiSettore: 0m);

        var cedolino = await ArchiviaAsync(tenant, rapportoId);

        // 9,19% base + 0,30% CIG = 9,49% dell'imponibile.
        var atteso = Arrotondamento.Euro(cedolino.ImponibileInps * 0.0949m);
        Assert.Equal(atteso, cedolino.ContributiDipendente);
    }

    [Fact]
    public async Task FondoDiSettore_IlCedolinoArchiviatoLoSomma()
    {
        // Multiservizi: 9,19% + 0,30% CIG + FIS 0,2667% = 9,7567% (verificato su buste Dussmann).
        var (tenant, rapportoId) = await PreparaAsync(oltreQuindici: true, fondiSettore: 0.002667m);

        var cedolino = await ArchiviaAsync(tenant, rapportoId);

        var atteso = Arrotondamento.Euro(cedolino.ImponibileInps * 0.097567m);
        Assert.Equal(atteso, cedolino.ContributiDipendente);
    }

    [Fact]
    public async Task AziendaSottoQuindiciDipendenti_RestaAllaQuotaBase()
    {
        var (tenant, rapportoId) = await PreparaAsync(oltreQuindici: false, fondiSettore: 0m);

        var cedolino = await ArchiviaAsync(tenant, rapportoId);

        var atteso = Arrotondamento.Euro(cedolino.ImponibileInps * 0.0919m);
        Assert.Equal(atteso, cedolino.ContributiDipendente);
    }

    [Fact]
    public async Task IlPdfRicalcolatoUsaGliStessiContributiDellArchivio()
    {
        // Il PDF passa da RicalcolaPerPdfAsync: se lì l'azienda non arrivasse al motore, il
        // documento consegnato al dipendente mostrerebbe numeri diversi da quelli archiviati.
        var (tenant, rapportoId) = await PreparaAsync(oltreQuindici: true, fondiSettore: 0.002667m);
        var archiviato = await ArchiviaAsync(tenant, rapportoId);

        await using var db = NuovoContesto();
        var perPdf = await new CedoliniService(db, new ParametriNormativiService(db))
            .RicalcolaPerPdfAsync(tenant, archiviato.Id);

        Assert.NotNull(perPdf);
        Assert.Equal(archiviato.ContributiDipendente, perPdf.Cedolino.ContributiDipendente);
    }
}
