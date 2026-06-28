using System.Security.Cryptography;
using System.Text;

namespace Paghe.Persistence.Sicurezza;

/// <summary>
/// Autenticazione a due fattori con codici temporanei <b>TOTP</b> (RFC 6238): HMAC-SHA1, passo di
/// 30 secondi, 6 cifre — compatibile con le app authenticator (Google Authenticator, Authy, ecc.).
/// Implementazione autonoma, nessuna dipendenza esterna. Il segreto è in <b>Base32</b> (RFC 4648).
/// </summary>
public static class Totp
{
    private const int PassoSecondi = 30;
    private const int Cifre = 6;
    private const string AlfabetoBase32 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>Genera un nuovo segreto casuale (20 byte) in Base32, da consegnare all'app authenticator.</summary>
    public static string GeneraSegreto() => CodificaBase32(RandomNumberGenerator.GetBytes(20));

    /// <summary>Calcola il codice a 6 cifre per il segreto Base32 a un dato istante (default: ora).</summary>
    public static string GeneraCodice(string segretoBase32, DateTimeOffset? istante = null)
    {
        var contatore = (istante ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / PassoSecondi;
        return GeneraPerContatore(DecodificaBase32(segretoBase32), contatore);
    }

    /// <summary>
    /// Verifica un codice ammettendo una finestra di tolleranza (default ±1 passo = ±30s) per coprire
    /// piccoli scarti di orologio tra server e dispositivo.
    /// </summary>
    public static bool Verifica(string segretoBase32, string codice, int finestra = 1, DateTimeOffset? istante = null)
    {
        if (string.IsNullOrWhiteSpace(segretoBase32) || string.IsNullOrWhiteSpace(codice))
            return false;

        codice = codice.Trim();
        var chiave = DecodificaBase32(segretoBase32);
        var contatore = (istante ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / PassoSecondi;

        for (var delta = -finestra; delta <= finestra; delta++)
        {
            var atteso = GeneraPerContatore(chiave, contatore + delta);
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(atteso), Encoding.ASCII.GetBytes(codice)))
                return true;
        }
        return false;
    }

    /// <summary>URI <c>otpauth://</c> da mostrare come QR code nell'app authenticator.</summary>
    public static string UriOtpauth(string emittente, string account, string segretoBase32)
    {
        var em = Uri.EscapeDataString(emittente);
        var ac = Uri.EscapeDataString(account);
        return $"otpauth://totp/{em}:{ac}?secret={segretoBase32}&issuer={em}&algorithm=SHA1&digits={Cifre}&period={PassoSecondi}";
    }

    private static string GeneraPerContatore(byte[] chiave, long contatore)
    {
        Span<byte> messaggio = stackalloc byte[8];
        for (var i = 7; i >= 0; i--)
        {
            messaggio[i] = (byte)(contatore & 0xFF);
            contatore >>= 8;
        }

        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(chiave, messaggio, hash);

        var offset = hash[^1] & 0x0F;
        var binario = ((hash[offset] & 0x7F) << 24)
                      | (hash[offset + 1] << 16)
                      | (hash[offset + 2] << 8)
                      | hash[offset + 3];

        var codice = binario % (int)Math.Pow(10, Cifre);
        return codice.ToString().PadLeft(Cifre, '0');
    }

    private static string CodificaBase32(ReadOnlySpan<byte> dati)
    {
        var sb = new StringBuilder((dati.Length + 4) / 5 * 8);
        int buffer = 0, bit = 0;
        foreach (var b in dati)
        {
            buffer = (buffer << 8) | b;
            bit += 8;
            while (bit >= 5)
            {
                bit -= 5;
                sb.Append(AlfabetoBase32[(buffer >> bit) & 0x1F]);
            }
        }
        if (bit > 0)
            sb.Append(AlfabetoBase32[(buffer << (5 - bit)) & 0x1F]);
        return sb.ToString();
    }

    private static byte[] DecodificaBase32(string testo)
    {
        var pulito = testo.Trim().TrimEnd('=').ToUpperInvariant();
        var byteList = new List<byte>(pulito.Length * 5 / 8);
        int buffer = 0, bit = 0;
        foreach (var c in pulito)
        {
            var valore = AlfabetoBase32.IndexOf(c);
            if (valore < 0) continue; // ignora separatori/spazi
            buffer = (buffer << 5) | valore;
            bit += 5;
            if (bit >= 8)
            {
                bit -= 8;
                byteList.Add((byte)((buffer >> bit) & 0xFF));
            }
        }
        return byteList.ToArray();
    }
}
