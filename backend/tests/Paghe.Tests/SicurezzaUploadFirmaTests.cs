using System.Text;
using Microsoft.AspNetCore.Http;
using Paghe.Api;

namespace Paghe.Tests;

public sealed class SicurezzaUploadFirmaTests
{
    [Fact]
    public void ValidaDimensione_RifiutaFileVuotoEOltreLimite()
    {
        Assert.Contains("mancante", SicurezzaUploadFirma.ValidaDimensione(null, 100, "File"));

        var grande = File(new byte[101], "documento.xml");
        Assert.Contains("troppo grande", SicurezzaUploadFirma.ValidaDimensione(grande, 100, "File"));
    }

    [Fact]
    public void RiconoscimentoPfx_NonSiFidaDiNomeOMime()
    {
        Assert.False(SicurezzaUploadFirma.SembraPkcs12("non-un-pfx"u8));
        Assert.True(SicurezzaUploadFirma.SembraPkcs12(new byte[] { 0x30, 0x02, 0x01, 0x00 }));
    }

    [Theory]
    [InlineData("<Signature />", true)]
    [InlineData("  \r\n<Signature />", true)]
    [InlineData("testo arbitrario", false)]
    public void RiconoscimentoDocumentoFirmato_DistingueXmlDaTesto(string contenuto, bool atteso)
    {
        Assert.Equal(atteso,
            SicurezzaUploadFirma.SembraDocumentoFirmato(Encoding.UTF8.GetBytes(contenuto)));
    }

    private static FormFile File(byte[] contenuto, string nome)
    {
        var stream = new MemoryStream(contenuto);
        return new FormFile(stream, 0, contenuto.Length, "file", nome);
    }
}
