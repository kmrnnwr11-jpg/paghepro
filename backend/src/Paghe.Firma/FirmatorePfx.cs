using System.Security.Cryptography.X509Certificates;

namespace Paghe.Firma;

/// <summary>
/// Firmatario che usa un certificato software in formato PKCS#12 (<c>.pfx</c>/<c>.p12</c>).
///
/// Adatto a server, automazioni, test e certificati di firma remota esportabili. Per la firma
/// a valore legale con la carta del titolare (CNS/smart card) usare <see cref="FirmatoreCns"/>.
/// </summary>
public sealed class FirmatorePfx : IFirmatoreDigitale, IDisposable
{
    private readonly X509Certificate2 _certificato;

    public FirmatorePfx(X509Certificate2 certificato)
    {
        ArgumentNullException.ThrowIfNull(certificato);
        if (!certificato.HasPrivateKey)
            throw new ArgumentException(
                "Il certificato PFX non contiene la chiave privata.", nameof(certificato));
        _certificato = certificato;
    }

    /// <summary>Carica un firmatario da un file PFX/P12 protetto da password.</summary>
    public static FirmatorePfx DaFile(string percorso, string password) =>
        new(X509CertificateLoader.LoadPkcs12FromFile(percorso, password));

    /// <summary>Carica un firmatario dal contenuto di un PFX/P12 (es. caricato via upload).</summary>
    public static FirmatorePfx DaBytes(byte[] pfx, string password) =>
        new(X509CertificateLoader.LoadPkcs12(pfx, password));

    public RisultatoFirma Firma(byte[] contenuto, string? nomeFileOriginale = null)
    {
        byte[] p7m = FirmaCades.Firma(contenuto, _certificato);
        return new RisultatoFirma(
            p7m,
            NomeFileFirmato.PerP7m(nomeFileOriginale),
            _certificato.GetNameInfo(X509NameType.SimpleName, forIssuer: false),
            DateTime.UtcNow,
            "SHA-256");
    }

    public void Dispose() => _certificato.Dispose();
}
