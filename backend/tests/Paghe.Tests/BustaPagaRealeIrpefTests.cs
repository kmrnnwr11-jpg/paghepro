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
    // 26 buste paga reali (Drive del fondatore) da 5 gestionali diversi — TeamSystem, Sistemi/JOB,
    // SeaSoft, INAZ, Zucchetti — e settori diversi (Logistica, Multiservizi, Agricoltura,
    // somministrazione, Edilizia, Metalmeccanico): l'IRPEF lorda torna al centesimo su tutte.
    // Logistica/Trasporti, Operaio-Autista C3 — 2025 (gennaio→luglio):
    [InlineData(2137.86, 491.71)] // gennaio
    [InlineData(2057.14, 473.14)] // febbraio
    [InlineData(2108.04, 484.85)] // marzo
    [InlineData(2088.65, 480.39)] // aprile
    [InlineData(2124.50, 488.64)] // maggio
    [InlineData(2198.77, 505.72)] // giugno
    [InlineData(1968.90, 452.85)] // luglio
    // Multiservizi/Pulizie, Operaio liv. 3 — 2026 (marzo, aprile, maggio):
    [InlineData(1614.87, 371.42)] // marzo
    [InlineData(1657.23, 381.16)] // aprile
    [InlineData(1523.24, 350.35)] // maggio
    // Logistica (coop, liv.5) — Sistemi/JOB — 2026:
    [InlineData(2013.55, 463.12)] // marzo
    [InlineData(1998.91, 459.75)] // aprile
    [InlineData(1544.80, 355.30)] // febbraio
    // Autotrasporto merci C3 — SeaSoft — marzo 2026:
    [InlineData(1838.81, 422.93)]
    // Multiservizi/Pulizie (Dussmann) — INAZ — 2026:
    [InlineData(1202.61, 276.60)] // gennaio
    [InlineData(1083.33, 249.17)] // febbraio
    [InlineData(1114.62, 256.36)] // marzo
    // Agricoltura OTD — INAZ — 2025:
    [InlineData(610.63, 140.44)]  // ottobre
    [InlineData(1232.21, 283.41)] // novembre
    // Somministrazione, Magazziniere D1 — Zucchetti — 2026:
    [InlineData(1642.92, 377.87)] // aprile
    [InlineData(1632.44, 375.46)] // marzo
    [InlineData(1518.89, 349.34)] // febbraio
    // Edilizia (Cassa Edile), Operaio comune — Zucchetti — gennaio 2026:
    [InlineData(184.38, 42.41)]
    // Metalmeccanico/cavi (turni notturni), Operaio liv. H — Zucchetti — 2025/2026:
    [InlineData(1819.29, 418.44)] // gennaio 2026
    [InlineData(1855.53, 426.77)] // febbraio 2026
    [InlineData(1282.55, 294.99)] // dicembre 2025
    public void IrpefLorda_RiproduceLeBusteReali(double imponibileMese, double irpefLordaRealeMese)
    {
        var scaglioni = Seed.Parametri2026().ScaglioniIrpef;

        decimal lordaAnnua = CalcoloIrpef.IrpefLordaAnnua((decimal)imponibileMese * 12m, scaglioni);
        decimal lordaMese = lordaAnnua / 12m;

        Assert.True(System.Math.Abs(lordaMese - (decimal)irpefLordaRealeMese) <= 0.02m,
            $"IRPEF lorda mese: atteso ~{irpefLordaRealeMese:F2} (busta reale), calcolato {lordaMese:F2}");
    }
}
