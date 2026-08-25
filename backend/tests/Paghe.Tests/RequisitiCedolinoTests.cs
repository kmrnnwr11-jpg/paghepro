using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Cedolini;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Prova end-to-end del gate che separa una simulazione da un'elaborazione archiviabile.
/// Il caso positivo usa soltanto dati esplicitamente documentati; quelli negativi verificano
/// che il servizio fallisca chiuso e che l'isolamento per tenant non sia aggirabile.
/// </summary>
public sealed class RequisitiCedolinoTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public RequisitiCedolinoTests()
    {
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
        var parametri = new ParametriNormativiService(db);
        parametri.ImportaSeedSeVuotoAsync().GetAwaiter().GetResult();
        new CcnlImportService(db).ImportaAsync(CcnlDatasetUfficiale.Carica()).GetAwaiter().GetResult();
        RegoleCcnlDataset.ImportaAsync(db, RegoleCcnlDataset.Carica()).GetAwaiter().GetResult();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    private static IReadOnlyList<GiornataCartellino> CalendarioOrdinario(
        int anno, int mese, decimal riportoPrimaSettimana = 0m)
    {
        var giornate = new List<GiornataCartellino>();
        (int Anno, int Settimana)? settimanaCorrente = null;
        decimal progressivo = 0m;

        for (int giorno = 1; giorno <= DateTime.DaysInMonth(anno, mese); giorno++)
        {
            var data = new DateOnly(anno, mese, giorno);
            var dataTempo = data.ToDateTime(TimeOnly.MinValue);
            var settimana = (
                System.Globalization.ISOWeek.GetYear(dataTempo),
                System.Globalization.ISOWeek.GetWeekOfYear(dataTempo));
            if (settimanaCorrente != settimana)
            {
                progressivo = giornate.Count == 0 ? riportoPrimaSettimana : 0m;
                settimanaCorrente = settimana;
            }

            bool lavorata = data.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
            if (lavorata) progressivo += 8m;
            giornate.Add(new GiornataCartellino
            {
                Data = data,
                Causale = lavorata ? CausaleGiornata.Lavorata : CausaleGiornata.RiposoSettimanale,
                OreOrdinarie = lavorata ? 8m : 0m,
                ProgressivoOreSettimanali = progressivo,
            });
        }

        return giornate;
    }

    private static Cartellino LuglioCompleto(IReadOnlyList<GiornataCartellino>? giornate = null) => new()
    {
        Anno = 2026,
        Mese = 7,
        StatoAssegniFamiliari = StatoAssegniFamiliari.NonSpettanti,
        RiferimentoQuattordicesimaSeparata = "Q14-OFFICINA-NORD-2026-07",
        DataPagamentoQuattordicesima = new DateOnly(2026, 7, 1),
        Giornate = giornate ?? CalendarioOrdinario(2026, 7, riportoPrimaSettimana: 16m),
    };

    private async Task<Scenario> CreaScenarioValidoAsync()
    {
        var scenario = new Scenario(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await using var db = NuovoContesto();
        db.Aziende.Add(new AziendaEntity
        {
            Id = scenario.AziendaId,
            TenantId = scenario.TenantId,
            RagioneSociale = "Officina Nord Srl",
            PartitaIva = "01567890122",
            CodiceFiscale = "01567890122",
            Indirizzo = "Via Nord 1",
            Cap = "20100",
            Comune = "Milano",
            Provincia = "MI",
            CodiceAteco = "45.20.10",
            MatricolaInps = "1234567890",
            CodiceSedeInps = "4900",
            CscInps = "70708",
            CodiciAutorizzazioneInps = "NESSUNO",
            CodiceFondoIntegrazioneSalariale = "FIS",
            CodiceAgevolazioneContributiva = "NESSUNA",
            AliquotaInpsDipendente = 0.0949m,
            AliquotaInpsDatore = 0.2788m,
            FonteProfiloContributivo = "Cassetto previdenziale 20/08/2026 - posizione 1234567890",
            RiferimentoVisuraCamerale = "VISURA-CCIAA-MI-2026-001",
            StatoAlboArtigiani = "NonIscritta",
            RiferimentoVerificaApplicabilitaCcnl = "VERIFICA-CAMPO-H011-2026-001",
            RiferimentoTerzoElementoTerritoriale = "TABELLA-TERRITORIALE-MI-2026-001",
        });
        db.Dipendenti.Add(new DipendenteEntity
        {
            Id = scenario.DipendenteId,
            TenantId = scenario.TenantId,
            CodiceFiscale = "BNCGLI93B41F205J",
            Nome = "Giulia",
            Cognome = "Bianchi",
            Sesso = "F",
        });
        db.Rapporti.Add(new RapportoLavoroEntity
        {
            Id = scenario.RapportoId,
            TenantId = scenario.TenantId,
            AziendaId = scenario.AziendaId,
            DipendenteId = scenario.DipendenteId,
            CodiceCcnl = "COMMERCIO_TERZIARIO",
            Livello = "3",
            Qualifica = "Impiegato",
            OreSettimanali = 40m,
            NumeroMensilita = 14,
            DataAssunzione = new DateOnly(2022, 9, 1),
            ProvinciaLavoro = "MI",
            Regione = "Lombardia",
            CodiceCatastaleComune = "F205",
            DataDecorrenzaDomicilioFiscale = new DateOnly(2022, 9, 1),
            RiferimentoProgressiviFiscaliPregressi = "PROGRESSIVI-CERTIFICATI-30-06-2026",
            AddizionaleRegionaleConguaglioAnnuo = 0m,
            AddizionaleComunaleSaldoAnnuo = 0m,
            AddizionaleComunaleAccontoAnnuo = 0m,
            RolResiduiIniziali = 0m,
            ExFestivitaResidueIniziali = 0m,
        });
        db.Cartellini.Add(new CartellinoEntity
        {
            Id = Guid.NewGuid(),
            TenantId = scenario.TenantId,
            RapportoLavoroId = scenario.RapportoId,
            Anno = 2026,
            Mese = 6,
            StatoAssegniFamiliari = nameof(StatoAssegniFamiliari.NonSpettanti),
            Giornate = CalendarioOrdinario(2026, 6),
        });
        await db.SaveChangesAsync();
        return scenario;
    }

    [Fact]
    public async Task DatiCompletiDocumentati_SuperanoIlGate()
    {
        var scenario = await CreaScenarioValidoAsync();
        await using var db = NuovoContesto();

        var blocchi = await new RequisitiCedolinoService(db).VerificaAsync(
            scenario.TenantId, scenario.RapportoId, LuglioCompleto());

        Assert.Empty(blocchi);
    }

    [Fact]
    public async Task UnAltroTenant_NonPuoVerificareIlRapporto()
    {
        var scenario = await CreaScenarioValidoAsync();
        await using var db = NuovoContesto();

        var blocchi = await new RequisitiCedolinoService(db).VerificaAsync(
            Guid.NewGuid(), scenario.RapportoId, LuglioCompleto());

        Assert.Collection(blocchi,
            blocco => Assert.Equal("RAPPORTO_NON_TROVATO", blocco.Codice));
    }

    [Fact]
    public async Task IdentificativiErrati_DomicilioProfiloEProveMancanti_Bloccano()
    {
        var scenario = await CreaScenarioValidoAsync();
        await using var db = NuovoContesto();
        var azienda = await db.Aziende.SingleAsync(a =>
            a.TenantId == scenario.TenantId && a.Id == scenario.AziendaId);
        var dipendente = await db.Dipendenti.SingleAsync(d =>
            d.TenantId == scenario.TenantId && d.Id == scenario.DipendenteId);
        var rapporto = await db.Rapporti.SingleAsync(r =>
            r.TenantId == scenario.TenantId && r.Id == scenario.RapportoId);

        azienda.PartitaIva = "01567890123";
        azienda.CodiceFiscale = "01567890123";
        azienda.CscInps = null;
        azienda.CodiciAutorizzazioneInps = null;
        azienda.CodiceAteco = null;
        azienda.RiferimentoVisuraCamerale = null;
        azienda.StatoAlboArtigiani = "DaVerificare";
        azienda.RiferimentoVerificaApplicabilitaCcnl = null;
        azienda.RiferimentoTerzoElementoTerritoriale = null;
        dipendente.CodiceFiscale = "BNCGLI93B41F205X";
        rapporto.Regione = "";
        rapporto.CodiceCatastaleComune = "";
        rapporto.DataDecorrenzaDomicilioFiscale = null;
        rapporto.AddizionaleRegionaleConguaglioAnnuo = null;
        rapporto.AddizionaleComunaleSaldoAnnuo = null;
        rapporto.AddizionaleComunaleAccontoAnnuo = null;
        await db.SaveChangesAsync();

        var senzaQuattordicesima = new Cartellino
        {
            Anno = 2026,
            Mese = 7,
            StatoAssegniFamiliari = StatoAssegniFamiliari.NonSpettanti,
            Giornate = CalendarioOrdinario(2026, 7, 16m),
        };
        var codici = (await new RequisitiCedolinoService(db).VerificaAsync(
                scenario.TenantId, scenario.RapportoId, senzaQuattordicesima))
            .Select(b => b.Codice)
            .ToHashSet(StringComparer.Ordinal);

        string[] attesi =
        [
            "PIVA_CHECKSUM", "CF_AZIENDA_CHECKSUM", "CF_DIPENDENTE_CHECKSUM",
            "DOMICILIO_FISCALE", "DECORRENZA_DOMICILIO",
            "CONGUAGLIO_REGIONALE", "SALDO_COMUNALE", "ACCONTO_COMUNALE",
            "CSC_INPS", "CA_INPS", "H011_ATECO", "H011_VISURA",
            "H011_ALBO_ARTIGIANI", "H011_APPLICABILITA", "H011_TERZO_ELEMENTO_MI",
            "QUATTORDICESIMA_SEPARATA", "DATA_QUATTORDICESIMA",
        ];
        foreach (string atteso in attesi)
            Assert.Contains(atteso, codici);
    }

    [Fact]
    public async Task CalendarioAssenteOConRiportoDiscordante_Blocca()
    {
        var scenario = await CreaScenarioValidoAsync();
        await using var db = NuovoContesto();
        var servizio = new RequisitiCedolinoService(db);

        var senzaCalendario = await servizio.VerificaAsync(
            scenario.TenantId,
            scenario.RapportoId,
            LuglioCompleto([]));
        Assert.Contains(senzaCalendario, b =>
            b.Codice == "CALENDARIO" && b.Messaggio.Contains("calendario giornaliero completo"));

        var riportoDiscordante = await servizio.VerificaAsync(
            scenario.TenantId,
            scenario.RapportoId,
            LuglioCompleto(CalendarioOrdinario(2026, 7, riportoPrimaSettimana: 0m)));
        Assert.Contains(riportoDiscordante, b =>
            b.Codice == "CALENDARIO" && b.Messaggio.Contains("fonte autorevole"));
    }

    private sealed record Scenario(
        Guid TenantId, Guid AziendaId, Guid DipendenteId, Guid RapportoId);

    public void Dispose() => _connection.Dispose();
}
