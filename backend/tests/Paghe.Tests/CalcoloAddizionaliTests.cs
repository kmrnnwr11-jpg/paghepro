using Paghe.Core.Calcolo;
using Paghe.Core.Parametri;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Verifica del calcolo dell'addizionale a scaglioni + soglia di esenzione, con valori
/// calcolati a mano. Copre: aliquota unica, esenzione "a soglia", scaglioni progressivi, regime vuoto.
/// </summary>
public sealed class CalcoloAddizionaliTests
{
    [Fact]
    public void AliquotaUnica_SenzaEsenzione_SuInteroImponibile()
    {
        var regime = RegimeAddizionale.Unica(0.008m); // 0,80%
        Assert.Equal(160m, CalcoloAddizionali.Dovuta(20000m, regime)); // 20000 * 0,8%
    }

    [Fact]
    public void SogliaEsenzione_SottoSoglia_Zero_SopraSoglia_SuTutto()
    {
        var regime = RegimeAddizionale.Unica(0.006m, sogliaEsenzione: 12000m);
        Assert.Equal(0m, CalcoloAddizionali.Dovuta(10000m, regime));  // sotto soglia → esente
        Assert.Equal(0m, CalcoloAddizionali.Dovuta(12000m, regime));  // pari alla soglia → esente
        Assert.Equal(120m, CalcoloAddizionali.Dovuta(20000m, regime)); // sopra → 20000 * 0,6% (su tutto)
    }

    [Fact]
    public void Scaglioni_Progressivi()
    {
        var regime = new RegimeAddizionale(0m, new[]
        {
            new ScaglioneAddizionale(0m,     15000m, 0.004m),
            new ScaglioneAddizionale(15000m, 28000m, 0.006m),
            new ScaglioneAddizionale(28000m, 50000m, 0.007m),
            new ScaglioneAddizionale(50000m, null,   0.008m),
        });

        // 20000: 15000*0,4% + 5000*0,6% = 60 + 30 = 90
        Assert.Equal(90m, CalcoloAddizionali.Dovuta(20000m, regime));

        // 60000: 15000*0,4% + 13000*0,6% + 22000*0,7% + 10000*0,8% = 60 + 78 + 154 + 80 = 372
        Assert.Equal(372m, CalcoloAddizionali.Dovuta(60000m, regime));
    }

    [Fact]
    public void Scaglioni_ConEsenzione_SottoSoglia_Zero()
    {
        var regime = new RegimeAddizionale(15000m, new[]
        {
            new ScaglioneAddizionale(0m,     28000m, 0.006m),
            new ScaglioneAddizionale(28000m, null,   0.008m),
        });
        Assert.Equal(0m, CalcoloAddizionali.Dovuta(14000m, regime)); // sotto soglia
        // 30000: 28000*0,6% + 2000*0,8% = 168 + 16 = 184
        Assert.Equal(184m, CalcoloAddizionali.Dovuta(30000m, regime));
    }

    [Fact]
    public void RegimeVuoto_OppureRedditoNullo_Zero()
    {
        Assert.Equal(0m, CalcoloAddizionali.Dovuta(30000m, RegimeAddizionale.Nessuno));
        Assert.Equal(0m, CalcoloAddizionali.Dovuta(0m, RegimeAddizionale.Unica(0.008m)));
        Assert.Equal(0m, CalcoloAddizionali.Dovuta(-5m, RegimeAddizionale.Unica(0.008m)));
    }

    [Fact]
    public void Unica_ConAliquotaZero_DiventaNessuno()
    {
        Assert.Same(RegimeAddizionale.Nessuno, RegimeAddizionale.Unica(0m));
    }
}
