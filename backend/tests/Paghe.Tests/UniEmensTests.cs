using System.Xml.Linq;
using Paghe.Core.AdeTelematico.UniEmens;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Il flusso UniEmens viene misurato contro lo schema di validazione ufficiale INPS
/// incorporato nel progetto: se il generatore sbaglia un ordine o un formato, il test cade.
/// </summary>
public class UniEmensTests
{
    private static MittenteUniEmens Mittente() => new(
        CfPersonaMittente: "RSSMRA85M01H501Q",
        RagioneSociale: "STUDIO PAGHE SRL",
        CfMittente: "01567890122",
        CfSoftwarehouse: "01567890122",
        SedeInps: "4600",
        Tipo: 2);

    private static TestataAziendaUniEmens Testata() => new(
        Anno: 2025, Mese: 12, CfAzienda: "01567890122",
        RagioneSociale: "ALFA SRL", Matricola: "1234567890");

    private static IReadOnlyList<GiornoUniEmens> CalendarioRealeEsempio(
        IReadOnlySet<int>? giorniLavorati = null)
    {
        giorniLavorati ??= Enumerable.Range(1, 31)
            .Where(g => new DateOnly(2025, 12, g).DayOfWeek != DayOfWeek.Sunday)
            .ToHashSet();
        return Enumerable.Range(1, 31)
            .Select(g => new GiornoUniEmens(g, giorniLavorati.Contains(g)))
            .ToList();
    }

    private static RigaUniEmens Operaio() => new(
        CodiceFiscale: "VRDLGI90A01F205J",
        Cognome: "VERDI", Nome: "LUIGI",
        Qualifica1: "1",
        ImponibileInps: 1_800.40m,
        ContributiDipendente: 165.64m,
        ContributiDatore: 540.15m,
        GiorniRetribuiti: 26)
    {
        Qualifica2 = "M",
        Qualifica3 = "D",
        TipoContribuzione = "00",
        RegimePost95 = "S",
        Cittadinanza = "100",
        CodiceComune = "F205",
        TipoPaga = "M",
        OrarioContrattuale = 4000,
        NumMensilita = 13000,
        Calendario = CalendarioRealeEsempio(),
    };

    private static FlussoUniEmens FlussoEsempio(params RigaUniEmens[] righe) =>
        UniEmensDaCedolini.Costruisci(
            Mittente(), Testata(), righe.Length == 0 ? [Operaio()] : righe);

    // ---- Numerazione delle settimane INPS ----

    /// <summary>
    /// Dicembre 2025 sono le settimane 49-53. Riscontro su un flusso UniEmens reale passato
    /// dal software di controllo ufficiale INPS: la numerazione INPS va da domenica a sabato
    /// e non coincide con quella ISO 8601 delle librerie standard.
    /// </summary>
    [Fact]
    public void Settimane_DicembreDuemilaVentiCinque_SonoQuarantanoveACinquantatre() =>
        Assert.Equal([49, 50, 51, 52, 53], SettimaneInps.DelMese(2025, 12));

    [Fact]
    public void Settimane_LaFrazioneIniziale_DellAnnoEraLaPrima()
    {
        // 1 gennaio 2025 è mercoledì: fino a sabato 4 si resta nella settimana 1,
        // la 2 comincia domenica 5.
        Assert.Equal(1, SettimaneInps.IdSettimana(new DateOnly(2025, 1, 1)));
        Assert.Equal(1, SettimaneInps.IdSettimana(new DateOnly(2025, 1, 4)));
        Assert.Equal(2, SettimaneInps.IdSettimana(new DateOnly(2025, 1, 5)));
    }

    [Fact]
    public void Settimane_AnnoCheIniziaDiDomenica_NonSaltaLaPrima()
    {
        // 1 gennaio 2023 è domenica: apre comunque la settimana 1, la 2 parte il giorno 8.
        Assert.Equal(1, SettimaneInps.IdSettimana(new DateOnly(2023, 1, 1)));
        Assert.Equal(1, SettimaneInps.IdSettimana(new DateOnly(2023, 1, 7)));
        Assert.Equal(2, SettimaneInps.IdSettimana(new DateOnly(2023, 1, 8)));
    }

    // ---- Conformità allo schema ufficiale ----

    [Fact]
    public void Xml_RispettaLoSchemaUfficialeInps()
    {
        string xml = UniEmensXmlWriter.GeneraStringa(FlussoEsempio());
        Assert.Empty(UniEmensValidator.Valida(xml));
    }

    [Fact]
    public void Xml_ConCalendarioPresenze_RispettaLoSchemaUfficialeInps()
    {
        var conCartellino = Operaio() with { };
        var riga = new RigaUniEmens(
            conCartellino.CodiceFiscale, conCartellino.Cognome, conCartellino.Nome,
            conCartellino.Qualifica1, conCartellino.ImponibileInps,
            conCartellino.ContributiDipendente, conCartellino.ContributiDatore,
            GiorniRetribuiti: 22)
        {
            Qualifica2 = "M", Qualifica3 = "D", TipoContribuzione = "H0",
            PercPartTime = 5000, PercPartTimeMese = 5000,
            OreContribuite = 8800, SettimaneUtili = 220, RispettoMinimale = "S",
            OreLavorabili = 17600,
            Calendario = CalendarioRealeEsempio(
                new HashSet<int> { 1, 2, 3, 4, 5, 8, 9, 10, 11, 12 }),
        };

        string xml = UniEmensXmlWriter.GeneraStringa(FlussoEsempio(riga));
        Assert.Empty(UniEmensValidator.Valida(xml));

        var giorni = XDocument.Parse(xml).Descendants("Giorno").ToList();
        Assert.Equal(31, giorni.Count);
        Assert.Equal("S", giorni[0].Element("Lavorato")!.Value);
        Assert.Equal("N", giorni[5].Element("Lavorato")!.Value);  // 6 dicembre: non lavorato
    }

    [Fact]
    public void Costruisci_SenzaCalendario_BloccaSenzaInventareGiorni()
    {
        var senzaCalendario = Operaio() with { Calendario = [] };

        var errore = Assert.Throws<ArgumentException>(() => FlussoEsempio(senzaCalendario));
        Assert.Contains("calendario giornaliero completo e reale", errore.Message);
    }

    [Fact]
    public void Costruisci_ConCalendarioIncompleto_Blocca()
    {
        var incompleto = Operaio() with
        {
            Calendario = CalendarioRealeEsempio().Where(g => g.Giorno != 15).ToList(),
        };

        Assert.Throws<ArgumentException>(() => FlussoEsempio(incompleto));
    }

    /// <summary>Un flusso rotto deve essere respinto: il validatore non è decorativo.</summary>
    [Fact]
    public void Validatore_RifiutaUnaMatricolaNonValida()
    {
        var testataRotta = Testata() with { Matricola = "12345" };
        string xml = UniEmensXmlWriter.GeneraStringa(
            UniEmensDaCedolini.Costruisci(Mittente(), testataRotta, [Operaio()]));
        Assert.NotEmpty(UniEmensValidator.Valida(xml));
    }

    // ---- Formati del tracciato ----

    [Fact]
    public void Formati_ImponibileInEuroInteri_ContributoConLaVirgola()
    {
        string xml = UniEmensXmlWriter.GeneraStringa(FlussoEsempio());
        var retributivi = XDocument.Parse(xml).Descendants("DatiRetributivi").Single();

        // 1.800,40 -> l'imponibile è un intero non negativo nel tracciato.
        Assert.Equal("1800", retributivi.Element("Imponibile")!.Value);
        // 165,64 + 540,15 = 705,79, con la virgola come separatore decimale.
        Assert.Equal("705,79", retributivi.Element("Contributo")!.Value);
    }

    [Fact]
    public void Formati_ContributoZero_SiScriveZeroSecco()
    {
        var riga = new RigaUniEmens(
            "VRDLGI90A01F205J", "VERDI", "LUIGI", "1",
            ImponibileInps: 0m, ContributiDipendente: 0m, ContributiDatore: 0m,
            GiorniRetribuiti: 0)
        {
            Calendario = CalendarioRealeEsempio(new HashSet<int>()),
        };
        string xml = UniEmensXmlWriter.GeneraStringa(FlussoEsempio(riga));

        Assert.Equal("0", XDocument.Parse(xml).Descendants("Contributo").Single().Value);
        Assert.Empty(UniEmensValidator.Valida(xml));
    }

    [Fact]
    public void Testata_MittenteEAziendaFinisconoNelFlusso()
    {
        var radice = UniEmensXmlWriter.Genera(FlussoEsempio()).Root!;

        Assert.Equal("DenunceMensili", radice.Name.LocalName);
        Assert.Equal("2", radice.Element("DatiMittente")!.Attribute("Tipo")!.Value);
        var azienda = radice.Element("Azienda")!;
        Assert.Equal("2025-12", azienda.Element("AnnoMeseDenuncia")!.Value);
        Assert.Equal("1234567890", azienda.Element("PosContributiva")!.Element("Matricola")!.Value);
        Assert.Equal("FP", azienda.Element("PosContributiva")!.Attribute("Composizione")!.Value);
    }

    // ---- Quadratura aziendale ----

    /// <summary>
    /// La forza aziendale conta i part-time in proporzione all'orario: quattro al 25% fanno
    /// un dipendente, mentre i lavoratori denunciati restano quattro.
    /// </summary>
    [Fact]
    public void Quadratura_PartTime_ContanoInProporzioneNellaForzaAziendale()
    {
        var righe = Enumerable.Range(0, 4)
            .Select(i => new RigaUniEmens(
                $"VRDLGI90A01F205J", "VERDI", $"LUIGI{i}", "1",
                ImponibileInps: 441m, ContributiDipendente: 40m, ContributiDatore: 136.66m,
                GiorniRetribuiti: 23)
            {
                PercPartTime = 2500,
                Calendario = CalendarioRealeEsempio(),
            })
            .ToList();

        var aziendale = UniEmensDaCedolini.Costruisci(Mittente(), Testata(), righe)
            .Azienda.Posizione.Aziendale;

        Assert.Equal(4, aziendale.NumLavoratori);
        Assert.Equal(1, aziendale.ForzaAziendale);
        Assert.Equal(4, aziendale.NumDenIndiv);
        Assert.Equal(706.64m, aziendale.TotaleADebito);
    }

    /// <summary>
    /// Regole imparate dal software di controllo ufficiale INPS (versione 4.2.0) facendogli
    /// elaborare un flusso prodotto da qui: unità operativa e produttiva sono obbligatorie
    /// anche quando valgono zero (DR01923), i giorni contribuiti servono ogni volta che c'è
    /// un contributo (DR00085) e ogni settimana esposta vuole i suoi giorni (EM00017).
    /// </summary>
    [Fact]
    public void ControlliInps_ElementiObbligatoriSemprePresenti()
    {
        var retributivi = XDocument.Parse(UniEmensXmlWriter.GeneraStringa(FlussoEsempio()))
            .Descendants("DenunciaIndividuale").Single();

        Assert.Equal("0", retributivi.Element("UnitaOperativa")!.Value);
        Assert.Equal("0", retributivi.Element("UnitaProduttiva")!.Value);

        var dati = retributivi.Element("DatiRetributivi")!;
        Assert.Equal("26", dati.Element("GiorniContribuiti")!.Value);
        Assert.NotEmpty(dati.Elements("Settimana"));
        Assert.Equal(31, dati.Elements("Giorno").Count());
    }

    /// <summary>Per i part-time il tracciato vuole le ore, non i giorni: sono alternativi.</summary>
    [Fact]
    public void ControlliInps_PartTime_EspongonoLeOreNonIGiorniContribuiti()
    {
        var partTime = new RigaUniEmens(
            "VRDLGI90A01F205J", "VERDI", "LUIGI", "1",
            ImponibileInps: 900m, ContributiDipendente: 82m, ContributiDatore: 270m,
            GiorniRetribuiti: 26)
        {
            OreContribuite = 8800,
            PercPartTime = 5000,
            Calendario = CalendarioRealeEsempio(),
        };

        var dati = XDocument.Parse(UniEmensXmlWriter.GeneraStringa(FlussoEsempio(partTime)))
            .Descendants("DatiRetributivi").Single();

        Assert.Equal("8800", dati.Element("OreContribuite")!.Value);
        Assert.Null(dati.Element("GiorniContribuiti"));
    }

    /// <summary>
    /// Il tracciato ammette solo maiuscole e pochi segni: l'anagrafica invece conserva i nomi
    /// come si scrivono. La conversione avviene sul file, non nell'archivio.
    /// </summary>
    [Fact]
    public void Nomi_ConAccentiEMinuscole_VengonoAdattatiAlTracciato()
    {
        var riga = new RigaUniEmens(
            "VRDLGI90A01F205J", "D'Amico", "Nicolò", "1",
            ImponibileInps: 1_000m, ContributiDipendente: 90m, ContributiDatore: 300m,
            GiorniRetribuiti: 26)
        {
            Calendario = CalendarioRealeEsempio(),
        };

        string xml = UniEmensXmlWriter.GeneraStringa(FlussoEsempio(riga));
        var lavoratore = XDocument.Parse(xml).Descendants("DenunciaIndividuale").Single();

        Assert.Equal("D'AMICO", lavoratore.Element("Cognome")!.Value);
        Assert.Equal("NICOLO", lavoratore.Element("Nome")!.Value);
        Assert.Empty(UniEmensValidator.Valida(xml));
    }

    // ---- Anagrafica contributiva ----

    [Theory]
    [InlineData("P", "P")]
    [InlineData("v", "V")]
    [InlineData(" M ", "M")]
    [InlineData("F", null)]      // il tempo pieno lo scrive il generatore, non l'operatore
    [InlineData("orizzontale", null)]
    [InlineData("", null)]
    public void Anagrafica_TipoPartTime_AccettaSoloICodiciDelTracciato(string dato, string? atteso) =>
        Assert.Equal(atteso, AnagraficaContributiva.TipoPartTime(dato));

    [Theory]
    [InlineData("330", "330")]
    [InlineData("100", "100")]   // la forma è valida: se il codice esista lo dice l'INPS
    [InlineData("33", null)]
    [InlineData("IT", null)]
    [InlineData("Italia", null)]
    public void Anagrafica_Cittadinanza_VuoleTreCifre(string dato, string? atteso) =>
        Assert.Equal(atteso, AnagraficaContributiva.Cittadinanza(dato));

    [Theory]
    [InlineData("8.1.3.3.0.2", "8.1.3.3.0.2")]
    [InlineData("8.1.3.3.02", "8.1.3.3.02")]
    [InlineData("8.1.3.3", null)]
    [InlineData("0.1.3.3.0.2", null)]   // il primo gruppo non può essere zero
    [InlineData("operaio agricolo", null)]
    public void Anagrafica_QualificaIstat_SegueIlFormatoDelloSchema(string dato, string? atteso) =>
        Assert.Equal(atteso, AnagraficaContributiva.QualificaProfessionaleIstat(dato));

    [Theory]
    [InlineData("00", "00")]
    [InlineData("g0", "G0")]
    [InlineData("000", null)]
    [InlineData("A", null)]
    public void Anagrafica_TipoContribuzione_VuoleDueCaratteri(string dato, string? atteso) =>
        Assert.Equal(atteso, AnagraficaContributiva.TipoContribuzione(dato));

    [Fact]
    public void Xml_Malattia_EsponeDettaglioGiornalieroESommePerEvento()
    {
        const string puc = "12345678901234567890";
        var giorni = CalendarioRealeEsempio().Select(g => g.Giorno is >= 8 and <= 14
            ? new GiornoUniEmens(g.Giorno, Lavorato: false)
            {
                TipoCopertura = g.Giorno is 11 or 12 ? "1" : "0",
                Eventi =
                [
                    new EventoGiornalieroUniEmens("MAL")
                    {
                        TipoInformazioneAggiuntiva = "CM",
                        InformazioneAggiuntiva = puc,
                    },
                ],
            }
            : g).ToList();
        var riga = Operaio() with
        {
            Calendario = giorni,
            CopertureSettimanali =
            [
                new SettimanaUniEmens(50, "1") { CodiciEvento = ["MAL"] },
            ],
            DifferenzeAccredito =
            [
                new DifferenzaAccreditoUniEmens("MAL", 200m)
                {
                    QuoteEvento =
                    [
                        new QuotaDifferenzaAccreditoUniEmens(puc, "CM", 200m),
                    ],
                },
            ],
        };

        string xml = UniEmensXmlWriter.GeneraStringa(FlussoEsempio(riga));
        Assert.Empty(UniEmensValidator.Valida(xml));

        var documento = XDocument.Parse(xml);
        var giorno11 = documento.Descendants("Giorno")
            .Single(g => g.Attribute("GG")!.Value == "11");
        Assert.Equal("1", giorno11.Element("TipoCoperturaGiorn")!.Value);
        Assert.Equal("MAL", giorno11.Descendants("CodiceEventoGiorn").Single().Value);
        Assert.Equal("CM", giorno11.Descendants("InfoAggEvento").Single()
            .Attribute("TipoInfoAggEvento")!.Value);
        Assert.Equal(puc, documento.Descendants("MotivoEvento").Single().Value);
        Assert.Equal("200", documento.Descendants("DiffAccredito").Single().Value);
    }

    [Fact]
    public void Costruisci_SenzaLavoratori_LanciaEccezione() =>
        Assert.Throws<ArgumentException>(() =>
            UniEmensDaCedolini.Costruisci(Mittente(), Testata(), []));
}
