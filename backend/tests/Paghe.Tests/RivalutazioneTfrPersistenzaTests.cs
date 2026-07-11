using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test della rivalutazione annua del fondo TFR sui cedolini archiviati: il fondo al 31/12
/// precedente è il saldo iniziale del rapporto + le quote degli anni prima; la variazione
/// ISTAT arriva dalla richiesta o dai parametri dell'anno, mai cablata.
/// </summary>
public sealed class RivalutazioneTfrPersistenzaTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public RivalutazioneTfrPersistenzaTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
        new ParametriNormativiService(db).ImportaSeedSeVuotoAsync().GetAwaiter().GetResult();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    private async Task<(Guid tenant, Guid aziendaId, Guid rapportoId)> CreaRapportoAsync()
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
        return (tenant, az.Id, rapporto.Id);
    }

    /// <summary>Inserisce direttamente un cedolino archiviato con la sola QuotaTfr rilevante.</summary>
    private async Task InserisciCedolinoAsync(
        Guid tenant, Guid aziendaId, Guid rapportoId, int anno, int mese, decimal quotaTfr)
    {
        await using var db = NuovoContesto();
        db.Cedolini.Add(new CedolinoEntity
        {
            Id = Guid.NewGuid(), TenantId = tenant, AziendaId = aziendaId,
            RapportoLavoroId = rapportoId, Anno = anno, Mese = mese,
            QuotaTfr = quotaTfr, CalcolatoIl = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Rivalutazione_FondoDaSaldoInizialeEQuoteAnniPrecedenti()
    {
        var (tenant, aziendaId, rapportoId) = await CreaRapportoAsync();
        await InserisciCedolinoAsync(tenant, aziendaId, rapportoId, 2025, 11, 100m);
        await InserisciCedolinoAsync(tenant, aziendaId, rapportoId, 2025, 12, 100m);
        await InserisciCedolinoAsync(tenant, aziendaId, rapportoId, 2026, 1, 120m); // anno corrente: escluso dal fondo

        await using (var db = NuovoContesto())
            await new AnagraficheLavoroService(db).CorreggiRapportoAsync(tenant, rapportoId,
                new AnagraficheLavoroService.CorrezioneRapporto(FondoTfrIniziale: 9800m));

        await using var db2 = NuovoContesto();
        var svc = new CedoliniService(db2, new ParametriNormativiService(db2));
        var r = await svc.CalcolaRivalutazioneTfrAsync(tenant, rapportoId, 2026, variazioneIstat: 0.02m);

        // Fondo = 9800 + 200 (quote 2025); coeff = 1,5% + 75%×2% = 3% → lorda 300, imposta 51, netta 249.
        Assert.Equal(10_000m, r.FondoAl31DicembrePrecedente);
        Assert.Equal(0.03m, r.Coefficiente);
        Assert.Equal(300m, r.RivalutazioneLorda);
        Assert.Equal(51m, r.ImpostaSostitutiva);
        Assert.Equal(10_249m, r.FondoRivalutato);
        Assert.Equal(120m, r.QuoteMaturateAnno);
        Assert.Equal(10_369m, r.FondoFinale);
    }

    [Fact]
    public async Task Rivalutazione_SenzaOverride_UsaIlParametroDellAnno()
    {
        var (tenant, aziendaId, rapportoId) = await CreaRapportoAsync();
        await InserisciCedolinoAsync(tenant, aziendaId, rapportoId, 2025, 12, 1000m);

        await using (var db = NuovoContesto())
        {
            var pa = await db.ParametriAnno.SingleAsync(p => p.Anno == 2026);
            pa.VariazioneIstatTfr = 0.01m;
            await db.SaveChangesAsync();
        }

        await using var db2 = NuovoContesto();
        var svc = new CedoliniService(db2, new ParametriNormativiService(db2));
        var r = await svc.CalcolaRivalutazioneTfrAsync(tenant, rapportoId, 2026);

        Assert.Equal(0.01m, r.VariazioneIstat);
        Assert.Equal(0.0225m, r.Coefficiente); // 1,5% + 75%×1%

        // L'override esplicito prevale sul parametro dell'anno.
        var conOverride = await svc.CalcolaRivalutazioneTfrAsync(tenant, rapportoId, 2026, 0.02m);
        Assert.Equal(0.03m, conOverride.Coefficiente);
    }

    [Fact]
    public async Task Rivalutazione_SenzaVariazioneIstat_Lancia()
    {
        var (tenant, aziendaId, rapportoId) = await CreaRapportoAsync();
        await InserisciCedolinoAsync(tenant, aziendaId, rapportoId, 2025, 12, 1000m);

        await using var db = NuovoContesto();
        var svc = new CedoliniService(db, new ParametriNormativiService(db));
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CalcolaRivalutazioneTfrAsync(tenant, rapportoId, 2026)); // parametro 2026 = null nel seed
        Assert.Contains("ISTAT", ex.Message);
    }

    [Fact]
    public async Task Rivalutazione_TenantSbagliato_Lancia()
    {
        var (_, _, rapportoId) = await CreaRapportoAsync();

        await using var db = NuovoContesto();
        var svc = new CedoliniService(db, new ParametriNormativiService(db));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CalcolaRivalutazioneTfrAsync(Guid.NewGuid(), rapportoId, 2026, 0.01m));
    }

    public void Dispose() => _connection.Dispose();
}
