using Paghe.Core.Calcolo;
using Paghe.Core.Parametri;
using Xunit;

namespace Paghe.Tests;

public class CalcoloIrpefTests
{
    private static readonly IReadOnlyList<ScaglioneIrpef> Scaglioni = new[]
    {
        new ScaglioneIrpef(0m,      28000m, 0.23m),
        new ScaglioneIrpef(28000m,  50000m, 0.35m),
        new ScaglioneIrpef(50000m,  null,   0.43m),
    };

    [Fact]
    public void IrpefLordaAnnua_RedditoZeroONegativo_RestituisceZero()
    {
        Assert.Equal(0m, CalcoloIrpef.IrpefLordaAnnua(0m, Scaglioni));
        Assert.Equal(0m, CalcoloIrpef.IrpefLordaAnnua(-100m, Scaglioni));
    }

    [Theory]
    [InlineData(28000, 6440)]    // 28000 * 23%
    [InlineData(30000, 7140)]    // 6440 + 2000*35%
    [InlineData(50000, 14140)]   // 6440 + 22000*35%
    [InlineData(60000, 18440)]   // 14140 + 10000*43%
    public void IrpefLordaAnnua_ScaglioniMultipli_CalcoloCorretto(double reddito, double atteso)
    {
        decimal risultato = CalcoloIrpef.IrpefLordaAnnua((decimal)reddito, Scaglioni);
        Assert.Equal((decimal)atteso, risultato);
    }

    [Fact]
    public void DetrazioneLavoroDipendente_RedditoBasso_1955()
    {
        Assert.Equal(1955m, CalcoloIrpef.DetrazioneLavoroDipendenteAnnua(10000m, 365));
    }

    [Fact]
    public void DetrazioneLavoroDipendente_FasciaIntermedia_FormulaDecrescente()
    {
        // 1910 + 1190 * (28000-20000)/13000 = 1910 + 732,3077 = 2642,3077
        Assert.Equal(2642.31m, CalcoloIrpef.DetrazioneLavoroDipendenteAnnua(20000m, 365), 2);
    }

    [Fact]
    public void DetrazioneLavoroDipendente_Fascia25k35k_AggiungeUlteriore65()
    {
        // 1910 * (50000-30000)/22000 + 65 = 1736,3636 + 65 = 1801,3636
        Assert.Equal(1801.36m, CalcoloIrpef.DetrazioneLavoroDipendenteAnnua(30000m, 365), 2);
    }

    [Fact]
    public void DetrazioneLavoroDipendente_RedditoOltre50k_Zero()
    {
        Assert.Equal(0m, CalcoloIrpef.DetrazioneLavoroDipendenteAnnua(55000m, 365));
    }

    [Fact]
    public void DetrazioneLavoroDipendente_GiorniRidotti_Proporzionale()
    {
        // 1955 * 180/365 = 964,1096
        Assert.Equal(964.11m, CalcoloIrpef.DetrazioneLavoroDipendenteAnnua(10000m, 180), 2);
    }
}
