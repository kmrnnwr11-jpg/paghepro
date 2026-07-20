using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Paghe.Api;
using Paghe.Persistence;
using Paghe.Persistence.Entities;

namespace Paghe.Tests;

public sealed class AuditApiTests : IDisposable
{
    private const string ChiaveJwt = "chiave-test-audit-api-sicura-almeno-32-byte-2026";
    private readonly string _database = Path.Combine(
        Path.GetTempPath(), $"paghe-audit-api-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;

    public AuditApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Jwt:Key", ChiaveJwt);
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={_database}");
            builder.UseSetting("Database:Provider", "Sqlite");
        });
    }

    [Theory]
    [InlineData("/api/f24/da-paghe")]
    [InlineData("/api/fatture/xml")]
    [InlineData("/api/bilancio/riclassifica")]
    [InlineData("/api/contabilita/iva/liquidazione")]
    public async Task EndpointConDatiProfessionali_SenzaToken_RispondeUnauthorized(string endpoint)
    {
        using var client = _factory.CreateClient();
        using var risposta = await client.PostAsJsonAsync(endpoint, new { });
        Assert.Equal(HttpStatusCode.Unauthorized, risposta.StatusCode);
    }

    [Fact]
    public async Task LetturaAutenticata_AppareNelRegistroAdmin_SenzaRigheDiAltriTenant()
    {
        var utenteA = await CreaUtenteAsync("admin", "Password-audit-A-2026");
        var utenteB = await CreaUtenteAsync("admin", "Password-audit-B-2026");
        using var clientA = await ClientAutenticatoAsync(utenteA);
        using var clientB = await ClientAutenticatoAsync(utenteB);

        using (var letturaA = await clientA.GetAsync("/api/aziende"))
            Assert.Equal(HttpStatusCode.OK, letturaA.StatusCode);
        using (var letturaB = await clientB.GetAsync("/api/aziende"))
            Assert.Equal(HttpStatusCode.OK, letturaB.StatusCode);

        using var risposta = await clientA.GetAsync("/api/audit?categoria=Accesso&dimensione=200");
        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        using var json = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        var voci = json.RootElement.GetProperty("voci").EnumerateArray().ToArray();
        Assert.Contains(voci, v =>
            v.GetProperty("utente").GetString() == utenteA.Email
            && v.GetProperty("azione").GetString() == "Lettura"
            && v.GetProperty("percorsoTemplate").GetString() == "/api/aziende"
            && v.GetProperty("quandoUtc").GetString()!.EndsWith('Z'));
        Assert.DoesNotContain(voci, v => v.GetProperty("utenteId").GetString() == utenteB.Id.ToString());
    }

    [Fact]
    public async Task Registro_NegaOperatore_AncheSeLaRichiestaEUnaGet()
    {
        var operatore = await CreaUtenteAsync("operatore", "Password-operatore-2026");
        using var client = await ClientAutenticatoAsync(operatore);

        using var risposta = await client.GetAsync("/api/audit");

        Assert.Equal(HttpStatusCode.Forbidden, risposta.StatusCode);
    }

    [Fact]
    public async Task Registro_ConfrontaIlPeriodoPerIstanteUtc_AncheConOffsetDiversi()
    {
        var admin = await CreaUtenteAsync("admin", "Password-periodo-utc-2026");
        using var client = await ClientAutenticatoAsync(admin);
        var da = Uri.EscapeDataString("2026-01-15T10:00:00+02:00"); // 08:00Z
        var a = Uri.EscapeDataString("2026-01-15T08:30:00Z");

        using var risposta = await client.GetAsync($"/api/audit?daUtc={da}&aUtc={a}");

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
    }

    [Fact]
    public async Task Registro_RifiutaPeriodoInvertitoDopoLaNormalizzazioneUtc()
    {
        var admin = await CreaUtenteAsync("admin", "Password-periodo-invertito-2026");
        using var client = await ClientAutenticatoAsync(admin);
        var da = Uri.EscapeDataString("2026-01-15T10:00:00+01:00"); // 09:00Z
        var a = Uri.EscapeDataString("2026-01-15T08:30:00Z");

        using var risposta = await client.GetAsync($"/api/audit?daUtc={da}&aUtc={a}");

        Assert.Equal(HttpStatusCode.BadRequest, risposta.StatusCode);
    }

    [Fact]
    public async Task LoginRiuscito_VieneRegistratoSenzaSalvareCredenziali()
    {
        const string password = "Password-login-audit-2026";
        var utente = await CreaUtenteAsync("admin", password);
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-PaghePro-Client", "web");

        using var risposta = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = utente.Email,
            password,
        });
        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var evento = await scope.ServiceProvider.GetRequiredService<PagheDbContext>().AuditLog
            .AsNoTracking()
            .SingleAsync(a =>
                a.TenantId == utente.TenantId
                && a.UtenteId == utente.Id
                && a.Azione == "Autenticazione");
        Assert.Equal("Riuscito", evento.Esito);
        var contenuto = string.Join('|', evento.Entita, evento.ChiaveEntita,
            evento.CampiModificati, evento.PercorsoTemplate);
        Assert.DoesNotContain(password, contenuto);
        Assert.DoesNotContain(utente.Email, contenuto);
    }

    [Fact]
    public async Task PasswordErrataDiAccountNoto_VieneAuditataMaLaRispostaRestaNeutra()
    {
        const string passwordCorretta = "Password-corretta-audit-2026";
        const string passwordErrata = "Password-errata-audit-2026";
        var utente = await CreaUtenteAsync("admin", passwordCorretta);
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-PaghePro-Client", "web");

        using var risposta = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = utente.Email,
            password = passwordErrata,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, risposta.StatusCode);
        Assert.Contains("Email o password non corretti", await risposta.Content.ReadAsStringAsync());

        await using var scope = _factory.Services.CreateAsyncScope();
        var evento = await scope.ServiceProvider.GetRequiredService<PagheDbContext>().AuditLog
            .AsNoTracking()
            .SingleAsync(a =>
                a.TenantId == utente.TenantId
                && a.UtenteId == utente.Id
                && a.Azione == "Autenticazione");
        Assert.Equal("Negato", evento.Esito);
        var contenuto = string.Join('|', evento.Entita, evento.ChiaveEntita,
            evento.CampiModificati, evento.PercorsoTemplate);
        Assert.DoesNotContain(passwordErrata, contenuto);
        Assert.DoesNotContain(utente.Email, contenuto);
    }

    [Fact]
    public async Task Superadmin_SospensioneStudio_ProduceEventoPiattaformaConsultabile()
    {
        var superadmin = await CreaUtenteAsync("superadmin", "Password-superadmin-2026");
        var utenteStudio = await CreaUtenteAsync("admin", "Password-studio-2026");
        using var client = await ClientAutenticatoAsync(superadmin);

        using (var sospensione = await client.PostAsync(
                   $"/api/admin/studi/{utenteStudio.TenantId}/sospendi", null))
            Assert.Equal(HttpStatusCode.OK, sospensione.StatusCode);

        using var risposta = await client.GetAsync("/api/audit?dimensione=200");
        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        using var json = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        var voci = json.RootElement.GetProperty("voci").EnumerateArray().ToArray();
        Assert.Contains(voci, v =>
            v.GetProperty("categoria").GetString() == "ModificaDati"
            && v.GetProperty("entita").GetString() == "Tenants"
            && v.GetProperty("chiaveEntita").GetString() == utenteStudio.TenantId.ToString()
            && (v.GetProperty("campiModificati").GetString() ?? "").Contains("Sospeso"));
        Assert.Contains(voci, v =>
            v.GetProperty("categoria").GetString() == "Accesso"
            && v.GetProperty("percorsoTemplate").GetString() == "/api/admin/studi/{id:guid}/sospendi");
    }

    private async Task<UtenteEntity> CreaUtenteAsync(string ruolo, string password)
    {
        // Avvia l'host e usa il suo database reale di test.
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
        var tenant = new TenantEntity
        {
            Id = Guid.NewGuid(),
            RagioneSociale = $"Studio test {Guid.NewGuid():N}",
            CreatoIl = DateTime.UtcNow,
        };
        var utente = new UtenteEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = $"audit-{Guid.NewGuid():N}@test.local",
            PasswordHash = PasswordHasher.Hash(password),
            Ruolo = ruolo,
            Attivo = true,
            CreatoIl = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        db.Utenti.Add(utente);
        await db.SaveChangesAsync();
        return utente;
    }

    private async Task<HttpClient> ClientAutenticatoAsync(UtenteEntity utente)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = ChiaveJwt })
            .Build();
        Guid famiglia;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sessioni = scope.ServiceProvider.GetRequiredService<Paghe.Persistence.Sicurezza.SessioniRefreshService>();
            famiglia = (await sessioni.CreaAsync(utente, "test")).FamigliaId;
        }
        var token = new TokenService(config).Crea(utente, famiglia);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return client;
    }

    public void Dispose()
    {
        _factory.Dispose();
        foreach (var suffisso in new[] { "", "-shm", "-wal" })
        {
            try { File.Delete(_database + suffisso); }
            catch (IOException) { /* best effort: il processo di test sta terminando */ }
        }
    }
}
