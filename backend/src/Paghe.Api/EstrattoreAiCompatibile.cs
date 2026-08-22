using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Paghe.Core.Ai;

namespace Paghe.Api;

/// <summary>
/// Lettore AI di documenti per qualsiasi provider con API OpenAI-compatibile
/// (chat/completions): DeepSeek, GLM/Zhipu, OpenAI, Ollama in locale, ecc.
/// Il provider si sceglie in configurazione (sezione "Ai"); per DeepSeek e GLM
/// BaseUrl e modello hanno un default. Typed HttpClient: testabile con handler finto.
/// </summary>
public sealed class EstrattoreAiCompatibile : IEstrattoreAiDocumenti
{
    private readonly HttpClient _http;
    private readonly AiOptions _opzioni;

    public EstrattoreAiCompatibile(HttpClient http, AiOptions opzioni)
    {
        _http = http;
        _opzioni = opzioni;

        var (baseUrlDefault, modelloDefault, visioneDefault) = DefaultProvider(opzioni.Provider);
        string baseUrl = string.IsNullOrWhiteSpace(opzioni.BaseUrl) ? baseUrlDefault : opzioni.BaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException(
                "Ai:BaseUrl mancante: per il provider \"Compatibile\" indicare l'endpoint OpenAI-compatibile.");
        if (!opzioni.EndpointConsentito(baseUrl))
            throw new InvalidOperationException(
                "Endpoint AI non autorizzato dal gate G1/G1-SaaS o fuori dalla allowlist egress.");
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        Modello = string.IsNullOrWhiteSpace(opzioni.Modello) ? modelloDefault : opzioni.Modello!;
        if (string.IsNullOrWhiteSpace(Modello))
            throw new InvalidOperationException(
                "Ai:Modello mancante: per il provider \"Compatibile\" indicare il nome del modello.");
        ModelloVisione = string.IsNullOrWhiteSpace(opzioni.ModelloVisione)
            ? visioneDefault : opzioni.ModelloVisione;
        if (!string.IsNullOrWhiteSpace(opzioni.ApiKey))
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", opzioni.ApiKey);
    }

    public string Modello { get; }

    /// <summary>Modello con la vista; vuoto = il provider non legge immagini.</summary>
    public string? ModelloVisione { get; }

    /// <summary>Con l'interruttore acceso ma senza chiave si resta spenti: il provider direbbe 401.</summary>
    public bool Attivo => !_opzioni.ChiaveMancante
        && _http.BaseAddress is not null
        && _opzioni.EndpointConsentito(_http.BaseAddress.ToString());

    public bool VedeImmagini => !string.IsNullOrWhiteSpace(ModelloVisione);

    /// <summary>Endpoint, modello testo e modello vista di default per i provider conosciuti.</summary>
    private static (string BaseUrl, string Modello, string? Visione) DefaultProvider(string provider) =>
        provider.ToLowerInvariant() switch
        {
            // Il nome storico deepseek-chat è deprecato dal provider dal 24 luglio 2026.
            "deepseek" => ("https://api.deepseek.com", "deepseek-v4-flash", null),
            "glm" => ("https://open.bigmodel.cn/api/paas/v4", "glm-4-flash", "glm-4v-flash"),
            _ => ("", "", null),
        };

    public async Task<PropostaAi?> EstraiAsync(
        string tipoDocumento, string testoDocumento,
        IReadOnlyList<string> campiRichiesti, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(testoDocumento) || campiRichiesti.Count == 0)
            return null;

        var corpo = new Dictionary<string, object?>
        {
            ["model"] = Modello,
            ["messages"] = new object[]
            {
                new { role = "system", content = AiOptions.IstruzioniEstrazione },
                new
                {
                    role = "user",
                    content = AiOptions.MessaggioUtente(tipoDocumento, testoDocumento, campiRichiesti),
                },
            },
            // La maggior parte dei provider compatibili rispetta il JSON mode; in ogni
            // caso le istruzioni chiedono JSON e il parser tollera risposte non valide.
            ["response_format"] = new { type = "json_object" },
        };

        // L'estrazione è deterministica e in un solo giro: il thinking predefinito di V4
        // aumenterebbe latenza e consumo senza aggiungere garanzie sui campi estratti.
        if (_opzioni.Provider.Equals("DeepSeek", StringComparison.OrdinalIgnoreCase))
            corpo["thinking"] = new { type = "disabled" };

        return await InviaAsync(corpo, ct);
    }

    public async Task<PropostaAi?> EstraiDaImmagineAsync(
        string tipoDocumento, byte[] immagine, string mimeType,
        IReadOnlyList<string> campiRichiesti, CancellationToken ct = default)
    {
        if (!VedeImmagini || immagine.Length == 0 || campiRichiesti.Count == 0)
            return null;

        string dataUri = $"data:{mimeType};base64,{Convert.ToBase64String(immagine)}";
        var corpo = new
        {
            model = ModelloVisione,
            messages = new object[]
            {
                new { role = "system", content = AiOptions.IstruzioniEstrazione },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Tipo documento: {tipoDocumento}\n"
                                + $"Campi da estrarre: {string.Join(", ", campiRichiesti)}\n"
                                + "Leggi il documento nell'immagine allegata.",
                        },
                        new { type = "image_url", image_url = new { url = dataUri } },
                    },
                },
            },
            // Niente response_format: alcuni modelli con la vista non lo supportano;
            // le istruzioni chiedono JSON e il parser tollera cornici ```json```.
        };

        return await InviaAsync(corpo, ct);
    }

    private async Task<PropostaAi?> InviaAsync(object corpo, CancellationToken ct)
    {
        using var richiesta = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json"),
        };
        using var risposta = await _http.SendAsync(richiesta, ct);
        if (!risposta.IsSuccessStatusCode)
            return null;

        string json = await risposta.Content.ReadAsStringAsync(ct);
        string? contenuto = EstraiContenuto(json);
        return contenuto is null ? null : PropostaAiJson.Interpreta(contenuto);
    }

    /// <summary>Estrae choices[0].message.content dalla risposta OpenAI-compatibile. Puro: testabile.</summary>
    public static string? EstraiContenuto(string rispostaJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rispostaJson);
            if (!doc.RootElement.TryGetProperty("choices", out var scelte)
                || scelte.ValueKind != JsonValueKind.Array || scelte.GetArrayLength() == 0)
                return null;
            var messaggio = scelte[0];
            if (!messaggio.TryGetProperty("message", out var m)
                || !m.TryGetProperty("content", out var contenuto)
                || contenuto.ValueKind != JsonValueKind.String)
                return null;
            return contenuto.GetString();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
