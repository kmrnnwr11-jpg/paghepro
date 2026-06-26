using Paghe.Core.Calcolo;
using Xunit;

namespace Paghe.Tests;

public class CalcoloTfrTests
{
    [Fact]
    public void QuotaMensile_CalcoloConContributoFondo()
    {
        // retribuzione 1500 x 14 = 21000 annua
        // 21000 / 13,5 = 1555,5556 ; contributo 21000 * 0,5% = 105
        // (1555,5556 - 105) / 12 = 120,8796 -> 120,88
        decimal quota = CalcoloTfr.QuotaMensile(
            retribuzioneUtileMensile: 1500m, numeroMensilita: 14, divisore: 13.5m, contributoFondo: 0.005m);

        Assert.Equal(120.88m, quota);
    }

    [Fact]
    public void QuotaMensile_RetribuzioneZero_Zero()
    {
        Assert.Equal(0m, CalcoloTfr.QuotaMensile(0m, 14, 13.5m, 0.005m));
    }
}
