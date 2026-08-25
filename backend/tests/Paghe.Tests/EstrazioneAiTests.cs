using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Paghe.Api;
using Paghe.Core.Ai;
using Paghe.Core.PartiteIva;
using Xunit;

// Paghe.Api espone un CampoProposto omonimo (etichetta + valore, per la chat AI): qui serve
// quello del seam di estrazione documenti, con la confidenza.
using CampoProposto = Paghe.Core.Ai.CampoProposto;

namespace Paghe.Tests;

/// <summary>
/// Test del livello AI di lettura documenti: default spento, fusione euristica+AI
/// (l'euristica vince, l'AI riempie solo i buchi sopra soglia), parsing delle risposte
/// e adapter OpenAI-compatibile con handler HTTP finto (nessuna rete).
/// </summary>
public sealed class EstrazioneAiTests
{
    // ---- Default: AI spenta ----

    [Fact]
    public async Task Spento_NonAttivo_E_NessunaProposta()
    {
        var ai = new EstrattoreAiSpento();
        Assert.False(ai.Attivo);
        Assert.False(ai.VedeImmagini);
        Assert.Null(await ai.EstraiAsync("visura camerale", "testo", ["partitaIva"]));
        Assert.Null(await ai.EstraiDaImmagineAsync("visura camerale", [1, 2], "image/png", ["partitaIva"]));
    }

    // ---- Fusione euristica + AI ----

    private static DatiAziendaDaVisura Euristica(string? piva = "01234567897", string? comune = null) =>
        new("Alfa Srl", piva, null, "Via Roma 1", "20100", comune, null, null, null);

    [Fact]
    public void CampiMancanti_ElencaSoloIVuoti()
    {
        var mancanti = CompletamentoVisuraAi.CampiMancanti(Euristica());
        Assert.Contains("codiceFiscale", mancanti);
        Assert.Contains("comune", mancanti);
        Assert.Contains("provincia", mancanti);
        Assert.DoesNotContain("ragioneSociale", mancanti);
        Assert.DoesNotContain("partitaIva", mancanti);
    }

    [Fact]
    public void Completa_AiRiempieSoloIBuchi_ELEuristicaVince()
    {
        var proposta = new PropostaAi(
        [
            new CampoProposto("comune", "Milano", 0.95m),
            new CampoProposto("partitaIva", "99999999999", 0.99m), // già estratta: NON deve vincere
            new CampoProposto("provincia", "MI", 0.4m),            // sotto soglia: scartata
        ]);

        var (dati, dallAi) = CompletamentoVisuraAi.Completa(Euristica(), proposta, 0.6m);

        Assert.Equal("Milano", dati.Comune);
        Assert.Equal("01234567897", dati.PartitaIva); // euristica intatta
        Assert.Null(dati.Provincia);                  // confidenza insufficiente
        Assert.Equal(["comune"], dallAi);
    }

    // ---- Parsing della risposta AI (contratto comune a tutti i provider) ----

    [Fact]
    public void Interpreta_JsonValido_ProduceCampi()
    {
        var proposta = PropostaAiJson.Interpreta(
            """{"campi":[{"nome":"comune","valore":"Milano","confidenza":0.9},{"nome":"cap","valore":null,"confidenza":0}]}""");
        Assert.NotNull(proposta);
        Assert.Equal(2, proposta.Campi.Count);
        Assert.Equal("Milano", proposta.Campi[0].Valore);
        Assert.Null(proposta.Campi[1].Valore);
    }

    [Theory]
    [InlineData("non è json")]
    [InlineData("{}")]
    [InlineData("""{"campi":[]}""")]
    public void Interpreta_RisposteNonUtilizzabili_RestituisceNull(string json) =>
        Assert.Null(PropostaAiJson.Interpreta(json));

    [Fact]
    public void Interpreta_ConfidenzaFuoriScala_VieneRiportataInScala()
    {
        var proposta = PropostaAiJson.Interpreta(
            """{"campi":[{"nome":"cap","valore":"20100","confidenza":7}]}""");
        Assert.Equal(1m, proposta!.Campi[0].Confidenza);
    }

    // ---- Adapter OpenAI-compatibile (DeepSeek/GLM/qualsiasi), con handler finto ----

    private sealed class HandlerFinto(HttpResponseMessage risposta) : HttpMessageHandler
    {
        public HttpRequestMessage? UltimaRichiesta { get; private set; }
        public string? UltimoCorpo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            UltimaRichiesta = request;
            UltimoCorpo = request.Content is null
                ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return risposta;
        }
    }

    private sealed record RichiestaCompatibile(
        Uri? Indirizzo, string? Autorizzazione, string? Corpo, bool HeaderGoogle);

    private sealed class HandlerSequenza(params HttpResponseMessage[] risposte) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _risposte = new(risposte);
        public List<RichiestaCompatibile> Richieste { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? corpo = request.Content is null
                ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Richieste.Add(new RichiestaCompatibile(
                request.RequestUri,
                request.Headers.Authorization?.Parameter,
                corpo,
                request.Headers.Contains("x-goog-api-client")));
            return _risposte.Dequeue();
        }
    }

    private static HttpResponseMessage RispostaChat(string contenuto) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { role = "assistant", content = contenuto } } },
        }), Encoding.UTF8, "application/json"),
    };

    [Fact]
    public async Task Compatibile_DeepSeek_ChiamaChatCompletions_EInterpretaLaProposta()
    {
        var handler = new HandlerFinto(RispostaChat(
            """{"campi":[{"nome":"comune","valore":"Milano","confidenza":0.9}]}"""));
        var ai = new EstrattoreAiCompatibile(new HttpClient(handler),
            new AiOptions
            {
                Provider = "DeepSeek", ApiKey = "chiave-prova",
                ProviderCloudApprovato = true,
            });

        Assert.True(ai.Attivo);
        Assert.Equal("deepseek-v4-flash", ai.Modello);

        var proposta = await ai.EstraiAsync("visura camerale", "testo visura", ["comune"]);

        Assert.NotNull(proposta);
        Assert.Equal("Milano", proposta.Campi[0].Valore);
        Assert.Equal("https://api.deepseek.com/chat/completions",
            handler.UltimaRichiesta!.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.UltimaRichiesta.Headers.Authorization!.Scheme);
        Assert.Contains("visura camerale", handler.UltimoCorpo);
        Assert.Contains("json_object", handler.UltimoCorpo);
        Assert.Contains("\"thinking\":{\"type\":\"disabled\"}", handler.UltimoCorpo);
    }

    [Fact]
    public async Task Compatibile_Gemini_Primario_UsaEndpointModelloEVistaGoogle()
    {
        var handler = new HandlerFinto(RispostaChat(
            """{"campi":[{"nome":"comune","valore":"Milano","confidenza":0.9}]}"""));
        var ai = new EstrattoreAiCompatibile(new HttpClient(handler), new AiOptions
        {
            Provider = "Gemini",
            ApiKey = "chiave-google-prova",
            ProviderCloudApprovato = true,
        });

        Assert.True(ai.Attivo);
        Assert.True(ai.VedeImmagini);
        Assert.Equal("gemini-3.7-flash", ai.Modello);
        Assert.Equal("gemini-3.7-flash", ai.ModelloVisione);

        var proposta = await ai.EstraiAsync("visura", "testo", ["comune"]);

        Assert.Equal("Milano", proposta!.Campi[0].Valore);
        Assert.Equal(
            "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions",
            handler.UltimaRichiesta!.RequestUri!.ToString());
        Assert.Equal("chiave-google-prova", handler.UltimaRichiesta.Headers.Authorization!.Parameter);
        Assert.True(handler.UltimaRichiesta.Headers.Contains("x-goog-api-client"));
        Assert.Contains("gemini-3.7-flash", handler.UltimoCorpo);
    }

    [Fact]
    public async Task Compatibile_DeepSeekFallisce_RiprovaUnaVoltaConGemini()
    {
        var handler = new HandlerSequenza(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            RispostaChat(
                """{"campi":[{"nome":"comune","valore":"Roma","confidenza":0.9}]}"""));
        var ai = new EstrattoreAiCompatibile(new HttpClient(handler), new AiOptions
        {
            Provider = "DeepSeek",
            ApiKey = "chiave-deepseek-prova",
            UsaGeminiComeFallback = true,
            GeminiApiKey = "chiave-gemini-prova",
            ProviderCloudApprovato = true,
        });

        var proposta = await ai.EstraiAsync("visura", "testo", ["comune"]);

        Assert.Equal("Roma", proposta!.Campi[0].Valore);
        Assert.Equal(2, handler.Richieste.Count);
        Assert.Equal("https://api.deepseek.com/chat/completions",
            handler.Richieste[0].Indirizzo!.ToString());
        Assert.Equal(
            "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions",
            handler.Richieste[1].Indirizzo!.ToString());
        Assert.True(handler.Richieste[1].HeaderGoogle);
    }

    [Fact]
    public async Task Compatibile_Immagine_UsaDirettamenteGeminiSenzaInviareADeepSeek()
    {
        var handler = new HandlerSequenza(RispostaChat(
            """{"campi":[{"nome":"partitaIva","valore":"01234567897","confidenza":0.95}]}"""));
        var ai = new EstrattoreAiCompatibile(new HttpClient(handler), new AiOptions
        {
            Provider = "DeepSeek",
            ApiKey = "chiave-deepseek-prova",
            UsaGeminiComeFallback = true,
            GeminiApiKey = "chiave-gemini-prova",
            ProviderCloudApprovato = true,
        });

        var proposta = await ai.EstraiDaImmagineAsync(
            "visura", [1, 2, 3], "image/jpeg", ["partitaIva"]);

        Assert.Equal("01234567897", proposta!.Campi[0].Valore);
        var richiesta = Assert.Single(handler.Richieste);
        Assert.Equal(
            "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions",
            richiesta.Indirizzo!.ToString());
        Assert.True(richiesta.HeaderGoogle);
        Assert.Contains("data:image/jpeg;base64,", richiesta.Corpo);
    }

    [Fact]
    public async Task Compatibile_ErroreDelProvider_RestituisceNull_SenzaLanciare()
    {
        var handler = new HandlerFinto(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var ai = new EstrattoreAiCompatibile(new HttpClient(handler),
            new AiOptions { Provider = "Glm", ApiKey = "chiave", ProviderCloudApprovato = true });
        Assert.Null(await ai.EstraiAsync("visura camerale", "testo", ["comune"]));
    }

    [Fact]
    public void Compatibile_Generico_SenzaBaseUrl_ErroreDiConfigurazioneChiaro()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new EstrattoreAiCompatibile(
            new HttpClient(new HandlerFinto(RispostaChat("{}"))),
            new AiOptions { Provider = "Compatibile" }));
        Assert.Contains("BaseUrl", ex.Message);
    }

    [Fact]
    public void EstraiContenuto_LeggeChoices() =>
        Assert.Equal("ciao", EstrattoreAiCompatibile.EstraiContenuto(
            """{"choices":[{"message":{"role":"assistant","content":"ciao"}}]}"""));

    // ---- Percorso "vista": il modello legge la foto direttamente ----

    [Fact]
    public void Interpreta_TolleraLeCornici_MarkdownDeiModelliVision()
    {
        var proposta = PropostaAiJson.Interpreta(
            "```json\n{\"campi\":[{\"nome\":\"comune\",\"valore\":\"Milano\",\"confidenza\":0.9}]}\n```");
        Assert.NotNull(proposta);
        Assert.Equal("Milano", proposta.Campi[0].Valore);
    }

    [Fact]
    public async Task Compatibile_Glm_HaLaVistaDiDefault_EInviaLaFoto()
    {
        var handler = new HandlerFinto(RispostaChat(
            """{"campi":[{"nome":"partitaIva","valore":"01234567897","confidenza":0.95}]}"""));
        var ai = new EstrattoreAiCompatibile(new HttpClient(handler),
            new AiOptions { Provider = "Glm", ApiKey = "chiave", ProviderCloudApprovato = true });

        Assert.True(ai.VedeImmagini);
        Assert.Equal("glm-4v-flash", ai.ModelloVisione);

        var proposta = await ai.EstraiDaImmagineAsync(
            "visura camerale", [1, 2, 3], "image/jpeg", ["partitaIva"]);

        Assert.NotNull(proposta);
        Assert.Equal("01234567897", proposta.Campi[0].Valore);
        Assert.Contains("glm-4v-flash", handler.UltimoCorpo);
        Assert.Contains("data:image/jpeg;base64,", handler.UltimoCorpo);
        Assert.Contains("image_url", handler.UltimoCorpo);
    }

    [Fact]
    public async Task Compatibile_DeepSeek_NonHaLaVista()
    {
        var handler = new HandlerFinto(RispostaChat("{}"));
        var ai = new EstrattoreAiCompatibile(new HttpClient(handler),
            new AiOptions { Provider = "DeepSeek", ApiKey = "chiave", ProviderCloudApprovato = true });
        Assert.False(ai.VedeImmagini);
        Assert.Null(await ai.EstraiDaImmagineAsync("visura", [1], "image/png", ["cap"]));
    }

    [Fact]
    public void Compatibile_Ollama_VistaConModelloEsplicito()
    {
        var ai = new EstrattoreAiCompatibile(
            new HttpClient(new HandlerFinto(RispostaChat("{}"))),
            new AiOptions
            {
                Provider = "Compatibile",
                BaseUrl = "http://localhost:11434/v1",
                Modello = "qwen3:14b",
                ModelloVisione = "qwen2.5vl:7b",
                SoloLocale = true,
                GateLocaleG1Verificato = true,
            });
        Assert.True(ai.VedeImmagini);
        Assert.Equal("qwen2.5vl:7b", ai.ModelloVisione);
    }

    // ---- Contatore consumi ("AI inclusa nel canone"): limite mensile per tenant ----

    [Fact]
    public async Task ConsumoAi_ContaPerTenant_EBloccaAlLimite()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        conn.Open();
        await using var db = new Paghe.Persistence.PagheDbContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Paghe.Persistence.PagheDbContext>()
                .UseSqlite(conn).Options);
        db.Database.EnsureCreated();

        var svc = new Paghe.Persistence.ConsumoAiService(db);
        var studioA = Guid.NewGuid();
        var studioB = Guid.NewGuid();

        // Limite 2: le prime due passano, la terza è bloccata.
        Assert.True(await svc.ProvaConsumaAsync(studioA, limiteMensile: 2));
        Assert.True(await svc.ProvaConsumaAsync(studioA, limiteMensile: 2));
        Assert.False(await svc.ProvaConsumaAsync(studioA, limiteMensile: 2));
        Assert.Equal(2, await svc.ConsumoDelMeseAsync(studioA));

        // Il limite è per studio: un altro tenant non è toccato.
        Assert.True(await svc.ProvaConsumaAsync(studioB, limiteMensile: 2));
        Assert.Equal(1, await svc.ConsumoDelMeseAsync(studioB));

        // Limite 0 = illimitato (es. AI locale): conteggia ma non blocca mai.
        Assert.True(await svc.ProvaConsumaAsync(studioA, limiteMensile: 0));
        Assert.Equal(3, await svc.ConsumoDelMeseAsync(studioA));
    }
}
