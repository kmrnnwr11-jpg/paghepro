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
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Paghe.Persistence.Sicurezza;

namespace Paghe.Tests;

/// <summary>
/// Confine HTTP del workflow cedolino: autenticazione, RBAC, tenant, versioni, firma e PDF
/// attraversano l'host reale; soltanto i dati di scenario vengono preparati dal fixture.
/// </summary>
public sealed class CedoliniWorkflowApiTests : IDisposable
{
    private const string ChiaveJwt = "chiave-test-workflow-api-sicura-almeno-32-byte-2026";
    private readonly string _database = Path.Combine(
        Path.GetTempPath(), $"paghe-workflow-api-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;

    public CedoliniWorkflowApiTests()
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
    public async Task WorkflowHttp_ApplicaAuthTenantRbacVersioniFirmaEPdfDefinitivo()
    {
        var scenario = await CreaScenarioAsync();
        var adminAltroTenant = await CreaUtenteAltroTenantAsync();

        using (var anonimo = _factory.CreateClient())
        using (var risposta = await anonimo.GetAsync(
                   $"/api/cedolini/{scenario.CedolinoAdminId}/stato"))
            Assert.Equal(HttpStatusCode.Unauthorized, risposta.StatusCode);

        using (var altroTenant = await ClientAutenticatoAsync(adminAltroTenant))
        {
            using var lettura = await altroTenant.GetAsync(
                $"/api/cedolini/{scenario.CedolinoAdminId}/stato");
            Assert.Equal(HttpStatusCode.NotFound, lettura.StatusCode);

            using var controllo = await altroTenant.PostAsJsonAsync(
                $"/api/cedolini/{scenario.CedolinoAdminId}/controlla",
                new { versioneAttesa = 1 });
            Assert.Equal(HttpStatusCode.NotFound, controllo.StatusCode);
        }

        using (var operatore = await ClientAutenticatoAsync(scenario.Operatore))
        {
            using var controllo = await operatore.PostAsJsonAsync(
                $"/api/cedolini/{scenario.CedolinoOperatoreId}/controlla",
                new { versioneAttesa = 1 });
            Assert.Equal(HttpStatusCode.OK, controllo.StatusCode);
            Assert.Equal(("Controllata", 2), await LeggiStatoVersioneAsync(controllo));

            using var valida = await operatore.PostAsJsonAsync(
                $"/api/cedolini/{scenario.CedolinoOperatoreId}/valida",
                new
                {
                    versioneAttesa = 2,
                    nominativo = "Dott.ssa Maria Verdi",
                    qualifica = "Consulente del lavoro",
                    riferimento = "RIF-OPERATORE",
                });
            Assert.Equal(HttpStatusCode.Forbidden, valida.StatusCode);

            using var chiudi = await operatore.PostAsJsonAsync(
                $"/api/cedolini/{scenario.CedolinoOperatoreId}/chiudi",
                new { versioneAttesa = 2 });
            Assert.Equal(HttpStatusCode.Forbidden, chiudi.StatusCode);

            using var firma = MultipartFirma(PngRosso, "firma.png");
            using var upload = await operatore.PostAsync(
                $"/api/aziende/{scenario.AziendaId}/firma-timbro", firma);
            Assert.Equal(HttpStatusCode.Forbidden, upload.StatusCode);
        }

        using var admin = await ClientAutenticatoAsync(scenario.Admin);

        using (var versioneErrata = await admin.PostAsJsonAsync(
                   $"/api/cedolini/{scenario.CedolinoAdminId}/controlla",
                   new { versioneAttesa = 99 }))
            Assert.Equal(HttpStatusCode.Conflict, versioneErrata.StatusCode);

        using (var controllo = await admin.PostAsJsonAsync(
                   $"/api/cedolini/{scenario.CedolinoAdminId}/controlla",
                   new { versioneAttesa = 1 }))
        {
            Assert.Equal(HttpStatusCode.OK, controllo.StatusCode);
            Assert.Equal(("Controllata", 2), await LeggiStatoVersioneAsync(controllo));
        }

        using (var multipartNonValido = MultipartFirma("non-immagine"u8.ToArray(), "falso.png"))
        using (var uploadNonValido = await admin.PostAsync(
                   $"/api/aziende/{scenario.AziendaId}/firma-timbro", multipartNonValido))
            Assert.Equal(HttpStatusCode.BadRequest, uploadNonValido.StatusCode);

        using (var firma = MultipartFirma(PngRosso, "firma.png"))
        using (var upload = await admin.PostAsync(
                   $"/api/aziende/{scenario.AziendaId}/firma-timbro", firma))
            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        using (var validaVecchia = await admin.PostAsJsonAsync(
                   $"/api/cedolini/{scenario.CedolinoAdminId}/valida",
                   new
                   {
                       versioneAttesa = 1,
                       nominativo = "Dott.ssa Maria Verdi",
                       qualifica = "Consulente del lavoro",
                       riferimento = "RIF-ADMIN",
                   }))
            Assert.Equal(HttpStatusCode.Conflict, validaVecchia.StatusCode);

        using (var valida = await admin.PostAsJsonAsync(
                   $"/api/cedolini/{scenario.CedolinoAdminId}/valida",
                   new
                   {
                       versioneAttesa = 2,
                       nominativo = "Dott.ssa Maria Verdi",
                       qualifica = "Consulente del lavoro",
                       riferimento = "RIF-ADMIN",
                   }))
        {
            Assert.Equal(HttpStatusCode.OK, valida.StatusCode);
            Assert.Equal(("Validata", 3), await LeggiStatoVersioneAsync(valida));
        }

        using (var chiusuraVecchia = await admin.PostAsJsonAsync(
                   $"/api/cedolini/{scenario.CedolinoAdminId}/chiudi",
                   new { versioneAttesa = 2 }))
            Assert.Equal(HttpStatusCode.Conflict, chiusuraVecchia.StatusCode);

        using (var chiusura = await admin.PostAsJsonAsync(
                   $"/api/cedolini/{scenario.CedolinoAdminId}/chiudi",
                   new { versioneAttesa = 3 }))
        {
            Assert.Equal(HttpStatusCode.OK, chiusura.StatusCode);
            Assert.Equal(("Chiusa", 4), await LeggiStatoVersioneAsync(chiusura));
        }

        using var pdf = await admin.GetAsync(
            $"/api/cedolini/{scenario.CedolinoAdminId}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Chiusa", Assert.Single(pdf.Headers.GetValues("X-PaghePro-Stato")));
        Assert.Equal(64, Assert.Single(pdf.Headers.GetValues("X-PaghePro-SHA256")).Length);
        Assert.True(pdf.Headers.CacheControl?.Private);
        Assert.True(pdf.Headers.CacheControl?.NoStore);
        Assert.Contains("chiusa.pdf", pdf.Content.Headers.ContentDisposition?.FileName ?? "",
            StringComparison.OrdinalIgnoreCase);
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.AsSpan().StartsWith("%PDF"u8));
    }

    private async Task<Scenario> CreaScenarioAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
        var tenant = new TenantEntity
        {
            Id = Guid.NewGuid(),
            RagioneSociale = "Studio workflow API",
            CreatoIl = DateTime.UtcNow,
        };
        var admin = Utente(tenant.Id, RuoloPolicy.Admin);
        var operatore = Utente(tenant.Id, RuoloPolicy.Operatore);
        db.Tenants.Add(tenant);
        db.Utenti.AddRange(admin, operatore);
        await db.SaveChangesAsync();

        var azienda = await new AziendeService(db).CreaAsync(tenant.Id, new AziendaEntity
        {
            RagioneSociale = "Alfa Workflow Srl",
            PartitaIva = "01234560017",
            CodiceFiscale = "01234560017",
            Indirizzo = "Via Roma 1",
            Cap = "20100",
            Comune = "Milano",
            Provincia = "MI",
            MatricolaInps = "1234567890",
            CodiceSedeInps = "0001",
            CscInps = "70708",
            CodiciAutorizzazioneInps = "NESSUNO",
            CodiceFondoIntegrazioneSalariale = "FIS",
            CodiceAgevolazioneContributiva = "NESSUNA",
            AliquotaInpsDipendente = 0.0949m,
            AliquotaInpsDatore = 0.2788m,
            FonteProfiloContributivo = "Fixture API workflow: profilo verificato",
            PatInail = "12345678",
            CcnlDefault = "COMMERCIO_TERZIARIO",
            CodiceAteco = "45.20.10",
            RiferimentoVisuraCamerale = "VISURA-API-WORKFLOW-2026",
            StatoAlboArtigiani = "NonIscritta",
            RiferimentoVerificaApplicabilitaCcnl = "VERIFICA-H011-API-WORKFLOW",
            RiferimentoTerzoElementoTerritoriale = "FONTE-TERRITORIALE-MI-API-WORKFLOW",
        });
        var rapporto = await new AnagraficheLavoroService(db).CreaDipendenteERapportoAsync(
            tenant.Id,
            azienda.Id,
            new DipendenteEntity
            {
                CodiceFiscale = "RSSMRA80A01F205X",
                Nome = "Mario",
                Cognome = "Rossi",
                Iban = "IT60X0542811101000000123456",
            },
            new RapportoLavoroEntity
            {
                CodiceCcnl = "COMMERCIO_TERZIARIO",
                Livello = "3",
                Qualifica = "Impiegato",
                NumeroMensilita = 14,
                Regione = "Lombardia",
                CodiceCatastaleComune = "F205",
                DataDecorrenzaDomicilioFiscale = new DateOnly(2026, 1, 1),
                AddizionaleRegionaleConguaglioAnnuo = 0m,
                AddizionaleComunaleSaldoAnnuo = 0m,
                AddizionaleComunaleAccontoAnnuo = 0m,
                RiferimentoProgressiviFiscaliPregressi = "IMPORT-API-WORKFLOW-2026",
                DataAssunzione = new DateOnly(2026, 5, 1),
                TipoContratto = "Tempo indeterminato",
            });

        var cedolini = new CedoliniService(db, new ParametriNormativiService(db));
        var cedolinoOperatore = await cedolini.CalcolaEArchiviaAsync(
            tenant.Id, rapporto.Id, CartellinoCompleto(2026, 5));
        var cedolinoAdmin = await cedolini.CalcolaEArchiviaAsync(
            tenant.Id, rapporto.Id, CartellinoCompleto(2026, 6));
        return new Scenario(
            tenant.Id, azienda.Id, rapporto.Id,
            cedolinoOperatore.Id, cedolinoAdmin.Id, admin, operatore);
    }

    private async Task<UtenteEntity> CreaUtenteAltroTenantAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
        var tenant = new TenantEntity
        {
            Id = Guid.NewGuid(),
            RagioneSociale = "Altro studio workflow",
            CreatoIl = DateTime.UtcNow,
        };
        var admin = Utente(tenant.Id, RuoloPolicy.Admin);
        db.Tenants.Add(tenant);
        db.Utenti.Add(admin);
        await db.SaveChangesAsync();
        return admin;
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
            famiglia = (await sessioni.CreaAsync(utente, "test-workflow")).FamigliaId;
        }
        var token = new TokenService(config).Crea(utente, famiglia);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Token);
        return client;
    }

    private static async Task<(string Stato, int Versione)> LeggiStatoVersioneAsync(
        HttpResponseMessage risposta)
    {
        using var json = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        return (
            json.RootElement.GetProperty("stato").GetString()!,
            json.RootElement.GetProperty("versione").GetInt32());
    }

    private static MultipartFormDataContent MultipartFirma(byte[] dati, string nome)
    {
        var multipart = new MultipartFormDataContent();
        var contenuto = new ByteArrayContent(dati);
        contenuto.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipart.Add(contenuto, "file", nome);
        return multipart;
    }

    private static UtenteEntity Utente(Guid tenantId, string ruolo) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Email = $"workflow-{ruolo}-{Guid.NewGuid():N}@test.local",
        PasswordHash = PasswordHasher.Hash("Password-workflow-api-2026!"),
        Ruolo = ruolo,
        Attivo = true,
        CreatoIl = DateTime.UtcNow,
    };

    private static Cartellino CartellinoCompleto(int anno, int mese)
    {
        var giornate = new List<GiornataCartellino>();
        decimal progressivo = 0m;
        for (int giorno = 1; giorno <= DateTime.DaysInMonth(anno, mese); giorno++)
        {
            var data = new DateOnly(anno, mese, giorno);
            var riposo = data.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            if (data.DayOfWeek == DayOfWeek.Monday)
                progressivo = 0m;
            decimal ore = riposo ? 0m : 8m;
            progressivo += ore;
            giornate.Add(new GiornataCartellino
            {
                Data = data,
                Causale = riposo ? CausaleGiornata.RiposoSettimanale : CausaleGiornata.Lavorata,
                OreOrdinarie = ore,
                ProgressivoOreSettimanali = progressivo,
            });
        }
        return new Cartellino
        {
            Anno = anno,
            Mese = mese,
            StatoAssegniFamiliari = StatoAssegniFamiliari.NonSpettanti,
            Giornate = giornate,
        };
    }

    private static byte[] PngRosso => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9ZQmcAAAAASUVORK5CYII=");

    private sealed record Scenario(
        Guid TenantId,
        Guid AziendaId,
        Guid RapportoId,
        Guid CedolinoOperatoreId,
        Guid CedolinoAdminId,
        UtenteEntity Admin,
        UtenteEntity Operatore);

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
