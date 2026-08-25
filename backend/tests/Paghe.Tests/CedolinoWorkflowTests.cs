using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Cedolini;
using Paghe.Persistence.Entities;
using UglyToad.PdfPig;
using Xunit;

namespace Paghe.Tests;

public sealed class CedolinoWorkflowTests : IDisposable
{
    private readonly SqliteConnection _connessione = new("DataSource=:memory:");

    public CedolinoWorkflowTests()
    {
        _connessione.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
        new ParametriNormativiService(db).ImportaSeedSeVuotoAsync().GetAwaiter().GetResult();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connessione).Options);

    [Fact]
    public async Task FlussoCompleto_CongelaPdf_CifraPayload_EBloccaRicalcolo()
    {
        var scenario = await CreaScenarioAsync(conFirma: true, mese: 6);
        var attore = Guid.NewGuid();
        decimal costoMedioAtteso = 0m;

        await using (var db = NuovoContesto())
        {
            var calcolo = new CedoliniService(db, new ParametriNormativiService(db));
            var c = await calcolo.CalcolaEArchiviaAsync(
                scenario.TenantId, scenario.RapportoId, CartellinoCompleto(2026, 6));
            scenario = scenario with { CedolinoId = c.Id };
            Assert.Equal(nameof(StatoCedolino.Bozza), c.Stato);
            Assert.Equal(1, c.Versione);
            Assert.Equal("H011", c.CodiceCnel);
            Assert.True(c.CostoMedioAnnualizzato > 0m);
            costoMedioAtteso = c.CostoMedioAnnualizzato!.Value;
            Assert.Equal(expected: c.NettoInBusta, actual: (await calcolo.LeggiPerPdfAsync(
                scenario.TenantId, c.Id))!.Cedolino.NettoInBusta);
        }

        await using (var db = NuovoContesto())
        {
            var workflow = Workflow(db);
            var controllata = await workflow.ControllaAsync(
                scenario.TenantId, scenario.CedolinoId, attore, 1);
            Assert.Equal(StatoCedolino.Controllata, controllata.Stato);
            Assert.Equal(2, controllata.Versione);

            var validata = await workflow.ValidaAsync(
                scenario.TenantId, scenario.CedolinoId, attore, 2,
                new DatiValidazioneCedolino(
                    "Dott.ssa Maria Verdi", "Consulente del lavoro", "Verbale controllo PAG-2026-06"));
            Assert.Equal(StatoCedolino.Validata, validata.Stato);
            Assert.Equal(3, validata.Versione);

            var chiusa = await workflow.ChiudiAsync(
                scenario.TenantId, scenario.CedolinoId, attore, 3);
            Assert.Equal(StatoCedolino.Chiusa, chiusa.Stato);
            Assert.Equal(4, chiusa.Versione);
            Assert.False(string.IsNullOrWhiteSpace(chiusa.HashPdf));
        }

        byte[] prima;
        await using (var db = NuovoContesto())
        {
            var documento = await Workflow(db).LeggiPdfAsync(scenario.TenantId, scenario.CedolinoId);
            Assert.NotNull(documento);
            Assert.False(documento!.Definitivo);
            Assert.Equal(StatoCedolino.Chiusa, documento.Stato);
            Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(documento.Dati, 0, 4));
            using (var pdf = PdfDocument.Open(documento.Dati))
            {
                var testo = string.Join('\n', pdf.GetPages().Select(p => p.Text));
                Assert.Contains("Validazione registrata: Dott.ssa Maria Verdi", testo);
                Assert.Contains("consegna", testo, StringComparison.OrdinalIgnoreCase);
                Assert.Contains(
                    "COSTO MEDIO PARZIALE STIMATO ANNUO/12",
                    testo,
                    StringComparison.OrdinalIgnoreCase);
                Assert.Contains(
                    costoMedioAtteso.ToString("N2", CultureInfo.GetCultureInfo("it-IT")),
                    testo);
            }
            prima = documento.Dati;
        }

        // Tutto ciò che normalmente cambierebbe una rigenerazione viene modificato dopo la chiusura.
        await using (var db = NuovoContesto())
        {
            var azienda = await db.Aziende.SingleAsync(a => a.Id == scenario.AziendaId);
            azienda.RagioneSociale = "Nome cambiato dopo la chiusura";
            azienda.FirmaSiglaTimbro = PngBlu;
            var dipendente = await db.Dipendenti.SingleAsync(d => d.Id == scenario.DipendenteId);
            dipendente.Cognome = "Cognome cambiato";
            var versione = await db.CcnlVersioni.SingleAsync(v =>
                v.CcnlCodice == "COMMERCIO_TERZIARIO" && v.Anno == 2026);
            versione.CodiceCnel = "ZZZZ";
            var parametro = await db.ParametriAnno.SingleAsync(p => p.Anno == 2026);
            parametro.DivisoreOrario = 999m;
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            var seconda = await Workflow(db).LeggiPdfAsync(scenario.TenantId, scenario.CedolinoId);
            Assert.NotNull(seconda);
            Assert.Equal(prima, seconda!.Dati); // ristampa davvero byte per byte identica

            var errore = await Assert.ThrowsAsync<ArgumentException>(() =>
                new CedoliniService(db, new ParametriNormativiService(db)).CalcolaEArchiviaAsync(
                    scenario.TenantId, scenario.RapportoId,
                    new Cartellino { Anno = 2026, Mese = 6 }, forzaRicalcolo: true));
            Assert.Contains("non può essere sostituito", errore.Message);
        }

        await using (var db = NuovoContesto())
        {
            Assert.Null(await Workflow(db).LeggiPdfAsync(Guid.NewGuid(), scenario.CedolinoId));
            Assert.Null(await Workflow(db).LeggiStatoAsync(Guid.NewGuid(), scenario.CedolinoId));
        }

        // Il database grezzo contiene soltanto envelope cifrati, mai JSON, PDF o firma in chiaro.
        await using (var comando = _connessione.CreateCommand())
        {
            comando.CommandText = "SELECT ParametriApplicatiProtetti FROM Cedolini LIMIT 1";
            Assert.StartsWith("enc:v2:", (string)(await comando.ExecuteScalarAsync())!);
            comando.CommandText = "SELECT DatiProtetti FROM CedoliniSnapshot LIMIT 1";
            Assert.StartsWith("enc:v2:", (string)(await comando.ExecuteScalarAsync())!);
            comando.CommandText = "SELECT PdfProtetto FROM CedoliniDocumenti LIMIT 1";
            var pdfProtetto = (string)(await comando.ExecuteScalarAsync())!;
            Assert.StartsWith("enc:v2:", pdfProtetto);
            Assert.DoesNotContain("%PDF", pdfProtetto);
            comando.CommandText = "SELECT FirmaSiglaTimbroProtetta FROM Aziende LIMIT 1";
            Assert.StartsWith("enc:v2:", (string)(await comando.ExecuteScalarAsync())!);
        }
    }

    [Fact]
    public async Task Transizioni_NonConsentonoSaltiVersioniVecchieONessunTimbro()
    {
        var scenario = await CreaScenarioAsync(conFirma: false, mese: 7);
        var attore = Guid.NewGuid();
        await using var db = NuovoContesto();
        var calcolo = new CedoliniService(db, new ParametriNormativiService(db));
        var c = await calcolo.CalcolaEArchiviaAsync(
            scenario.TenantId, scenario.RapportoId, CartellinoCompleto(2026, 7));
        var workflow = Workflow(db);

        await Assert.ThrowsAsync<ConflittoCedolinoException>(() => workflow.ValidaAsync(
            scenario.TenantId, c.Id, attore, 1,
            new DatiValidazioneCedolino("Mario Rossi", "Consulente", "RIF-1")));

        await workflow.ControllaAsync(scenario.TenantId, c.Id, attore, 1);
        await Assert.ThrowsAsync<ConflittoCedolinoException>(() =>
            workflow.ControllaAsync(scenario.TenantId, c.Id, attore, 1));
        await workflow.ValidaAsync(
            scenario.TenantId, c.Id, attore, 2,
            new DatiValidazioneCedolino("Mario Rossi", "Consulente", "RIF-1"));

        var errore = await Assert.ThrowsAsync<ArgumentException>(() =>
            workflow.ChiudiAsync(scenario.TenantId, c.Id, attore, 3));
        Assert.Contains("firma, sigla o timbro autentico", errore.Message);
        var stato = await workflow.LeggiStatoAsync(scenario.TenantId, c.Id);
        Assert.Equal(StatoCedolino.Validata, stato!.Stato);
        Assert.Equal(3, stato.Versione);
    }

    [Fact]
    public async Task CodiceCnelMancante_BloccaIlControlloSenzaCreareSnapshot()
    {
        var scenario = await CreaScenarioAsync(conFirma: true, mese: 8);
        await using var db = NuovoContesto();
        var versione = await db.CcnlVersioni.SingleAsync(v =>
            v.CcnlCodice == "COMMERCIO_TERZIARIO" && v.Anno == 2026);
        versione.CodiceCnel = null;
        await db.SaveChangesAsync();

        var calcolo = new CedoliniService(db, new ParametriNormativiService(db));
        var c = await calcolo.CalcolaEArchiviaAsync(
            scenario.TenantId, scenario.RapportoId, CartellinoCompleto(2026, 8));
        var errore = await Assert.ThrowsAsync<ArgumentException>(() =>
            Workflow(db).ControllaAsync(scenario.TenantId, c.Id, Guid.NewGuid(), 1));
        Assert.Contains("codice CNEL", errore.Message);
        Assert.Empty(await db.CedoliniSnapshot.ToListAsync());
        Assert.Equal(nameof(StatoCedolino.Bozza), c.Stato);
    }

    [Fact]
    public async Task CalendarioMancante_BloccaIlControlloSenzaCreareSnapshot()
    {
        var scenario = await CreaScenarioAsync(conFirma: true, mese: 5);
        await using var db = NuovoContesto();
        var calcolo = new CedoliniService(db, new ParametriNormativiService(db));
        var c = await calcolo.CalcolaEArchiviaAsync(
            scenario.TenantId, scenario.RapportoId, new Cartellino { Anno = 2026, Mese = 5 });

        var errore = await Assert.ThrowsAsync<ArgumentException>(() =>
            Workflow(db).ControllaAsync(scenario.TenantId, c.Id, Guid.NewGuid(), 1));

        Assert.Contains("calendario giornaliero completo", errore.Message);
        Assert.Empty(await db.CedoliniSnapshot.ToListAsync());
        Assert.Equal(nameof(StatoCedolino.Bozza), c.Stato);
        Assert.Equal(1, c.Versione);
    }

    [Fact]
    public async Task PreWorkflow_NonTransita_NonSiModificaEliminaOSostituisce()
    {
        var scenario = await CreaCedolinoPreWorkflowAsync(3);
        var attore = Guid.NewGuid();

        await using (var db = NuovoContesto())
        {
            var workflow = Workflow(db);
            var controllo = await Assert.ThrowsAsync<ConflittoCedolinoException>(() =>
                workflow.ControllaAsync(
                    scenario.TenantId, scenario.CedolinoId, attore, 1));
            Assert.Contains("pre-workflow", controllo.Message, StringComparison.OrdinalIgnoreCase);

            var validazione = await Assert.ThrowsAsync<ConflittoCedolinoException>(() =>
                workflow.ValidaAsync(
                    scenario.TenantId,
                    scenario.CedolinoId,
                    attore,
                    1,
                    new DatiValidazioneCedolino("Mario Rossi", "Consulente", "RIF-STORICO")));
            Assert.Contains("pre-workflow", validazione.Message, StringComparison.OrdinalIgnoreCase);

            var chiusura = await Assert.ThrowsAsync<ConflittoCedolinoException>(() =>
                workflow.ChiudiAsync(
                    scenario.TenantId, scenario.CedolinoId, attore, 1));
            Assert.Contains("pre-workflow", chiusura.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(await db.CedoliniSnapshot.Where(s =>
                s.TenantId == scenario.TenantId && s.CedolinoId == scenario.CedolinoId).ToListAsync());
        }

        await using (var db = NuovoContesto())
        {
            var storico = await db.Cedolini.SingleAsync(c =>
                c.TenantId == scenario.TenantId && c.Id == scenario.CedolinoId);
            storico.NettoInBusta += 1m;
            var modifica = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            Assert.Contains("pre-workflow", modifica.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using (var db = NuovoContesto())
        {
            var storico = await db.Cedolini.SingleAsync(c =>
                c.TenantId == scenario.TenantId && c.Id == scenario.CedolinoId);
            db.Cedolini.Remove(storico);
            var eliminazione = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            Assert.Contains("pre-workflow", eliminazione.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using (var db = NuovoContesto())
        {
            var calcolo = new CedoliniService(db, new ParametriNormativiService(db));
            var sostituzione = await Assert.ThrowsAsync<ArgumentException>(() =>
                calcolo.CalcolaEArchiviaAsync(
                    scenario.TenantId,
                    scenario.RapportoId,
                    CartellinoCompleto(2026, 3),
                    forzaRicalcolo: true));
            Assert.Contains("non può essere sostituito", sostituzione.Message);

            var persistito = await db.Cedolini.AsNoTracking().SingleAsync(c =>
                c.TenantId == scenario.TenantId && c.Id == scenario.CedolinoId);
            Assert.Equal(nameof(StatoCedolino.PreWorkflow), persistito.Stato);
            Assert.Empty(await db.CedoliniSnapshot.Where(s =>
                s.TenantId == scenario.TenantId && s.CedolinoId == scenario.CedolinoId).ToListAsync());
            Assert.Empty(await db.CedoliniDocumenti.Where(d =>
                d.TenantId == scenario.TenantId && d.CedolinoId == scenario.CedolinoId).ToListAsync());
        }
    }

    [Fact]
    public async Task PreWorkflow_PdfStoricoNonDefinitivo_SenzaSnapshot_ETenantIsolato()
    {
        var scenario = await CreaCedolinoPreWorkflowAsync(4);
        await using var db = NuovoContesto();
        var workflow = Workflow(db);

        var documento = await workflow.LeggiPdfAsync(
            scenario.TenantId, scenario.CedolinoId);

        Assert.NotNull(documento);
        Assert.Equal(StatoCedolino.PreWorkflow, documento!.Stato);
        Assert.False(documento.Definitivo);
        Assert.Contains("preworkflow", documento.NomeFile, StringComparison.OrdinalIgnoreCase);
        using (var pdf = PdfDocument.Open(documento.Dati))
        {
            var testo = string.Join('\n', pdf.GetPages().Select(p => p.Text));
            Assert.Contains("ARCHIVIO PRE-WORKFLOW", testo, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("NON DEFINITIVA", testo, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CEDOLINO CHIUSO", testo, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("VALIDAZIONE REGISTRATA", testo, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Empty(await db.CedoliniSnapshot.Where(s =>
            s.TenantId == scenario.TenantId && s.CedolinoId == scenario.CedolinoId).ToListAsync());
        Assert.Empty(await db.CedoliniDocumenti.Where(d =>
            d.TenantId == scenario.TenantId && d.CedolinoId == scenario.CedolinoId).ToListAsync());

        var altroTenant = Guid.NewGuid();
        Assert.Null(await workflow.LeggiStatoAsync(altroTenant, scenario.CedolinoId));
        Assert.Null(await workflow.LeggiPdfAsync(altroTenant, scenario.CedolinoId));
        await Assert.ThrowsAsync<CedolinoNonTrovatoException>(() =>
            workflow.ControllaAsync(altroTenant, scenario.CedolinoId, Guid.NewGuid(), 1));
    }

    private async Task<Scenario> CreaCedolinoPreWorkflowAsync(int mese)
    {
        var scenario = await CreaScenarioAsync(conFirma: false, mese: mese);
        await using var db = NuovoContesto();
        var calcolo = new CedoliniService(db, new ParametriNormativiService(db));
        var cedolino = await calcolo.CalcolaEArchiviaAsync(
            scenario.TenantId, scenario.RapportoId, CartellinoCompleto(2026, mese));

        // Simula il backfill di una futura migrazione senza crearla in questo incarico. I record
        // storici possono non avere la fotografia dei parametri introdotta col nuovo workflow.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE Cedolini
            SET Stato = {nameof(StatoCedolino.PreWorkflow)},
                ParametriApplicatiProtetti = '',
                HashParametriSha256 = ''
            WHERE Id = {cedolino.Id} AND TenantId = {scenario.TenantId}
            """);
        return scenario with { CedolinoId = cedolino.Id };
    }

    private CedolinoWorkflowService Workflow(PagheDbContext db)
    {
        var cedolini = new CedoliniService(db, new ParametriNormativiService(db));
        return new CedolinoWorkflowService(db, cedolini);
    }

    private static Cartellino CartellinoCompleto(int anno, int mese)
    {
        var giornate = new List<GiornataCartellino>();
        decimal progressivo = 0m;
        for (int giorno = 1; giorno <= DateTime.DaysInMonth(anno, mese); giorno++)
        {
            var data = new DateOnly(anno, mese, giorno);
            bool riposo = data.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            if (data.DayOfWeek == DayOfWeek.Monday)
                progressivo = 0m;
            decimal ore = riposo ? 0m : 8m;
            progressivo += ore;
            giornate.Add(new GiornataCartellino
            {
                Data = data,
                Causale = riposo ? CausaleGiornata.RiposoSettimanale : CausaleGiornata.Lavorata,
                OreOrdinarie = ore,
                ProgressivoOreSettimanali = progressivo,
            });
        }

        return new Cartellino
        {
            Anno = anno,
            Mese = mese,
            StatoAssegniFamiliari = StatoAssegniFamiliari.NonSpettanti,
            RiferimentoQuattordicesimaSeparata = mese == 7 ? "Q14-WORKFLOW-2026-07" : null,
            DataPagamentoQuattordicesima = mese == 7 ? new DateOnly(anno, 7, 1) : null,
            Giornate = giornate,
        };
    }

    private async Task<Scenario> CreaScenarioAsync(bool conFirma, int mese)
    {
        var tenant = Guid.NewGuid();
        await using var db = NuovoContesto();
        var azienda = await new AziendeService(db).CreaAsync(tenant, new AziendaEntity
        {
            RagioneSociale = "Alfa Srl",
            PartitaIva = "01234560017",
            CodiceFiscale = "01234560017",
            Indirizzo = "Via Roma 1",
            Cap = "20100",
            Comune = "Milano",
            Provincia = "MI",
            MatricolaInps = "1234567890",
            CodiceSedeInps = "7100",
            CscInps = "70708",
            CodiciAutorizzazioneInps = "NESSUNO",
            CodiceFondoIntegrazioneSalariale = "FIS",
            CodiceAgevolazioneContributiva = "NESSUNA",
            AliquotaInpsDipendente = 0.0949m,
            AliquotaInpsDatore = 0.2788m,
            FonteProfiloContributivo = "Fixture workflow: profilo contributivo verificato",
            PatInail = "12345678",
            CodiceAteco = "45.20.10",
            RiferimentoVisuraCamerale = "VISURA-WORKFLOW-2026",
            StatoAlboArtigiani = "NonIscritta",
            RiferimentoVerificaApplicabilitaCcnl = "VERIFICA-H011-WORKFLOW",
            RiferimentoTerzoElementoTerritoriale = "FONTE-TERRITORIALE-MI-WORKFLOW",
            FirmaSiglaTimbro = conFirma ? PngRosso : null,
        });
        var dipendente = new DipendenteEntity
        {
            CodiceFiscale = "RSSMRA80A01F205X",
            Nome = "Mario",
            Cognome = "Rossi",
            Iban = "IT60X0542811101000000123456",
        };
        var rapporto = await new AnagraficheLavoroService(db).CreaDipendenteERapportoAsync(
            tenant, azienda.Id, dipendente, new RapportoLavoroEntity
            {
                CodiceCcnl = "COMMERCIO_TERZIARIO",
                Livello = "3",
                Qualifica = "Impiegato",
                NumeroMensilita = 14,
                Regione = "Lombardia",
                CodiceCatastaleComune = "F205",
                DataDecorrenzaDomicilioFiscale = new DateOnly(2026, 1, 1),
                AddizionaleRegionaleConguaglioAnnuo = 0m,
                AddizionaleComunaleSaldoAnnuo = 0m,
                AddizionaleComunaleAccontoAnnuo = 0m,
                RiferimentoProgressiviFiscaliPregressi = "IMPORT-WORKFLOW-2026",
                DataAssunzione = new DateOnly(2026, mese, 1),
                TipoContratto = "Tempo indeterminato",
            });
        return new Scenario(tenant, azienda.Id, dipendente.Id, rapporto.Id, Guid.Empty);
    }

    // PNG 1×1 validi e distinti: rappresentano solo fixture, non marchi reali.
    private static byte[] PngRosso => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9ZQmcAAAAASUVORK5CYII=");
    private static byte[] PngBlu => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private sealed record Scenario(
        Guid TenantId, Guid AziendaId, Guid DipendenteId, Guid RapportoId, Guid CedolinoId);

    public void Dispose() => _connessione.Dispose();
}
