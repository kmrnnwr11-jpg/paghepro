using System.Text;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Paghe.Persistence.Ai;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test del Usage Governor (G2): nessun accesso senza mandato valido, tenant isolation,
/// scadenza/revoca/inattività, lease firmata anti-manomissione, quota atomica sotto
/// concorrenza e catena audit tamper-evident. Il DB è un file SQLite temporaneo così i test
/// di concorrenza usano connessioni separate come in produzione.
/// </summary>
public sealed class AiGovernorTests : IDisposable
{
    private readonly string _percorsoDb =
        Path.Combine(Path.GetTempPath(), $"paghe-aigov-{Guid.NewGuid():N}.db");

    private readonly AiGovernorOptions _opzioni =
        new(AiLeaseCodec.DerivaChiave(Encoding.UTF8.GetBytes("chiave-di-prova-per-i-test-32byte!")));

    private readonly OrologioRegolabile _orologio = new(new DateTimeOffset(2026, 7, 16, 9, 0, 0, TimeSpan.Zero));

    public AiGovernorTests()
    {
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        foreach (var suffisso in new[] { "", "-wal", "-shm" })
        {
            var percorso = _percorsoDb + suffisso;
            if (File.Exists(percorso)) File.Delete(percorso);
        }
    }

    private PagheDbContext NuovoContesto() => new(
        new DbContextOptionsBuilder<PagheDbContext>()
            .UseSqlite($"DataSource={_percorsoDb}")
            .Options);

    private AiGrantService NuovoServizio(PagheDbContext db) => new(db, _opzioni, _orologio);

    private static RichiestaGrant RichiestaTipo(Guid azienda) => new(
        PurposeCode: "CONTROLLO_ANOMALIE",
        Modalita: "Guidata",
        ScopeLevel: "S2",
        AziendaId: azienda,
        Periodo: "2026-07",
        Risorse: ["Cartellini", "Rapporti", "Cedolini"],
        Operazioni: ["read", "search", "draft"]);

    // ---------- Emissione: policy e catalogo (default DENY) ----------

    [Fact]
    public async Task FinalitaNonAmmessaNegata()
    {
        await using var db = NuovoContesto();
        var (grant, errore) = await NuovoServizio(db).EmettiAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            RichiestaTipo(Guid.NewGuid()) with { PurposeCode = "FINALITA_INVENTATA" });
        Assert.Null(grant);
        Assert.Contains("non ammessa", errore);
    }

    [Fact]
    public async Task RisorsaNonACatalogoNegata()
    {
        await using var db = NuovoContesto();
        var (grant, errore) = await NuovoServizio(db).EmettiAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            RichiestaTipo(Guid.NewGuid()) with { Risorse = ["TabellaSconosciuta"] });
        Assert.Null(grant);
        Assert.Contains("non a catalogo", errore);
    }

    [Theory]
    [InlineData("Cartellini.Malattia")] // D3A: serve grant GS, non in G2
    [InlineData("Utenti")]              // D4: mai al modello
    [InlineData("AuditLog")]            // control plane
    public async Task ClassiSensibiliEControlPlaneNegati(string risorsa)
    {
        await using var db = NuovoContesto();
        var (grant, errore) = await NuovoServizio(db).EmettiAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            RichiestaTipo(Guid.NewGuid()) with { Risorse = [risorsa] });
        Assert.Null(grant);
        Assert.Contains("non concedibile", errore);
    }

    [Fact]
    public async Task PreflightSpiegaCosaFaraInItaliano()
    {
        await using var db = NuovoContesto();
        var esito = await NuovoServizio(db).PreflightAsync(
            Guid.NewGuid(), Guid.NewGuid(), RichiestaTipo(Guid.NewGuid()));
        Assert.True(esito.Consentito);
        Assert.Contains("Leggerò:", esito.Descrizione);
        Assert.Contains("Non farò: invii, pagamenti o modifiche", esito.Descrizione);
    }

    // ---------- Enforcement: nessun accesso senza mandato valido ----------

    [Fact]
    public async Task AccessoSenzaLeaseNegatoEAuditato()
    {
        var tenant = Guid.NewGuid();
        await using var db = NuovoContesto();
        var esito = await NuovoServizio(db).AutorizzaAccessoAsync(
            tenant, Guid.NewGuid(), lease: null, "Cedolini", "read");
        Assert.False(esito.Consentito);

        var diniego = Assert.Single(db.AiAudit.Where(a => a.TenantId == tenant && a.Evento == "Diniego"));
        Assert.Contains("Lease assente", diniego.Motivo);
    }

    [Fact]
    public async Task LeaseManomessaNegata()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();
        await using var db = NuovoContesto();
        var servizio = NuovoServizio(db);
        var (grant, _) = await servizio.EmettiAsync(tenant, utente, RichiestaTipo(Guid.NewGuid()));

        // Alterare un carattere del payload invalida la firma HMAC.
        var parti = grant!.Lease.Split('.');
        var alterato = parti[1][0] == 'A' ? 'B' : 'A';
        var manomessa = $"{parti[0]}.{alterato}{parti[1][1..]}.{parti[2]}";

        var esito = await servizio.AutorizzaAccessoAsync(tenant, utente, manomessa, "Cedolini", "read");
        Assert.False(esito.Consentito);
        Assert.Contains("firma non valida", esito.Motivo);
    }

    [Fact]
    public async Task TenantDiversoNegato()
    {
        var tenantA = Guid.NewGuid();
        var utente = Guid.NewGuid();
        await using var db = NuovoContesto();
        var servizio = NuovoServizio(db);
        var (grant, _) = await servizio.EmettiAsync(tenantA, utente, RichiestaTipo(Guid.NewGuid()));

        var esito = await servizio.AutorizzaAccessoAsync(Guid.NewGuid(), utente, grant!.Lease, "Cedolini", "read");
        Assert.False(esito.Consentito);
        Assert.Contains("Tenant non corrispondente", esito.Motivo);
    }

    [Fact]
    public async Task RisorsaEOperazioneFuoriMandatoNegate()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();
        await using var db = NuovoContesto();
        var servizio = NuovoServizio(db);
        var (grant, _) = await servizio.EmettiAsync(tenant, utente, RichiestaTipo(Guid.NewGuid()) with
        {
            PurposeCode = "SPIEGAZIONE_CALCOLO", ScopeLevel = "S1", Periodo = null,
            Risorse = ["Cedolini"], Operazioni = ["read"],
        });

        var fuoriRisorsa = await servizio.AutorizzaAccessoAsync(tenant, utente, grant!.Lease, "Fatture", "read");
        Assert.False(fuoriRisorsa.Consentito);
        Assert.Contains("fuori dal mandato", fuoriRisorsa.Motivo);

        var fuoriOperazione = await servizio.AutorizzaAccessoAsync(tenant, utente, grant.Lease, "Cedolini", "draft");
        Assert.False(fuoriOperazione.Consentito);
        Assert.Contains("fuori dal mandato", fuoriOperazione.Motivo);
    }

    [Fact]
    public async Task ScadenzaEInattivitaBloccano()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();
        await using var db = NuovoContesto();
        var servizio = NuovoServizio(db);
        var (grant, _) = await servizio.EmettiAsync(tenant, utente, RichiestaTipo(Guid.NewGuid()));

        // Dentro il TTL ma oltre i 30 minuti di inattività della modalità Guidata.
        _orologio.Avanza(TimeSpan.FromMinutes(31));
        var inattivo = await servizio.AutorizzaAccessoAsync(tenant, utente, grant!.Lease, "Cedolini", "read");
        Assert.False(inattivo.Consentito);
        Assert.Contains("inattivo", inattivo.Motivo);

        // Oltre il TTL assoluto (120 minuti per CONTROLLO_ANOMALIE): la lease stessa è scaduta.
        _orologio.Avanza(TimeSpan.FromMinutes(120));
        var scaduto = await servizio.AutorizzaAccessoAsync(tenant, utente, grant.Lease, "Cedolini", "read");
        Assert.False(scaduto.Consentito);
        Assert.Contains("scadut", scaduto.Motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RevocaBloccaSubitoEArrestaTuttoRevocaTutti()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();
        await using var db = NuovoContesto();
        var servizio = NuovoServizio(db);
        var (grant, _) = await servizio.EmettiAsync(tenant, utente, RichiestaTipo(Guid.NewGuid()));

        Assert.True((await servizio.AutorizzaAccessoAsync(tenant, utente, grant!.Lease, "Cedolini", "read")).Consentito);
        await servizio.RevocaAsync(tenant, utente, grant.Manifest.GrantId);

        var dopoRevoca = await servizio.AutorizzaAccessoAsync(tenant, utente, grant.Lease, "Cedolini", "read");
        Assert.False(dopoRevoca.Consentito);
        Assert.Contains("revocato", dopoRevoca.Motivo);

        // «Arresta AI»: due nuovi mandati attivi, un colpo li revoca entrambi.
        var (g1, _) = await servizio.EmettiAsync(tenant, utente, RichiestaTipo(Guid.NewGuid()));
        var (g2, _) = await servizio.EmettiAsync(tenant, utente, RichiestaTipo(Guid.NewGuid()));
        Assert.Equal(2, await servizio.ArrestaTuttoAsync(tenant, utente));
        Assert.False((await servizio.AutorizzaAccessoAsync(tenant, utente, g1!.Lease, "Cedolini", "read")).Consentito);
        Assert.False((await servizio.AutorizzaAccessoAsync(tenant, utente, g2!.Lease, "Cedolini", "read")).Consentito);
    }

    // ---------- Quota atomica sotto concorrenza ----------

    [Fact]
    public async Task QuotaNonSiAggiraConAccessiConcorrenti()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();
        string lease;
        Guid grantId;
        await using (var db = NuovoContesto())
        {
            var (grant, errore) = await NuovoServizio(db).EmettiAsync(
                tenant, utente, RichiestaTipo(Guid.NewGuid()) with { MaxLetture = 10 });
            Assert.Null(errore);
            lease = grant!.Lease;
            grantId = grant.Manifest.GrantId;
        }

        // 25 accessi in parallelo, ciascuno con la propria connessione (come richieste HTTP reali).
        var esiti = await Task.WhenAll(Enumerable.Range(0, 25).Select(async _ =>
        {
            await using var db = NuovoContesto();
            return await NuovoServizio(db).AutorizzaAccessoAsync(tenant, utente, lease, "Cedolini", "read");
        }));

        Assert.Equal(10, esiti.Count(e => e.Consentito));
        Assert.Equal(15, esiti.Count(e => !e.Consentito && e.Motivo!.Contains("Quota")));

        await using var verifica = NuovoContesto();
        var salvato = await verifica.AiScopeGrants.SingleAsync(g => g.Id == grantId);
        Assert.Equal(10, salvato.LettureUsate); // mai oltre il massimo, mai doppi conteggi
    }

    // ---------- Audit: catena tamper-evident e niente rumore nel registro GDPR ----------

    [Fact]
    public async Task CatenaAuditIntegraEManomissioneRilevata()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();
        await using var db = NuovoContesto();
        var servizio = NuovoServizio(db);
        var (grant, _) = await servizio.EmettiAsync(tenant, utente, RichiestaTipo(Guid.NewGuid()));
        await servizio.AutorizzaAccessoAsync(tenant, utente, grant!.Lease, "Cedolini", "read");
        await servizio.AutorizzaAccessoAsync(tenant, utente, grant.Lease, "Fatture", "read"); // diniego
        await servizio.RevocaAsync(tenant, utente, grant.Manifest.GrantId);

        var integra = await servizio.VerificaCatenaAsync(tenant);
        Assert.True(integra.Integra);
        Assert.True(integra.Righe >= 4); // emissione, accesso, diniego, revoca

        // Manomissione diretta sul DB (EF la vieterebbe): la catena la rileva.
        await db.Database.ExecuteSqlAsync(
            $"UPDATE AiAudit SET Dettaglio = 'alterato' WHERE TenantId = {tenant} AND Sequenza = 2");
        var dopoTampering = await servizio.VerificaCatenaAsync(tenant);
        Assert.False(dopoTampering.Integra);
        Assert.Equal(2, dopoTampering.PrimaSequenzaAnomala);
    }

    [Fact]
    public async Task IlGovernorNonInquinaIlRegistroGdpr()
    {
        var tenant = Guid.NewGuid();
        var utente = Guid.NewGuid();
        await using var db = NuovoContesto();
        var servizio = NuovoServizio(db);
        var (grant, _) = await servizio.EmettiAsync(tenant, utente, RichiestaTipo(Guid.NewGuid()));
        await servizio.AutorizzaAccessoAsync(tenant, utente, grant!.Lease, "Cedolini", "read");

        // Mandati e audit AI sono control plane: nessuna riga nel registro attività GDPR.
        Assert.Empty(db.AuditLog.Where(a => a.TenantId == tenant));
    }

    private sealed class OrologioRegolabile(DateTimeOffset iniziale) : TimeProvider
    {
        private DateTimeOffset _adesso = iniziale;
        public override DateTimeOffset GetUtcNow() => _adesso;
        public void Avanza(TimeSpan delta) => _adesso += delta;
    }
}
