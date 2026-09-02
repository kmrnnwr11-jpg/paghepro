using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Calcolo;
using Paghe.Core.Domain;
using Paghe.Core.Parametri;
using Paghe.Documenti;
using Paghe.Persistence;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Regressioni del caso Officina Nord / Giulia Bianchi, luglio 2026. Questi test non
/// certificano un cedolino: fissano i dati confermati dalla revisione e impediscono che il
/// prospetto ordinario incorpori dati presunti o la quattordicesima separata.
/// </summary>
public sealed class RevisioneConsulenteCommercioTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ParametriNormativi _parametri;
    private readonly Ccnl _ccnl;

    public RevisioneConsulenteCommercioTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = new PagheDbContext(
            new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);
        db.Database.EnsureCreated();
        var servizio = new ParametriNormativiService(db);
        servizio.ImportaSeedSeVuotoAsync().GetAwaiter().GetResult();
        new CcnlImportService(db).ImportaAsync(CcnlDatasetUfficiale.Carica()).GetAwaiter().GetResult();
        RegoleCcnlDataset.ImportaAsync(db, RegoleCcnlDataset.Carica()).GetAwaiter().GetResult();
        _parametri = servizio.CaricaParametriAsync(2026).GetAwaiter().GetResult();
        _ccnl = servizio.CaricaCcnlAsync("COMMERCIO_TERZIARIO", 2026).GetAwaiter().GetResult();
    }

    private static RapportoLavoro RapportoOfficina(
        bool oltre15 = false,
        bool aderisceFondi = true,
        decimal aliquotaDipendente = 0.0949m,
        decimal aliquotaDatore = 0.2788m) => new()
    {
        CodiceCcnl = "COMMERCIO_TERZIARIO",
        Livello = "3",
        Qualifica = Qualifica.Impiegato,
        NumeroMensilita = 14,
        DataAssunzione = new DateOnly(2022, 9, 1),
        ProvinciaLavoro = "MI",
        Regione = "Lombardia",
        CodiceCatastaleComune = "F205",
        AziendaOltre15Dipendenti = oltre15,
        AderisceFondiContrattuali = aderisceFondi,
        AliquotaInpsDipendenteProfilo = aliquotaDipendente,
        AliquotaInpsDatoreProfilo = aliquotaDatore,
        AddizionaleRegionaleConguaglioAnnuo = 0m,
        AddizionaleComunaleSaldoAnnuo = 0m,
        AddizionaleComunaleAccontoAnnuo = 0m,
        RolResiduiIniziali = 0m,
        ExFestivitaResidueIniziali = 0m,
    };

    private static Cartellino Luglio(
        decimal giorniMalattia = 0m,
        decimal giorniInfortunio = 0m,
        string? identificativoEvento = null,
        DateOnly? dataInizioEvento = null,
        decimal? rmg = null,
        decimal? ratei = null,
        decimal? lordizzazione = null) => new()
    {
        Anno = 2026,
        Mese = 7,
        StatoAssegniFamiliari = StatoAssegniFamiliari.NonSpettanti,
        RiferimentoQuattordicesimaSeparata = "Q14-OFFICINA-NORD-2026-07",
        DataPagamentoQuattordicesima = new DateOnly(2026, 7, 1),
        GiorniMalattia = giorniMalattia,
        GiorniInfortunio = giorniInfortunio,
        IdentificativoEvento = identificativoEvento,
        DataInizioEvento = dataInizioEvento,
        RetribuzioneMediaGiornalieraPrecedente = rmg,
        RateiGiornalieri = ratei,
        CoefficienteLordizzazione = lordizzazione,
    };

    private Cedolino Calcola(RapportoLavoro? rapporto = null, Cartellino? cartellino = null) =>
        new MotoreCalcoloCedolino(_parametri, _ccnl)
            .Calcola(rapporto ?? RapportoOfficina(), cartellino ?? Luglio());

    private static CedolinoRiga Riga(Cedolino cedolino, string codice) =>
        cedolino.Righe.Single(r => r.Codice == codice);

    [Fact]
    public void H011_LivelloTre_UsaComponentiConfermate_SenzaEdrSeparato()
    {
        Assert.Equal("H011", _ccnl.CodiceCnel);
        var livello = _ccnl.Livello("3");
        Assert.Equal(1_981.84m, livello.MinimoTabellare);
        Assert.Equal(527.90m, livello.Contingenza);
        Assert.Equal(0m, livello.Edr);
        Assert.Equal(21.95m, livello.ImportoScatto);

        var cedolino = Calcola();
        Assert.Equal(1_453.94m, Riga(cedolino, "PAGA_BASE").Importo);
        Assert.Equal(527.90m, Riga(cedolino, "CONTINGENZA").Importo);
        Assert.DoesNotContain(cedolino.Righe, r => r.Codice == "EDR");
    }

    [Fact]
    public void Scatto_DecorreDalMeseSuccessivo_AlTriennio()
    {
        var motore = new MotoreCalcoloCedolino(_parametri, _ccnl);
        var rapporto = RapportoOfficina();

        var settembre = motore.Calcola(rapporto, new Cartellino { Anno = 2025, Mese = 9 });
        var ottobre = motore.Calcola(rapporto, new Cartellino { Anno = 2025, Mese = 10 });
        var luglio = Calcola();

        Assert.DoesNotContain(settembre.Righe, r => r.Codice == "SCATTI");
        Assert.Equal(1m, Riga(ottobre, "SCATTI").Quantita);
        Assert.Equal(21.95m, Riga(luglio, "SCATTI").Importo);
    }

    [Fact]
    public void RetribuzioneFissa_Tariffe_E_TerzoElemento_CorrispondonoAllaRevisione()
    {
        var cedolino = Calcola();

        Assert.Equal(11.36m, Riga(cedolino, "TERZO_EL").Importo);
        Assert.Equal(2_015.15m, cedolino.RetribuzioneFissa);
        Assert.Equal(11.9949m, decimal.Round(cedolino.TariffaOraria, 4));
        Assert.Equal(77.5058m, decimal.Round(cedolino.TariffaGiornaliera, 4));
        Assert.Equal(2_015.15m, cedolino.TotaleCompetenze);
        Assert.Equal(2_015.15m, cedolino.ImponibileInps);
    }

    [Fact]
    public void Luglio_RichiamaLaQuattordicesimaSeparata_MaNonLaSomma()
    {
        var cedolino = Calcola();
        var richiamo = Riga(cedolino, "QUATTORDICESIMA_SEPARATA");

        Assert.Equal(TipoVoce.Figurativa, richiamo.Tipo);
        Assert.Equal(0m, richiamo.Importo);
        Assert.Contains("Q14-OFFICINA-NORD-2026-07", richiamo.Descrizione);
        Assert.DoesNotContain(cedolino.Righe, r => r.Codice == "QUATTORDICESIMA");
        Assert.Equal(2_015.15m, cedolino.TotaleCompetenze);
    }

    [Fact]
    public void AliquoteProvengonoDalProfilo_E_SonoEsposteConLaPrecisioneUsata()
    {
        var cedolino = Calcola();

        Assert.Equal(191.24m, cedolino.ContributiDipendente);
        Assert.Equal(561.82m, cedolino.ContributiDatore);
        Assert.Equal(0.0949m,
            decimal.Round(cedolino.ContributiDipendente / cedolino.ImponibileInps, 4));
        Assert.Equal(0.2788m,
            decimal.Round(cedolino.ContributiDatore / cedolino.ImponibileInps, 4));
        Assert.Contains("9,49%", Riga(cedolino, "INPS_DIP").Descrizione);
        Assert.Contains("27,88%", Riga(cedolino, "INPS_DATORE").Descrizione);
    }

    [Fact]
    public void Addizionali_NonSonoCalcolateDirettamenteSulMese()
    {
        var senzaConguaglio = new RapportoLavoro
        {
            CodiceCcnl = "COMMERCIO_TERZIARIO",
            Livello = "3",
            NumeroMensilita = 14,
            DataAssunzione = new DateOnly(2022, 9, 1),
            ProvinciaLavoro = "MI",
            Regione = "Lombardia",
            CodiceCatastaleComune = "F205",
            AliquotaInpsDipendenteProfilo = 0.0949m,
            AliquotaInpsDatoreProfilo = 0.2788m,
        };
        var cedolino = Calcola(senzaConguaglio);

        Assert.Equal(0m, cedolino.AddizionaleRegionale);
        Assert.Equal(0m, cedolino.AddizionaleComunale);
        Assert.Contains(cedolino.Righe, r => r.Codice == "ADD_REG_MANCANTE");
        Assert.Contains(cedolino.Righe, r => r.Codice == "ADD_COM_MANCANTE");
    }

    [Fact]
    public void Addizionali_UsanoSoloLeRateDelConguaglioAnnuale()
    {
        var rapporto = new RapportoLavoro
        {
            CodiceCcnl = "COMMERCIO_TERZIARIO",
            Livello = "3",
            NumeroMensilita = 14,
            DataAssunzione = new DateOnly(2022, 9, 1),
            ProvinciaLavoro = "MI",
            Regione = "Lombardia",
            CodiceCatastaleComune = "F205",
            AliquotaInpsDipendenteProfilo = 0.0949m,
            AliquotaInpsDatoreProfilo = 0.2788m,
            AddizionaleRegionaleConguaglioAnnuo = 355.94m,
            AddizionaleComunaleSaldoAnnuo = 206.81m,
            AddizionaleComunaleAccontoAnnuo = 62.04m,
        };
        var cedolino = Calcola(rapporto);

        Assert.Equal(32.36m, cedolino.AddizionaleRegionale);
        Assert.Equal(25.69m, cedolino.AddizionaleComunale);
        Assert.Contains("rata 7 di 11", Riga(cedolino, "ADD_REG").Descrizione);
        Assert.Contains("rata 5 di 9", Riga(cedolino, "ADD_COM_ACC").Descrizione);
    }

    [Fact]
    public void RolEdExFestivita_SonoSeparati_E_MaturanoPrimaDelGoduto()
    {
        var fino15 = Calcola();
        var oltre15 = Calcola(RapportoOfficina(oltre15: true));

        Assert.Equal(16.33m, fino15.RolMaturati);
        Assert.Equal(18.67m, fino15.ExFestivitaMaturate);
        Assert.Equal(35.00m, fino15.PermessiMaturati);
        Assert.Equal(21.00m, oltre15.RolMaturati);
        Assert.Equal(18.67m, oltre15.ExFestivitaMaturate);
        Assert.Equal(39.67m, oltre15.PermessiMaturati);
    }

    [Fact]
    public void Tfr_PerCodice_Fondi_E_CostiSonoSeparati()
    {
        var cedolino = Calcola();

        Assert.Equal(117.17m, Riga(cedolino, "TFR_PAGA_BASE").Importo);
        Assert.Equal(42.54m, Riga(cedolino, "TFR_CONTINGENZA").Importo);
        Assert.Equal(0.92m, Riga(cedolino, "TFR_TERZO_EL").Importo);
        Assert.Equal(1.77m, Riga(cedolino, "TFR_SCATTI").Importo);
        Assert.Equal(162.40m, cedolino.QuotaTfr);
        Assert.Equal(-2.00m, Riga(cedolino, "FONDO_SAN").Importo);
        Assert.Equal(-0.99m, Riga(cedolino, "ENTE_BIL").Importo);
        Assert.Equal(13.00m, Riga(cedolino, "FONDO_SAN_DATORE").Importo);
        Assert.Equal(1.98m, Riga(cedolino, "ENTE_BIL_DATORE").Importo);
        Assert.Equal(2_755.65m, cedolino.CostoAzienda);
        Assert.Equal(3_185.48m, cedolino.CostoMedioAnnualizzato);
    }

    [Fact]
    public void Malattia_SeparaPrestazioneInps_FiscaleNonContributiva_ERecuperabile()
    {
        var cedolino = Calcola(cartellino: Luglio(
            giorniMalattia: 5,
            identificativoEvento: "MAL-2026-0001",
            dataInizioEvento: new DateOnly(2026, 7, 6),
            rmg: 70m,
            ratei: 5m,
            lordizzazione: 1.10m));

        Assert.Equal(3m, Riga(cedolino, "MAL_CARENZA").Quantita);
        Assert.Equal(2m, Riga(cedolino, "MAL_INPS").Quantita);
        Assert.Equal(75.00m, Riga(cedolino, "MAL_INPS").Importo);
        Assert.Equal(75.00m, cedolino.PrestazioniRecuperabili);
        Assert.Equal(
            cedolino.ImponibileInps + cedolino.PrestazioniRecuperabili - cedolino.ContributiDipendente,
            cedolino.ImponibileIrpef);
        Assert.Equal(-75.00m, Riga(cedolino, "PREST_RECUPERABILI").Importo);
        Assert.Contains("RMG precedente + ratei 75,0000", Riga(cedolino, "MAL_INPS").Descrizione);
        Assert.Contains("lordizzazione 1,1000", Riga(cedolino, "MAL_INPS").Descrizione);
    }

    [Fact]
    public void Infortunio_DistingueEvento_Carenza_EPeriodoInail()
    {
        var cedolino = Calcola(cartellino: Luglio(
            giorniInfortunio: 6,
            identificativoEvento: "INAIL-2026-0042",
            dataInizioEvento: new DateOnly(2026, 7, 6),
            rmg: 70m,
            ratei: 5m,
            lordizzazione: 1m));

        Assert.Equal(1m, Riga(cedolino, "INF_EVENTO").Quantita);
        Assert.Contains("azienda 100%", Riga(cedolino, "INF_EVENTO").Descrizione);
        Assert.Equal(3m, Riga(cedolino, "INF_CARENZA").Quantita);
        Assert.Contains("azienda 60%", Riga(cedolino, "INF_CARENZA").Descrizione);
        Assert.Equal(2m, Riga(cedolino, "INF_INAIL").Quantita);
        Assert.Equal(90.00m, Riga(cedolino, "INF_INAIL").Importo);
        Assert.Equal(90.00m, cedolino.PrestazioniRecuperabili);
    }

    [Fact]
    public void QuantitaPerTariffa_RiconciliaConLImportoSenzaPrecisioneNascosta()
    {
        var cartellino = new Cartellino
        {
            Anno = 2026,
            Mese = 7,
            OreStraordinario = 3.25m,
            StatoAssegniFamiliari = StatoAssegniFamiliari.NonSpettanti,
            RiferimentoQuattordicesimaSeparata = "Q14-OFFICINA-NORD-2026-07",
            DataPagamentoQuattordicesima = new DateOnly(2026, 7, 1),
        };
        var cedolino = Calcola(cartellino: cartellino);

        foreach (var riga in cedolino.Righe.Where(r =>
                     r.Quantita is not null && r.Base is not null && r.Importo != 0m))
            Assert.Equal(
                decimal.Round(riga.Quantita!.Value * riga.Base!.Value, 2,
                    MidpointRounding.AwayFromZero),
                decimal.Abs(riga.Importo));
    }

    [Theory]
    [MemberData(nameof(PdfTests.TuttiGliStili), MemberType = typeof(PdfTests))]
    public void PdfOfficinaNord_RestaSimulazione_EsponeValoriRiconciliabili(StileCedolino stile)
    {
        var cedolino = Calcola();
        byte[] pdf = CedolinoPdf.Genera(cedolino, new IntestazioneCedolino
        {
            Azienda = "OFFICINA NORD SRL",
            AziendaIndirizzo = "Milano",
            PartitaIva = "01567890122",
            Dipendente = "BIANCHI GIULIA",
            CodiceFiscaleDipendente = "BNCGLI93B41F205J",
            DataAssunzione = "01/09/2022",
            Ccnl = "CCNL Terziario, Distribuzione e Servizi",
            CodiceCnel = "H011",
            Livello = "III",
            Qualifica = "Impiegata",
            Mensilita = 14,
            Orario = "40 ore/sett.",
        }, Luglio(), emissione: new ContestoEmissioneCedolino("Chiusa"), stile: stile);

        Assert.True(pdf.Length > 3_000);
        string testo = EstrattoreTestoPdf.EstraiTesto(pdf).ToUpperInvariant();
        Assert.Contains("PROSPETTO PAGA DI SIMULAZIONE – NON COSTITUISCE LUL", testo);
        Assert.Contains("BOZZA / NON CERTIFICATO", testo);
        Assert.Contains("2.015,15", testo);
        Assert.Contains("11,9949", testo);
        Assert.Contains("77,5058", testo);
        Assert.Contains("162,40", testo);
        Assert.Contains("16,33", testo);
        Assert.Contains("18,67", testo);
        Assert.Contains("QUATTORDICESIMA", testo);
        Assert.Contains("COSTO PARZIALE STIMATO DEL MESE", testo);
        Assert.Contains("COSTO MEDIO PARZIALE STIMATO ANNUO/12", testo);
        Assert.DoesNotContain("NETTO DEFINITIVO", testo);

        string? percorso = Environment.GetEnvironmentVariable("PAGHE_PDF_REVISIONE");
        if (stile == StileCedolino.Classico && !string.IsNullOrWhiteSpace(percorso))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
            File.WriteAllBytes(percorso, pdf);
        }
    }

    public void Dispose() => _connection.Dispose();
}
