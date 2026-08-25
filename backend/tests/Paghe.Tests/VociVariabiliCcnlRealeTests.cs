using Paghe.Core.Parametri;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Golden test sulle voci variabili <b>verificate su buste reali</b> (docs/dati/voci-variabili-ccnl-2026.json).
/// A differenza dei minimi (pubblicati puliti dal CNEL), le voci variabili vanno confermate contro le
/// buste: qui la trasferta forfettaria autotrasporto e lo straordinario diurno provengono dalle buste
/// TORELLO/GIOVE e devono coincidere al centesimo. Evita di fidarsi di valori stimati/non verificati.
/// </summary>
public sealed class VociVariabiliCcnlRealeTests
{
    private static VociVariabiliCcnl Logistica()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "dati", "voci-variabili-ccnl-2026.json");
        var lista = VociVariabiliCcnlJson.Carica(File.ReadAllText(path));
        return Assert.Single(lista, c => c.Codice == "TRASPORTI_LOGISTICA");
    }

    [Fact]
    public void Logistica_TrasferteForfettarieAutotrasporto_CoincidonoConLaBustaReale()
    {
        var c = Logistica();

        // Busta TORELLO (autista C3): "Trasf.ItaliaForfet.Autotrasp 46,48000", "Trasf.EsteroForfet. 77,47000".
        // (Regime autotrasporto, distinto dalle fasce orarie generali del CCNL.)
        var italia = Assert.Single(c.Indennita!, i => i.Nome.Contains("Italia forfettaria autotrasporto"));
        var estero = Assert.Single(c.Indennita!, i => i.Nome.Contains("estero forfettaria autotrasporto"));
        Assert.Equal(46.48m, italia.Importo);
        Assert.Equal(77.47m, estero.Importo);
        Assert.Equal("giornaliera", italia.Tipo);
    }

    [Fact]
    public void Dataset_CaricaTuttiE5ICcnl()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "dati", "voci-variabili-ccnl-2026.json");
        var lista = VociVariabiliCcnlJson.Carica(File.ReadAllText(path));
        // 5 dalle buste reali + il Terziario/Commercio aggiunto con la revisione del 24/08/2026.
        Assert.Equal(6, lista.Count);
        var commercio = lista.Single(c => c.Codice == "COMMERCIO_TERZIARIO");
        Assert.Equal("triennale", commercio.Scatti!.Periodicita);
        Assert.Equal(10, commercio.Scatti.NumeroMassimo);
        Assert.Equal(7, commercio.MeseErogazioneQuattordicesima);
        Assert.Equal(7.3333m, commercio.OrePermessiMese);
    }

    [Fact]
    public void Logistica_StraordinarioDiurno_CoincideConLaBustaReale()
    {
        var c = Logistica();

        // Busta GIOVE (coop): "Str feriale diurno 30% lu-ve" → base 11,20200 × 1,30 = 14,56260.
        Assert.Equal(0.30m, c.Maggiorazioni!.StraordinarioDiurno);
    }
}
