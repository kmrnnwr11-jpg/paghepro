using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Paghe.Api;
using Paghe.Persistence.Entities;

namespace Paghe.Tests;

public sealed class SicurezzaFirmaApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ChiaveJwt = "chiave-test-firma-sicura-almeno-32-byte-2026";
    private readonly WebApplicationFactory<Program> _factory;

    public SicurezzaFirmaApiTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Jwt:Key", ChiaveJwt);
            builder.UseSetting("ConnectionStrings:Default", "Data Source=firma-api-tests.db");
        });

    [Theory]
    [InlineData("/api/fatture/firma")]
    [InlineData("/api/firma/cades")]
    [InlineData("/api/firma/verifica")]
    public async Task EndpointFirma_SenzaToken_RispondeUnauthorized(string endpoint)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var risposta = endpoint == "/api/fatture/firma"
            ? await client.PostAsJsonAsync(endpoint, new { })
            : await client.PostAsync(endpoint, new ByteArrayContent([]));

        Assert.Equal(HttpStatusCode.Unauthorized, risposta.StatusCode);
    }

    [Fact]
    public async Task UploadFirma_ConDocumentoOltreLimite_VieneRifiutato()
    {
        using var client = ClientAutenticato();
        using var multipart = new MultipartFormDataContent();
        multipart.Add(
            new ByteArrayContent(new byte[SicurezzaUploadFirma.DimensioneMassimaDocumento + 1]),
            "file", "troppo-grande.xml");

        using var risposta = await client.PostAsync("/api/firma/cades", multipart);

        Assert.True(risposta.StatusCode is HttpStatusCode.BadRequest
            or HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task UploadFirma_ConPfxInvalido_NonEsponeEccezioneInterna()
    {
        using var client = ClientAutenticato();
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent("documento"u8.ToArray()), "file", "documento.txt");
        multipart.Add(new ByteArrayContent(new byte[] { 0x30, 0x02, 0x01, 0x00 }), "pfx", "falso.pfx");
        multipart.Add(new StringContent("segreto-test"), "password");

        using var risposta = await client.PostAsync("/api/firma/cades", multipart);
        string corpo = await risposta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, risposta.StatusCode);
        Assert.Contains("Firma non riuscita. Verificare certificato", corpo);
        Assert.DoesNotContain("System.", corpo);
        Assert.DoesNotContain(" at ", corpo);
        Assert.DoesNotContain("segreto-test", corpo);
    }

    [Fact]
    public async Task VerificaFirma_ConXmlNonFirmato_RestituisceErroreNeutro()
    {
        using var client = ClientAutenticato();
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent("<fattura/>"u8.ToArray()), "file", "non-firmato.xml");

        using var risposta = await client.PostAsync("/api/firma/verifica", multipart);
        string corpo = await risposta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        Assert.Contains("\"valido\":false", corpo);
        Assert.DoesNotContain("System.", corpo);
        Assert.DoesNotContain(" at ", corpo);
    }

    [Fact]
    public async Task FirmaFattura_SuperataQuota_RispondeTooManyRequests()
    {
        using var client = ClientAutenticato();
        var fatturaSenzaRighe = new
        {
            numero = "TEST-1",
            data = "2026-07-15",
            cedente = new { denominazione = "Cedente", nazione = "IT", partitaIva = "12345678901" },
            cessionario = new { denominazione = "Cliente", nazione = "IT", codiceFiscale = "RSSMRA80A01H501U" },
            righe = Array.Empty<object>(),
        };

        for (int i = 0; i < 30; i++)
        {
            using var risposta = await client.PostAsJsonAsync("/api/fatture/firma", fatturaSenzaRighe);
            Assert.Equal(HttpStatusCode.BadRequest, risposta.StatusCode);
        }

        using var limitata = await client.PostAsJsonAsync("/api/fatture/firma", fatturaSenzaRighe);
        Assert.Equal(HttpStatusCode.TooManyRequests, limitata.StatusCode);
    }

    private HttpClient ClientAutenticato()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = ChiaveJwt })
            .Build();
        var token = new TokenService(config).Crea(new UtenteEntity
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "firma-test@paghe.local",
            Ruolo = "admin",
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
