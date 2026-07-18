using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Ai;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Paghe.Persistence.Gdpr;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Modulo Posta (email/PEC): credenziale casella e corpo messaggio cifrati a riposo, isolamento
/// per tenant, e triage (urgente/pubblicità) SEMPRE come proposta finché l'utente non la conferma
/// — mai applicato in automatico (Email/PEC restano fuori dal perimetro AI diretto).
/// </summary>
public sealed class PostaTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public PostaTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    // --- Cifratura a riposo ---

    [Fact]
    public async Task PasswordCasella_CifrataNelDb_MaRilettaInChiaro()
    {
        const string password = "segreto-imap-123";
        var tenant = Guid.NewGuid();
        var casellaId = Guid.NewGuid();

        await using (var db = NuovoContesto())
        {
            db.CaselleEmail.Add(new CasellaEmailEntity
            {
                Id = casellaId, TenantId = tenant, Etichetta = "PEC studio", Tipo = "Pec",
                Host = "imaps.pec.it", Porta = 993, Utente = "studio@pec.it", Password = password,
            });
            await db.SaveChangesAsync();
        }

        await using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Password FROM CaselleEmail WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", casellaId);
            var grezzo = (string?)await cmd.ExecuteScalarAsync();
            Assert.NotNull(grezzo);
            Assert.StartsWith(ProtettoreDati.Corrente.PrefissoChiaveCorrente, grezzo);
            Assert.DoesNotContain(password, grezzo);
        }

        await using (var db = NuovoContesto())
        {
            var c = await db.CaselleEmail.SingleAsync(x => x.Id == casellaId);
            Assert.Equal(password, c.Password);
        }
    }

    [Fact]
    public async Task CorpoMessaggio_CifratoNelDb_MaRilettoInChiaro()
    {
        const string corpo = "Contenuto riservato del messaggio.";
        var tenant = Guid.NewGuid();
        var messaggioId = Guid.NewGuid();

        await using (var db = NuovoContesto())
        {
            db.MessaggiEmail.Add(new MessaggioEmailEntity
            {
                Id = messaggioId, TenantId = tenant, CasellaId = Guid.NewGuid(), UidImap = 1,
                Mittente = "cliente@example.it", Oggetto = "Richiesta", DataRicezione = DateTime.UtcNow,
                Corpo = corpo,
            });
            await db.SaveChangesAsync();
        }

        await using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Corpo FROM MessaggiEmail WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", messaggioId);
            var grezzo = (string?)await cmd.ExecuteScalarAsync();
            Assert.NotNull(grezzo);
            Assert.DoesNotContain(corpo, grezzo);
        }

        await using (var db = NuovoContesto())
            Assert.Equal(corpo, (await db.MessaggiEmail.SingleAsync(m => m.Id == messaggioId)).Corpo);
    }

    // --- Isolamento tenant ---

    [Fact]
    public async Task Caselle_IsolatePerTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using (var db = NuovoContesto())
            await new CasellaEmailService(db).CreaCasellaAsync(tenantA,
                new CasellaEmailInput("Email studio", "Email", "imap.example.it", 993, "a@example.it", "pw", true, true));

        await using (var db = NuovoContesto())
        {
            var svc = new CasellaEmailService(db);
            Assert.Single(await svc.ElencoCaselleAsync(tenantA));
            Assert.Empty(await svc.ElencoCaselleAsync(tenantB));
        }
    }

    [Fact]
    public async Task EliminaCasella_DiUnAltroTenant_NonRiesce()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        CasellaEmailEntity casella;
        await using (var db = NuovoContesto())
            casella = await new CasellaEmailService(db).CreaCasellaAsync(tenantA,
                new CasellaEmailInput("Email studio", "Email", "imap.example.it", 993, "a@example.it", "pw", true, true));

        await using (var db = NuovoContesto())
            Assert.False(await new CasellaEmailService(db).EliminaCasellaAsync(tenantB, casella.Id));

        await using (var db = NuovoContesto())
            Assert.Equal(1, await db.CaselleEmail.CountAsync(c => c.Id == casella.Id));
    }

    [Fact]
    public async Task Messaggio_DiUnAltroTenant_NonVisibile()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var messaggioId = Guid.NewGuid();

        await using (var db = NuovoContesto())
        {
            db.MessaggiEmail.Add(new MessaggioEmailEntity
            {
                Id = messaggioId, TenantId = tenantA, CasellaId = Guid.NewGuid(), UidImap = 1,
                Mittente = "x@example.it", Oggetto = "Ciao", DataRicezione = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            var svc = new CasellaEmailService(db);
            Assert.Null(await svc.OttieniMessaggioAsync(tenantB, messaggioId));
            Assert.NotNull(await svc.OttieniMessaggioAsync(tenantA, messaggioId));
        }
    }

    [Fact]
    public async Task ElencoMessaggi_OrdinatoPerDataDiscendente()
    {
        var tenant = Guid.NewGuid();
        var casellaId = Guid.NewGuid();
        var vecchio = Guid.NewGuid();
        var recente = Guid.NewGuid();

        await using (var db = NuovoContesto())
        {
            db.MessaggiEmail.AddRange(
                new MessaggioEmailEntity
                {
                    Id = vecchio, TenantId = tenant, CasellaId = casellaId, UidImap = 1,
                    Mittente = "a@example.it", Oggetto = "Vecchio", DataRicezione = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                },
                new MessaggioEmailEntity
                {
                    Id = recente, TenantId = tenant, CasellaId = casellaId, UidImap = 2,
                    Mittente = "b@example.it", Oggetto = "Recente", DataRicezione = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                });
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            var elenco = await new CasellaEmailService(db).ElencoMessaggiAsync(tenant, new FiltriMessaggiEmail());
            Assert.Equal([recente, vecchio], elenco.Select(m => m.Id));
        }
    }

    // --- Proposta AI: mai applicata da sola ---

    [Fact]
    public async Task SalvaProposta_NonTocca_ICampiDefinitivi()
    {
        var tenant = Guid.NewGuid();
        var messaggioId = Guid.NewGuid();
        await using (var db = NuovoContesto())
        {
            db.MessaggiEmail.Add(new MessaggioEmailEntity
            {
                Id = messaggioId, TenantId = tenant, CasellaId = Guid.NewGuid(), UidImap = 1,
                Mittente = "x@example.it", Oggetto = "Urgente!", DataRicezione = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            var svc = new CasellaEmailService(db);
            await svc.SalvaPropostaAsync(tenant, messaggioId,
                new PropostaTriageEmail(true, 0.9m, false, 0.8m, "Ai"));

            var m = await svc.OttieniMessaggioAsync(tenant, messaggioId);
            Assert.Null(m!.Urgente);
            Assert.Null(m.Pubblicita);
            Assert.True(m.UrgenteProposto);
            Assert.False(m.PubblicitaProposto);
            Assert.Equal("Ai", m.FonteProposta);
        }
    }

    [Fact]
    public async Task ConfermaProposta_RendeDefinitivoEAzzeraLaProposta()
    {
        var tenant = Guid.NewGuid();
        var messaggioId = Guid.NewGuid();
        await using (var db = NuovoContesto())
        {
            db.MessaggiEmail.Add(new MessaggioEmailEntity
            {
                Id = messaggioId, TenantId = tenant, CasellaId = Guid.NewGuid(), UidImap = 1,
                Mittente = "x@example.it", Oggetto = "Urgente!", DataRicezione = DateTime.UtcNow,
                UrgenteProposto = true, ConfidenzaUrgente = 0.9m, FonteProposta = "Ai",
            });
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
            Assert.True(await new CasellaEmailService(db).ConfermaPropostaAsync(tenant, messaggioId, true, null));

        await using (var db = NuovoContesto())
        {
            var m = await db.MessaggiEmail.SingleAsync(x => x.Id == messaggioId);
            Assert.True(m.Urgente);
            Assert.Null(m.UrgenteProposto);
            Assert.Null(m.ConfidenzaUrgente);
            Assert.Null(m.FonteProposta);
        }
    }

    // --- Audit: nomi semantici, mai il valore ---

    [Fact]
    public async Task ModificaPasswordCasella_RegistraSoloNomeSemantico()
    {
        var tenant = Guid.NewGuid();
        var casellaId = Guid.NewGuid();
        await using (var db = NuovoContesto())
        {
            db.CaselleEmail.Add(new CasellaEmailEntity
            {
                Id = casellaId, TenantId = tenant, Etichetta = "Email studio", Tipo = "Email",
                Host = "imap.example.it", Porta = 993, Utente = "studio@example.it", Password = "vecchia",
            });
            await db.SaveChangesAsync();
        }

        using (AmbientContestoAudit.Imposta(tenant, Guid.NewGuid()))
        await using (var db = NuovoContesto())
        {
            var c = await db.CaselleEmail.SingleAsync(x => x.Id == casellaId);
            c.Password = "nuova-password-segreta";
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            var modifica = await db.AuditLog.SingleAsync(a => a.TenantId == tenant && a.Azione == "Modifica");
            Assert.Equal("CredenzialeCasellaPosta", modifica.CampiModificati);
            Assert.DoesNotContain("nuova-password-segreta", modifica.CampiModificati ?? "");
        }
    }

    // --- Euristica pubblicità + fusione con la proposta AI ---

    [Fact]
    public void Interpreta_EuristicaPubblicita_VinceSempreENonConsumaAi()
    {
        var proposta = CompletamentoTriageEmail.Interpreta(
            pubblicitaEuristica: true,
            propostaAi: null,
            sogliaConfidenza: 0.6m);

        Assert.True(proposta.Pubblicita);
        Assert.Equal(0.95m, proposta.ConfidenzaPubblicita);
        Assert.Equal("Euristica", proposta.Fonte);
        Assert.Null(proposta.Urgente);
    }

    [Fact]
    public void Interpreta_AiSopraSoglia_VieneApplicata()
    {
        var ai = new PropostaAi([
            new CampoProposto("urgente", "true", 0.85m),
            new CampoProposto("pubblicita", "false", 0.7m),
        ]);

        var proposta = CompletamentoTriageEmail.Interpreta(false, ai, 0.6m);

        Assert.True(proposta.Urgente);
        Assert.Equal(0.85m, proposta.ConfidenzaUrgente);
        Assert.False(proposta.Pubblicita);
        Assert.Equal("Ai", proposta.Fonte);
    }

    [Fact]
    public void Interpreta_AiSottoSoglia_VieneIgnorata()
    {
        var ai = new PropostaAi([new CampoProposto("urgente", "true", 0.4m)]);

        var proposta = CompletamentoTriageEmail.Interpreta(false, ai, 0.6m);

        Assert.Null(proposta.Urgente);
        Assert.Null(proposta.Fonte);
    }

    [Fact]
    public void Interpreta_ValoreAiNonBooleano_VieneIgnorato()
    {
        var ai = new PropostaAi([new CampoProposto("urgente", "forse", 0.9m)]);

        var proposta = CompletamentoTriageEmail.Interpreta(false, ai, 0.6m);

        Assert.Null(proposta.Urgente);
    }

    public void Dispose() => _connection.Dispose();
}
