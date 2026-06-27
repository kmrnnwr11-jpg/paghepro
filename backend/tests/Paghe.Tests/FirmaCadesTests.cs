using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Paghe.Firma;
using Xunit;

namespace Paghe.Tests;

public class FirmaCadesTests
{
    // Certificato di test auto-firmato con chiave privata, ricaricato da PKCS#12 come accade in produzione.
    private static FirmatorePfx FirmatoreDiProva(string cn = "CN=Mario Rossi Firma Test")
    {
        using var rsa = RSA.Create(2048);
        var richiesta = new CertificateRequest(cn, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        richiesta.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.NonRepudiation | X509KeyUsageFlags.DigitalSignature, critical: true));
        using var cert = richiesta.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        byte[] pfx = cert.Export(X509ContentType.Pkcs12, "prova");
        return FirmatorePfx.DaBytes(pfx, "prova");
    }

    [Fact]
    public void Firma_ProduceBustaP7m_VerificabileEConContenutoIntegro()
    {
        using var firmatore = FirmatoreDiProva();
        byte[] documento = Encoding.UTF8.GetBytes("<FatturaElettronica>contenuto di prova</FatturaElettronica>");

        var risultato = firmatore.Firma(documento, "IT01234567890_00001.xml");

        Assert.True(FirmaCades.Verifica(risultato.ContenutoFirmato));
        Assert.Equal(documento, FirmaCades.EstraiContenuto(risultato.ContenutoFirmato));
        Assert.Equal("IT01234567890_00001.xml.p7m", risultato.NomeFile);
        Assert.Equal("SHA-256", risultato.AlgoritmoDigest);
        Assert.Contains("Mario Rossi", risultato.Firmatario);
    }

    [Fact]
    public void Firma_UsaDigestSha256_EAttributoSigningCertificateV2()
    {
        using var firmatore = FirmatoreDiProva();

        var risultato = firmatore.Firma(Encoding.UTF8.GetBytes("x"), "doc.xml");

        var cms = new SignedCms();
        cms.Decode(risultato.ContenutoFirmato);
        Assert.Single(cms.SignerInfos);
        var firma = cms.SignerInfos[0];

        // Digest SHA-256.
        Assert.Equal("2.16.840.1.101.3.4.2.1", firma.DigestAlgorithm.Value);
        // Attributo CAdES-BES: id-aa-signingCertificateV2.
        Assert.Contains(firma.SignedAttributes.Cast<CryptographicAttributeObject>(),
            a => a.Oid.Value == "1.2.840.113549.1.9.16.2.47");
        // Il certificato del firmatario è incluso nella busta.
        Assert.NotEmpty(cms.Certificates);
    }

    [Fact]
    public void Verifica_BustaManomessa_RestituisceFalse()
    {
        using var firmatore = FirmatoreDiProva();
        byte[] busta = firmatore.Firma(Encoding.UTF8.GetBytes("contenuto-originale"), "doc.xml").ContenutoFirmato;

        // Altera il contenuto incapsulato (stessa lunghezza): la firma non deve più risultare valida.
        int i = IndiceDi(busta, Encoding.UTF8.GetBytes("originale"));
        Assert.True(i >= 0);
        busta[i] ^= 0x01;

        Assert.False(FirmaCades.Verifica(busta));
    }

    [Fact]
    public void Verifica_BytesNonValidi_RestituisceFalseSenzaEccezioni()
    {
        Assert.False(FirmaCades.Verifica(new byte[] { 1, 2, 3, 4 }));
    }

    [Fact]
    public void NomeFileFirmato_PerP7m_AggiungeEstensione()
    {
        Assert.Equal("IT0123_00001.xml.p7m", NomeFileFirmato.PerP7m("IT0123_00001.xml"));
        Assert.Equal("documento.p7m", NomeFileFirmato.PerP7m(null));
        Assert.Equal("documento.p7m", NomeFileFirmato.PerP7m("  "));
    }

    private static int IndiceDi(byte[] sorgente, byte[] cerca)
    {
        for (int i = 0; i <= sorgente.Length - cerca.Length; i++)
        {
            bool trovato = true;
            for (int j = 0; j < cerca.Length; j++)
                if (sorgente[i + j] != cerca[j]) { trovato = false; break; }
            if (trovato) return i;
        }
        return -1;
    }
}
