using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Paghe.Api;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Protezione degli endpoint /api/retention: anonimi respinti, ruoli non admin respinti,
/// l'admin dello studio vede il catalogo delle policy.
/// </summary>
public sealed class RetentionApiTests : IDisposable
{
    private const string ChiaveJwt = "chiave-test-retention-api-sicura-32-byte-2026";
    private readonly string _database = Path.Combine(
        Path.GetTempPath(), $"paghe-retention-api-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;

    public RetentionApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Jwt:Key", ChiaveJwt);
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={_database}");
            builder.UseSetting("Database:Provider", "Sqlite");
        });
    }

    [Fact]
    public async Task SenzaToken_RispondeUnauthorized()
    {
        using var client = _factory.CreateClient();
        using var risposta = await client.GetAsync("/api/retention");
        Assert.Equal(HttpStatusCode.Unauthorized, risposta.StatusCode);
    }

    [Theory]
    [InlineData("operatore")]
    [InlineData("sola_lettura")]
    public async Task RuoliNonAdmin_RispondeForbidden(string ruolo)
    {
        var utente = await CreaUtenteAsync(ruolo);
        using var client = await ClientAutenticatoAsync(utente);
        using var risposta = await client.GetAsync("/api/retention");
        Assert.Equal(HttpStatusCode.Forbidden, risposta.StatusCode);
    }

    [Fact]
    public async Task Admin_VedeIlCatalogoDellePolicy()
    {
        var admin = await CreaUtenteAsync("admin");
        using var client = await ClientAutenticatoAsync(admin);

        using var risposta = await client.GetAsync("/api/retention");
        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);

        using var json = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        var categorie = json.RootElement.EnumerateArray()
            .Select(p => p.GetProperty("categoria").GetString())
            .ToArray();
        Assert.Contains("RegistroAttivita", categorie);
        Assert.Contains("PromemoriaManualiChiusi", categorie);
    }

    private async Task<UtenteEntity> CreaUtenteAsync(string ruolo)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
        var tenant = new TenantEntity
        {
            Id = Guid.NewGuid(),
            RagioneSociale = $"Studio retention {Guid.NewGuid():N}",
            CreatoIl = DateTime.UtcNow,
        };
        var utente = new UtenteEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = $"retention-{Guid.NewGuid():N}@test.local",
            PasswordHash = PasswordHasher.Hash("Password-retention-api-2026"),
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
            var sessioni = scope.ServiceProvider
                .GetRequiredService<Paghe.Persistence.Sicurezza.SessioniRefreshService>();
            famiglia = (await sessioni.CreaAsync(utente, "test")).FamigliaId;
        }
        var token = new TokenService(config).Crea(utente, famiglia);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Token);
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
