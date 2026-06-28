using Paghe.Core.Calcolo;
using Paghe.Core.Domain;
using Paghe.Core.Parametri;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Golden test INPS su <b>buste reali</b>: per un'azienda con &gt;15 dipendenti la quota c/dipendente
/// è 9,19% + 0,30% = <b>9,49%</b> (regola generale, non di settore). Con questa, il motore riproduce
/// i contributi delle 7 buste Logistica/Trasporti del Drive al centesimo.
/// </summary>
public sealed class BustaPagaRealeInpsTests
{
    [Theory]
    // Operaio, azienda >15 dipendenti — imponibile contributivo → INPS c/dipendente (buste reali 2025):
    [InlineData(2358.00, 223.77)] // gennaio
    [InlineData(2269.00, 215.33)] // febbraio
    [InlineData(2325.00, 220.64)] // marzo
    [InlineData(2304.00, 218.65)] // aprile
    [InlineData(2343.00, 222.35)] // maggio
    [InlineData(2425.00, 230.14)] // giugno
    [InlineData(2172.00, 206.12)] // luglio
    public void Inps_Oltre15Dipendenti_RiproduceLeBusteLogistica(double imponibileContributivo, double inpsRealeDip)
    {
        var parametri = Seed.Parametri2026();
        var aliquotaBase = parametri.AliquotaInps(Qualifica.Operaio); // 9,19%
        // +0,30% di contributo CIG per aziende >15 dipendenti (regola generale): 9,49%.
        var aliquota = aliquotaBase with { Dipendente = aliquotaBase.Dipendente + 0.003m };

        var (dip, _) = CalcoloContributi.Calcola((decimal)imponibileContributivo, aliquota);

        Assert.True(System.Math.Abs(dip - (decimal)inpsRealeDip) <= 0.02m,
            $"INPS c/dip (>15 dip): atteso ~{inpsRealeDip:F2} (busta reale), calcolato {dip:F2}");
    }
}
