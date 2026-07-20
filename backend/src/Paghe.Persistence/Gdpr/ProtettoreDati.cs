using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Paghe.Persistence.Gdpr;

/// <summary>
/// Cifratura applicativa dei dati personali sensibili a riposo nel database — GDPR.
/// Usa AES-256-GCM (riservatezza + autenticità) con un nonce casuale per ogni valore.
///
/// <para>Il formato corrente è <c>enc:v2:ID_CHIAVE:BASE64(nonce|tag|cifrato)</c>. L'ID è
/// un'impronta non segreta della chiave e consente di ruotarla senza tentativi ambigui. I valori
/// storici <c>enc:v1:</c> e quelli ancora in chiaro restano leggibili esclusivamente per permettere
/// una migrazione controllata; <see cref="MigrazioneDatiSensibili"/> li riscrive nel formato corrente.</para>
///
/// <para>La chiave corrente arriva da <c>PAGHE_DATA_KEY</c>. Durante una rotazione le vecchie
/// passphrase si forniscono, solo per il tempo della migrazione, come array JSON nel secret
/// <c>PAGHE_DATA_OLD_KEYS_JSON</c>. Le chiavi non devono stare nel repository né insieme al backup
/// del database.</para>
/// </summary>
public sealed class ProtettoreDati
{
    private const string PrefissoV1 = "enc:v1:";
    private const string PrefissoV2 = "enc:v2:";
    private const int DimensioneNonce = 12; // 96 bit, raccomandato per GCM
    private const int DimensioneTag = 16;   // 128 bit
    private const int DimensioneChiave = 32;

    /// <summary>Passphrase di SOLO sviluppo. In produzione impostare <c>PAGHE_DATA_KEY</c>.</summary>
    public const string PassphraseSviluppo = "paghe-dev-data-key-NON-usare-in-produzione";

    /// <summary>Secret opzionale usato durante la rotazione: array JSON di passphrase precedenti.</summary>
    public const string VariabileChiaviPrecedenti = "PAGHE_DATA_OLD_KEYS_JSON";

    private readonly byte[] _chiaveCorrente;
    private readonly IReadOnlyDictionary<string, byte[]> _chiaviPerId;
    private readonly bool _usaChiaveSviluppo;

    public ProtettoreDati(string passphrase) : this(passphrase, [], AmbienteProduzione()) { }

    /// <summary>Variante con ambiente esplicito, usata anche dai test.</summary>
    public ProtettoreDati(string passphrase, bool produzione) : this(passphrase, [], produzione) { }

    /// <summary>
    /// Costruisce un portachiavi con una chiave corrente e zero o più chiavi precedenti di sola
    /// lettura. Le precedenti servono a decifrare durante la rotazione, ma non sono mai usate per
    /// cifrare nuovi valori.
    /// </summary>
    public ProtettoreDati(
        string passphrase,
        IEnumerable<string> passphrasePrecedenti,
        bool produzione)
    {
        ValidaPassphraseCorrente(passphrase, produzione);

        _usaChiaveSviluppo = passphrase == PassphraseSviluppo;
        _chiaveCorrente = CreaChiaveCorrente(passphrase, produzione);
        IdChiaveCorrente = CalcolaIdChiave(_chiaveCorrente);

        var chiavi = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [IdChiaveCorrente] = _chiaveCorrente,
        };

        // Compatibilità con la v1: il vecchio codice applicava sempre SHA-256 anche quando il
        // secret era già una chiave Base64. La candidata è decrypt-only e verrà rimossa a cutover.
        AggiungiChiaveSeNuova(chiavi, DerivaPassphraseLegacy(passphrase));

        foreach (var precedente in passphrasePrecedenti ?? [])
        {
            if (string.IsNullOrWhiteSpace(precedente))
                throw new InvalidOperationException(
                    $"{VariabileChiaviPrecedenti} contiene una chiave vuota.");

            AggiungiChiaveSeNuova(chiavi, DerivaPassphraseLegacy(precedente));
            if (ProvaDecodificaChiave(precedente, out var chiaveRaw))
                AggiungiChiaveSeNuova(chiavi, chiaveRaw);
        }

        _chiaviPerId = chiavi;
    }

    /// <summary>Istanza condivisa costruita una volta all'avvio dai secret d'ambiente.</summary>
    public static ProtettoreDati Corrente { get; } = CreaDaAmbiente(produzione: false);

    /// <summary>ID non segreto della chiave usata per le nuove cifrature.</summary>
    public string IdChiaveCorrente { get; }

    /// <summary>Prefisso completo dei valori già allineati alla chiave corrente.</summary>
    public string PrefissoChiaveCorrente => $"{PrefissoV2}{IdChiaveCorrente}:";

    /// <summary>True se l'istanza usa la chiave nota di sviluppo.</summary>
    public bool UsaChiaveDiSviluppo => _usaChiaveSviluppo;

    /// <summary>Cifra un valore in chiaro. <c>null</c> ed empty restano invariati.</summary>
    public string? Proteggi(string? valore)
    {
        if (string.IsNullOrEmpty(valore)) return valore;

        // Utile per migrazioni e import controllati: un cifrato precedente viene prima decifrato,
        // poi riscritto con la chiave corrente. Un formato "enc:" sconosciuto fallisce chiuso.
        if (valore.StartsWith("enc:", StringComparison.Ordinal))
            return Ricifra(valore);

        return CifraConChiaveCorrente(valore);
    }

    /// <summary>
    /// Cifra legando autenticità e posizione del valore. Spostare il ciphertext tra tenant,
    /// record, tabelle o campi provoca il fallimento del tag GCM.
    /// </summary>
    public string? ProteggiConContesto(string? valore, string contesto)
    {
        if (string.IsNullOrEmpty(valore)) return valore;
        ValidaContesto(contesto);
        var aad = Encoding.UTF8.GetBytes(contesto);
        try
        {
            return valore.StartsWith("enc:", StringComparison.Ordinal)
                ? RicifraConContestoInterno(valore, aad)
                : CifraConChiaveCorrente(valore, aad);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(aad);
        }
    }

    /// <summary>
    /// Decifra un valore. Il testo storico privo di prefisso è restituito invariato per il solo
    /// periodo necessario alla migrazione; pacchetti cifrati corrotti o sconosciuti non sono mai
    /// scambiati per testo in chiaro.
    /// </summary>
    public string? Svela(string? valore) => SvelaInterno(valore, [], consentiStorico: true);

    /// <summary>Decifratura strict tenant-bound: testo in chiaro e formato v1 sono rifiutati.</summary>
    public string? SvelaConContesto(string? valore, string contesto)
    {
        if (string.IsNullOrEmpty(valore)) return valore;
        ValidaContesto(contesto);
        var aad = Encoding.UTF8.GetBytes(contesto);
        try
        {
            return SvelaInterno(valore, aad, consentiStorico: false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(aad);
        }
    }

    private string? SvelaInterno(
        string? valore,
        ReadOnlySpan<byte> aad,
        bool consentiStorico)
    {
        if (string.IsNullOrEmpty(valore)) return valore;

        if (valore.StartsWith(PrefissoV2, StringComparison.Ordinal))
        {
            var fineId = valore.IndexOf(':', PrefissoV2.Length);
            if (fineId <= PrefissoV2.Length || fineId == valore.Length - 1)
                throw ErroreDatoProtetto("formato v2 non valido");

            var idChiave = valore[PrefissoV2.Length..fineId];
            if (!_chiaviPerId.TryGetValue(idChiave, out var chiave))
                throw ErroreDatoProtetto(
                    $"chiave '{idChiave}' non disponibile; configurare temporaneamente " +
                    VariabileChiaviPrecedenti);

            return DecifraPacchetto(valore[(fineId + 1)..], chiave, aad);
        }

        if (valore.StartsWith(PrefissoV1, StringComparison.Ordinal))
        {
            if (!consentiStorico)
                throw ErroreDatoProtetto("formato storico v1 non ammesso dopo il cutover");

            // v1 non conteneva un ID: durante la rotazione proviamo il portachiavi. Il numero di
            // chiavi precedenti deve restare piccolo e temporaneo.
            foreach (var chiave in _chiaviPerId.Values)
            {
                try
                {
                    return DecifraPacchetto(valore[PrefissoV1.Length..], chiave, []);
                }
                catch (InvalidOperationException ex) when (ex.InnerException is CryptographicException)
                {
                    // Prova la chiave storica successiva.
                }
            }

            throw ErroreDatoProtetto(
                "nessuna chiave configurata riesce a decifrare il formato storico v1");
        }

        if (valore.StartsWith("enc:", StringComparison.Ordinal))
            throw ErroreDatoProtetto("versione di cifratura sconosciuta");

        if (!consentiStorico)
            throw ErroreDatoProtetto("testo in chiaro non ammesso dopo il cutover");

        return valore;
    }

    /// <summary>True per testo in chiaro, formato v1 o cifrato con una chiave precedente.</summary>
    public bool RichiedeRicifratura(string? valore) =>
        !string.IsNullOrEmpty(valore)
        && !valore.StartsWith(PrefissoChiaveCorrente, StringComparison.Ordinal);

    /// <summary>Porta un valore storico al formato e alla chiave correnti.</summary>
    public string? Ricifra(string? valore)
    {
        if (string.IsNullOrEmpty(valore)) return valore;
        if (!RichiedeRicifratura(valore))
        {
            // Valida comunque il tag: un pacchetto corrente corrotto non deve passare inosservato.
            _ = Svela(valore);
            return valore;
        }

        return CifraConChiaveCorrente(Svela(valore)!);
    }

    /// <summary>Ruota un envelope già tenant-bound mantenendo invariato il suo contesto AAD.</summary>
    public string? RicifraConContesto(string? valore, string contesto)
    {
        if (string.IsNullOrEmpty(valore)) return valore;
        ValidaContesto(contesto);
        var aad = Encoding.UTF8.GetBytes(contesto);
        try
        {
            return RicifraConContestoInterno(valore, aad);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(aad);
        }
    }

    /// <summary>Converter EF Core per una proprietà stringa.</summary>
    public ValueConverter<string?, string?> ConverterStringa() =>
        new(v => Proteggi(v), v => Svela(v));

    private string RicifraConContestoInterno(string valore, ReadOnlySpan<byte> aad)
    {
        if (!RichiedeRicifratura(valore))
        {
            _ = SvelaInterno(valore, aad, consentiStorico: false);
            return valore;
        }

        // Un envelope con vecchio kid ma già contestualizzato è decifrabile col medesimo AAD.
        var chiaro = SvelaInterno(valore, aad, consentiStorico: false)!;
        return CifraConChiaveCorrente(chiaro, aad);
    }

    private string CifraConChiaveCorrente(string valore, ReadOnlySpan<byte> aad = default)
    {
        var nonce = RandomNumberGenerator.GetBytes(DimensioneNonce);
        var testo = Encoding.UTF8.GetBytes(valore);
        var cifrato = new byte[testo.Length];
        var tag = new byte[DimensioneTag];

        try
        {
            using var aes = new AesGcm(_chiaveCorrente, DimensioneTag);
            aes.Encrypt(nonce, testo, cifrato, tag, aad);

            var pacchetto = new byte[DimensioneNonce + DimensioneTag + cifrato.Length];
            Buffer.BlockCopy(nonce, 0, pacchetto, 0, DimensioneNonce);
            Buffer.BlockCopy(tag, 0, pacchetto, DimensioneNonce, DimensioneTag);
            Buffer.BlockCopy(cifrato, 0, pacchetto, DimensioneNonce + DimensioneTag, cifrato.Length);
            return PrefissoChiaveCorrente + Convert.ToBase64String(pacchetto);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(testo);
        }
    }

    private static string DecifraPacchetto(
        string base64,
        byte[] chiave,
        ReadOnlySpan<byte> aad)
    {
        byte[] pacchetto;
        try
        {
            pacchetto = Convert.FromBase64String(base64);
        }
        catch (FormatException ex)
        {
            throw ErroreDatoProtetto("pacchetto Base64 non valido", ex);
        }

        if (pacchetto.Length < DimensioneNonce + DimensioneTag)
            throw ErroreDatoProtetto("pacchetto cifrato troncato");

        var nonce = pacchetto.AsSpan(0, DimensioneNonce);
        var tag = pacchetto.AsSpan(DimensioneNonce, DimensioneTag);
        var cifrato = pacchetto.AsSpan(DimensioneNonce + DimensioneTag);
        var testo = new byte[cifrato.Length];

        try
        {
            using var aes = new AesGcm(chiave, DimensioneTag);
            aes.Decrypt(nonce, cifrato, tag, testo, aad);
            return Encoding.UTF8.GetString(testo);
        }
        catch (CryptographicException ex)
        {
            throw ErroreDatoProtetto(
                "chiave non corrispondente oppure dato corrotto/manomesso", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(testo);
            CryptographicOperations.ZeroMemory(pacchetto);
        }
    }

    /// <summary>Factory host-aware: il chiamante decide se l'ambiente richiede una chiave reale.</summary>
    public static ProtettoreDati CreaDaAmbiente(bool produzione)
    {
        var passphrase = Environment.GetEnvironmentVariable("PAGHE_DATA_KEY") is { Length: > 0 } chiave
            ? chiave
            : PassphraseSviluppo;
        var precedenti = LeggiChiaviPrecedenti(
            Environment.GetEnvironmentVariable(VariabileChiaviPrecedenti));
        return new ProtettoreDati(passphrase, precedenti, produzione);
    }

    /// <summary>
    /// Solo gli ambienti esplicitamente locali/test possono usare la chiave demo. Staging, QA,
    /// nomi personalizzati e Production richiedono tutti una chiave casuale reale.
    /// </summary>
    public static bool RichiedeChiaveReale(string? ambiente) =>
        !string.Equals(ambiente, "Development", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(ambiente, "Test", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(ambiente, "Testing", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> LeggiChiaviPrecedenti(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            return JsonSerializer.Deserialize<string[]>(json)
                ?? throw new JsonException("Array JSON nullo.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"{VariabileChiaviPrecedenti} deve essere un array JSON di stringhe.", ex);
        }
    }

    private static void ValidaPassphraseCorrente(string passphrase, bool produzione)
    {
        if (string.IsNullOrWhiteSpace(passphrase))
            throw new ArgumentException("La passphrase di cifratura non può essere vuota.", nameof(passphrase));
        if (produzione && passphrase == PassphraseSviluppo)
            throw new InvalidOperationException(
                "PAGHE_DATA_KEY mancante in produzione: configurare una chiave di cifratura " +
                "robusta tramite il gestore di segreti.");
        if (produzione)
        {
            if (!ProvaDecodificaChiave(passphrase, out var chiave))
                throw new InvalidOperationException(
                    "PAGHE_DATA_KEY deve essere una chiave casuale di 32 byte codificata Base64 " +
                    "e custodita nel gestore di segreti.");
            CryptographicOperations.ZeroMemory(chiave);
        }
    }

    private static byte[] CreaChiaveCorrente(string passphrase, bool produzione)
    {
        if (ProvaDecodificaChiave(passphrase, out var chiaveRaw))
            return chiaveRaw;
        if (produzione)
            throw new InvalidOperationException("PAGHE_DATA_KEY di produzione non valida.");
        return DerivaPassphraseLegacy(passphrase);
    }

    private static byte[] DerivaPassphraseLegacy(string passphrase)
    {
        var bytes = Encoding.UTF8.GetBytes(passphrase);
        try
        {
            return SHA256.HashData(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static bool ProvaDecodificaChiave(string valore, out byte[] chiave)
    {
        try
        {
            chiave = Convert.FromBase64String(valore);
            if (chiave.Length == DimensioneChiave) return true;
            CryptographicOperations.ZeroMemory(chiave);
        }
        catch (FormatException)
        {
            // Non è una chiave raw Base64: può essere soltanto una passphrase legacy/development.
        }

        chiave = [];
        return false;
    }

    private static void AggiungiChiaveSeNuova(
        IDictionary<string, byte[]> chiavi,
        byte[] candidata)
    {
        var id = CalcolaIdChiave(candidata);
        if (!chiavi.TryAdd(id, candidata))
            CryptographicOperations.ZeroMemory(candidata);
    }

    private static string CalcolaIdChiave(ReadOnlySpan<byte> chiave)
    {
        Span<byte> impronta = stackalloc byte[32];
        SHA256.HashData(chiave, impronta);
        return Convert.ToHexString(impronta[..8]).ToLowerInvariant();
    }

    private static bool AmbienteProduzione()
    {
        var ambiente = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        // ASP.NET usa Production quando l'ambiente non è specificato: il protettore fa lo stesso.
        return RichiedeChiaveReale(ambiente);
    }

    private static void ValidaContesto(string contesto)
    {
        if (string.IsNullOrWhiteSpace(contesto))
            throw new ArgumentException("Il contesto AAD non può essere vuoto.", nameof(contesto));
    }

    private static InvalidOperationException ErroreDatoProtetto(
        string dettaglio,
        Exception? inner = null) =>
        new(
            "Impossibile decifrare un dato protetto: " + dettaglio + ".",
            inner);
}
