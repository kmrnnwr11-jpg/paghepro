using Paghe.Core.Calcolo;
using Paghe.Core.Domain;
using Paghe.Core.Parametri;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Golden test INPS su <b>buste reali</b>: la quota c/dipendente è base (9,19%) + 0,30% CIG (aziende
/// &gt;15 dip) + fondi di settore (FIS 0,2667% Multiservizi, FSBS 0,15% somministrazione). Con questa
/// composizione il motore riproduce i contributi delle buste reali del Drive al centesimo.
/// </summary>
public sealed class BustaPagaRealeInpsTests
{
    [Theory]
    // imponibile contributivo · azienda >15 dip · fondi di settore · INPS c/dip reale
    // Logistica/Trasporti (9,19% + 0,30% = 9,49%) — 2025:
    [InlineData(2358.00, true, 0.0, 223.77)] // gennaio
    [InlineData(2269.00, true, 0.0, 215.33)] // febbraio
    [InlineData(2325.00, true, 0.0, 220.64)] // marzo
    [InlineData(2304.00, true, 0.0, 218.65)] // aprile
    [InlineData(2343.00, true, 0.0, 222.35)] // maggio
    [InlineData(2425.00, true, 0.0, 230.14)] // giugno
    [InlineData(2172.00, true, 0.0, 206.12)] // luglio
    // Multiservizi/Pulizie (Dussmann) — 9,19% + 0,30% + FIS 0,2667% = 9,7567% — 2026:
    [InlineData(1333.00, true, 0.002667, 130.06)] // gennaio
    [InlineData(1200.00, true, 0.002667, 117.08)] // febbraio
    [InlineData(1235.00, true, 0.002667, 120.50)] // marzo
    // Somministrazione (Zucchetti) — 9,19% + FSBS 0,15% = 9,34% (no CIGS) — 2026:
    [InlineData(1835.00, false, 0.0015, 171.39)] // aprile
    [InlineData(1823.00, false, 0.0015, 170.26)] // marzo
    [InlineData(1675.00, false, 0.0015, 156.44)] // febbraio
    public void Inps_RiproduceLeBusteReali_PerSettore(
        double imponibileContributivo, bool oltre15Dipendenti, double fondiSettore, double inpsRealeDip)
    {
        var parametri = Seed.Parametri2026();
        var baseDip = parametri.AliquotaInps(Qualifica.Operaio).Dipendente; // 9,19%
        var quota = CalcoloContributi.QuotaInpsDipendente(baseDip, oltre15Dipendenti, (decimal)fondiSettore);

        var (dip, _) = CalcoloContributi.Calcola((decimal)imponibileContributivo, new AliquotaInps(quota, 0.28m));

        Assert.True(System.Math.Abs(dip - (decimal)inpsRealeDip) <= 0.02m,
            $"INPS c/dip: atteso ~{inpsRealeDip:F2} (busta reale), calcolato {dip:F2}");
    }
}
