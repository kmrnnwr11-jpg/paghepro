using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text;

namespace Paghe.Core.Notifiche;

/// <summary>Invio email via SMTP (provider classico o PEC). Usa solo BCL, nessuna dipendenza esterna.</summary>
public sealed class NotificatoreEmailSmtp : INotificatore
{
    private readonly EmailOptions _opt;
    public NotificatoreEmailSmtp(NotificheOptions opt) => _opt = opt.Email;

    public CanaleNotifica Canale => CanaleNotifica.Email;

    public async Task<EsitoNotifica> InviaAsync(string destinatario, MessaggioNotifica messaggio, CancellationToken ct = default)
    {
        if (!_opt.Configurato)
            return NonConfigurato(destinatario, "Email", "imposta Notifiche:Email (Host, Mittente, credenziali).");
        if (string.IsNullOrWhiteSpace(destinatario))
            return new EsitoNotifica(false, Canale, destinatario, "Destinatario email mancante.");

        using var client = new SmtpClient(_opt.Host, _opt.Porta)
        {
            EnableSsl = _opt.Ssl,
            Credentials = new NetworkCredential(_opt.Utente, _opt.Password),
        };
        using var mail = new MailMessage(_opt.Mittente, destinatario, messaggio.Oggetto, messaggio.Corpo);
        await client.SendMailAsync(mail, ct);
        return new EsitoNotifica(true, Canale, destinatario, $"Email inviata via {_opt.Host}.");
    }

    private EsitoNotifica NonConfigurato(string dest, string canale, string come) =>
        new(false, Canale, dest, $"Canale {canale} non configurato: {come}");
}

/// <summary>
/// Invio SMS sagomato sull'API Twilio (POST Messages.json, form-encoded, Basic auth).
/// L'<c>HttpClient</c> è iniettato come typed client: così è sostituibile nei test senza rete.
/// </summary>
public sealed class NotificatoreSms : INotificatore
{
    private readonly HttpClient _http;
    private readonly SmsOptions _opt;

    public NotificatoreSms(HttpClient http, NotificheOptions opt)
    {
        _http = http;
        _opt = opt.Sms;
    }

    public CanaleNotifica Canale => CanaleNotifica.Sms;

    public async Task<EsitoNotifica> InviaAsync(string destinatario, MessaggioNotifica messaggio, CancellationToken ct = default)
    {
        if (!_opt.Configurato)
            return new EsitoNotifica(false, Canale, destinatario,
                "Canale SMS non configurato: imposta Notifiche:Sms (AccountSid, AuthToken, Mittente).");
        if (string.IsNullOrWhiteSpace(destinatario))
            return new EsitoNotifica(false, Canale, destinatario, "Numero destinatario SMS mancante.");

        var url = $"{_opt.BaseUrl.TrimEnd('/')}/2010-04-01/Accounts/{_opt.AccountSid}/Messages.json";
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = destinatario,
                ["From"] = _opt.Mittente,
                ["Body"] = messaggio.TestoBreve,
            }),
        };
        var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_opt.AccountSid}:{_opt.AuthToken}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        using var resp = await _http.SendAsync(req, ct);
        var corpo = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            return new EsitoNotifica(false, Canale, destinatario, $"Errore SMS {(int)resp.StatusCode}: {corpo}");
        return new EsitoNotifica(true, Canale, destinatario, "SMS accettato dal gateway.");
    }
}

/// <summary>
/// Invio WhatsApp via Meta Cloud API (Graph API, messaggio di tipo testo, Bearer token).
/// NOTA produzione: fuori dalla finestra di 24h i messaggi business-initiated richiedono un
/// <em>template</em> approvato da Meta; qui si invia testo semplice (adatto a test/finestra aperta).
/// </summary>
public sealed class NotificatoreWhatsApp : INotificatore
{
    private readonly HttpClient _http;
    private readonly WhatsAppOptions _opt;

    public NotificatoreWhatsApp(HttpClient http, NotificheOptions opt)
    {
        _http = http;
        _opt = opt.WhatsApp;
    }

    public CanaleNotifica Canale => CanaleNotifica.WhatsApp;

    public async Task<EsitoNotifica> InviaAsync(string destinatario, MessaggioNotifica messaggio, CancellationToken ct = default)
    {
        if (!_opt.Configurato)
            return new EsitoNotifica(false, Canale, destinatario,
                "Canale WhatsApp non configurato: imposta Notifiche:WhatsApp (PhoneNumberId, Token).");
        if (string.IsNullOrWhiteSpace(destinatario))
            return new EsitoNotifica(false, Canale, destinatario, "Numero destinatario WhatsApp mancante.");

        var url = $"{_opt.BaseUrl.TrimEnd('/')}/{_opt.Versione}/{_opt.PhoneNumberId}/messages";
        var payload = new
        {
            messaging_product = "whatsapp",
            to = destinatario,
            type = "text",
            text = new { body = messaggio.TestoBreve },
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opt.Token);

        using var resp = await _http.SendAsync(req, ct);
        var corpo = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            return new EsitoNotifica(false, Canale, destinatario, $"Errore WhatsApp {(int)resp.StatusCode}: {corpo}");
        return new EsitoNotifica(true, Canale, destinatario, "Messaggio WhatsApp accettato da Meta.");
    }
}
