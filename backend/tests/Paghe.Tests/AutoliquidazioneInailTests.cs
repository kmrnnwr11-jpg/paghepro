using Paghe.Core.AdeTelematico;
using Xunit;

namespace Paghe.Tests;

/// <summary>Test del calcolo dell'autoliquidazione del premio INAIL.</summary>
public sealed class AutoliquidazioneInailTests
{
    [Fact]
    public void Calcola_RegolazioneERata_DalTassoPerMille()
    {
        // 100.000 € × 12,5‰ = 1.250 regolazione; rata anticipata identica (presunto = consuntivo).
        var r = AutoliquidazioneInail.Calcola(100_000m, tassoPerMille: 12.5m);

        Assert.Equal(1250m, r.Regolazione);
        Assert.Equal(1250m, r.RataAnticipata);
        Assert.Equal(2500m, r.TotaleDaVersare);
    }

    [Fact]
    public void Calcola_RataGiaVersata_ScomputataDalTotale()
    {
        var r = AutoliquidazioneInail.Calcola(100_000m, tassoPerMille: 12.5m, rataGiaVersata: 1200m);

        Assert.Equal(1250m, r.Regolazione);
        Assert.Equal(1300m, r.TotaleDaVersare); // 1250 − 1200 + 1250
    }

    [Fact]
    public void Calcola_ArrotondaAlCentesimo()
    {
        // 33.333 × 7,77‰ = 258,997... → 259,00 (arrotondamento al centesimo)
        var r = AutoliquidazioneInail.Calcola(33_333m, tassoPerMille: 7.77m);

        Assert.Equal(259.00m, r.Regolazione);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Calcola_TassoNonPositivo_Lancia(double tasso)
    {
        Assert.Throws<ArgumentException>(() => AutoliquidazioneInail.Calcola(1000m, (decimal)tasso));
    }

    [Fact]
    public void Calcola_RetribuzioniNegative_Lancia()
    {
        Assert.Throws<ArgumentException>(() => AutoliquidazioneInail.Calcola(-1m, 10m));
    }
}
