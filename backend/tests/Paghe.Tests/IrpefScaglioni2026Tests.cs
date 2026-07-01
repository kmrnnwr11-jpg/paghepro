using Paghe.Core.Calcolo;
using Paghe.Core.Parametri;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Blinda gli scaglioni IRPEF 2026: 23% (0-28.000), <b>33%</b> (28.000-50.000, sceso dal 35% del
/// 2024-2025 con la Legge di Bilancio 2026), 43% (oltre 50.000). Le buste reali del Drive sono tutte
/// sotto i 28.000 e non coprivano il secondo scaglione: questo test evita che il 33% regredisca.
/// </summary>
public sealed class IrpefScaglioni2026Tests
{
    [Theory]
    [InlineData(28000.0, 6440.0)]   // tutto al 23%
    [InlineData(40000.0, 10400.0)]  // 6.440 + 12.000×33%
    [InlineData(50000.0, 13700.0)]  // punto di controllo ufficiale: 28.000×23% + 22.000×33%
    [InlineData(60000.0, 18000.0)]  // 13.700 + 10.000×43%
    public void IrpefLordaAnnua_2026_SecondaAliquotaAl33(double imponibileAnnuo, double lordaAttesa)
    {
        var scaglioni = Seed.Parametri2026().ScaglioniIrpef;

        var lorda = CalcoloIrpef.IrpefLordaAnnua((decimal)imponibileAnnuo, scaglioni);

        Assert.True(System.Math.Abs(lorda - (decimal)lordaAttesa) <= 0.01m,
            $"IRPEF lorda annua 2026: atteso {lordaAttesa:F2}, calcolato {lorda:F2}");
    }
}
