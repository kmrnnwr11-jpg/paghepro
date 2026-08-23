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
using Paghe.Persistence.Entities;
using Paghe.Persistence.Gdpr;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Confine HTTP della chat Aurora: la rotta legacy di azione resta chiusa e nessun provider
/// viene chiamato senza un mandato Governor valido.
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
    public async Task Chat_SenzaLease_NonChiamaProviderENonConsumaQuota()
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
        Assert.Equal(0, _providerFinto.Chiamate);
        Assert.Null(_providerFinto.UltimoIndirizzo);
        Assert.Null(_providerFinto.UltimoCorpo);

        using (var ricevuto = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync()))
        {
            Assert.Equal(JsonValueKind.Null, ricevuto.RootElement.GetProperty("azione").ValueKind);
            Assert.Contains(
                "avviare il mandato",
                ricevuto.RootElement.GetProperty("testo").GetString(),
                StringComparison.OrdinalIgnoreCase);
        }

        var dopo = await FotografaDominioAsync(tenant);
        Assert.Equal(prima, dopo);

        await using var scope = _factory.Services.CreateAsyncScope();
        Assert.Null(scope.ServiceProvider.GetService<AiChatStrumenti>());
    }

    [Fact]
    public async Task Conferma_ConTokenNonFirmatoDalServer_RespintaSenzaEffetti()
    {
        using var client = await NuovoClientAutenticatoAsync();
        var tenant = _tenantCorrente;
        var prima = await FotografaDominioAsync(tenant);

        using var risposta = await client.PostAsJsonAsync("/api/ai/chat/conferma", new
        {
            token = "token-che-non-deve-essere-letto",
        });

        // Il protocollo A3 accetta soltanto token firmati dal server: qualunque altro
        // valore muore prima di toccare provider, proposta o dominio.
        Assert.Equal(HttpStatusCode.BadRequest, risposta.StatusCode);
        Assert.Contains("alterato", await risposta.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, _providerFinto.Chiamate);
        Assert.Equal(prima, await FotografaDominioAsync(tenant));
    }

    [Fact]
    public async Task Chat_ConLeaseValida_RaggiungeRuntimeEOffreSoloToolDelMandato()
    {
        using var client = await NuovoClientAutenticatoAsync();
        var tenant = _tenantCorrente;
        var aziendaId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            using var _ = AmbientContestoAudit.Imposta(tenant, Guid.NewGuid());
            var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
            db.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId,
                TenantId = tenant,
                RagioneSociale = "Azienda test runtime HTTP",
            });
            await db.SaveChangesAsync();
        }

        using var emissione = await client.PostAsJsonAsync("/api/ai/governor/grants", new
        {
            purposeCode = "PREPARAZIONE_MESE_PAGHE",
            modalita = "Guidata",
            scopeLevel = "S2",
            aziendaId,
            periodo = "2026-08",
            risorse = new[] { "Cedolini" },
            operazioni = new[] { "read" },
            durataMinuti = 30,
            maxLetture = 10,
        });
        Assert.Equal(HttpStatusCode.OK, emissione.StatusCode);
        using var grant = JsonDocument.Parse(await emissione.Content.ReadAsStringAsync());
        var lease = grant.RootElement.GetProperty("lease").GetString();
        Assert.False(string.IsNullOrWhiteSpace(lease));

        using var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Controlla lo stato del mese.",
            storia = Array.Empty<object>(),
            aziendaId,
            contestoSelezionato = (object?)null,
            sezioneUi = "Cedolini",
            lease,
        });

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        Assert.Equal(AuroraAgentRuntime.MassimoGiri, _providerFinto.Chiamate);
        Assert.NotNull(_providerFinto.UltimoCorpo);
        using (var inviato = JsonDocument.Parse(_providerFinto.UltimoCorpo!))
        {
            // Con la sola risorsa Cedolini in lettura il mandato offre esattamente i due
            // strumenti compatibili: lo stato del mese e la sintesi UniEmens (bozza).
            var nomi = inviato.RootElement.GetProperty("tools").EnumerateArray()
                .Select(t => t.GetProperty("function").GetProperty("name").GetString())
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(["sintesi_uniemens", "stato_cedolini_mese"], nomi);
        }

        await using var verifica = _factory.Services.CreateAsyncScope();
        var dbVerifica = verifica.ServiceProvider.GetRequiredService<PagheDbContext>();
        Assert.Equal(AuroraAgentRuntime.MassimoGiri,
            await dbVerifica.ConsumiAi.AsNoTracking()
                .Where(c => c.TenantId == tenant)
                .Select(c => c.Chiamate)
                .SingleAsync());
        Assert.Empty(await dbVerifica.Dipendenti.AsNoTracking()
            .Where(d => d.TenantId == tenant).ToListAsync());
    }

    [Fact]
    public async Task LottoDieci_PoiChat_UsanoLoStessoBudgetServerSenzaEsaurirlo()
    {
        using var client = await NuovoClientAutenticatoAsync();
        var tenant = _tenantCorrente;
        var aziendaId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            using var _ = AmbientContestoAudit.Imposta(tenant, Guid.NewGuid());
            var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
            db.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId,
                TenantId = tenant,
                RagioneSociale = "Azienda con lotto completo",
            });
            for (var indice = 0; indice < 10; indice++)
            {
                var dipendenteId = Guid.NewGuid();
                db.Dipendenti.Add(new DipendenteEntity
                {
                    Id = dipendenteId,
                    TenantId = tenant,
                    CodiceFiscale = $"TEST{indice:000000000000}",
                    Nome = $"Nome {indice:00}",
                    Cognome = "Test",
                });
                db.Rapporti.Add(new RapportoLavoroEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant,
                    AziendaId = aziendaId,
                    DipendenteId = dipendenteId,
                    CodiceCcnl = "TEST",
                    Livello = "1",
                    DataAssunzione = new DateOnly(2025, 1, 1),
                });
            }
            await db.SaveChangesAsync();
        }

        // maxLetture è volutamente omesso: il server applica e comunica il budget
        // effettivo della policy, mentre il broker mantiene il lotto di dieci per query.
        using var emissione = await client.PostAsJsonAsync("/api/ai/governor/grants", new
        {
            purposeCode = "PREPARAZIONE_MESE_PAGHE",
            modalita = "Guidata",
            scopeLevel = "S2",
            aziendaId,
            periodo = "2026-08",
            risorse = new[] { "Cedolini" },
            operazioni = new[] { "read" },
            durataMinuti = 30,
        });
        Assert.Equal(HttpStatusCode.OK, emissione.StatusCode);
        using var grant = JsonDocument.Parse(await emissione.Content.ReadAsStringAsync());
        var lease = grant.RootElement.GetProperty("lease").GetString();
        var grantId = grant.RootElement.GetProperty("manifest").GetProperty("grantId").GetGuid();
        Assert.Equal(250,
            grant.RootElement.GetProperty("manifest").GetProperty("maxLetture").GetInt32());

        using var controllo = await client.PostAsJsonAsync("/api/ai/governor/query", new
        {
            lease,
            risorsa = "Cedolini",
        });
        Assert.Equal(HttpStatusCode.OK, controllo.StatusCode);
        using (var esito = JsonDocument.Parse(await controllo.Content.ReadAsStringAsync()))
        {
            Assert.Equal(10, esito.RootElement.GetProperty("righe").GetArrayLength());
            Assert.Equal(240, esito.RootElement.GetProperty("lettureRimanenti").GetInt32());
        }

        _providerFinto.RispondeConToolCedolini = true;
        using var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Spiegami lo stato del lotto appena controllato.",
            storia = Array.Empty<object>(),
            aziendaId,
            contestoSelezionato = (object?)null,
            sezioneUi = "Cedolini",
            lease,
        });
        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        Assert.Equal(2, _providerFinto.Chiamate);

        using var trasparenza = await client.GetAsync($"/api/ai/governor/grants/{grantId}");
        Assert.Equal(HttpStatusCode.OK, trasparenza.StatusCode);
        using var cosaVede = JsonDocument.Parse(await trasparenza.Content.ReadAsStringAsync());
        var manifest = cosaVede.RootElement.GetProperty("manifest");
        Assert.Equal(250, manifest.GetProperty("maxLetture").GetInt32());
        Assert.Equal(20, manifest.GetProperty("lettureUsate").GetInt32());
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
            await db.AiProposte.AsNoTracking().CountAsync(p => p.TenantId == tenant),
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
        int AiProposte,
        int ConsumiAi);

    private sealed class HandlerProviderFinto : HttpMessageHandler
    {
        private int _chiamate;

        public int Chiamate => Volatile.Read(ref _chiamate);
        public bool RispondeConToolCedolini { get; set; }
        public string? UltimoCorpo { get; private set; }
        public string? UltimoIndirizzo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var numeroChiamata = Interlocked.Increment(ref _chiamate);
            UltimoIndirizzo = request.RequestUri?.ToString();
            UltimoCorpo = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (RispondeConToolCedolini)
            {
                var corpo = numeroChiamata == 1
                    ? """
                        {
                          "choices": [{
                            "message": {
                              "role": "assistant",
                              "content": null,
                              "tool_calls": [{
                                "id": "call_stato_cedolini",
                                "type": "function",
                                "function": {
                                  "name": "stato_cedolini_mese",
                                  "arguments": "{}"
                                }
                              }]
                            }
                          }]
                        }
                        """
                    : """
                        {
                          "choices": [{
                            "message": {
                              "role": "assistant",
                              "content": "Il lotto di dieci cedolini è stato letto nel mandato corrente."
                            }
                          }]
                        }
                        """;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
                };
            }

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
