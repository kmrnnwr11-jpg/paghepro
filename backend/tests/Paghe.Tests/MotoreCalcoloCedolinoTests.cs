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

        // Competenze (revisione consulente 24-25/08/2026): paga base 1453,94
        // + contingenza EDR incluso 527,90 + terzo elemento nazionale 2,07 = 1983,91
        Assert.Equal(1983.91m, cedolino.TotaleCompetenze);
        Assert.Equal(1983.91m, cedolino.ImponibileInps);

        // Contributi c/dipendente: 1983,91 * 9,19% = 182,32
        Assert.Equal(182.32m, cedolino.ContributiDipendente);

        // Imponibile fiscale: 1983,91 - 182,32 = 1801,59
        Assert.Equal(1801.59m, cedolino.ImponibileIrpef);

        // IRPEF lorda del mese: scaglioni ragguagliati sull'ordinario 1801,59 × 12 = 21619,08 -> 23% /12 = 414,37
        Assert.Equal(414.37m, cedolino.IrpefLorda);
        // Detrazione art. 13 proiettata sulle mensilità contrattuali (revisione consulente)
        // e ulteriore detrazione cuneo L. 207/2024 in casella separata: 1000/12 = 83,33.
        Assert.Equal(185.77m, cedolino.DetrazioniLavoroDipendente);
        Assert.Equal(83.33m, cedolino.UlterioreDetrazioneCuneo);
        Assert.Equal(145.27m, cedolino.IrpefNetta);

        // Addizionali a rate dal conguaglio: senza importi annui caricati sul rapporto
        // non c'è nulla da trattenere nel mese (resta la sola anteprima informativa).
        Assert.Equal(0m, cedolino.AddizionaleRegionale);
        Assert.Equal(0m, cedolino.AddizionaleComunale);

        // Netto: 1983,91 - (182,32 + 145,27 + Fondo Est 2,00 + EBiTer 0,99) = 1653,33
        Assert.Equal(1653.33m, cedolino.NettoInBusta);
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
        // Oltre a contributi e imposte, il CCNL Terziario trattiene le quote lavoratore
        // dei fondi contrattuali (Fondo Est ed EBiTer).
        decimal fondiContrattuali = Math.Abs(c.Righe
            .Where(r => r.Codice is "FONDO_SAN" or "ENTE_BIL")
            .Sum(r => r.Importo));
        decimal sommaComponenti = c.ContributiDipendente + c.IrpefNetta
            + c.AddizionaleRegionale + c.AddizionaleComunale + fondiContrattuali;
        Assert.Equal(sommaComponenti, c.TotaleTrattenute);
    }

    [Fact]
    public void Calcola_CostoAzienda_LordoPiuContributiDatorePiuTfr()
    {
        var c = Motore().Calcola(Impiegato3Liv(), new Cartellino { Anno = 2026, Mese = 6 });
        // Il costo comprende anche i fondi contrattuali c/azienda e il contributo di
        // solidarietà 10% sul fondo sanitario (revisione consulente 25/08/2026).
        decimal fondiDatoriali = c.Righe
            .Where(r => r.Codice is "FONDO_SAN_DATORE" or "ENTE_BIL_DATORE" or "SOLIDARIETA_FONDI")
            .Sum(r => r.Importo);
        Assert.Equal(
            Math.Round(c.TotaleCompetenze + c.ContributiDatore + c.QuotaTfr + fondiDatoriali, 2),
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

        // 10 ore * (1983,91/168) * 1,15 = 10 * 11,80899 * 1,15 = 135,80 (circa)
        var riga = con.Righe.First(r => r.Codice == "STRAORD");
        Assert.Equal(135.80m, riga.Importo, 1);
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
