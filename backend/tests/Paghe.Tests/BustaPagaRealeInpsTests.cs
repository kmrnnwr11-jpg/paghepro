using Paghe.Core.Calcolo;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Golden test INPS su <b>buste reali</b>: con l'aliquota di settore (autotrasporto 9,49%) il motore
/// calcola gli stessi contributi c/dipendente delle 7 buste Logistica/Trasporti del Drive.
/// </summary>
public sealed class BustaPagaRealeInpsTests
{
    [Theory]
    // Logistica/Trasporti C3 — 2025 (imponibile contributivo arrotondato → INPS c/dipendente):
    [InlineData(2358.00, 223.77)] // gennaio
    [InlineData(2269.00, 215.33)] // febbraio
    [InlineData(2325.00, 220.64)] // marzo
    [InlineData(2304.00, 218.65)] // aprile
    [InlineData(2343.00, 222.35)] // maggio
    [InlineData(2425.00, 230.14)] // giugno
    [InlineData(2172.00, 206.12)] // luglio
    public void Inps_RiproduceLeBusteLogistica(double imponibileContributivo, double inpsRealeDip)
    {
        var aliquota = AliquoteInpsSettore.PerCcnl("TRASPORTI_LOGISTICA");
        Assert.NotNull(aliquota);

        var (dip, _) = CalcoloContributi.Calcola((decimal)imponibileContributivo, aliquota!);

        Assert.True(System.Math.Abs(dip - (decimal)inpsRealeDip) <= 0.02m,
            $"INPS c/dip: atteso ~{inpsRealeDip:F2} (busta reale), calcolato {dip:F2}");
    }
}
