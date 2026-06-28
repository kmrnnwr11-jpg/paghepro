using Paghe.Core.AdeTelematico;
using Paghe.Core.Calcolo;
using Xunit;

namespace Paghe.Tests;

/// <summary>Test del calcolo di ravvedimento operoso (sanzione + interessi) e delle righe F24 derivate.</summary>
public sealed class RavvedimentoTests
{
    [Fact]
    public void Calcola_SanzioneFlatEInteressiLegali()
    {
        var scaglioni = new[] { new ScaglioneRavvedimento("Test", int.MaxValue, 0.03m) };

        var r = Ravvedimento.Calcola(
            importoTributo: 1000m,
            scadenza: new DateOnly(2025, 1, 1),
            versamento: new DateOnly(2026, 1, 1), // 365 giorni
            tassoLegaleAnnuo: 0.02m,
            scaglioni);

        Assert.Equal(365, r.GiorniRitardo);
        Assert.Equal(30.00m, r.Sanzione);            // 3% di 1000
        Assert.Equal(20.00m, r.Interessi);           // 1000 × 2% × 365/365
        Assert.Equal(1050.00m, r.Totale);            // 1000 + 30 + 20
    }

    [Fact]
    public void Calcola_Sprint_SanzioneGiornaliera()
    {
        var scaglioni = new[] { new ScaglioneRavvedimento("Sprint", 14, 0.001m, PerGiorno: true) };

        var r = Ravvedimento.Calcola(
            importoTributo: 1000m,
            scadenza: new DateOnly(2026, 6, 16),
            versamento: new DateOnly(2026, 6, 26), // 10 giorni
            tassoLegaleAnnuo: 0m,
            scaglioni);

        Assert.Equal("Sprint", r.ScaglioneApplicato);
        Assert.Equal(10.00m, r.Sanzione);            // 0,1% × 10 giorni × 1000
        Assert.Equal(1010.00m, r.Totale);
    }

    [Fact]
    public void Calcola_ScaglioneEsempio_30Giorni_BreveAll_1_5()
    {
        var r = Ravvedimento.Calcola(
            importoTributo: 1000m,
            scadenza: new DateOnly(2026, 6, 16),
            versamento: new DateOnly(2026, 7, 16), // 30 giorni
            tassoLegaleAnnuo: 0m,
            Ravvedimento.ScaglioniEsempioStorico);

        Assert.Equal("Breve (entro 30 giorni)", r.ScaglioneApplicato);
        Assert.Equal(15.00m, r.Sanzione);            // 1,5% di 1000
    }

    [Fact]
    public void Calcola_NessunRitardo_Lancia()
    {
        Assert.Throws<ArgumentException>(() => Ravvedimento.Calcola(
            1000m, new DateOnly(2026, 6, 16), new DateOnly(2026, 6, 16),
            0.02m, Ravvedimento.ScaglioniEsempioStorico));
    }

    [Fact]
    public void VociErario_ProduceTributoSanzioneInteressi()
    {
        var r = new RisultatoRavvedimento(365, "Test", Tributo: 1000m, Sanzione: 30m, Interessi: 20m, Totale: 1050m);

        var voci = RavvedimentoF24.VociErario(r, "1040", 2025, "8906", "1989");

        Assert.Equal(3, voci.Count);
        Assert.Equal(("1040", 1000m), (voci[0].CodiceTributo, voci[0].ImportoDebito));
        Assert.Equal(("8906", 30m), (voci[1].CodiceTributo, voci[1].ImportoDebito));
        Assert.Equal(("1989", 20m), (voci[2].CodiceTributo, voci[2].ImportoDebito));
    }

    [Fact]
    public void VociErario_InteressiZero_OmetteLaRiga()
    {
        var r = new RisultatoRavvedimento(5, "Sprint", Tributo: 1000m, Sanzione: 5m, Interessi: 0m, Totale: 1005m);

        var voci = RavvedimentoF24.VociErario(r, "1040", 2026, "8906", "1989");

        Assert.Equal(2, voci.Count); // niente riga interessi
        Assert.DoesNotContain(voci, v => v.CodiceTributo == "1989");
    }
}
