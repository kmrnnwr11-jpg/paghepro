using System.Xml.Linq;
using Paghe.Core.Fatturazione;
using Xunit;

namespace Paghe.Tests;

public class FatturaPaXmlTests
{
    private static Fattura Fattura() => new()
    {
        Numero = "1/2026",
        Data = new DateOnly(2026, 6, 26),
        Cedente = new DatiAnagrafici
        {
            Denominazione = "Alfa Srl", PartitaIva = "01234567897",
            Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
        },
        Cessionario = new DatiAnagrafici
        {
            Denominazione = "Beta Spa", PartitaIva = "09876543217",
            Indirizzo = "Via Po 2", Cap = "10100", Comune = "Torino", Provincia = "TO",
        },
        CodiceDestinatario = "ABCDEFG",
        Righe =
        {
            new RigaFattura { Descrizione = "Consulenza", PrezzoUnitario = 100m, AliquotaIva = 22m },
            new RigaFattura { Descrizione = "Materiali",  PrezzoUnitario = 50m,  AliquotaIva = 10m },
        },
    };

    [Fact]
    public void Genera_RadiceEVersioneCorrette()
    {
        var xml = FatturaPaXml.Genera(Fattura());
        var doc = XDocument.Parse(xml);

        Assert.NotNull(doc.Root);
        Assert.Equal("FatturaElettronica", doc.Root!.Name.LocalName);
        Assert.Equal("FPR12", doc.Root.Attribute("versione")?.Value);
    }

    [Fact]
    public void Genera_ContieneDatiDocumento()
    {
        var doc = XDocument.Parse(FatturaPaXml.Genera(Fattura()));

        Assert.Equal("1/2026", doc.Descendants("Numero").First().Value);
        Assert.Equal("2026-06-26", doc.Descendants("Data").First().Value);
        Assert.Equal("TD01", doc.Descendants("TipoDocumento").First().Value);
        // Totale documento: 100 + 22 + 50 + 5 = 177,00
        Assert.Equal("177.00", doc.Descendants("ImportoTotaleDocumento").First().Value);
    }

    [Fact]
    public void Genera_UnaLineaPerRiga()
    {
        var doc = XDocument.Parse(FatturaPaXml.Genera(Fattura()));
        Assert.Equal(2, doc.Descendants("DettaglioLinee").Count());
    }

    [Fact]
    public void Genera_UnRiepilogoPerAliquota()
    {
        var doc = XDocument.Parse(FatturaPaXml.Genera(Fattura()));
        var riepiloghi = doc.Descendants("DatiRiepilogo").ToList();
        Assert.Equal(2, riepiloghi.Count);

        // Importi imponibili presenti nei riepiloghi.
        var imponibili = doc.Descendants("ImponibileImporto").Select(e => e.Value).ToList();
        Assert.Contains("100.00", imponibili);
        Assert.Contains("50.00", imponibili);
    }

    [Fact]
    public void Genera_ContieneAnagraficheCedenteECessionario()
    {
        var xml = FatturaPaXml.Genera(Fattura());
        Assert.Contains("Alfa Srl", xml);
        Assert.Contains("Beta Spa", xml);
        Assert.Contains("01234567897", xml);
        Assert.Contains("ABCDEFG", xml); // codice destinatario
    }
}
