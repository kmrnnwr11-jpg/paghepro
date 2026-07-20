using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
        var broker = new ProfessionalQueryBroker(
            db, governor,
            new ScadenzarioService(db), new PresenzeService(db), new AnagraficheLavoroService(db),
            _orologio);
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
        PurposeCode: "CONTROLLO_ANOMALIE",
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
        var (grant, _) = await governor.EmettiAsync(tenant, utente, MandatoSu(aziendaId, "Cedolini"));

        var esito = await broker.LeggiAsync(tenant, utente, grant!.Lease, "Cedolini");
        Assert.False(esito.Consentito);
        Assert.Contains("proiezione", esito.Motivo);
        Assert.Empty(esito.Righe);
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

    private sealed class OrologioRegolabile(DateTimeOffset iniziale) : TimeProvider
    {
        private DateTimeOffset _adesso = iniziale;
        public override DateTimeOffset GetUtcNow() => _adesso;
        public void Avanza(TimeSpan delta) => _adesso += delta;
    }
}
