using Paghe.Core.Calcolo;
using Paghe.Core.Parametri;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Golden test su <b>buste paga reali</b> (anonimizzate, da gestionali diversi — TeamSystem e Sistemi):
/// dato l'imponibile fiscale del mese, il motore deve calcolare la <b>stessa IRPEF lorda</b> della busta.
/// Prova concreta che la matematica fiscale del motore riproduce la realtà.
/// Vedi docs/buste-reali/. (Lordo, INPS di settore ed elementi CCNL restano da modellare: vedi note.)
/// </summary>
public sealed class BustaPagaRealeIrpefTests
{
    [Theory]
    // 8 buste paga reali (Drive del fondatore), 2 gestionali e 2 CCNL diversi.
    // Logistica/Trasporti, Operaio-Autista C3 — 2025 (gennaio→luglio):
    [InlineData(2137.86, 491.71)] // gennaio
    [InlineData(2057.14, 473.14)] // febbraio
    [InlineData(2108.04, 484.85)] // marzo
    [InlineData(2088.65, 480.39)] // aprile
    [InlineData(2124.50, 488.64)] // maggio
    [InlineData(2198.77, 505.72)] // giugno
    [InlineData(1968.90, 452.85)] // luglio
    // Multiservizi/Pulizie, Operaio liv. 3 — maggio 2026:
    [InlineData(1523.24, 350.35)]
    public void IrpefLorda_RiproduceLeBusteReali(double imponibileMese, double irpefLordaRealeMese)
    {
        var scaglioni = Seed.Parametri2026().ScaglioniIrpef;

        decimal lordaAnnua = CalcoloIrpef.IrpefLordaAnnua((decimal)imponibileMese * 12m, scaglioni);
        decimal lordaMese = lordaAnnua / 12m;

        Assert.True(System.Math.Abs(lordaMese - (decimal)irpefLordaRealeMese) <= 0.02m,
            $"IRPEF lorda mese: atteso ~{irpefLordaRealeMese:F2} (busta reale), calcolato {lordaMese:F2}");
    }
}
