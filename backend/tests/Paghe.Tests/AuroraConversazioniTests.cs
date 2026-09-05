using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Paghe.Persistence.Ai;
using Paghe.Persistence.Gdpr;

namespace Paghe.Tests;

public sealed class AuroraConversazioniTests : IDisposable
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid UtenteA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UtenteB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Ora = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connessione = new("Data Source=:memory:");
    private readonly OrologioRegolabile _orologio = new(Ora);
    private readonly ProtettoreDati _protettore = new(
        "chiave-test-aurora-conversazioni-abbastanza-lunga", produzione: false);

    public AuroraConversazioniTests()
    {
        _connessione.Open();
        using var db = NuovoDb();
        db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Cronologia_DisattivataPerDefault_EOptInEsplicito()
    {
        await using var db = NuovoDb();
        var servizio = NuovoServizio(db);

        Assert.False((await servizio.ImpostazioneAsync(TenantA, UtenteA)).Attiva);
        await Assert.ThrowsAsync<CronologiaAuroraDisattivataException>(() =>
            servizio.CreaAsync(TenantA, UtenteA, new()));

        var attiva = await servizio.ImpostaAsync(TenantA, UtenteA, true);
        var conversazione = await servizio.CreaAsync(
            TenantA, UtenteA, new("Pratica Rossi"));

        Assert.True(attiva.Attiva);
        Assert.Equal("Pratica Rossi", conversazione.Titolo);
        Assert.Equal(Ora.UtcDateTime.AddHours(24), conversazione.ScadeUtc);
    }

    [Fact]
    public async Task Turno_CompletoConservaUserEAssistant_EBloccaReplay()
    {
        await using var db = NuovoDb();
        var servizio = NuovoServizio(db);
        await servizio.ImpostaAsync(TenantA, UtenteA, true);
        var conversazione = await servizio.CreaAsync(TenantA, UtenteA, new("Cedolino aprile"));
        var clientTurnId = Guid.NewGuid();

        var avviato = await servizio.AvviaTurnoAsync(
            TenantA, UtenteA, conversazione.Id, clientTurnId,
            "Qual è il netto?", conversazione.Versione);
        var versione = await servizio.CompletaTurnoAsync(
            TenantA, UtenteA, conversazione.Id, avviato.TurnoId,
            "Il netto risulta dalla bozza validata.");
        var dettaglio = await servizio.ContestoChatAsync(TenantA, UtenteA, conversazione.Id);

        Assert.Equal(3, versione?.VersioneConversazione);
        Assert.False(versione?.RispostaTroncata);
        Assert.NotNull(dettaglio);
        Assert.Collection(dettaglio!.Messaggi,
            user =>
            {
                Assert.Equal("user", user.Ruolo);
                Assert.Equal("Qual è il netto?", user.Contenuto);
            },
            assistant =>
            {
                Assert.Equal("assistant", assistant.Ruolo);
                Assert.Equal("Il netto risulta dalla bozza validata.", assistant.Contenuto);
            });
        await Assert.ThrowsAsync<TurnoAuroraDuplicatoException>(() =>
            servizio.AvviaTurnoAsync(
                TenantA, UtenteA, conversazione.Id, clientTurnId,
                "Qual è il netto?", versione?.VersioneConversazione));
    }

    [Fact]
    public async Task Query_FiltranoInsiemeTenantEUtente()
    {
        await using var db = NuovoDb();
        var servizio = NuovoServizio(db);
        await servizio.ImpostaAsync(TenantA, UtenteA, true);
        await servizio.ImpostaAsync(TenantA, UtenteB, true);
        var conversazione = await servizio.CreaAsync(TenantA, UtenteA, new("Solo utente A"));

        Assert.Empty(await servizio.ElencoAsync(TenantA, UtenteB));
        Assert.Null(await servizio.DettaglioAsync(TenantA, UtenteB, conversazione.Id));
        Assert.False(await servizio.EliminaAsync(TenantA, UtenteB, conversazione.Id));
        Assert.NotNull(await servizio.DettaglioAsync(TenantA, UtenteA, conversazione.Id));

        var altroTenant = Guid.NewGuid();
        await servizio.ImpostaAsync(altroTenant, UtenteA, true);
        Assert.Empty(await servizio.ElencoAsync(altroTenant, UtenteA));
        Assert.Null(await servizio.DettaglioAsync(altroTenant, UtenteA, conversazione.Id));
    }

    [Fact]
    public async Task Database_NonContieneTitoloOMessaggiInChiaro()
    {
        const string titolo = "SENTINEL_TITOLO_AURORA";
        const string domanda = "SENTINEL_DOMANDA_AURORA";
        const string risposta = "SENTINEL_RISPOSTA_AURORA";
        await using var db = NuovoDb();
        var servizio = NuovoServizio(db);
        await servizio.ImpostaAsync(TenantA, UtenteA, true);
        var conversazione = await servizio.CreaAsync(TenantA, UtenteA, new(titolo));
        var turno = await servizio.AvviaTurnoAsync(
            TenantA, UtenteA, conversazione.Id, Guid.NewGuid(), domanda,
            conversazione.Versione);
        await servizio.CompletaTurnoAsync(
            TenantA, UtenteA, conversazione.Id, turno.TurnoId, risposta);

        await using var comando = _connessione.CreateCommand();
        comando.CommandText = """
            SELECT group_concat(Valore, '|') FROM (
                SELECT TitoloProtetto AS Valore FROM AuroraConversazioni
                UNION ALL
                SELECT ContenutoProtetto AS Valore FROM AuroraMessaggi
            );
            """;
        var archivio = (string?)await comando.ExecuteScalarAsync() ?? "";

        Assert.DoesNotContain(titolo, archivio);
        Assert.DoesNotContain(domanda, archivio);
        Assert.DoesNotContain(risposta, archivio);
        Assert.Contains("enc:v2:", archivio);
    }

    [Fact]
    public async Task Ciphertext_TrapiantatoFraMessaggi_FallisceChiuso()
    {
        await using var db = NuovoDb();
        var servizio = NuovoServizio(db);
        await servizio.ImpostaAsync(TenantA, UtenteA, true);
        var prima = await servizio.CreaAsync(TenantA, UtenteA, new("Prima"));
        var seconda = await servizio.CreaAsync(TenantA, UtenteA, new("Seconda"));
        var t1 = await servizio.AvviaTurnoAsync(
            TenantA, UtenteA, prima.Id, Guid.NewGuid(), "Messaggio uno", prima.Versione);
        await servizio.CompletaTurnoAsync(TenantA, UtenteA, prima.Id, t1.TurnoId, "Risposta uno");
        var t2 = await servizio.AvviaTurnoAsync(
            TenantA, UtenteA, seconda.Id, Guid.NewGuid(), "Messaggio due", seconda.Versione);
        await servizio.CompletaTurnoAsync(TenantA, UtenteA, seconda.Id, t2.TurnoId, "Risposta due");
        db.ChangeTracker.Clear();

        var fonte = await db.AuroraMessaggi.AsNoTracking()
            .FirstAsync(m => m.ConversazioneId == prima.Id && m.Sequenza == 1);
        var destinazione = await db.AuroraMessaggi.AsNoTracking()
            .FirstAsync(m => m.ConversazioneId == seconda.Id && m.Sequenza == 1);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE AuroraMessaggi SET ContenutoProtetto = {fonte.ContenutoProtetto} WHERE Id = {destinazione.Id}");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            servizio.DettaglioAsync(TenantA, UtenteA, seconda.Id));
    }

    [Fact]
    public async Task Ttl24Ore_EOptOut_EliminanoFisicamenteIlTranscript()
    {
        await using var db = NuovoDb();
        var servizio = NuovoServizio(db);
        await servizio.ImpostaAsync(TenantA, UtenteA, true);
        var scade = await servizio.CreaAsync(TenantA, UtenteA, new("Scade"));
        _orologio.Avanti(TimeSpan.FromHours(24).Add(TimeSpan.FromSeconds(1)));

        Assert.Null(await servizio.DettaglioAsync(TenantA, UtenteA, scade.Id));
        Assert.Equal(0, await db.AuroraConversazioni.CountAsync());

        var nuova = await servizio.CreaAsync(TenantA, UtenteA, new("Da eliminare"));
        await servizio.ImpostaAsync(TenantA, UtenteA, false);
        Assert.Null(await servizio.DettaglioAsync(TenantA, UtenteA, nuova.Id));
        Assert.Equal(0, await db.AuroraConversazioni.CountAsync());
    }

    [Fact]
    public async Task PuliziaAutonoma_EliminaScaduteAncheSenzaNuovoAccessoDellUtente()
    {
        await using var db = NuovoDb();
        var servizio = NuovoServizio(db);
        await servizio.ImpostaAsync(TenantA, UtenteA, true);
        await servizio.ImpostaAsync(TenantA, UtenteB, true);
        await servizio.ImpostaAsync(TenantB, UtenteA, true);
        await servizio.CreaAsync(TenantA, UtenteA, new("Scaduta A"));
        await servizio.CreaAsync(TenantA, UtenteB, new("Scaduta B"));
        await servizio.CreaAsync(TenantB, UtenteA, new("Scaduta altro tenant"));
        _orologio.Avanti(TimeSpan.FromHours(24).Add(TimeSpan.FromSeconds(1)));

        var eliminate = await servizio.EliminaScaduteTenantAsync(TenantA);

        Assert.Equal(2, eliminate);
        Assert.Equal(1, await db.AuroraConversazioni.CountAsync());
        Assert.Equal(1, await db.AuroraConversazioni.CountAsync(c => c.TenantId == TenantB));
        Assert.Equal(1, await servizio.EliminaScaduteTenantAsync(TenantB));
        Assert.Equal(0, await db.AuroraConversazioni.CountAsync());
        Assert.Equal(0, await db.AuroraTurni.CountAsync());
        Assert.Equal(0, await db.AuroraMessaggi.CountAsync());
    }

    [Fact]
    public async Task Metadati_RifiutanoSegretiEDatiAltamenteSensibili()
    {
        await using var db = NuovoDb();
        var servizio = NuovoServizio(db);
        await servizio.ImpostaAsync(TenantA, UtenteA, true);
        var conversazione = await servizio.CreaAsync(TenantA, UtenteA, new("Titolo sicuro"));

        await Assert.ThrowsAsync<ArgumentException>(() => servizio.CreaAsync(
            TenantA, UtenteA, new(Titolo: "password=SegretoMoltoLungo123")));
        await Assert.ThrowsAsync<ArgumentException>(() => servizio.CreaAsync(
            TenantA, UtenteA, new(Progetto: "Certificato medico del lavoratore")));
        await Assert.ThrowsAsync<ArgumentException>(() => servizio.ModificaAsync(
            TenantA, UtenteA, conversazione.Id,
            new("access_token=Abcdefghijklmnop123456")));

        Assert.Single(await servizio.ElencoAsync(TenantA, UtenteA));
        Assert.Equal("Titolo sicuro", (await servizio.ElencoAsync(TenantA, UtenteA))[0].Titolo);
    }

    [Fact]
    public async Task Archivio_SeparaLeListe_EMantieneRipristinabileIlFilo()
    {
        await using var db = NuovoDb();
        var servizio = NuovoServizio(db);
        await servizio.ImpostaAsync(TenantA, UtenteA, true);
        var conversazione = await servizio.CreaAsync(TenantA, UtenteA, new("Da archiviare"));

        var archiviata = await servizio.ModificaAsync(
            TenantA, UtenteA, conversazione.Id, new(Archiviata: true));

        Assert.NotNull(archiviata?.ArchiviataUtc);
        Assert.Empty(await servizio.ElencoAsync(TenantA, UtenteA));
        Assert.Single(await servizio.ElencoAsync(TenantA, UtenteA, archiviate: true));

        var ripristinata = await servizio.ModificaAsync(
            TenantA, UtenteA, conversazione.Id, new(Archiviata: false));
        Assert.Null(ripristinata?.ArchiviataUtc);
        Assert.Single(await servizio.ElencoAsync(TenantA, UtenteA));
        Assert.Empty(await servizio.ElencoAsync(TenantA, UtenteA, archiviate: true));
    }

    [Theory]
    [InlineData("Certificato medico per malattia")]
    [InlineData("Iscrizione sindacale del dipendente")]
    [InlineData("Condanna nel casellario giudiziale")]
    [InlineData("Mario è musulmano")]
    [InlineData("Opinione politica del lavoratore")]
    [InlineData("Origine etnica dichiarata")]
    [InlineData("Orientamento sessuale della persona")]
    [InlineData("Dato biometrico per riconoscimento facciale")]
    [InlineData("Analisi del DNA e dato genetico")]
    [InlineData("Ha un tumore e segue un farmaco")]
    public void DatiD3_NonSonoPersistibili(string testo) =>
        Assert.True(AuroraConversazioniService.ContieneDatiEsclusi(testo));

    [Theory]
    [InlineData("Politica ferie aziendale")]
    [InlineData("Controllo ordinario del cedolino")]
    [InlineData("Matricola INPS e livello CCNL")]
    public void DatiOrdinari_NonSonoClassificatiComeD3(string testo) =>
        Assert.False(AuroraConversazioniService.ContieneDatiEsclusi(testo));

    [Fact]
    public async Task VersioneVecchia_BloccaLostUpdate()
    {
        await using var db = NuovoDb();
        var servizio = NuovoServizio(db);
        await servizio.ImpostaAsync(TenantA, UtenteA, true);
        var conversazione = await servizio.CreaAsync(TenantA, UtenteA, new("Concorrenza"));
        await servizio.AvviaTurnoAsync(
            TenantA, UtenteA, conversazione.Id, Guid.NewGuid(), "Prima", conversazione.Versione);

        await Assert.ThrowsAsync<ConversazioneAuroraConflittoException>(() =>
            servizio.AvviaTurnoAsync(
                TenantA, UtenteA, conversazione.Id, Guid.NewGuid(), "Seconda",
                conversazione.Versione));
    }

    private PagheDbContext NuovoDb()
    {
        var opzioni = new DbContextOptionsBuilder<PagheDbContext>()
            .UseSqlite(_connessione)
            .Options;
        return new PagheDbContext(opzioni, _protettore);
    }

    private AuroraConversazioniService NuovoServizio(PagheDbContext db) =>
        new(db, _orologio);

    public void Dispose() => _connessione.Dispose();

    private sealed class OrologioRegolabile(DateTimeOffset ora) : TimeProvider
    {
        private DateTimeOffset _ora = ora;
        public override DateTimeOffset GetUtcNow() => _ora;
        public void Avanti(TimeSpan durata) => _ora = _ora.Add(durata);
    }
}
