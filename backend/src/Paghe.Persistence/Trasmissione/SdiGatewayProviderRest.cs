using System.Net.Http.Json;
using System.Text.Json;
using Paghe.Core.Fatturazione.Trasmissione;

namespace Paghe.Persistence.Trasmissione;

/// <summary>
/// Configurazione del provider SdI accreditato (Aruba, Fatture in Cloud, Acube, Openapi, ...).
/// Va valorizzata da appsettings/secret nella sezione "Sdi". I nomi esatti dei campi della
/// risposta variano per provider: adattare <see cref="CampoId"/> e <see cref="CampoStato"/>.
/// </summary>
public sealed class SdiProviderOptions
{
    /// <summary>Selettore del canale: "Manuale" (default) oppure "Rest".</summary>
    public string Provider { get; set; } = "Manuale";

    /// <summary>URL base dell'API del provider (es. https://api.provider.it/v1/).</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>API key / token Bearer del provider.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Endpoint relativo per l'invio della fattura.</summary>
    public string EndpointInvio { get; set; } = "invoices";

    /// <summary>Endpoint relativo per l'interrogazione esito; "{id}" è sostituito con l'identificativo.</summary>
    public string EndpointEsito { get; set; } = "invoices/{id}";

    /// <summary>Vero se il provider richiede la fattura già firmata (.p7m) lato client.</summary>
    public bool FirmaLocale { get; set; }

    /// <summary>Nome del campo JSON con l'identificativo restituito dal provider.</summary>
    public string CampoId { get; set; } = "id";

    /// <summary>Nome del campo JSON con lo stato restituito dal provider.</summary>
    public string CampoStato { get; set; } = "status";
}

/// <summary>
/// Canale REST generico verso un provider accreditato SdI. Invia il documento (XML o .p7m in
/// base64) all'endpoint del provider con autenticazione Bearer, interpreta la risposta JSON e
/// mappa lo stato del provider nello stato interno. È volutamente generico: il mapping dei campi
/// si adatta via <see cref="SdiProviderOptions"/> al provider scelto.
/// </summary>
public sealed class SdiGatewayProviderRest : IFatturaSdiGateway
{
    private readonly HttpClient _http;
    private readonly SdiProviderOptions _options;

    public SdiGatewayProviderRest(HttpClient http, SdiProviderOptions options)
    {
        _http = http;
        _options = options;
        if (!string.IsNullOrWhiteSpace(_options.BaseUrl) && _http.BaseAddress is null)
            _http.BaseAddress = new Uri(_options.BaseUrl.EndsWith('/') ? _options.BaseUrl : _options.BaseUrl + "/");
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    public string Nome => $"Provider REST ({_options.BaseUrl})";

    public bool RichiedeFirmaLocale => _options.FirmaLocale;

    public async Task<RisultatoTrasmissioneSdi> TrasmettiAsync(FatturaDaTrasmettere fattura, CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                fileName = fattura.NomeFile,
                contentBase64 = Convert.ToBase64String(fattura.Contenuto),
                signed = fattura.Firmato,
                format = fattura.FormatoTrasmissione,
            };

            using var resp = await _http.PostAsJsonAsync(_options.EndpointInvio, payload, ct);
            string corpo = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                return new RisultatoTrasmissioneSdi(
                    StatoTrasmissioneSdi.Errore,
                    Messaggio: $"Invio rifiutato dal provider (HTTP {(int)resp.StatusCode}): {Tronca(corpo)}",
                    CodiceErrore: ((int)resp.StatusCode).ToString());

            var (id, stato) = LeggiRisposta(corpo);
            return new RisultatoTrasmissioneSdi(
                stato ?? StatoTrasmissioneSdi.Inviata,
                IdentificativoSdi: id,
                Messaggio: "Inviata al provider accreditato.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new RisultatoTrasmissioneSdi(
                StatoTrasmissioneSdi.Errore,
                Messaggio: $"Errore di comunicazione con il provider: {ex.Message}",
                CodiceErrore: "NETWORK");
        }
    }

    public async Task<EsitoSdi?> InterrogaEsitoAsync(string identificativoSdi, CancellationToken ct = default)
    {
        string endpoint = _options.EndpointEsito.Replace("{id}", Uri.EscapeDataString(identificativoSdi));
        try
        {
            using var resp = await _http.GetAsync(endpoint, ct);
            string corpo = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                return null;

            var (id, stato) = LeggiRisposta(corpo);
            return new EsitoSdi(
                stato ?? StatoTrasmissioneSdi.Inviata,
                IdentificativoSdi: id ?? identificativoSdi,
                DataOra: DateTimeOffset.UtcNow,
                ContenutoGrezzo: Tronca(corpo, 4000));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    private (string? id, StatoTrasmissioneSdi? stato) LeggiRisposta(string corpoJson)
    {
        if (string.IsNullOrWhiteSpace(corpoJson))
            return (null, null);

        using var doc = JsonDocument.Parse(corpoJson);
        var root = doc.RootElement;

        string? id = root.TryGetProperty(_options.CampoId, out var idEl)
            ? (idEl.ValueKind == JsonValueKind.Number ? idEl.GetRawText() : idEl.GetString())
            : null;

        StatoTrasmissioneSdi? stato = root.TryGetProperty(_options.CampoStato, out var statoEl)
            ? MappaStatoProvider(statoEl.GetString())
            : null;

        return (id, stato);
    }

    /// <summary>Mappa la stringa di stato del provider negli stati interni. Adattare ai valori reali del provider.</summary>
    private static StatoTrasmissioneSdi MappaStatoProvider(string? statoProvider) =>
        (statoProvider?.Trim().ToLowerInvariant()) switch
        {
            "sent" or "submitted" or "inviata" or "transmitting" or "queued" => StatoTrasmissioneSdi.Inviata,
            "delivered" or "consegnata" or "rc" => StatoTrasmissioneSdi.ConsegnataSdi,
            "not_delivered" or "mancata_consegna" or "mc" => StatoTrasmissioneSdi.MancataConsegna,
            "rejected" or "scartata" or "ns" or "discarded" => StatoTrasmissioneSdi.ScartataSdi,
            "accepted" or "accettata" => StatoTrasmissioneSdi.AccettataPa,
            "refused" or "rifiutata" => StatoTrasmissioneSdi.RifiutataPa,
            "expired" or "decorrenza_termini" or "dt" => StatoTrasmissioneSdi.DecorrenzaTermini,
            "error" or "errore" or "failed" => StatoTrasmissioneSdi.Errore,
            _ => StatoTrasmissioneSdi.Inviata,
        };

    private static string Tronca(string testo, int max = 500) =>
        testo.Length <= max ? testo : testo[..max] + "…";
}
