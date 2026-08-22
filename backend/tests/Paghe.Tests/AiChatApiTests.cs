using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Paghe.Api;
using Paghe.Persistence;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Confine HTTP della chat Aurora: la rotta legacy di azione resta chiusa e la chat
/// esplicativa non offre né esegue strumenti, anche se il provider ne inventa uno.
/// </summary>
public sealed class AiChatApiTests : IDisposable
{
    private const string ChiaveJwt = "chiave-test-chat-api-sicura-almeno-32-byte-2026";
    private readonly string _database = Path.Combine(
        Path.GetTempPath(), $"paghe-chat-api-{Guid.NewGuid():N}.db");
    private readonly HandlerProviderFinto _providerFinto = new();
    private readonly WebApplicationFactory<Program> _factory;

    public AiChatApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Jwt:Key", ChiaveJwt);
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={_database}");
            builder.UseSetting("Database:Provider", "Sqlite");

            // Profilo cloud esclusivamente di test: il trasporto sottostante è sostituito dal
            // handler in memoria, quindi nessuna richiesta può lasciare il processo di test.
            builder.UseSetting("Ai:Provider", "Compatibile");
            builder.UseSetting("Ai:BaseUrl", "https://provider.test/v1");
            builder.UseSetting("Ai:ApiKey", "chiave-provider-finta");
            builder.UseSetting("Ai:ModelloChat", "modello-chat-finto");
            builder.UseSetting("Ai:SoloLocale", "false");
            builder.UseSetting("Ai:ProviderCloudApprovato", "true");
            builder.UseSetting("Ai:HostCloudConsentiti:0", "provider.test");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<AiChatClient>();
                services.AddHttpClient<AiChatClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => _providerFinto);
            });
        });
    }

    [Fact]
    public async Task AzioneLegacy_Autenticata_Risponde409_ENonModificaDominio()
    {
        using var client = await NuovoClientAutenticatoAsync();
        var tenant = _tenantCorrente;
        var prima = await FotografaDominioAsync(tenant);
        var auditPrima = await ContaAuditAccessiAsync(tenant);

        using var risposta = await client.PostAsJsonAsync("/api/ai/chat/azione", new
        {
            strumento = "registra_azienda",
            argomentiJson =
                """{"ragioneSociale":"Non Deve Nascere Srl","partitaIva":"12345678901"}""",
            storia = Array.Empty<object>(),
            aziendaId = (Guid?)null,
        });

        Assert.Equal(HttpStatusCode.Conflict, risposta.StatusCode);
        Assert.Contains("Azioni Aurora sospese", await risposta.Content.ReadAsStringAsync());
        Assert.Equal(0, _providerFinto.Chiamate);
        Assert.Equal(prima, await FotografaDominioAsync(tenant));

        // L'unica scrittura ammessa è il registro attività del rifiuto HTTP; body e valori
        // della falsa azienda non vengono conservati.
        Assert.Equal(auditPrima + 1, await ContaAuditAccessiAsync(tenant));
        await using var scope = _factory.Services.CreateAsyncScope();
        var audit = await scope.ServiceProvider.GetRequiredService<PagheDbContext>()
            .AuditLog.AsNoTracking()
            .Where(a => a.TenantId == tenant
                && a.PercorsoTemplate == "/api/ai/chat/azione")
            .OrderByDescending(a => a.QuandoUtc)
            .FirstAsync();
        Assert.Equal(409, audit.StatusCode);
        Assert.Equal("Rifiutato", audit.Esito);
        Assert.DoesNotContain("Non Deve Nascere", audit.ChiaveEntita);
    }

    [Fact]
    public async Task Chat_ToolCallSpontanea_NonInviaTools_ENonEsegueAzione()
    {
        using var client = await NuovoClientAutenticatoAsync();
        var tenant = _tenantCorrente;
        var prima = await FotografaDominioAsync(tenant);

        using var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Spiegami come preparare il mese paghe.",
            storia = Array.Empty<object>(),
            aziendaId = (Guid?)null,
            contestoSelezionato = (object?)null,
            sezioneUi = "Cedolini",
        });

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        Assert.Equal(1, _providerFinto.Chiamate);
        Assert.Equal("https://provider.test/v1/chat/completions", _providerFinto.UltimoIndirizzo);

        using (var inviato = JsonDocument.Parse(Assert.IsType<string>(_providerFinto.UltimoCorpo)))
        {
            Assert.False(inviato.RootElement.TryGetProperty("tools", out _));
            Assert.Equal("none", inviato.RootElement.GetProperty("tool_choice").GetString());
        }

        using (var ricevuto = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync()))
        {
            Assert.Equal(JsonValueKind.Null, ricevuto.RootElement.GetProperty("azione").ValueKind);
            Assert.Contains(
                "strumenti di Aurora sono sospesi",
                ricevuto.RootElement.GetProperty("testo").GetString(),
                StringComparison.OrdinalIgnoreCase);
        }

        var dopo = await FotografaDominioAsync(tenant);
        Assert.Equal(prima with { ConsumiAi = prima.ConsumiAi + 1 }, dopo);

        await using var scope = _factory.Services.CreateAsyncScope();
        Assert.Null(scope.ServiceProvider.GetService<AiChatStrumenti>());
    }

    private Guid _tenantCorrente;

    private async Task<HttpClient> NuovoClientAutenticatoAsync()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        client.DefaultRequestHeaders.Add("X-PaghePro-Client", "web");

        using var registrazione = await client.PostAsJsonAsync("/api/auth/registra", new
        {
            ragioneSociale = "Studio test Aurora",
            email = $"aurora-{Guid.NewGuid():N}@test.local",
            password = "Password-chat-api-test-2026!",
        });
        Assert.Equal(HttpStatusCode.OK, registrazione.StatusCode);
        var accesso = await registrazione.Content.ReadFromJsonAsync<AccessoResponse>();
        Assert.NotNull(accesso);
        _tenantCorrente = accesso!.TenantId;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accesso.Token);
        return client;
    }

    private async Task<StatoDominio> FotografaDominioAsync(Guid tenant)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
        return new StatoDominio(
            await db.Aziende.AsNoTracking().CountAsync(a => a.TenantId == tenant),
            await db.Dipendenti.AsNoTracking().CountAsync(d => d.TenantId == tenant),
            await db.Rapporti.AsNoTracking().CountAsync(r => r.TenantId == tenant),
            await db.Promemoria.AsNoTracking().CountAsync(p => p.TenantId == tenant),
            await db.AiScopeGrants.AsNoTracking().CountAsync(g => g.TenantId == tenant),
            await db.AiAudit.AsNoTracking().CountAsync(a => a.TenantId == tenant),
            await db.ConsumiAi.AsNoTracking().CountAsync(c => c.TenantId == tenant));
    }

    private async Task<int> ContaAuditAccessiAsync(Guid tenant)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PagheDbContext>()
            .AuditLog.AsNoTracking()
            .CountAsync(a => a.TenantId == tenant && a.Categoria == "Accesso");
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

    private sealed record StatoDominio(
        int Aziende,
        int Dipendenti,
        int Rapporti,
        int Promemoria,
        int AiScopeGrants,
        int AiAudit,
        int ConsumiAi);

    private sealed class HandlerProviderFinto : HttpMessageHandler
    {
        private int _chiamate;

        public int Chiamate => Volatile.Read(ref _chiamate);
        public string? UltimoCorpo { get; private set; }
        public string? UltimoIndirizzo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _chiamate);
            UltimoIndirizzo = request.RequestUri?.ToString();
            UltimoCorpo = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            const string toolCallSpontanea = """
                {
                  "choices": [{
                    "message": {
                      "role": "assistant",
                      "content": null,
                      "tool_calls": [{
                        "id": "call_ostile",
                        "type": "function",
                        "function": {
                          "name": "registra_azienda",
                          "arguments": "{\"ragioneSociale\":\"Non Deve Nascere Srl\"}"
                        }
                      }]
                    }
                  }]
                }
                """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(toolCallSpontanea, Encoding.UTF8, "application/json"),
            };
        }
    }
}
