using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Paghe.Persistence.Gdpr;

/// <summary>
/// Cifratura applicativa dei dati personali sensibili (es. IBAN) a riposo nel database — GDPR.
/// Usa <b>AES-256-GCM</b> (riservatezza + autenticità): ogni valore è cifrato con un nonce casuale
/// e un tag di autenticazione, e salvato come testo <c>enc:v1:BASE64(nonce|tag|cifrato)</c>.
///
/// <para>La chiave deriva (SHA-256) da una passphrase letta dalla variabile d'ambiente
/// <c>PAGHE_DATA_KEY</c>. In assenza si usa una passphrase di <b>solo sviluppo</b> (come per la
/// chiave JWT): in produzione <b>impostare sempre</b> <c>PAGHE_DATA_KEY</c> da un gestore di segreti.</para>
///
/// <para>È <b>retrocompatibile</b>: un valore senza il prefisso <c>enc:v1:</c> (dato storico in
/// chiaro) viene restituito così com'è, così il passaggio alla cifratura non rompe i DB esistenti.
/// Alla prima riscrittura della riga il valore viene cifrato.</para>
/// </summary>
public sealed class ProtettoreDati
{
    private const string Prefisso = "enc:v1:";
    private const int DimensioneNonce = 12; // 96 bit, raccomandato per GCM
    private const int DimensioneTag = 16;   // 128 bit

    /// <summary>Passphrase di SOLO sviluppo. In produzione impostare <c>PAGHE_DATA_KEY</c>.</summary>
    public const string PassphraseSviluppo = "paghe-dev-data-key-NON-usare-in-produzione";

    private readonly byte[] _chiave; // 32 byte (AES-256)

    public ProtettoreDati(string passphrase) : this(passphrase, AmbienteProduzione()) { }

    /// <summary>
    /// Variante con l'ambiente esplicito (testabile). In produzione la <b>passphrase di sviluppo</b>
    /// è rifiutata: se <c>PAGHE_DATA_KEY</c> non è impostata l'avvio fallisce (fail-fast), così i dati
    /// non vengono mai cifrati con una chiave nota. Stessa logica della chiave JWT.
    /// </summary>
    public ProtettoreDati(string passphrase, bool produzione)
    {
        if (string.IsNullOrEmpty(passphrase))
            throw new ArgumentException("La passphrase di cifratura non può essere vuota.", nameof(passphrase));
        if (produzione && passphrase == PassphraseSviluppo)
            throw new InvalidOperationException(
                "PAGHE_DATA_KEY mancante in produzione: configurare una chiave di cifratura robusta " +
                "tramite la variabile d'ambiente PAGHE_DATA_KEY (da secret). La passphrase di " +
                "sviluppo non è ammessa in ambiente Production.");
        _chiave = SHA256.HashData(Encoding.UTF8.GetBytes(passphrase));
    }

    private static bool AmbienteProduzione() =>
        string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            "Production", StringComparison.OrdinalIgnoreCase);

    /// <summary>Istanza condivisa, costruita dalla variabile d'ambiente o dalla passphrase di sviluppo.</summary>
    public static ProtettoreDati Corrente { get; } = new(
        Environment.GetEnvironmentVariable("PAGHE_DATA_KEY") is { Length: > 0 } chiave
            ? chiave
            : PassphraseSviluppo);

    /// <summary>True se sta usando la passphrase di sviluppo (utile per avvisi in avvio).</summary>
    public bool UsaChiaveDiSviluppo =>
        Environment.GetEnvironmentVariable("PAGHE_DATA_KEY") is not { Length: > 0 };

    /// <summary>Cifra un valore in chiaro. <c>null</c> ed empty restano invariati.</summary>
    public string? Proteggi(string? valore)
    {
        if (string.IsNullOrEmpty(valore)) return valore;
        if (valore.StartsWith(Prefisso, StringComparison.Ordinal)) return valore; // già cifrato

        var nonce = RandomNumberGenerator.GetBytes(DimensioneNonce);
        var testo = Encoding.UTF8.GetBytes(valore);
        var cifrato = new byte[testo.Length];
        var tag = new byte[DimensioneTag];

        using var aes = new AesGcm(_chiave, DimensioneTag);
        aes.Encrypt(nonce, testo, cifrato, tag);

        var pacchetto = new byte[DimensioneNonce + DimensioneTag + cifrato.Length];
        Buffer.BlockCopy(nonce, 0, pacchetto, 0, DimensioneNonce);
        Buffer.BlockCopy(tag, 0, pacchetto, DimensioneNonce, DimensioneTag);
        Buffer.BlockCopy(cifrato, 0, pacchetto, DimensioneNonce + DimensioneTag, cifrato.Length);

        return Prefisso + Convert.ToBase64String(pacchetto);
    }

    /// <summary>Decifra un valore. I dati storici in chiaro (senza prefisso) sono restituiti invariati.</summary>
    public string? Svela(string? valore)
    {
        if (string.IsNullOrEmpty(valore)) return valore;
        if (!valore.StartsWith(Prefisso, StringComparison.Ordinal)) return valore; // dato storico in chiaro

        byte[] pacchetto;
        try { pacchetto = Convert.FromBase64String(valore[Prefisso.Length..]); }
        catch (FormatException) { return valore; }

        if (pacchetto.Length < DimensioneNonce + DimensioneTag)
            return valore;

        var nonce = pacchetto.AsSpan(0, DimensioneNonce);
        var tag = pacchetto.AsSpan(DimensioneNonce, DimensioneTag);
        var cifrato = pacchetto.AsSpan(DimensioneNonce + DimensioneTag);
        var testo = new byte[cifrato.Length];

        try
        {
            using var aes = new AesGcm(_chiave, DimensioneTag);
            aes.Decrypt(nonce, cifrato, tag, testo);
        }
        catch (AuthenticationTagMismatchException ex)
        {
            // Chiave errata o dato manomesso: meglio fallire chiaro che esporre dati sbagliati.
            throw new InvalidOperationException(
                "Impossibile decifrare un dato protetto: la chiave PAGHE_DATA_KEY non corrisponde " +
                "a quella usata per cifrarlo (oppure il dato è corrotto).", ex);
        }

        return Encoding.UTF8.GetString(testo);
    }

    /// <summary>Converter EF Core per una proprietà stringa: cifra in scrittura, decifra in lettura.</summary>
    public ValueConverter<string?, string?> ConverterStringa() =>
        new(v => Proteggi(v), v => Svela(v));
}
