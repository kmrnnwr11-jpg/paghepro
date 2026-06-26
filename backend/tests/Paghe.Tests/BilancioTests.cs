using Paghe.Core.Contabilita;
using Xunit;

namespace Paghe.Tests;

public class BilancioTests
{
    private static Bilancio BilancioEsempio() =>
        RiclassificazioneBilancio.Riclassifica(
            SeedPianoConti.PianoConti(), SeedPianoConti.SaldiEsempio());

    [Fact]
    public void Riclassifica_ContoEconomico_ValoriAttesi()
    {
        var b = BilancioEsempio();

        Assert.Equal(200000m, b.ValoreDellaProduzione);
        Assert.Equal(170000m, b.CostiDellaProduzione);   // 120.000 + 50.000
        Assert.Equal(30000m, b.DifferenzaValoreCosti);
        Assert.Equal(-2000m, b.ProventiOneriFinanziari); // 0 proventi - 2.000 oneri
        Assert.Equal(28000m, b.RisultatoPrimaDelleImposte);
        Assert.Equal(8000m, b.Imposte);
        Assert.Equal(20000m, b.UtilePerditaEsercizio);
    }

    [Fact]
    public void Riclassifica_StatoPatrimoniale_UtileNelPatrimonioNetto()
    {
        var b = BilancioEsempio();

        Assert.Equal(50000m, b.Immobilizzazioni);
        Assert.Equal(50000m, b.AttivoCircolante);        // 30.000 + 20.000
        Assert.Equal(100000m, b.TotaleAttivo);

        // PN = capitale 40.000 + utile 20.000 = 60.000
        Assert.Equal(60000m, b.PatrimonioNetto);
        Assert.Equal(100000m, b.TotalePassivo);          // 60.000 + 10.000 TFR + 30.000 debiti
    }

    [Fact]
    public void Riclassifica_IlBilancioQuadra()
    {
        var b = BilancioEsempio();
        Assert.Equal(b.TotaleAttivo, b.TotalePassivo);
        Assert.Equal(0m, b.SbilancioAttivoPassivo);
        Assert.True(b.Quadra);
    }

    [Fact]
    public void Riclassifica_InPerdita_PatrimonioNettoSiRiduce_EQuadra()
    {
        // Ricavi inferiori ai costi: 100.000 di ricavi a fronte di 170.000 di costi.
        // Perché il dare/avere quadri, la banca è uno scoperto di -70.000
        // (= perdita d'esercizio assorbita dalla liquidità).
        var piano = SeedPianoConti.PianoConti();
        var saldi = new[]
        {
            new SaldoConto("10.10",  50000m),
            new SaldoConto("20.10",  30000m),
            new SaldoConto("20.20", -70000m),  // banca negativa (scoperto) per far quadrare il dare/avere
            new SaldoConto("30.10",  40000m),
            new SaldoConto("40.10",  10000m),
            new SaldoConto("50.10",  30000m),
            new SaldoConto("60.10", 100000m),  // ricavi
            new SaldoConto("70.10", 120000m),
            new SaldoConto("70.20",  50000m),
        };

        var b = RiclassificazioneBilancio.Riclassifica(piano, saldi);

        Assert.Equal(-70000m, b.UtilePerditaEsercizio);          // 100.000 - 170.000
        Assert.Equal(-30000m, b.PatrimonioNetto);                // 40.000 - 70.000
        Assert.True(b.Quadra);
    }

    [Fact]
    public void Riclassifica_ContoNonInPiano_LanciaEccezione()
    {
        var piano = SeedPianoConti.PianoConti();
        var saldi = new[] { new SaldoConto("99.99", 1000m) };

        Assert.Throws<ArgumentException>(() =>
            RiclassificazioneBilancio.Riclassifica(piano, saldi));
    }
}
