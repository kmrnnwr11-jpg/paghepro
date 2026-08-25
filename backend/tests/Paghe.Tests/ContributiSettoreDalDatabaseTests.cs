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
            RagioneSociale = "Pulizie Beta Srl", PartitaIva = "01234561007",
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
    public async Task CalendarioGiornaliero_IlServerRicalcolaITotaliENonSiFidaDelClient()
    {
        var (tenant, rapportoId) = await PreparaAsync(oltreQuindici: false, fondiSettore: 0m);

        // Il client manda un calendario con 3 giorni di ferie e, per sbaglio o per malizia,
        // dichiara zero ferie nei totali: deve vincere il calendario.
        var giornate = new List<GiornataCartellino>();
        decimal progressivo = 0m;
        for (int g = 1; g <= 31; g++)
        {
            var data = new DateOnly(2026, 5, g);
            if (data.DayOfWeek == DayOfWeek.Monday)
                progressivo = 0m;
            decimal ore = data.DayOfWeek == DayOfWeek.Sunday || g is 4 or 5 or 6 ? 0m : 8m;
            progressivo += ore;
            giornate.Add(new GiornataCartellino
            {
                Data = data,
                Causale = data.DayOfWeek == DayOfWeek.Sunday ? CausaleGiornata.RiposoSettimanale
                    : g is 4 or 5 or 6 ? CausaleGiornata.Ferie
                    : CausaleGiornata.Lavorata,
                OreOrdinarie = ore,
                ProgressivoOreSettimanali = progressivo,
            });
        }

        await using var db = NuovoContesto();
        // Maggio 2026 comincia di venerdì: il riporto della prima settimana arriva dal
        // cartellino di aprile (27-30/04 in ferie: zero ore lavorate pregresse).
        db.Cartellini.Add(new CartellinoEntity
        {
            Id = Guid.NewGuid(), TenantId = tenant, RapportoLavoroId = rapportoId,
            Anno = 2026, Mese = 4,
            Giornate = Enumerable.Range(27, 4).Select(g => new GiornataCartellino
            {
                Data = new DateOnly(2026, 4, g), Causale = CausaleGiornata.Ferie,
                ProgressivoOreSettimanali = 0m,
            }).ToList(),
        });
        await db.SaveChangesAsync();
        var svc = new CedoliniService(db, new ParametriNormativiService(db),
            new OrologioFisso(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero)));
        var cedolino = await svc.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino
        {
            Anno = 2026, Mese = 5, GiorniFerie = 0m, Giornate = giornate,
        });

        var cartellino = await db.Cartellini.SingleAsync(c => c.Mese == 5);
        Assert.Equal(3m, cartellino.GiorniFerie);       // derivate dal calendario, non lo zero inviato
        Assert.Equal(31, cartellino.Giornate.Count);    // e il calendario resta archiviato
        Assert.True(cedolino.NettoInBusta > 0m);
    }

    [Fact]
    public async Task CalendarioIncompleto_LElaborazioneSiFerma()
    {
        var (tenant, rapportoId) = await PreparaAsync(oltreQuindici: false, fondiSettore: 0m);

        await using var db = NuovoContesto();
        var svc = new CedoliniService(db, new ParametriNormativiService(db),
            new OrologioFisso(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero)));

        var errore = await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino
            {
                Anno = 2026, Mese = 5,
                Giornate =
                [
                    new GiornataCartellino
                    {
                        Data = new DateOnly(2026, 5, 1),
                        Causale = CausaleGiornata.Lavorata, OreOrdinarie = 8m,
                    },
                ],
            }));

        Assert.Contains("non è elaborabile", errore.Message);
        Assert.Empty(await db.Cedolini.ToListAsync());
    }

    [Fact]
    public async Task IlPdfRicalcolatoUsaGliStessiContributiDellArchivio()
    {
        // Il PDF passa da LeggiPerPdfAsync: se lì l'azienda non arrivasse al motore, il
        // documento consegnato al dipendente mostrerebbe numeri diversi da quelli archiviati.
        var (tenant, rapportoId) = await PreparaAsync(oltreQuindici: true, fondiSettore: 0.002667m);
        var archiviato = await ArchiviaAsync(tenant, rapportoId);

        await using var db = NuovoContesto();
        var perPdf = await new CedoliniService(db, new ParametriNormativiService(db))
            .LeggiPerPdfAsync(tenant, archiviato.Id);

        Assert.NotNull(perPdf);
        Assert.Equal(archiviato.ContributiDipendente, perPdf.Cedolino.ContributiDipendente);
    }
}
