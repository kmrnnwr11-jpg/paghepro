using Paghe.Core.Calcolo;
using Paghe.Core.Parametri;
using Xunit;

namespace Paghe.Tests;

public class CalcoloContributiTests
{
    [Fact]
    public void Calcola_ApplicaAliquoteEArrotonda()
    {
        var aliquota = new AliquotaInps(Dipendente: 0.0919m, Datore: 0.2788m);

        var (dip, datore) = CalcoloContributi.Calcola(2000m, aliquota);

        // 2000 * 9,19% = 183,80 ; 2000 * 27,88% = 557,60
        Assert.Equal(183.80m, dip);
        Assert.Equal(557.60m, datore);
    }

    [Fact]
    public void Calcola_ImponibileZero_ContributiZero()
    {
        var aliquota = new AliquotaInps(0.0919m, 0.2788m);
        var (dip, datore) = CalcoloContributi.Calcola(0m, aliquota);
        Assert.Equal(0m, dip);
        Assert.Equal(0m, datore);
    }
}
