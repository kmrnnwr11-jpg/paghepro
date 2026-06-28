using Paghe.Core.Calcolo;
using Paghe.Core.Parametri;
using Xunit;

namespace Paghe.Tests;

public class CalcoloTfrTests
{
    private static readonly ScaglioneIrpef[] Scaglioni2026 =
    [
        new(0m, 28000m, 0.23m),
        new(28000m, 50000m, 0.35m),
        new(50000m, null, 0.43m),
    ];

    [Fact]
    public void Rivalutazione_CoefficienteEImpostaSostitutiva()
    {
        // coeff = 1,5% + 75% × 1% = 2,25% ; lorda = 10000 × 2,25% = 225
        // imposta sost. 17% = 38,25 ; netta = 186,75 ; fondo = 10186,75
        var r = CalcoloTfr.Rivalutazione(fondoIniziale: 10000m, variazioneIstat: 0.01m);

        Assert.Equal(0.0225m, r.Coefficiente);
        Assert.Equal(225.00m, r.RivalutazioneLorda);
        Assert.Equal(38.25m, r.ImpostaSostitutiva);
        Assert.Equal(186.75m, r.RivalutazioneNetta);
        Assert.Equal(10186.75m, r.FondoRivalutato);
    }

    [Fact]
    public void TassazioneSeparata_AliquotaMediaSulRedditoDiRiferimento()
    {
        // RR = 20000 × 12 / 10 = 24000 → IRPEF 23% = 5520 → aliquota media 23%
        // imposta = 20000 × 23% = 4600 ; netto = 15400
        var t = CalcoloTfr.TassazioneSeparata(tfrImponibile: 20000m, anniAnzianita: 10m, Scaglioni2026);

        Assert.Equal(24000m, t.RedditoRiferimento);
        Assert.Equal(0.23m, t.AliquotaMedia);
        Assert.Equal(4600.00m, t.Imposta);
        Assert.Equal(15400.00m, t.NettoTfr);
    }

    [Fact]
    public void TassazioneSeparata_AnniNonPositivi_Lancia()
    {
        Assert.Throws<ArgumentException>(() =>
            CalcoloTfr.TassazioneSeparata(10000m, 0m, Scaglioni2026));
    }

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
