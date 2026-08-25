namespace Paghe.Api;

/// <summary>
/// Configurazione risolta di un provider che parla il protocollo OpenAI Chat Completions.
/// Le chiavi restano solo in memoria e non entrano mai negli endpoint di stato o nei log.
/// </summary>
public sealed record ConfigurazioneProviderAiCompatibile(
    string Provider,
    string BaseUrl,
    string Modello,
    string ModelloChat,
    string? ModelloVisione,
    string? ApiKey);

/// <summary>
/// Unico catalogo degli endpoint OpenAI-compatibili noti. In questo modo chat ed estrazione
/// usano gli stessi host, modelli e regole di fallback senza endpoint configurabili in modo
/// diverso o non verificato.
/// </summary>
public static class CatalogoProviderAiCompatibili
{
    public const string BaseUrlDeepSeek = "https://api.deepseek.com";
    public const string BaseUrlGemini = "https://generativelanguage.googleapis.com/v1beta/openai";
    public const string BaseUrlGlm = "https://open.bigmodel.cn/api/paas/v4";

    public const string ModelloDeepSeek = "deepseek-v4-flash";
    public const string ModelloGemini = "gemini-3.7-flash";
    public const string ModelloGlm = "glm-4-flash";
    public const string ModelloVisioneGlm = "glm-4v-flash";

    /// <summary>Provider principale scelto da <c>Ai:Provider</c>.</summary>
    public static ConfigurazioneProviderAiCompatibile? Primario(AiOptions opzioni)
    {
        string provider = opzioni.Provider.Trim();
        string? baseUrlDefault;
        string? modelloDefault;
        string? visioneDefault;

        switch (provider.ToLowerInvariant())
        {
            case "deepseek":
                baseUrlDefault = BaseUrlDeepSeek;
                modelloDefault = ModelloDeepSeek;
                visioneDefault = null;
                break;
            case "gemini":
                baseUrlDefault = BaseUrlGemini;
                modelloDefault = ModelloGemini;
                visioneDefault = ModelloGemini;
                break;
            case "glm":
                baseUrlDefault = BaseUrlGlm;
                modelloDefault = ModelloGlm;
                visioneDefault = ModelloVisioneGlm;
                break;
            case "compatibile":
                baseUrlDefault = null;
                modelloDefault = null;
                visioneDefault = null;
                break;
            default:
                return null;
        }

        string baseUrl = string.IsNullOrWhiteSpace(opzioni.BaseUrl)
            ? baseUrlDefault ?? ""
            : opzioni.BaseUrl.Trim();
        string modello = string.IsNullOrWhiteSpace(opzioni.Modello)
            ? modelloDefault ?? ""
            : opzioni.Modello.Trim();
        string modelloChat = string.IsNullOrWhiteSpace(opzioni.ModelloChat)
            ? modello
            : opzioni.ModelloChat.Trim();
        string? modelloVisione = string.IsNullOrWhiteSpace(opzioni.ModelloVisione)
            ? visioneDefault
            : opzioni.ModelloVisione.Trim();
        string? chiave = provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase)
            ? PrimaNonVuota(opzioni.ApiKey, opzioni.GeminiApiKey)
            : opzioni.ApiKey;

        return new ConfigurazioneProviderAiCompatibile(
            NomeNormalizzato(provider), baseUrl, modello, modelloChat, modelloVisione, chiave);
    }

    /// <summary>
    /// DeepSeek resta principale; Gemini viene aggiunto solo se richiesto e non è già il
    /// provider principale. Una risposta riuscita non viene mai duplicata sul secondo provider.
    /// </summary>
    public static IReadOnlyList<ConfigurazioneProviderAiCompatibile> Tutte(AiOptions opzioni)
    {
        var primario = Primario(opzioni);
        if (primario is null) return [];

        var configurazioni = new List<ConfigurazioneProviderAiCompatibile> { primario };
        if (opzioni.UsaGeminiComeFallback
            && !primario.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            string modello = string.IsNullOrWhiteSpace(opzioni.ModelloGemini)
                ? ModelloGemini
                : opzioni.ModelloGemini.Trim();
            configurazioni.Add(new ConfigurazioneProviderAiCompatibile(
                "Gemini", BaseUrlGemini, modello, modello, modello, opzioni.GeminiApiKey));
        }

        return configurazioni;
    }

    /// <summary>Provider utilizzabili dalla chat; il modello documentale può essere assente.</summary>
    public static IReadOnlyList<ConfigurazioneProviderAiCompatibile> AttivePerChat(AiOptions opzioni) =>
        [.. Tutte(opzioni).Where(c =>
            c.BaseUrl.Length > 0
            && c.ModelloChat.Length > 0
            && (!string.IsNullOrWhiteSpace(c.ApiKey)
                || AiOptions.ÈEndpointOllamaLocale(c.BaseUrl))
            && opzioni.EndpointConsentito(c.Provider, c.BaseUrl))];

    /// <summary>Provider utilizzabili dall'estrazione documentale.</summary>
    public static IReadOnlyList<ConfigurazioneProviderAiCompatibile> AttivePerEstrazione(
        AiOptions opzioni) =>
        [.. Tutte(opzioni).Where(c =>
            c.BaseUrl.Length > 0
            && c.Modello.Length > 0
            && (!string.IsNullOrWhiteSpace(c.ApiKey)
                || AiOptions.ÈEndpointOllamaLocale(c.BaseUrl))
            && opzioni.EndpointConsentito(c.Provider, c.BaseUrl))];

    private static string NomeNormalizzato(string provider) => provider.ToLowerInvariant() switch
    {
        "deepseek" => "DeepSeek",
        "gemini" => "Gemini",
        "glm" => "Glm",
        "compatibile" => "Compatibile",
        _ => provider,
    };

    private static string? PrimaNonVuota(string? prima, string? seconda) =>
        !string.IsNullOrWhiteSpace(prima) ? prima : seconda;
}
