using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Netto concordato con conguaglio (docs/NETTO-CONCORDATO-CONGUAGLIO.md): dato il netto
/// pattuito, il superminimo del mese viene ricalcolato dal calcolo inverso — mai sotto il
/// minimo CCNL, mai retroattivo, con la voce di trasparenza in busta.
/// </summary>
public sealed class NettoConcordatoTests : IDisposable
{
    private static readonly TimeProvider Oggi15Luglio2026 =
        new OrologioFisso(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero));

    private readonly SqliteConnection _connection;

    public NettoConcordatoTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
        new ParametriNormativiService(db).ImportaSeedSeVuotoAsync().GetAwaiter().GetResult();
    }

    public void Dispose() => _connection.Dispose();

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    private async Task<Guid> SeedRapportoAsync(Guid tenant)
    {
        await using var db = NuovoContesto();
        var azienda = await new AziendeService(db).CreaAsync(tenant, new AziendaEntity
        {
            RagioneSociale = "Prova Netto Concordato Srl", PartitaIva = "01234561007",
            Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
        });
        var rapporto = await new AnagraficheLavoroService(db).CreaDipendenteERapportoAsync(
            tenant, azienda.Id,
            new DipendenteEntity { CodiceFiscale = "RSSMRA80A01F205X", Nome = "Mario", Cognome = "Rossi" },
            new RapportoLavoroEntity
            {
                CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = "Impiegato",
                Regione = "Lombardia", CodiceCatastaleComune = "F205",
                DataAssunzione = new DateOnly(2024, 1, 1),
            });
        return rapporto.Id;
    }

    private async Task ImpostaNettoConcordatoAsync(
        Guid tenant, Guid rapportoId, decimal netto, DateOnly? decorrenza = null)
    {
        await using var db = NuovoContesto();
        var esito = await new AnagraficheLavoroService(db, Oggi15Luglio2026).CorreggiRapportoAsync(
            tenant, rapportoId, new AnagraficheLavoroService.CorrezioneRapporto(
                NettoConcordatoMensile: netto, NettoConcordatoDecorrenza: decorrenza));
        Assert.NotNull(esito);
    }

    [Fact]
    public async Task NettoConcordato_RicalcolaIlSuperminimo_EIlNettoDelMeseEIlPattuito()
    {
        var tenant = Guid.NewGuid();
        var rapportoId = await SeedRapportoAsync(tenant);
        await ImpostaNettoConcordatoAsync(tenant, rapportoId, 1800m);

        await using var db = NuovoContesto();
        var svc = new CedoliniService(db, new ParametriNormativiService(db), Oggi15Luglio2026);
        var cedolino = await svc.CalcolaEArchiviaAsync(
            tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 7 });

        Assert.InRange(cedolino.NettoInBusta, 1799.98m, 1800.02m);
    }

    [Fact]
    public async Task NettoConcordato_SottoIlMinimoContrattuale_RifiutaLElaborazione()
    {
        var tenant = Guid.NewGuid();
        var rapportoId = await SeedRapportoAsync(tenant);
        // 500 € netti per un livello 3 full-time: molto sotto il netto del solo minimo CCNL.
        await ImpostaNettoConcordatoAsync(tenant, rapportoId, 500m);

        await using var db = NuovoContesto();
        var svc = new CedoliniService(db, new ParametriNormativiService(db), Oggi15Luglio2026);
        var errore = await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CalcolaEArchiviaAsync(tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 7 }));

        Assert.Contains("non raggiungibile", errore.Message);
        Assert.Contains("minimo", errore.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NettoConcordato_NonSiApplicaAiMesiPrimaDellaDecorrenza()
    {
        var tenant = Guid.NewGuid();
        var rapportoId = await SeedRapportoAsync(tenant);
        await ImpostaNettoConcordatoAsync(tenant, rapportoId, 1800m); // decorrenza: luglio 2026

        await using var db = NuovoContesto();
        var svc = new CedoliniService(db, new ParametriNormativiService(db), Oggi15Luglio2026);
        var giugno = await svc.CalcolaEArchiviaAsync(
            tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 6 });
        var luglio = await svc.CalcolaEArchiviaAsync(
            tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 7 });

        // Giugno precede la decorrenza: busta normale dal lordo, nessun netto pattuito.
        Assert.NotInRange(giugno.NettoInBusta, 1799.98m, 1800.02m);
        Assert.InRange(luglio.NettoInBusta, 1799.98m, 1800.02m);
    }

    [Fact]
    public async Task NettoConcordato_ConDecorrenzaRetroattiva_ERifiutato()
    {
        var tenant = Guid.NewGuid();
        var rapportoId = await SeedRapportoAsync(tenant);

        await using var db = NuovoContesto();
        var errore = await Assert.ThrowsAsync<ArgumentException>(() =>
            new AnagraficheLavoroService(db, Oggi15Luglio2026).CorreggiRapportoAsync(
                tenant, rapportoId, new AnagraficheLavoroService.CorrezioneRapporto(
                    NettoConcordatoMensile: 1800m,
                    NettoConcordatoDecorrenza: new DateOnly(2026, 6, 1))));

        Assert.Contains("retroattivo", errore.Message);
    }

    [Fact]
    public async Task NettoConcordato_LaVoceDiTrasparenzaCompareInBusta()
    {
        var tenant = Guid.NewGuid();
        var rapportoId = await SeedRapportoAsync(tenant);
        await ImpostaNettoConcordatoAsync(tenant, rapportoId, 1800m);

        await using var db = NuovoContesto();
        var svc = new CedoliniService(db, new ParametriNormativiService(db), Oggi15Luglio2026);
        var cedolino = await svc.CalcolaEArchiviaAsync(
            tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 7 });

        Assert.Contains(cedolino.Voci, v => v.Codice == "NETTO_CONC");
        Assert.Contains(cedolino.Voci, v => v.Codice == "SUPERMIN"); // superminimo esposto come voce
    }

    [Fact]
    public async Task NettoConcordato_IlPdfDellaBusta_EsponeVoceENettoPattuito()
    {
        var tenant = Guid.NewGuid();
        var rapportoId = await SeedRapportoAsync(tenant);
        await ImpostaNettoConcordatoAsync(tenant, rapportoId, 1800m);

        await using var db = NuovoContesto();
        var svc = new CedoliniService(db, new ParametriNormativiService(db), Oggi15Luglio2026);
        var cedolino = await svc.CalcolaEArchiviaAsync(
            tenant, rapportoId, new Cartellino { Anno = 2026, Mese = 7 });
        var dati = await svc.LeggiPerPdfAsync(tenant, cedolino.Id);
        Assert.NotNull(dati);

        byte[] pdf = Paghe.Documenti.CedolinoPdf.Genera(
            dati!.Cedolino, dati.Intestazione, dati.Cartellino,
            Paghe.Documenti.ContestoEmissioneCedolino.Bozza, progressivi: dati.Progressivi);

        using var documento = UglyToad.PdfPig.PdfDocument.Open(pdf);
        string testo = string.Join("\n", documento.GetPages().Select(p => p.Text));
        Assert.Contains("Netto concordato", testo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1.800,00", testo);

        string? destinazione = Environment.GetEnvironmentVariable("PAGHE_PDF_NETTO_CONCORDATO");
        if (!string.IsNullOrWhiteSpace(destinazione))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinazione)!);
            File.WriteAllBytes(destinazione, pdf);
        }
    }
}
