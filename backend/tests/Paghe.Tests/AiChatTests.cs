using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Api;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Assistente a chat: dialogo con il provider (function calling), strumenti di lettura e
/// — soprattutto — la regola che nessuna scrittura parte senza la conferma dell'utente.
/// </summary>
public sealed class AiChatTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Guid _tenant = Guid.NewGuid();

    public AiChatTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    private AiChatStrumenti NuoviStrumenti(PagheDbContext db) =>
        new(new AziendeService(db), new AnagraficheLavoroService(db), new ScadenzarioService(db));

    public void Dispose() => _connection.Dispose();

    // ---- Dialogo con il provider ----

    private sealed class HandlerFinto(HttpResponseMessage risposta) : HttpMessageHandler
    {
        public string? UltimoCorpo { get; private set; }
        public Uri? UltimoIndirizzo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            UltimoIndirizzo = request.RequestUri;
            UltimoCorpo = request.Content is null
                ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return risposta;
        }
    }

    [Fact]
    public async Task Chat_DeepSeek_MandaGliStrumenti_ELeggeLaChiamata()
    {
        var risposta = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
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
                                    id = "call_1",
                                    type = "function",
                                    function = new { name = "elenco_aziende", arguments = "{}" },
                                },
                            },
                        },
                    },
                },
            }), Encoding.UTF8, "application/json"),
        };
        var handler = new HandlerFinto(risposta);
        var chat = new AiChatClient(new HttpClient(handler),
            new AiOptions { Provider = "DeepSeek", ApiKey = "chiave-prova" });

        Assert.True(chat.Attivo);
        Assert.Equal("deepseek-chat", chat.Modello);

        var esito = await chat.ParlaAsync(
            [new MessaggioChatAi { Ruolo = "user", Contenuto = "quante aziende ho?" }],
            AiChatStrumenti.Catalogo);

        Assert.Equal("https://api.deepseek.com/chat/completions", chat_Indirizzo(handler));
        Assert.Contains("registra_azienda", handler.UltimoCorpo);
        Assert.Null(esito.Testo);
        var chiamata = Assert.Single(esito.Chiamate);
        Assert.Equal("elenco_aziende", chiamata.Nome);
        Assert.Equal("call_1", chiamata.Id);

        static string chat_Indirizzo(HandlerFinto h) => h.UltimoIndirizzo!.ToString();
    }

    [Fact]
    public void Chat_ProviderNonConfigurato_RestaSpentaSenzaEsplodere()
    {
        var chat = new AiChatClient(new HttpClient(), new AiOptions()); // Provider "Spento"
        Assert.False(chat.Attivo);
    }

    [Fact]
    public void Chat_ProviderSenzaChiave_RestaSpenta_ESpiegaCheMancaSoloLaChiave()
    {
        var opzioni = new AiOptions { Provider = "DeepSeek" }; // nessuna ApiKey
        Assert.False(new AiChatClient(new HttpClient(), opzioni).Attivo);
        Assert.Contains("Ai__ApiKey", AiChatEndpoints.CosaManca(opzioni));
    }

    [Fact]
    public void Chat_ModelloInLocale_NonChiedeChiavi()
    {
        var chat = new AiChatClient(new HttpClient(), new AiOptions
        {
            Provider = "Compatibile", BaseUrl = "http://localhost:11434/v1", Modello = "qwen2.5:14b",
        });
        Assert.True(chat.Attivo);
    }

    [Fact]
    public void Serializza_MessaggioTool_UsaIlFormatoDelProtocollo()
    {
        string json = JsonSerializer.Serialize(AiChatClient.Serializza(new MessaggioChatAi
        {
            Ruolo = "tool", ChiamataId = "call_9", Contenuto = "[]",
        }));
        Assert.Contains("\"tool_call_id\":\"call_9\"", json);

        string conChiamate = JsonSerializer.Serialize(AiChatClient.Serializza(new MessaggioChatAi
        {
            Ruolo = "assistant",
            Chiamate = [new ChiamataStrumentoAi { Id = "call_9", Nome = "scheda_azienda", ArgomentiJson = "{}" }],
        }));
        Assert.Contains("\"tool_calls\"", conChiamate);
        Assert.Contains("scheda_azienda", conChiamate);
    }

    // ---- Letture ----

    [Fact]
    public async Task Lettura_ElencoAziende_VedeSoloIlProprioStudio()
    {
        await using var db = NuovoContesto();
        var aziende = new AziendeService(db);
        await aziende.CreaAsync(_tenant, new AziendaEntity
        {
            RagioneSociale = "Alfa Srl", PartitaIva = "12345678901",
            Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
        });
        await aziende.CreaAsync(Guid.NewGuid(), new AziendaEntity
        {
            RagioneSociale = "Beta di un altro studio", Indirizzo = "Via Po 2",
            Cap = "10100", Comune = "Torino", Provincia = "TO",
        });

        string json = await NuoviStrumenti(db).EseguiLetturaAsync(_tenant, "elenco_aziende", "{}");

        Assert.Contains("Alfa Srl", json);
        Assert.DoesNotContain("Beta", json);
    }

    // ---- Scritture: prima si propongono, poi si eseguono ----

    [Fact]
    public async Task Proposta_AziendaIncompleta_ElencaCosaMancaEBloccaLaConferma()
    {
        await using var db = NuovoContesto();
        var proposta = await NuoviStrumenti(db).PreparaScritturaAsync(_tenant, "registra_azienda",
            """{"ragioneSociale":"Gamma Srl","partitaIva":"12345678901"}""");

        Assert.False(proposta.PuoProcedere);
        Assert.Contains(proposta.Campi, c => c.Etichetta == "Denominazione" && c.Valore == "Gamma Srl");
        Assert.Contains(proposta.Mancanti, m => m.Etichetta == "Comune" && m.Bloccante);
        // La matricola INPS manca ma non impedisce di salvare l'anagrafica.
        Assert.Contains(proposta.Mancanti, m => m.Etichetta == "Matricola INPS" && !m.Bloccante);
        Assert.Equal("aziende", proposta.Sezione);
    }

    [Fact]
    public async Task Proposta_AziendaCompleta_SiPuoConfermare_MaNienteVieneScritto()
    {
        await using var db = NuovoContesto();
        var proposta = await NuoviStrumenti(db).PreparaScritturaAsync(_tenant, "registra_azienda",
            """
            {"ragioneSociale":"Delta Srl","partitaIva":"12345678901","indirizzo":"Via Verdi 3",
             "cap":"20100","comune":"Milano","provincia":"MI"}
            """);

        Assert.True(proposta.PuoProcedere);
        Assert.Empty(await db.Aziende.Where(a => a.TenantId == _tenant).ToListAsync());
    }

    [Fact]
    public async Task Conferma_RegistraAzienda_ScriveDavvero_ESegnalaCosaResta()
    {
        await using var db = NuovoContesto();
        var (riuscita, messaggio, _) = await NuoviStrumenti(db).EseguiScritturaAsync(
            _tenant, "registra_azienda",
            """
            {"ragioneSociale":"Epsilon Srl","partitaIva":"12345678901","indirizzo":"Via Dante 5",
             "cap":"20100","comune":"Milano","provincia":"MI","pec":"epsilon@pec.it"}
            """);

        Assert.True(riuscita);
        Assert.Contains("Epsilon Srl", messaggio);
        Assert.Contains("matricolaInps", messaggio); // i dati contributivi restano da completare

        var salvata = Assert.Single(await db.Aziende.Where(a => a.TenantId == _tenant).ToListAsync());
        Assert.Equal("12345678901", salvata.PartitaIva);
        Assert.Equal("MI", salvata.Provincia);
    }

    [Fact]
    public async Task Conferma_AziendaDuplicata_RiportaIlMessaggioDelProgramma()
    {
        await using var db = NuovoContesto();
        var strumenti = NuoviStrumenti(db);
        const string dati = """
            {"ragioneSociale":"Zeta Srl","partitaIva":"12345678901","indirizzo":"Via Po 1",
             "cap":"10100","comune":"Torino","provincia":"TO"}
            """;
        await strumenti.EseguiScritturaAsync(_tenant, "registra_azienda", dati);

        var (riuscita, messaggio, _) = await strumenti.EseguiScritturaAsync(_tenant, "registra_azienda", dati);

        Assert.False(riuscita);
        Assert.Contains("12345678901", messaggio);
        Assert.Single(await db.Aziende.Where(a => a.TenantId == _tenant).ToListAsync());
    }

    [Fact]
    public async Task Proposta_Dipendente_SenzaCcnl_NonSiPuoConfermare()
    {
        await using var db = NuovoContesto();
        var azienda = await new AziendeService(db).CreaAsync(_tenant, new AziendaEntity
        {
            RagioneSociale = "Eta Srl", Indirizzo = "Via Emilia 9",
            Cap = "40100", Comune = "Bologna", Provincia = "BO",
        });

        var proposta = await NuoviStrumenti(db).PreparaScritturaAsync(_tenant, "registra_dipendente",
            $$"""
            {"aziendaId":"{{azienda.Id}}","nome":"Mario","cognome":"Rossi",
             "codiceFiscale":"RSSMRA80A01F205X"}
            """);

        Assert.False(proposta.PuoProcedere);
        Assert.Contains(proposta.Mancanti, m => m.Etichetta == "CCNL" && m.Bloccante);
        Assert.Contains("Rossi Mario", proposta.Titolo);
        Assert.Contains("Eta Srl", proposta.Titolo);
    }

    [Fact]
    public async Task Conferma_Dipendente_CreaAnagraficaERapporto()
    {
        await using var db = NuovoContesto();
        var azienda = await new AziendeService(db).CreaAsync(_tenant, new AziendaEntity
        {
            RagioneSociale = "Theta Srl", Indirizzo = "Via Napoli 2",
            Cap = "80100", Comune = "Napoli", Provincia = "NA",
        });

        var (riuscita, _, _) = await NuoviStrumenti(db).EseguiScritturaAsync(_tenant, "registra_dipendente",
            $$"""
            {"aziendaId":"{{azienda.Id}}","nome":"Luca","cognome":"Bianchi",
             "codiceFiscale":"bnclcu85a01f839x","codiceCcnl":"COMMERCIO","livello":"4",
             "dataAssunzione":"2026-03-01","oreSettimanali":20,"partTime":true,
             "percentualePartTime":50}
            """);

        Assert.True(riuscita);
        var rapporto = Assert.Single(await db.Rapporti.Include(r => r.Dipendente).ToListAsync());
        Assert.Equal("BNCLCU85A01F839X", rapporto.Dipendente!.CodiceFiscale);
        Assert.Equal(new DateOnly(2026, 3, 1), rapporto.DataAssunzione);
        Assert.True(rapporto.PartTime);
        Assert.Equal(50m, rapporto.PercentualePartTime);
    }

    [Fact]
    public void Catalogo_DistingueLettureEScritture()
    {
        Assert.True(AiChatStrumenti.ÈScrittura("registra_azienda"));
        Assert.False(AiChatStrumenti.ÈScrittura("elenco_aziende"));
        Assert.False(AiChatStrumenti.ÈScrittura("strumento_inventato"));
        Assert.True(AiChatStrumenti.EsisteStrumento("scheda_azienda"));
    }
}
