using Paghe.Core.Calcolo;
using Paghe.Core.Domain;
using Paghe.Core.Parametri;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Correzione e hardening della busta agricola OTD sul caso reale GOOD JOBS luglio 2026
/// (CPL Piacenza 2024-2027 + tabella E.B.A.P. OTD dal 01/06/2026): giorni di detrazione dal
/// calendario del rapporto, costanti contributive uniche per calcolo ed etichetta, validatore
/// di coerenza, recupero mensile dell'arrotondamento e le 17 buste come fixture di regressione.
/// </summary>
public sealed class BustaAgricolaHardeningTests
{
    // ---- Parametri Piacenza 2026: le stesse costanti guidano calcolo ed etichetta -------
    private static ParametriAgricoli Piacenza2026() => new()
    {
        Anno = 2026,
        Provincia = "PC",
        Fonte = "Tabella E.B.A.P. OTD dal 01/06/2026 + CPL Piacenza 2024-2027 artt. 53-54 (test)",
        Tariffe = [new TariffaAgricola("COMUNE", "3ª area - 5º liv. Comune", 8.95m, 2.73m)],
        Contributi =
        [
            new ContributoAgricolo("FAP", "Contributo FAP (IVS) c/lavoratore", 0.0884m,
                ACaricoDipendente: true, DeducibileIrpef: true),
            new ContributoAgricolo("FIMI", "Contributo FIMI E.B.A.P. c/lavoratore (art. 53 CPL)",
                0.0033m, ACaricoDipendente: true),
            new ContributoAgricolo("CAC", "Contributo assistenza contrattuale c/lavoratore (art. 54 CPL)",
                0.0025m, ACaricoDipendente: true),
        ],
        // Zero = la quota deducibile viene dai flag (solo FAP), non da un valore osservato.
        AliquotaContributiDeducibili = 0m,
    };

    private static ParametriNormativi Parametri2026() => new()
    {
        Anno = 2026,
        ScaglioniIrpef =
        [
            new ScaglioneIrpef(0m, 28000m, 0.23m),
            new ScaglioneIrpef(28000m, 50000m, 0.33m),
            new ScaglioneIrpef(50000m, null, 0.43m),
        ],
        AliquoteInps = new Dictionary<Qualifica, AliquotaInps>
        {
            [Qualifica.Operaio] = new AliquotaInps(0.0919m, 0.2788m),
        },
        AddizionaleRegionale = new Dictionary<string, decimal>(),
        AddizionaleComunale = new Dictionary<string, decimal>(),
    };

    private static Cedolino Calcola(CartellinoAgricolo cartellino, decimal reddito = 5000m) =>
        new MotoreCalcoloCedolinoAgricolo(Parametri2026(), Piacenza2026()).Calcola(
            new RapportoAgricolo
            {
                Provincia = "PC",
                Tipo = TipoOperaioAgricolo.Determinato,
                Livello = "COMUNE",
                RedditoPresuntoAnnuo = reddito,
            },
            cartellino);

    // ---- BUG 1: giorni di detrazione = min(fine, cessazione) − max(inizio, assunzione) + 1

    [Theory]
    [InlineData("2026-06-30", null, 31)]  // assunto prima del mese: tutto luglio
    [InlineData("2026-07-07", null, 25)]
    [InlineData("2026-07-08", null, 24)]
    [InlineData("2026-07-09", null, 23)]
    [InlineData("2026-07-09", "2026-07-20", 12)] // cessazione infra-mese
    [InlineData("2026-07-01", null, 31)]
    [InlineData("2026-08-01", null, 0)]   // assunto dopo il mese elaborato
    [InlineData("2026-07-07", "2026-06-30", 0)]  // cessato prima del mese
    public void GiorniDetrazione_SeguonoIlCalendarioDelRapporto(
        string assunzione, string? cessazione, int attesi)
    {
        Assert.Equal(attesi, ValidatoreBustaAgricola.GiorniDetrazioneNelMese(
            2026, 7,
            DateOnly.Parse(assunzione),
            cessazione is null ? null : DateOnly.Parse(cessazione)));
    }

    [Fact]
    public void GiorniDetrazione_MesiDa30E28Giorni()
    {
        // Giugno (30 giorni) intero e febbraio 2026 (28 giorni) intero.
        Assert.Equal(30, ValidatoreBustaAgricola.GiorniDetrazioneNelMese(
            2026, 6, new DateOnly(2026, 1, 1), null));
        Assert.Equal(28, ValidatoreBustaAgricola.GiorniDetrazioneNelMese(
            2026, 2, new DateOnly(2026, 1, 1), null));
        // Assunzione a metà di un mese corto.
        Assert.Equal(16, ValidatoreBustaAgricola.GiorniDetrazioneNelMese(
            2026, 6, new DateOnly(2026, 6, 15), null));
    }

    // ---- BUG 5/9: contributi dalle costanti configurate, etichetta = calcolo -----------

    [Theory]
    [InlineData(40, 467, "41.28", "1.54", "1.17")]
    [InlineData(55, 642, "56.75", "2.12", "1.61")]
    [InlineData(60, 701, "61.97", "2.31", "1.75")]
    [InlineData(65, 759, "67.10", "2.50", "1.90")]
    [InlineData(70, 818, "72.31", "2.70", "2.05")]
    [InlineData(80, 934, "82.57", "3.08", "2.34")]
    public void Contributi_CalcolatiConLeCostantiConfigurate(
        int ore, int imponibileAtteso, string fap, string fimi, string cac)
    {
        var cedolino = Calcola(new CartellinoAgricolo
        {
            Anno = 2026, Mese = 7, OreOrdinarie = ore, GiornateLavorate = ore / 5,
            GiorniDetrazione = 23,
        });

        Assert.Equal(imponibileAtteso, cedolino.ImponibileInps);
        Assert.Equal(-decimal.Parse(fap, System.Globalization.CultureInfo.InvariantCulture),
            Assert.Single(cedolino.Righe, r => r.Codice == "FAP").Importo);
        Assert.Equal(-decimal.Parse(fimi, System.Globalization.CultureInfo.InvariantCulture),
            Assert.Single(cedolino.Righe, r => r.Codice == "FIMI").Importo);
        Assert.Equal(-decimal.Parse(cac, System.Globalization.CultureInfo.InvariantCulture),
            Assert.Single(cedolino.Righe, r => r.Codice == "CAC").Importo);
        Assert.Equal(
            decimal.Parse(fap, System.Globalization.CultureInfo.InvariantCulture)
            + decimal.Parse(fimi, System.Globalization.CultureInfo.InvariantCulture)
            + decimal.Parse(cac, System.Globalization.CultureInfo.InvariantCulture),
            cedolino.ContributiDipendente);
    }

    [Fact]
    public void EtichetteContributi_StampanoLaStessaAliquotaUsataNelCalcolo()
    {
        var cedolino = Calcola(new CartellinoAgricolo
        {
            Anno = 2026, Mese = 7, OreOrdinarie = 60m, GiornateLavorate = 12,
            GiorniDetrazione = 23,
        });

        Assert.Contains("(8,84%)", Assert.Single(cedolino.Righe, r => r.Codice == "FAP").Descrizione);
        Assert.Contains("(0,33%)", Assert.Single(cedolino.Righe, r => r.Codice == "FIMI").Descrizione);
        Assert.Contains("(0,25%)", Assert.Single(cedolino.Righe, r => r.Codice == "CAC").Descrizione);
        // La somma delle costanti (9,42%) è l'aliquota totale esposta.
        Assert.Equal(0.0942m, Piacenza2026().AliquotaTotaleDipendente);
        Assert.Equal("9,42%", FormatoAliquota.Percentuale(Piacenza2026().AliquotaTotaleDipendente));
        // Un'aliquota con più decimali stampa TUTTI i decimali significativi.
        Assert.Equal("0,3333%", FormatoAliquota.Percentuale(0.003333m));
    }

    [Fact]
    public void QuotaDeducibile_SeguonoIFlagDeiContributi_FapSiFimiCacNo()
    {
        // Effettiva dai flag: solo FAP (8,84%). FIMI e CAC restano tassati.
        Assert.Equal(0.0884m, Piacenza2026().AliquotaDeducibileEffettiva);
        // Il valore osservato, quando c'è, vince sui flag (caso Cuneo 7,80%).
        Assert.Equal(0.0780m, SeedAgricoltura.Cuneo2025().AliquotaDeducibileEffettiva);

        var cedolino = Calcola(new CartellinoAgricolo
        {
            Anno = 2026, Mese = 7, OreOrdinarie = 60m, GiornateLavorate = 12,
            GiorniDetrazione = 23,
        });
        // Imponibile IRPEF = lordo − FAP dedotto (61,97 su imponibile 701): 700,80 − 61,97.
        Assert.Equal(638.83m, cedolino.ImponibileIrpef);
    }

    // ---- BUG 3: normalizzazione trasferte (inversione palese, gruppi uguali) ------------

    [Fact]
    public void TrasfertaInvertita_VieneNormalizzataConAvviso()
    {
        var segnalazioni = new List<SegnalazioneBustaAgricola>();
        var gruppi = ValidatoreBustaAgricola.NormalizzaTrasferte(
            [new TrasfertaGiorniAgricola(20m, 3m)], giornateLavorate: 11, segnalazioni);

        var gruppo = Assert.Single(gruppi);
        Assert.Equal(3m, gruppo.Giorni);
        Assert.Equal(20m, gruppo.ImportoGiorno);
        var avviso = Assert.Single(segnalazioni);
        Assert.Equal(LivelloSegnalazione.Avviso, avviso.Livello);
        Assert.Equal("TRASF_INVERTITA", avviso.Codice);
    }

    [Fact]
    public void TrasferteAmbigue_NonVengonoToccate()
    {
        var segnalazioni = new List<SegnalazioneBustaAgricola>();
        // 7 giorni × 3 € con 8 giornate lavorate: plausibile in entrambi i versi → non si tocca.
        var gruppi = ValidatoreBustaAgricola.NormalizzaTrasferte(
            [new TrasfertaGiorniAgricola(7m, 3m)], giornateLavorate: 8, segnalazioni);

        Assert.Equal(7m, Assert.Single(gruppi).Giorni);
        Assert.Empty(segnalazioni);
    }

    [Fact]
    public void GruppiConLoStessoImporto_DiventanoUnaRigaSola()
    {
        var segnalazioni = new List<SegnalazioneBustaAgricola>();
        var gruppi = ValidatoreBustaAgricola.NormalizzaTrasferte(
            [new TrasfertaGiorniAgricola(4m, 20m), new TrasfertaGiorniAgricola(2m, 20m)],
            giornateLavorate: 11, segnalazioni);

        var gruppo = Assert.Single(gruppi);
        Assert.Equal(6m, gruppo.Giorni);
        Assert.Equal(20m, gruppo.ImportoGiorno);

        // Anche il motore stampa una sola riga AGR_TRASF per importo giornaliero.
        var cedolino = Calcola(new CartellinoAgricolo
        {
            Anno = 2026, Mese = 7, OreOrdinarie = 55m, GiornateLavorate = 11,
            GiorniDetrazione = 23,
            Trasferte =
            [
                new TrasfertaGiorniAgricola(4m, 20m),
                new TrasfertaGiorniAgricola(2m, 20m),
            ],
        });
        var voce = Assert.Single(cedolino.Righe, r => r.Codice == "AGR_TRASF");
        Assert.Equal(6m, voce.Quantita);      // quantità = GIORNI
        Assert.Equal(20m, voce.Base);         // base = IMPORTO GIORNALIERO
        Assert.Equal(120m, voce.Importo);
    }

    // ---- BUG 4: controlli di coerenza ---------------------------------------------------

    [Fact]
    public void TrasferteOltreLeGiornate_SonoUnErrore()
    {
        // Il caso ALOM NUR: 7×3 + 5×1 = 12 giorni di trasferta su 8 giornate lavorate.
        var esito = ValidatoreBustaAgricola.Valida(
            2026, 7, oreOrdinarie: 40m, giornateLavorate: 8,
            trasferte: [new TrasfertaGiorniAgricola(7m, 3m), new TrasfertaGiorniAgricola(5m, 1m)],
            dataAssunzione: new DateOnly(2026, 6, 30), dataCessazione: new DateOnly(2026, 9, 30),
            limiteEsenzioneTrasfertaGiorno: 46.48m);

        Assert.Contains(esito, s =>
            s.Codice == "TRASFERTE_OLTRE_GIORNATE" && s.Livello == LivelloSegnalazione.Errore);
        // E l'indennità da 1 €/giorno è un avviso di implausibilità (caso MIA Sahan).
        Assert.Contains(esito, s =>
            s.Codice == "TRASFERTA_IMPORTO_IMPLAUSIBILE" && s.Livello == LivelloSegnalazione.Avviso);
    }

    [Fact]
    public void TrasfertaOltreIlLimite_ErroreEQuotaEccedenteImponibile()
    {
        var esito = ValidatoreBustaAgricola.Valida(
            2026, 7, oreOrdinarie: 55m, giornateLavorate: 11,
            trasferte: [new TrasfertaGiorniAgricola(2m, 50m)],
            dataAssunzione: new DateOnly(2026, 7, 1), dataCessazione: null,
            limiteEsenzioneTrasfertaGiorno: 46.48m);
        Assert.Contains(esito, s =>
            s.Codice == "TRASFERTA_OLTRE_LIMITE_ESENZIONE" && s.Livello == LivelloSegnalazione.Errore);

        // Il motore rende comunque imponibile l'eccedenza: 2 × 46,48 esenti, 2 × 3,52 tassati.
        var cedolino = Calcola(new CartellinoAgricolo
        {
            Anno = 2026, Mese = 7, OreOrdinarie = 55m, GiornateLavorate = 11,
            GiorniDetrazione = 31,
            Trasferte = [new TrasfertaGiorniAgricola(2m, 50m)],
        });
        Assert.Equal(92.96m, Assert.Single(cedolino.Righe, r => r.Codice == "AGR_TRASF").Importo);
        Assert.Equal(7.04m, Assert.Single(cedolino.Righe, r => r.Codice == "AGR_TRASF_IMP").Importo);
    }

    [Fact]
    public void GiornateIncoerentiColContratto_SonoUnAvviso()
    {
        // Il caso FOKIR: 60 ore su 15 giornate = 4 h/g contro le 5 h/g del contratto (30/6).
        var esito = ValidatoreBustaAgricola.Valida(
            2026, 7, oreOrdinarie: 60m, giornateLavorate: 15,
            trasferte: [],
            dataAssunzione: new DateOnly(2026, 7, 9), dataCessazione: new DateOnly(2026, 8, 31),
            limiteEsenzioneTrasfertaGiorno: 46.48m,
            oreGiornaliereContratto: 5m);
        Assert.Contains(esito, s =>
            s.Codice == "GIORNATE_INCOERENTI_COL_CONTRATTO" && s.Livello == LivelloSegnalazione.Avviso);

        // Con le stesse ore su 12 giornate (5 h/g) nessun avviso.
        var coerente = ValidatoreBustaAgricola.Valida(
            2026, 7, oreOrdinarie: 60m, giornateLavorate: 12,
            trasferte: [],
            dataAssunzione: new DateOnly(2026, 7, 9), dataCessazione: new DateOnly(2026, 8, 31),
            limiteEsenzioneTrasfertaGiorno: 46.48m,
            oreGiornaliereContratto: 5m);
        Assert.DoesNotContain(coerente, s => s.Codice == "GIORNATE_INCOERENTI_COL_CONTRATTO");
    }

    [Theory]
    [InlineData(0, 8, "ORE_ZERO_CON_GIORNATE")]
    [InlineData(40, 0, "GIORNATE_ZERO_CON_ORE")]
    public void OreEGiornate_SiDichiaranoInsieme(int ore, int giornate, string codice)
    {
        var esito = ValidatoreBustaAgricola.Valida(
            2026, 7, ore, giornate, [],
            new DateOnly(2026, 7, 1), null, 46.48m);
        Assert.Contains(esito, s => s.Codice == codice && s.Livello == LivelloSegnalazione.Errore);
    }

    [Fact]
    public void CessazionePrimaDellAssunzione_EDelMese_SonoErrori()
    {
        var esito = ValidatoreBustaAgricola.Valida(
            2026, 7, 40m, 8, [],
            new DateOnly(2026, 7, 9), new DateOnly(2026, 7, 1), 46.48m);
        Assert.Contains(esito, s => s.Codice == "CESSAZIONE_PRIMA_ASSUNZIONE");

        var esito2 = ValidatoreBustaAgricola.Valida(
            2026, 7, 40m, 8, [],
            new DateOnly(2026, 5, 1), new DateOnly(2026, 6, 15), 46.48m);
        Assert.Contains(esito2, s => s.Codice == "CESSAZIONE_PRIMA_DEL_MESE");
    }

    [Fact]
    public void GiornateOltreIlCalendarioDelRapporto_SonoUnErrore()
    {
        // Assunto il 9/7: nel mese il rapporto copre 23 giorni, non 25.
        var esito = ValidatoreBustaAgricola.Valida(
            2026, 7, 125m, 25, [],
            new DateOnly(2026, 7, 9), null, 46.48m);
        Assert.Contains(esito, s =>
            s.Codice == "GIORNATE_OLTRE_CALENDARIO" && s.Livello == LivelloSegnalazione.Errore);
    }

    [Fact]
    public void MatricolaInpsMancante_AvvisoInBozza_ErroreInValidazione()
    {
        var bozza = ValidatoreBustaAgricola.Valida(
            2026, 7, 40m, 8, [], new DateOnly(2026, 7, 1), null, 46.48m,
            matricolaInpsAzienda: null, perValidazione: false);
        Assert.Contains(bozza, s =>
            s.Codice == "MATRICOLA_INPS_MANCANTE" && s.Livello == LivelloSegnalazione.Avviso);

        var validazione = ValidatoreBustaAgricola.Valida(
            2026, 7, 40m, 8, [], new DateOnly(2026, 7, 1), null, 46.48m,
            matricolaInpsAzienda: "", perValidazione: true);
        Assert.Contains(validazione, s =>
            s.Codice == "MATRICOLA_INPS_MANCANTE" && s.Livello == LivelloSegnalazione.Errore);
    }

    // ---- BUG 10: cessazioni difformi a parità di assunzione -----------------------------

    [Fact]
    public void CessazioneDifformeDalGruppo_EUnAvviso()
    {
        var esito = ValidatoreBustaAgricola.ValidaCessazioniOmogenee(
        [
            ("MIA Md Amran", new DateOnly(2026, 7, 8), new DateOnly(2026, 8, 30)),
            ("MIA Md Mahfuz", new DateOnly(2026, 7, 8), new DateOnly(2026, 8, 31)),
            ("BEPARI Shahin", new DateOnly(2026, 7, 9), new DateOnly(2026, 8, 31)),
            ("MIA Abjal", new DateOnly(2026, 7, 9), new DateOnly(2026, 8, 31)),
            ("SHEIKH Rana", new DateOnly(2026, 7, 9), new DateOnly(2026, 8, 31)),
        ]);

        var avviso = Assert.Single(esito);
        Assert.Equal("CESSAZIONE_DIFFORME", avviso.Codice);
        Assert.Contains("MIA Md Amran", avviso.Messaggio);
        Assert.Contains("30/08/2026", avviso.Messaggio);
    }

    // ---- BUG 8: recupero dell'arrotondamento nel mese successivo ------------------------

    [Fact]
    public void ArrotondamentoDiLuglio_SiRecuperaAdAgostoConVoceEsplicita()
    {
        // Luglio: il caso ALOM (40 h, trasferte 7×3 + 5×1) chiude con +0,81 di arrotondamento.
        var luglio = Calcola(new CartellinoAgricolo
        {
            Anno = 2026, Mese = 7, OreOrdinarie = 40m, GiornateLavorate = 8,
            GiorniDetrazione = 31,
            Trasferte = [new TrasfertaGiorniAgricola(7m, 3m), new TrasfertaGiorniAgricola(5m, 1m)],
        }, reddito: 6000m);
        Assert.Equal(0.79m, Assert.Single(luglio.Righe, r => r.Codice == "AGR_ARROT").Importo);
        Assert.Equal(450m, luglio.NettoInBusta);

        // Agosto: la voce AGR_ARROT_REC recupera l'anticipo PRIMA del nuovo arrotondamento.
        var agosto = Calcola(new CartellinoAgricolo
        {
            Anno = 2026, Mese = 8, OreOrdinarie = 40m, GiornateLavorate = 8,
            GiorniDetrazione = 31,
            ArrotondamentoRiportato = 0.79m,
        }, reddito: 6000m);
        var recupero = Assert.Single(agosto.Righe, r => r.Codice == "AGR_ARROT_REC");
        Assert.Equal(-0.79m, recupero.Importo);
        Assert.Equal(TipoVoce.Trattenuta, recupero.Tipo);
        // Il netto resta intero: 467,20 − 43,99 − 0,79 = 422,42 → 423,00 (+0,58).
        Assert.Equal(423m, agosto.NettoInBusta);
        Assert.Equal(0.58m, Assert.Single(agosto.Righe, r => r.Codice == "AGR_ARROT").Importo);
    }

    [Fact]
    public void UltimoCedolino_PagaLEsattoERecuperaIlRiporto()
    {
        var ultimo = Calcola(new CartellinoAgricolo
        {
            Anno = 2026, Mese = 8, OreOrdinarie = 40m, GiornateLavorate = 8,
            GiorniDetrazione = 31,
            ArrotondamentoRiportato = 0.79m, UltimoCedolino = true,
        }, reddito: 6000m);

        Assert.Equal(-0.79m, Assert.Single(ultimo.Righe, r => r.Codice == "AGR_ARROT_REC").Importo);
        Assert.DoesNotContain(ultimo.Righe, r => r.Codice == "AGR_ARROT");
        // 467,20 − 43,99 − 0,79: nessun arrotondamento sull'ultima busta.
        Assert.Equal(422.42m, ultimo.NettoInBusta);
    }

    // ---- BUG 6: trattamento integrativo solo con capienza -------------------------------

    [Fact]
    public void TrattamentoIntegrativo_NonSpettaSenzaCapienza_ESpettaConCapienza()
    {
        // Reddito 5.000: IRPEF lorda annua 1.150 < detrazione minima 1.380 → niente bonus.
        var senza = Calcola(new CartellinoAgricolo
        {
            Anno = 2026, Mese = 7, OreOrdinarie = 60m, GiornateLavorate = 12,
            GiorniDetrazione = 23,
        });
        Assert.DoesNotContain(senza.Righe, r => r.Codice == "TRATT_INTEGR");

        // Reddito 14.000: IRPEF lorda 3.220 > detrazione 1.955 → quota per 23 giorni: 75,62.
        var con = Calcola(new CartellinoAgricolo
        {
            Anno = 2026, Mese = 7, OreOrdinarie = 60m, GiornateLavorate = 12,
            GiorniDetrazione = 23,
        }, reddito: 14000m);
        var voce = Assert.Single(con.Righe, r => r.Codice == "TRATT_INTEGR");
        Assert.Equal(75.62m, voce.Importo);
        Assert.Equal(TipoVoce.Competenza, voce.Tipo);
        // Fuori dagli imponibili: non è retribuzione.
        Assert.Equal(701m, con.ImponibileInps);
        Assert.Equal(638.83m, con.ImponibileIrpef);
        // Ma dentro il netto: competenze − trattenute + arrotondamento = netto.
        Assert.Equal(con.TotaleCompetenze - con.TotaleTrattenute
            + (con.Righe.SingleOrDefault(r => r.Codice == "AGR_ARROT")?.Importo ?? 0m),
            con.NettoInBusta);
    }

    // ---- Le 17 buste GOOD JOBS di luglio 2026 come fixture di regressione ---------------
    //
    // Dati ricostruiti dalla terza generazione del 2/9/2026 (busta ALOM reale + totali del
    // caso: 985 ore, 200 giornate, lordo 13.128,80, trasferte 1.624,00, netto 12.050,00).
    // KHAN ha la trasferta caricata invertita (20 g × 3 €) e va normalizzata prima del motore,
    // come fa l'endpoint. Con FIMI allo 0,33% (costante configurata) i netti NON cambiano
    // rispetto alla generazione con 0,3333%: cambia di qualche centesimo la trattenuta e
    // quindi l'arrotondamento che la compensa.
    public static readonly TheoryData<string, int, int, decimal[][], int, int> BusteLuglio2026 = new()
    {
        // nominativo, ore, giornate, gruppi trasferta {giorni, importo}, reddito, netto atteso
        { "ALOM NUR", 40, 8, [[7, 3], [5, 1]], 6000, 450 },
        { "KHAN Md Rinku", 55, 11, [[20, 3]], 6000, 642 },
        { "MIA Sahan", 30, 6, [[4, 20], [1, 1]], 5000, 399 },
        { "MIA Md Amran", 55, 11, [[6, 15]], 5000, 672 },
        { "FOKIR Md Didar Hossan", 60, 15, [[1, 43]], 5000, 678 },
        { "MIA Md Mahfuz", 55, 11, [[6, 15]], 5000, 672 },
        { "BEPARI Shahin", 60, 12, [[8, 14]], 5000, 747 },
        { "MIA Abjal", 50, 10, [[5, 19]], 5000, 624 },
        { "KHALASI Liton", 65, 13, [[8, 16]], 5000, 816 },
        { "MIA SOHAG", 55, 11, [[8, 16]], 5000, 710 },
        { "SHEIKH Rana", 65, 13, [[7, 14]], 5000, 786 },
        { "MATUBBER Romjan", 80, 16, [[7, 14]], 5000, 945 },
        { "FAHIM Abdullah Al", 55, 11, [[4, 20], [2, 20]], 5000, 702 },
        { "ULLAH Hedayet", 60, 12, [[4, 19]], 5000, 711 },
        { "HOWLADER Sujon", 65, 13, [[8, 16]], 5000, 816 },
        { "SARIF NEWAJ", 65, 13, [[8, 17]], 5000, 824 },
        { "HUQUE AMINUL", 70, 14, [[5, 23]], 5000, 856 },
    };

    private static Cedolino CalcolaBusta(int ore, int giornate, decimal[][] trasferte, int reddito)
    {
        var segnalazioni = new List<SegnalazioneBustaAgricola>();
        var gruppi = ValidatoreBustaAgricola.NormalizzaTrasferte(
            trasferte.Select(t => new TrasfertaGiorniAgricola(t[0], t[1])).ToList(),
            giornate, segnalazioni);
        return Calcola(new CartellinoAgricolo
        {
            Anno = 2026, Mese = 7, OreOrdinarie = ore, GiornateLavorate = giornate,
            GiorniDetrazione = 23, Trasferte = gruppi,
        }, reddito);
    }

    [Theory]
    [MemberData(nameof(BusteLuglio2026))]
    public void Netto_EInteroEQuadraAlCentesimo_PerOgniBustaDiLuglio(
        string nominativo, int ore, int giornate, decimal[][] trasferte, int reddito, int nettoAtteso)
    {
        var cedolino = CalcolaBusta(ore, giornate, trasferte, reddito);

        decimal arrotondamento =
            cedolino.Righe.SingleOrDefault(r => r.Codice == "AGR_ARROT")?.Importo ?? 0m;
        Assert.True(cedolino.NettoInBusta == decimal.Truncate(cedolino.NettoInBusta),
            $"{nominativo}: il netto {cedolino.NettoInBusta} non è un intero.");
        Assert.Equal(nettoAtteso, cedolino.NettoInBusta);
        // Invariante del prospetto: netto = competenze − trattenute + arrotondamento.
        Assert.Equal(
            cedolino.TotaleCompetenze - cedolino.TotaleTrattenute + arrotondamento,
            cedolino.NettoInBusta);
        Assert.Equal(0m, cedolino.IrpefNetta);
    }

    [Fact]
    public void TotaliDelMese_TornanoConIlCasoReale()
    {
        var buste = new List<(decimal[][] Trasferte, Cedolino Cedolino)>();
        foreach (var riga in BusteLuglio2026)
        {
            var (nominativo, ore, giornate, trasferte, reddito, netto) =
                ((string)riga[0], (int)riga[1], (int)riga[2],
                 (decimal[][])riga[3], (int)riga[4], (int)riga[5]);
            buste.Add((trasferte, CalcolaBusta(ore, giornate, trasferte, reddito)));
        }

        Assert.Equal(985m, buste.Sum(b =>
            b.Cedolino.Righe.Single(r => r.Codice == "AGR_ORD").Quantita!.Value));
        Assert.Equal(200m, buste.Sum(b =>
            b.Cedolino.Righe.Single(r => r.Codice == "AGR_GG").Quantita!.Value));
        Assert.Equal(13128.80m, buste.Sum(b => b.Cedolino.TotaleCompetenze));
        Assert.Equal(1624.00m, buste.Sum(b => b.Cedolino.Righe
            .Where(r => r.Codice is "AGR_TRASF" or "AGR_TRASF_IMP").Sum(r => r.Importo)));
        Assert.Equal(12050m, buste.Sum(b => b.Cedolino.NettoInBusta));
        // Con FIMI 0,33% (etichetta = calcolo) le trattenute scendono di 42 centesimi
        // rispetto alla generazione del 2/9 (FIMI 0,3333%: 1.083,95 e arrotondamenti +5,15);
        // gli arrotondamenti compensano e i netti restano identici.
        Assert.Equal(1083.53m, buste.Sum(b => b.Cedolino.TotaleTrattenute));
        Assert.Equal(4.73m, buste.Sum(b =>
            b.Cedolino.Righe.SingleOrDefault(r => r.Codice == "AGR_ARROT")?.Importo ?? 0m));
    }
}
