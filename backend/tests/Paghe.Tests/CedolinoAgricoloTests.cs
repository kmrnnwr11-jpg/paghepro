using Paghe.Core.Calcolo;
using Paghe.Core.Domain;
using Paghe.Core.Parametri;
using Paghe.Documenti;
using UglyToad.PdfPig;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Golden test sulle <b>buste paga agricole reali</b> di un OTD in provincia di Cuneo
/// (ottobre, novembre e dicembre 2025, software INAZ; dossier di un lavoratore stagionale).
/// Il totale delle giornate — 39 — è confermato dall'estratto conto previdenziale INPS.
/// Dettaglio della busta e punti ancora aperti in
/// docs/buste-reali/busta-05-agricoltura-otd-cuneo-2025.md.
/// </summary>
public sealed class CedolinoAgricoloTests
{
    /// <summary>
    /// Parametri nazionali 2025: la seconda aliquota IRPEF è ancora al 35% (dal 2026 è 33%).
    /// Il resto non incide su questa busta e resta ai valori generali.
    /// </summary>
    private static ParametriNormativi Parametri2025() => new()
    {
        Anno = 2025,
        ScaglioniIrpef = new[]
        {
            new ScaglioneIrpef(0m,     28000m, 0.23m),
            new ScaglioneIrpef(28000m, 50000m, 0.35m),
            new ScaglioneIrpef(50000m, null,   0.43m),
        },
        AliquoteInps = new Dictionary<Qualifica, AliquotaInps>
        {
            [Qualifica.Operaio] = new AliquotaInps(0.0919m, 0.2788m),
        },
        // La busta non dice dove risiede il lavoratore: senza comune non si inventa un'addizionale.
        AddizionaleRegionale = new Dictionary<string, decimal>(),
        AddizionaleComunale = new Dictionary<string, decimal>(),
    };

    private static RapportoAgricolo Rapporto(
        TipoOperaioAgricolo tipo = TipoOperaioAgricolo.Determinato) => new()
    {
        Provincia = "CN",
        Tipo = tipo,
        Livello = "COMUNE",
        // Reddito della stagione stimato dal consulente: annualizzare il mese × 12 porterebbe
        // il bracciante in uno scaglione che non raggiungerà mai.
        RedditoPresuntoAnnuo = 6000m,
    };

    private static Cedolino Calcola(CartellinoAgricolo cartellino) =>
        new MotoreCalcoloCedolinoAgricolo(Parametri2025(), SeedAgricoltura.Cuneo2025())
            .Calcola(Rapporto(), cartellino);

    [Fact]
    public void Oti_SenzaUnaTabellaDedicata_NonRiusaLaPagaOtd()
    {
        var rapporto = Rapporto(TipoOperaioAgricolo.Indeterminato);

        var errore = Assert.Throws<ArgumentException>(() =>
            new MotoreCalcoloCedolinoAgricolo(Parametri2025(), SeedAgricoltura.Cuneo2025())
                .Calcola(rapporto, Ottobre()));

        Assert.Contains("OTI", errore.Message);
        Assert.Contains("solo gli OTD", errore.Message);
    }

    private static CartellinoAgricolo Ottobre() => new()
    {
        Anno = 2025, Mese = 10,
        OreOrdinarie = 64m, GiornateLavorate = 8, GiorniDetrazione = 10,
        UnaTantum = 114.00m,
    };

    private static CartellinoAgricolo Novembre() => new()
    {
        Anno = 2025, Mese = 11,
        OreOrdinarie = 156m, GiornateLavorate = 24, GiorniDetrazione = 30,
        DetrazioniGiaGoduteAnno = 140.44m,
    };

    private static CartellinoAgricolo Dicembre() => new()
    {
        Anno = 2025, Mese = 12,
        OreOrdinarie = 45.5m, GiornateLavorate = 7, GiorniDetrazione = 10,
        DetrazioniGiaGoduteAnno = 423.85m,
        // Fine rapporto: si paga il netto esatto e si recuperano gli 0,88 + 0,04 anticipati.
        UltimoCedolino = true, ArrotondamentoRiportato = 0.92m,
    };

    [Fact]
    public void Ottobre_RiproduceLaBustaReale()
    {
        var c = Calcola(Ottobre());

        Assert.Equal(662.27m, c.TotaleCompetenze);   // 64 h × 8,56671 = 548,27 + una tantum 114,00
        Assert.Equal(662m, c.ImponibileInps);        // imponibile contributivo arrotondato all'euro
        Assert.Equal(63.15m, c.ContributiDipendente); // 8,84% FAP + 0,30% FAVLA + 0,20% + 0,20% CAC
        Assert.Equal(140.44m, c.IrpefLorda);
        Assert.Equal(0m, c.IrpefNetta);              // azzerata dalla detrazione minima del tempo determinato
        Assert.Equal(600.00m, c.NettoInBusta);
    }

    [Fact]
    public void Novembre_RiproduceLaBustaReale()
    {
        var c = Calcola(Novembre());

        Assert.Equal(1336.41m, c.TotaleCompetenze);  // 156 h × 8,56671
        Assert.Equal(1336m, c.ImponibileInps);
        Assert.Equal(127.45m, c.ContributiDipendente);
        Assert.Equal(283.41m, c.IrpefLorda);
        Assert.Equal(0m, c.IrpefNetta);
        Assert.Equal(1209.00m, c.NettoInBusta);
    }

    [Fact]
    public void Dicembre_RiproduceLaBustaReale_ERecuperaGliArrotondamenti()
    {
        var c = Calcola(Dicembre());

        Assert.Equal(389.79m, c.TotaleCompetenze);   // 45,5 h × 8,56671
        Assert.Equal(390m, c.ImponibileInps);
        Assert.Equal(37.21m, c.ContributiDipendente);
        Assert.Equal(359.37m, c.ImponibileIrpef);
        Assert.Equal(0m, c.IrpefNetta);
        // 389,79 − 37,21 = 352,58, meno gli 0,92 già anticipati nei due mesi precedenti.
        Assert.Equal(351.66m, c.NettoInBusta);
    }

    [Fact]
    public void ImponibileFiscale_DeduceSoloIContributiDeducibili()
    {
        // Sulle buste reali la quota dedotta è il 7,80% dell'imponibile contributivo, meno del
        // 9,54% trattenuto e meno anche del solo FAP: il perché è ancora da chiarire col
        // consulente, quindi è un parametro esplicito e non una formula inventata.
        Assert.Equal(610.63m, Calcola(Ottobre()).ImponibileIrpef);
        Assert.Equal(359.37m, Calcola(Dicembre()).ImponibileIrpef);
        // Novembre sulla busta reale riporta 1.232,21: un centesimo sopra il nostro calcolo.
        Assert.Equal(1232.20m, Calcola(Novembre()).ImponibileIrpef);
    }

    [Fact]
    public void Giornate_SonoIlDatoDellaDenunciaInps_ENonSiRicavanoDalleOre()
    {
        // 8 + 24 + 7 = 39 giornate, come nell'estratto conto INPS. Le ore invece sono
        // 64 + 156 + 45,5: dividere per un orario fisso darebbe 8 + 24 + 7 solo per caso.
        var giornate = new[] { Ottobre(), Novembre(), Dicembre() }
            .Select(mese => Calcola(mese).Righe.Single(r => r.Codice == "AGR_GG").Quantita)
            .Sum();

        Assert.Equal(39m, giornate);
    }

    [Fact]
    public void TariffaDiUnAltraProvincia_NonVieneApplicata()
    {
        var rapportoDiPiacenza = new RapportoAgricolo
        {
            Provincia = "PC", Tipo = TipoOperaioAgricolo.Determinato, Livello = "COMUNE",
        };
        var motore = new MotoreCalcoloCedolinoAgricolo(Parametri2025(), SeedAgricoltura.Cuneo2025());

        var errore = Assert.Throws<ArgumentException>(
            () => motore.Calcola(rapportoDiPiacenza, Ottobre()));
        Assert.Contains("provinciali", errore.Message);
    }

    [Fact]
    public void PdfAgricolo_MostraOreGiornateTariffaContributiFonteENetto()
    {
        var cartellino = Ottobre();
        var rapporto = Rapporto();
        var parametri = SeedAgricoltura.Cuneo2025();
        var cedolino = Calcola(cartellino);
        var pdf = CedolinoAgricoloPdf.Genera(
            cedolino,
            new IntestazioneCedolino
            {
                Azienda = "Azienda Agricola La Quercia",
                AziendaIndirizzo = "Via dei Campi 12",
                AziendaComune = "12010 Cuneo (CN)",
                CodiceFiscaleAzienda = "01234567897",
                PartitaIva = "01234567897",
                Dipendente = "Rossi Mario",
                CodiceFiscaleDipendente = "RSSMRA80A01D205X",
                Matricola = "AGR-001",
                DataAssunzione = "01/10/2025",
                Qualifica = "Operaio agricolo comune",
                Ccnl = "Operai agricoli e florovivaisti",
                TipoContratto = "OTD - tempo determinato",
            },
            rapporto,
            cartellino,
            parametri);

        Assert.True(pdf.Length > 10_000);
        using var documento = PdfDocument.Open(pdf);
        Assert.Single(documento.GetPages());
        string testo = string.Join("\n", documento.GetPages().Select(p => p.Text));
        Assert.Contains("BUSTA PAGA AGRICOLA", testo);
        Assert.Contains("OTD", testo);
        Assert.Contains("6,16000", testo);
        Assert.Contains("2,40671", testo);
        Assert.Contains("8,56671", testo);
        Assert.Contains("64,00", testo);
        Assert.Contains("GIORNATE INPS", testo);
        Assert.Contains("32300", testo);
        Assert.Contains("40000", testo);
        Assert.Contains("+0,88", testo);
        Assert.Contains("REDDITO ANNUO PRESUNTO", testo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("6.000,00", testo);
        Assert.Contains("GIORNI DI DETRAZIONE", testo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("600,00", testo);
        Assert.Contains("FONTE TABELLA APPLICATA", testo);
        Assert.Contains("BOZZA DA VALIDARE", testo);
        Assert.DoesNotContain("TFR", testo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ROL", testo, StringComparison.OrdinalIgnoreCase);

        string? destinazione = Environment.GetEnvironmentVariable("PAGHE_PDF_AGRICOLO");
        if (!string.IsNullOrWhiteSpace(destinazione))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinazione)!);
            File.WriteAllBytes(destinazione, pdf);
        }
    }

    // ---- Trasferta e rimborsi documentati: stessa regola del motore ordinario ----------

    [Fact]
    public void TrasfertaEntroSoglia_EsenteNelNettoENonNegliImponibili()
    {
        var senza = Calcola(Novembre());
        var con = Calcola(new CartellinoAgricolo
        {
            Anno = 2025, Mese = 11,
            OreOrdinarie = 156m, GiornateLavorate = 24, GiorniDetrazione = 30,
            DetrazioniGiaGoduteAnno = 140.44m,
            GiorniTrasferta = 10m, IndennitaTrasfertaGiornaliera = 15m,
        });

        var voce = Assert.Single(con.Righe, r => r.Codice == "AGR_TRASF");
        Assert.Equal(150.00m, voce.Importo);         // 10 g × 15 €, tutto sotto i 46,48 €/giorno
        Assert.DoesNotContain(con.Righe, r => r.Codice == "AGR_TRASF_IMP");
        Assert.Equal(senza.ImponibileInps, con.ImponibileInps);
        Assert.Equal(senza.ImponibileIrpef, con.ImponibileIrpef);
        Assert.Equal(senza.TotaleTrattenute, con.TotaleTrattenute);
        Assert.Equal(senza.NettoInBusta + 150.00m, con.NettoInBusta);
    }

    [Fact]
    public void TrasfertaOltreSoglia_TassaSoloLEccedenza()
    {
        var con = Calcola(new CartellinoAgricolo
        {
            Anno = 2025, Mese = 11,
            OreOrdinarie = 156m, GiornateLavorate = 24, GiorniDetrazione = 30,
            DetrazioniGiaGoduteAnno = 140.44m,
            GiorniTrasferta = 2m, IndennitaTrasfertaGiornaliera = 50m,
        });

        Assert.Equal(92.96m, Assert.Single(con.Righe, r => r.Codice == "AGR_TRASF").Importo);
        Assert.Equal(7.04m, Assert.Single(con.Righe, r => r.Codice == "AGR_TRASF_IMP").Importo);
        // L'eccedenza entra nella base contributiva: imponibile = competenze − quota esente.
        Assert.Equal(
            Math.Round(con.TotaleCompetenze - 92.96m, 0, MidpointRounding.AwayFromZero),
            con.ImponibileInps);
    }

    [Fact]
    public void TrasfertaAGruppi_SommaLeQuoteEsentiETassaSoloLeEccedenze()
    {
        var con = Calcola(new CartellinoAgricolo
        {
            Anno = 2025, Mese = 11,
            OreOrdinarie = 156m, GiornateLavorate = 24, GiorniDetrazione = 30,
            DetrazioniGiaGoduteAnno = 140.44m,
            Trasferte =
            [
                new TrasfertaGiorniAgricola(10m, 15m),   // 150,00 esenti
                new TrasfertaGiorniAgricola(3m, 20m),    // 60,00 esenti
                new TrasfertaGiorniAgricola(2m, 50m),    // 92,96 esenti + 7,04 imponibili
            ],
        });

        var esenti = con.Righe.Where(r => r.Codice == "AGR_TRASF").ToList();
        Assert.Equal(3, esenti.Count);
        Assert.Equal(302.96m, esenti.Sum(r => r.Importo));
        Assert.Equal(7.04m, Assert.Single(con.Righe, r => r.Codice == "AGR_TRASF_IMP").Importo);
        // Le quote esenti dei gruppi restano fuori dalla base contributiva.
        Assert.Equal(
            Math.Round(con.TotaleCompetenze - 302.96m, 0, MidpointRounding.AwayFromZero),
            con.ImponibileInps);
    }

    [Fact]
    public void RimborsoSpeseDocumentate_EsenteNelNettoENonNegliImponibili()
    {
        var senza = Calcola(Novembre());
        var con = Calcola(new CartellinoAgricolo
        {
            Anno = 2025, Mese = 11,
            OreOrdinarie = 156m, GiornateLavorate = 24, GiorniDetrazione = 30,
            DetrazioniGiaGoduteAnno = 140.44m,
            RimborsiSpeseDocumentate = 45.50m,
        });

        Assert.Equal(45.50m, Assert.Single(con.Righe, r => r.Codice == "AGR_RIMB_SPESE").Importo);
        Assert.Equal(senza.ImponibileInps, con.ImponibileInps);
        Assert.Equal(senza.ImponibileIrpef, con.ImponibileIrpef);
        Assert.Equal(senza.TotaleTrattenute, con.TotaleTrattenute);
    }
}
