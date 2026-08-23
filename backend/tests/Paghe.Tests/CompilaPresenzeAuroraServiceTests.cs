using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Ai;
using Paghe.Persistence.Entities;

namespace Paghe.Tests;

/// <summary>
/// Patcher A3 <c>compila_presenze</c>: il modello sceglie soltanto rapporto e date, il server
/// rilegge lo stato autorevole e trasforma esclusivamente giornate lavorate pulite. Qui si
/// provano intento e diff, applicazione con pre/postcondizioni, undo esatto e tutte le guardie
/// (dati salute indistinguibili, cedolino, F24 finale, date future, mese parziale, schema).
/// </summary>
public sealed class CompilaPresenzeAuroraServiceTests : IDisposable
{
    private static readonly TimeProvider Oggi15Agosto2026 =
        new OrologioFisso(new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero));

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public CompilaPresenzeAuroraServiceTests()
    {
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private PagheDbContext NuovoContesto() => new(
        new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    private static CompilaPresenzeAuroraService Servizio(
        PagheDbContext db, TimeProvider? orologio = null) =>
        new(db, new CartellinoBozzaService(db, orologio ?? Oggi15Agosto2026),
            orologio ?? Oggi15Agosto2026);

    private static string Argomenti(Guid rapportoId, params string[] date) =>
        JsonSerializer.Serialize(new
        {
            rapportoId,
            date,
            causale = "AssenzaNonRetribuita",
        });

    // ---------- Scenario: rapporto pieno e cartellino completo di luglio 2026 ----------

    private sealed record Scenario(Guid TenantId, Guid AziendaId, Guid RapportoId);

    private async Task<Scenario> SeedScenarioConCartellinoAsync(
        IReadOnlyCollection<int>? giorniMalattia = null)
    {
        var scenario = new Scenario(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await using var db = NuovoContesto();
        db.Aziende.Add(new AziendaEntity
        {
            Id = scenario.AziendaId, TenantId = scenario.TenantId,
            RagioneSociale = "Aurora Collaudo Srl",
        });
        var dipendenteId = Guid.NewGuid();
        db.Dipendenti.Add(new DipendenteEntity
        {
            Id = dipendenteId, TenantId = scenario.TenantId,
            CodiceFiscale = "RSSMRA80A01F205X", Nome = "Mario", Cognome = "Rossi",
        });
        db.Rapporti.Add(new RapportoLavoroEntity
        {
            Id = scenario.RapportoId, TenantId = scenario.TenantId,
            AziendaId = scenario.AziendaId, DipendenteId = dipendenteId,
            CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "3", Qualifica = "Impiegato",
            DataAssunzione = new DateOnly(2024, 1, 1), OreSettimanali = 40m,
        });
        await db.SaveChangesAsync();

        await new CartellinoBozzaService(db, Oggi15Agosto2026).SalvaAsync(
            scenario.TenantId, scenario.RapportoId,
            new SalvaCartellinoBozza(new Cartellino
            {
                Anno = 2026,
                Mese = 7,
                StatoAssegniFamiliari = StatoAssegniFamiliari.NonSpettanti,
                Giornate = Calendario(2026, 7, giorniMalattia ?? []),
            }, null, 0));
        return scenario;
    }

    private static IReadOnlyList<GiornataCartellino> Calendario(
        int anno, int mese, IReadOnlyCollection<int> giorniMalattia) =>
        [.. Enumerable.Range(1, DateTime.DaysInMonth(anno, mese)).Select(giorno =>
        {
            var data = new DateOnly(anno, mese, giorno);
            if (giorniMalattia.Contains(giorno))
                return new GiornataCartellino
                {
                    Data = data, Causale = CausaleGiornata.Malattia,
                };
            bool domenica = data.DayOfWeek == DayOfWeek.Sunday;
            return new GiornataCartellino
            {
                Data = data,
                Causale = domenica ? CausaleGiornata.RiposoSettimanale : CausaleGiornata.Lavorata,
                OreOrdinarie = domenica ? 0m : 8m,
            };
        })];

    // ---------- Intento e applicazione ----------

    [Fact]
    public async Task Prepara_ProduceTitoloDiffEHashesDelloStatoAutorevole()
    {
        var scenario = await SeedScenarioConCartellinoAsync();
        await using var db = NuovoContesto();

        var preparazione = await Servizio(db).PreparaAsync(
            scenario.TenantId, scenario.AziendaId, "2026-07",
            Argomenti(scenario.RapportoId, "2026-07-15", "2026-07-16"));

        Assert.Contains("2 assenze non retribuite", preparazione.Titolo);
        Assert.Contains("Rossi Mario", preparazione.Titolo);
        Assert.Equal(2, preparazione.Diff.Count);
        Assert.Equal("15/07/2026", preparazione.Diff[0].Etichetta);
        Assert.Equal("8 ore lavorate", preparazione.Diff[0].Da);
        Assert.Equal("8 ore di assenza non retribuita", preparazione.Diff[0].A);
        Assert.NotEqual(preparazione.PrecondizioneHash, preparazione.PostcondizioneHash);
        Assert.False(string.IsNullOrWhiteSpace(preparazione.ComandoCanonicoJson));
        Assert.False(string.IsNullOrWhiteSpace(preparazione.UndoJson));
    }

    [Fact]
    public async Task Applica_TrasformaSoloLeDateRichieste_EVersionaIlCartellino()
    {
        var scenario = await SeedScenarioConCartellinoAsync();
        await using var db = NuovoContesto();
        var servizio = Servizio(db);
        var preparazione = await servizio.PreparaAsync(
            scenario.TenantId, scenario.AziendaId, "2026-07",
            Argomenti(scenario.RapportoId, "2026-07-15", "2026-07-16"));

        var esito = await servizio.ApplicaAsync(
            scenario.TenantId, scenario.AziendaId, "2026-07", preparazione.ComandoCanonicoJson);

        Assert.Equal(2, esito.DateModificate);
        Assert.Equal(2, esito.Versione);
        var entita = await db.Cartellini.AsNoTracking().SingleAsync(c =>
            c.TenantId == scenario.TenantId && c.RapportoLavoroId == scenario.RapportoId);
        Assert.All(new[] { 15, 16 }, giorno =>
        {
            var g = entita.Giornate.Single(x => x.Data.Day == giorno);
            Assert.Equal(CausaleGiornata.AssenzaNonRetribuita, g.Causale);
            Assert.Equal(8m, g.OreAssenzaNonRetribuita);
            Assert.Equal(0m, g.OreOrdinarie);
        });
        Assert.Equal(CausaleGiornata.Lavorata,
            entita.Giornate.Single(x => x.Data.Day == 17).Causale);
        Assert.Equal(16m, entita.OreAssenzaNonRetribuita);
    }

    [Fact]
    public async Task Applica_DopoUnaModificaConcorrente_NonToccaNulla()
    {
        var scenario = await SeedScenarioConCartellinoAsync();
        await using var db = NuovoContesto();
        var servizio = Servizio(db);
        var preparazione = await servizio.PreparaAsync(
            scenario.TenantId, scenario.AziendaId, "2026-07",
            Argomenti(scenario.RapportoId, "2026-07-15"));

        // Tra anteprima e conferma qualcuno cambia il cartellino dalla pagina Presenze.
        var corrente = await db.Cartellini.AsNoTracking().SingleAsync(c =>
            c.TenantId == scenario.TenantId && c.RapportoLavoroId == scenario.RapportoId);
        var giornate = corrente.Giornate.Select(g => g.Data.Day == 20
            ? new GiornataCartellino
            {
                Data = g.Data, Causale = CausaleGiornata.AssenzaNonRetribuita,
                OreAssenzaNonRetribuita = 8m,
            }
            : g).ToArray();
        await new CartellinoBozzaService(db, Oggi15Agosto2026).SalvaAsync(
            scenario.TenantId, scenario.RapportoId,
            new SalvaCartellinoBozza(new Cartellino
            {
                Anno = 2026, Mese = 7,
                StatoAssegniFamiliari = StatoAssegniFamiliari.NonSpettanti,
                Giornate = giornate,
            }, corrente.Id, corrente.Versione));

        var ex = await Assert.ThrowsAsync<OperazioneAuroraNonConsentitaException>(() =>
            servizio.ApplicaAsync(
                scenario.TenantId, scenario.AziendaId, "2026-07",
                preparazione.ComandoCanonicoJson));

        Assert.Contains("cambiato dopo l'anteprima", ex.Message);
        var dopo = await db.Cartellini.AsNoTracking().SingleAsync(c =>
            c.TenantId == scenario.TenantId && c.RapportoLavoroId == scenario.RapportoId);
        Assert.Equal(CausaleGiornata.Lavorata,
            dopo.Giornate.Single(g => g.Data.Day == 15).Causale);
    }

    [Fact]
    public async Task Ripristina_RiportaEsattamenteLoStatoPrecedente_EUnaSolaVolta()
    {
        var scenario = await SeedScenarioConCartellinoAsync();
        await using var db = NuovoContesto();
        var servizio = Servizio(db);
        var preparazione = await servizio.PreparaAsync(
            scenario.TenantId, scenario.AziendaId, "2026-07",
            Argomenti(scenario.RapportoId, "2026-07-15", "2026-07-16"));
        await servizio.ApplicaAsync(
            scenario.TenantId, scenario.AziendaId, "2026-07", preparazione.ComandoCanonicoJson);

        var ripristino = await servizio.RipristinaAsync(
            scenario.TenantId, scenario.AziendaId, "2026-07", preparazione.UndoJson);

        Assert.Equal(2, ripristino.DateModificate);
        Assert.Equal(3, ripristino.Versione);
        var entita = await db.Cartellini.AsNoTracking().SingleAsync(c =>
            c.TenantId == scenario.TenantId && c.RapportoLavoroId == scenario.RapportoId);
        Assert.All(new[] { 15, 16 }, giorno =>
        {
            var g = entita.Giornate.Single(x => x.Data.Day == giorno);
            Assert.Equal(CausaleGiornata.Lavorata, g.Causale);
            Assert.Equal(8m, g.OreOrdinarie);
        });
        Assert.Equal(0m, entita.OreAssenzaNonRetribuita);

        // Il cartellino è andato oltre lo stato dell'applicazione: l'undo automatico si ferma.
        var ex = await Assert.ThrowsAsync<OperazioneAuroraNonConsentitaException>(() =>
            servizio.RipristinaAsync(
                scenario.TenantId, scenario.AziendaId, "2026-07", preparazione.UndoJson));
        Assert.Contains("ripristino automatico è bloccato", ex.Message);
    }

    // ---------- Guardie: dati salute, blocchi di dominio, date, schema ----------

    [Fact]
    public async Task MeseConMalattia_Fermato_SenzaRivelareLaCausaSanitaria()
    {
        var scenario = await SeedScenarioConCartellinoAsync(giorniMalattia: [10]);
        await using var db = NuovoContesto();

        // La richiesta tocca un giorno QUALSIASI: il blocco vale per l'intero mese.
        var ex = await Assert.ThrowsAsync<OperazioneAuroraNonConsentitaException>(() =>
            Servizio(db).PreparaAsync(
                scenario.TenantId, scenario.AziendaId, "2026-07",
                Argomenti(scenario.RapportoId, "2026-07-15")));

        Assert.DoesNotContain("malattia", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("salute", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("non è idoneo alla modifica automatica", ex.Message);
    }

    [Theory]
    [InlineData("cedolino")]
    [InlineData("f24")]
    public async Task CedolinoOF24Finale_BloccanoPreparaEApplica(string blocco)
    {
        var scenario = await SeedScenarioConCartellinoAsync();
        await using var db = NuovoContesto();
        var servizio = Servizio(db);
        // L'intento nasce prima del blocco: anche l'applicazione tardiva deve fermarsi.
        var preparazione = await servizio.PreparaAsync(
            scenario.TenantId, scenario.AziendaId, "2026-07",
            Argomenti(scenario.RapportoId, "2026-07-15"));

        if (blocco == "cedolino")
            db.Cedolini.Add(new CedolinoEntity
            {
                Id = Guid.NewGuid(), TenantId = scenario.TenantId,
                AziendaId = scenario.AziendaId, RapportoLavoroId = scenario.RapportoId,
                Anno = 2026, Mese = 7,
            });
        else
            db.PraticheF24.Add(new PraticaF24Entity
            {
                Id = Guid.NewGuid(), TenantId = scenario.TenantId,
                AziendaId = scenario.AziendaId, Anno = 2026, Mese = 7,
                Stato = "InviataManualmente",
            });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<OperazioneAuroraNonConsentitaException>(() =>
            servizio.PreparaAsync(
                scenario.TenantId, scenario.AziendaId, "2026-07",
                Argomenti(scenario.RapportoId, "2026-07-16")));
        await Assert.ThrowsAsync<OperazioneAuroraNonConsentitaException>(() =>
            servizio.ApplicaAsync(
                scenario.TenantId, scenario.AziendaId, "2026-07",
                preparazione.ComandoCanonicoJson));
    }

    [Fact]
    public async Task DateFutureFuoriPeriodoONonLavorate_Rifiutate()
    {
        var scenario = await SeedScenarioConCartellinoAsync();
        await using var db = NuovoContesto();
        // Orologio dentro il mese: il 20 luglio è ancora futuro.
        var servizio = Servizio(db, new OrologioFisso(
            new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero)));

        var futura = await Assert.ThrowsAsync<OperazioneAuroraNonConsentitaException>(() =>
            servizio.PreparaAsync(
                scenario.TenantId, scenario.AziendaId, "2026-07",
                Argomenti(scenario.RapportoId, "2026-07-20")));
        Assert.Contains("future", futura.Message);

        var fuoriPeriodo = await Assert.ThrowsAsync<OperazioneAuroraNonConsentitaException>(() =>
            servizio.PreparaAsync(
                scenario.TenantId, scenario.AziendaId, "2026-07",
                Argomenti(scenario.RapportoId, "2026-06-30")));
        Assert.Contains("periodo del mandato", fuoriPeriodo.Message);

        // Il 5 luglio 2026 è domenica: non è una giornata lavorata trasformabile.
        var domenica = await Assert.ThrowsAsync<OperazioneAuroraNonConsentitaException>(() =>
            servizio.PreparaAsync(
                scenario.TenantId, scenario.AziendaId, "2026-07",
                Argomenti(scenario.RapportoId, "2026-07-05")));
        Assert.Contains("solo giornate lavorate", domenica.Message);
    }

    [Fact]
    public async Task RapportoDiAltraAziendaOMeseParziale_Rifiutati()
    {
        var scenario = await SeedScenarioConCartellinoAsync();
        await using var db = NuovoContesto();
        var servizio = Servizio(db);

        var estraneo = await Assert.ThrowsAsync<OperazioneAuroraNonConsentitaException>(() =>
            servizio.PreparaAsync(
                scenario.TenantId, Guid.NewGuid(), "2026-07",
                Argomenti(scenario.RapportoId, "2026-07-15")));
        Assert.Contains("non trovato nell'azienda del mandato", estraneo.Message);

        var rapporto = await db.Rapporti.SingleAsync(r => r.Id == scenario.RapportoId);
        rapporto.DataAssunzione = new DateOnly(2026, 7, 15);
        await db.SaveChangesAsync();
        var parziale = await Assert.ThrowsAsync<OperazioneAuroraNonConsentitaException>(() =>
            servizio.PreparaAsync(
                scenario.TenantId, scenario.AziendaId, "2026-07",
                Argomenti(scenario.RapportoId, "2026-07-20")));
        Assert.Contains("solo una parte del mese", parziale.Message);
    }

    [Theory]
    [InlineData("""{"rapportoId":"non-un-guid","date":["2026-07-15"],"causale":"AssenzaNonRetribuita"}""")]
    [InlineData("""{"rapportoId":"3f2504e0-4f89-11d3-9a0c-0305e82c3301","date":["2026-07-15"],"causale":"Ferie"}""")]
    [InlineData("""{"rapportoId":"3f2504e0-4f89-11d3-9a0c-0305e82c3301","date":["2026-07-15","2026-07-15"],"causale":"AssenzaNonRetribuita"}""")]
    [InlineData("""{"rapportoId":"3f2504e0-4f89-11d3-9a0c-0305e82c3301","date":["2026-07-15"],"causale":"AssenzaNonRetribuita","extra":true}""")]
    [InlineData("""{"rapportoId":"3f2504e0-4f89-11d3-9a0c-0305e82c3301","date":[],"causale":"AssenzaNonRetribuita"}""")]
    [InlineData("non-json")]
    public void SchemaOstileDelModello_RespintoPrimaDiToccareIlDominio(string argomenti)
    {
        Assert.False(CompilaPresenzeAuroraService.ProvaNormalizzaArgomentiModello(
            argomenti, out _, out var motivo));
        Assert.False(string.IsNullOrWhiteSpace(motivo));
    }
}
