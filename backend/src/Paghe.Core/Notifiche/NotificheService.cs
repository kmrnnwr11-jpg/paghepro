namespace Paghe.Core.Notifiche;

/// <summary>
/// Sceglie il canale (Manuale/Email/SMS/WhatsApp) e recapita il messaggio. Tutti i notificatori sono
/// registrati: la richiesta può forzare un canale, altrimenti si usa quello di default in configurazione.
/// </summary>
public sealed class NotificheService
{
    private readonly NotificheOptions _opt;
    private readonly IReadOnlyDictionary<CanaleNotifica, INotificatore> _canali;

    public NotificheService(
        NotificheOptions opt,
        NotificatoreManuale manuale,
        NotificatoreEmailSmtp email,
        NotificatoreSms sms,
        NotificatoreWhatsApp whatsApp)
    {
        _opt = opt;
        _canali = new Dictionary<CanaleNotifica, INotificatore>
        {
            [CanaleNotifica.Manuale] = manuale,
            [CanaleNotifica.Email] = email,
            [CanaleNotifica.Sms] = sms,
            [CanaleNotifica.WhatsApp] = whatsApp,
        };
    }

    /// <summary>Recapita il messaggio sul canale indicato (o quello di default), al destinatario dato
    /// (o a quello di ripiego in configurazione).</summary>
    public Task<EsitoNotifica> InviaAsync(
        CanaleNotifica? canale, string? destinatario, MessaggioNotifica messaggio, CancellationToken ct = default)
    {
        var scelto = canale ?? _opt.CanaleDefault;
        var a = string.IsNullOrWhiteSpace(destinatario) ? (_opt.DestinatarioDefault ?? "") : destinatario.Trim();
        return _canali[scelto].InviaAsync(a, messaggio, ct);
    }
}
