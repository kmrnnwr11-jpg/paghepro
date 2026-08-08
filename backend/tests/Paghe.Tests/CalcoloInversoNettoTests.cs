using Paghe.Core.Calcolo;
using Paghe.Core.Domain;
using Paghe.Core.Parametri;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Calcolo inverso netto → lordo: dato il netto voluto, trova il superminimo che lo produce.
///
/// Il vincolo importante è il pavimento contrattuale: sotto il netto prodotto dal minimo
/// tabellare il calcolo dichiara che l'importo non è raggiungibile, invece di inventare una
/// retribuzione inferiore al CCNL.
/// </summary>
public class CalcoloInversoNettoTests
{
    private static MotoreCalcoloCedolino Motore() =>
        new(Seed.Parametri2026(), Seed.CcnlCommercio2026());

    private static RapportoLavoro Impiegato3Liv() => new()
    {
        CodiceCcnl = "COMMERCIO_TERZIARIO",
        Livello = "3",
        Qualifica = Qualifica.Impiegato,
        NumeroMensilita = 14,
        Regione = "Lombardia",
        CodiceCatastaleComune = "F205",
    };

    private static Cartellino Giugno() => new() { Anno = 2026, Mese = 6 };

    [Fact]
    public void TrovaIlSuperminimoCheProduceIlNettoVoluto()
    {
        var esito = CalcoloInversoNetto.Calcola(Motore(), Impiegato3Liv(), Giugno(), 1500m);

        Assert.True(esito.Raggiungibile);
        Assert.InRange(esito.NettoOttenuto, 1499.98m, 1500.02m);
        Assert.True(esito.SuperminimoNecessario > 0m);
    }

    [Fact]
    public void IlSuperminimoTrovatoRicalcolatoDaDavveroQuelNetto()
    {
        // Verifica di chiusura: il risultato deve reggere se lo si rimette nel motore.
        var esito = CalcoloInversoNetto.Calcola(Motore(), Impiegato3Liv(), Giugno(), 1800m);
        var ricalcolato = Motore().Calcola(
            Impiegato3Liv().ConSuperminimo(esito.SuperminimoNecessario), Giugno());

        Assert.InRange(ricalcolato.NettoInBusta, 1799.90m, 1800.10m);
    }

    [Fact]
    public void NettoSottoIlMinimoContrattuale_NonERaggiungibile()
    {
        // Il caso che deve fallire: 300 € per un full-time inquadrato al 3° livello.
        var esito = CalcoloInversoNetto.Calcola(Motore(), Impiegato3Liv(), Giugno(), 300m);

        Assert.False(esito.Raggiungibile);
        Assert.Equal(0m, esito.SuperminimoNecessario);
        Assert.Contains("minimo", esito.Nota, StringComparison.OrdinalIgnoreCase);
        // Il netto riportato è quello vero del minimo tabellare, non l'importo chiesto.
        Assert.True(esito.NettoOttenuto > 300m);
    }

    [Fact]
    public void PartTime_AbbassaIlPavimentoContrattuale()
    {
        // Lo stesso importo irraggiungibile a tempo pieno può esserlo con orario ridotto:
        // è la strada corretta, cioè ridurre le ore contrattuali.
        var rapporto = new RapportoLavoro
        {
            CodiceCcnl = "COMMERCIO_TERZIARIO",
            Livello = "3",
            Qualifica = Qualifica.Impiegato,
            NumeroMensilita = 14,
            Regione = "Lombardia",
            CodiceCatastaleComune = "F205",
            PartTime = true,
            PercentualePartTime = 30m,
        };

        var pieno = CalcoloInversoNetto.Calcola(Motore(), Impiegato3Liv(), Giugno(), 400m);
        var ridotto = CalcoloInversoNetto.Calcola(Motore(), rapporto, Giugno(), 400m);

        Assert.False(pieno.Raggiungibile);
        Assert.True(ridotto.NettoOttenuto < pieno.NettoOttenuto);
    }

    [Fact]
    public void NettoNegativoOZero_Rifiutato()
    {
        Assert.Throws<ArgumentException>(() =>
            CalcoloInversoNetto.Calcola(Motore(), Impiegato3Liv(), Giugno(), 0m));
        Assert.Throws<ArgumentException>(() =>
            CalcoloInversoNetto.Calcola(Motore(), Impiegato3Liv(), Giugno(), -100m));
    }

    [Fact]
    public void RiportaAncheLordoECostoAzienda()
    {
        var esito = CalcoloInversoNetto.Calcola(Motore(), Impiegato3Liv(), Giugno(), 1600m);

        Assert.True(esito.LordoMensile > esito.NettoOttenuto);
        Assert.True(esito.CostoAzienda > esito.LordoMensile);
    }
}
