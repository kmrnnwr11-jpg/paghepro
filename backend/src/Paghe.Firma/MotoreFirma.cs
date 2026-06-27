using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Paghe.Firma;

/// <summary>
/// Orchestratore di firma: dato un certificato (e l'eventuale chiave su hardware) produce la
/// firma nel formato richiesto (CAdES <c>.p7m</c> o XAdES XML) e ne descrive l'esito.
/// È il punto unico usato da <see cref="FirmatorePfx"/> e <see cref="FirmatoreCns"/>.
/// </summary>
public static class MotoreFirma
{
    public static RisultatoFirma Firma(
        byte[] contenuto, string? nomeFileOriginale, TipoFirma tipo,
        X509Certificate2 certificato, AsymmetricAlgorithm? chiavePrivata = null)
    {
        string firmatario = certificato.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        var ora = DateTime.UtcNow;

        return tipo switch
        {
            TipoFirma.Xades => new RisultatoFirma(
                FirmaXades.Firma(contenuto, certificato, chiavePrivata),
                NomeFileFirmato.PerXadesXml(nomeFileOriginale),
                firmatario, ora, "SHA-256", "XAdES", "application/xml"),

            _ => new RisultatoFirma(
                FirmaCades.Firma(contenuto, certificato, chiavePrivata),
                NomeFileFirmato.PerP7m(nomeFileOriginale),
                firmatario, ora, "SHA-256", "CAdES", "application/pkcs7-mime"),
        };
    }

    /// <summary>
    /// Verifica un documento firmato riconoscendone automaticamente il formato: una busta CAdES
    /// (DER, inizia con SEQUENCE) oppure un XML con firma XAdES/XML-DSig.
    /// </summary>
    public static RisultatoVerifica Verifica(byte[] documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        return SembraBustaCades(documento)
            ? FirmaCades.VerificaConDettagli(documento)
            : FirmaXades.VerificaConDettagli(documento);
    }

    // Un .p7m DER inizia con il tag SEQUENCE (0x30); un XML inizia con '<' (0x3C), eventualmente
    // preceduto da BOM o spazi bianchi.
    private static bool SembraBustaCades(byte[] d)
    {
        int i = 0;
        while (i < d.Length &&
               (d[i] is 0xEF or 0xBB or 0xBF or (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r'))
            i++;
        return i < d.Length && d[i] == 0x30;
    }
}
