using Paghe.Core.Parametri;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Verifica che il caricatore delle voci variabili CCNL deserializzi esattamente il formato JSON
/// del "Prompt 2" (raccolta dati da fonti ufficiali), così è pronto quando arrivano i dati reali.
/// </summary>
public sealed class VociVariabiliCcnlTests
{
    [Fact]
    public void Carica_DeserializzaIlFormatoDelPrompt2()
    {
        const string json = """
            [
              {
                "codice": "TRASPORTI_LOGISTICA",
                "anno": 2026,
                "fonte": "https://www.cnel.it/... (esempio)",
                "orarioSettimanale": 39,
                "divisoreOrario": 168,
                "numeroMensilita": 14,
                "maggiorazioni": {
                  "straordinarioDiurno": 0.30, "straordinarioNotturno": 0.50, "lavoroFestivo": 0.65,
                  "lavoroNotturno": 0.28, "lavoroDomenicale": 0.10, "straordinarioFestivo": 0.55
                },
                "indennita": [
                  { "nome": "Trasferta Italia forfettaria", "tipo": "giornaliera", "importo": 46.48 },
                  { "nome": "Indennità di disagio art.16", "tipo": "giornaliera", "importo": 0.93 }
                ],
                "scatti": { "numeroMassimo": 5, "periodicita": "biennale" }
              }
            ]
            """;

        var lista = VociVariabiliCcnlJson.Carica(json);

        var c = Assert.Single(lista);
        Assert.Equal("TRASPORTI_LOGISTICA", c.Codice);
        Assert.Equal(2026, c.Anno);
        Assert.Equal(39m, c.OrarioSettimanale);
        Assert.Equal(14, c.NumeroMensilita);
        Assert.Equal(0.65m, c.Maggiorazioni!.LavoroFestivo);
        Assert.Equal(0.28m, c.Maggiorazioni!.LavoroNotturno);
        Assert.Equal(2, c.Indennita!.Count);
        Assert.Equal("Trasferta Italia forfettaria", c.Indennita[0].Nome);
        Assert.Equal("giornaliera", c.Indennita[0].Tipo);
        Assert.Equal(46.48m, c.Indennita[0].Importo);
        Assert.Equal(5, c.Scatti!.NumeroMassimo);
        Assert.Equal("biennale", c.Scatti!.Periodicita);
    }

    [Fact]
    public void Carica_Vuoto_RestituisceListaVuota() =>
        Assert.Empty(VociVariabiliCcnlJson.Carica(""));
}
