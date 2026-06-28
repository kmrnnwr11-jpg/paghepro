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
    [InlineData(2137.86, 491.71)] // Logistica/Trasporti, Operaio-Autista C3 — Gennaio 2025
    [InlineData(2057.14, 473.14)] // Logistica/Trasporti, Operaio-Autista C3 — Febbraio 2025
    [InlineData(1523.24, 350.35)] // Multiservizi/Pulizie, Operaio liv. 3 — Maggio 2026
    public void IrpefLorda_RiproduceLeBusteReali(double imponibileMese, double irpefLordaRealeMese)
    {
        var scaglioni = Seed.Parametri2026().ScaglioniIrpef;

        decimal lordaAnnua = CalcoloIrpef.IrpefLordaAnnua((decimal)imponibileMese * 12m, scaglioni);
        decimal lordaMese = lordaAnnua / 12m;

        Assert.True(System.Math.Abs(lordaMese - (decimal)irpefLordaRealeMese) <= 0.02m,
            $"IRPEF lorda mese: atteso ~{irpefLordaRealeMese:F2} (busta reale), calcolato {lordaMese:F2}");
    }
}
