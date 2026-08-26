using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Paghe.Api;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Allegati della chat Aurora: il documento viene letto e ne torna solo il testo. Il confine
/// resta quello del testo selezionato — niente file conservati, niente segreti inoltrati.
/// </summary>
public sealed class AiDocumentoApiTests : IDisposable
{
    private const string ChiaveJwt = "chiave-test-documento-ai-sicura-almeno-32-byte-2026";
    private readonly string _database = Path.Combine(
        Path.GetTempPath(), $"paghe-doc-ai-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;

    public AiDocumentoApiTests()
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
    public async Task Documento_Testuale_RestituisceIlTesto()
    {
        using var client = await NuovoClientAutenticatoAsync();

        using var risposta = await CaricaAsync(
            client, "appunti.txt", "text/plain",
            Encoding.UTF8.GetBytes("Ferie residue del dipendente: 12 giorni."));

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        var letto = await risposta.Content.ReadFromJsonAsync<DocumentoLetto>();
        Assert.NotNull(letto);
        Assert.Equal("appunti.txt", letto!.NomeFile);
        Assert.Contains("Ferie residue", letto.Testo);
        Assert.False(letto.Troncato);
    }

    [Fact]
    public async Task Documento_ConSegreto_VieneRifiutato()
    {
        using var client = await NuovoClientAutenticatoAsync();

        using var risposta = await CaricaAsync(
            client, "credenziali.txt", "text/plain",
            Encoding.UTF8.GetBytes("password: sk-Aa1Bb2Cc3Dd4Ee5Ff6Gg7Hh8Ii9Jj0Kk1Ll2Mm3"));

        Assert.Equal(HttpStatusCode.BadRequest, risposta.StatusCode);
        Assert.Contains("chiave segreta", await risposta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Documento_TroppoLungo_VieneTroncatoEDichiarato()
    {
        using var client = await NuovoClientAutenticatoAsync();

        using var risposta = await CaricaAsync(
            client, "lungo.txt", "text/plain",
            Encoding.UTF8.GetBytes(new string('a', 9_000)));

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        var letto = await risposta.Content.ReadFromJsonAsync<DocumentoLetto>();
        Assert.NotNull(letto);
        Assert.True(letto!.Troncato);
        Assert.Equal(4_000, letto.Testo.Length);
    }

    [Fact]
    public async Task Documento_DiTipoNonAmmesso_VieneRifiutato()
    {
        using var client = await NuovoClientAutenticatoAsync();

        using var risposta = await CaricaAsync(
            client, "programma.exe", "application/octet-stream", [0x4D, 0x5A, 0x90, 0x00]);

        Assert.Equal(HttpStatusCode.BadRequest, risposta.StatusCode);
        Assert.Contains("non supportato", await risposta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Documento_ConAnagraficaImpresa_ProponeLAzienda()
    {
        using var client = await NuovoClientAutenticatoAsync();
        const string visura = """
            REGISTRO IMPRESE - VISURA ORDINARIA
            Denominazione: OFFICINA NORD SRL
            Codice fiscale e numero di iscrizione: 12345678903
            Partita IVA: 12345678903
            Sede legale: VIA ROMA 12 - 20100 MILANO (MI)
            Codice ATECO: 45.20.10
            """;

        using var risposta = await CaricaAsync(
            client, "visura.txt", "text/plain", Encoding.UTF8.GetBytes(visura));

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        var letto = await risposta.Content.ReadFromJsonAsync<DocumentoLetto>();
        Assert.NotNull(letto!.Azienda);
        Assert.Contains("OFFICINA NORD", letto.Azienda!.RagioneSociale ?? "");
        Assert.Equal("12345678903", letto.Azienda.PartitaIva);
    }

    [Fact]
    public async Task Documento_SenzaAnagrafica_NonProponeNulla()
    {
        using var client = await NuovoClientAutenticatoAsync();

        using var risposta = await CaricaAsync(
            client, "nota.txt", "text/plain",
            Encoding.UTF8.GetBytes("Promemoria: chiamare il consulente giovedì."));

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        var letto = await risposta.Content.ReadFromJsonAsync<DocumentoLetto>();
        Assert.Null(letto!.Azienda);
    }

    [Fact]
    public async Task Documento_SenzaAutenticazione_Risponde401()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-PaghePro-Client", "web");

        using var risposta = await CaricaAsync(
            client, "appunti.txt", "text/plain", Encoding.UTF8.GetBytes("testo"));

        Assert.Equal(HttpStatusCode.Unauthorized, risposta.StatusCode);
    }

    private static Task<HttpResponseMessage> CaricaAsync(
        HttpClient client, string nome, string tipo, byte[] contenuto)
    {
        var form = new MultipartFormDataContent();
        var parte = new ByteArrayContent(contenuto);
        parte.Headers.ContentType = new MediaTypeHeaderValue(tipo);
        form.Add(parte, "file", nome);
        return client.PostAsync("/api/ai/documento", form);
    }

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
            ragioneSociale = "Studio test allegati",
            email = $"allegati-{Guid.NewGuid():N}@test.local",
            password = "Password-documento-ai-test-2026!",
        });
        Assert.Equal(HttpStatusCode.OK, registrazione.StatusCode);
        var accesso = await registrazione.Content.ReadFromJsonAsync<AccessoResponse>();
        Assert.NotNull(accesso);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accesso!.Token);
        return client;
    }

    private sealed record DocumentoLetto(
        string NomeFile, string Testo, bool Troncato, AziendaProposta? Azienda);

    private sealed record AziendaProposta(
        string? RagioneSociale, string? PartitaIva, string? CodiceFiscale, string? Comune);

    public void Dispose()
    {
        _factory.Dispose();
        if (File.Exists(_database)) File.Delete(_database);
    }
}
