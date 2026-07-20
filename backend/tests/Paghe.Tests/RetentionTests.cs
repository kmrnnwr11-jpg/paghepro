using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Paghe.Persistence.Gdpr.Retention;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test del motore di retention GDPR: policy versionate append-only con concorrenza ottimistica,
/// anteprima vincolante con impronte e scadenza, esecuzione idempotente con codice di conferma,
/// blocchi di conservazione (legal hold), isolamento tenant e catena digest delle ricevute.
/// </summary>
public sealed class RetentionTests : IDisposable
{
    private static readonly DateTimeOffset Ora = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;

    public RetentionTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    private static RetentionService Servizio(PagheDbContext db, DateTimeOffset? ora = null) =>
        new(db, new OrologioFisso(ora ?? Ora));

    private static ModificaPolicyRetention PolicyCompleta(
        int giorni, int versioneAttesa = 0, bool attiva = true) => new(
        versioneAttesa,
        giorni,
        attiva,
        Approvata: true,
        BaseGiuridica: "Art. 6.1.f GDPR — interesse legittimo (test)",
        RiferimentoDecisione: "Verbale di prova luglio 2026",
        IstruzioniBackup: "I backup ruotano entro 30 giorni dalla cancellazione.");

    private async Task SeminaAuditAsync(Guid tenant, string azione, int giorniFa)
    {
        await using var db = NuovoContesto();
        db.AuditLog.Add(new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            // "Accesso" come le righe reali del registro accessi: le righe "ModificaDati" Added
            // vengono staccate dall'interceptor (anti-duplicato) e non arriverebbero al DB.
            Categoria = "Accesso",
            Azione = azione,
            Entita = "SeedTest",
            ChiaveEntita = Guid.NewGuid().ToString(),
            QuandoUtc = Ora.UtcDateTime.AddDays(-giorniFa),
        });
        await db.SaveChangesAsync();
    }

    private async Task<PolicyRetentionVista> AttivaPolicyAsync(
        Guid tenant, Guid utente, string categoria, int giorni)
    {
        await using var db = NuovoContesto();
        return await Servizio(db).AggiornaPolicyAsync(
            tenant, utente, categoria, PolicyCompleta(giorni));
    }

    [Fact]
    public async Task Catalogo_SenzaPolicySalvate_EspostoConVersioneZeroENonAttivo()
    {
        await using var db = NuovoContesto();
        var policy = await Servizio(db).ElencaPolicyAsync(Guid.NewGuid());

        Assert.Equal(2, policy.Count);
        Assert.Contains(policy, p => p.Categoria == CatalogoRetention.RegistroAttivita);
        Assert.Contains(policy, p => p.Categoria == CatalogoRetention.PromemoriaManualiChiusi);
        Assert.All(policy, p =>
        {
            Assert.Equal(0, p.Versione);
            Assert.False(p.Attiva);
            Assert.Null(p.GiorniConservazione);
        });
    }

    [Fact]
    public async Task Attivazione_SenzaCriteriLegaliCompleti_Rifiutata()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();
        await using var db = NuovoContesto();
        var servizio = Servizio(db);

        // Attiva ma senza base giuridica: il motore non accetta durate "di fatto".
        await Assert.ThrowsAsync<RetentionNonValidaException>(() =>
            servizio.AggiornaPolicyAsync(tenant, utente, CatalogoRetention.RegistroAttivita,
                new ModificaPolicyRetention(0, 365, Attiva: true, Approvata: true,
                    BaseGiuridica: null, RiferimentoDecisione: "Verbale", IstruzioniBackup: "Rotazione 30 gg")));

        // Attiva ma senza durata.
        await Assert.ThrowsAsync<RetentionNonValidaException>(() =>
            servizio.AggiornaPolicyAsync(tenant, utente, CatalogoRetention.RegistroAttivita,
                new ModificaPolicyRetention(0, null, Attiva: true, Approvata: true,
                    BaseGiuridica: "Art. 6", RiferimentoDecisione: "Verbale", IstruzioniBackup: "Rotazione 30 gg")));

        Assert.Empty(await db.PolicyRetention.ToListAsync());
    }

    [Fact]
    public async Task AggiornaPolicy_ConcorrenzaOttimisticaEVersioniAppendOnly()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();

        var v1 = await AttivaPolicyAsync(tenant, utente, CatalogoRetention.RegistroAttivita, 365);
        Assert.Equal(1, v1.Versione);

        await using var db = NuovoContesto();
        var servizio = Servizio(db);

        // Versione attesa stantia: conflitto, nessuna scrittura.
        await Assert.ThrowsAsync<RetentionConflittoException>(() =>
            servizio.AggiornaPolicyAsync(tenant, utente,
                CatalogoRetention.RegistroAttivita, PolicyCompleta(180, versioneAttesa: 0)));

        var v2 = await servizio.AggiornaPolicyAsync(tenant, utente,
            CatalogoRetention.RegistroAttivita, PolicyCompleta(180, versioneAttesa: 1));
        Assert.Equal(2, v2.Versione);

        // Storia append-only: entrambe le versioni restano archiviate.
        var versioni = await db.PolicyRetention
            .Where(p => p.TenantId == tenant)
            .OrderBy(p => p.Versione)
            .ToListAsync();
        Assert.Equal([1, 2], versioni.Select(p => p.Versione));
        Assert.Equal(365, versioni[0].GiorniConservazione);
        Assert.Equal(180, versioni[1].GiorniConservazione);
    }

    [Fact]
    public async Task Anteprima_SenzaPolicyAttivaECompleta_Conflitto()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();
        await using var db = NuovoContesto();
        var servizio = Servizio(db);

        // Nessuna policy salvata.
        await Assert.ThrowsAsync<RetentionConflittoException>(() =>
            servizio.CreaAnteprimaAsync(tenant, utente, CatalogoRetention.RegistroAttivita));

        // Policy salvata ma non attiva.
        await servizio.AggiornaPolicyAsync(tenant, utente,
            CatalogoRetention.RegistroAttivita, PolicyCompleta(365, attiva: false));
        await Assert.ThrowsAsync<RetentionConflittoException>(() =>
            servizio.CreaAnteprimaAsync(tenant, utente, CatalogoRetention.RegistroAttivita));
    }

    [Fact]
    public async Task Esecuzione_CancellaSoloIVecchiDelTenant_EProduceRicevutaConDigest()
    {
        var tenant = Guid.NewGuid();
        var altroTenant = Guid.NewGuid();
        var utente = Guid.NewGuid();
        await SeminaAuditAsync(tenant, "MarkerVecchio1", giorniFa: 400);
        await SeminaAuditAsync(tenant, "MarkerVecchio2", giorniFa: 500);
        await SeminaAuditAsync(tenant, "MarkerVecchio3", giorniFa: 600);
        await SeminaAuditAsync(tenant, "MarkerRecente", giorniFa: 10);
        await SeminaAuditAsync(altroTenant, "MarkerAltroTenantVecchio", giorniFa: 700);
        await AttivaPolicyAsync(tenant, utente, CatalogoRetention.RegistroAttivita, 365);

        await using var db = NuovoContesto();
        var servizio = Servizio(db);
        var anteprima = await servizio.CreaAnteprimaAsync(
            tenant, utente, CatalogoRetention.RegistroAttivita);
        Assert.Equal(3, anteprima.Candidati);
        Assert.Equal(0, anteprima.Bloccati);
        Assert.Equal(3, anteprima.Elaborabili);

        var ricevuta = await servizio.EseguiAsync(
            tenant, utente, anteprima.Id, Guid.NewGuid(), anteprima.CodiceConferma);

        Assert.Equal(3, ricevuta.Elaborati);
        Assert.Equal("Completata", ricevuta.Esito);
        Assert.Equal(0, ricevuta.RestantiStimati);
        Assert.False(string.IsNullOrWhiteSpace(ricevuta.Digest));

        var marcatori = await db.AuditLog.AsNoTracking()
            .Where(a => a.Entita == "SeedTest")
            .Select(a => new { a.TenantId, a.Azione })
            .ToListAsync();
        Assert.DoesNotContain(marcatori, m => m.Azione.StartsWith("MarkerVecchio"));
        Assert.Contains(marcatori, m => m.TenantId == tenant && m.Azione == "MarkerRecente");
        Assert.Contains(marcatori, m => m.TenantId == altroTenant); // l'altro tenant è intatto

        var anteprimaDb = await db.AnteprimeRetention.AsNoTracking()
            .SingleAsync(a => a.Id == anteprima.Id);
        Assert.NotNull(anteprimaDb.ConsumataUtc);
    }

    [Fact]
    public async Task Esecuzione_LottoPiccolo_ProduceEsitoParzialeConRestantiStimati()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();
        await SeminaAuditAsync(tenant, "MarkerVecchio1", giorniFa: 400);
        await SeminaAuditAsync(tenant, "MarkerVecchio2", giorniFa: 500);
        await SeminaAuditAsync(tenant, "MarkerVecchio3", giorniFa: 600);
        await AttivaPolicyAsync(tenant, utente, CatalogoRetention.RegistroAttivita, 365);

        await using var db = NuovoContesto();
        var servizio = Servizio(db);
        var anteprima = await servizio.CreaAnteprimaAsync(
            tenant, utente, CatalogoRetention.RegistroAttivita);

        var ricevuta = await servizio.EseguiAsync(
            tenant, utente, anteprima.Id, Guid.NewGuid(), anteprima.CodiceConferma,
            dimensioneLotto: 1);

        Assert.Equal(1, ricevuta.Elaborati);
        Assert.Equal("Parziale", ricevuta.Esito);
        Assert.Equal(2, ricevuta.RestantiStimati);
        Assert.Equal(2, await db.AuditLog.CountAsync(a =>
            a.TenantId == tenant && a.Entita == "SeedTest"));
    }

    [Fact]
    public async Task Esecuzione_Idempotente_StessaChiaveNonCancellaDueVolte()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();
        var chiave = Guid.NewGuid();
        await SeminaAuditAsync(tenant, "MarkerVecchio", giorniFa: 400);
        await AttivaPolicyAsync(tenant, utente, CatalogoRetention.RegistroAttivita, 365);

        Guid anteprimaId;
        RicevutaRetentionVista prima;
        await using (var db = NuovoContesto())
        {
            var servizio = Servizio(db);
            var anteprima = await servizio.CreaAnteprimaAsync(
                tenant, utente, CatalogoRetention.RegistroAttivita);
            anteprimaId = anteprima.Id;
            prima = await servizio.EseguiAsync(
                tenant, utente, anteprima.Id, chiave, anteprima.CodiceConferma);
        }

        await using (var db = NuovoContesto())
        {
            var servizio = Servizio(db);

            // Replay con la stessa chiave: restituisce la stessa ricevuta, senza rieseguire.
            var replay = await servizio.EseguiAsync(
                tenant, utente, anteprimaId, chiave, "CODICE-IGNORATO");
            Assert.Equal(prima.Id, replay.Id);
            Assert.Equal(prima.Digest, replay.Digest);
            Assert.Equal(1, await db.EsecuzioniRetention.CountAsync(e => e.TenantId == tenant));

            // Stessa chiave su un'altra anteprima: conflitto esplicito.
            await Assert.ThrowsAsync<RetentionConflittoException>(() =>
                servizio.EseguiAsync(tenant, utente, Guid.NewGuid(), chiave, "X"));
        }
    }

    [Fact]
    public async Task Esecuzione_CodiceConfermaErrato_RifiutataSenzaCancellare()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();
        await SeminaAuditAsync(tenant, "MarkerVecchio", giorniFa: 400);
        await AttivaPolicyAsync(tenant, utente, CatalogoRetention.RegistroAttivita, 365);

        await using var db = NuovoContesto();
        var servizio = Servizio(db);
        var anteprima = await servizio.CreaAnteprimaAsync(
            tenant, utente, CatalogoRetention.RegistroAttivita);

        await Assert.ThrowsAsync<RetentionNonValidaException>(() =>
            servizio.EseguiAsync(tenant, utente, anteprima.Id, Guid.NewGuid(), "CONFERMA-SBAGLIATA"));

        Assert.Equal(1, await db.AuditLog.CountAsync(a =>
            a.TenantId == tenant && a.Entita == "SeedTest"));
    }

    [Fact]
    public async Task Esecuzione_AnteprimaScaduta_Conflitto()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();
        await SeminaAuditAsync(tenant, "MarkerVecchio", giorniFa: 400);
        await AttivaPolicyAsync(tenant, utente, CatalogoRetention.RegistroAttivita, 365);

        await using var db = NuovoContesto();
        var anteprima = await Servizio(db).CreaAnteprimaAsync(
            tenant, utente, CatalogoRetention.RegistroAttivita);

        // 11 minuti dopo (l'anteprima vale 10): rifiutata, si deve ricalcolare.
        var dopo = Servizio(db, Ora.AddMinutes(11));
        await Assert.ThrowsAsync<RetentionConflittoException>(() =>
            dopo.EseguiAsync(tenant, utente, anteprima.Id, Guid.NewGuid(), anteprima.CodiceConferma));
    }

    [Fact]
    public async Task BloccoConservazione_AzzeraGliElaborabiliEImpedisceLEsecuzione()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();
        await SeminaAuditAsync(tenant, "MarkerVecchio", giorniFa: 400);
        await AttivaPolicyAsync(tenant, utente, CatalogoRetention.RegistroAttivita, 365);

        await using var db = NuovoContesto();
        var servizio = Servizio(db);
        var blocco = await servizio.CreaBloccoAsync(tenant, utente,
            CatalogoRetention.RegistroAttivita, MotiviBloccoConservazione.Contenzioso);

        var anteprima = await servizio.CreaAnteprimaAsync(
            tenant, utente, CatalogoRetention.RegistroAttivita);
        Assert.Equal(1, anteprima.Candidati);
        Assert.Equal(1, anteprima.Bloccati);
        Assert.Equal(0, anteprima.Elaborabili);

        await Assert.ThrowsAsync<RetentionNonValidaException>(() =>
            servizio.EseguiAsync(tenant, utente, anteprima.Id, Guid.NewGuid(), anteprima.CodiceConferma));

        // Revocato il blocco, una NUOVA anteprima torna elaborabile.
        await servizio.RevocaBloccoAsync(tenant, utente, blocco.Id);
        var dopoRevoca = await servizio.CreaAnteprimaAsync(
            tenant, utente, CatalogoRetention.RegistroAttivita);
        Assert.Equal(1, dopoRevoca.Elaborabili);
    }

    [Fact]
    public async Task BloccoCreatoDopoLAnteprima_InvalidaLeImpronte()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();
        await SeminaAuditAsync(tenant, "MarkerVecchio", giorniFa: 400);
        await AttivaPolicyAsync(tenant, utente, CatalogoRetention.RegistroAttivita, 365);

        await using var db = NuovoContesto();
        var servizio = Servizio(db);
        var anteprima = await servizio.CreaAnteprimaAsync(
            tenant, utente, CatalogoRetention.RegistroAttivita);
        await servizio.CreaBloccoAsync(tenant, utente,
            CatalogoRetention.RegistroAttivita, MotiviBloccoConservazione.RichiestaInteressato);

        await Assert.ThrowsAsync<RetentionConflittoException>(() =>
            servizio.EseguiAsync(tenant, utente, anteprima.Id, Guid.NewGuid(), anteprima.CodiceConferma));
        Assert.Equal(1, await db.AuditLog.CountAsync(a =>
            a.TenantId == tenant && a.Entita == "SeedTest"));
    }

    [Fact]
    public async Task PromemoriaManualiChiusi_NonToccaAutomaticiApertiORecenti()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();
        var vecchia = Ora.UtcDateTime.AddDays(-400);
        var recente = Ora.UtcDateTime.AddDays(-10);

        await using (var db = NuovoContesto())
        {
            db.Promemoria.AddRange(
                Promemoria(tenant, "Manuale chiuso vecchio", TipoPromemoria.Manuale,
                    StatoPromemoria.Completato, vecchia),
                Promemoria(tenant, "Manuale annullato vecchio", TipoPromemoria.Manuale,
                    StatoPromemoria.Annullato, vecchia),
                Promemoria(tenant, "Manuale aperto vecchio", TipoPromemoria.Manuale,
                    StatoPromemoria.Aperto, vecchia),
                Promemoria(tenant, "Durc chiuso vecchio", TipoPromemoria.Durc,
                    StatoPromemoria.Completato, vecchia),
                Promemoria(tenant, "Manuale chiuso recente", TipoPromemoria.Manuale,
                    StatoPromemoria.Completato, recente));
            await db.SaveChangesAsync();
        }
        await AttivaPolicyAsync(tenant, utente, CatalogoRetention.PromemoriaManualiChiusi, 365);

        await using (var db = NuovoContesto())
        {
            var servizio = Servizio(db);
            var anteprima = await servizio.CreaAnteprimaAsync(
                tenant, utente, CatalogoRetention.PromemoriaManualiChiusi);
            Assert.Equal(2, anteprima.Candidati); // solo i manuali chiusi e vecchi

            var ricevuta = await servizio.EseguiAsync(
                tenant, utente, anteprima.Id, Guid.NewGuid(), anteprima.CodiceConferma);
            Assert.Equal(2, ricevuta.Elaborati);

            var titoliRestanti = await db.Promemoria.AsNoTracking()
                .Where(p => p.TenantId == tenant)
                .Select(p => p.Titolo)
                .ToListAsync();
            Assert.Equal(
                ["Durc chiuso vecchio", "Manuale aperto vecchio", "Manuale chiuso recente"],
                titoliRestanti.OrderBy(t => t));
        }
    }

    [Fact]
    public async Task CatenaDigest_LaSecondaRicevutaEAncorataAllaPrima()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();
        await SeminaAuditAsync(tenant, "MarkerVecchio1", giorniFa: 400);
        await AttivaPolicyAsync(tenant, utente, CatalogoRetention.RegistroAttivita, 365);

        await using var db = NuovoContesto();
        var servizio = Servizio(db);
        var anteprima1 = await servizio.CreaAnteprimaAsync(
            tenant, utente, CatalogoRetention.RegistroAttivita);
        var prima = await servizio.EseguiAsync(
            tenant, utente, anteprima1.Id, Guid.NewGuid(), anteprima1.CodiceConferma);

        await SeminaAuditAsync(tenant, "MarkerVecchio2", giorniFa: 500);
        var anteprima2 = await servizio.CreaAnteprimaAsync(
            tenant, utente, CatalogoRetention.RegistroAttivita);
        var seconda = await servizio.EseguiAsync(
            tenant, utente, anteprima2.Id, Guid.NewGuid(), anteprima2.CodiceConferma);

        var righe = await db.EsecuzioniRetention.AsNoTracking()
            .Where(e => e.TenantId == tenant)
            .ToDictionaryAsync(e => e.Id);
        Assert.Null(righe[prima.Id].DigestPrecedente);
        Assert.Equal(prima.Digest, righe[seconda.Id].DigestPrecedente);
        Assert.NotEqual(prima.Digest, seconda.Digest);
    }

    private static PromemoriaEntity Promemoria(
        Guid tenant, string titolo, string tipo, string stato, DateTime ultimoAggiornamento) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenant,
        Tipo = tipo,
        Titolo = titolo,
        Descrizione = titolo,
        DataScadenza = DateOnly.FromDateTime(ultimoAggiornamento),
        Stato = stato,
        Messaggio = "Testo di prova",
        CreatoIl = ultimoAggiornamento,
        UltimoAggiornamento = ultimoAggiornamento,
    };

    public void Dispose() => _connection.Dispose();
}
