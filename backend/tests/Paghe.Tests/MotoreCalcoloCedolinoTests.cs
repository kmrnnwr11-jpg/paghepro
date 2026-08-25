using Paghe.Core.Calcolo;
using Paghe.Core.Domain;
using Paghe.Core.Parametri;
using Xunit;

namespace Paghe.Tests;

public class MotoreCalcoloCedolinoTests
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
        CodiceCatastaleComune = "F205", // Milano
    };

    [Fact]
    public void Calcola_Impiegato3Liv_FullTime_ValoriAttesi()
    {
        var cedolino = Motore().Calcola(
            Impiegato3Liv(),
            new Cartellino { Anno = 2026, Mese = 6 });

        // Competenze: minimo 1330,66 + EDR 10,33 = 1340,99
        Assert.Equal(1340.99m, cedolino.TotaleCompetenze);
        Assert.Equal(1340.99m, cedolino.ImponibileInps);

        // Contributi c/dipendente: 1340,99 * 9,19% = 123,24
        Assert.Equal(123.24m, cedolino.ContributiDipendente);

        // Imponibile fiscale: 1340,99 - 123,24 = 1217,75
        Assert.Equal(1217.75m, cedolino.ImponibileIrpef);

        // IRPEF lorda del mese: scaglioni ragguagliati sull'ordinario 1217,75 × 12 = 14613 -> 23% /12 = 280,08
        Assert.Equal(280.08m, cedolino.IrpefLorda);
        // Detrazione sul reddito presunto CON le 14 mensilità del contratto (revisione
        // consulente 24/08/2026): 14613 + 1217,75 × 2 = 17048,50 → 2912,48 annui /12 = 242,71
        Assert.Equal(242.71m, cedolino.DetrazioniLavoroDipendente);
        Assert.Equal(37.37m, cedolino.IrpefNetta);

        // Addizionali sullo stesso presunto: 17048,50 * 1,23% /12 = 17,47 ; * 0,80% /12 = 11,37
        Assert.Equal(17.47m, cedolino.AddizionaleRegionale);
        Assert.Equal(11.37m, cedolino.AddizionaleComunale);

        // Netto: 1340,99 - (123,24 + 37,37 + 17,47 + 11,37) = 1151,54
        Assert.Equal(1151.54m, cedolino.NettoInBusta);
    }

    [Fact]
    public void Calcola_NettoUgualeCompetenzeMenoTrattenute()
    {
        var c = Motore().Calcola(Impiegato3Liv(), new Cartellino { Anno = 2026, Mese = 6 });
        Assert.Equal(c.NettoInBusta, c.TotaleCompetenze - c.TotaleTrattenute);
    }

    [Fact]
    public void Calcola_TrattenuteUgualiSommaComponenti()
    {
        var c = Motore().Calcola(Impiegato3Liv(), new Cartellino { Anno = 2026, Mese = 6 });
        decimal sommaComponenti = c.ContributiDipendente + c.IrpefNetta
            + c.AddizionaleRegionale + c.AddizionaleComunale;
        Assert.Equal(sommaComponenti, c.TotaleTrattenute);
    }

    [Fact]
    public void Calcola_CostoAzienda_LordoPiuContributiDatorePiuTfr()
    {
        var c = Motore().Calcola(Impiegato3Liv(), new Cartellino { Anno = 2026, Mese = 6 });
        Assert.Equal(
            Math.Round(c.TotaleCompetenze + c.ContributiDatore + c.QuotaTfr, 2),
            c.CostoAzienda);
        Assert.True(c.QuotaTfr > 0m, "La quota TFR deve essere positiva.");
        Assert.True(c.ContributiDatore > c.ContributiDipendente, "I contributi c/datore superano quelli c/dipendente.");
    }

    [Fact]
    public void Calcola_Straordinario_AumentaCompetenzeEImponibile()
    {
        var senza = Motore().Calcola(Impiegato3Liv(), new Cartellino { Anno = 2026, Mese = 6 });
        var con = Motore().Calcola(Impiegato3Liv(),
            new Cartellino { Anno = 2026, Mese = 6, OreStraordinario = 10m });

        Assert.True(con.TotaleCompetenze > senza.TotaleCompetenze);
        Assert.Contains(con.Righe, r => r.Codice == "STRAORD");

        // 10 ore * (1340,99/168) * 1,15 = 10 * 7,98208 * 1,15 = 91,79 (circa)
        var riga = con.Righe.First(r => r.Codice == "STRAORD");
        Assert.Equal(91.79m, riga.Importo, 1);
    }

    [Fact]
    public void Calcola_PartTime50_DimezzaLaRetribuzioneDiBase()
    {
        var fullTime = new RapportoLavoro
        {
            CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "5", Qualifica = Qualifica.Impiegato,
        };
        var partTime = new RapportoLavoro
        {
            CodiceCcnl = "COMMERCIO_TERZIARIO", Livello = "5", Qualifica = Qualifica.Impiegato,
            PartTime = true, PercentualePartTime = 50m,
        };

        var cFull = Motore().Calcola(fullTime, new Cartellino { Anno = 2026, Mese = 6 });
        var cPart = Motore().Calcola(partTime, new Cartellino { Anno = 2026, Mese = 6 });

        // La retribuzione part-time è circa la metà (tolleranza per arrotondamenti).
        Assert.InRange(cPart.TotaleCompetenze, cFull.TotaleCompetenze * 0.49m, cFull.TotaleCompetenze * 0.51m);
        Assert.True(cPart.NettoInBusta < cFull.NettoInBusta);
    }

    [Fact]
    public void Calcola_Malattia_GeneraVociCarenzaIndennitaIntegrazione()
    {
        var c = Motore().Calcola(Impiegato3Liv(), new Cartellino
        {
            Anno = 2026, Mese = 6, GiorniMalattia = 5m, NumeroEventoMalattiaAnno = 1
        });

        Assert.Contains(c.Righe, r => r.Codice == "MAL_CARENZA");
        Assert.Contains(c.Righe, r => r.Codice == "MAL_INPS");
        Assert.Contains(c.Righe, r => r.Codice == "MAL_INTEGR");
        var storno = Assert.Single(c.Righe, r => r.Codice == "MAL_STORNO");
        Assert.Equal(5m, storno.Quantita);
        Assert.True(storno.Importo < 0m);
    }

    [Fact]
    public void Calcola_Infortunio_GeneraVociInail()
    {
        var c = Motore().Calcola(Impiegato3Liv(), new Cartellino
        {
            Anno = 2026, Mese = 6, GiorniInfortunio = 10m
        });

        Assert.Contains(c.Righe, r => r.Codice == "INF_CARENZA");
        Assert.Contains(c.Righe, r => r.Codice == "INF_INAIL");
        Assert.Contains(c.Righe, r => r.Codice == "INF_INTEGR");
        var storno = Assert.Single(c.Righe, r => r.Codice == "INF_STORNO");
        Assert.Equal(10m, storno.Quantita);
        Assert.True(storno.Importo < 0m);
    }

    [Fact]
    public void Calcola_FerieEPermessi_AggiornaSaldi()
    {
        var c = Motore().Calcola(Impiegato3Liv(), new Cartellino
        {
            Anno = 2026, Mese = 6, GiorniFerie = 2m, OrePermessi = 8m
        });

        Assert.Equal(2m, c.FerieGodute);
        Assert.True(c.FerieMaturate > 0m);
        Assert.Equal(c.FerieMaturate - 2m, c.FerieResidue);

        Assert.Equal(8m, c.PermessiGoduti);
        Assert.True(c.PermessiMaturati > 0m);
        Assert.Equal(c.PermessiMaturati - 8m, c.PermessiResidui);
    }

    [Fact]
    public void Calcola_FerieOltreIlMaturato_SaldoNegativoNonAzzerato()
    {
        // Ferie anticipate: il residuo deve poter andare sotto zero (alimenta l'avviso in UI).
        var c = Motore().Calcola(Impiegato3Liv(), new Cartellino
        {
            Anno = 2026, Mese = 6, GiorniFerie = 25m
        });

        Assert.True(c.FerieResidue < 0m);
        Assert.Equal(c.FerieMaturate - 25m, c.FerieResidue);
    }
}
