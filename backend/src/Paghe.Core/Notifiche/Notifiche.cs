namespace Paghe.Core.Notifiche;

/// <summary>Canale con cui recapitare un promemoria/sollecito.</summary>
public enum CanaleNotifica
{
    /// <summary>Nessun invio: il messaggio viene solo preparato (da copiare/inviare a mano).</summary>
    Manuale,
    Email,
    Sms,
    WhatsApp,
}

/// <summary>
/// Messaggio neutro rispetto al canale. <see cref="Oggetto"/>/<see cref="Corpo"/> servono per l'email;
/// <see cref="TestoBreve"/> è la versione compatta per SMS/WhatsApp.
/// </summary>
public sealed record MessaggioNotifica(string Oggetto, string Corpo, string TestoBreve);

/// <summary>Esito di un invio (o della preparazione, per il canale manuale).</summary>
public sealed record EsitoNotifica(
    bool Inviato,
    CanaleNotifica Canale,
    string Destinatario,
    string Dettaglio,
    string? IdMessaggio = null);

/// <summary>Astrazione di un canale di invio. Le implementazioni concrete non devono lanciare per
/// configurazione mancante: restituiscono un <see cref="EsitoNotifica"/> non inviato e spiegano perché.</summary>
public interface INotificatore
{
    CanaleNotifica Canale { get; }
    Task<EsitoNotifica> InviaAsync(string destinatario, MessaggioNotifica messaggio, CancellationToken ct = default);
}

/// <summary>Configurazione dei canali di notifica (sezione "Notifiche" della config).</summary>
public sealed class NotificheOptions
{
    /// <summary>Canale predefinito: Manuale (default), Email, Sms, WhatsApp.</summary>
    public string Canale { get; set; } = "Manuale";

    /// <summary>Destinatario di ripiego (es. il numero/email dello studio) quando non indicato nella richiesta.</summary>
    public string? DestinatarioDefault { get; set; }

    public EmailOptions Email { get; set; } = new();
    public SmsOptions Sms { get; set; } = new();
    public WhatsAppOptions WhatsApp { get; set; } = new();

    public CanaleNotifica CanaleDefault => Canali.Parse(Canale);
}

/// <summary>SMTP per l'invio email (host/porta/credenziali del provider o PEC).</summary>
public sealed class EmailOptions
{
    public string Host { get; set; } = "";
    public int Porta { get; set; } = 587;
    public string Utente { get; set; } = "";
    public string Password { get; set; } = "";
    public string Mittente { get; set; } = "";
    public bool Ssl { get; set; } = true;

    public bool Configurato => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Mittente);
}

/// <summary>Gateway SMS (default sagomato su Twilio: Account SID + Auth Token + mittente).</summary>
public sealed class SmsOptions
{
    public string BaseUrl { get; set; } = "https://api.twilio.com";
    public string AccountSid { get; set; } = "";
    public string AuthToken { get; set; } = "";
    public string Mittente { get; set; } = "";

    public bool Configurato =>
        !string.IsNullOrWhiteSpace(AccountSid) && !string.IsNullOrWhiteSpace(AuthToken) &&
        !string.IsNullOrWhiteSpace(Mittente);
}

/// <summary>WhatsApp Business (Meta Cloud API): id numero + token. In produzione servono template approvati.</summary>
public sealed class WhatsAppOptions
{
    public string BaseUrl { get; set; } = "https://graph.facebook.com";
    public string Versione { get; set; } = "v21.0";
    public string PhoneNumberId { get; set; } = "";
    public string Token { get; set; } = "";

    public bool Configurato =>
        !string.IsNullOrWhiteSpace(PhoneNumberId) && !string.IsNullOrWhiteSpace(Token);
}

/// <summary>Helper di parsing del canale (case-insensitive, default Manuale).</summary>
public static class Canali
{
    public static CanaleNotifica Parse(string? valore) =>
        (valore?.Trim().ToLowerInvariant()) switch
        {
            "email" or "mail" or "smtp" => CanaleNotifica.Email,
            "sms" => CanaleNotifica.Sms,
            "whatsapp" or "wa" => CanaleNotifica.WhatsApp,
            _ => CanaleNotifica.Manuale,
        };
}

/// <summary>Canale di default: non invia nulla, prepara solo il messaggio da copiare/inviare a mano.</summary>
public sealed class NotificatoreManuale : INotificatore
{
    public CanaleNotifica Canale => CanaleNotifica.Manuale;

    public Task<EsitoNotifica> InviaAsync(string destinatario, MessaggioNotifica messaggio, CancellationToken ct = default) =>
        Task.FromResult(new EsitoNotifica(
            Inviato: false,
            Canale: CanaleNotifica.Manuale,
            Destinatario: destinatario,
            Dettaglio: "Canale manuale: messaggio preparato, copialo e invialo (nessun invio automatico)."));
}

/// <summary>Adatta un testo lungo a un messaggio breve per SMS/WhatsApp (toglie l'intestazione "Oggetto:" e compatta).</summary>
public static class FormattatoreNotifiche
{
    public static MessaggioNotifica DaTesto(string oggetto, string corpo, int lunghezzaMax = 480)
    {
        var righe = corpo.Replace("\r\n", "\n").Split('\n')
            .Select(r => r.Trim())
            .Where(r => r.Length > 0 && !r.StartsWith("Oggetto:", StringComparison.OrdinalIgnoreCase));
        string breve = string.Join(" ", righe);
        if (breve.Length > lunghezzaMax)
            breve = breve[..(lunghezzaMax - 1)].TrimEnd() + "…";
        return new MessaggioNotifica(oggetto, corpo, breve);
    }
}
