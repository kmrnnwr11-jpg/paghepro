using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Contabilita;
using Paghe.Core.Fatturazione;
using Paghe.Core.PartiteIva;
using Paghe.Persistence;
using Xunit;

namespace Paghe.Tests;

public class PianoAperturaPartitaIvaTests
{
    [Fact]
    public void Professionista_UsaAa9ENonRichiedeCameraCommercio()
    {
        var piano = PianoAperturaPartitaIvaBuilder.Crea(new PraticaAperturaPartitaIva
        {
            FormaGiuridica = FormaGiuridicaApertura.ProfessionistaPersonaFisica,
            RegimeContabile = RegimeContabilePartitaIva.Forfettario,
            DataInizioAttivita = new DateOnly(2026, 7, 1),
            Denominazione = "Mario Rossi",
            CodiceFiscale = "RSSMRA80A01F205X",
            CodiceAteco = "70.22.09",
            DescrizioneAttivita = "Consulenza aziendale",
            IndirizzoSede = "Via Roma 1",
            Cap = "20100",
            Comune = "Milano",
            Provincia = "MI",
        });

        Assert.Equal("AA9/12", piano.ModelloAgenziaEntrate);
        Assert.False(piano.RichiedeCameraCommercio);
        Assert.Contains(piano.Adempimenti, a => a.Codice == "AE-INIZIO-IVA");
        Assert.Contains(piano.Adempimenti, a => a.Codice == "PREVIDENZA");
    }

    [Fact]
    public void ImpresaCommerciale_RichiedeComUnicaCameraInpsESuap()
    {
        var piano = PianoAperturaPartitaIvaBuilder.Crea(new PraticaAperturaPartitaIva
        {
            FormaGiuridica = FormaGiuridicaApertura.ImpresaIndividuale,
            RegimeContabile = RegimeContabilePartitaIva.Semplificato,
            DataInizioAttivita = new DateOnly(2026, 7, 1),
            Denominazione = "Rossi Market",
            CodiceFiscale = "RSSMRA80A01F205X",
            CodiceAteco = "47.11.40",
            DescrizioneAttivita = "Minimarket",
            IndirizzoSede = "Via Roma 1",
            Cap = "20100",
            Comune = "Milano",
            Provincia = "MI",
            AttivitaCommercio = true,
        });

        Assert.True(piano.RichiedeComunicazioneUnica);
        Assert.True(piano.RichiedeCameraCommercio);
        Assert.Contains(piano.Adempimenti, a => a.Codice == "COMUNICA");
        Assert.Contains(piano.Adempimenti, a => a.Codice == "RI-REA");
        Assert.Contains(piano.Adempimenti, a => a.Codice == "INPS-COM");
        Assert.Contains(piano.Adempimenti, a => a.Codice == "SUAP-SCIA");
    }

    [Fact]
    public void ImpresaAgricola_RichiedeDenunciaAziendaleInps()
    {
        var piano = PianoAperturaPartitaIvaBuilder.Crea(Pratica(agricola: true));

        Assert.Contains(piano.Adempimenti, a => a.Codice == "INPS-DA");
    }

    [Fact]
    public void Variazione_GeneraAdempimentiDiVariazione()
    {
        var piano = PianoAperturaPartitaIvaBuilder.Crea(
            Pratica(TipoPraticaComUnica.Variazione, partitaIva: "01234567890", inail: true));

        Assert.Contains(piano.Adempimenti, a => a.Codice == "AE-VARIAZIONE-IVA");
        Assert.Contains(piano.Adempimenti, a => a.Codice == "COMUNICA");
        Assert.Contains(piano.Adempimenti, a => a.Codice == "INAIL");
        Assert.DoesNotContain(piano.Adempimenti, a => a.Codice == "AE-INIZIO-IVA");
    }

    [Fact]
    public void Cessazione_GeneraChiusurePosizioni()
    {
        var piano = PianoAperturaPartitaIvaBuilder.Crea(
            Pratica(TipoPraticaComUnica.Cessazione, partitaIva: "01234567890", commercio: true));

        Assert.Contains(piano.Adempimenti, a => a.Codice == "AE-CESSAZIONE-IVA");
        Assert.Contains(piano.Adempimenti, a => a.Codice == "INPS-COM");
        Assert.Contains(piano.Adempimenti, a => a.Codice == "CONTABILITA");
    }

    [Fact]
    public void VariazioneSenzaPartitaIva_Rifiutata()
    {
        Assert.Throws<ArgumentException>(() =>
            PianoAperturaPartitaIvaBuilder.Crea(Pratica(TipoPraticaComUnica.Variazione)));
    }

    private static PraticaAperturaPartitaIva Pratica(
        TipoPraticaComUnica tipo = TipoPraticaComUnica.NuovaImpresa,
        string? partitaIva = null,
        bool commercio = false,
        bool agricola = false,
        bool inail = false) => new()
    {
        TipoPratica = tipo,
        FormaGiuridica = FormaGiuridicaApertura.ImpresaIndividuale,
        RegimeContabile = RegimeContabilePartitaIva.Semplificato,
        DataInizioAttivita = new DateOnly(2026, 7, 1),
        Denominazione = "Rossi Market",
        CodiceFiscale = "RSSMRA80A01F205X",
        PartitaIva = partitaIva,
        CodiceAteco = "47.11.40",
        DescrizioneAttivita = "Minimarket",
        IndirizzoSede = "Via Roma 1",
        Cap = "20100",
        Comune = "Milano",
        Provincia = "MI",
        AttivitaCommercio = commercio,
        ImpresaAgricola = agricola,
        SoggettaInail = inail,
    };
}

public class EstrattoreTestoPraticaTests
{
    [Fact]
    public void MessaggioCompleto_EstraeTuttiIDati()
    {
        var dati = EstrattoreTestoPratica.Estrai("""
            Apertura nuova impresa individuale, regime forfettario, dal 01/09/2026.
            Nome e cognome: Luigi Bianchi
            Codice fiscale: BNCLGU75B02F839W
            Codice ATECO: 47.11.40
            Descrizione attività: minimarket di quartiere (negozio, vendita al dettaglio)
            Indirizzo sede: Via Napoli 12 - 80100 Napoli (NA)
            PEC: luigi.bianchi@pec.it
            """);

        Assert.Equal(TipoPraticaComUnica.NuovaImpresa, dati.TipoPratica);
        Assert.Equal(FormaGiuridicaApertura.ImpresaIndividuale, dati.FormaGiuridica);
        Assert.Equal(RegimeContabilePartitaIva.Forfettario, dati.RegimeContabile);
        Assert.Equal(new DateOnly(2026, 9, 1), dati.DataDecorrenza);
        Assert.Equal("Luigi Bianchi", dati.Denominazione);
        Assert.Equal("BNCLGU75B02F839W", dati.CodiceFiscale);
        Assert.Equal("47.11.40", dati.CodiceAteco);
        Assert.Equal("luigi.bianchi@pec.it", dati.Pec);
        Assert.Equal("80100", dati.Cap);
        Assert.Equal("Napoli", dati.Comune);
        Assert.Equal("NA", dati.Provincia);
        Assert.True(dati.AttivitaCommercio);
        Assert.Empty(dati.CampiMancanti);
    }

    [Fact]
    public void MessaggioParziale_ElencaICampiMancanti()
    {
        var dati = EstrattoreTestoPratica.Estrai(
            "Il cliente vuole chiudere la partita iva, cessazione dal 31/12/2026. CF BNCLGU75B02F839W");

        Assert.Equal(TipoPraticaComUnica.Cessazione, dati.TipoPratica);
        Assert.Equal("BNCLGU75B02F839W", dati.CodiceFiscale);
        Assert.Contains(dati.CampiMancanti, c => c.Contains("partita IVA"));
        Assert.Contains(dati.CampiMancanti, c => c.Contains("codice ATECO"));
        Assert.Contains(dati.CampiMancanti, c => c.Contains("denominazione"));
    }

    [Fact]
    public void FraseNaturale_EstraeNomeSenzaTrascinareIlResto()
    {
        var dati = EstrattoreTestoPratica.Estrai(
            "Apertura impresa individuale forfettario. Si chiama Anna Verdi, codice fiscale VRDNNA85C45H501T. Fa la parrucchiera.");

        Assert.Equal("Anna Verdi", dati.Denominazione);
        Assert.Equal("VRDNNA85C45H501T", dati.CodiceFiscale);
        Assert.True(dati.AttivitaArtigiana);
    }

    [Fact]
    public void SedeInlineEDescrizioneConPunto_EstratteSenzaSbavature()
    {
        var dati = EstrattoreTestoPratica.Estrai(
            "ATECO: 96.02.01 — descrizione: salone di parrucchiera. Sede: Via Garibaldi 3 - 40121 Bologna (BO)");

        Assert.Equal("salone di parrucchiera", dati.DescrizioneAttivita);
        Assert.Equal("Via Garibaldi 3", dati.Indirizzo);
        Assert.Equal("40121", dati.Cap);
        Assert.Equal("Bologna", dati.Comune);
        Assert.Equal("BO", dati.Provincia);
    }

    [Fact]
    public void TestoVuoto_NonEsplodeEChiedeTutto()
    {
        var dati = EstrattoreTestoPratica.Estrai("");

        Assert.Null(dati.TipoPratica);
        Assert.NotEmpty(dati.CampiMancanti);
    }
}

public class StatiPraticaComUnicaTests
{
    [Fact]
    public void FlussoCompleto_Ammesso()
    {
        Assert.True(StatiPraticaComUnica.PuoTransizionare(StatoPraticaComUnica.Bozza, StatoPraticaComUnica.Pronta));
        Assert.True(StatiPraticaComUnica.PuoTransizionare(StatoPraticaComUnica.Pronta, StatoPraticaComUnica.Presentata));
        Assert.True(StatiPraticaComUnica.PuoTransizionare(StatoPraticaComUnica.Presentata, StatoPraticaComUnica.Protocollata));
        Assert.True(StatiPraticaComUnica.PuoTransizionare(StatoPraticaComUnica.Protocollata, StatoPraticaComUnica.Evasa));
        Assert.True(StatiPraticaComUnica.PuoTransizionare(StatoPraticaComUnica.Scartata, StatoPraticaComUnica.Bozza));
    }

    [Fact]
    public void SaltiEStatiFinali_Vietati()
    {
        Assert.False(StatiPraticaComUnica.PuoTransizionare(StatoPraticaComUnica.Bozza, StatoPraticaComUnica.Protocollata));
        Assert.False(StatiPraticaComUnica.PuoTransizionare(StatoPraticaComUnica.Evasa, StatoPraticaComUnica.Bozza));
        Assert.False(StatiPraticaComUnica.PuoTransizionare(StatoPraticaComUnica.Annullata, StatoPraticaComUnica.Bozza));
    }
}

public class LiquidazioneIvaTests
{
    [Fact]
    public void Calcola_IvaVenditeMenoAcquistiDetraibili()
    {
        var fattura = new Fattura
        {
            Numero = "1/2026",
            Data = new DateOnly(2026, 7, 10),
            Cedente = Soggetto("Alfa Srl", "01234567890"),
            Cessionario = Soggetto("Beta Spa", "09876543210"),
            Righe = { new RigaFattura { Descrizione = "Servizi", PrezzoUnitario = 100m, AliquotaIva = 22m } },
        };
        var spesa = new Spesa
        {
            Data = new DateOnly(2026, 7, 11),
            Descrizione = "Software",
            ContoCosto = "6400",
            Imponibile = 50m,
            AliquotaIva = 22m,
        };

        var liq = CalcoloLiquidazioneIva.Calcola(
            new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            PeriodicitaIva.Mensile, new[] { fattura }, new[] { spesa });

        Assert.Equal(22m, liq.IvaVendite);
        Assert.Equal(11m, liq.IvaAcquistiDetraibile);
        Assert.Equal(11m, liq.IvaDaVersare);
        Assert.Equal(0m, liq.CreditoDaRiportare);
    }

    private static DatiAnagrafici Soggetto(string denominazione, string partitaIva) => new()
    {
        Denominazione = denominazione,
        PartitaIva = partitaIva,
        Indirizzo = "Via Roma 1",
        Cap = "20100",
        Comune = "Milano",
        Provincia = "MI",
    };
}

public sealed class PraticheAperturaPartitaIvaServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public PraticheAperturaPartitaIvaServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task Crea_E_IsolaPerTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        Guid praticaId;
        await using (var db = NuovoContesto())
        {
            var pratica = await new PraticheAperturaPartitaIvaService(db).CreaAsync(tenantA, Input());
            praticaId = pratica.Id;
            Assert.NotEmpty(pratica.Adempimenti);
        }

        await using (var db = NuovoContesto())
        {
            var svc = new PraticheAperturaPartitaIvaService(db);
            Assert.NotNull(await svc.GetAsync(tenantA, praticaId));
            Assert.Null(await svc.GetAsync(tenantB, praticaId));
        }
    }

    [Fact]
    public async Task CambiaStato_ValidaTransizioni()
    {
        var tenant = Guid.NewGuid();

        await using var db = NuovoContesto();
        var svc = new PraticheAperturaPartitaIvaService(db);
        var pratica = await svc.CreaAsync(tenant, Input());

        var aggiornata = await svc.CambiaStatoAsync(tenant, pratica.Id, "Pronta");
        Assert.Equal("Pronta", aggiornata.Stato);

        // Salto di stato vietato: Pronta → Protocollata senza presentazione.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CambiaStatoAsync(tenant, pratica.Id, "Protocollata"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.CambiaStatoAsync(tenant, pratica.Id, "StatoInventato"));
    }

    [Fact]
    public async Task Distinta_RiepilogaDatiEAdempimenti()
    {
        var tenant = Guid.NewGuid();

        await using var db = NuovoContesto();
        var svc = new PraticheAperturaPartitaIvaService(db);
        var pratica = await svc.CreaAsync(tenant, Input(TipoPraticaComUnica.Cessazione, "01234567890"));

        var distinta = await svc.DistintaAsync(tenant, pratica.Id);

        Assert.Contains("Rossi Market", distinta);
        Assert.Contains("Cessazione attività", distinta);
        Assert.Contains("01234567890", distinta);
        Assert.Contains("Agenzia Entrate", distinta);
        Assert.Contains("intermediario abilitato", distinta);
    }

    private static PraticaAperturaPartitaIvaInput Input(
        TipoPraticaComUnica tipo = TipoPraticaComUnica.NuovaImpresa,
        string? partitaIva = null) => new(
        FormaGiuridica: FormaGiuridicaApertura.ImpresaIndividuale,
        RegimeContabile: RegimeContabilePartitaIva.Semplificato,
        DataInizioAttivita: new DateOnly(2026, 7, 1),
        Denominazione: "Rossi Market",
        CodiceFiscale: "RSSMRA80A01F205X",
        CodiceAteco: "47.11.40",
        DescrizioneAttivita: "Minimarket",
        IndirizzoSede: "Via Roma 1",
        Cap: "20100",
        Comune: "Milano",
        Provincia: "MI",
        PartitaIva: partitaIva,
        AttivitaCommercio: true,
        TipoPratica: tipo);

    public void Dispose() => _connection.Dispose();
}
