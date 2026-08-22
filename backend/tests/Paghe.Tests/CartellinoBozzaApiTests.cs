using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Paghe.Api;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Paghe.Persistence.Sicurezza;

namespace Paghe.Tests;

public sealed class CartellinoBozzaApiTests : IDisposable
{
    private const string ChiaveJwt = "chiave-test-cartellino-bozza-almeno-32-byte-2026";
    private static readonly JsonSerializerOptions Json = CreaJson();

    private readonly string _database = Path.Combine(
        Path.GetTempPath(), $"paghe-cartellino-bozza-api-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;

    public CartellinoBozzaApiTests()
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
    public async Task PutBozza_ApplicaAuthRbacTenantEConcorrenzaOttimistica()
    {
        var scenario = await CreaScenarioAsync();
        string percorso = $"/api/rapporti/{scenario.RapportoId}/cartellino-bozza";
        var primaRichiesta = Richiesta(
            CalendarioCompleto(2025, 7, assenzeNonRetribuite: [10]), null, 0);

        using (var anonimo = _factory.CreateClient())
        using (var risposta = await anonimo.PutAsJsonAsync(percorso, primaRichiesta, Json))
            Assert.Equal(HttpStatusCode.Unauthorized, risposta.StatusCode);
        Assert.Equal(0, await ContaCartelliniAsync(scenario.TenantId));

        using (var solaLettura = await ClientAutenticatoAsync(scenario.SolaLettura))
        using (var risposta = await solaLettura.PutAsJsonAsync(percorso, primaRichiesta, Json))
            Assert.Equal(HttpStatusCode.Forbidden, risposta.StatusCode);
        Assert.Equal(0, await ContaCartelliniAsync(scenario.TenantId));

        using (var altroTenant = await ClientAutenticatoAsync(scenario.AdminAltroTenant))
        using (var risposta = await altroTenant.PutAsJsonAsync(percorso, primaRichiesta, Json))
            Assert.Equal(HttpStatusCode.NotFound, risposta.StatusCode);
        Assert.Equal(0, await ContaCartelliniAsync(scenario.TenantId));

        Guid id;
        using (var admin = await ClientAutenticatoAsync(scenario.Admin))
        {
            using (var creazione = await admin.PutAsJsonAsync(percorso, primaRichiesta, Json))
            {
                Assert.Equal(HttpStatusCode.OK, creazione.StatusCode);
                (id, var versione) = await LeggiIdVersioneAsync(creazione);
                Assert.Equal(1, versione);
            }

            var secondaRichiesta = Richiesta(
                CalendarioCompleto(2025, 7, assenzeNonRetribuite: [10, 11]), id, 1);
            using (var aggiornamento = await admin.PutAsJsonAsync(percorso, secondaRichiesta, Json))
            {
                Assert.Equal(HttpStatusCode.OK, aggiornamento.StatusCode);
                Assert.Equal((id, 2), await LeggiIdVersioneAsync(aggiornamento));
            }

            var obsoleta = Richiesta(
                CalendarioCompleto(2025, 7, assenzeNonRetribuite: [21]), id, 1);
            using var conflitto = await admin.PutAsJsonAsync(percorso, obsoleta, Json);
            Assert.Equal(HttpStatusCode.Conflict, conflitto.StatusCode);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
        var salvata = await db.Cartellini.AsNoTracking().SingleAsync(c =>
            c.Id == id && c.TenantId == scenario.TenantId);
        Assert.Equal(2, salvata.Versione);
        Assert.Equal(16m, salvata.OreAssenzaNonRetribuita);
        Assert.Equal(CausaleGiornata.Lavorata,
            salvata.Giornate.Single(g => g.Data.Day == 21).Causale);
    }

    [Fact]
    public async Task PutBozza_UsaIlRateLimitSensibili()
    {
        var scenario = await CreaScenarioAsync();
        string percorso = $"/api/rapporti/{scenario.RapportoId}/cartellino-bozza";
        using var admin = await ClientAutenticatoAsync(scenario.Admin);
        var nonValida = new SalvaCartellinoBozza(
            new Cartellino { Anno = 2101, Mese = 1 }, null, 0);

        for (int i = 0; i < 30; i++)
        {
            using var risposta = await admin.PutAsJsonAsync(percorso, nonValida, Json);
            Assert.Equal(HttpStatusCode.BadRequest, risposta.StatusCode);
        }

        using var limitata = await admin.PutAsJsonAsync(percorso, nonValida, Json);
        Assert.Equal(HttpStatusCode.TooManyRequests, limitata.StatusCode);
        Assert.Equal(0, await ContaCartelliniAsync(scenario.TenantId));
    }

    private async Task<Scenario> CreaScenarioAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
        var tenant = new TenantEntity
        {
            Id = Guid.NewGuid(), RagioneSociale = "Studio bozza cartellino", CreatoIl = DateTime.UtcNow,
        };
        var tenantAltro = new TenantEntity
        {
            Id = Guid.NewGuid(), RagioneSociale = "Altro studio bozza", CreatoIl = DateTime.UtcNow,
        };
        var admin = Utente(tenant.Id, RuoloPolicy.Admin);
        var solaLettura = Utente(tenant.Id, RuoloPolicy.SolaLettura);
        var adminAltro = Utente(tenantAltro.Id, RuoloPolicy.Admin);
        var azienda = new AziendaEntity
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, RagioneSociale = "Alfa Bozza Srl",
            Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
        };
        var dipendente = new DipendenteEntity
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id,
            CodiceFiscale = $"TST{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            Nome = "Mario", Cognome = "Rossi",
        };
        var rapporto = new RapportoLavoroEntity
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, AziendaId = azienda.Id,
            DipendenteId = dipendente.Id, CodiceCcnl = "COMMERCIO_TERZIARIO",
            Livello = "3", Qualifica = "Impiegato", DataAssunzione = new DateOnly(2024, 1, 1),
            OreSettimanali = 40m,
        };
        db.Tenants.AddRange(tenant, tenantAltro);
        db.Utenti.AddRange(admin, solaLettura, adminAltro);
        db.Aziende.Add(azienda);
        db.Dipendenti.Add(dipendente);
        db.Rapporti.Add(rapporto);
        await db.SaveChangesAsync();
        return new(tenant.Id, rapporto.Id, admin, solaLettura, adminAltro);
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
            var sessioni = scope.ServiceProvider.GetRequiredService<SessioniRefreshService>();
            famiglia = (await sessioni.CreaAsync(utente, "test-cartellino-bozza")).FamigliaId;
        }
        var token = new TokenService(config).Crea(utente, famiglia);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Token);
        return client;
    }

    private async Task<int> ContaCartelliniAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
        return await db.Cartellini.AsNoTracking().CountAsync(c => c.TenantId == tenantId);
    }

    private static async Task<(Guid Id, int Versione)> LeggiIdVersioneAsync(
        HttpResponseMessage risposta)
    {
        using var json = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        return (
            json.RootElement.GetProperty("id").GetGuid(),
            json.RootElement.GetProperty("versione").GetInt32());
    }

    private static SalvaCartellinoBozza Richiesta(
        IReadOnlyList<GiornataCartellino> giornate, Guid? id, int versione) => new(
        new Cartellino
        {
            Anno = 2025,
            Mese = 7,
            StatoAssegniFamiliari = StatoAssegniFamiliari.NonSpettanti,
            Giornate = giornate,
        }, id, versione);

    private static IReadOnlyList<GiornataCartellino> CalendarioCompleto(
        int anno, int mese, IReadOnlyCollection<int>? assenzeNonRetribuite = null)
    {
        assenzeNonRetribuite ??= [];
        return Enumerable.Range(1, DateTime.DaysInMonth(anno, mese)).Select(giorno =>
        {
            var data = new DateOnly(anno, mese, giorno);
            if (assenzeNonRetribuite.Contains(giorno))
                return new GiornataCartellino
                {
                    Data = data, Causale = CausaleGiornata.AssenzaNonRetribuita,
                    OreAssenzaNonRetribuita = 8m,
                };
            bool riposo = data.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            return new GiornataCartellino
            {
                Data = data,
                Causale = riposo ? CausaleGiornata.RiposoSettimanale : CausaleGiornata.Lavorata,
                OreOrdinarie = riposo ? 0m : 8m,
            };
        }).ToArray();
    }

    private static UtenteEntity Utente(Guid tenantId, string ruolo) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId,
        Email = $"cartellino-{ruolo}-{Guid.NewGuid():N}@test.local",
        PasswordHash = PasswordHasher.Hash("Password-cartellino-bozza-2026!"),
        Ruolo = ruolo, Attivo = true, CreatoIl = DateTime.UtcNow,
    };

    private static JsonSerializerOptions CreaJson()
    {
        var opzioni = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        opzioni.Converters.Add(new JsonStringEnumConverter());
        return opzioni;
    }

    private sealed record Scenario(
        Guid TenantId,
        Guid RapportoId,
        UtenteEntity Admin,
        UtenteEntity SolaLettura,
        UtenteEntity AdminAltroTenant);

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
