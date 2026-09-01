using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Paghe.Api;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Paghe.Persistence.Sicurezza;
using UglyToad.PdfPig;

namespace Paghe.Tests;

/// <summary>
/// Prova l'intera catena della busta agricola dal vivo: tabelle salariali lette dal database,
/// motore di calcolo, endpoint autenticato. Il risultato atteso è quello della busta reale di
/// ottobre 2025 (docs/buste-reali/busta-05-agricoltura-otd-cuneo-2025.md).
/// </summary>
public sealed class AgricolturaApiTests : IDisposable
{
    private const string ChiaveJwt = "chiave-test-agricoltura-almeno-32-byte-2026-ok";

    private readonly string _database = Path.Combine(
        Path.GetTempPath(), $"paghe-agricoltura-api-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;
    private Guid _ultimoTenantId;

    public AgricolturaApiTests()
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
    public async Task Anteprima_CalcolaLaBustaRealeDiOttobre_DalleTabelleSulDatabase()
    {
        using var client = await ClientAutenticatoAsync(RuoloPolicy.Admin);
        await CreaParametriNazionali2025Async();

        using var risposta = await client.PostAsJsonAsync(
            "/api/agricoltura/cedolino/anteprima", Ottobre2025());
        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);

        using var json = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        var cedolino = json.RootElement.GetProperty("cedolino");

        Assert.Equal(662.27m, cedolino.GetProperty("totaleCompetenze").GetDecimal());
        Assert.Equal(63.15m, cedolino.GetProperty("contributiDipendente").GetDecimal());
        Assert.Equal(0m, cedolino.GetProperty("irpefNetta").GetDecimal());
        Assert.Equal(600.00m, cedolino.GetProperty("nettoInBusta").GetDecimal());

        // La fonte viaggia con il risultato: chi rivede la busta deve sapere da quale tabella
        // arrivano i numeri, senza doverlo chiedere.
        Assert.Contains("buste paga reali", json.RootElement.GetProperty("fonteTabelle").GetString()!,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Anteprima_SenzaAutenticazione_NonCalcolaNulla()
    {
        using var anonimo = _factory.CreateClient();
        using var risposta = await anonimo.PostAsJsonAsync(
            "/api/agricoltura/cedolino/anteprima", Ottobre2025());

        Assert.Equal(HttpStatusCode.Unauthorized, risposta.StatusCode);
    }

    [Fact]
    public async Task Anteprima_ProvinciaSenzaTabelle_SpiegaCheMancaIlContrattoProvinciale()
    {
        using var client = await ClientAutenticatoAsync(RuoloPolicy.Admin);
        await CreaParametriNazionali2025Async();

        using var risposta = await client.PostAsJsonAsync(
            "/api/agricoltura/cedolino/anteprima", Ottobre2025() with { Provincia = "PC" });

        Assert.Equal(HttpStatusCode.BadRequest, risposta.StatusCode);
        using var json = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        Assert.Contains("contratto provinciale",
            json.RootElement.GetProperty("errore").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportoTabelle_NonEAllaPortataDiUnoStudio()
    {
        using var client = await ClientAutenticatoAsync(RuoloPolicy.Admin);

        using var risposta = await client.PostAsJsonAsync(
            "/api/agricoltura/tabelle/importa",
            new AgricolturaEndpoints.ImportTabelleAgricoleInput(
                2025, "CN", 0.0780m, "prova",
                [new AgricolturaEndpoints.TariffaAgricolaInput("COMUNE", "x", 9m, 3m)],
                []));

        // Le tabelle salariali sono condivise da tutti gli studi: le tocca solo la piattaforma.
        Assert.Equal(HttpStatusCode.Forbidden, risposta.StatusCode);
    }

    [Fact]
    public async Task Tabelle_SonoConsultabiliConLaLoroFonte()
    {
        using var client = await ClientAutenticatoAsync(RuoloPolicy.SolaLettura);

        using var risposta = await client.GetAsync("/api/agricoltura/tabelle/2025/cn");
        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);

        using var json = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        Assert.Equal(0.0954m, json.RootElement.GetProperty("aliquotaTotaleDipendente").GetDecimal());
        Assert.Equal(8.56671m, json.RootElement.GetProperty("tariffe")[0].GetProperty("pagaOraria").GetDecimal());
    }

    [Fact]
    public async Task AnteprimaCollegata_DerivaProvinciaTipoELivelloDalRapportoDelTenant()
    {
        using var client = await ClientAutenticatoAsync(RuoloPolicy.Admin);
        await CreaParametriNazionali2025Async();
        Guid rapportoId = await CreaRapportoAgricoloAsync(_ultimoTenantId);

        using var risposta = await client.PostAsJsonAsync(
            $"/api/agricoltura/rapporti/{rapportoId}/cedolino/anteprima", DatiOttobre2025());

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        using var json = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        Assert.Equal("CN", json.RootElement.GetProperty("provinciaCpl").GetString());
        Assert.Equal("OTD", json.RootElement.GetProperty("tipoOperaio").GetString());
        Assert.Equal("COMUNE", json.RootElement.GetProperty("livelloTariffa").GetString());
        Assert.Equal(600.00m,
            json.RootElement.GetProperty("cedolino").GetProperty("nettoInBusta").GetDecimal());
        Assert.True(json.RootElement.GetProperty("bozzaDaValidare").GetBoolean());
        Assert.False(json.RootElement.GetProperty("archiviato").GetBoolean());
    }

    [Fact]
    public async Task AnteprimaCollegata_OtiSenzaTabelleDedicate_RestaBloccata()
    {
        using var client = await ClientAutenticatoAsync(RuoloPolicy.Admin);
        await CreaParametriNazionali2025Async();
        Guid rapportoId = await CreaRapportoAgricoloAsync(
            _ultimoTenantId, tipoContratto: "Tempo indeterminato");

        using var risposta = await client.PostAsJsonAsync(
            $"/api/agricoltura/rapporti/{rapportoId}/cedolino/anteprima", DatiOttobre2025());

        Assert.Equal(HttpStatusCode.BadRequest, risposta.StatusCode);
        using var json = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        Assert.Contains("OTI", json.RootElement.GetProperty("errore").GetString()!);
        Assert.Contains("solo gli OTD", json.RootElement.GetProperty("errore").GetString()!);
    }

    [Fact]
    public async Task PdfCollegato_EUnaBozzaAgricolaCompletaENonCacheabile()
    {
        using var client = await ClientAutenticatoAsync(RuoloPolicy.Admin);
        await CreaParametriNazionali2025Async();
        Guid rapportoId = await CreaRapportoAgricoloAsync(_ultimoTenantId);

        using var risposta = await client.PostAsJsonAsync(
            $"/api/agricoltura/rapporti/{rapportoId}/cedolino/pdf", DatiOttobre2025());

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        Assert.Equal("application/pdf", risposta.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", risposta.Headers.CacheControl?.ToString() ?? "");
        Assert.Equal("BozzaAgricola", risposta.Headers.GetValues("X-PaghePro-Stato").Single());
        Assert.Contains(
            "busta-agricola-Rossi-Mario-2025-10-bozza.pdf",
            risposta.Content.Headers.ContentDisposition?.ToString() ?? "");
        var dati = await risposta.Content.ReadAsByteArrayAsync();
        using var pdf = PdfDocument.Open(dati);
        string testo = string.Join("\n", pdf.GetPages().Select(p => p.Text));
        Assert.Contains("AZIENDA AGRICOLA TEST", testo);
        Assert.Contains("Rossi Mario", testo);
        Assert.Contains("600,00", testo);
        Assert.Contains("BOZZA DA VALIDARE", testo);
    }

    // ---- Archivio delle buste agricole -------------------------------------------------

    [Fact]
    public async Task Archivia_SalvaLaBusta_EIlPdfSiRileggeIdenticoDallArchivio()
    {
        using var client = await ClientAutenticatoAsync(RuoloPolicy.Admin);
        await CreaParametriNazionali2025Async();
        Guid rapportoId = await CreaRapportoAgricoloAsync(_ultimoTenantId);

        using var archiviazione = await client.PostAsJsonAsync(
            $"/api/agricoltura/rapporti/{rapportoId}/cedolino/archivia", DatiOttobre2025());
        Assert.Equal(HttpStatusCode.OK, archiviazione.StatusCode);
        using var esito = JsonDocument.Parse(await archiviazione.Content.ReadAsStringAsync());
        var bustaId = esito.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(600.00m, esito.RootElement.GetProperty("nettoInBusta").GetDecimal());
        Assert.Equal(1, esito.RootElement.GetProperty("versione").GetInt32());
        string hashArchiviato = esito.RootElement.GetProperty("hashPdfSha256").GetString()!;
        Assert.Contains("non costituisce LUL", esito.RootElement.GetProperty("avviso").GetString());

        using var pdf = await client.GetAsync($"/api/cedolini-agricoli/{bustaId}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("AgricolaArchiviata", pdf.Headers.GetValues("X-PaghePro-Stato").Single());
        Assert.Equal(hashArchiviato, pdf.Headers.GetValues("X-PaghePro-SHA256").Single());
        Assert.Contains(
            "busta-agricola-Rossi-Mario-2025-10-archiviata.pdf",
            pdf.Content.Headers.ContentDisposition?.ToString() ?? "");
        using var documento = PdfDocument.Open(await pdf.Content.ReadAsByteArrayAsync());
        string testo = string.Join("\n", documento.GetPages().Select(pagina => pagina.Text));
        Assert.Contains("Rossi Mario", testo);
        Assert.Contains("600,00", testo);
        Assert.Contains("BOZZA DA VALIDARE", testo);
    }

    [Fact]
    public async Task Archivia_LoStessoPeriodoDueVolte_SostituisceEAlzaLaVersione()
    {
        using var client = await ClientAutenticatoAsync(RuoloPolicy.Admin);
        await CreaParametriNazionali2025Async();
        Guid rapportoId = await CreaRapportoAgricoloAsync(_ultimoTenantId);

        using var prima = await client.PostAsJsonAsync(
            $"/api/agricoltura/rapporti/{rapportoId}/cedolino/archivia", DatiOttobre2025());
        Assert.Equal(HttpStatusCode.OK, prima.StatusCode);

        // Correzione delle presenze: stesso rapporto e stesso mese.
        var corretto = DatiOttobre2025() with { OreOrdinarie = 48m, GiornateLavorate = 6 };
        using var seconda = await client.PostAsJsonAsync(
            $"/api/agricoltura/rapporti/{rapportoId}/cedolino/archivia", corretto);
        Assert.Equal(HttpStatusCode.OK, seconda.StatusCode);
        using var esito = JsonDocument.Parse(await seconda.Content.ReadAsStringAsync());
        Assert.Equal(2, esito.RootElement.GetProperty("versione").GetInt32());

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
        var buste = db.CedoliniAgricoli.Where(c => c.RapportoLavoroId == rapportoId).ToList();
        var unica = Assert.Single(buste);
        Assert.Equal(6, unica.GiornateLavorate);   // vale l'ultima archiviazione
        Assert.Equal(2, unica.Versione);
    }

    [Fact]
    public async Task BustaAgricolaArchiviata_RestaFuoriDallArchivioDeiCedoliniOrdinari()
    {
        using var client = await ClientAutenticatoAsync(RuoloPolicy.Admin);
        await CreaParametriNazionali2025Async();
        Guid rapportoId = await CreaRapportoAgricoloAsync(_ultimoTenantId);

        using var archiviazione = await client.PostAsJsonAsync(
            $"/api/agricoltura/rapporti/{rapportoId}/cedolino/archivia", DatiOttobre2025());
        Assert.Equal(HttpStatusCode.OK, archiviazione.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
        // L'invariante che protegge gli adempimenti: F24 da paghe, UniEmens e CU leggono la
        // tabella Cedolini. Una busta agricola lì dentro produrrebbe versamenti sbagliati.
        Assert.Empty(db.Cedolini.Where(c => c.RapportoLavoroId == rapportoId).ToList());
        Assert.Single(db.CedoliniAgricoli.Where(c => c.RapportoLavoroId == rapportoId).ToList());
    }

    [Fact]
    public async Task Elenco_MostraLeBusteArchiviateDellAziendaColNominativo()
    {
        using var client = await ClientAutenticatoAsync(RuoloPolicy.Admin);
        await CreaParametriNazionali2025Async();
        Guid rapportoId = await CreaRapportoAgricoloAsync(_ultimoTenantId);
        using var archiviazione = await client.PostAsJsonAsync(
            $"/api/agricoltura/rapporti/{rapportoId}/cedolino/archivia", DatiOttobre2025());
        Assert.Equal(HttpStatusCode.OK, archiviazione.StatusCode);

        Guid aziendaId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
            aziendaId = db.Rapporti.Single(r => r.Id == rapportoId).AziendaId;
        }

        using var elenco = await client.GetAsync(
            $"/api/aziende/{aziendaId}/cedolini-agricoli?anno=2025&mese=10");
        Assert.Equal(HttpStatusCode.OK, elenco.StatusCode);
        using var json = JsonDocument.Parse(await elenco.Content.ReadAsStringAsync());
        var riga = Assert.Single(json.RootElement.EnumerateArray().ToList());
        Assert.Equal("Rossi Mario", riga.GetProperty("nominativo").GetString());
        Assert.Equal(600.00m, riga.GetProperty("nettoInBusta").GetDecimal());
        Assert.Equal(8, riga.GetProperty("giornateLavorate").GetInt32());
        Assert.Equal("CN", riga.GetProperty("provinciaCpl").GetString());

        // Un mese senza buste non ne inventa.
        using var vuoto = await client.GetAsync(
            $"/api/aziende/{aziendaId}/cedolini-agricoli?anno=2025&mese=11");
        using var jsonVuoto = JsonDocument.Parse(await vuoto.Content.ReadAsStringAsync());
        Assert.Empty(jsonVuoto.RootElement.EnumerateArray().ToList());
    }

    [Fact]
    public async Task Archivia_NonAccettaIlRapportoDiUnAltroTenant()
    {
        using var client = await ClientAutenticatoAsync(RuoloPolicy.Admin);
        await CreaParametriNazionali2025Async();
        Guid tenantEstraneo;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
            tenantEstraneo = Guid.NewGuid();
            db.Tenants.Add(new TenantEntity
            {
                Id = tenantEstraneo,
                RagioneSociale = "Studio estraneo archivio",
                CreatoIl = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        Guid rapportoEstraneo = await CreaRapportoAgricoloAsync(tenantEstraneo);

        using var risposta = await client.PostAsJsonAsync(
            $"/api/agricoltura/rapporti/{rapportoEstraneo}/cedolino/archivia", DatiOttobre2025());

        Assert.Equal(HttpStatusCode.NotFound, risposta.StatusCode);
        await using var verifica = _factory.Services.CreateAsyncScope();
        var contesto = verifica.ServiceProvider.GetRequiredService<PagheDbContext>();
        Assert.Empty(contesto.CedoliniAgricoli.ToList());
    }

    [Fact]
    public async Task AnteprimaCollegata_NonLeggeIlRapportoDiUnAltroTenant()
    {
        using var client = await ClientAutenticatoAsync(RuoloPolicy.Admin);
        await CreaParametriNazionali2025Async();
        Guid tenantEstraneo;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
            tenantEstraneo = Guid.NewGuid();
            db.Tenants.Add(new TenantEntity
            {
                Id = tenantEstraneo,
                RagioneSociale = "Studio estraneo",
                CreatoIl = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        Guid rapportoEstraneo = await CreaRapportoAgricoloAsync(tenantEstraneo);

        using var risposta = await client.PostAsJsonAsync(
            $"/api/agricoltura/rapporti/{rapportoEstraneo}/cedolino/anteprima", DatiOttobre2025());

        Assert.Equal(HttpStatusCode.NotFound, risposta.StatusCode);
    }

    [Fact]
    public async Task WorkflowOrdinario_RifiutaIlRapportoAgricoloSenzaArchiviare()
    {
        using var client = await ClientAutenticatoAsync(RuoloPolicy.Admin);
        Guid rapportoId = await CreaRapportoAgricoloAsync(_ultimoTenantId);

        using var risposta = await client.PostAsJsonAsync(
            $"/api/rapporti/{rapportoId}/cedolini",
            new Cartellino { Anno = 2025, Mese = 10 });

        Assert.Equal(HttpStatusCode.BadRequest, risposta.StatusCode);
        using var json = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        Assert.Contains("agricolo", json.RootElement.GetProperty("errore").GetString()!,
            StringComparison.OrdinalIgnoreCase);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
        Assert.False(db.Cedolini.Any(c => c.TenantId == _ultimoTenantId));
        Assert.False(db.Cartellini.Any(c => c.TenantId == _ultimoTenantId));
    }

    private static AgricolturaEndpoints.AnteprimaCedolinoAgricoloInput Ottobre2025() => new(
        Anno: 2025, Mese: 10, Provincia: "CN", Livello: "COMUNE", Tipo: "Determinato",
        OreOrdinarie: 64m, GiornateLavorate: 8, GiorniDetrazione: 10,
        UnaTantum: 114.00m, DetrazioniGiaGoduteAnno: 0m,
        ArrotondamentoRiportato: 0m, UltimoCedolino: false,
        RedditoPresuntoAnnuo: 6000m, Regione: null, CodiceCatastaleComune: null);

    private static AgricolturaEndpoints.DatiMensiliCedolinoAgricoloInput DatiOttobre2025() => new(
        Anno: 2025, Mese: 10, OreOrdinarie: 64m, GiornateLavorate: 8,
        GiorniDetrazione: 10, UnaTantum: 114.00m, DetrazioniGiaGoduteAnno: 0m,
        ArrotondamentoRiportato: 0m, UltimoCedolino: false,
        RedditoPresuntoAnnuo: 6000m);

    private async Task<Guid> CreaRapportoAgricoloAsync(
        Guid tenantId, string tipoContratto = "Tempo determinato")
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
        var azienda = new AziendaEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RagioneSociale = "Azienda Agricola Test",
            CodiceFiscale = "01234567897",
            PartitaIva = "01234567897",
            Indirizzo = "Via dei Campi 12",
            Cap = "12010",
            Comune = "Cuneo",
            Provincia = "CN",
            HaDipendenti = true,
        };
        var dipendente = new DipendenteEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CodiceFiscale = "RSSMRA80A01D205X",
            Nome = "Mario",
            Cognome = "Rossi",
        };
        var rapporto = new RapportoLavoroEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AziendaId = azienda.Id,
            DipendenteId = dipendente.Id,
            CodiceCcnl = "AGRICOLTURA_OPERAI",
            Livello = "COMUNE",
            Qualifica = "Operaio",
            TipoContratto = tipoContratto,
            ProvinciaLavoro = "CN",
            DataAssunzione = new DateOnly(2025, 10, 1),
            DetrazioniLavoroDipendente = true,
            OreSettimanali = 39m,
            NumeroMensilita = 14,
        };
        db.Aziende.Add(azienda);
        db.Dipendenti.Add(dipendente);
        db.Rapporti.Add(rapporto);
        await db.SaveChangesAsync();
        return rapporto.Id;
    }

    /// <summary>
    /// I parametri nazionali sono versionati per anno e il database ne contiene solo l'ultimo:
    /// per rielaborare una busta del 2025 vanno caricati i valori di quell'anno, seconda
    /// aliquota IRPEF al 35% compresa. Senza, l'API risponde che l'anno non c'è — ed è giusto
    /// così: meglio un rifiuto chiaro che una busta calcolata con le aliquote sbagliate.
    /// </summary>
    private async Task CreaParametriNazionali2025Async()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
        if (await db.ParametriAnno.FindAsync(2025) is not null) return;

        db.ParametriAnno.Add(new ParametriAnnoEntity
        {
            Anno = 2025,
            DivisoreOrario = 168m,
            MaggiorazioneStraordinario = 0.15m,
            MaggiorazioneStraordinarioNotturno = 0.50m,
            MaggiorazioneStraordinarioFestivo = 0.30m,
            TfrDivisore = 13.5m,
            TfrContributoFondo = 0.005m,
        });
        db.IrpefScaglioni.AddRange(
            new IrpefScaglioneEntity { Id = Guid.NewGuid(), Anno = 2025, DaImporto = 0m, AImporto = 28000m, Aliquota = 0.23m },
            new IrpefScaglioneEntity { Id = Guid.NewGuid(), Anno = 2025, DaImporto = 28000m, AImporto = 50000m, Aliquota = 0.35m },
            new IrpefScaglioneEntity { Id = Guid.NewGuid(), Anno = 2025, DaImporto = 50000m, AImporto = null, Aliquota = 0.43m });
        await db.SaveChangesAsync();
    }

    private async Task<HttpClient> ClientAutenticatoAsync(string ruolo)
    {
        UtenteEntity utente;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
            var tenant = new TenantEntity
            {
                Id = Guid.NewGuid(), RagioneSociale = "Studio agricolo", CreatoIl = DateTime.UtcNow,
            };
            utente = new UtenteEntity
            {
                Id = Guid.NewGuid(), TenantId = tenant.Id,
                Email = $"agricoltura-{Guid.NewGuid():N}@test.local",
                PasswordHash = PasswordHasher.Hash("Password-agricoltura-2026!"),
                Ruolo = ruolo, Attivo = true, CreatoIl = DateTime.UtcNow,
            };
            db.Tenants.Add(tenant);
            db.Utenti.Add(utente);
            await db.SaveChangesAsync();
            _ultimoTenantId = tenant.Id;
        }

        var client = _factory.CreateClient();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = ChiaveJwt })
            .Build();
        Guid famiglia;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sessioni = scope.ServiceProvider.GetRequiredService<SessioniRefreshService>();
            famiglia = (await sessioni.CreaAsync(utente, "test-agricoltura")).FamigliaId;
        }
        var token = new TokenService(config).Crea(utente, famiglia);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Token);
        return client;
    }

    public void Dispose()
    {
        _factory.Dispose();
        if (File.Exists(_database)) File.Delete(_database);
    }
}
