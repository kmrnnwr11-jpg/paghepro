using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Paghe.Core.Ai;

namespace Paghe.Api;

/// <summary>
/// Configurazione del livello AI (sezione "Ai"). Il provider è a scelta dell'utente:
/// "Spento" (default, nessuna chiamata esterna), "Claude" (SDK ufficiale Anthropic),
/// "DeepSeek", "Gemini", "Glm" o "Compatibile" (qualsiasi endpoint OpenAI-compatibile, anche
/// Ollama in locale, impostando BaseUrl e Modello).
/// </summary>
public sealed class AiOptions
{
    /// <summary>"Spento" | "Claude" | "DeepSeek" | "Gemini" | "Glm" | "Compatibile".</summary>
    public string Provider { get; set; } = "Spento";

    /// <summary>Chiave API del provider. Per Claude vuota = variabile d'ambiente ANTHROPIC_API_KEY.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Chiave Google Gemini separata. Quando <see cref="UsaGeminiComeFallback"/> è attivo,
    /// DeepSeek resta il provider principale e Gemini interviene soltanto se il primo non
    /// risponde; per le immagini, che DeepSeek non legge, Gemini viene usato direttamente.
    /// </summary>
    public string? GeminiApiKey { get; set; }

    /// <summary>Abilita Gemini come riserva del provider principale OpenAI-compatibile.</summary>
    public bool UsaGeminiComeFallback { get; set; }

    /// <summary>Modello Gemini per chat, estrazione testo e immagini; vuoto = default corrente.</summary>
    public string? ModelloGemini { get; set; }

    /// <summary>Endpoint base per i provider OpenAI-compatibili (per DeepSeek/GLM c'è un default).</summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Profilo personale: vieta provider remoti. Il runtime Ollama resta bloccato finché il gate
    /// G1 non prova socket locale, zero egress, vault e ripristino cifrato.
    /// </summary>
    public bool SoloLocale { get; set; }

    /// <summary>
    /// Interruttore esplicito del percorso G1-SaaS. Rimane falso finché titolare e supporto
    /// legale non hanno approvato DPA, retention e residenza del provider selezionato.
    /// </summary>
    public bool ProviderCloudApprovato { get; set; }

    /// <summary>Il runtime locale può partire soltanto dopo la prova tecnica G1.</summary>
    public bool GateLocaleG1Verificato { get; set; }

    /// <summary>
    /// Gate di autonomia del runtime. Il valore di produzione resta A2; A3 richiede inoltre
    /// che tenant e utente compaiano entrambi nelle allowlist immutabili qui sotto.
    /// </summary>
    public string GateAurora { get; set; } = "A2";

    /// <summary>
    /// Coppie esatte "tenant-guid:utente-guid" ammesse al collaudo. Due liste separate
    /// produrrebbero un pericoloso prodotto cartesiano; email e domini non autorizzano mai.
    /// </summary>
    public string[] CoppieShadowA3 { get; set; } = [];

    /// <summary>Off | DryRun (sole proposte) | Confirmed (proposte, conferma e undo).</summary>
    public string ModalitaA3 { get; set; } = "Off";

    /// <summary>
    /// Strada A3 del consenso pieno: chi ha concesso il consenso ad Aurora (tabella
    /// ConsensiAurora, sempre revocabile) può ricevere proposte di modifica con conferma
    /// e undo anche senza coppie di collaudo in configurazione. False = kill-switch.
    /// </summary>
    public bool A3ConConsensoPieno { get; set; } = true;

    /// <summary>
    /// Host HTTPS ammessi per Provider=Compatibile. I provider conosciuti hanno una allowlist
    /// incorporata; una BaseUrl configurabile non diventa mai un canale egress arbitrario.
    /// </summary>
    public string[] HostCloudConsentiti { get; set; } = [];

    /// <summary>Modello da usare; vuoto = default del provider.</summary>
    public string? Modello { get; set; }

    /// <summary>
    /// Modello dell'assistente a chat (function calling). Vuoto = si usa <see cref="Modello"/>
    /// e, se manca anche quello, il default del provider (DeepSeek → deepseek-v4-flash).
    /// </summary>
    public string? ModelloChat { get; set; }

    /// <summary>
    /// Modello con la "vista" per leggere foto/scansioni. Vuoto = default del provider
    /// (GLM → glm-4v-flash; Claude vede già con il modello principale; DeepSeek → niente
    /// vista). Per Ollama in locale: es. "qwen2.5vl:7b".
    /// </summary>
    public string? ModelloVisione { get; set; }

    /// <summary>Confidenza minima (0-1) perché un valore proposto dall'AI venga usato.</summary>
    public decimal SogliaConfidenza { get; set; } = 0.6m;

    /// <summary>
    /// Il provider è remoto ma la chiave non c'è: l'interruttore può essere acceso in
    /// configurazione, però ogni chiamata prenderebbe 401. Meglio dirlo che fingersi attivi.
    /// Un modello sulla stessa macchina (Ollama e simili) non chiede chiavi.
    /// </summary>
    public bool ChiaveMancante
    {
        get
        {
            var configurazione = CatalogoProviderAiCompatibili.Primario(this);
            return configurazione is not null
                && string.IsNullOrWhiteSpace(configurazione.ApiKey)
                && !ÈEndpointOllamaLocale(configurazione.BaseUrl);
        }
    }

    /// <summary>La riserva Gemini è richiesta ma non ha ancora una chiave utilizzabile.</summary>
    public bool ChiaveGeminiMancante => UsaGeminiComeFallback
        && !Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase)
        && string.IsNullOrWhiteSpace(GeminiApiKey);

    /// <summary>
    /// Il piano vincolante vieta di accendere Ollama prima della prova G1. È un blocco nel codice,
    /// non un flag di configurazione aggirabile accidentalmente.
    /// </summary>
    public bool BloccatoDaGateG1 =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri)
        && uri.IsLoopback && !GateLocaleG1Verificato;

    /// <summary>
    /// Validazione fail-closed dell'unico endpoint locale ammesso. Evita controlli per sottostringa
    /// che accetterebbero host come localhost.example o redirect verso la rete.
    /// </summary>
    public static bool ÈEndpointOllamaLocale(string? baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)) return false;
        bool hostLoopback = uri.Host.Equals("127.0.0.1", StringComparison.Ordinal)
            || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("::1", StringComparison.Ordinal)
            || uri.Host.Equals("[::1]", StringComparison.Ordinal);
        string path = uri.AbsolutePath.TrimEnd('/');
        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && hostLoopback
            && uri.Port == 11434
            && uri.UserInfo.Length == 0
            && uri.Query.Length == 0
            && uri.Fragment.Length == 0
            && path.Equals("/v1", StringComparison.Ordinal);
    }

    /// <summary>
    /// Controlla l'endpoint effettivo (incluso il default del provider) con regole fail-closed.
    /// </summary>
    public bool EndpointConsentito(string? baseUrl) => EndpointConsentito(Provider, baseUrl);

    /// <summary>Valida un endpoint rispetto al provider che lo userà, incluso il fallback.</summary>
    public bool EndpointConsentito(string provider, string? baseUrl)
    {
        if (provider.Equals("Spento", StringComparison.OrdinalIgnoreCase)) return false;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            return false;

        if (uri.IsLoopback)
            return SoloLocale && GateLocaleG1Verificato && ÈEndpointOllamaLocale(baseUrl);

        if (SoloLocale || !ProviderCloudApprovato
            || !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || uri.Port != 443)
            return false;

        string host = uri.IdnHost.ToLowerInvariant();
        return provider.ToLowerInvariant() switch
        {
            "deepseek" => host == "api.deepseek.com",
            "gemini" => host == "generativelanguage.googleapis.com",
            "glm" => host == "open.bigmodel.cn",
            "claude" => host == "api.anthropic.com",
            "compatibile" => HostCloudConsentiti.Any(h =>
                string.Equals(h?.Trim().TrimEnd('.'), host, StringComparison.OrdinalIgnoreCase)),
            _ => false,
        };
    }

    /// <summary>Blocca all'avvio cloud non approvato e runtime locale prima del gate G1.</summary>
    public void ValidaProfiloLocale()
    {
        if (Provider.Equals("Spento", StringComparison.OrdinalIgnoreCase)) return;
        if (Provider.Equals("Claude", StringComparison.OrdinalIgnoreCase))
        {
            if (SoloLocale)
                throw new InvalidOperationException(
                    "Profilo AI solo locale: i provider cloud sono vietati. Usare Provider=Spento "
                    + "finché il runtime locale G1 non è pronto.");
            if (!ProviderCloudApprovato)
                throw new InvalidOperationException(
                    "Provider AI cloud non approvato: completare il gate G1-SaaS (DPA, egress e trasparenza) "
                    + "oppure usare Provider=Spento.");
            if (UsaGeminiComeFallback)
                throw new InvalidOperationException(
                    "Il fallback Gemini è disponibile con i provider OpenAI-compatibili, non con Claude.");
            return;
        }
        foreach (var configurazione in CatalogoProviderAiCompatibili.Tutte(this))
        {
            if (!Uri.TryCreate(configurazione.BaseUrl, UriKind.Absolute, out var uri))
                throw new InvalidOperationException(
                    $"Endpoint AI non valido per il provider {configurazione.Provider}.");
            if (uri.IsLoopback)
            {
                if (!SoloLocale || !GateLocaleG1Verificato
                    || !ÈEndpointOllamaLocale(configurazione.BaseUrl))
                    throw new InvalidOperationException(
                        "Aurora locale resta sospesa: endpoint canonico e gate di isolamento G1 non verificati.");
                continue;
            }
            if (SoloLocale)
                throw new InvalidOperationException(
                    "Profilo AI solo locale: i provider cloud sono vietati. Usare Provider=Spento "
                    + "finché il runtime locale G1 non è pronto.");
            if (!ProviderCloudApprovato)
                throw new InvalidOperationException(
                    "Provider AI cloud non approvato: completare il gate G1-SaaS (DPA, egress e trasparenza) "
                    + "oppure usare Provider=Spento.");
            if (!EndpointConsentito(configurazione.Provider, configurazione.BaseUrl))
                throw new InvalidOperationException(
                    $"Endpoint AI non autorizzato per il provider {configurazione.Provider}.");
        }
    }

    /// <summary>
    /// Limite mensile di chiamate AI per studio (tenant), per il modello "AI inclusa nel
    /// canone" con chiave centrale. 0 o negativo = illimitato (es. AI locale/Ollama).
    /// </summary>
    public int LimiteMensileChiamate { get; set; } = 300;

    /// <summary>Istruzioni comuni a tutti i provider: solo estrazione, mai inventare.</summary>
    public const string IstruzioniEstrazione =
        "Leggi documenti amministrativi italiani ed estrai SOLO i campi richiesti. "
        + "Non inventare mai: se un campo non è presente nel testo, valore null e confidenza 0. "
        + "Riporta i valori esattamente come scritti nel documento (senza etichette). "
        + "La confidenza è un numero 0-1 su quanto sei sicuro del valore letto. "
        + "Rispondi in JSON: {\"campi\":[{\"nome\":...,\"valore\":...,\"confidenza\":...}]}.";

    /// <summary>Limite prudenziale sul testo inviato (una visura sta ampiamente sotto).</summary>
    public const int MassimoCaratteriDocumento = 100_000;

    /// <summary>Compone il messaggio utente comune a tutti i provider.</summary>
    public static string MessaggioUtente(
        string tipoDocumento, string testoDocumento, IReadOnlyList<string> campiRichiesti)
    {
        string testo = testoDocumento.Length > MassimoCaratteriDocumento
            ? testoDocumento[..MassimoCaratteriDocumento]
            : testoDocumento;
        return $"Tipo documento: {tipoDocumento}\n"
            + $"Campi da estrarre: {string.Join(", ", campiRichiesti)}\n\n"
            + $"Testo del documento:\n{testo}";
    }
}

/// <summary>
/// Lettore AI di documenti basato su Claude (SDK ufficiale Anthropic). Riceve il testo già
/// estratto (PdfPig/OCR) e propone i valori dei campi richiesti con una confidenza per campo,
/// in JSON garantito dallo structured output. Solo lettura/proposta: niente calcoli, niente
/// salvataggi — la conferma resta all'utente (regola architetturale del prodotto).
/// </summary>
public sealed class EstrattoreAiAnthropic : IEstrattoreAiDocumenti
{
    private readonly AnthropicClient _client;
    private readonly AiOptions _opzioni;

    public EstrattoreAiAnthropic(AiOptions opzioni)
    {
        _opzioni = opzioni;
        _client = string.IsNullOrWhiteSpace(opzioni.ApiKey)
            ? new AnthropicClient() // legge ANTHROPIC_API_KEY dall'ambiente
            : new AnthropicClient { ApiKey = opzioni.ApiKey };
    }

    public bool Attivo => _opzioni.Provider.Equals("Claude", StringComparison.OrdinalIgnoreCase)
        && _opzioni.EndpointConsentito("https://api.anthropic.com");

    /// <summary>Tutti i modelli Claude attuali leggono immagini con il modello principale.</summary>
    public bool VedeImmagini => true;

    public async Task<PropostaAi?> EstraiDaImmagineAsync(
        string tipoDocumento, byte[] immagine, string mimeType,
        IReadOnlyList<string> campiRichiesti, CancellationToken ct = default)
    {
        if (!Attivo || immagine.Length == 0 || campiRichiesti.Count == 0)
            return null;

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = string.IsNullOrWhiteSpace(_opzioni.Modello) ? "claude-opus-4-8" : _opzioni.Modello,
            MaxTokens = 4096,
            Thinking = new ThinkingConfigAdaptive(),
            System = AiOptions.IstruzioniEstrazione,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new ImageBlockParam
                        {
                            Source = new Base64ImageSource
                            {
                                Data = Convert.ToBase64String(immagine),
                                MediaType = mimeType,
                            },
                        },
                        new TextBlockParam
                        {
                            Text = $"Tipo documento: {tipoDocumento}\n"
                                + $"Campi da estrarre: {string.Join(", ", campiRichiesti)}\n"
                                + "Leggi il documento nell'immagine allegata.",
                        },
                    },
                },
            ],
            OutputConfig = new OutputConfig
            {
                Format = new JsonOutputFormat { Schema = SchemaProposta() },
            },
        });

        if (response.StopReason == "refusal")
            return null;

        string? json = response.Content
            .Select(b => b.Value).OfType<TextBlock>()
            .Select(t => t.Text)
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
        return json is null ? null : PropostaAiJson.Interpreta(json);
    }

    public async Task<PropostaAi?> EstraiAsync(
        string tipoDocumento, string testoDocumento,
        IReadOnlyList<string> campiRichiesti, CancellationToken ct = default)
    {
        if (!Attivo || string.IsNullOrWhiteSpace(testoDocumento) || campiRichiesti.Count == 0)
            return null;

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = string.IsNullOrWhiteSpace(_opzioni.Modello) ? "claude-opus-4-8" : _opzioni.Modello,
            MaxTokens = 4096,
            Thinking = new ThinkingConfigAdaptive(),
            System = AiOptions.IstruzioniEstrazione,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = AiOptions.MessaggioUtente(tipoDocumento, testoDocumento, campiRichiesti),
                },
            ],
            OutputConfig = new OutputConfig
            {
                Format = new JsonOutputFormat { Schema = SchemaProposta() },
            },
        });

        if (response.StopReason == "refusal")
            return null;

        string? json = response.Content
            .Select(b => b.Value).OfType<TextBlock>()
            .Select(t => t.Text)
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
        return json is null ? null : PropostaAiJson.Interpreta(json);
    }

    /// <summary>Schema JSON dello structured output: {"campi":[{nome, valore, confidenza}]}.</summary>
    private static Dictionary<string, JsonElement> SchemaProposta() => new()
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["properties"] = JsonSerializer.SerializeToElement(new
        {
            campi = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        nome = new { type = "string" },
                        valore = new { type = new[] { "string", "null" } },
                        confidenza = new { type = "number" },
                    },
                    required = new[] { "nome", "valore", "confidenza" },
                    additionalProperties = false,
                },
            },
        }),
        ["required"] = JsonSerializer.SerializeToElement(new[] { "campi" }),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
    };
}
