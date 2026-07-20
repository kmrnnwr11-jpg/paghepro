using System.Security.Cryptography;
using System.Text;

namespace Paghe.Persistence.Ai;

/// <summary>
/// AiScopeLease del piano §6: handle opaco firmato con soli grantId, nonce, issuedAt ed
/// expiresAt. Il modello non riceve mai la lease; i tool la risolvono lato server e il
/// Governor ricontrolla comunque il record autorevole. Formato: v1.&lt;payload&gt;.&lt;firma&gt;
/// (base64url, HMAC-SHA256). Qualsiasi alterazione invalida la firma.
/// </summary>
public static class AiLeaseCodec
{
    public sealed record LeaseDecodificata(Guid GrantId, string Nonce, DateTime IssuedAtUtc, DateTime ExpiresAtUtc);

    public static string Crea(Guid grantId, string nonce, DateTime issuedAtUtc, DateTime expiresAtUtc, byte[] chiave)
    {
        var payload = $"{grantId:N}|{nonce}|{issuedAtUtc:O}|{expiresAtUtc:O}";
        var payloadB64 = Base64Url(Encoding.UTF8.GetBytes(payload));
        return $"v1.{payloadB64}.{Base64Url(Firma(payloadB64, chiave))}";
    }

    /// <summary>Null se formato, firma o struttura non sono validi (la scadenza la valuta il Governor).</summary>
    public static LeaseDecodificata? Prova(string? lease, byte[] chiave)
    {
        if (string.IsNullOrWhiteSpace(lease)) return null;
        var parti = lease.Split('.');
        if (parti.Length != 3 || parti[0] != "v1") return null;

        byte[] firmaRicevuta;
        byte[] payloadBytes;
        try
        {
            firmaRicevuta = DaBase64Url(parti[2]);
            payloadBytes = DaBase64Url(parti[1]);
        }
        catch (FormatException)
        {
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(firmaRicevuta, Firma(parti[1], chiave)))
            return null;

        var campi = Encoding.UTF8.GetString(payloadBytes).Split('|');
        if (campi.Length != 4
            || !Guid.TryParseExact(campi[0], "N", out var grantId)
            || string.IsNullOrEmpty(campi[1])
            || !DateTime.TryParse(campi[2], null, System.Globalization.DateTimeStyles.RoundtripKind, out var issued)
            || !DateTime.TryParse(campi[3], null, System.Globalization.DateTimeStyles.RoundtripKind, out var expires))
            return null;

        return new LeaseDecodificata(grantId, campi[1], issued, expires);
    }

    /// <summary>Chiave della lease derivata dalla chiave JWT (già protetta in produzione): nessun nuovo secret.</summary>
    public static byte[] DerivaChiave(byte[] chiaveJwt) =>
        HMACSHA256.HashData(chiaveJwt, Encoding.UTF8.GetBytes("paghe-ai-governor-lease-v1"));

    private static byte[] Firma(string payloadB64, byte[] chiave) =>
        HMACSHA256.HashData(chiave, Encoding.UTF8.GetBytes(payloadB64));

    private static string Base64Url(byte[] dati) =>
        Convert.ToBase64String(dati).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] DaBase64Url(string testo)
    {
        var s = testo.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}
