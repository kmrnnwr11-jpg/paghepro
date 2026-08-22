using System.Security.Cryptography;
using System.Text;

namespace Paghe.Persistence.Ai;

/// <summary>
/// Token di conferma di una proposta Aurora (piano Aurora §3.2): la SPA rimanda soltanto
/// questo handle firmato, mai gli argomenti. Porta id della proposta, hash degli argomenti
/// e scadenza; qualunque alterazione invalida la firma HMAC-SHA256. Stesso formato della
/// lease del Governor (v1.&lt;payload&gt;.&lt;firma&gt;, base64url) ma con chiave derivata distinta:
/// una lease non è mai spendibile come conferma, né viceversa.
/// </summary>
public static class AiPropostaCodec
{
    public sealed record TokenDecodificato(Guid PropostaId, string ArgomentiHash, DateTime ScadeUtc);

    public static string Crea(Guid propostaId, string argomentiHash, DateTime scadeUtc, byte[] chiave)
    {
        var payload = $"{propostaId:N}|{argomentiHash}|{scadeUtc:O}";
        var payloadB64 = Base64Url(Encoding.UTF8.GetBytes(payload));
        return $"v1.{payloadB64}.{Base64Url(Firma(payloadB64, chiave))}";
    }

    /// <summary>Null se formato, firma o struttura non sono validi (la scadenza la valuta lo store).</summary>
    public static TokenDecodificato? Prova(string? token, byte[] chiave)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var parti = token.Split('.');
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
        if (campi.Length != 3
            || !Guid.TryParseExact(campi[0], "N", out var propostaId)
            || string.IsNullOrEmpty(campi[1])
            || !DateTime.TryParse(campi[2], null, System.Globalization.DateTimeStyles.RoundtripKind, out var scade))
            return null;

        return new TokenDecodificato(propostaId, campi[1], scade);
    }

    public static string CreaConferma(
        Guid propostaId, string intentoHash, DateTime scadeUtc, byte[] chiave) =>
        CreaV2("v2c", propostaId, intentoHash, scadeUtc, chiave);

    public static TokenDecodificato? ProvaConferma(string? token, byte[] chiave) =>
        ProvaV2("v2c", token, chiave);

    public static string CreaUndo(
        Guid propostaId, string intentoHash, DateTime scadeUtc, byte[] chiave) =>
        CreaV2("v2u", propostaId, intentoHash, scadeUtc, chiave);

    public static TokenDecodificato? ProvaUndo(string? token, byte[] chiave) =>
        ProvaV2("v2u", token, chiave);

    /// <summary>Chiave derivata dalla chiave JWT con etichetta propria: nessun nuovo secret da gestire.</summary>
    public static byte[] DerivaChiave(byte[] chiaveJwt) =>
        HMACSHA256.HashData(chiaveJwt, Encoding.UTF8.GetBytes("paghe-ai-proposta-v1"));

    private static byte[] Firma(string payloadB64, byte[] chiave) =>
        HMACSHA256.HashData(chiave, Encoding.UTF8.GetBytes(payloadB64));

    private static string Base64Url(byte[] dati) =>
        Convert.ToBase64String(dati).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] DaBase64Url(string testo)
    {
        var s = testo.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }

    private static string CreaV2(
        string tipo, Guid propostaId, string intentoHash, DateTime scadeUtc, byte[] chiave)
    {
        var payload = $"{tipo}|{propostaId:N}|{intentoHash}|{scadeUtc:O}";
        var payloadB64 = Base64Url(Encoding.UTF8.GetBytes(payload));
        return $"{tipo}.{payloadB64}.{Base64Url(Firma($"{tipo}.{payloadB64}", chiave))}";
    }

    private static TokenDecodificato? ProvaV2(string tipo, string? token, byte[] chiave)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var parti = token.Split('.');
        if (parti.Length != 3 || parti[0] != tipo) return null;
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
        if (!CryptographicOperations.FixedTimeEquals(
                firmaRicevuta, Firma($"{tipo}.{parti[1]}", chiave)))
            return null;
        var campi = Encoding.UTF8.GetString(payloadBytes).Split('|');
        if (campi.Length != 4 || campi[0] != tipo
            || !Guid.TryParseExact(campi[1], "N", out var propostaId)
            || string.IsNullOrWhiteSpace(campi[2])
            || !DateTime.TryParse(campi[3], null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var scade))
            return null;
        return new TokenDecodificato(propostaId, campi[2], scade);
    }
}
