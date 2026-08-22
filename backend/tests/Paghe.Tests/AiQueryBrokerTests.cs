using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Ai;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test del ProfessionalQueryBroker (G2): lo scope viene dal MANIFEST del mandato (mai dal
/// client), le proiezioni sono minimizzate (dell'IBAN esce solo il flag), le risorse senza
/// proiezione sono negate. In coda il fuzz del §13: 10.000 tentativi ostili, zero accessi.
/// </summary>
public sealed class AiQueryBrokerTests : IDisposable
{
    // I test del broker sono a thread singolo: SQLite in-memory con connessione condivisa
    // è molto più veloce del file (il fuzz da 10.000 dinieghi auditati resta nei tempi suite).
    private readonly SqliteConnection _connection;

    private readonly AiGovernorOptions _opzioni =
        new(AiLeaseCodec.DerivaChiave(Encoding.UTF8.GetBytes("chiave-di-prova-broker-32-byte!!!")));

    private readonly OrologioRegolabile _orologio = new(new DateTimeOffset(2026, 7, 16, 9, 0, 0, TimeSpan.Zero));

    public AiQueryBrokerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private PagheDbContext NuovoContesto() => new(
        new DbContextOptionsBuilder<PagheDbContext>()
            .UseSqlite(_connection)
            .Options);

    private (ProfessionalQueryBroker Broker, AiGrantService Governor) NuoviServizi(PagheDbContext db)
    {
        var governor = new AiGrantService(db, _opzioni, _orologio);
        var broker = new ProfessionalQueryBroker(db, governor, _orologio);
        return (broker, governor);
    }

    private static async Task<Guid> CreaAziendaConDipendenteAsync(
        PagheDbContext db, Guid tenant, string ragioneSociale, string cognome)
    {
        var azienda = new AziendaEntity { Id = Guid.NewGuid(), TenantId = tenant, RagioneSociale = ragioneSociale };
        var dipendente = new DipendenteEntity
        {
            Id = Guid.NewGuid(), TenantId = tenant,
            CodiceFiscale = $"XXXXXX00A01{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
            Nome = "Test", Cognome = cognome,
        };
        db.Aziende.Add(azienda);
        db.Dipendenti.Add(dipendente);
        db.Rapporti.Add(new RapportoLavoroEntity
        {
            Id = Guid.NewGuid(), TenantId = tenant, AziendaId = azienda.Id, DipendenteId = dipendente.Id,
            CodiceCcnl = "COMMERCIO", Livello = "IV",
            DataAssunzione = new DateOnly(2026, 1, 1),
        });
        await db.SaveChangesAsync();
        return azienda.Id;
    }

    private static RichiestaGrant MandatoSu(Guid azienda, params string[] risorse) => new(
        PurposeCode: "CONTROLLO_ANOMALIE_PAGHE",
        Modalita: "Guidata",
        ScopeLevel: "S2",
        AziendaId: azienda,
        Periodo: "2026-07",
        Risorse: risorse,
        Operazioni: ["read"]);

    [Fact]
    public async Task CartelliniFiltratiDallAziendaDelMandato_NonDalClient()
    {
        var tenant = Guid.NewGuid();
        Guid aziendaA, aziendaB;
        await using (var db = NuovoContesto())
        {
            using var _ = Paghe.Persistence.Gdpr.AmbientContestoAudit.Imposta(tenant, Guid.NewGuid());
            aziendaA = await CreaAziendaConDipendenteAsync(db, tenant, "Alfa Srl", "DiAlfa");
            aziendaB = await CreaAziendaConDipendenteAsync(db, tenant, "Beta Srl", "DiBeta");
            var rapportoA = await db.Rapporti.SingleAsync(r =>
                r.TenantId == tenant && r.AziendaId == aziendaA);
            var cartellinoId = Guid.NewGuid();
            db.Cartellini.Add(new CartellinoEntity
            {
                Id = cartellinoId,
                TenantId = tenant,
                RapportoLavoroId = rapportoA.Id,
                Anno = 2026,
                Mese = 7,
            });
            await db.SaveChangesAsync();
            // Payload volutamente illeggibile: la proiezione G2 deve leggere solo scalari e
            // non materializzare/decifrare mai CartellinoEntity (D3A).
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Cartellini SET DatiSaluteProtetti = '' WHERE Id = {cartellinoId}");
        }

        await using var contesto = NuovoContesto();
        var (broker, governor) = NuoviServizi(contesto);
        var utente = Guid.NewGuid();
        var (grant, errore) = await governor.EmettiAsync(tenant, utente, MandatoSu(aziendaA, "Cartellini"));
        Assert.Null(errore);

        var esito = await broker.LeggiAsync(tenant, utente, grant!.Lease, "Cartellini");
        Assert.True(esito.Consentito);
        var riga = Assert.Single(esito.Righe.Cast<RigaCartellino>());
        Assert.Contains("DiAlfa", riga.Nominativo); // solo l'azienda del mandato, mai la B
        Assert.True(riga.Elaborato);
        var json = JsonSerializer.Serialize(riga);
        Assert.DoesNotContain("Malattia", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Infortunio", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Evento", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RapportoLegacyCrossTenant_NonEsponeIlDipendenteInNessunaProiezionePaghe()
    {
        var tenantMandato = Guid.NewGuid();
        var tenantPersona = Guid.NewGuid();
        var aziendaId = Guid.NewGuid();
        var dipendenteId = Guid.NewGuid();
        await using (var db = NuovoContesto())
        {
            db.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId,
                TenantId = tenantMandato,
                RagioneSociale = "Azienda del mandato",
            });
            db.Dipendenti.Add(new DipendenteEntity
            {
                Id = dipendenteId,
                TenantId = tenantPersona,
                CodiceFiscale = "CROSSS80A01F205X",
                Nome = "NomeSegreto",
                Cognome = "PersonaAltroTenant",
            });
            await db.SaveChangesAsync();

            // Riproduce un archivio legacy corrotto prima della migrazione. Il modello corrente
            // lo vieta; disabilitiamo il controllo solo per provare la difesa in profondità del
            // broker davanti a dati già presenti, poi lo riattiviamo immediatamente.
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = 0;");
            try
            {
                db.Rapporti.Add(new RapportoLavoroEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantMandato,
                    AziendaId = aziendaId,
                    DipendenteId = dipendenteId,
                    CodiceCcnl = "COMMERCIO",
                    Livello = "IV",
                    DataAssunzione = new DateOnly(2026, 1, 1),
                });
                await db.SaveChangesAsync();
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = 1;");
            }
        }

        await using var contesto = NuovoContesto();
        var (broker, governor) = NuoviServizi(contesto);
        var utente = Guid.NewGuid();
        var (grant, errore) = await governor.EmettiAsync(
            tenantMandato,
            utente,
            MandatoSu(aziendaId, "Dipendenti", "Cartellini", "Cedolini"));
        Assert.Null(errore);

        foreach (var risorsa in new[] { "Dipendenti", "Cartellini", "Cedolini" })
        {
            var esito = await broker.LeggiAsync(
                tenantMandato, utente, grant!.Lease, risorsa);
            Assert.True(esito.Consentito);
            Assert.Empty(esito.Righe);
            Assert.Equal(0, esito.Totale);
            Assert.DoesNotContain(
                "PersonaAltroTenant",
                JsonSerializer.Serialize(esito),
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AziendaEsceSoloComeFlagDiPresenza_MaiValori()
    {
        var tenant = Guid.NewGuid();
        Guid aziendaId;
        await using (var db = NuovoContesto())
        {
            using var _ = Paghe.Persistence.Gdpr.AmbientContestoAudit.Imposta(tenant, Guid.NewGuid());
            db.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId = Guid.NewGuid(), TenantId = tenant, RagioneSociale = "Gamma Srl",
                MatricolaInps = "1234567890", Pec = "gamma@pec.it",
            });
            await db.SaveChangesAsync();
        }

        await using var contesto = NuovoContesto();
        var (broker, governor) = NuoviServizi(contesto);
        var utente = Guid.NewGuid();
        var (grant, _) = await governor.EmettiAsync(tenant, utente, MandatoSu(aziendaId, "Aziende"));

        var esito = await broker.LeggiAsync(tenant, utente, grant!.Lease, "Aziende");
        Assert.True(esito.Consentito);
        var riga = Assert.Single(esito.Righe.Cast<RigaAzienda>());
        Assert.True(riga.MatricolaInpsPresente);
        Assert.True(riga.RecapitoFiscalePresente);
        Assert.False(riga.IbanPresente); // e il tipo, per costruzione, non porta MAI il valore
        Assert.False(riga.PatInailPresente);
    }

    [Fact]
    public async Task RisorsaAmmessaMaSenzaProiezioneNegata()
    {
        var tenant = Guid.NewGuid();
        Guid aziendaId;
        await using (var db = NuovoContesto())
        {
            using var _ = Paghe.Persistence.Gdpr.AmbientContestoAudit.Imposta(tenant, Guid.NewGuid());
            db.Aziende.Add(new AziendaEntity { Id = aziendaId = Guid.NewGuid(), TenantId = tenant, RagioneSociale = "Delta Srl" });
            await db.SaveChangesAsync();
        }

        await using var contesto = NuovoContesto();
        var (broker, governor) = NuoviServizi(contesto);
        var utente = Guid.NewGuid();
        var (grant, _) = await governor.EmettiAsync(tenant, utente, MandatoSu(aziendaId, "Rapporti"));

        var esito = await broker.LeggiAsync(tenant, utente, grant!.Lease, "Rapporti");
        Assert.False(esito.Consentito);
        Assert.Contains("proiezione", esito.Motivo);
        Assert.Empty(esito.Righe);
    }

    [Fact]
    public async Task ControlloCedoliniRestituisceUnLottoDiDieciESenzaImportiOSalute()
    {
        var tenant = Guid.NewGuid();
        var aziendaId = Guid.NewGuid();
        var chiusaCedolinoId = Guid.NewGuid();
        var validataCedolinoId = Guid.NewGuid();
        var preWorkflowCedolinoId = Guid.NewGuid();
        var controllataCedolinoId = Guid.NewGuid();
        await using (var db = NuovoContesto())
        {
            using var _ = Paghe.Persistence.Gdpr.AmbientContestoAudit.Imposta(tenant, Guid.NewGuid());
            db.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId,
                TenantId = tenant,
                RagioneSociale = "Lotto Dieci Srl",
                MatricolaInps = "1234567890",
                CodiceSedeInps = "0001",
                PatInail = "PAT-TEST",
                CcnlDefault = "COMMERCIO",
            });
            Guid primoRapportoId = Guid.Empty;
            Guid secondoRapportoId = Guid.Empty;
            Guid terzoRapportoId = Guid.Empty;
            Guid quartoRapportoId = Guid.Empty;
            Guid quintoRapportoId = Guid.Empty;
            for (var i = 0; i < 12; i++)
            {
                var dipendenteId = Guid.NewGuid();
                var rapportoId = Guid.NewGuid();
                if (i == 0) primoRapportoId = rapportoId;
                if (i == 1) secondoRapportoId = rapportoId;
                if (i == 2) terzoRapportoId = rapportoId;
                if (i == 3) quartoRapportoId = rapportoId;
                if (i == 4) quintoRapportoId = rapportoId;
                db.Dipendenti.Add(new DipendenteEntity
                {
                    Id = dipendenteId,
                    TenantId = tenant,
                    CodiceFiscale = $"TEST{i:000000000000}",
                    Nome = $"Nome{i:00}",
                    Cognome = $"Cognome{i:00}",
                });
                db.Rapporti.Add(new RapportoLavoroEntity
                {
                    Id = rapportoId,
                    TenantId = tenant,
                    AziendaId = aziendaId,
                    DipendenteId = dipendenteId,
                    CodiceCcnl = "COMMERCIO",
                    Livello = "IV",
                    DataAssunzione = new DateOnly(2026, 1, 1),
                });
            }
            db.Cedolini.Add(new CedolinoEntity
            {
                Id = chiusaCedolinoId,
                TenantId = tenant,
                AziendaId = aziendaId,
                RapportoLavoroId = primoRapportoId,
                Anno = 2026,
                Mese = 7,
                Stato = nameof(StatoCedolino.Bozza),
                CalcolatoIl = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc),
            });
            db.Cedolini.Add(new CedolinoEntity
            {
                Id = validataCedolinoId,
                TenantId = tenant,
                AziendaId = aziendaId,
                RapportoLavoroId = secondoRapportoId,
                Anno = 2026,
                Mese = 7,
                Stato = nameof(StatoCedolino.Bozza),
                CalcolatoIl = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc),
            });
            db.Cedolini.Add(new CedolinoEntity
            {
                Id = preWorkflowCedolinoId,
                TenantId = tenant,
                AziendaId = aziendaId,
                RapportoLavoroId = terzoRapportoId,
                Anno = 2026,
                Mese = 7,
                Stato = nameof(StatoCedolino.Bozza),
                CalcolatoIl = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc),
            });
            db.Cedolini.Add(new CedolinoEntity
            {
                Id = controllataCedolinoId,
                TenantId = tenant,
                AziendaId = aziendaId,
                RapportoLavoroId = quartoRapportoId,
                Anno = 2026,
                Mese = 7,
                Stato = nameof(StatoCedolino.Bozza),
                CalcolatoIl = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc),
            });
            db.Cedolini.Add(new CedolinoEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenant,
                AziendaId = aziendaId,
                RapportoLavoroId = quintoRapportoId,
                Anno = 2026,
                Mese = 7,
                Stato = nameof(StatoCedolino.Bozza),
                CalcolatoIl = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc),
            });
            await db.SaveChangesAsync();

            // Il test isola la classificazione read-only del broker: gli stati persistiti
            // vengono preparati direttamente senza simulare qui l'intero workflow documentale.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Cedolini SET Stato = {nameof(StatoCedolino.Chiusa)} WHERE TenantId = {tenant} AND Id = {chiusaCedolinoId}");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Cedolini SET Stato = {nameof(StatoCedolino.Validata)} WHERE TenantId = {tenant} AND Id = {validataCedolinoId}");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Cedolini SET Stato = {nameof(StatoCedolino.PreWorkflow)} WHERE TenantId = {tenant} AND Id = {preWorkflowCedolinoId}");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Cedolini SET Stato = {nameof(StatoCedolino.Controllata)} WHERE TenantId = {tenant} AND Id = {controllataCedolinoId}");
        }

        await using var contesto = NuovoContesto();
        var (broker, governor) = NuoviServizi(contesto);
        var utente = Guid.NewGuid();
        var (grant, errore) = await governor.EmettiAsync(
            tenant, utente, MandatoSu(aziendaId, "Cedolini") with
            {
                PurposeCode = "PREPARAZIONE_MESE_PAGHE",
                MaxLetture = 10,
            });
        Assert.Null(errore);

        var esito = await broker.LeggiAsync(tenant, utente, grant!.Lease, "Cedolini");
        Assert.True(esito.Consentito);
        Assert.Equal(10, esito.Righe.Count);
        Assert.Equal(12, esito.Totale);
        Assert.Equal(0, esito.LettureRimanenti);
        var righe = esito.Righe.Cast<RigaCedolinoMese>().ToList();
        Assert.Equal(4, righe.Count(r => r.Stato == "protetto"));
        Assert.Equal(5, righe.Count(r => r.Stato == "da_compilare"));
        var controllato = Assert.Single(righe, r =>
            r.StatoCedolino == nameof(StatoCedolino.Controllata));
        Assert.Equal("protetto", controllato.Stato);
        Assert.Contains("controllato", controllato.Motivo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sola lettura", controllato.Motivo, StringComparison.OrdinalIgnoreCase);
        var bozza = Assert.Single(righe, r =>
            r.StatoCedolino == nameof(StatoCedolino.Bozza));
        Assert.Equal("gia_elaborato", bozza.Stato);
        Assert.Contains("bozza", bozza.Motivo, StringComparison.OrdinalIgnoreCase);
        var storico = Assert.Single(righe, r =>
            r.StatoCedolino == nameof(StatoCedolino.PreWorkflow));
        Assert.Equal("protetto", storico.Stato);
        Assert.Contains("storico", storico.Motivo, StringComparison.OrdinalIgnoreCase);

        var json = JsonSerializer.Serialize(esito.Righe);
        Assert.DoesNotContain("Malattia", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Infortunio", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Netto", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CodiceFiscale", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ControlloCedoliniSenzaRapporti_RestituisceZeroSenzaConsumareQuota()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();
        var aziendaId = Guid.NewGuid();
        await using (var db = NuovoContesto())
        {
            using var _ = Paghe.Persistence.Gdpr.AmbientContestoAudit.Imposta(tenant, utente);
            db.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId,
                TenantId = tenant,
                RagioneSociale = "Nessun Rapporto Srl",
            });
            await db.SaveChangesAsync();
        }

        await using var contesto = NuovoContesto();
        var (broker, governor) = NuoviServizi(contesto);
        var (grant, errore) = await governor.EmettiAsync(
            tenant, utente, MandatoSu(aziendaId, "Cedolini") with
            {
                PurposeCode = "PREPARAZIONE_MESE_PAGHE",
                MaxLetture = 10,
            });
        Assert.Null(errore);

        var esito = await broker.LeggiAsync(tenant, utente, grant!.Lease, "Cedolini");

        Assert.True(esito.Consentito);
        Assert.Empty(esito.Righe);
        Assert.Equal(0, esito.Totale);
        Assert.Equal(10, esito.LettureRimanenti);
    }

    [Fact]
    public async Task RevocaTraProiezioneEConsumo_NegaENonRestituisceRighe()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();
        var aziendaId = Guid.NewGuid();
        var stringaConnessione =
            $"Data Source=revoca-broker-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

        // L'ancora mantiene vivo il database in-memory, mentre broker e revoca usano
        // connessioni/DbContext distinti come accade tra due richieste concorrenti reali.
        await using var ancora = new SqliteConnection(stringaConnessione);
        await ancora.OpenAsync();
        var opzioniBase = new DbContextOptionsBuilder<PagheDbContext>()
            .UseSqlite(stringaConnessione)
            .Options;

        await using (var preparazione = new PagheDbContext(opzioniBase))
        {
            await preparazione.Database.EnsureCreatedAsync();
            preparazione.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId,
                TenantId = tenant,
                RagioneSociale = "Revoca Concorrente Srl",
            });
            await preparazione.SaveChangesAsync();
        }

        GrantEmesso grant;
        await using (var emissione = new PagheDbContext(opzioniBase))
        {
            var governor = new AiGrantService(emissione, _opzioni, _orologio);
            var (emesso, errore) = await governor.EmettiAsync(
                tenant, utente, MandatoSu(aziendaId, "Aziende"));
            Assert.Null(errore);
            grant = Assert.IsType<GrantEmesso>(emesso);
        }

        var revocaRiuscita = false;
        var intercettore = new RevocaPrimaDelConsumoInterceptor(async ct =>
        {
            await using var contestoRevoca = new PagheDbContext(opzioniBase);
            var governorRevoca = new AiGrantService(contestoRevoca, _opzioni, _orologio);
            revocaRiuscita = await governorRevoca.RevocaAsync(
                tenant, utente, grant.Manifest.GrantId, ct);
        });
        var opzioniBroker = new DbContextOptionsBuilder<PagheDbContext>()
            .UseSqlite(stringaConnessione)
            .AddInterceptors(intercettore)
            .Options;

        await using var contestoBroker = new PagheDbContext(opzioniBroker);
        var (broker, _) = NuoviServizi(contestoBroker);

        var esito = await broker.LeggiAsync(
            tenant, utente, grant.Lease, "Aziende");

        Assert.True(intercettore.Intercettato);
        Assert.True(revocaRiuscita);
        Assert.False(esito.Consentito);
        Assert.Contains("revocato", esito.Motivo, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(esito.Righe);
        Assert.Equal(0, esito.Totale);

        await using var verifica = new PagheDbContext(opzioniBase);
        var persistito = await verifica.AiScopeGrants.AsNoTracking()
            .SingleAsync(g => g.Id == grant.Manifest.GrantId && g.TenantId == tenant);
        Assert.Equal("Revocato", persistito.Stato);
        Assert.Equal(0, persistito.LettureUsate);
    }

    [Fact]
    public async Task RevocaDopoPrenotazioneEPrimaDellaProiezione_NegaAncheConGrantGiaTracciato()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();
        var aziendaId = Guid.NewGuid();
        var stringaConnessione =
            $"Data Source=revoca-finale-broker-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

        await using var ancora = new SqliteConnection(stringaConnessione);
        await ancora.OpenAsync();
        var opzioniBase = new DbContextOptionsBuilder<PagheDbContext>()
            .UseSqlite(stringaConnessione)
            .Options;
        await using (var preparazione = new PagheDbContext(opzioniBase))
        {
            await preparazione.Database.EnsureCreatedAsync();
            preparazione.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId,
                TenantId = tenant,
                RagioneSociale = "Revoca dopo prenotazione Srl",
            });
            await preparazione.SaveChangesAsync();
        }

        GrantEmesso grant;
        await using (var emissione = new PagheDbContext(opzioniBase))
        {
            var governor = new AiGrantService(emissione, _opzioni, _orologio);
            var (emesso, errore) = await governor.EmettiAsync(
                tenant, utente, MandatoSu(aziendaId, "Aziende"));
            Assert.Null(errore);
            grant = Assert.IsType<GrantEmesso>(emesso);
        }

        var pausa = new PausaPrimaDellaProiezioneAziendaInterceptor();
        var opzioniBroker = new DbContextOptionsBuilder<PagheDbContext>()
            .UseSqlite(stringaConnessione)
            .AddInterceptors(pausa)
            .Options;
        await using var contestoBroker = new PagheDbContext(opzioniBroker);
        var (broker, _) = NuoviServizi(contestoBroker);

        var lettura = broker.LeggiAsync(tenant, utente, grant.Lease, "Aziende");
        await pausa.AttendiAsync();
        await using (var contestoRevoca = new PagheDbContext(opzioniBase))
        {
            var governorRevoca = new AiGrantService(contestoRevoca, _opzioni, _orologio);
            Assert.True(await governorRevoca.RevocaAsync(
                tenant, utente, grant.Manifest.GrantId));
        }
        pausa.Riprendi();

        var esito = await lettura;
        Assert.False(esito.Consentito);
        Assert.Contains("revocato", esito.Motivo, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(esito.Righe);

        await using var verifica = new PagheDbContext(opzioniBase);
        var persistito = await verifica.AiScopeGrants.AsNoTracking()
            .SingleAsync(g => g.Id == grant.Manifest.GrantId && g.TenantId == tenant);
        Assert.Equal("Revocato", persistito.Stato);
        Assert.Equal(0, persistito.LettureUsate);
    }

    [Fact]
    public async Task RisorsaFuoriMandatoNegataDalGovernor()
    {
        var tenant = Guid.NewGuid();
        Guid aziendaId;
        await using (var db = NuovoContesto())
        {
            using var _ = Paghe.Persistence.Gdpr.AmbientContestoAudit.Imposta(tenant, Guid.NewGuid());
            db.Aziende.Add(new AziendaEntity { Id = aziendaId = Guid.NewGuid(), TenantId = tenant, RagioneSociale = "Epsilon Srl" });
            await db.SaveChangesAsync();
        }

        await using var contesto = NuovoContesto();
        var (broker, governor) = NuoviServizi(contesto);
        var utente = Guid.NewGuid();
        var (grant, _) = await governor.EmettiAsync(tenant, utente, MandatoSu(aziendaId, "Aziende"));

        var esito = await broker.LeggiAsync(tenant, utente, grant!.Lease, "Dipendenti");
        Assert.False(esito.Consentito);
        Assert.Contains("fuori dal mandato", esito.Motivo);
    }

    // ---------- Fuzz del §13: 10.000 tentativi ostili, zero accessi ----------

    [Fact]
    public async Task Fuzz10000TentativiOstili_ZeroAccessiConsentiti()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();
        await using var db = NuovoContesto();
        var (broker, governor) = NuoviServizi(db);

        // Un mandato legittimo (per rubarne la lease) e uno revocato.
        Guid aziendaId;
        using (var _ = Paghe.Persistence.Gdpr.AmbientContestoAudit.Imposta(tenant, utente))
        {
            db.Aziende.Add(new AziendaEntity { Id = aziendaId = Guid.NewGuid(), TenantId = tenant, RagioneSociale = "Zeta Srl" });
            await db.SaveChangesAsync();
        }
        var (valido, _) = await governor.EmettiAsync(tenant, utente, MandatoSu(aziendaId, "Aziende"));
        var (revocato, _) = await governor.EmettiAsync(tenant, utente, MandatoSu(aziendaId, "Aziende"));
        await governor.RevocaAsync(tenant, utente, revocato!.Manifest.GrantId);

        // Una chiave DIVERSA da quella del server: l'attaccante non può firmare lease valide.
        var chiaveAttaccante = AiLeaseCodec.DerivaChiave(Encoding.UTF8.GetBytes("chiave-che-l-attaccante-si-inventa"));

        var casuale = new Random(20260716);
        var risorse = new[] { "Aziende", "Cedolini", "Utenti", "AuditLog", "Cartellini.Malattia", "TabellaInventata" };
        var consentiti = 0;

        for (var i = 0; i < 10_000; i++)
        {
            var risorsa = risorse[casuale.Next(risorse.Length)];
            var esito = (i % 5) switch
            {
                // 1) lease del tenant giusto usata da un ALTRO tenant
                0 => await governor.AutorizzaAccessoAsync(Guid.NewGuid(), utente, valido!.Lease, risorsa, "read"),
                // 2) lease con un byte alterato (firma rotta)
                1 => await governor.AutorizzaAccessoAsync(tenant, utente, Manometti(valido!.Lease, casuale), risorsa, "read"),
                // 3) lease firmata con la chiave dell'attaccante su un grant vero
                2 => await governor.AutorizzaAccessoAsync(tenant, utente,
                    AiLeaseCodec.Crea(valido!.Manifest.GrantId, Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
                        _orologio.GetUtcNow().UtcDateTime, _orologio.GetUtcNow().UtcDateTime.AddHours(1), chiaveAttaccante),
                    risorsa, "read"),
                // 4) lease legittima di un mandato REVOCATO
                3 => await governor.AutorizzaAccessoAsync(tenant, utente, revocato!.Lease, risorsa, "read"),
                // 5) spazzatura casuale al posto della lease
                _ => await governor.AutorizzaAccessoAsync(tenant, utente,
                    Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)), risorsa, "read"),
            };
            if (esito.Consentito) consentiti++;
        }

        Assert.Equal(0, consentiti);

        // Anche via broker: la lease valida usata sul tenant sbagliato non restituisce righe.
        var cross = await broker.LeggiAsync(Guid.NewGuid(), utente, valido!.Lease, "Aziende");
        Assert.False(cross.Consentito);
        Assert.Empty(cross.Righe);

        // E il mandato legittimo continua a funzionare per il suo titolare (nessun lockout).
        var legittimo = await broker.LeggiAsync(tenant, utente, valido.Lease, "Aziende");
        Assert.True(legittimo.Consentito);
    }

    private static string Manometti(string lease, Random casuale)
    {
        var caratteri = lease.ToCharArray();
        var i = casuale.Next(3, caratteri.Length); // oltre il prefisso "v1."
        caratteri[i] = caratteri[i] == 'A' ? 'B' : 'A';
        return new string(caratteri);
    }

    private sealed class RevocaPrimaDelConsumoInterceptor(
        Func<CancellationToken, Task> revoca) : SaveChangesInterceptor
    {
        private int _intercettato;

        public bool Intercettato => Volatile.Read(ref _intercettato) == 1;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var staConsumandoQuota = eventData.Context?.ChangeTracker
                .Entries<AiScopeGrantEntity>()
                .Any(e => e.State == EntityState.Modified
                    && e.Property(g => g.LettureUsate).IsModified) == true;

            if (staConsumandoQuota
                && Interlocked.CompareExchange(ref _intercettato, 1, 0) == 0)
                await revoca(cancellationToken);

            return result;
        }
    }

    private sealed class PausaPrimaDellaProiezioneAziendaInterceptor : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _raggiunta = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _riprendi = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _intercettato;

        public Task AttendiAsync() => _raggiunta.Task.WaitAsync(TimeSpan.FromSeconds(10));

        public void Riprendi() => _riprendi.TrySetResult();

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            var proiezioneAzienda = command.CommandText.Contains("RagioneSociale", StringComparison.Ordinal)
                && command.CommandText.Contains("IbanProtetto", StringComparison.Ordinal)
                && command.CommandText.Contains("FROM \"Aziende\"", StringComparison.Ordinal);
            if (proiezioneAzienda
                && Interlocked.CompareExchange(ref _intercettato, 1, 0) == 0)
            {
                _raggiunta.TrySetResult();
                await _riprendi.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class OrologioRegolabile(DateTimeOffset iniziale) : TimeProvider
    {
        private DateTimeOffset _adesso = iniziale;
        public override DateTimeOffset GetUtcNow() => _adesso;
        public void Avanza(TimeSpan delta) => _adesso += delta;
    }
}
