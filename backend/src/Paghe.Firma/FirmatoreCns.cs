using System.Security.Cryptography.X509Certificates;
using Net.Pkcs11Interop.X509Store;

namespace Paghe.Firma;

/// <summary>
/// Firmatario che usa una smart card / CNS (Carta Nazionale dei Servizi) tramite il middleware
/// PKCS#11 del produttore della carta. La chiave privata NON lascia mai la carta: l'hash del
/// documento viene firmato dal chip dopo la verifica del PIN.
///
/// <para><b>Requisiti a runtime</b>: il modulo PKCS#11 del middleware (file <c>.so</c>/<c>.dll</c>/
/// <c>.dylib</c>) installato sulla macchina che esegue la firma, più lettore e carta inseriti.
/// Per questo la firma CNS funziona in contesto desktop o tramite un agente locale sul PC
/// dell'utente, e NON in un backend cloud dove la carta fisica è assente. In SaaS la firma
/// lato server si fa con un certificato PFX/firma remota (<see cref="FirmatorePfx"/>).</para>
/// </summary>
public sealed class FirmatoreCns : IFirmatoreDigitale
{
    private readonly string _libreriaPkcs11;
    private readonly string _pin;
    private readonly string? _etichettaCertificato;

    /// <param name="libreriaPkcs11">
    /// Percorso del modulo PKCS#11 del middleware della carta. Esempi: Bit4id
    /// <c>libbit4xpki.dylib</c>/<c>bit4xpki.dll</c>, Oberthur <c>libASEP11.dylib</c>,
    /// CRS/Comuni <c>OCSCryptoki</c>. Va indicato il percorso del file del produttore della CNS.
    /// </param>
    /// <param name="pin">PIN di firma della carta.</param>
    /// <param name="etichettaCertificato">
    /// Etichetta (label) del certificato di firma da usare. Se nulla, viene scelto il primo
    /// certificato idoneo alla firma (uso "non ripudio" / "firma digitale"). Le CNS hanno spesso
    /// due certificati: uno di autenticazione e uno di sottoscrizione; questa selezione preferisce
    /// quello di sottoscrizione.
    /// </param>
    public FirmatoreCns(string libreriaPkcs11, string pin, string? etichettaCertificato = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libreriaPkcs11);
        ArgumentException.ThrowIfNullOrWhiteSpace(pin);
        _libreriaPkcs11 = libreriaPkcs11;
        _pin = pin;
        _etichettaCertificato = etichettaCertificato;
    }

    public RisultatoFirma Firma(byte[] contenuto, string? nomeFileOriginale = null)
    {
        using var store = new Pkcs11X509Store(_libreriaPkcs11, new PinFisso(_pin));

        var cert = SelezionaCertificato(store)
            ?? throw new InvalidOperationException(
                "Nessun certificato di firma trovato sulla carta CNS/smart card. " +
                "Verificare che la carta sia inserita e che il middleware PKCS#11 sia corretto.");

        using var chiave = cert.GetPrivateKey();
        X509Certificate2 pubblico = cert.Info.ParsedCertificate;

        byte[] p7m = FirmaCades.Firma(contenuto, pubblico, chiave);
        return new RisultatoFirma(
            p7m,
            NomeFileFirmato.PerP7m(nomeFileOriginale),
            pubblico.GetNameInfo(X509NameType.SimpleName, forIssuer: false),
            DateTime.UtcNow,
            "SHA-256");
    }

    private Pkcs11X509Certificate? SelezionaCertificato(Pkcs11X509Store store)
    {
        var disponibili = store.Slots
            .Where(s => s.Token is not null)
            .SelectMany(s => s.Token.Certificates)
            .Where(c => c.HasPrivateKeyObject)
            .ToList();

        if (_etichettaCertificato is not null)
            return disponibili.FirstOrDefault(c =>
                string.Equals(c.Info.Label, _etichettaCertificato, StringComparison.OrdinalIgnoreCase));

        // Preferisci il certificato di sottoscrizione (uso "non ripudio"); in subordine il primo idoneo.
        return disponibili.FirstOrDefault(EUsoSottoscrizione) ?? disponibili.FirstOrDefault();
    }

    private static bool EUsoSottoscrizione(Pkcs11X509Certificate cert)
    {
        foreach (var estensione in cert.Info.ParsedCertificate.Extensions)
            if (estensione is X509KeyUsageExtension ku)
                return ku.KeyUsages.HasFlag(X509KeyUsageFlags.NonRepudiation);
        return false;
    }

    /// <summary>Fornisce sempre lo stesso PIN al middleware PKCS#11 durante la sessione di firma.</summary>
    private sealed class PinFisso(string pin) : IPinProvider
    {
        private readonly byte[] _pin = System.Text.Encoding.UTF8.GetBytes(pin);

        public GetPinResult GetTokenPin(
            Pkcs11X509StoreInfo storeInfo, Pkcs11SlotInfo slotInfo, Pkcs11TokenInfo tokenInfo)
            => new(cancel: false, pin: _pin);

        public GetPinResult GetKeyPin(
            Pkcs11X509StoreInfo storeInfo, Pkcs11SlotInfo slotInfo, Pkcs11TokenInfo tokenInfo,
            Pkcs11X509CertificateInfo certificateInfo)
            => new(cancel: false, pin: _pin);
    }
}
