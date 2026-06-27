using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Paghe.Core.Fatturazione.Trasmissione;

namespace Paghe.Persistence.Trasmissione;

/// <summary>
/// Configurazione del provider accreditato A-Cube (sezione "Acube" di appsettings/secret).
/// Le credenziali (email/password) le ottieni aprendo l'account A-Cube e vanno tenute come secret.
/// </summary>
public sealed class AcubeOptions
{
    /// <summary>Email dell'account A-Cube.</summary>
    public string Email { get; set; } = "";

    /// <summary>Password dell'account A-Cube (tenere come secret, non in chiaro nel repo).</summary>
    public string Password { get; set; } = "";

    /// <summary>Ambiente A-Cube: "sandbox" (prove) o "production" (reale).</summary>
    public string Environment { get; set; } = "sandbox";

    /// <summary>Endpoint di login comune (uguale per sandbox e produzione; l'ambiente è nel body).</summary>
    public string LoginUrl { get; set; } = "https://common.api.acubeapi.com/login";

    /// <summary>Host base dell'API e-invoicing in sandbox.</summary>
    public string BaseUrlSandbox { get; set; } = "https://api-sandbox.acubeapi.com";

    /// <summary>Host base dell'API e-invoicing in produzione.</summary>
    public string BaseUrlProduction { get; set; } = "https://api.acubeapi.com";

    /// <summary>Endpoint relativo per l'invio della fattura (POST). Default A-Cube: "/invoices".</summary>
    public string EndpointInvio { get; set; } = "/invoices";

    /// <summary>Endpoint relativo per leggere la fattura/esito; "{uuid}" è sostituito. Default "/invoices/{uuid}".</summary>
    public string EndpointEsito { get; set; } = "/invoices/{uuid}";

    /// <summary>Nome del campo JSON con l'UUID restituito da A-Cube. Default "uuid".</summary>
    public string CampoUuid { get; set; } = "uuid";

    public string BaseUrl => Environment.Equals("production", StringComparison.OrdinalIgnoreCase)
        ? BaseUrlProduction.TrimEnd('/')
        : BaseUrlSandbox.TrimEnd('/');
}

/// <summary>
/// Canale di trasmissione verso il provider accreditato <b>A-Cube</b>.
/// Flusso: login (email/password → token JWT 24h, riusato) → POST della FatturaPA XML su /invoices
/// (A-Cube firma e trasmette allo SdI) → lettura esito/notifiche su /invoices/{uuid}.
/// Riferimenti: https://docs.acubeapi.com/ — verificare in sandbox i nomi-campo della risposta
/// (sono resi configurabili via <see cref="AcubeOptions"/>).
/// </summary>
public sealed class SdiGatewayAcube : IFatturaSdiGateway
{
    private readonly HttpClient _http;
    private readonly AcubeOptions _opt;

    // Token condiviso tra le richieste: A-Cube consiglia un solo token ogni 24h.
    private static string? _token;
    private static DateTime _tokenScadenzaUtc;
    private static readonly SemaphoreSlim _tokenLock = new(1, 1);

    public SdiGatewayAcube(HttpClient http, AcubeOptions opt)
    {
        _http = http;
        _opt = opt;
    }

    public string Nome => $"A-Cube ({_opt.Environment})";

    // A-Cube firma e trasmette lato server: non serve la firma locale.
    public bool RichiedeFirmaLocale => false;

    public async Task<RisultatoTrasmissioneSdi> TrasmettiAsync(FatturaDaTrasmettere fattura, CancellationToken ct = default)
    {
        try
        {
            string token = await TokenAsync(ct);

            using var req = new HttpRequestMessage(HttpMethod.Post, _opt.BaseUrl + _opt.EndpointInvio);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = new ByteArrayContent(fattura.Contenuto);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue(
                fattura.Firmato ? "application/pkcs7-mime" : "application/xml");

            using var resp = await _http.SendAsync(req, ct);
            string body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                return new RisultatoTrasmissioneSdi(
                    StatoTrasmissioneSdi.Errore,
                    Messaggio: $"A-Cube ha rifiutato l'invio (HTTP {(int)resp.StatusCode}): {Tronca(body)}",
                    CodiceErrore: ((int)resp.StatusCode).ToString());

            string? uuid = LeggiCampo(body, _opt.CampoUuid) ?? LeggiCampo(body, "id");
            return new RisultatoTrasmissioneSdi(
                StatoTrasmissioneSdi.Inviata,
                IdentificativoSdi: uuid,
                Messaggio: "Fattura inviata ad A-Cube (firma e trasmissione SdI lato provider).");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new RisultatoTrasmissioneSdi(
                StatoTrasmissioneSdi.Errore,
                Messaggio: $"Errore di comunicazione con A-Cube: {ex.Message}",
                CodiceErrore: "NETWORK");
        }
    }

    public async Task<EsitoSdi?> InterrogaEsitoAsync(string identificativoSdi, CancellationToken ct = default)
    {
        try
        {
            string token = await TokenAsync(ct);
            string url = _opt.BaseUrl + _opt.EndpointEsito.Replace("{uuid}", Uri.EscapeDataString(identificativoSdi));

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
                return null;

            string body = await resp.Content.ReadAsStringAsync(ct);
            var stato = LeggiStato(body);
            return new EsitoSdi(
                stato,
                IdentificativoSdi: identificativoSdi,
                DataOra: DateTime.UtcNow,
                ContenutoGrezzo: Tronca(body, 4000));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    private async Task<string> TokenAsync(CancellationToken ct)
    {
        if (_token is not null && DateTime.UtcNow < _tokenScadenzaUtc)
            return _token;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTime.UtcNow < _tokenScadenzaUtc)
                return _token;

            using var resp = await _http.PostAsJsonAsync(_opt.LoginUrl, new
            {
                email = _opt.Email,
                password = _opt.Password,
                environment = _opt.Environment,
            }, ct);
            resp.EnsureSuccessStatusCode();

            string body = await resp.Content.ReadAsStringAsync(ct);
            _token = LeggiCampo(body, "token")
                ?? throw new InvalidOperationException("A-Cube: token non presente nella risposta di login.");
            // Il token dura 24h; lo rinnoviamo con un po' di margine.
            _tokenScadenzaUtc = DateTime.UtcNow.AddHours(20);
            return _token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>Mappa lo stato/notifica A-Cube nello stato interno. Cerca prima le notifiche SdI (RC/MC/NS/NE/DT).</summary>
    private static StatoTrasmissioneSdi LeggiStato(string corpoJson)
    {
        if (string.IsNullOrWhiteSpace(corpoJson))
            return StatoTrasmissioneSdi.Inviata;

        using var doc = JsonDocument.Parse(corpoJson);
        var root = doc.RootElement;

        // Notifiche SdI: A-Cube espone i tipi ufficiali (RC, MC, NS, NE, DT).
        if (root.TryGetProperty("notifications", out var notifiche) && notifiche.ValueKind == JsonValueKind.Array && notifiche.GetArrayLength() > 0)
        {
            var ultima = notifiche[notifiche.GetArrayLength() - 1];
            string? tipo = ultima.TryGetProperty("type", out var t) ? t.GetString() : null;
            string? codice = ultima.TryGetProperty("code", out var c) ? c.GetString() : null;
            if (!string.IsNullOrWhiteSpace(tipo))
                return MappaturaRicevuteSdi.DaTipo(tipo!, codice);
        }

        // Altrimenti un campo stato testuale.
        if (root.TryGetProperty("status", out var statoEl) && statoEl.ValueKind == JsonValueKind.String)
        {
            return (statoEl.GetString()?.Trim().ToLowerInvariant()) switch
            {
                "delivered" or "consegnata" => StatoTrasmissioneSdi.ConsegnataSdi,
                "not_delivered" or "mancata_consegna" => StatoTrasmissioneSdi.MancataConsegna,
                "rejected" or "scartata" or "error" => StatoTrasmissioneSdi.ScartataSdi,
                "accepted" => StatoTrasmissioneSdi.AccettataPa,
                "refused" => StatoTrasmissioneSdi.RifiutataPa,
                _ => StatoTrasmissioneSdi.Inviata,
            };
        }

        return StatoTrasmissioneSdi.Inviata;
    }

    private static string? LeggiCampo(string corpoJson, string campo)
    {
        if (string.IsNullOrWhiteSpace(corpoJson))
            return null;
        using var doc = JsonDocument.Parse(corpoJson);
        if (!doc.RootElement.TryGetProperty(campo, out var el))
            return null;
        return el.ValueKind == JsonValueKind.Number ? el.GetRawText() : el.GetString();
    }

    private static string Tronca(string testo, int max = 500) =>
        testo.Length <= max ? testo : testo[..max] + "…";
}
