using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    /// <summary>
    /// Firma opaca ("thought_signature") che Gemini allega a ogni sua chiamata e pretende
    /// indietro al giro successivo, altrimenti risponde 400. Resta nel ciclo del server:
    /// non esce verso la SPA e non viene mai rimandata a un provider diverso.
    /// </summary>
    [JsonIgnore]
    public string? FirmaPensiero { get; set; }
}

/// <summary>Strumento offerto al modello: nome, descrizione e schema JSON dei parametri.</summary>
public sealed record StrumentoAi(string Nome, string Descrizione, object Parametri, bool Scrittura);

/// <summary>Risposta di un turno: testo per l'utente e/o strumenti da eseguire.</summary>
public sealed record RispostaChatAi(
    string? Testo, IReadOnlyList<ChiamataStrumentoAi> Chiamate, int? TokenTotali = null);

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
    private readonly IReadOnlyList<ConfigurazioneProviderAiCompatibile> _provider;

    public AiChatClient(HttpClient http, AiOptions opzioni)
    {
        _http = http;
        _provider = CatalogoProviderAiCompatibili.AttivePerChat(opzioni);

        // Il costruttore non deve mai far fallire l'avvio: se la configurazione non basta,
        // la chat resta spenta e l'endpoint lo spiega in italiano all'utente. Senza chiave
        // un provider remoto risponderebbe 401 a ogni messaggio: meglio dire subito che
        // manca la chiave (un modello in locale, invece, non ne vuole nessuna).
        Attivo = _provider.Count > 0;
        if (!Attivo) return;

        _http.Timeout = TimeSpan.FromSeconds(120);
    }

    public string Modello => _provider.FirstOrDefault()?.ModelloChat ?? "";

    public string Provider => _provider.FirstOrDefault()?.Provider ?? "";

    public string? ProviderSecondario => _provider.Skip(1).FirstOrDefault()?.Provider;

    public string? ModelloSecondario => _provider.Skip(1).FirstOrDefault()?.ModelloChat;

    /// <summary>Falso quando il provider non è configurato: la chat lo dice, non esplode.</summary>
    public bool Attivo { get; }

    /// <summary>Un turno di conversazione: manda la storia + gli strumenti, legge la risposta.</summary>
    public async Task<RispostaChatAi> ParlaAsync(
        IReadOnlyList<MessaggioChatAi> messaggi, IReadOnlyList<StrumentoAi> strumenti,
        CancellationToken ct = default)
    {
        if (!Attivo)
            throw new InvalidOperationException(
                "Aurora non è disponibile: controlla che il modello locale sia configurato e avviato.");

        Exception? ultimoErrore = null;
        foreach (var provider in _provider)
        {
            try
            {
                return await InviaAsync(provider, messaggi, strumenti, ct);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                ultimoErrore = new InvalidOperationException(
                    $"Il provider {provider.Provider} non ha risposto entro il tempo previsto.");
            }
            catch (HttpRequestException ex)
            {
                ultimoErrore = new InvalidOperationException(
                    $"Il provider {provider.Provider} non è raggiungibile.", ex);
            }
            catch (InvalidOperationException ex)
            {
                ultimoErrore = ex;
            }
        }

        throw ultimoErrore ?? new InvalidOperationException("Nessun provider AI disponibile.");
    }

    private async Task<RispostaChatAi> InviaAsync(
        ConfigurazioneProviderAiCompatibile provider,
        IReadOnlyList<MessaggioChatAi> messaggi,
        IReadOnlyList<StrumentoAi> strumenti,
        CancellationToken ct)
    {

        var corpo = new Dictionary<string, object?>
        {
            ["model"] = provider.ModelloChat,
            ["messages"] = messaggi
                .Select(m => Serializza(m, provider.Provider.Equals(
                    "Gemini", StringComparison.OrdinalIgnoreCase)))
                .ToArray(),
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
        if (provider.Provider.Equals("DeepSeek", StringComparison.OrdinalIgnoreCase))
            corpo["thinking"] = new { type = "disabled" };

        using var richiesta = new HttpRequestMessage(
            HttpMethod.Post, Indirizzo(provider.BaseUrl, "chat/completions"))
        {
            Content = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrWhiteSpace(provider.ApiKey))
            richiesta.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        if (provider.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
            richiesta.Headers.TryAddWithoutValidation(
                "x-goog-api-client", "paghepro-openai-compatible/1.0");

        using var risposta = await _http.SendAsync(richiesta, ct);
        string json = await risposta.Content.ReadAsStringAsync(ct);
        if (!risposta.IsSuccessStatusCode)
            throw new InvalidOperationException(
                DescriviErrore(provider.Provider, (int)risposta.StatusCode, json));

        var interpretata = Interpreta(json);
        if (interpretata.Testo is null && interpretata.Chiamate.Count == 0)
            throw new InvalidOperationException(
                $"Il provider {provider.Provider} ha restituito una risposta non leggibile.");
        return interpretata;
    }

    /// <summary>Messaggio d'errore in italiano, senza rigirare all'utente il JSON del provider.</summary>
    private static string DescriviErrore(string provider, int stato, string corpo) => stato switch
    {
        401 or 403 => $"Il provider {provider} ha rifiutato la chiave: controlla che sia valida e attiva.",
        402 => $"Credito del provider {provider} esaurito: controlla il relativo account.",
        429 => $"Troppe richieste al provider {provider} in questo momento: riprova tra qualche secondo.",
        >= 500 => $"Il provider {provider} non risponde in questo momento: riprova tra poco.",
        _ => $"Il provider {provider} ha risposto con errore {stato}. "
            + (corpo.Length > 200 ? corpo[..200] : corpo),
    };

    private static Uri Indirizzo(string baseUrl, string percorso) =>
        new(new Uri(baseUrl.TrimEnd('/') + "/"), percorso);

    /// <summary>Legge la firma opaca di Gemini, se il provider l'ha allegata alla chiamata.</summary>
    private static string? FirmaPensiero(JsonElement chiamata) =>
        chiamata.TryGetProperty("extra_content", out var extra)
        && extra.ValueKind == JsonValueKind.Object
        && extra.TryGetProperty("google", out var google)
        && google.ValueKind == JsonValueKind.Object
        && google.TryGetProperty("thought_signature", out var firma)
        && firma.ValueKind == JsonValueKind.String
            ? firma.GetString()
            : null;

    /// <summary>Traduce un messaggio interno nel formato dell'API. Puro: testabile.</summary>
    public static object Serializza(MessaggioChatAi m, bool includiFirmaPensiero = false) => m.Ruolo switch
    {
        "tool" => (object)new { role = "tool", tool_call_id = m.ChiamataId ?? "", content = m.Contenuto ?? "" },
        "assistant" when m.Chiamate is { Count: > 0 } => new
        {
            role = "assistant",
            content = m.Contenuto ?? "",
            tool_calls = m.Chiamate.Select(c => SerializzaChiamata(c, includiFirmaPensiero)).ToArray(),
        },
        _ => new { role = m.Ruolo, content = m.Contenuto ?? "" },
    };

    /// <summary>La firma torna soltanto al provider che l'ha prodotta: agli altri è ignota.</summary>
    private static object SerializzaChiamata(ChiamataStrumentoAi c, bool includiFirmaPensiero) =>
        includiFirmaPensiero && !string.IsNullOrEmpty(c.FirmaPensiero)
            ? new
            {
                id = c.Id,
                type = "function",
                function = new { name = c.Nome, arguments = c.ArgomentiJson },
                extra_content = new { google = new { thought_signature = c.FirmaPensiero } },
            }
            : new { id = c.Id, type = "function", function = new { name = c.Nome, arguments = c.ArgomentiJson } };

    /// <summary>Legge testo e tool_calls dalla risposta OpenAI-compatibile. Puro: testabile.</summary>
    public static RispostaChatAi Interpreta(string rispostaJson)
    {
        var chiamate = new List<ChiamataStrumentoAi>();
        string? testo = null;
        int? tokenTotali = null;
        try
        {
            using var doc = JsonDocument.Parse(rispostaJson);
            // Consumo dichiarato dal provider: alimenta il SessionLog, non è mai obbligatorio.
            if (doc.RootElement.TryGetProperty("usage", out var uso)
                && uso.TryGetProperty("total_tokens", out var totale)
                && totale.TryGetInt32(out var interi))
                tokenTotali = interi;

            if (!doc.RootElement.TryGetProperty("choices", out var scelte)
                || scelte.ValueKind != JsonValueKind.Array || scelte.GetArrayLength() == 0)
                return new RispostaChatAi(null, chiamate, tokenTotali);

            if (!scelte[0].TryGetProperty("message", out var messaggio))
                return new RispostaChatAi(null, chiamate, tokenTotali);

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
                        FirmaPensiero = FirmaPensiero(t),
                    });
                }
            }
        }
        catch (JsonException)
        {
            return new RispostaChatAi(null, chiamate, tokenTotali);
        }
        return new RispostaChatAi(string.IsNullOrWhiteSpace(testo) ? null : testo, chiamate, tokenTotali);
    }
}
