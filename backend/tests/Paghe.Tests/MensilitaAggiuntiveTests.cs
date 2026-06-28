using Paghe.Core.Calcolo;
using Xunit;

namespace Paghe.Tests;

/// <summary>Test dei ratei delle mensilità aggiuntive (13ª/14ª).</summary>
public sealed class MensilitaAggiuntiveTests
{
    [Fact]
    public void RateoMensile_UnDodicesimo()
    {
        Assert.Equal(130.00m, MensilitaAggiuntive.RateoMensile(1560m)); // 1560 / 12
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(6, 780)]
    [InlineData(12, 1560)]
    public void Maturato_ProporzionaleAiMesi(int mesi, decimal atteso)
    {
        Assert.Equal(atteso, MensilitaAggiuntive.Maturato(1560m, mesi));
    }

    [Fact]
    public void Maturato_PeriodoNonStandard_14a()
    {
        // 14ª con periodo di 12 mesi, 4 mesi maturati: 1560 × 4/12 = 520
        Assert.Equal(520.00m, MensilitaAggiuntive.Maturato(1560m, 4, mesiPeriodo: 12));
    }

    [Fact]
    public void Maturato_MesiNegativi_Lancia()
    {
        Assert.Throws<ArgumentException>(() => MensilitaAggiuntive.Maturato(1560m, -1));
    }
}
