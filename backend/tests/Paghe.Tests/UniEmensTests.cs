using System.Xml.Linq;
using Paghe.Core.AdeTelematico.UniEmens;
using Xunit;

namespace Paghe.Tests;

public class UniEmensTests
{
    private static FlussoUniEmens FlussoEsempio() =>
        UniEmensDaCedolini.Costruisci(
            new DatiMittenteUniEmens("STDPLG80A01F205X", "Studio Paghe"),
            new DenunciaAziendaleUniEmens("1234567890", "01567890123", 2026, 5),
            new[]
            {
                new RigaUniEmens("RSSMRA85M01H501Z", "Rossi", "Mario", "1",
                    ImponibileInps: 2000m, ContributiDipendente: 184m, ContributiDatore: 600m,
                    GiorniRetribuiti: 26, SettimaneUtili: 4),
                new RigaUniEmens("VRDLGI90A41F205Y", "Verdi", "Luigi", "2",
                    ImponibileInps: 1500m, ContributiDipendente: 138m, ContributiDatore: 450m,
                    GiorniRetribuiti: 26, SettimaneUtili: 4),
            });

    [Fact]
    public void Costruisci_SommaContributiDipendenteEDatore()
    {
        var flusso = FlussoEsempio();

        Assert.Equal(2, flusso.Lavoratori.Count);
        var mario = flusso.Lavoratori[0];
        Assert.Equal(2000m, mario.Imponibile);
        Assert.Equal(784m, mario.Contributo);   // 184 + 600
    }

    [Fact]
    public void Costruisci_SenzaLavoratori_LanciaEccezione()
    {
        Assert.Throws<ArgumentException>(() => UniEmensDaCedolini.Costruisci(
            new DatiMittenteUniEmens("CF", "Mittente"),
            new DenunciaAziendaleUniEmens("1234567890", "01567890123", 2026, 5),
            Array.Empty<RigaUniEmens>()));
    }

    [Fact]
    public void Xml_HaRadiceEUnaDenunciaPerLavoratore()
    {
        var xml = UniEmensXmlWriter.Genera(FlussoEsempio());
        var radice = xml.Root!;

        Assert.Equal("UniEmens", radice.Name.LocalName);
        Assert.Equal("1234567890", radice.Element("DenunciaAziendale")!.Element("Matricola")!.Value);
        Assert.Equal("2026-05", radice.Element("DenunciaAziendale")!.Element("AnnoMese")!.Value);

        var individuali = radice.Element("DenunceIndividuali")!.Elements("DenunciaIndividuale").ToList();
        Assert.Equal(2, individuali.Count);
        Assert.Equal("RSSMRA85M01H501Z", individuali[0].Element("CodiceFiscale")!.Value);
        Assert.Equal("2000.00", individuali[0].Element("DatiRetributivi")!.Element("Imponibile")!.Value);
        Assert.Equal("784.00", individuali[0].Element("DatiRetributivi")!.Element("Contributo")!.Value);
    }

    [Fact]
    public void Xml_DichiaraEsplicitamenteCheNonEValidato()
    {
        var s = UniEmensXmlWriter.GeneraStringa(FlussoEsempio());
        Assert.Contains("NON validata", s);   // avviso nel commento XML
    }
}
