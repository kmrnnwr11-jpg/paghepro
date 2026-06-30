using System.Text.Json;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Golden test sul dataset <b>ufficiale</b> dei minimi tabellari (docs/dati/minimi-ccnl-2026.json, fonti
/// CNEL/associazioni). Verifica che il file sia valido e <b>importabile</b> nel formato di
/// <c>CcnlImportService</c>, e che i valori coincidano con le <b>buste reali</b> del Drive.
/// Esempio chiave: Logistica C3 = paga base 1971,61 + EDR 10,04 della busta TORELLO TRASPORTI (autista C3).
/// </summary>
public sealed class MinimiCcnlUfficialiTests
{
    private sealed record LivelloJson(
        string Livello, string? Descrizione, decimal? MinimoTabellare, decimal? Edr, decimal? ImportoScatto);
    private sealed record CcnlJson(
        string Codice, string? Descrizione, int Anno, string? Fonte, List<LivelloJson> Livelli);

    private static List<CcnlJson> CaricaDataset()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "dati", "minimi-ccnl-2026.json");
        var json = File.ReadAllText(path);
        var lista = JsonSerializer.Deserialize<List<CcnlJson>>(
            json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(lista);
        return lista!;
    }

    [Fact]
    public void Dataset_ContieneIDieciCcnlConILivelliAttesi()
    {
        var lista = CaricaDataset();

        var conteggi = lista.ToDictionary(c => c.Codice, c => c.Livelli.Count);
        Assert.Equal(10, lista.Count);
        Assert.Equal(23, conteggi["TRASPORTI_LOGISTICA"]);
        Assert.Equal(8, conteggi["MULTISERVIZI_PULIZIE"]);
        Assert.Equal(7, conteggi["EDILIZIA_INDUSTRIA"]);
        Assert.Equal(9, conteggi["METALMECCANICI_INDUSTRIA"]);
        Assert.Equal(3, conteggi["AGRICOLTURA_OPERAI"]);
        Assert.Equal(8, conteggi["COMMERCIO_TERZIARIO"]);
        Assert.Equal(8, conteggi["STUDI_PROFESSIONALI"]);
        Assert.Equal(10, conteggi["TURISMO_PUBBLICI_ESERCIZI"]);
        Assert.Equal(15, conteggi["CHIMICO_FARMACEUTICO"]);
        Assert.Equal(8, conteggi["ALIMENTARE_INDUSTRIA"]);
    }

    [Theory]
    // codice CCNL · livello · minimo atteso · EDR atteso — valori riscontrati su buste reali del Drive
    [InlineData("TRASPORTI_LOGISTICA", "C3", 1971.61, 10.04)]   // = paga base + EDR busta TORELLO (autista C3)
    [InlineData("MULTISERVIZI_PULIZIE", "1", 1296.39, 10.33)]   // EDR 10,33 = busta Dussmann (pulizie)
    [InlineData("METALMECCANICI_INDUSTRIA", "C3", 2211.43, 0.0)]
    [InlineData("EDILIZIA_INDUSTRIA", "1", 1630.23, 10.33)]
    public void Livello_HaIMinimiUfficiali(string codice, string livello, double minimo, double edr)
    {
        var lista = CaricaDataset();

        var ccnl = Assert.Single(lista, c => c.Codice == codice);
        var liv = Assert.Single(ccnl.Livelli, l => l.Livello == livello);

        Assert.Equal((decimal)minimo, liv.MinimoTabellare);
        Assert.Equal((decimal)edr, liv.Edr);
    }
}
