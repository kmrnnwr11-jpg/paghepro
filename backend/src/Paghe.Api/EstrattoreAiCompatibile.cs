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
    private readonly IReadOnlyList<ConfigurazioneProviderAiCompatibile> _provider;

    public EstrattoreAiCompatibile(HttpClient http, AiOptions opzioni)
    {
        _http = http;
        var configurati = CatalogoProviderAiCompatibili.Tutte(opzioni);
        if (configurati.Count == 0)
            throw new InvalidOperationException(
                "Ai:Provider non è un provider OpenAI-compatibile supportato.");
        if (configurati.Any(c => string.IsNullOrWhiteSpace(c.BaseUrl)))
            throw new InvalidOperationException(
                "Ai:BaseUrl mancante: per il provider \"Compatibile\" indicare l'endpoint OpenAI-compatibile.");
        if (configurati.Any(c => !opzioni.EndpointConsentito(c.Provider, c.BaseUrl)))
            throw new InvalidOperationException(
                "Endpoint AI non autorizzato dal gate G1/G1-SaaS o fuori dalla allowlist egress.");
        if (configurati.Any(c => string.IsNullOrWhiteSpace(c.Modello)))
            throw new InvalidOperationException(
                "Ai:Modello mancante: per il provider \"Compatibile\" indicare il nome del modello.");
        _provider = CatalogoProviderAiCompatibili.AttivePerEstrazione(opzioni);
    }

    public string Modello => _provider.FirstOrDefault()?.Modello ?? "";

    /// <summary>Modello con la vista; vuoto = il provider non legge immagini.</summary>
    public string? ModelloVisione => _provider.FirstOrDefault(c =>
        !string.IsNullOrWhiteSpace(c.ModelloVisione))?.ModelloVisione;

    /// <summary>Con l'interruttore acceso ma senza chiave si resta spenti: il provider direbbe 401.</summary>
    public bool Attivo => _provider.Count > 0;

    public bool VedeImmagini => !string.IsNullOrWhiteSpace(ModelloVisione);

    public async Task<PropostaAi?> EstraiAsync(
        string tipoDocumento, string testoDocumento,
        IReadOnlyList<string> campiRichiesti, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(testoDocumento) || campiRichiesti.Count == 0)
            return null;

        foreach (var provider in _provider)
        {
            var corpo = new Dictionary<string, object?>
            {
                ["model"] = provider.Modello,
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
            if (provider.Provider.Equals("DeepSeek", StringComparison.OrdinalIgnoreCase))
                corpo["thinking"] = new { type = "disabled" };

            var proposta = await InviaAsync(provider, corpo, ct);
            if (proposta is not null) return proposta;
        }

        return null;
    }

    public async Task<PropostaAi?> EstraiDaImmagineAsync(
        string tipoDocumento, byte[] immagine, string mimeType,
        IReadOnlyList<string> campiRichiesti, CancellationToken ct = default)
    {
        if (!VedeImmagini || immagine.Length == 0 || campiRichiesti.Count == 0)
            return null;

        string dataUri = $"data:{mimeType};base64,{Convert.ToBase64String(immagine)}";
        foreach (var provider in _provider.Where(c => !string.IsNullOrWhiteSpace(c.ModelloVisione)))
        {
            var corpo = new
            {
                model = provider.ModelloVisione,
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

            var proposta = await InviaAsync(provider, corpo, ct);
            if (proposta is not null) return proposta;
        }

        return null;
    }

    private async Task<PropostaAi?> InviaAsync(
        ConfigurazioneProviderAiCompatibile provider, object corpo, CancellationToken ct)
    {
        using var richiesta = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(provider.BaseUrl.TrimEnd('/') + "/"), "chat/completions"))
        {
            Content = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrWhiteSpace(provider.ApiKey))
            richiesta.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        if (provider.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
            richiesta.Headers.TryAddWithoutValidation(
                "x-goog-api-client", "paghepro-openai-compatible/1.0");

        HttpResponseMessage risposta;
        try
        {
            risposta = await _http.SendAsync(richiesta, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }

        using (risposta)
        {
            if (!risposta.IsSuccessStatusCode)
                return null;

            string json = await risposta.Content.ReadAsStringAsync(ct);
            string? contenuto = EstraiContenuto(json);
            return contenuto is null ? null : PropostaAiJson.Interpreta(contenuto);
        }
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
