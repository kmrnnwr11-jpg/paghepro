using Paghe.Core.Calcolo;
using Paghe.Core.Domain;
using Paghe.Core.Parametri;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Elementi retributivi fiscalmente agevolati: trasferta, rimborsi documentati, fringe benefit,
/// buoni pasto e premio di risultato. Limiti 2026.
///
/// I test verificano che le quote esenti NON entrino nell'imponibile e che le eccedenze siano
/// invece tassate come retribuzione ordinaria: è la differenza fra un'agevolazione e un modo
/// per pagare in nero.
/// </summary>
public class ElementiAgevolatiTests
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

    // ---- Trasferta ---------------------------------------------------------------

    [Fact]
    public void Trasferta_EntroLimite_NonEntraNellImponibile()
    {
        var senza = Motore().Calcola(Impiegato3Liv(), new Cartellino { Anno = 2026, Mese = 6 });
        var con = Motore().Calcola(Impiegato3Liv(), new Cartellino
        {
            Anno = 2026, Mese = 6,
            GiorniTrasfertaItalia = 5m,
            IndennitaTrasfertaGiornaliera = 40m,   // sotto i 46,48 esenti
        });

        // 5 x 40 = 200 esenti: aumentano il netto ma non l'imponibile.
        Assert.Equal(200m, con.TotaleEsenti);
        Assert.Equal(senza.ImponibileInps, con.ImponibileInps);
        Assert.Equal(senza.ContributiDipendente, con.ContributiDipendente);
        Assert.Equal(senza.NettoInBusta + 200m, con.NettoInBusta);
    }

    [Fact]
    public void Trasferta_OltreLimite_LEccedenzaEImponibile()
    {
        var senza = Motore().Calcola(Impiegato3Liv(), new Cartellino { Anno = 2026, Mese = 6 });
        var con = Motore().Calcola(Impiegato3Liv(), new Cartellino
        {
            Anno = 2026, Mese = 6,
            GiorniTrasfertaItalia = 2m,
            IndennitaTrasfertaGiornaliera = 60m,   // 13,52 al giorno oltre soglia
        });

        Assert.Equal(92.96m, con.TotaleEsenti);                       // 2 x 46,48
        Assert.Equal(27.04m, con.ImponibileInps - senza.ImponibileInps); // 2 x 13,52
        Assert.Contains(con.Righe, r => r.Codice == "TRASF_IT_IMP" && r.Importo == 27.04m);
    }

    [Fact]
    public void Trasferta_RimborsoMisto_RiduceLaSogliaEsente()
    {
        // Rimborsati vitto e alloggio: soglia ridotta di due terzi (46,48 -> 15,49).
        var c = Motore().Calcola(Impiegato3Liv(), new Cartellino
        {
            Anno = 2026, Mese = 6,
            GiorniTrasfertaItalia = 1m,
            IndennitaTrasfertaGiornaliera = 46.48m,
            RimborsoTrasferta = TipoRimborsoTrasferta.MistoVittoEAlloggio,
        });

        Assert.Equal(15.49m, c.TotaleEsenti);
        Assert.Contains(c.Righe, r => r.Codice == "TRASF_IT_IMP" && r.Importo == 30.99m);
    }

    [Fact]
    public void Trasferta_Estero_UsaLaSogliaPiuAlta()
    {
        var c = Motore().Calcola(Impiegato3Liv(), new Cartellino
        {
            Anno = 2026, Mese = 6,
            GiorniTrasfertaEstero = 3m,
            IndennitaTrasfertaGiornaliera = 77.47m,
        });

        Assert.Equal(232.41m, c.TotaleEsenti);                        // 3 x 77,47
        Assert.DoesNotContain(c.Righe, r => r.Codice == "TRASF_EE_IMP");
    }

    // ---- Fringe benefit ----------------------------------------------------------

    [Fact]
    public void FringeBenefit_EntroSoglia_Esente()
    {
        var c = Motore().Calcola(Impiegato3Liv(), new Cartellino
        {
            Anno = 2026, Mese = 6,
            FringeBenefit = 300m,
            FringeBenefitGiaErogatoAnno = 400m,   // totale 700 < 1000
        });

        Assert.Equal(300m, c.TotaleEsenti);
        Assert.DoesNotContain(c.Righe, r => r.Codice == "FRINGE_IMP");
    }

    [Fact]
    public void FringeBenefit_OltreSoglia_DiventaImponibilePerIntero()
    {
        // Regola "tutto o niente": superata la soglia annua, l'intero importo è imponibile,
        // non la sola eccedenza.
        var c = Motore().Calcola(Impiegato3Liv(), new Cartellino
        {
            Anno = 2026, Mese = 6,
            FringeBenefit = 300m,
            FringeBenefitGiaErogatoAnno = 800m,   // totale 1100 > 1000
        });

        Assert.Equal(0m, c.TotaleEsenti);
        Assert.Contains(c.Righe, r => r.Codice == "FRINGE_IMP" && r.Importo == 300m);
    }

    [Fact]
    public void FringeBenefit_ConFigliACarico_SogliaRaddoppiata()
    {
        var c = Motore().Calcola(Impiegato3Liv(), new Cartellino
        {
            Anno = 2026, Mese = 6,
            FringeBenefit = 300m,
            FringeBenefitGiaErogatoAnno = 1500m,  // 1800 < 2000 con figli
            FigliACarico = true,
        });

        Assert.Equal(300m, c.TotaleEsenti);
    }

    // ---- Buoni pasto -------------------------------------------------------------

    [Fact]
    public void BuoniPasto_Elettronici_EsentiFinoA10Euro()
    {
        var c = Motore().Calcola(Impiegato3Liv(), new Cartellino
        {
            Anno = 2026, Mese = 6,
            GiorniBuonoPasto = 20, ValoreBuonoPasto = 10m, BuonoPastoElettronico = true,
        });

        Assert.Equal(200m, c.TotaleEsenti);
        Assert.DoesNotContain(c.Righe, r => r.Codice == "BUONI_IMP");
    }

    [Fact]
    public void BuoniPasto_SoloLEccedenzaEImponibile()
    {
        // A differenza dei fringe benefit, qui NON vale il "tutto o niente".
        var c = Motore().Calcola(Impiegato3Liv(), new Cartellino
        {
            Anno = 2026, Mese = 6,
            GiorniBuonoPasto = 20, ValoreBuonoPasto = 12m, BuonoPastoElettronico = true,
        });

        Assert.Equal(200m, c.TotaleEsenti);                            // 20 x 10
        Assert.Contains(c.Righe, r => r.Codice == "BUONI_IMP" && r.Importo == 40m); // 20 x 2
    }

    [Fact]
    public void BuoniPasto_Cartacei_HannoLimitePiuBasso()
    {
        var c = Motore().Calcola(Impiegato3Liv(), new Cartellino
        {
            Anno = 2026, Mese = 6,
            GiorniBuonoPasto = 20, ValoreBuonoPasto = 10m, BuonoPastoElettronico = false,
        });

        Assert.Equal(80m, c.TotaleEsenti);                             // 20 x 4
        Assert.Contains(c.Righe, r => r.Codice == "BUONI_IMP" && r.Importo == 120m);
    }

    // ---- Premio di risultato -----------------------------------------------------

    [Fact]
    public void PremioRisultato_EntroLimite_PagaImpostaSostitutivaMaRestaContributivo()
    {
        var senza = Motore().Calcola(Impiegato3Liv(), new Cartellino { Anno = 2026, Mese = 6 });
        var con = Motore().Calcola(Impiegato3Liv(), new Cartellino
        {
            Anno = 2026, Mese = 6,
            PremioRisultato = 1000m, RedditoAnnoPrecedente = 25000m,
        });

        Assert.Equal(1000m, con.PremioRisultatoAgevolato);

        // I contributi restano dovuti: l'agevolazione riguarda la sola IRPEF.
        Assert.Equal(1000m, con.ImponibileInps - senza.ImponibileInps);
        Assert.True(con.ContributiDipendente > senza.ContributiDipendente);

        // Imposta sostitutiva 1% sul premio al netto dei suoi contributi.
        Assert.True(con.ImpostaSostitutivaPremio > 0m);
        Assert.Contains(con.Righe, r => r.Codice == "PREMIO_IMP");
    }

    [Fact]
    public void PremioRisultato_RedditoOltreIlTetto_NienteAgevolazione()
    {
        var c = Motore().Calcola(Impiegato3Liv(), new Cartellino
        {
            Anno = 2026, Mese = 6,
            PremioRisultato = 1000m, RedditoAnnoPrecedente = 90000m,   // oltre 80.000
        });

        Assert.Equal(0m, c.PremioRisultatoAgevolato);
        Assert.Equal(0m, c.ImpostaSostitutivaPremio);
        Assert.Contains(c.Righe, r => r.Codice == "PREMIO_ORD" && r.Importo == 1000m);
    }

    [Fact]
    public void PremioRisultato_OltreIlLimiteAnnuo_LEccedenzaEOrdinaria()
    {
        var c = Motore().Calcola(Impiegato3Liv(), new Cartellino
        {
            Anno = 2026, Mese = 6,
            PremioRisultato = 6000m, RedditoAnnoPrecedente = 30000m,
        });

        Assert.Equal(5000m, c.PremioRisultatoAgevolato);
        Assert.Contains(c.Righe, r => r.Codice == "PREMIO_ORD" && r.Importo == 1000m);
    }

    // ---- Rimborsi documentati ----------------------------------------------------

    [Fact]
    public void RimborsiSpeseDocumentate_SonoEsenti()
    {
        var senza = Motore().Calcola(Impiegato3Liv(), new Cartellino { Anno = 2026, Mese = 6 });
        var con = Motore().Calcola(Impiegato3Liv(), new Cartellino
        {
            Anno = 2026, Mese = 6, RimborsiSpeseDocumentate = 150m,
        });

        Assert.Equal(150m, con.TotaleEsenti);
        Assert.Equal(senza.ImponibileInps, con.ImponibileInps);
        Assert.Equal(senza.NettoInBusta + 150m, con.NettoInBusta);
    }

    // ---- Nessun elemento: il cedolino non cambia ---------------------------------

    [Fact]
    public void SenzaElementiAgevolati_IlCedolinoRestaIdenticoAPrima()
    {
        var c = Motore().Calcola(Impiegato3Liv(), new Cartellino { Anno = 2026, Mese = 6 });

        Assert.Equal(0m, c.TotaleEsenti);
        Assert.Equal(0m, c.PremioRisultatoAgevolato);
        Assert.Equal(0m, c.ImpostaSostitutivaPremio);
        Assert.Equal(1340.99m, c.TotaleCompetenze);
    }
}
