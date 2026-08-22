using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Api;
using Paghe.Persistence;
using Paghe.Persistence.Ai;
using Paghe.Persistence.Entities;
using Paghe.Persistence.Gdpr;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Runtime Aurora della fase A2: registry filtrato da gate/ruolo/mandato, letture solo dal
/// broker governato, guardie del loop (ripetizioni, giri, timeout), interruzione pulita su
/// revoca e quota, SessionLog a digest senza payload, proposta firmata con conferma
/// idempotente e anti-contraffazione.
/// </summary>
public sealed class AuroraRuntimeTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _utente = Guid.NewGuid();

    private readonly AiGovernorOptions _opzioniGovernor =
        new(AiLeaseCodec.DerivaChiave(Encoding.UTF8.GetBytes("chiave-di-prova-runtime-32-byte!!")));

    private readonly AiPropostaOptions _opzioniProposta =
        new(AiPropostaCodec.DerivaChiave(Encoding.UTF8.GetBytes("chiave-di-prova-runtime-32-byte!!")));

    private readonly OrologioRegolabile _orologio =
        new(new DateTimeOffset(2026, 7, 16, 9, 0, 0, TimeSpan.Zero));

    public AuroraRuntimeTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private PagheDbContext NuovoContesto() => new(
        new DbContextOptionsBuilder<PagheDbContext>()
            .UseSqlite(_connection)
            .Options);

    // ---------- Infrastruttura di prova ----------

    private sealed class OrologioRegolabile(DateTimeOffset iniziale) : TimeProvider
    {
        private DateTimeOffset _ora = iniziale;
        public override DateTimeOffset GetUtcNow() => _ora;
        public void Avanza(TimeSpan salto) => _ora += salto;
    }

    /// <summary>Provider finto che risponde in sequenza: un corpo per ogni giro del loop.</summary>
    private sealed class HandlerSequenza(params string[] corpi) : HttpMessageHandler
    {
        private int _indice;
        public List<string> CorpiRichieste { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
                CorpiRichieste.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            var corpo = corpi[Math.Min(_indice++, corpi.Length - 1)];
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
            };
        }
    }

    private static AiChatClient ChatFinta(HandlerSequenza handler) => new(
        new HttpClient(handler),
        new AiOptions
        {
            Provider = "Compatibile",
            BaseUrl = "https://provider.test/v1",
            ApiKey = "chiave-di-test-non-reale",
            Modello = "modello-finto",
            SoloLocale = false,
            ProviderCloudApprovato = true,
            HostCloudConsentiti = ["provider.test"],
        });

    private static string RispostaTesto(string testo) => JsonSerializer.Serialize(new
    {
        choices = new[] { new { message = new { role = "assistant", content = testo } } },
        usage = new { total_tokens = 10 },
    });

    private static string RispostaToolCall(string id, string nome, string argomenti = "{}") =>
        JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        role = "assistant",
                        content = (string?)null,
                        tool_calls = new[]
                        {
                            new
                            {
                                id,
                                type = "function",
                                function = new { name = nome, arguments = argomenti },
                            },
                        },
                    },
                },
            },
            usage = new { total_tokens = 20 },
        });

    private (AuroraAgentRuntime Runtime, AiRuntimeStore Store, AiGrantService Governor, AiToolRegistry Registry)
        NuovoRuntime(PagheDbContext db, AiChatClient chat, IEnumerable<IStrumentoAurora>? extra = null,
            TimeSpan? timeoutStrumento = null)
    {
        var governor = new AiGrantService(db, _opzioniGovernor, _orologio);
        var broker = new ProfessionalQueryBroker(db, governor, new ScadenzarioService(db), _orologio);
        var strumenti = AuroraStrumentiCatalogo.Letture
            .Select(d => (IStrumentoAurora)new StrumentoLetturaBroker(d, broker))
            .Concat(AuroraStrumentiCatalogo.ScrittureDichiarate
                .Select(d => (IStrumentoAurora)new StrumentoScritturaDichiarata(d)))
            .Concat(extra ?? []);
        var registry = new AiToolRegistry(strumenti);
        var store = new AiRuntimeStore(db, _opzioniProposta, _orologio);
        return (new AuroraAgentRuntime(chat, registry, governor, store, timeoutStrumento),
            store, governor, registry);
    }

    private async Task<Guid> CreaAziendaConDipendentiAsync(PagheDbContext db, int dipendenti = 1)
    {
        using var _ = AmbientContestoAudit.Imposta(_tenant, _utente);
        var azienda = new AziendaEntity
        {
            Id = Guid.NewGuid(), TenantId = _tenant, RagioneSociale = "Runtime Srl",
        };
        db.Aziende.Add(azienda);
        for (int i = 0; i < dipendenti; i++)
        {
            var dipendente = new DipendenteEntity
            {
                Id = Guid.NewGuid(), TenantId = _tenant,
                CodiceFiscale = $"XXXXXX00A0{i}{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
                Nome = "Aurora", Cognome = $"DiProva{i}",
            };
            db.Dipendenti.Add(dipendente);
            db.Rapporti.Add(new RapportoLavoroEntity
            {
                Id = Guid.NewGuid(), TenantId = _tenant,
                AziendaId = azienda.Id, DipendenteId = dipendente.Id,
                CodiceCcnl = "COMMERCIO", Livello = "IV",
                DataAssunzione = new DateOnly(2026, 1, 1),
            });
        }
        await db.SaveChangesAsync();
        return azienda.Id;
    }

    private async Task<(ManifestGrant Manifest, string Lease)> MandatoAsync(
        AiGrantService governor, Guid azienda, string[] risorse, int? maxLetture = null)
    {
        var (grant, errore) = await governor.EmettiAsync(_tenant, _utente, new RichiestaGrant(
            PurposeCode: "CONTROLLO_ANOMALIE_PAGHE",
            Modalita: "Guidata",
            ScopeLevel: "S2",
            AziendaId: azienda,
            Periodo: "2026-07",
            Risorse: risorse,
            Operazioni: ["read"],
            MaxLetture: maxLetture));
        Assert.Null(errore);
        return (grant!.Manifest, grant.Lease);
    }

    private ContestoAurora Contesto(ManifestGrant manifest, string lease, string ruolo = "operatore") =>
        new(_tenant, _utente, ruolo, lease, manifest);

    private static List<MessaggioChatAi> Conversazione(string domanda) =>
    [
        new() { Ruolo = "system", Contenuto = "Istruzioni di prova." },
        new() { Ruolo = "user", Contenuto = domanda },
    ];

    /// <summary>Strumento di prova con comportamento iniettabile (per timeout e scritture).</summary>
    private sealed class StrumentoDiProva(
        DescrittoreStrumento descrittore,
        Func<ContestoAurora, string, CancellationToken, Task<RisultatoStrumentoAurora>>? esegui = null,
        Func<ContestoAurora, string, CancellationToken, Task<PropostaAurora>>? prepara = null,
        Func<ContestoAurora, string, CancellationToken, Task<(bool, string)>>? applica = null)
        : IStrumentoAurora
    {
        public DescrittoreStrumento Descrittore { get; } = descrittore;

        public Task<RisultatoStrumentoAurora> EseguiAsync(
            ContestoAurora contesto, string argomentiJson, CancellationToken ct) =>
            esegui!(contesto, argomentiJson, ct);

        public Task<PropostaAurora> PreparaAsync(
            ContestoAurora contesto, string argomentiJson, CancellationToken ct) =>
            prepara!(contesto, argomentiJson, ct);

        public Task<(bool Riuscita, string EsitoJson)> ApplicaAsync(
            ContestoAurora contesto, string argomentiJson, CancellationToken ct) =>
            applica!(contesto, argomentiJson, ct);
    }

    private static DescrittoreStrumento DescrittoreLettura(string nome, string risorsa) => new(
        nome, "Prova", "Prova", new { type = "object" }, "lettura", "testo", "sola_lettura",
        risorsa, "read", "A2", Idempotente: true, Undo: "Nessuno.");

    // ---------- Registry: default DENY per gate, ruolo e mandato ----------

    [Fact]
    public async Task Registry_ScrittureDichiarateA3_MaiOfferteAlModello()
    {
        await using var db = NuovoContesto();
        var azienda = await CreaAziendaConDipendentiAsync(db);
        var (_, store, governor, registry) = NuovoRuntime(db, ChatFinta(new HandlerSequenza()));
        _ = store;
        var (manifest, _) = await MandatoAsync(governor, azienda, ["Aziende", "Dipendenti"]);

        var offerti = registry.PerMandato("admin", manifest).Select(s => s.Descrittore.Nome).ToArray();

        Assert.Contains("scheda_azienda", offerti);
        Assert.Contains("elenco_dipendenti", offerti);
        Assert.DoesNotContain("registra_azienda", offerti);
        Assert.DoesNotContain("correggi_azienda", offerti);
        Assert.DoesNotContain("registra_dipendente", offerti);
    }

    [Fact]
    public async Task Registry_RisorsaFuoriMandato_NonOfferta()
    {
        await using var db = NuovoContesto();
        var azienda = await CreaAziendaConDipendentiAsync(db);
        var (_, _, governor, registry) = NuovoRuntime(db, ChatFinta(new HandlerSequenza()));
        var (manifest, _) = await MandatoAsync(governor, azienda, ["Scadenzario"]);

        var offerti = registry.PerMandato("operatore", manifest).Select(s => s.Descrittore.Nome).ToArray();

        Assert.Equal(["scadenze_studio"], offerti);
    }

    [Fact]
    public void Registry_RuoloSolaLettura_NonRiceveScritture()
    {
        var scrittura = new DescrittoreStrumento(
            "scrivi_prova", "Prova", "Prova", new { }, "scrittura_reversibile", "diff",
            "operatore", "Dipendenti", "draft", "A2", true, "Nessuno.");

        Assert.False(AiToolRegistry.RuoloAmmesso("sola_lettura", scrittura));
        Assert.True(AiToolRegistry.RuoloAmmesso("operatore", scrittura));
        Assert.True(AiToolRegistry.RuoloAmmesso("admin", scrittura));
        Assert.True(AiToolRegistry.RuoloAmmesso(
            "sola_lettura", DescrittoreLettura("leggi_prova", "Dipendenti")));
        Assert.False(AiToolRegistry.RuoloAmmesso(
            "ruolo_inventato", DescrittoreLettura("leggi_prova", "Dipendenti")));
    }

    // ---------- Loop: lettura governata, SessionLog e injection ----------

    [Fact]
    public async Task Turno_LetturaGovernata_EseguitaELoggataADigest()
    {
        await using var db = NuovoContesto();
        var azienda = await CreaAziendaConDipendentiAsync(db);
        var handler = new HandlerSequenza(
            RispostaToolCall("call_1", "elenco_dipendenti"),
            RispostaTesto("C'è un dipendente: inquadramento COMMERCIO livello IV."));
        var (runtime, _, governor, _) = NuovoRuntime(db, ChatFinta(handler));
        var (manifest, lease) = await MandatoAsync(governor, azienda, ["Dipendenti"]);

        var esito = await runtime.TurnoAsync(
            Contesto(manifest, lease), Conversazione("Che dipendenti ho?"), "Dipendenti", default);

        Assert.True(esito.Ok);
        Assert.Null(esito.Proposta);
        Assert.Contains("COMMERCIO", esito.Testo);

        // Il secondo giro del modello ha visto le righe minimizzate del broker.
        Assert.Equal(2, handler.CorpiRichieste.Count);
        Assert.Contains("DiProva0", handler.CorpiRichieste[1]);

        var run = Assert.Single(await db.AiRuns.AsNoTracking().ToListAsync());
        Assert.Equal(_tenant, run.TenantId);
        Assert.Equal(manifest.GrantId, run.GrantId);
        Assert.Equal("Ok", run.Esito);
        Assert.Equal(2, run.Giri);
        Assert.Equal(30, run.TokenTotali);
        Assert.Equal(64, run.DigestMessaggi.Length);

        var chiamata = Assert.Single(await db.AiToolCalls.AsNoTracking().ToListAsync());
        Assert.Equal("elenco_dipendenti", chiamata.Strumento);
        Assert.Equal("Ok", chiamata.Esito);
        Assert.Equal(1, chiamata.Righe);
        Assert.Equal(64, chiamata.ArgomentiHash.Length);

        // Il log non duplica contenuti: né la domanda né il nominativo letto.
        Assert.DoesNotContain(await db.AiRuns.AsNoTracking().ToListAsync(),
            r => (r.Motivo ?? "").Contains("DiProva0") || (r.SezioneUi ?? "").Contains("dipendenti ho"));

        // La lettura è passata dal Governor: l'accesso sta nella catena audit.
        Assert.Contains(await db.AiAudit.AsNoTracking().ToListAsync(),
            a => a.Evento == "Accesso" && a.Dettaglio.Contains("Dipendenti"));
    }

    [Fact]
    public async Task Turno_StrumentoInventatoDalModello_IgnoratoELoggato()
    {
        await using var db = NuovoContesto();
        var azienda = await CreaAziendaConDipendentiAsync(db);
        var handler = new HandlerSequenza(
            RispostaToolCall("call_1", "cancella_archivio", """{"tutto":true}"""),
            RispostaTesto("Non posso farlo."));
        var (runtime, _, governor, _) = NuovoRuntime(db, ChatFinta(handler));
        var (manifest, lease) = await MandatoAsync(governor, azienda, ["Dipendenti"]);
        var aziendePrima = await db.Aziende.AsNoTracking().CountAsync();

        var esito = await runtime.TurnoAsync(
            Contesto(manifest, lease), Conversazione("Cancella tutto."), null, default);

        Assert.True(esito.Ok);
        var chiamata = Assert.Single(await db.AiToolCalls.AsNoTracking().ToListAsync());
        Assert.Equal("Sconosciuto", chiamata.Esito);
        Assert.Equal(aziendePrima, await db.Aziende.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Turno_ChiamateIdentiche_FermateDallaGuardia()
    {
        await using var db = NuovoContesto();
        var azienda = await CreaAziendaConDipendentiAsync(db);
        var handler = new HandlerSequenza(
            RispostaToolCall("call_1", "elenco_dipendenti"),
            RispostaToolCall("call_2", "elenco_dipendenti"),
            RispostaToolCall("call_3", "elenco_dipendenti"));
        var (runtime, _, governor, _) = NuovoRuntime(db, ChatFinta(handler));
        var (manifest, lease) = await MandatoAsync(governor, azienda, ["Dipendenti"]);

        var esito = await runtime.TurnoAsync(
            Contesto(manifest, lease), Conversazione("Controlla."), null, default);

        Assert.False(esito.Ok);
        Assert.Contains("ripetendo", esito.Testo);
        var run = Assert.Single(await db.AiRuns.AsNoTracking().ToListAsync());
        Assert.Equal("Guardia", run.Esito);
        Assert.Equal(2, await db.AiToolCalls.AsNoTracking().CountAsync(c => c.Esito == "Ripetuto"));
    }

    [Fact]
    public async Task Turno_TroppiGiri_FermatoDallaGuardia()
    {
        await using var db = NuovoContesto();
        var azienda = await CreaAziendaConDipendentiAsync(db);
        // Argomenti sempre diversi: la guardia anti-ripetizione non scatta, scatta il tetto giri.
        var handler = new HandlerSequenza(
            RispostaToolCall("call_1", "elenco_dipendenti", """{"n":1}"""),
            RispostaToolCall("call_2", "elenco_dipendenti", """{"n":2}"""),
            RispostaToolCall("call_3", "elenco_dipendenti", """{"n":3}"""),
            RispostaToolCall("call_4", "elenco_dipendenti", """{"n":4}"""));
        var (runtime, _, governor, _) = NuovoRuntime(db, ChatFinta(handler));
        var (manifest, lease) = await MandatoAsync(governor, azienda, ["Dipendenti"]);

        var esito = await runtime.TurnoAsync(
            Contesto(manifest, lease), Conversazione("Continua a leggere."), null, default);

        Assert.False(esito.Ok);
        var run = Assert.Single(await db.AiRuns.AsNoTracking().ToListAsync());
        Assert.Equal("Guardia", run.Esito);
        Assert.Equal(AuroraAgentRuntime.MassimoGiri, run.Giri);
    }

    [Fact]
    public async Task Turno_MandatoRevocato_InterrottoPulito()
    {
        await using var db = NuovoContesto();
        var azienda = await CreaAziendaConDipendentiAsync(db);
        var handler = new HandlerSequenza(RispostaToolCall("call_1", "elenco_dipendenti"));
        var (runtime, _, governor, _) = NuovoRuntime(db, ChatFinta(handler));
        var (manifest, lease) = await MandatoAsync(governor, azienda, ["Dipendenti"]);
        Assert.True(await governor.RevocaAsync(_tenant, _utente, manifest.GrantId));

        // La sessione era già aperta: la revoca colpisce la tool call, non l'ingresso.
        var esito = await runtime.TurnoAsync(
            Contesto(manifest, lease), Conversazione("Leggi i dipendenti."), null, default);

        Assert.False(esito.Ok);
        Assert.Contains("revocato", esito.Testo, StringComparison.OrdinalIgnoreCase);
        var run = Assert.Single(await db.AiRuns.AsNoTracking().ToListAsync());
        Assert.Equal("Revocato", run.Esito);
    }

    [Fact]
    public async Task Turno_QuotaEsaurita_CheckpointNonSilenzioso()
    {
        await using var db = NuovoContesto();
        var azienda = await CreaAziendaConDipendentiAsync(db, dipendenti: 3);
        var handler = new HandlerSequenza(RispostaToolCall("call_1", "elenco_dipendenti"));
        var (runtime, _, governor, _) = NuovoRuntime(db, ChatFinta(handler));
        // Quota 1: il broker trova 3 righe e il Governor rifiuta il consumo.
        var (manifest, lease) = await MandatoAsync(governor, azienda, ["Dipendenti"], maxLetture: 1);

        var esito = await runtime.TurnoAsync(
            Contesto(manifest, lease), Conversazione("Leggi i dipendenti."), null, default);

        Assert.False(esito.Ok);
        Assert.Contains("checkpoint", esito.Testo, StringComparison.OrdinalIgnoreCase);
        var run = Assert.Single(await db.AiRuns.AsNoTracking().ToListAsync());
        Assert.Equal("Checkpoint", run.Esito);
    }

    [Fact]
    public async Task Turno_StrumentoLento_TimeoutSenzaBloccareIlTurno()
    {
        await using var db = NuovoContesto();
        var azienda = await CreaAziendaConDipendentiAsync(db);
        var lento = new StrumentoDiProva(
            DescrittoreLettura("lettura_lenta", "Dipendenti"),
            esegui: async (_, _, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                return new RisultatoStrumentoAurora("{}", false, null, 0);
            });
        var handler = new HandlerSequenza(
            RispostaToolCall("call_1", "lettura_lenta"),
            RispostaTesto("Proseguo senza quel dato."));
        var (runtime, _, governor, _) = NuovoRuntime(
            db, ChatFinta(handler), [lento], timeoutStrumento: TimeSpan.FromMilliseconds(50));
        var (manifest, lease) = await MandatoAsync(governor, azienda, ["Dipendenti"]);

        var esito = await runtime.TurnoAsync(
            Contesto(manifest, lease), Conversazione("Leggi."), null, default);

        Assert.True(esito.Ok);
        var chiamata = Assert.Single(await db.AiToolCalls.AsNoTracking().ToListAsync());
        Assert.Equal("Timeout", chiamata.Esito);
    }

    // ---------- Scritture: oggi le policy non concedono draft ----------

    [Fact]
    public async Task Grant_OperazioneDraft_RifiutataDallePolicyCorrenti()
    {
        await using var db = NuovoContesto();
        var azienda = await CreaAziendaConDipendentiAsync(db);
        var governor = new AiGrantService(db, _opzioniGovernor, _orologio);

        var (grant, errore) = await governor.EmettiAsync(_tenant, _utente, new RichiestaGrant(
            "CONTROLLO_ANOMALIE_PAGHE", "Guidata", "S2", azienda, "2026-07",
            ["Dipendenti"], ["read", "draft"]));

        Assert.Null(grant);
        Assert.Contains("draft", errore);
    }

    // ---------- Sessione: la lease del pannello si risolve, mai si forgia ----------

    [Fact]
    public async Task Sessione_LeaseValida_RestituisceIlManifest()
    {
        await using var db = NuovoContesto();
        var azienda = await CreaAziendaConDipendentiAsync(db);
        var governor = new AiGrantService(db, _opzioniGovernor, _orologio);
        var (manifest, lease) = await MandatoAsync(governor, azienda, ["Dipendenti"]);

        var (risolto, motivo) = await governor.RisolviSessioneAsync(_tenant, _utente, lease);

        Assert.Null(motivo);
        Assert.Equal(manifest.GrantId, risolto!.GrantId);
        Assert.Equal(manifest.AziendaId, risolto.AziendaId);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("utente")]
    [InlineData("revocato")]
    [InlineData("alterata")]
    public async Task Sessione_LeaseNonSpendibile_Negata(string caso)
    {
        await using var db = NuovoContesto();
        var azienda = await CreaAziendaConDipendentiAsync(db);
        var governor = new AiGrantService(db, _opzioniGovernor, _orologio);
        var (manifest, lease) = await MandatoAsync(governor, azienda, ["Dipendenti"]);

        Guid tenant = _tenant, utente = _utente;
        switch (caso)
        {
            case "tenant": tenant = Guid.NewGuid(); break;
            case "utente": utente = Guid.NewGuid(); break;
            case "revocato": await governor.RevocaAsync(_tenant, _utente, manifest.GrantId); break;
            case "alterata": lease = lease[..^2] + (lease[^1] == 'A' ? "BB" : "AA"); break;
        }

        var (risolto, motivo) = await governor.RisolviSessioneAsync(tenant, utente, lease);

        Assert.Null(risolto);
        Assert.NotNull(motivo);
    }

    // ---------- Proposta firmata: idempotente e anti-contraffazione ----------

    private const string ArgomentiProva =
        """{"nome":"Mario","cognome":"Rossi","codiceFiscale":"RSSMRA80A01F205X"}""";

    private async Task<(AiPropostaEntity Proposta, string Token, AiRuntimeStore Store)>
        NuovaPropostaAsync(PagheDbContext db)
    {
        var store = new AiRuntimeStore(db, _opzioniProposta, _orologio);
        var (proposta, token) = await store.CreaPropostaAsync(
            _tenant, _utente, null, null, "strumento_prova", ArgomentiProva,
            "Assunzione di Rossi Mario",
            [new RigaDiffAurora("Nome", null, "Mario"), new RigaDiffAurora("Cognome", null, "Rossi")]);
        return (proposta, token, store);
    }

    [Fact]
    public async Task Proposta_ArgomentiCifrati_MaiInChiaroNelDatabase()
    {
        await using var db = NuovoContesto();
        var (proposta, _, _) = await NuovaPropostaAsync(db);

        var salvata = await db.AiProposte.AsNoTracking().SingleAsync(p => p.Id == proposta.Id);
        Assert.DoesNotContain("Rossi", salvata.ArgomentiProtetti);
        Assert.DoesNotContain("RSSMRA80A01F205X", salvata.ArgomentiProtetti);
        Assert.Equal("InAttesa", salvata.Stato);
        Assert.Equal(AiRuntimeStore.Hash(ArgomentiProva), salvata.ArgomentiHash);
    }

    [Fact]
    public async Task Proposta_ConfermaApplicaUnaVolta_EIlReplayNonDuplica()
    {
        await using var db = NuovoContesto();
        var (_, token, store) = await NuovaPropostaAsync(db);
        int applicazioni = 0;

        var primo = await store.ConfermaAsync(_tenant, _utente, token, (strumento, argomenti, _) =>
        {
            applicazioni++;
            Assert.Equal("strumento_prova", strumento);
            Assert.Equal(ArgomentiProva, argomenti); // gli argomenti tornano decifrati e intatti
            return Task.FromResult((true, """{"rapportoId":"nuovo"}"""));
        });
        var replay = await store.ConfermaAsync(_tenant, _utente, token,
            (_, _, _) => { applicazioni++; return Task.FromResult((true, "{}")); });

        Assert.Equal("Applicata", primo.Stato);
        Assert.Equal("GiaApplicata", replay.Stato);
        Assert.Equal(primo.EsitoJson, replay.EsitoJson);
        Assert.Equal(1, applicazioni);
    }

    [Fact]
    public async Task Proposta_TokenContraffatto_Rifiutato()
    {
        await using var db = NuovoContesto();
        var (_, token, store) = await NuovaPropostaAsync(db);
        int applicazioni = 0;

        var alterato = token[..^2] + (token[^1] == 'A' ? "BB" : "AA");
        var esito = await store.ConfermaAsync(_tenant, _utente, alterato,
            (_, _, _) => { applicazioni++; return Task.FromResult((true, "{}")); });

        Assert.Equal("NonValida", esito.Stato);
        Assert.Equal(0, applicazioni);
    }

    [Fact]
    public async Task Proposta_UtenteOTenantDiversi_NonTrovata()
    {
        await using var db = NuovoContesto();
        var (_, token, store) = await NuovaPropostaAsync(db);
        int applicazioni = 0;
        Task<(bool, string)> MaiApplicare(string s, string a, CancellationToken c)
        {
            applicazioni++;
            return Task.FromResult((true, "{}"));
        }

        Assert.Equal("NonTrovata",
            (await store.ConfermaAsync(Guid.NewGuid(), _utente, token, MaiApplicare)).Stato);
        Assert.Equal("NonTrovata",
            (await store.ConfermaAsync(_tenant, Guid.NewGuid(), token, MaiApplicare)).Stato);
        Assert.Equal(0, applicazioni);
    }

    [Fact]
    public async Task Proposta_Scaduta_NonApplicabile()
    {
        await using var db = NuovoContesto();
        var (proposta, token, store) = await NuovaPropostaAsync(db);
        _orologio.Avanza(AiRuntimeStore.DurataProposta + TimeSpan.FromSeconds(1));

        var esito = await store.ConfermaAsync(_tenant, _utente, token,
            (_, _, _) => Task.FromResult((true, "{}")));

        Assert.Equal("Scaduta", esito.Stato);
        Assert.Equal("Scaduta",
            (await db.AiProposte.AsNoTracking().SingleAsync(p => p.Id == proposta.Id)).Stato);
    }

    [Fact]
    public async Task Proposta_DominioRifiuta_RegistrataComeAnnullata()
    {
        await using var db = NuovoContesto();
        var (proposta, token, store) = await NuovaPropostaAsync(db);

        var esito = await store.ConfermaAsync(_tenant, _utente, token,
            (_, _, _) => Task.FromResult((false, """{"errore":"Codice fiscale già assunto."}""")));

        Assert.Equal("Fallita", esito.Stato);
        Assert.Contains("già assunto", esito.EsitoJson);
        Assert.Equal("Annullata",
            (await db.AiProposte.AsNoTracking().SingleAsync(p => p.Id == proposta.Id)).Stato);
    }

    [Fact]
    public void Codec_Proposta_FirmaERoundTrip()
    {
        var chiave = AiPropostaCodec.DerivaChiave(Encoding.UTF8.GetBytes("chiave-qualunque-32-byte-di-test!"));
        var id = Guid.NewGuid();
        var scade = new DateTime(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);

        var token = AiPropostaCodec.Crea(id, "HASH", scade, chiave);
        var decodificato = AiPropostaCodec.Prova(token, chiave);

        Assert.NotNull(decodificato);
        Assert.Equal(id, decodificato.PropostaId);
        Assert.Equal("HASH", decodificato.ArgomentiHash);
        Assert.Equal(scade, decodificato.ScadeUtc);

        Assert.Null(AiPropostaCodec.Prova(token[..^2] + "zz", chiave));
        Assert.Null(AiPropostaCodec.Prova(token,
            AiPropostaCodec.DerivaChiave(Encoding.UTF8.GetBytes("una-chiave-diversa-32-byte-test!!"))));
        // Una lease del Governor non è spendibile come token di conferma (etichette distinte).
        var lease = AiLeaseCodec.Crea(id, "NONCE", DateTime.UtcNow, scade,
            AiLeaseCodec.DerivaChiave(Encoding.UTF8.GetBytes("chiave-qualunque-32-byte-di-test!")));
        Assert.Null(AiPropostaCodec.Prova(lease, chiave));
    }
}
