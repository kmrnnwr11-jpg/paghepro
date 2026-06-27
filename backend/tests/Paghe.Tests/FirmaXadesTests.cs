using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Paghe.Firma;
using Xunit;

namespace Paghe.Tests;

public class FirmaXadesTests
{
    private static FirmatorePfx FirmatoreDiProva()
    {
        using var rsa = RSA.Create(2048);
        var richiesta = new CertificateRequest(
            "CN=Mario Rossi Firma Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = richiesta.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return FirmatorePfx.DaBytes(cert.Export(X509ContentType.Pkcs12, "prova"), "prova");
    }

    private const string XmlDiProva =
        "<FatturaElettronica><Body>contenuto-originale</Body></FatturaElettronica>";

    [Fact]
    public void FirmaXades_ProduceXmlFirmato_Verificabile()
    {
        using var firmatore = FirmatoreDiProva();

        var r = firmatore.Firma(Encoding.UTF8.GetBytes(XmlDiProva), "fattura.xml", TipoFirma.Xades);

        Assert.Equal("XAdES", r.Formato);
        Assert.Equal("application/xml", r.Mime);
        Assert.Equal("fattura-firmato.xml", r.NomeFile);
        Assert.Contains("Signature", Encoding.UTF8.GetString(r.ContenutoFirmato));

        var verifica = MotoreFirma.Verifica(r.ContenutoFirmato);
        Assert.True(verifica.Valido);
        Assert.Equal("XAdES", verifica.Formato);
        Assert.Contains("Mario Rossi", verifica.Firmatario);
        Assert.NotNull(verifica.DataFirmaUtc);
    }

    [Fact]
    public void VerificaXades_XmlManomesso_RestituisceFalse()
    {
        using var firmatore = FirmatoreDiProva();
        byte[] firmato = firmatore.Firma(Encoding.UTF8.GetBytes(XmlDiProva), "fattura.xml", TipoFirma.Xades).ContenutoFirmato;

        // Altera il contenuto firmato: la firma enveloped non deve più risultare valida.
        string manomesso = Encoding.UTF8.GetString(firmato).Replace("contenuto-originale", "contenuto-alterato");

        var verifica = MotoreFirma.Verifica(Encoding.UTF8.GetBytes(manomesso));
        Assert.False(verifica.Valido);
    }

    [Fact]
    public void Verifica_RiconosceFormatoCades_E_NeEstraeIDettagli()
    {
        using var firmatore = FirmatoreDiProva();
        byte[] p7m = firmatore.Firma(Encoding.UTF8.GetBytes(XmlDiProva), "fattura.xml").ContenutoFirmato;

        var verifica = MotoreFirma.Verifica(p7m);

        Assert.True(verifica.Valido);
        Assert.Equal("CAdES", verifica.Formato);
        Assert.Contains("Mario Rossi", verifica.Firmatario);
        Assert.NotNull(verifica.DataFirmaUtc);
    }
}
