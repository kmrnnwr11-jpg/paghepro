using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace Paghe.Firma;

/// <summary>
/// Motore di firma CAdES-BES (CMS / PKCS#7) con digest SHA-256, nel profilo accettato dal
/// Sistema di Interscambio per la fattura elettronica (<c>.xml.p7m</c>).
///
/// Usa solo le API del runtime .NET (<see cref="SignedCms"/>): nessuna dipendenza GPL/AGPL.
/// La chiave privata può essere quella del certificato (PFX) oppure una chiave esterna che
/// firma sull'hardware (smart card / CNS / HSM), passata in <paramref name="chiavePrivata"/>.
/// </summary>
public static class FirmaCades
{
    private const string OidSha256 = "2.16.840.1.101.3.4.2.1";
    private const string OidSigningCertificateV2 = "1.2.840.113549.1.9.16.2.47";

    /// <summary>
    /// Firma <paramref name="contenuto"/> e restituisce la busta CAdES-BES (CMS con contenuto
    /// incapsulato). Il certificato del firmatario viene incluso nella busta per la verifica.
    /// </summary>
    /// <param name="certificato">Certificato del firmatario (parte pubblica).</param>
    /// <param name="chiavePrivata">
    /// Chiave privata da usare per la firma. Se nulla viene usata quella contenuta nel
    /// certificato (caso PFX). Per smart card/CNS si passa la chiave fornita dal driver PKCS#11,
    /// così la firma avviene sul chip e la chiave non lascia mai la carta.
    /// </param>
    public static byte[] Firma(byte[] contenuto, X509Certificate2 certificato,
        AsymmetricAlgorithm? chiavePrivata = null)
    {
        ArgumentNullException.ThrowIfNull(contenuto);
        ArgumentNullException.ThrowIfNull(certificato);

        // detached:false => il contenuto è incapsulato nella busta (.p7m completo e autoconsistente).
        var cms = new SignedCms(new ContentInfo(contenuto), detached: false);

        var signer = chiavePrivata is null
            ? new CmsSigner(certificato)
            : new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificato, chiavePrivata);

        signer.DigestAlgorithm = new Oid(OidSha256);
        // Il certificato di firma DEVE essere incluso nella busta per consentire la verifica.
        signer.IncludeOption = X509IncludeOption.EndCertOnly;

        // Attributi firmati richiesti dal profilo CAdES-BES.
        signer.SignedAttributes.Add(new Pkcs9SigningTime(DateTime.UtcNow));
        // L'overload Add(AsnEncodedData) avvolge l'attributo senza nominare il tipo Pkcs.
        signer.SignedAttributes.Add(SigningCertificateV2(certificato));

        cms.ComputeSignature(signer);
        return cms.Encode();
    }

    /// <summary>
    /// Verifica una busta CAdES (<c>.p7m</c>): firma crittograficamente valida e contenuto integro.
    /// Non verifica la catena di certificazione né la revoca (verifica solo la firma).
    /// </summary>
    public static bool Verifica(byte[] bustaP7m)
    {
        ArgumentNullException.ThrowIfNull(bustaP7m);
        var cms = new SignedCms();
        cms.Decode(bustaP7m);
        try
        {
            cms.CheckSignature(verifySignatureOnly: true);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>Estrae il contenuto originale incapsulato in una busta CAdES (<c>.p7m</c>).</summary>
    public static byte[] EstraiContenuto(byte[] bustaP7m)
    {
        ArgumentNullException.ThrowIfNull(bustaP7m);
        var cms = new SignedCms();
        cms.Decode(bustaP7m);
        return cms.ContentInfo.Content;
    }

    // SigningCertificateV2 (RFC 5035): lega la firma al certificato tramite il suo hash SHA-256.
    // Struttura DER: SigningCertificateV2 ::= SEQUENCE { certs SEQUENCE OF ESSCertIDv2 }
    //               ESSCertIDv2 ::= SEQUENCE { hashAlgorithm (default SHA-256, omesso), certHash OCTET STRING }
    private static AsnEncodedData SigningCertificateV2(X509Certificate2 cert)
    {
        byte[] certHash = SHA256.HashData(cert.RawData);

        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())          // SigningCertificateV2
        {
            using (writer.PushSequence())      // certs: SEQUENCE OF ESSCertIDv2
            {
                using (writer.PushSequence())  // ESSCertIDv2 (hashAlgorithm SHA-256 = default, omesso)
                {
                    writer.WriteOctetString(certHash);
                }
            }
        }

        var oid = new Oid(OidSigningCertificateV2, "id-aa-signingCertificateV2");
        return new AsnEncodedData(oid, writer.Encode());
    }
}
