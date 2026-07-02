using Paghe.Core.Calcolo;
using Paghe.Core.Parametri;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Verifica sui <b>dati reali</b> MEF incorporati nell'assembly: il caricatore legge i file
/// dell'anno e produce regimi corretti per comuni/regioni noti (spot-check calcolati a mano
/// dai valori pubblicati).
/// </summary>
public sealed class CaricaRegimiMefTests
{
    [Fact]
    public void Comunali2026_CaricaMigliaiaDiComuni()
    {
        var regimi = CaricaRegimiMef.PerAnno(2026);
        Assert.True(regimi.Comunali.Count > 7000,
            $"Attesi >7000 comuni, trovati {regimi.Comunali.Count}");
    }

    [Fact]
    public void Comunale2026_AliquotaUnica_Abbateggio()
    {
        var r = CaricaRegimiMef.PerAnno(2026).Comunali["A008"]; // Abbateggio (PE): 0,50% unica
        Assert.Equal(100m, CalcoloAddizionali.Dovuta(20000m, r)); // 20000 * 0,5%
    }

    [Fact]
    public void Comunale2026_EsenzioneASoglia_AbbadiaSanSalvatore()
    {
        var r = CaricaRegimiMef.PerAnno(2026).Comunali["A006"]; // esente < 12.000, poi 0,60%
        Assert.Equal(12000m, r.SogliaEsenzione);
        Assert.Equal(0m, CalcoloAddizionali.Dovuta(11000m, r));
        Assert.Equal(120m, CalcoloAddizionali.Dovuta(20000m, r));
    }

    [Fact]
    public void Comunale2026_ScaglioniConEsenzioneDaFascia_Agordo()
    {
        // Agordo (BL): esenzione < 10.000 (indicata nella fascia, non in IMPORTO_ESENTE),
        // scaglioni 0,63 / 0,76 / 0,79 / 0,80.
        var r = CaricaRegimiMef.PerAnno(2026).Comunali["A083"];
        Assert.Equal(10000m, r.SogliaEsenzione);
        Assert.Equal(4, r.Scaglioni.Count);
        Assert.Equal(0m, CalcoloAddizionali.Dovuta(9000m, r)); // sotto soglia
        // 30000: 15000*0,63% + 13000*0,76% + 2000*0,79% = 209,10
        Assert.Equal(209.10m, CalcoloAddizionali.Dovuta(30000m, r));
    }

    [Fact]
    public void Regionale2025_Lombardia_QuattroScaglioni()
    {
        var r = CaricaRegimiMef.PerAnno(2025).Regionali["Lombardia"]; // 1,23 / 1,58 / 1,72 / 1,73
        Assert.Equal(4, r.Scaglioni.Count);
        // 20000: 15000*1,23% + 5000*1,58% = 263,50
        Assert.Equal(263.50m, CalcoloAddizionali.Dovuta(20000m, r));
    }

    [Fact]
    public void Regionale2026_RegioniPrincipaliPresenti()
    {
        var reg = CaricaRegimiMef.PerAnno(2026).Regionali;
        foreach (var nome in new[] { "Lombardia", "Lazio", "Campania", "Veneto" })
        {
            Assert.True(reg.ContainsKey(nome), $"Manca la regione {nome}");
            Assert.NotEmpty(reg[nome].Scaglioni);
        }
    }

    [Fact]
    public void Comunale2026_CoperturaCompleta_GrandiCittaENuoveFusioni()
    {
        var comunali = CaricaRegimiMef.PerAnno(2026).Comunali;

        // Copertura nazionale: il dataset MEF 2026 elenca ~7.897 comuni.
        Assert.True(comunali.Count >= 7800,
            $"Copertura comuni incompleta: {comunali.Count} (attesi >= 7800)");

        // Grandi città sempre presenti.
        foreach (var codice in new[] { "H501" /*Roma*/, "F205" /*Milano*/, "F839" /*Napoli*/,
                                       "L219" /*Torino*/, "G273" /*Palermo*/, "A944" /*Bologna*/ })
            Assert.True(comunali.ContainsKey(codice), $"Manca il comune {codice}");

        // Comuni nati dalle fusioni 2021-2025 (codici M432-M439): devono avere un regime.
        foreach (var codice in new[] { "M432", "M433", "M434", "M435", "M436", "M437", "M438", "M439" })
            Assert.True(comunali.ContainsKey(codice), $"Manca il comune da fusione {codice}");
    }
}
