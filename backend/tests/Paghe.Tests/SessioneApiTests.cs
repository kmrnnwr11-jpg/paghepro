using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Paghe.Api;
using Paghe.Persistence;

namespace Paghe.Tests;

public sealed class SessioneApiTests : IDisposable
{
    private const string ChiaveJwt = "chiave-test-sessioni-api-sicura-almeno-32-byte-2026";
    private readonly string _database = Path.Combine(
        Path.GetTempPath(), $"paghe-sessioni-api-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;

    public SessioneApiTests()
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
    public async Task Web_RefreshSoloCookieHttpOnly_LogoutRevocaSubitoAccesso()
    {
        using var client = NuovoClientWeb();
        using var registrazione = await client.PostAsJsonAsync("/api/auth/registra", new
        {
            ragioneSociale = "Studio web",
            email = $"web-{Guid.NewGuid():N}@test.local",
            password = "Password-web-test-2026!",
        });
        Assert.Equal(HttpStatusCode.OK, registrazione.StatusCode);
        Assert.Contains("no-store", registrazione.Headers.CacheControl?.ToString() ?? "");
        var json = await registrazione.Content.ReadAsStringAsync();
        Assert.DoesNotContain("refreshToken", json, StringComparison.OrdinalIgnoreCase);
        var cookie = Assert.Single(registrazione.Headers.GetValues("Set-Cookie"));
        Assert.Contains("paghepro_refresh=", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", cookie, StringComparison.OrdinalIgnoreCase);

        var accesso = JsonSerializer.Deserialize<AccessoResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accesso.Token);
        Assert.Equal(TokenService.VersioneProtocollo, jwt.Claims.Single(x => x.Type == "ver").Value);
        Assert.True(Guid.TryParse(jwt.Claims.Single(x => x.Type == "sid").Value, out _));
        Assert.False(string.IsNullOrWhiteSpace(jwt.Id));
        Assert.Equal(TokenService.EmittentePredefinito, jwt.Issuer);
        Assert.Contains(TokenService.DestinatarioPredefinito, jwt.Audiences);
        Assert.InRange(jwt.ValidTo - jwt.ValidFrom, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(16));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accesso.Token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/aziende")).StatusCode);

        using var rinnovo = await client.PostAsync("/api/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, rinnovo.StatusCode);
        Assert.DoesNotContain("refreshToken", await rinnovo.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);

        using var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/aziende")).StatusCode);
    }

    [Fact]
    public async Task Mobile_RefreshNelBody_EPersistenzaContieneSoloHash()
    {
        using var client = _factory.CreateClient();
        using var registrazione = await client.PostAsJsonAsync("/api/auth/mobile/registra", new
        {
            ragioneSociale = "Studio mobile",
            email = $"mobile-{Guid.NewGuid():N}@test.local",
            password = "Password-mobile-test-2026!",
        });
        Assert.Equal(HttpStatusCode.OK, registrazione.StatusCode);
        var accesso = await registrazione.Content.ReadFromJsonAsync<AccessoMobileResponse>();
        Assert.NotNull(accesso);
        Assert.NotEmpty(accesso!.RefreshToken);
        Assert.False(registrazione.Headers.Contains("Set-Cookie"));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var righe = await scope.ServiceProvider.GetRequiredService<PagheDbContext>()
                .SessioniRefresh.AsNoTracking().ToListAsync();
            Assert.NotEmpty(righe);
            Assert.DoesNotContain(righe, x => x.HashSegreto.Contains(accesso.RefreshToken));
        }

        using var rinnovo = await client.PostAsJsonAsync("/api/auth/mobile/refresh", new
        {
            refreshToken = accesso.RefreshToken,
        });
        Assert.Equal(HttpStatusCode.OK, rinnovo.StatusCode);
        var ruotata = await rinnovo.Content.ReadFromJsonAsync<AccessoMobileResponse>();
        Assert.NotEqual(accesso.RefreshToken, ruotata!.RefreshToken);
    }

    [Fact]
    public async Task Web_SenzaHeaderAntiCsrf_ECorsConCredenzialiSonoFailClosed()
    {
        using var client = _factory.CreateClient();
        using var negata = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "nessuno@test.local",
            password = "non-importa",
        });
        Assert.Equal(HttpStatusCode.BadRequest, negata.StatusCode);

        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/auth/refresh");
        preflight.Headers.Add("Origin", "http://localhost:5173");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "X-PaghePro-Client");
        using var cors = await client.SendAsync(preflight);
        Assert.Equal(HttpStatusCode.NoContent, cors.StatusCode);
        Assert.Equal("true", Assert.Single(cors.Headers.GetValues("Access-Control-Allow-Credentials")));
    }

    [Fact]
    public async Task JwtV1SenzaSessione_VieneRifiutato()
    {
        using var client = _factory.CreateClient();
        var chiave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ChiaveJwt));
        var token = new JwtSecurityToken(
            issuer: TokenService.EmittentePredefinito,
            audience: TokenService.DestinatarioPredefinito,
            claims:
            [
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("tenant_id", Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "admin"),
            ],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(chiave, SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtSecurityTokenHandler().WriteToken(token));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/aziende")).StatusCode);
    }

    private HttpClient NuovoClientWeb()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        client.DefaultRequestHeaders.Add("X-PaghePro-Client", "web");
        return client;
    }

    public void Dispose()
    {
        _factory.Dispose();
        foreach (var suffisso in new[] { "", "-shm", "-wal" })
        {
            try { File.Delete(_database + suffisso); }
            catch (IOException) { }
        }
    }
}
