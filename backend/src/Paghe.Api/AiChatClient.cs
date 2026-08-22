using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Paghe.Api;

/// <summary>
/// Un messaggio della conversazione, nel formato OpenAI-compatibile usato da DeepSeek
/// (e da GLM, Ollama, qualsiasi endpoint /chat/completions). Viaggia anche verso la SPA:
/// il client rimanda indietro la storia così com'è, il server non tiene sessioni in memoria.
/// </summary>
public sealed class MessaggioChatAi
{
    /// <summary>"system" | "user" | "assistant" | "tool".</summary>
    public string Ruolo { get; set; } = "user";

    public string? Contenuto { get; set; }

    /// <summary>Strumenti che l'assistente ha deciso di usare in questo turno.</summary>
    public List<ChiamataStrumentoAi>? Chiamate { get; set; }

    /// <summary>Per i messaggi "tool": id della chiamata a cui questo risultato risponde.</summary>
    public string? ChiamataId { get; set; }
}

/// <summary>Una chiamata a strumento decisa dal modello (function calling).</summary>
public sealed class ChiamataStrumentoAi
{
    public string Id { get; set; } = "";
    public string Nome { get; set; } = "";
    public string ArgomentiJson { get; set; } = "{}";
}

/// <summary>Strumento offerto al modello: nome, descrizione e schema JSON dei parametri.</summary>
public sealed record StrumentoAi(string Nome, string Descrizione, object Parametri, bool Scrittura);

/// <summary>Risposta di un turno: testo per l'utente e/o strumenti da eseguire.</summary>
public sealed record RispostaChatAi(string? Testo, IReadOnlyList<ChiamataStrumentoAi> Chiamate);

/// <summary>
/// Chat con un modello linguistico su endpoint OpenAI-compatibile (DeepSeek di default),
/// con function calling. È il canale dell'assistente conversazionale: a differenza di
/// <see cref="IEstrattoreAiDocumenti"/> (che estrae campi da un documento) qui il modello
/// dialoga e può chiedere al programma di leggere dati o di preparare un'operazione.
/// Le operazioni che scrivono non partono mai da qui: le conferma l'utente.
/// </summary>
public sealed class AiChatClient
{
    private readonly HttpClient _http;
    private readonly AiOptions _opzioni;

    public AiChatClient(HttpClient http, AiOptions opzioni)
    {
        _http = http;
        _opzioni = opzioni;

        var (baseUrlDefault, modelloDefault) = DefaultProvider(opzioni.Provider);
        string baseUrl = string.IsNullOrWhiteSpace(opzioni.BaseUrl) ? baseUrlDefault : opzioni.BaseUrl;
        Modello = string.IsNullOrWhiteSpace(opzioni.ModelloChat)
            ? (string.IsNullOrWhiteSpace(opzioni.Modello) ? modelloDefault : opzioni.Modello!)
            : opzioni.ModelloChat!;

        // Il costruttore non deve mai far fallire l'avvio: se la configurazione non basta,
        // la chat resta spenta e l'endpoint lo spiega in italiano all'utente. Senza chiave
        // un provider remoto risponderebbe 401 a ogni messaggio: meglio dire subito che
        // manca la chiave (un modello in locale, invece, non ne vuole nessuna).
        Attivo = baseUrl.Length > 0 && Modello.Length > 0
            && !opzioni.Provider.Equals("Spento", StringComparison.OrdinalIgnoreCase)
            && !opzioni.ChiaveMancante
            && opzioni.EndpointConsentito(baseUrl);
        if (!Attivo) return;

        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(120);
        if (!string.IsNullOrWhiteSpace(opzioni.ApiKey))
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", opzioni.ApiKey);
    }

    public string Modello { get; }

    /// <summary>Falso quando il provider non è configurato: la chat lo dice, non esplode.</summary>
    public bool Attivo { get; }

    /// <summary>Endpoint e modello di default dei provider conosciuti.</summary>
    private static (string BaseUrl, string Modello) DefaultProvider(string provider) =>
        provider.ToLowerInvariant() switch
        {
            // deepseek-chat e deepseek-reasoner sono deprecati dal 24 luglio 2026.
            // Aurora richiede il function calling, disponibile sul modello Flash corrente.
            "deepseek" => ("https://api.deepseek.com", "deepseek-v4-flash"),
            "glm" => ("https://open.bigmodel.cn/api/paas/v4", "glm-4-flash"),
            _ => ("", ""),
        };

    /// <summary>Un turno di conversazione: manda la storia + gli strumenti, legge la risposta.</summary>
    public async Task<RispostaChatAi> ParlaAsync(
        IReadOnlyList<MessaggioChatAi> messaggi, IReadOnlyList<StrumentoAi> strumenti,
        CancellationToken ct = default)
    {
        if (!Attivo)
            throw new InvalidOperationException(
                "Aurora non è disponibile: controlla che il modello locale sia configurato e avviato.");

        var corpo = new Dictionary<string, object?>
        {
            ["model"] = Modello,
            ["messages"] = messaggi.Select(Serializza).ToArray(),
            ["temperature"] = 0.2,
        };
        if (strumenti.Count > 0)
        {
            corpo["tools"] = strumenti.Select(s => new
            {
                type = "function",
                function = new { name = s.Nome, description = s.Descrizione, parameters = s.Parametri },
            }).ToArray();
            corpo["tool_choice"] = "auto";
        }
        else
        {
            // Modalità spiegazione: il modello non deve neppure vedere il catalogo strumenti.
            corpo["tool_choice"] = "none";
        }

        // V4 attiva il thinking per default. Nei giri con strumenti il provider obbliga
        // a rimandare il reasoning_content integrale; Aurora non lo conserva né lo espone
        // alla SPA. La modalità non-thinking mantiene il protocollo semplice e prevedibile
        // del vecchio deepseek-chat e impedisce il 400 al secondo giro dello strumento.
        if (_opzioni.Provider.Equals("DeepSeek", StringComparison.OrdinalIgnoreCase))
            corpo["thinking"] = new { type = "disabled" };

        using var richiesta = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json"),
        };
        using var risposta = await _http.SendAsync(richiesta, ct);
        string json = await risposta.Content.ReadAsStringAsync(ct);
        if (!risposta.IsSuccessStatusCode)
            throw new InvalidOperationException(DescriviErrore((int)risposta.StatusCode, json));

        return Interpreta(json);
    }

    /// <summary>Messaggio d'errore in italiano, senza rigirare all'utente il JSON del provider.</summary>
    private static string DescriviErrore(int stato, string corpo) => stato switch
    {
        401 or 403 => "Il provider AI ha rifiutato la chiave (Ai:ApiKey): controlla che sia valida e attiva.",
        402 => "Credito del provider AI esaurito: ricarica il conto DeepSeek.",
        429 => "Troppe richieste al provider AI in questo momento: riprova tra qualche secondo.",
        >= 500 => "Il provider AI non risponde in questo momento: riprova tra poco.",
        _ => $"Il provider AI ha risposto con errore {stato}. "
            + (corpo.Length > 200 ? corpo[..200] : corpo),
    };

    /// <summary>Traduce un messaggio interno nel formato dell'API. Puro: testabile.</summary>
    public static object Serializza(MessaggioChatAi m) => m.Ruolo switch
    {
        "tool" => (object)new { role = "tool", tool_call_id = m.ChiamataId ?? "", content = m.Contenuto ?? "" },
        "assistant" when m.Chiamate is { Count: > 0 } => new
        {
            role = "assistant",
            content = m.Contenuto ?? "",
            tool_calls = m.Chiamate.Select(c => new
            {
                id = c.Id,
                type = "function",
                function = new { name = c.Nome, arguments = c.ArgomentiJson },
            }).ToArray(),
        },
        _ => new { role = m.Ruolo, content = m.Contenuto ?? "" },
    };

    /// <summary>Legge testo e tool_calls dalla risposta OpenAI-compatibile. Puro: testabile.</summary>
    public static RispostaChatAi Interpreta(string rispostaJson)
    {
        var chiamate = new List<ChiamataStrumentoAi>();
        string? testo = null;
        try
        {
            using var doc = JsonDocument.Parse(rispostaJson);
            if (!doc.RootElement.TryGetProperty("choices", out var scelte)
                || scelte.ValueKind != JsonValueKind.Array || scelte.GetArrayLength() == 0)
                return new RispostaChatAi(null, chiamate);

            if (!scelte[0].TryGetProperty("message", out var messaggio))
                return new RispostaChatAi(null, chiamate);

            if (messaggio.TryGetProperty("content", out var contenuto)
                && contenuto.ValueKind == JsonValueKind.String)
                testo = contenuto.GetString();

            if (messaggio.TryGetProperty("tool_calls", out var tools)
                && tools.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in tools.EnumerateArray())
                {
                    if (!t.TryGetProperty("function", out var f)) continue;
                    chiamate.Add(new ChiamataStrumentoAi
                    {
                        Id = t.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                        Nome = f.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                        ArgomentiJson = f.TryGetProperty("arguments", out var a)
                            ? (a.GetString() ?? "{}") : "{}",
                    });
                }
            }
        }
        catch (JsonException)
        {
            return new RispostaChatAi(null, chiamate);
        }
        return new RispostaChatAi(string.IsNullOrWhiteSpace(testo) ? null : testo, chiamate);
    }
}
