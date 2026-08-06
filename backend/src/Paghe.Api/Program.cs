using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Paghe.Api;
using Paghe.Core.AdeTelematico;
using Paghe.Core.Ai;
using Paghe.Core.Calcolo;
using Paghe.Core.Contabilita;
using Paghe.Core.Domain;
using Paghe.Core.Fatturazione;
using Paghe.Core.Fatturazione.Trasmissione;
using Paghe.Core.Notifiche;
using Paghe.Core.Parametri;
using Paghe.Core.PartiteIva;
using Paghe.Documenti;
using Paghe.Firma;
using Paghe.Persistence;
using Paghe.Persistence.Ai;
using Paghe.Persistence.Gdpr;
using Paghe.Persistence.Gdpr.Retention;
using Paghe.Persistence.Sicurezza;
using Paghe.Persistence.Trasmissione;

var builder = WebApplication.CreateBuilder(args);

// Solo Development/Test possono usare la chiave demo. Anche Staging, QA e ambienti con nomi
// personalizzati falliscono chiusi se PAGHE_DATA_KEY non è configurata.
var protettoreDati = ProtettoreDati.CreaDaAmbiente(
    ProtettoreDati.RichiedeChiaveReale(builder.Environment.EnvironmentName));
builder.Services.AddSingleton(protettoreDati);
var eseguiMigrazioneGdpr = args.Contains(
    "--migra-dati-sensibili",
    StringComparer.OrdinalIgnoreCase);
var verificaMigrazioneGdpr = args.Contains(
    "--verifica-dati-sensibili",
    StringComparer.OrdinalIgnoreCase);

// Gli enum (es. Qualifica) viaggiano come stringhe nel JSON ("Impiegato").
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Persistenza: sceglie il provider automaticamente dalla stringa di connessione.
//  - SQLite (default sviluppo): "Data Source=paghe.db"
//  - PostgreSQL / Supabase:     "Host=...;Database=postgres;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true"
// Si può forzare con la chiave di config "Database:Provider" = "Postgres" | "Sqlite".
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=paghe.db";
var providerConfig = builder.Configuration["Database:Provider"];
bool usaPostgres =
    string.Equals(providerConfig, "Postgres", StringComparison.OrdinalIgnoreCase)
    || (providerConfig is null
        && !connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
        && (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("postgres", StringComparison.OrdinalIgnoreCase)));
builder.Services.AddDbContext<PagheDbContext>(o =>
{
    // Ogni provider ha il proprio assembly di migrazioni: EF Core non può tenerne due nello
    // stesso (un solo ModelSnapshot per contesto). Vedi Paghe.Migrazioni.Sqlite/.Postgres.
    if (usaPostgres)
        o.UseNpgsql(connectionString, sql => sql.MigrationsAssembly("Paghe.Migrazioni.Postgres"));
    else
        o.UseSqlite(connectionString, sql => sql.MigrationsAssembly("Paghe.Migrazioni.Sqlite"));
});
builder.Services.AddScoped<AziendeService>();
builder.Services.AddScoped<DemoDatasetService>();
builder.Services.AddScoped<FattureService>();
builder.Services.AddScoped<AvvisiPagamentoService>();

// Parametri normativi e CCNL caricati dal database, versionati per anno (non più cablati).
builder.Services.AddScoped<ParametriNormativiService>();
builder.Services.AddScoped<AnagraficheLavoroService>();
builder.Services.AddScoped<CedoliniService>();
builder.Services.AddScoped<PraticheF24Service>();
builder.Services.AddScoped<PraticheInailService>();
builder.Services.AddScoped<PraticheAperturaPartitaIvaService>();
builder.Services.AddScoped<ContabilitaIvaService>();
builder.Services.AddScoped<BilancioService>();
builder.Services.AddScoped<UniEmensService>();
builder.Services.AddScoped<ScadenzarioService>();
builder.Services.AddScoped<DocumentiScadenzeService>();
builder.Services.AddScoped<ConfigurazioneSdiService>();
builder.Services.AddScoped<CertificazioneUnicaService>();
builder.Services.AddScoped<LipeService>();
builder.Services.AddScoped<Modello770Service>();
builder.Services.AddScoped<PresenzeService>();
builder.Services.AddScoped<RedditiPfService>();
builder.Services.AddScoped<IrapService>();
builder.Services.AddScoped<IresService>();
builder.Services.AddScoped<ImuService>();
builder.Services.AddScoped<InailService>();
builder.Services.AddScoped<IntrastatService>();
builder.Services.AddScoped<PersoneService>();
builder.Services.AddScoped<ConsumoAiService>();
builder.Services.AddScoped<CasellaEmailService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<RetentionService>();
builder.Services.AddScoped<AiGrantService>();
builder.Services.AddScoped<ProfessionalQueryBroker>();
builder.Services.AddSingleton(TimeProvider.System);

// Trasmissione fattura elettronica (SdI) e deleghe intermediario.
// Canale configurabile in "Sdi": "Manuale" (default, prepara il file) oppure "Rest" (provider accreditato).
var sdiOptions = new SdiProviderOptions();
builder.Configuration.GetSection("Sdi").Bind(sdiOptions);
builder.Services.AddSingleton(sdiOptions);

// Credenziali provider A-Cube (sezione "Acube"): usate quando Sdi:Provider = "Acube".
var acubeOptions = new AcubeOptions();
builder.Configuration.GetSection("Acube").Bind(acubeOptions);
builder.Services.AddSingleton(acubeOptions);

// Certificato di firma digitale CAdES (sezione "Firma"): PFX lato server o smart card/CNS via PKCS#11.
var firmaOptions = new FirmaOptions();
builder.Configuration.GetSection("Firma").Bind(firmaOptions);
builder.Services.AddSingleton(firmaOptions);

// Livello AI di lettura documenti (sezione "Ai"): default "Spento" (nessuna chiamata esterna).
// Provider a scelta: "Claude" (SDK ufficiale Anthropic), "DeepSeek", "Glm" o "Compatibile"
// (qualsiasi endpoint OpenAI-compatibile, anche Ollama in locale). Regola architetturale:
// l'AI legge e PROPONE soltanto — la conferma resta all'utente, i calcoli nel codice.
var aiOptions = new AiOptions();
builder.Configuration.GetSection("Ai").Bind(aiOptions);
builder.Services.AddSingleton(aiOptions);
switch (aiOptions.Provider.ToLowerInvariant())
{
    case "claude":
        builder.Services.AddSingleton<IEstrattoreAiDocumenti, EstrattoreAiAnthropic>();
        break;
    case "deepseek":
    case "glm":
    case "compatibile":
        builder.Services.AddHttpClient<IEstrattoreAiDocumenti, EstrattoreAiCompatibile>();
        break;
    default:
        builder.Services.AddSingleton<IEstrattoreAiDocumenti, EstrattoreAiSpento>();
        break;
}

// Canale "Sdi": "Rest" (provider accreditato), "Simulato" (demo, ciclo completo senza invio reale),
// "Manuale" (default, prepara il file da firmare/caricare).
switch (sdiOptions.Provider.ToLowerInvariant())
{
    case "rest":
        builder.Services.AddHttpClient<IFatturaSdiGateway, SdiGatewayProviderRest>();
        if (sdiOptions.FirmaLocale)
            // Firma CAdES locale reale (Paghe.Firma): PFX o smart card/CNS. Se non configurata, l'errore è esplicito.
            builder.Services.AddScoped<IFirmaFatturaElettronica, FirmaCadesLocale>();
        else
            builder.Services.AddScoped<IFirmaFatturaElettronica, FirmaDelegataAlProvider>();
        break;
    case "acube":
        // Provider accreditato A-Cube: login email/password → token, invio fattura, lettura esiti.
        builder.Services.AddHttpClient<IFatturaSdiGateway, SdiGatewayAcube>();
        builder.Services.AddScoped<IFirmaFatturaElettronica, FirmaDelegataAlProvider>();
        break;
    case "simulato":
        builder.Services.AddScoped<IFatturaSdiGateway, SdiGatewaySimulato>();
        builder.Services.AddScoped<IFirmaFatturaElettronica, FirmaDelegataAlProvider>();
        break;
    default: // "manuale"
        builder.Services.AddScoped<IFatturaSdiGateway, SdiGatewayManuale>();
        builder.Services.AddScoped<IFirmaFatturaElettronica, FirmaDelegataAlProvider>();
        break;
}
builder.Services.AddScoped<TrasmissioneFattureService>();
builder.Services.AddScoped<DelegheService>();

// Canali di notifica per promemoria/solleciti (sezione "Notifiche"): Manuale (default, non invia),
// Email (SMTP), Sms (gateway sagomato Twilio), WhatsApp (Meta Cloud API). Sono registrati tutti:
// il canale effettivo si sceglie per richiesta o dal default in configurazione.
var notificheOptions = new NotificheOptions();
builder.Configuration.GetSection("Notifiche").Bind(notificheOptions);
builder.Services.AddSingleton(notificheOptions);
builder.Services.AddSingleton<NotificatoreManuale>();
builder.Services.AddScoped<NotificatoreEmailSmtp>();
builder.Services.AddHttpClient<NotificatoreSms>();
builder.Services.AddHttpClient<NotificatoreWhatsApp>();
builder.Services.AddScoped<NotificheService>();

// Autenticazione JWT: il tenant è derivato dai claim del token firmato, non da un header.
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<SessioniRefreshService>();
builder.Services.AddHostedService<PuliziaSessioniRefreshWorker>();
builder.Services.AddScoped<PiattaformaAdminService>();
// La chiave si valuta SUBITO (non nella lambda, che è lazy): in produzione senza una chiave
// robusta l'avvio si blocca qui con un errore esplicito, non al primo login.
var chiaveJwt = Encoding.UTF8.GetBytes(TokenService.ChiaveJwt(builder.Configuration));

// Chiave delle lease del Governor AI: derivata dalla chiave JWT (stessa protezione in produzione).
builder.Services.AddSingleton(new AiGovernorOptions(AiLeaseCodec.DerivaChiave(chiaveJwt)));
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var chiave = chiaveJwt;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = TokenService.Emittente(builder.Configuration),
            ValidateAudience = true,
            ValidAudience = TokenService.Destinatario(builder.Configuration),
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(chiave),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = ClaimTypes.Role,
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async contesto =>
            {
                var principale = contesto.Principal;
                var versione = principale?.FindFirst("ver")?.Value;
                var ruolo = principale?.FindFirst(ClaimTypes.Role)?.Value;
                if (versione != TokenService.VersioneProtocollo
                    || !Guid.TryParse(principale?.FindFirst("sub")?.Value, out var utenteId)
                    || !Guid.TryParse(principale?.FindFirst("tenant_id")?.Value, out var tenantId)
                    || !Guid.TryParse(principale?.FindFirst("sid")?.Value, out var famigliaId)
                    || string.IsNullOrWhiteSpace(ruolo))
                {
                    contesto.Fail("Sessione non valida.");
                    return;
                }

                var sessioni = contesto.HttpContext.RequestServices
                    .GetRequiredService<SessioniRefreshService>();
                if (!await sessioni.ValidaAccessoAsync(
                    tenantId, utenteId, famigliaId, ruolo,
                    contesto.HttpContext.RequestAborted))
                    contesto.Fail("Sessione revocata o scaduta.");
            },
        };
    });
builder.Services.AddAuthorization();

// Rate limiting anti-brute-force sugli endpoint di autenticazione (login + 2FA): max 10 tentativi
// al minuto per indirizzo IP, poi HTTP 429. Protegge password e codice TOTP dalla forza bruta.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", http =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "sconosciuto",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    // Operazioni costose e sensibili (firma, upload, AI): limite per utente autenticato, poi per IP.
    options.AddPolicy("sensibili", http =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.User.FindFirst("sub")?.Value
                ?? http.Connection.RemoteIpAddress?.ToString() ?? "sconosciuto",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
});

// Documentazione interattiva degli endpoint (Swagger / OpenAPI).
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS per il frontend SPA: permissivo SOLO in sviluppo. In produzione gli origin ammessi
// vanno elencati in "Cors:Origini" (es. https://paghe-frontend.onrender.com); senza lista
// nessun sito esterno può chiamare l'API dal browser.
var originiCors = builder.Configuration.GetSection("Cors:Origini").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (builder.Environment.IsDevelopment())
        p.WithOrigins(
                "http://localhost:5173", "https://localhost:5173",
                "capacitor://localhost", "https://localhost")
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    else
        p.WithOrigins(originiCors
                .Concat(["capacitor://localhost", "https://localhost"])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray())
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    // Gli origin Capacitor sono sempre ammessi; i siti web esterni restano esplicitamente configurati.
}));

var app = builder.Build();

// Presente nell'immagine Railway full-stack; assente nello sviluppo locale e nei test.
var frontendIntegrato = Directory.Exists(
    Path.Combine(app.Environment.ContentRootPath, "wwwroot"));

// In produzione senza origin CORS configurati il frontend su altro dominio non funziona:
// avvisa chiaramente nei log invece di fallire in silenzio.
if (!app.Environment.IsDevelopment() && originiCors.Length == 0 && !frontendIntegrato)
    app.Logger.LogWarning(
        "CORS: nessun origin configurato in 'Cors:Origini'. Il browser bloccherà le chiamate " +
        "da un frontend su altro dominio. Impostare es. Cors__Origini__0=https://tuo-frontend.");

// Swagger UI su /swagger e spec OpenAPI su /swagger/v1/swagger.json.
// In produzione è spento di default (non esporre la mappa dell'API); riattivabile con Swagger:Abilitato=true.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Swagger:Abilitato", false))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Header di sicurezza HTTP su ogni risposta. HSTS solo in produzione (impostato direttamente
// perché dietro il proxy TLS del provider la richiesta arriva in HTTP).
bool produzione = !app.Environment.IsDevelopment();
app.Use(async (context, next) =>
{
    var h = context.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "no-referrer";
    if (context.Request.Path.StartsWithSegments("/api/auth"))
    {
        h["Cache-Control"] = "no-store";
        h["Pragma"] = "no-cache";
    }
    if (produzione)
        h["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    await next();
});

// Nel deploy Railway di prova il frontend Vite viene copiato in /app/wwwroot e
// servito dalla stessa API. In sviluppo e nei test la cartella non esiste, quindi
// il comportamento corrente (frontend separato su Vite) resta invariato.
if (frontendIntegrato)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseCors();
app.UseAuthentication();
// Il registro avvolge limiter, autorizzazione e RBAC: così conserva anche tentativi autenticati
// negati (403/429), senza registrare body, query string, IP o user-agent.
app.UseAuditRichieste();
// Il limiter delle operazioni sensibili usa il claim "sub" quando autenticato; deve quindi
// essere eseguito dopo l'autenticazione (con fallback sull'IP per le richieste anonime).
app.UseRateLimiter();
app.UseAuthorization();

// RBAC: applica la policy per ruolo (sola_lettura → solo lettura; anonimizzazione GDPR → solo admin).
app.UseControlloRuoli();

// Gruppo di endpoint che richiede un token valido (i dati di un tenant).
var secured = app.MapGroup("").RequireAuthorization();

// Porta il database allo schema corrente applicando le migrazioni EF versionate
// e carica i parametri normativi di esempio se le tabelle sono vuote.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
    var migrazioneGdpr = new MigrazioneDatiSensibili(db, protettoreDati);
    if (verificaMigrazioneGdpr)
    {
        // Questo ramo deve restare realmente read-only: niente EnsureCreated, DDL, seed o
        // rotazione della sentinella. Qualunque prerequisito assente provoca un errore.
        migrazioneGdpr.VerificaCutoverCompletatoAsync().GetAwaiter().GetResult();
    }
    else
    {
        // Schema gestito dalle migrazioni EF versionate (un assembly per provider).
        // I database creati prima delle migrazioni vengono adottati con una baseline.
        app.Logger.LogInformation("Schema database: {Esito}.", SchemaDatabase.Applica(db));

        if (eseguiMigrazioneGdpr)
        {
            // Comando amministrativo one-shot: il DDL/backfill non viene eseguito da ogni replica.
            var esitoMigrazioneGdpr = migrazioneGdpr.EseguiTuttiAsync().GetAwaiter().GetResult();
            app.Logger.LogInformation(
                "Migrazione GDPR completata: {ValoriMigrati} valori su {TenantEsaminati} tenant.",
                esitoMigrazioneGdpr.ValoriMigrati,
                esitoMigrazioneGdpr.TenantEsaminati);
        }
        else
        {
            // Solo un database realmente vuoto può ricevere automaticamente la prima sentinella.
            // Su database popolati il gate resta fail-closed e richiede il runner esplicito.
            migrazioneGdpr.InizializzaSeDatabaseVuotoAsync().GetAwaiter().GetResult();
            migrazioneGdpr.VerificaCutoverCompletatoAsync().GetAwaiter().GetResult();
        }

        if (!eseguiMigrazioneGdpr && !verificaMigrazioneGdpr)
        {
            scope.ServiceProvider.GetRequiredService<ParametriNormativiService>()
                .ImportaSeedSeVuotoAsync().GetAwaiter().GetResult();

            // Sovrascrive i minimi CCNL d'esempio del Seed con il dataset ufficiale (fonti CNEL),
            // anche sui database già esistenti: upsert idempotente per (codice + anno).
            new CcnlImportService(db)
                .ImportaAsync(CcnlDatasetUfficiale.Carica()).GetAwaiter().GetResult();
        }
    }
}

if (eseguiMigrazioneGdpr || verificaMigrazioneGdpr)
    return;

// Tenant corrente derivato dal claim "tenant_id" del token JWT (utente autenticato).
static Guid TenantCorrente(ClaimsPrincipal user) =>
    Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id)
        ? id
        : throw new InvalidOperationException("Tenant non presente nel token.");

static object AvvisoPagamentoDto(Paghe.Persistence.Entities.AvvisoPagamentoEntity a)
{
    var oggi = DateOnly.FromDateTime(DateTime.UtcNow);
    return new
    {
        a.Id,
        a.AziendaId,
        Cliente = a.Azienda?.RagioneSociale,
        a.Anno,
        a.Mese,
        a.Descrizione,
        a.Importo,
        a.DataEmissione,
        a.DataScadenza,
        a.Stato,
        a.MetodoPagamento,
        a.Iban,
        a.Causale,
        a.Note,
        a.PagatoIl,
        a.RiferimentoPagamento,
        a.NumeroPromemoria,
        a.UltimoPromemoriaIl,
        GiorniAllaScadenza = a.DataScadenza.DayNumber - oggi.DayNumber,
        Scaduto = a.Stato != AvvisiPagamentoService.Pagato
            && a.Stato != AvvisiPagamentoService.Annullato
            && a.DataScadenza < oggi,
    };
}

// Costruisce il motore di calcolo caricando parametri e CCNL dal DB per l'anno indicato.
static async Task<MotoreCalcoloCedolino> CostruisciMotore(
    ParametriNormativiService svc, RapportoLavoro rapporto, int anno)
{
    var parametri = await svc.CaricaParametriAsync(anno);
    var ccnl = await svc.CaricaCcnlAsync(rapporto.CodiceCcnl, anno);
    return new MotoreCalcoloCedolino(parametri, ccnl);
}

// Legge in memoria il contenuto di un file caricato (multipart/form-data).
static async Task<byte[]> LeggiFile(IFormFile file)
{
    using var ms = new MemoryStream();
    await file.CopyToAsync(ms);
    return ms.ToArray();
}

// Riconosce dal contenuto il tipo di immagine accettato per il logo aziendale (PNG/JPEG).
static string? TipoImmagineLogo(byte[] dati) => dati switch
{
    [0x89, (byte)'P', (byte)'N', (byte)'G', ..] => "image/png",
    [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
    _ => null,
};

// Proietta un promemoria aggiungendo i giorni mancanti e l'urgenza calcolata a oggi.
static object PromemoriaDto(Paghe.Persistence.Entities.PromemoriaEntity p)
{
    var oggi = DateOnly.FromDateTime(DateTime.UtcNow);
    int giorni = p.DataScadenza.DayNumber - oggi.DayNumber;
    string urgenza =
        p.Stato is "Completato" or "Annullato" ? "chiuso"
        : giorni < 0 ? "scaduto"
        : giorni == 0 ? "oggi"
        : giorni <= p.GiorniPreavviso ? "in_scadenza"
        : "futuro";
    return new
    {
        p.Id,
        p.AziendaId,
        p.Tipo,
        p.RiferimentoId,
        p.Titolo,
        p.Descrizione,
        p.DataScadenza,
        p.GiorniPreavviso,
        p.Stato,
        p.Messaggio,
        giorniAllaScadenza = giorni,
        urgenza,
    };
}

app.MapGet(frontendIntegrato ? "/api" : "/", () => Results.Ok(new
{
    servizio = "Paghe SaaS API",
    versione = "0.1.0-mvp",
    avvertenza = "Valori normativi di esempio: validare con un Consulente del Lavoro.",
    endpoint = new[]
    {
        "GET  /health",
        "POST /api/auth/registra",
        "POST /api/auth/login",
        "GET  /api/ccnl/{codice}/livelli",
        "POST /api/cedolini/calcola",
        "POST /api/f24/da-paghe",
        "POST /api/fatture/calcola",
        "POST /api/fatture/xml",
        "POST /api/fatture/valida",
        "POST /api/fatture/firma         (CAdES .xml.p7m o XAdES con ?formato=xades)",
        "POST /api/firma/cades           (firma file: PFX o CNS/smart card; campo formato=cades|xades)",
        "POST /api/firma/verifica        (verifica .p7m/XML firmato: validità, firmatario, data)",
        "POST /api/bilancio/riclassifica",
        "POST /api/aziende/{id}/bilanci",
        "POST /api/aziende/{id}/bilanci/esempio",
        "GET  /api/aziende/{id}/bilanci/precompila?anno=",
        "GET  /api/aziende/{id}/bilanci",
        "GET  /api/bilanci/{id}",
        "GET  /api/bilanci/{id}/pdf",
        "POST /api/bilanci/{id}/stato",
        "POST /api/contabilita/iva/liquidazione",
        "POST /api/aziende            (richiede Bearer token)",
        "GET  /api/aziende",
        "POST /api/aziende/{id}/spese",
        "GET  /api/aziende/{id}/spese",
        "GET  /api/spese/{id}",
        "POST /api/aziende/{id}/liquidazioni-iva",
        "GET  /api/aziende/{id}/liquidazioni-iva",
        "GET  /api/liquidazioni-iva/{id}",
        "POST /api/pratiche/apertura-partita-iva/simula",
        "POST /api/pratiche/apertura-partita-iva",
        "GET  /api/pratiche/apertura-partita-iva",
        "GET  /api/pratiche/apertura-partita-iva/{id}",
        "PATCH /api/pratiche/apertura-partita-iva/{praticaId}/adempimenti/{adempimentoId}",
        "POST /api/pratiche/apertura-partita-iva/{id}/stato",
        "GET  /api/pratiche/apertura-partita-iva/{id}/distinta",
        "POST /api/pratiche/apertura-partita-iva/analizza-testo",
        "POST /api/aziende/{id}/fatture",
        "GET  /api/aziende/{id}/fatture",
        "GET  /api/fatture/{id}",
        "POST /api/aziende/{id}/dipendenti",
        "POST /api/aziende/{id}/dipendenti/da-unilav",
        "GET  /api/aziende/{id}/dipendenti",
        "POST /api/rapporti/{id}/cedolini",
        "GET  /api/rapporti/{id}/cedolini",
        "GET  /api/cedolini/{id}",
        "POST /api/aziende/{id}/f24/mese",
        "POST /api/aziende/{id}/f24/mese/salva",
        "GET  /api/aziende/{id}/f24",
        "GET  /api/f24/{id}",
        "POST /api/f24/{id}/stato",
        "POST /api/f24/{id}/esporta",
        "POST /api/aziende/{id}/avvisi-pagamento",
        "GET  /api/aziende/{id}/avvisi-pagamento",
        "GET  /api/avvisi-pagamento",
        "POST /api/avvisi-pagamento/mese",
        "GET  /api/avvisi-pagamento/promemoria",
        "GET  /api/avvisi-pagamento/{id}/avviso",
        "POST /api/avvisi-pagamento/{id}/promemoria",
        "POST /api/avvisi-pagamento/{id}/stato",
        "POST /api/aziende/{id}/durc",
        "GET  /api/aziende/{id}/durc",
        "GET  /api/scadenzario",
        "POST /api/scadenzario/rigenera",
        "POST /api/scadenzario",
        "GET  /api/scadenzario/{id}",
        "POST /api/scadenzario/{id}/stato",
        "POST /api/scadenzario/{id}/invia",
        "POST /api/cedolini/pdf",
        "GET  /api/cedolini/{id}/pdf",
        "POST /api/fatture/pdf",
        "GET  /api/fatture/{id}/pdf",
        "POST /api/aziende/{id}/logo",
        "GET  /api/aziende/{id}/logo",
        "DELETE /api/aziende/{id}/logo",
        "GET  /api/rapporti/{id}/cu/{anno}/pdf"
    }
}));

app.MapGet("/health", () => Results.Ok(new { stato = "ok" }));

static async Task RegistraAutenticazioneSenzaBloccareAsync(
    IServiceScopeFactory scopeFactory,
    Guid? tenantId,
    Guid? utenteId,
    string esito,
    int statusCode,
    HttpContext http,
    string azione = "Autenticazione")
{
    if (tenantId is null || utenteId is null) return; // credenziali sconosciute: non attribuibili
    try
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var audit = scope.ServiceProvider.GetRequiredService<AuditLogService>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await audit.RegistraRichiestaAsync(new RichiestaAudit(
            tenantId.Value,
            utenteId.Value,
            "Autenticazione",
            azione,
            "",
            http.Request.Method,
            http.Request.Path.Value ?? "/api/auth",
            esito,
            statusCode,
            http.TraceIdentifier,
            DateTime.UtcNow), timeout.Token);
    }
    catch (Exception ex)
    {
        http.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("RegistroAttivita")
            .LogError(ex, "Audit autenticazione fallito. TraceId: {TraceId}", http.TraceIdentifier);
    }
}

const string CookieRefresh = "paghepro_refresh";
const string HeaderClientWeb = "X-PaghePro-Client";

static bool VerificaRichiestaWeb(HttpContext http) =>
    string.Equals(http.Request.Headers[HeaderClientWeb], "web", StringComparison.Ordinal);

static void ScriviCookieRefresh(HttpContext http, SessioneRefreshEmessa sessione)
{
    var produzione = !http.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment();
    http.Response.Cookies.Append(CookieRefresh, sessione.RefreshToken, new CookieOptions
    {
        HttpOnly = true,
        Secure = produzione,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth",
        Expires = new DateTimeOffset(sessione.ScadeUtc, TimeSpan.Zero),
        IsEssential = true,
    });
}

static void EliminaCookieRefresh(HttpContext http)
{
    var produzione = !http.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment();
    http.Response.Cookies.Delete(CookieRefresh, new CookieOptions
    {
        HttpOnly = true,
        Secure = produzione,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth",
        IsEssential = true,
    });
}

static AccessoResponse CreaRispostaAccesso(
    TokenService tokens,
    SessioneRefreshEmessa sessione)
{
    var accesso = tokens.Crea(sessione.Utente, sessione.FamigliaId);
    return new(accesso.Token, accesso.ScadeUtc, sessione.Utente.TenantId, sessione.Utente.Ruolo);
}

static AccessoMobileResponse CreaRispostaAccessoMobile(
    TokenService tokens,
    SessioneRefreshEmessa sessione)
{
    var accesso = tokens.Crea(sessione.Utente, sessione.FamigliaId);
    return new(
        accesso.Token,
        accesso.ScadeUtc,
        sessione.Utente.TenantId,
        sessione.Utente.Ruolo,
        sessione.RefreshToken,
        new DateTimeOffset(sessione.ScadeUtc, TimeSpan.Zero));
}

// --- Autenticazione (pubblici) -----------------------------------------------------
app.MapPost("/api/auth/registra", async (
    RegistraRequest req, AuthService auth, TokenService tokens, SessioniRefreshService sessioni,
    IServiceScopeFactory scopeFactory, HttpContext http) =>
{
    if (!VerificaRichiestaWeb(http))
        return Results.BadRequest(new { errore = "Client web non riconosciuto." });
    try
    {
        var utente = await auth.RegistraAsync(req.RagioneSociale, req.Email, req.Password);
        var sessione = await sessioni.CreaAsync(utente, "web", http.RequestAborted);
        ScriviCookieRefresh(http, sessione);
        await RegistraAutenticazioneSenzaBloccareAsync(
            scopeFactory, utente.TenantId, utente.Id, "Riuscito", StatusCodes.Status200OK, http);
        return Results.Ok(CreaRispostaAccesso(tokens, sessione));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/login", async (
    LoginRequest req, AuthService auth, TokenService tokens, SessioniRefreshService sessioni,
    IServiceScopeFactory scopeFactory, HttpContext http) =>
{
    if (!VerificaRichiestaWeb(http))
        return Results.BadRequest(new { errore = "Client web non riconosciuto." });
    var r = await auth.AccediAsync(req.Email, req.Password, req.Codice2fa);
    var statusAccesso = r.Esito switch
    {
        EsitoAccesso.Ok => StatusCodes.Status200OK,
        EsitoAccesso.AccountSospeso => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status401Unauthorized,
    };
    var identitaAudit = r.Utente is not null
        ? new IdentitaAccessoAudit(r.Utente.TenantId, r.Utente.Id)
        : r.IdentitaAudit;
    if (identitaAudit is not null)
        await RegistraAutenticazioneSenzaBloccareAsync(
            scopeFactory,
            identitaAudit.TenantId,
            identitaAudit.UtenteId,
            r.Esito switch
            {
                EsitoAccesso.Ok => "Riuscito",
                EsitoAccesso.ServeCodice2fa => "SecondoFattoreRichiesto",
                _ => "Negato",
            },
            statusAccesso,
            http);
    if (r.Esito == EsitoAccesso.Ok)
    {
        var sessione = await sessioni.CreaAsync(r.Utente!, "web", http.RequestAborted);
        ScriviCookieRefresh(http, sessione);
        return Results.Ok(CreaRispostaAccesso(tokens, sessione));
    }
    return r.Esito switch
    {
        EsitoAccesso.ServeCodice2fa => Results.Json(new { serveCodice2fa = true }, statusCode: 401),
        EsitoAccesso.Codice2faErrato => Results.Json(
            new { serveCodice2fa = true, errore = "Codice 2FA non valido." }, statusCode: 401),
        EsitoAccesso.AccountSospeso => Results.Json(
            new { errore = "Studio sospeso. Contatta l'amministratore della piattaforma." }, statusCode: 403),
        _ => Results.Json(new { errore = "Email o password non corretti." }, statusCode: 401),
    };
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/refresh", async (
    SessioniRefreshService sessioni, TokenService tokens,
    IServiceScopeFactory scopeFactory, HttpContext http) =>
{
    if (!VerificaRichiestaWeb(http))
        return Results.BadRequest(new { errore = "Client web non riconosciuto." });
    http.Request.Cookies.TryGetValue(CookieRefresh, out var refresh);
    var risultato = await sessioni.RuotaAsync(refresh, "web", http.RequestAborted);
    if (risultato.Esito == EsitoRotazioneRefresh.Ok)
    {
        ScriviCookieRefresh(http, risultato.Sessione!);
        await RegistraAutenticazioneSenzaBloccareAsync(
            scopeFactory, risultato.Sessione!.Utente.TenantId, risultato.Sessione.Utente.Id,
            "Riuscito", StatusCodes.Status200OK, http, "RinnovoSessione");
        return Results.Ok(CreaRispostaAccesso(tokens, risultato.Sessione!));
    }
    if (risultato.Esito != EsitoRotazioneRefresh.Collisione)
        EliminaCookieRefresh(http);
    return risultato.Esito switch
    {
        EsitoRotazioneRefresh.Collisione => Results.Json(
            new { errore = "Aggiornamento sessione già in corso." }, statusCode: 409),
        EsitoRotazioneRefresh.AccountNonAttivo => Results.Json(
            new { errore = "Account non attivo." }, statusCode: 403),
        _ => Results.Json(new { errore = "Sessione scaduta. Accedi di nuovo." }, statusCode: 401),
    };
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/logout", async (
    SessioniRefreshService sessioni, IServiceScopeFactory scopeFactory, HttpContext http) =>
{
    if (!VerificaRichiestaWeb(http))
        return Results.BadRequest(new { errore = "Client web non riconosciuto." });
    http.Request.Cookies.TryGetValue(CookieRefresh, out var refresh);
    var revocata = await sessioni.RevocaDaTokenAsync(refresh, "Logout", http.RequestAborted);
    EliminaCookieRefresh(http);
    if (revocata is not null)
        await RegistraAutenticazioneSenzaBloccareAsync(
            scopeFactory, revocata.TenantId, revocata.UtenteId,
            "Riuscito", StatusCodes.Status204NoContent, http, "Logout");
    return Results.NoContent();
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/mobile/registra", async (
    RegistraRequest req, AuthService auth, TokenService tokens, SessioniRefreshService sessioni,
    IServiceScopeFactory scopeFactory, HttpContext http) =>
{
    try
    {
        var utente = await auth.RegistraAsync(req.RagioneSociale, req.Email, req.Password);
        var sessione = await sessioni.CreaAsync(utente, "mobile", http.RequestAborted);
        await RegistraAutenticazioneSenzaBloccareAsync(
            scopeFactory, utente.TenantId, utente.Id, "Riuscito", StatusCodes.Status200OK, http);
        return Results.Ok(CreaRispostaAccessoMobile(tokens, sessione));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/mobile/login", async (
    LoginRequest req, AuthService auth, TokenService tokens, SessioniRefreshService sessioni,
    IServiceScopeFactory scopeFactory, HttpContext http) =>
{
    var r = await auth.AccediAsync(req.Email, req.Password, req.Codice2fa);
    var statusAccesso = r.Esito switch
    {
        EsitoAccesso.Ok => StatusCodes.Status200OK,
        EsitoAccesso.AccountSospeso => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status401Unauthorized,
    };
    var identitaAudit = r.Utente is not null
        ? new IdentitaAccessoAudit(r.Utente.TenantId, r.Utente.Id)
        : r.IdentitaAudit;
    if (identitaAudit is not null)
        await RegistraAutenticazioneSenzaBloccareAsync(
            scopeFactory, identitaAudit.TenantId, identitaAudit.UtenteId,
            r.Esito == EsitoAccesso.Ok ? "Riuscito" : "Negato", statusAccesso, http);

    if (r.Esito == EsitoAccesso.Ok)
    {
        var sessione = await sessioni.CreaAsync(r.Utente!, "mobile", http.RequestAborted);
        return Results.Ok(CreaRispostaAccessoMobile(tokens, sessione));
    }
    return r.Esito switch
    {
        EsitoAccesso.ServeCodice2fa => Results.Json(new { serveCodice2fa = true }, statusCode: 401),
        EsitoAccesso.Codice2faErrato => Results.Json(
            new { serveCodice2fa = true, errore = "Codice 2FA non valido." }, statusCode: 401),
        EsitoAccesso.AccountSospeso => Results.Json(
            new { errore = "Studio sospeso. Contatta l'amministratore della piattaforma." }, statusCode: 403),
        _ => Results.Json(new { errore = "Email o password non corretti." }, statusCode: 401),
    };
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/mobile/refresh", async (
    RefreshMobileRequest req, SessioniRefreshService sessioni, TokenService tokens,
    IServiceScopeFactory scopeFactory, HttpContext http) =>
{
    var risultato = await sessioni.RuotaAsync(req.RefreshToken, "mobile", http.RequestAborted);
    if (risultato.Esito == EsitoRotazioneRefresh.Ok)
        await RegistraAutenticazioneSenzaBloccareAsync(
            scopeFactory, risultato.Sessione!.Utente.TenantId, risultato.Sessione.Utente.Id,
            "Riuscito", StatusCodes.Status200OK, http, "RinnovoSessioneMobile");
    return risultato.Esito switch
    {
        EsitoRotazioneRefresh.Ok => Results.Ok(CreaRispostaAccessoMobile(tokens, risultato.Sessione!)),
        EsitoRotazioneRefresh.Collisione => Results.Json(
            new { errore = "Aggiornamento sessione già in corso." }, statusCode: 409),
        EsitoRotazioneRefresh.AccountNonAttivo => Results.Json(
            new { errore = "Account non attivo." }, statusCode: 403),
        _ => Results.Json(new { errore = "Sessione scaduta. Accedi di nuovo." }, statusCode: 401),
    };
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/mobile/logout", async (
    RefreshMobileRequest req, SessioniRefreshService sessioni,
    IServiceScopeFactory scopeFactory, HttpContext http) =>
{
    var revocata = await sessioni.RevocaDaTokenAsync(req.RefreshToken, "Logout", http.RequestAborted);
    if (revocata is not null)
        await RegistraAutenticazioneSenzaBloccareAsync(
            scopeFactory, revocata.TenantId, revocata.UtenteId,
            "Riuscito", StatusCodes.Status204NoContent, http, "LogoutMobile");
    return Results.NoContent();
}).RequireRateLimiting("auth");

// Accesso con Google (Sign in with Google): il frontend invia il "credential" (ID token Google);
// lo verifichiamo tramite l'endpoint tokeninfo di Google (aud = nostro Client ID, email verificata),
// poi accediamo/creiamo l'utente per quell'email. Richiede la config "Google:ClientId".
app.MapPost("/api/auth/google", async (
    GoogleLoginRequest req, IHttpClientFactory httpFactory, IConfiguration cfg,
    AuthService auth, TokenService tokens, SessioniRefreshService sessioni,
    IServiceScopeFactory scopeFactory, HttpContext http) =>
{
    if (!VerificaRichiestaWeb(http))
        return Results.BadRequest(new { errore = "Client web non riconosciuto." });
    var clientId = cfg["Google:ClientId"];
    if (string.IsNullOrWhiteSpace(clientId))
        return Results.Json(new { errore = "Accesso con Google non configurato (manca Google:ClientId)." }, statusCode: 501);
    if (string.IsNullOrWhiteSpace(req.Credential))
        return Results.BadRequest(new { errore = "Credenziale Google mancante." });

    GoogleTokenInfo? info;
    try
    {
        var client = httpFactory.CreateClient();
        info = await client.GetFromJsonAsync<GoogleTokenInfo>(
            $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(req.Credential)}");
    }
    catch
    {
        return Results.Json(new { errore = "Token Google non valido." }, statusCode: 401);
    }

    if (info is null || info.Aud != clientId || info.EmailVerified != "true" || string.IsNullOrEmpty(info.Email))
        return Results.Json(new { errore = "Token Google non valido o email non verificata." }, statusCode: 401);

    var utente = await auth.AccediConGoogleAsync(info.Email, info.Name ?? info.Email);
    var sessione = await sessioni.CreaAsync(utente, "web", http.RequestAborted);
    ScriviCookieRefresh(http, sessione);
    await RegistraAutenticazioneSenzaBloccareAsync(
        scopeFactory, utente.TenantId, utente.Id, "Riuscito", StatusCodes.Status200OK, http);
    return Results.Ok(CreaRispostaAccesso(tokens, sessione));
}).RequireRateLimiting("auth");

// --- Super-admin di piattaforma: gestione/sospensione studi (solo ruolo superadmin) ----
static bool ÈSuperAdmin(ClaimsPrincipal u) =>
    u.IsInRole(AuthService.RuoloSuperAdmin)
    || u.FindFirst(ClaimTypes.Role)?.Value == AuthService.RuoloSuperAdmin
    || u.FindFirst("role")?.Value == AuthService.RuoloSuperAdmin;

// Bootstrap del super-admin: unico canale per ottenere il ruolo. Richiede utente autenticato
// e il token segreto 'SuperAdmin:BootstrapToken'; funziona una sola volta (finché non esiste
// già un super-admin). Sostituisce la vecchia promozione automatica via email registrabile.
secured.MapPost("/api/admin/bootstrap-superadmin",
    async (BootstrapSuperAdminRequest req, ClaimsPrincipal user, AuthService auth) =>
{
    if (UtenteCorrente(user) is not { } uid) return Results.Unauthorized();
    try
    {
        return await auth.BootstrapSuperAdminAsync(uid, req.Token)
            ? Results.Ok(new { promosso = true })
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { errore = ex.Message });
    }
}).RequireRateLimiting("auth");

secured.MapGet("/api/admin/studi", async (ClaimsPrincipal user, PiattaformaAdminService svc) =>
    ÈSuperAdmin(user)
        ? Results.Ok(await svc.ElencaStudiAsync())
        : Results.StatusCode(StatusCodes.Status403Forbidden));

secured.MapPost("/api/admin/studi/{id:guid}/sospendi",
    async (Guid id, ClaimsPrincipal user, PiattaformaAdminService svc) =>
{
    if (!ÈSuperAdmin(user)) return Results.StatusCode(StatusCodes.Status403Forbidden);
    return await svc.ImpostaSospensioneAsync(id, true)
        ? Results.Ok(new { id, sospeso = true })
        : Results.NotFound(new { errore = "Studio non trovato." });
});

secured.MapPost("/api/admin/studi/{id:guid}/riattiva",
    async (Guid id, ClaimsPrincipal user, PiattaformaAdminService svc) =>
{
    if (!ÈSuperAdmin(user)) return Results.StatusCode(StatusCodes.Status403Forbidden);
    return await svc.ImpostaSospensioneAsync(id, false)
        ? Results.Ok(new { id, sospeso = false })
        : Results.NotFound(new { errore = "Studio non trovato." });
});

// --- Secondo fattore (2FA / TOTP) — endpoint protetti per l'utente autenticato ---
static Guid? UtenteCorrente(ClaimsPrincipal user) =>
    Guid.TryParse(user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
        ? id : null;

secured.MapPost("/api/auth/2fa/inizia", async (ClaimsPrincipal user, AuthService auth) =>
{
    if (UtenteCorrente(user) is not { } uid) return Results.Unauthorized();
    try
    {
        var conf = await auth.IniziaDueFattoriAsync(uid);
        return Results.Ok(new { conf.Segreto, conf.UriOtpauth });
    }
    catch (InvalidOperationException ex) // 2FA già attivo: va prima disattivato con un codice valido
    {
        return Results.Conflict(new { errore = ex.Message });
    }
}).RequireRateLimiting("auth");

secured.MapPost("/api/auth/2fa/attiva", async (Codice2faRequest req, ClaimsPrincipal user, AuthService auth) =>
{
    if (UtenteCorrente(user) is not { } uid) return Results.Unauthorized();
    return await auth.AttivaDueFattoriAsync(uid, req.Codice)
        ? Results.Ok(new { dueFattoriAttivo = true })
        : Results.BadRequest(new { errore = "Codice non valido." });
}).RequireRateLimiting("auth");

secured.MapPost("/api/auth/2fa/disattiva", async (Codice2faRequest req, ClaimsPrincipal user, AuthService auth) =>
{
    if (UtenteCorrente(user) is not { } uid) return Results.Unauthorized();
    return await auth.DisattivaDueFattoriAsync(uid, req.Codice)
        ? Results.Ok(new { dueFattoriAttivo = false })
        : Results.BadRequest(new { errore = "Codice non valido." });
}).RequireRateLimiting("auth");

// Elenco dei livelli di un CCNL per un dato anno (default: anno corrente del seed).
app.MapGet("/api/ccnl/{codice}/livelli", async (string codice, int? anno, ParametriNormativiService svc) =>
{
    try
    {
        var ccnl = await svc.CaricaCcnlAsync(codice, anno ?? 2026);
        var livelli = ccnl.Livelli
            .OrderBy(l => l.Codice)
            .Select(l => new { l.Codice, l.Descrizione, l.MinimoTabellare, l.Edr, l.ImportoScatto });
        return Results.Ok(new { ccnl.Codice, ccnl.Descrizione, livelli });
    }
    catch (ArgumentException ex)
    {
        return Results.NotFound(new { errore = ex.Message });
    }
});

// Calcolo del cedolino lordo -> netto. I parametri sono caricati dal DB per l'anno del cartellino.
secured.MapPost("/api/cedolini/calcola", async (CalcolaCedolinoRequest req, ParametriNormativiService svc) =>
{
    if (req.Rapporto is null || req.Cartellino is null)
        return Results.BadRequest(new { errore = "Rapporto e cartellino sono obbligatori." });

    try
    {
        var motore = await CostruisciMotore(svc, req.Rapporto, req.Cartellino.Anno);
        return Results.Ok(motore.Calcola(req.Rapporto, req.Cartellino));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

// Genera l'F24 da versare a partire dagli importi delle paghe (ritenute IRPEF, addizionali, INPS).
// Restituisce il modello, l'esito dei controlli interni e una bozza leggibile (NON ufficiale).
secured.MapPost("/api/f24/da-paghe", (F24DaPagheRequest req) =>
{
    if (req.Sostituto is null || req.Righe is null || req.Righe.Count == 0)
        return Results.BadRequest(new { errore = "Sostituto e almeno una riga di paga sono obbligatori." });

    try
    {
        var f24 = F24DaCedolini.Costruisci(
            req.Sostituto, req.Righe, req.AnnoPeriodo, req.MesePeriodo, req.DataVersamento);
        var esito = ValidazioneF24.Verifica(f24);
        return Results.Ok(new
        {
            f24.DataVersamento,
            f24.TotaleDebiti,
            f24.TotaleCrediti,
            f24.SaldoFinale,
            valido = esito.Valido,
            errori = esito.Errori,
            f24.Erario,
            f24.Inps,
            f24.Regioni,
            f24.TributiLocali,
            bozza = new BozzaLeggibileF24Writer().Genera(f24),
        });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

// Calcolo di imponibili, IVA e totali di una fattura.
secured.MapPost("/api/fatture/calcola", (Fattura fattura) =>
{
    if (fattura.Righe.Count == 0)
        return Results.BadRequest(new { errore = "La fattura non ha righe." });
    return Results.Ok(CalcoloFattura.Calcola(fattura));
});

// Generazione del tracciato FatturaPA XML (subset FPR12).
secured.MapPost("/api/fatture/xml", (Fattura fattura) =>
{
    if (fattura.Righe.Count == 0)
        return Results.BadRequest(new { errore = "La fattura non ha righe." });
    string xml = FatturaPaXml.Genera(fattura);
    return Results.Text(xml, "application/xml");
});

// Validazione FatturaPA: schema XSD ufficiale + controlli semantici SdI.
secured.MapPost("/api/fatture/valida", (Fattura fattura) =>
{
    if (fattura.Righe.Count == 0)
        return Results.BadRequest(new { errore = "La fattura non ha righe." });

    string xml = FatturaPaXml.Genera(fattura);
    var erroriXsd = FatturaPaValidator.Valida(xml);
    var erroriSdi = ControlliSdi.Verifica(fattura);

    return Results.Ok(new
    {
        valido = erroriXsd.Count == 0 && erroriSdi.Count == 0,
        erroriXsd,
        erroriSdi
    });
});

// Firma digitale CAdES-BES (busta .p7m) di un documento qualsiasi (es. XML FatturaPA).
// multipart/form-data: campo "file" (obbligatorio); per firmare con un certificato software
// allegare "pfx" + "password"; per firmare con smart card/CNS configurata passare "pin".
app.MapPost("/api/firma/cades", async (
    HttpRequest http, FirmaOptions firma, ILoggerFactory loggerFactory) =>
{
    if (!http.HasFormContentType)
        return Results.BadRequest(new { errore = "Inviare multipart/form-data con il campo 'file'." });

    var form = await http.ReadFormAsync();
    var file = form.Files["file"];
    if (SicurezzaUploadFirma.ValidaDimensione(
            file, SicurezzaUploadFirma.DimensioneMassimaDocumento, "File da firmare") is { } erroreFile)
        return Results.BadRequest(new { errore = erroreFile });

    byte[] contenuto = await LeggiFile(file!);
    var pfxFile = form.Files["pfx"];
    if (pfxFile is not null && SicurezzaUploadFirma.ValidaDimensione(
            pfxFile, SicurezzaUploadFirma.DimensioneMassimaPfx, "Certificato PFX") is { } errorePfx)
        return Results.BadRequest(new { errore = errorePfx });
    string? password = form["password"];
    string? pin = form["pin"];
    string formatoRichiesto = string.IsNullOrWhiteSpace(form["formato"])
        ? "cades" : form["formato"].ToString();
    if (!string.Equals(formatoRichiesto, "cades", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(formatoRichiesto, "xades", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { errore = "Formato firma non supportato: usare 'cades' o 'xades'." });
    if (pfxFile is not null && !string.IsNullOrWhiteSpace(pin))
        return Results.BadRequest(new { errore = "Scegliere un solo dispositivo di firma: PFX oppure CNS/PIN." });

    var tipo = string.Equals(formatoRichiesto, "xades", StringComparison.OrdinalIgnoreCase)
        ? TipoFirma.Xades : TipoFirma.Cades;

    IFirmatoreDigitale? firmatore = null;
    try
    {
        if (pfxFile is not null)
        {
            byte[] pfx = await LeggiFile(pfxFile!);
            try
            {
                if (!SicurezzaUploadFirma.SembraPkcs12(pfx))
                    return Results.BadRequest(new { errore = "Il certificato caricato non è un PFX/P12 valido." });
                firmatore = FirmatorePfx.DaBytes(pfx, password ?? "");
            }
            finally
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(pfx);
            }
        }
        else
            firmatore = FirmaCadesLocale.CreaFirmatore(firma, pin);

        var risultato = firmatore!.Firma(contenuto, file!.FileName, tipo);
        return Results.File(risultato.ContenutoFirmato, risultato.Mime, risultato.NomeFile);
    }
    catch (Exception ex)
    {
        loggerFactory.CreateLogger("FirmaDigitale").LogWarning(
            ex, "Firma documento non riuscita. TraceId: {TraceId}", http.HttpContext.TraceIdentifier);
        return Results.BadRequest(new
        {
            errore = "Firma non riuscita. Verificare certificato, password/PIN e configurazione.",
            riferimento = http.HttpContext.TraceIdentifier,
        });
    }
    finally { (firmatore as IDisposable)?.Dispose(); }
}).RequireAuthorization().RequireRateLimiting("sensibili").DisableAntiforgery()
  .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(
      SicurezzaUploadFirma.DimensioneMassimaRichiesta));

// Verifica un documento firmato (.p7m CAdES o XML XAdES): riconosce il formato, controlla la
// validità della firma e restituisce firmatario e data. multipart/form-data con campo "file".
app.MapPost("/api/firma/verifica", async (HttpRequest http, ILoggerFactory loggerFactory) =>
{
    if (!http.HasFormContentType)
        return Results.BadRequest(new { errore = "Inviare multipart/form-data con il campo 'file'." });

    var form = await http.ReadFormAsync();
    var file = form.Files["file"];
    if (SicurezzaUploadFirma.ValidaDimensione(
            file, SicurezzaUploadFirma.DimensioneMassimaDocumentoFirmato, "File firmato") is { } erroreFile)
        return Results.BadRequest(new { errore = erroreFile });

    byte[] documento = await LeggiFile(file!);
    if (!SicurezzaUploadFirma.SembraDocumentoFirmato(documento))
        return Results.BadRequest(new { errore = "Il file non è una firma CAdES o XAdES riconoscibile." });

    try
    {
        var r = MotoreFirma.Verifica(documento);
        return Results.Ok(new
        {
            valido = r.Valido,
            formato = r.Formato,
            firmatario = r.Firmatario,
            dataFirmaUtc = r.DataFirmaUtc,
            algoritmoDigest = r.AlgoritmoDigest,
        });
    }
    catch (Exception ex)
    {
        loggerFactory.CreateLogger("FirmaDigitale").LogWarning(
            ex, "Verifica firma non riuscita. TraceId: {TraceId}", http.HttpContext.TraceIdentifier);
        return Results.BadRequest(new
        {
            errore = "Verifica non riuscita: il file firmato non è valido o supportato.",
            riferimento = http.HttpContext.TraceIdentifier,
        });
    }
}).RequireAuthorization().RequireRateLimiting("sensibili").DisableAntiforgery()
  .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(
      SicurezzaUploadFirma.DimensioneMassimaDocumentoFirmato + 64 * 1024));

// Genera l'XML FatturaPA, lo valida e lo firma in CAdES (.xml.p7m) con il certificato configurato
// nella sezione "Firma". Per la CNS il PIN può essere passato nell'header "X-Firma-Pin".
app.MapPost("/api/fatture/firma", async (
    Fattura fattura, HttpRequest http, FirmaOptions firma, string? formato,
    ILoggerFactory loggerFactory) =>
{
    if (fattura.Righe.Count == 0)
        return Results.BadRequest(new { errore = "La fattura non ha righe." });
    if (!string.IsNullOrWhiteSpace(formato)
        && !string.Equals(formato, "cades", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(formato, "xades", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { errore = "Formato firma non supportato: usare 'cades' o 'xades'." });

    string xml = FatturaPaXml.Genera(fattura);
    var erroriXsd = FatturaPaValidator.Valida(xml);
    var erroriSdi = ControlliSdi.Verifica(fattura);
    if (erroriXsd.Count > 0 || erroriSdi.Count > 0)
        return Results.BadRequest(new { errore = "Fattura non valida: firma annullata.", erroriXsd, erroriSdi });

    var tipo = string.Equals(formato, "xades", StringComparison.OrdinalIgnoreCase)
        ? TipoFirma.Xades : TipoFirma.Cades;

    IFirmatoreDigitale? firmatore = null;
    try
    {
        firmatore = FirmaCadesLocale.CreaFirmatore(firma, http.Headers["X-Firma-Pin"]);
        string idCodice = fattura.Cedente.PartitaIva ?? fattura.Cedente.CodiceFiscale ?? "00000000000";
        string nomeXml = NomeFileFatturaPa.Genera(
            fattura.Cedente.Nazione, idCodice, NomeFileFatturaPa.ProgressivoCasuale());

        var risultato = firmatore.Firma(Encoding.UTF8.GetBytes(xml), nomeXml, tipo);
        return Results.File(risultato.ContenutoFirmato, risultato.Mime, risultato.NomeFile);
    }
    catch (Exception ex)
    {
        loggerFactory.CreateLogger("FirmaDigitale").LogWarning(
            ex, "Firma fattura non riuscita. TraceId: {TraceId}", http.HttpContext.TraceIdentifier);
        return Results.BadRequest(new
        {
            errore = "Firma non riuscita. Verificare certificato/PIN e configurazione.",
            riferimento = http.HttpContext.TraceIdentifier,
        });
    }
    finally { (firmatore as IDisposable)?.Dispose(); }
}).RequireAuthorization().RequireRateLimiting("sensibili");

// Riclassificazione del bilancio (Stato Patrimoniale + Conto Economico, schema CEE).
secured.MapPost("/api/bilancio/riclassifica", (RiclassificaRequest req) =>
{
    try
    {
        var bilancio = RiclassificazioneBilancio.Riclassifica(req.PianoConti, req.Saldi);
        return Results.Ok(bilancio);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

// --- Bilancio d'esercizio archiviato per azienda/anno -------------------------------
// Restituisce una sintesi del bilancio archiviato (per elenchi e creazione).
static object SintesiBilancio(Paghe.Persistence.Entities.BilancioEntity b) => new
{
    b.Id,
    b.Anno,
    b.Stato,
    b.Quadra,
    b.ValoreDellaProduzione,
    b.CostiDellaProduzione,
    b.UtilePerditaEsercizio,
    b.TotaleAttivo,
    b.TotalePassivo,
    b.PatrimonioNetto,
    b.SbilancioAttivoPassivo,
};

// Redige e archivia il bilancio dell'anno per l'azienda a partire dalle voci indicate.
secured.MapPost("/api/aziende/{aziendaId:guid}/bilanci",
    async (Guid aziendaId, CreaBilancioRequest req, BilancioService svc, ClaimsPrincipal user) =>
{
    try
    {
        var voci = (req.Voci ?? Array.Empty<VoceBilancioRequest>())
            .Select(v => new VoceBilancioInput(v.Codice, v.Descrizione, v.Voce, v.Importo))
            .ToList();
        var b = await svc.CreaAsync(TenantCorrente(user), aziendaId, req.Anno, voci);
        return Results.Created($"/api/bilanci/{b.Id}", SintesiBilancio(b));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

// Crea un bilancio d'esempio quadrato (piano dei conti CEE di base) — utile per demo/SRLS.
secured.MapPost("/api/aziende/{aziendaId:guid}/bilanci/esempio",
    async (Guid aziendaId, CreaBilancioEsempioRequest req, BilancioService svc, ClaimsPrincipal user) =>
{
    try
    {
        var b = await svc.CreaDaEsempioAsync(TenantCorrente(user), aziendaId, req.Anno);
        return Results.Created($"/api/bilanci/{b.Id}", SintesiBilancio(b));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

// Precompila le voci di Conto Economico dalla contabilità registrata (fatture, spese, cedolini).
secured.MapGet("/api/aziende/{aziendaId:guid}/bilanci/precompila",
    async (Guid aziendaId, int anno, BilancioService svc, ClaimsPrincipal user) =>
{
    try
    {
        var voci = await svc.PrecompilaDaContabilitaAsync(TenantCorrente(user), aziendaId, anno);
        return Results.Ok(new
        {
            anno,
            voci,
            nota = "Bozza dalle sole voci di Conto Economico ricavate dalla contabilità. "
                + "Aggiungere le voci di Stato Patrimoniale per completare il bilancio.",
        });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

secured.MapGet("/api/aziende/{aziendaId:guid}/bilanci",
    async (Guid aziendaId, BilancioService svc, ClaimsPrincipal user) =>
        Results.Ok((await svc.ElencoAsync(TenantCorrente(user), aziendaId)).Select(SintesiBilancio)));

secured.MapGet("/api/bilanci/{id:guid}", async (Guid id, BilancioService svc, ClaimsPrincipal user) =>
{
    var b = await svc.GetAsync(TenantCorrente(user), id);
    if (b is null)
        return Results.NotFound(new { errore = "Bilancio non trovato." });
    return Results.Ok(new
    {
        b.Id,
        b.Anno,
        b.Stato,
        b.Quadra,
        b.ValoreDellaProduzione,
        b.CostiDellaProduzione,
        b.ProventiFinanziari,
        b.OneriFinanziari,
        b.Imposte,
        b.UtilePerditaEsercizio,
        b.TotaleAttivo,
        b.TotalePassivo,
        b.PatrimonioNetto,
        b.SbilancioAttivoPassivo,
        b.Bozza,
        righe = b.Righe.Select(r => new { r.Codice, r.Descrizione, r.Voce, r.Importo }),
        bilancio = JsonSerializer.Deserialize<JsonElement>(b.BilancioJson),
    });
});

// Fascicolo PDF del bilancio: intestazione, sintesi, SP affiancato, CE scalare, dettaglio conti.
secured.MapGet("/api/bilanci/{id:guid}/pdf",
    async (Guid id, BilancioService svc, PagheDbContext db, ClaimsPrincipal user) =>
{
    var b = await svc.GetAsync(TenantCorrente(user), id);
    if (b is null)
        return Results.NotFound(new { errore = "Bilancio non trovato." });

    var bilancio = JsonSerializer.Deserialize<Bilancio>(b.BilancioJson)!;
    var azienda = await db.Aziende.FirstAsync(a => a.Id == b.AziendaId);
    byte[] pdf = BilancioPdf.Genera(
        bilancio, b.Anno, azienda.RagioneSociale, azienda.PartitaIva, b.Stato,
        b.Righe.Select(r => new RigaBilancioPdf(r.Codice, r.Descrizione, r.Voce, r.Importo)).ToList());
    return Results.File(pdf, "application/pdf", $"bilancio-{b.Anno}.pdf");
});

secured.MapPost("/api/bilanci/{id:guid}/stato",
    async (Guid id, CambiaStatoBilancioRequest req, BilancioService svc, ClaimsPrincipal user) =>
{
    if (!Enum.TryParse<StatoBilancio>(req.Stato, ignoreCase: true, out var nuovo))
        return Results.BadRequest(new { errore = $"Stato '{req.Stato}' non valido." });
    try
    {
        var b = await svc.CambiaStatoAsync(TenantCorrente(user), id, nuovo);
        return Results.Ok(new { b.Id, b.Stato });
    }
    catch (ArgumentException ex) { return Results.NotFound(new { errore = ex.Message }); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { errore = ex.Message }); }
});

secured.MapPost("/api/contabilita/iva/liquidazione", (LiquidazioneIvaRequest req) =>
{
    try
    {
        return Results.Ok(CalcoloLiquidazioneIva.Calcola(
            req.Dal,
            req.Al,
            req.Periodicita,
            req.FattureAttive,
            req.Spese,
            req.CreditoPeriodoPrecedente));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

// --- Aziende (persistite) ----------------------------------------------------------
secured.MapPost("/api/aziende", async (CreaAziendaRequest req, AziendeService svc, ClaimsPrincipal user) =>
{
    try
    {
        var azienda = await svc.CreaAsync(TenantCorrente(user), new Paghe.Persistence.Entities.AziendaEntity
        {
            RagioneSociale = req.RagioneSociale,
            PartitaIva = req.PartitaIva,
            CodiceFiscale = req.CodiceFiscale,
            Indirizzo = req.Indirizzo,
            Cap = req.Cap,
            Comune = req.Comune,
            Provincia = req.Provincia,
            Nazione = req.Nazione,
            RegimeFiscale = req.RegimeFiscale,
            Telefono = req.Telefono,
            Email = req.Email,
            Pec = req.Pec,
            CodiceDestinatarioSdi = req.CodiceDestinatarioSdi,
            CodiceCatastale = req.CodiceCatastale,
            Settore = req.Settore,
            CodiceAteco = req.CodiceAteco,
            MatricolaInps = req.MatricolaInps,
            CodiceSedeInps = req.CodiceSedeInps,
            CscInps = req.CscInps,
            PatInail = req.PatInail,
            CcnlDefault = req.CcnlDefault,
            LivelloDefault = req.LivelloDefault,
            Iban = req.Iban,
            LegaleRappresentanteNome = req.LegaleRappresentanteNome,
            LegaleRappresentanteCognome = req.LegaleRappresentanteCognome,
            LegaleRappresentanteCodiceFiscale = req.LegaleRappresentanteCodiceFiscale,
            CanalePredefinito = req.CanalePredefinito,
            HaDipendenti = req.HaDipendenti,
            TipoContribuente = req.TipoContribuente,
            AssoggettataIva = req.AssoggettataIva,
        });
        return Results.Created($"/api/aziende/{azienda.Id}", new { azienda.Id, azienda.RagioneSociale });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { errore = ex.Message });
    }
});

secured.MapGet("/api/aziende", async (AziendeService svc, ClaimsPrincipal user) =>
    Results.Ok(await svc.ElencoAsync(TenantCorrente(user))));

// Dataset demo: solo account @paghe.local, per rendere la prova ripetibile in ogni browser.
secured.MapPost("/api/demo/dataset", async (
    DemoDatasetService svc, ClaimsPrincipal user, CancellationToken ct) =>
{
    var email = user.FindFirst("email")?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value ?? "";
    if (!email.EndsWith("@paghe.local", StringComparison.OrdinalIgnoreCase))
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    return Results.Ok(await svc.CreaSeMancanteAsync(TenantCorrente(user), ct));
});

secured.MapGet("/api/aziende/{id:guid}", async (Guid id, AziendeService svc, ClaimsPrincipal user) =>
{
    var sintesi = await svc.GetSintesiAsync(TenantCorrente(user), id);
    return sintesi is null
        ? Results.NotFound(new { errore = "Azienda non trovata." })
        : Results.Ok(sintesi);
});

// Correzione dell'anagrafica (es. dopo una visura/OCR letta male): aggiorna solo i campi inviati.
secured.MapPatch("/api/aziende/{id:guid}", async (
    Guid id, CorreggiAziendaRequest req, AziendeService svc, ClaimsPrincipal user) =>
{
    var a = await svc.CorreggiAsync(TenantCorrente(user), id,
        new AziendeService.CorrezioneAzienda(
            req.RagioneSociale, req.PartitaIva, req.CodiceFiscale,
            req.Indirizzo, req.Cap, req.Comune, req.Provincia,
            req.Telefono, req.Email, req.CanalePredefinito,
            req.HaDipendenti, req.TipoContribuente, req.AssoggettataIva,
            req.Pec, req.CodiceDestinatarioSdi, req.CodiceCatastale,
            req.Settore, req.CodiceAteco, req.MatricolaInps, req.CodiceSedeInps,
            req.CscInps, req.PatInail, req.CcnlDefault,
            req.LivelloDefault, req.Iban,
            req.LegaleRappresentanteNome, req.LegaleRappresentanteCognome,
            req.LegaleRappresentanteCodiceFiscale));
    return a is null
        ? Results.NotFound(new { errore = "Azienda non trovata per il tenant indicato." })
        : Results.Ok(new { a.Id, a.RagioneSociale });
});

secured.MapDelete("/api/aziende/{id:guid}", async (
    Guid id, AziendeService svc, ClaimsPrincipal user) =>
{
    var eliminata = await svc.EliminaAsync(TenantCorrente(user), id);
    return eliminata ? Results.NoContent() : Results.NotFound(new { errore = "Azienda non trovata." });
});

secured.MapPost("/api/aziende/elimina-bulk", async (
    EliminaAziendeBulkRequest req, AziendeService svc, ClaimsPrincipal user) =>
{
    var n = await svc.EliminaBulkAsync(TenantCorrente(user), req.Ids);
    return Results.Ok(new { eliminate = n });
});

// --- Logo aziendale (facoltativo, mostrato sui documenti: es. busta paga) ----------
secured.MapPost("/api/aziende/{id:guid}/logo", async (
    Guid id, HttpRequest http, AziendeService svc, ClaimsPrincipal user) =>
{
    if (!http.HasFormContentType)
        return Results.BadRequest(new { errore = "Inviare multipart/form-data con il campo 'file'." });
    var form = await http.ReadFormAsync();
    var file = form.Files["file"];
    if (file is null || file.Length == 0)
        return Results.BadRequest(new { errore = "Immagine del logo mancante (campo 'file')." });
    if (file.Length > 1_000_000)
        return Results.BadRequest(new { errore = "Logo troppo grande: massimo 1 MB." });

    byte[] logo = await LeggiFile(file);
    if (TipoImmagineLogo(logo) is null)
        return Results.BadRequest(new { errore = "Formato non supportato: usare un'immagine PNG o JPEG." });

    var salvato = await svc.SalvaLogoAsync(TenantCorrente(user), id, logo);
    return salvato ? Results.Ok(new { salvato = true }) : Results.NotFound(new { errore = "Azienda non trovata." });
}).RequireAuthorization().RequireRateLimiting("sensibili").DisableAntiforgery();

secured.MapGet("/api/aziende/{id:guid}/logo", async (Guid id, AziendeService svc, ClaimsPrincipal user) =>
{
    var azienda = await svc.GetAsync(TenantCorrente(user), id);
    if (azienda is null) return Results.NotFound(new { errore = "Azienda non trovata." });
    return azienda.Logo is null
        ? Results.NotFound(new { errore = "L'azienda non ha un logo." })
        : Results.File(azienda.Logo, TipoImmagineLogo(azienda.Logo) ?? "application/octet-stream");
});

secured.MapDelete("/api/aziende/{id:guid}/logo", async (Guid id, AziendeService svc, ClaimsPrincipal user) =>
{
    var rimosso = await svc.SalvaLogoAsync(TenantCorrente(user), id, null);
    return rimosso ? Results.NoContent() : Results.NotFound(new { errore = "Azienda non trovata." });
});

// --- Contabilità IVA persistita ---------------------------------------------------
secured.MapPost("/api/aziende/{aziendaId:guid}/spese",
    async (Guid aziendaId, SalvaSpesaRequest req, ContabilitaIvaService svc, ClaimsPrincipal user) =>
{
    try
    {
        var spesa = await svc.SalvaSpesaAsync(TenantCorrente(user), aziendaId, new SpesaInput(
            req.Data,
            req.Descrizione,
            req.ContoCosto,
            req.Imponibile,
            req.AliquotaIva,
            req.PercentualeDetraibilita,
            req.Fornitore,
            req.NumeroDocumento));
        return Results.Created($"/api/spese/{spesa.Id}", spesa);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

secured.MapGet("/api/aziende/{aziendaId:guid}/spese",
    async (Guid aziendaId, ContabilitaIvaService svc, ClaimsPrincipal user) =>
        Results.Ok(await svc.ElencoSpeseAsync(TenantCorrente(user), aziendaId)));

secured.MapGet("/api/spese/{id:guid}", async (Guid id, ContabilitaIvaService svc, ClaimsPrincipal user) =>
{
    var spesa = await svc.GetSpesaAsync(TenantCorrente(user), id);
    return spesa is null ? Results.NotFound(new { errore = "Spesa non trovata." }) : Results.Ok(spesa);
});

secured.MapPost("/api/aziende/{aziendaId:guid}/liquidazioni-iva",
    async (Guid aziendaId, CreaLiquidazioneIvaRequest req, ContabilitaIvaService svc, ClaimsPrincipal user) =>
{
    try
    {
        var liquidazione = await svc.CreaLiquidazioneAsync(TenantCorrente(user), aziendaId, new LiquidazioneIvaInput(
            req.Dal,
            req.Al,
            req.Periodicita,
            req.CreditoPeriodoPrecedente));
        return Results.Created($"/api/liquidazioni-iva/{liquidazione.Id}", liquidazione);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

secured.MapGet("/api/aziende/{aziendaId:guid}/liquidazioni-iva",
    async (Guid aziendaId, ContabilitaIvaService svc, ClaimsPrincipal user) =>
        Results.Ok(await svc.ElencoLiquidazioniAsync(TenantCorrente(user), aziendaId)));

secured.MapGet("/api/liquidazioni-iva/{id:guid}", async (Guid id, ContabilitaIvaService svc, ClaimsPrincipal user) =>
{
    var liquidazione = await svc.GetLiquidazioneAsync(TenantCorrente(user), id);
    return liquidazione is null ? Results.NotFound(new { errore = "Liquidazione IVA non trovata." }) : Results.Ok(liquidazione);
});

// Bozza dichiarazione IVA annuale (aggrega le liquidazioni periodiche dell'anno).
secured.MapGet("/api/aziende/{aziendaId:guid}/iva/dichiarazione-annuale/{anno:int}",
    async (Guid aziendaId, int anno, ContabilitaIvaService svc, ClaimsPrincipal user) =>
{
    try
    {
        return Results.Ok(await svc.CostruisciDichiarazioneIvaAnnualeAsync(TenantCorrente(user), aziendaId, anno));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

secured.MapGet("/api/aziende/{aziendaId:guid}/iva/dichiarazione-annuale/{anno:int}/export.csv",
    async (Guid aziendaId, int anno, ContabilitaIvaService svc, ClaimsPrincipal user) =>
{
    try
    {
        var bozza = await svc.CostruisciDichiarazioneIvaAnnualeAsync(TenantCorrente(user), aziendaId, anno);
        return Results.Text(ContabilitaIvaService.EsportaCsvDichiarazioneIva(bozza), "text/csv");
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

// --- Pratiche apertura P.IVA / Camera di Commercio --------------------------------
static PraticaAperturaPartitaIvaInput ToInput(CreaPraticaAperturaPartitaIvaRequest req) => new(
    req.FormaGiuridica,
    req.RegimeContabile,
    req.DataInizioAttivita,
    req.Denominazione,
    req.CodiceFiscale,
    req.CodiceAteco,
    req.DescrizioneAttivita,
    req.IndirizzoSede,
    req.Cap,
    req.Comune,
    req.Provincia,
    req.PartitaIva,
    req.Pec,
    req.AttivitaCommercio,
    req.AttivitaArtigiana,
    req.HaDipendentiPrevisti,
    req.SoggettaInail,
    req.AziendaId,
    req.TipoPratica,
    req.ImpresaAgricola);

secured.MapPost("/api/pratiche/apertura-partita-iva/simula", (CreaPraticaAperturaPartitaIvaRequest req) =>
{
    try
    {
        return Results.Ok(PraticheAperturaPartitaIvaService.Simula(ToInput(req)));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

secured.MapPost("/api/pratiche/apertura-partita-iva",
    async (CreaPraticaAperturaPartitaIvaRequest req, PraticheAperturaPartitaIvaService svc, ClaimsPrincipal user) =>
{
    try
    {
        var pratica = await svc.CreaAsync(TenantCorrente(user), ToInput(req));
        return Results.Created($"/api/pratiche/apertura-partita-iva/{pratica.Id}", pratica);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

secured.MapGet("/api/pratiche/apertura-partita-iva",
    async (PraticheAperturaPartitaIvaService svc, ClaimsPrincipal user) =>
        Results.Ok(await svc.ElencoAsync(TenantCorrente(user))));

secured.MapGet("/api/pratiche/apertura-partita-iva/{id:guid}",
    async (Guid id, PraticheAperturaPartitaIvaService svc, ClaimsPrincipal user) =>
{
    var pratica = await svc.GetAsync(TenantCorrente(user), id);
    return pratica is null ? Results.NotFound(new { errore = "Pratica non trovata." }) : Results.Ok(pratica);
});

secured.MapPatch("/api/pratiche/apertura-partita-iva/{praticaId:guid}/adempimenti/{adempimentoId:guid}",
    async (Guid praticaId, Guid adempimentoId, AggiornaAdempimentoPraticaRequest req,
        PraticheAperturaPartitaIvaService svc, ClaimsPrincipal user) =>
{
    try
    {
        var adempimento = await svc.AggiornaAdempimentoAsync(
            TenantCorrente(user), praticaId, adempimentoId, req.Stato, req.Note);
        return Results.Ok(adempimento);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

secured.MapPost("/api/pratiche/apertura-partita-iva/{id:guid}/stato",
    async (Guid id, CambiaStatoPraticaComUnicaRequest req,
        PraticheAperturaPartitaIvaService svc, ClaimsPrincipal user) =>
{
    try
    {
        return Results.Ok(await svc.CambiaStatoAsync(TenantCorrente(user), id, req.Stato));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

secured.MapGet("/api/pratiche/apertura-partita-iva/{id:guid}/distinta",
    async (Guid id, PraticheAperturaPartitaIvaService svc, ClaimsPrincipal user) =>
{
    try
    {
        return Results.Ok(new { distinta = await svc.DistintaAsync(TenantCorrente(user), id) });
    }
    catch (ArgumentException ex)
    {
        return Results.NotFound(new { errore = ex.Message });
    }
});

// Analizza testo libero (email, appunti, messaggio del cliente) e propone i dati della pratica.
secured.MapPost("/api/pratiche/apertura-partita-iva/analizza-testo",
    (AnalizzaTestoPraticaRequest req) => Results.Ok(EstrattoreTestoPratica.Estrai(req.Testo ?? "")));

// --- Fatture (persistite, numerazione automatica) ----------------------------------
secured.MapPost("/api/aziende/{aziendaId:guid}/fatture",
    async (Guid aziendaId, SalvaFatturaRequest req, FattureService svc, ClaimsPrincipal user) =>
{
    try
    {
        var input = new FatturaInput(
            Data: req.Data,
            Cessionario: req.Cessionario,
            Righe: req.Righe,
            Numero: req.Numero,
            TipoDocumento: req.TipoDocumento,
            CodiceDestinatario: req.CodiceDestinatario);
        var f = await svc.SalvaAsync(TenantCorrente(user), aziendaId, input);
        return Results.Created($"/api/fatture/{f.Id}",
            new { f.Id, f.Numero, f.TotaleImponibile, f.TotaleImposta, f.TotaleDocumento });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

secured.MapGet("/api/aziende/{aziendaId:guid}/fatture",
    async (Guid aziendaId, FattureService svc, ClaimsPrincipal user) =>
        Results.Ok(await svc.ElencoAsync(TenantCorrente(user), aziendaId)));

secured.MapGet("/api/fatture/{id:guid}", async (Guid id, FattureService svc, ClaimsPrincipal user) =>
{
    var f = await svc.GetAsync(TenantCorrente(user), id);
    return f is null ? Results.NotFound(new { errore = "Fattura non trovata." }) : Results.Ok(f);
});

// --- Dipendenti e rapporti di lavoro (persistiti) ----------------------------------
secured.MapPost("/api/aziende/{aziendaId:guid}/dipendenti",
    async (Guid aziendaId, CreaDipendenteRequest req, AnagraficheLavoroService svc, ClaimsPrincipal user) =>
{
    try
    {
        var dipendente = new Paghe.Persistence.Entities.DipendenteEntity
        {
            CodiceFiscale = req.CodiceFiscale,
            Nome = req.Nome,
            Cognome = req.Cognome,
            Iban = req.Iban,
        };
        var rapporto = new Paghe.Persistence.Entities.RapportoLavoroEntity
        {
            CodiceCcnl = req.CodiceCcnl,
            Livello = req.Livello,
            Qualifica = req.Qualifica,
            NumeroMensilita = req.NumeroMensilita,
            PartTime = req.PartTime,
            PercentualePartTime = req.PercentualePartTime,
            ScattiAnzianita = req.ScattiAnzianita,
            Superminimo = req.Superminimo,
            Regione = req.Regione,
            CodiceCatastaleComune = req.CodiceCatastaleComune,
            DetrazioniLavoroDipendente = req.DetrazioniLavoroDipendente,
            GiorniDetrazioneAnno = req.GiorniDetrazioneAnno,
            DataAssunzione = req.DataAssunzione ?? DateOnly.FromDateTime(DateTime.UtcNow),
            OreSettimanali = req.OreSettimanali,
            TipoContratto = req.TipoContratto,
        };
        var creato = await svc.CreaDipendenteERapportoAsync(TenantCorrente(user), aziendaId, dipendente, rapporto);
        return Results.Created($"/api/rapporti/{creato.Id}",
            new { rapportoId = creato.Id, dipendenteId = creato.DipendenteId, req.Nome, req.Cognome });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

// Crea dipendente + rapporto leggendo in automatico un UNILAV (Comunicazione Obbligatoria).
// Il corpo della richiesta è l'XML UNILAV (Content-Type: application/xml).
secured.MapPost("/api/aziende/{aziendaId:guid}/dipendenti/da-unilav",
    async (Guid aziendaId, HttpRequest http, AnagraficheLavoroService svc, ClaimsPrincipal user) =>
{
    using var reader = new StreamReader(http.Body);
    string xml = await reader.ReadToEndAsync();

    try
    {
        var imp = ImportazioneUnilav.Importa(xml);

        if (string.IsNullOrWhiteSpace(imp.Lavoratore.CodiceFiscale))
            return Results.BadRequest(new
            {
                errore = "UNILAV incompleto: codice fiscale del lavoratore non riconosciuto.",
                riconosciuto = imp,
            });

        // Lancia ArgumentException elencando i dati mancanti (es. CCNL, Livello).
        var rl = ImportazioneUnilav.CostruisciRapportoLavoro(imp);

        var dipendente = new Paghe.Persistence.Entities.DipendenteEntity
        {
            CodiceFiscale = imp.Lavoratore.CodiceFiscale!,
            Nome = imp.Lavoratore.Nome ?? "",
            Cognome = imp.Lavoratore.Cognome ?? "",
        };
        var rapporto = new Paghe.Persistence.Entities.RapportoLavoroEntity
        {
            CodiceCcnl = rl.CodiceCcnl,
            Livello = rl.Livello,
            Qualifica = rl.Qualifica.ToString(),
            NumeroMensilita = rl.NumeroMensilita,
            PartTime = rl.PartTime,
            PercentualePartTime = rl.PercentualePartTime,
            ScattiAnzianita = rl.ScattiAnzianita,
            Superminimo = rl.Superminimo,
            Regione = rl.Regione,
            CodiceCatastaleComune = rl.CodiceCatastaleComune,
            DetrazioniLavoroDipendente = rl.DetrazioniLavoroDipendente,
            GiorniDetrazioneAnno = rl.GiorniDetrazioneAnno,
            DataAssunzione = imp.Rapporto.DataInizio ?? DateOnly.FromDateTime(DateTime.UtcNow),
        };

        var creato = await svc.CreaDipendenteERapportoAsync(TenantCorrente(user), aziendaId, dipendente, rapporto);
        return Results.Created($"/api/rapporti/{creato.Id}", new
        {
            rapportoId = creato.Id,
            dipendenteId = creato.DipendenteId,
            nominativo = $"{dipendente.Cognome} {dipendente.Nome}".Trim(),
            rapporto.CodiceCcnl,
            rapporto.Livello,
            avvertenze = imp.Avvertenze,
        });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

secured.MapGet("/api/aziende/{aziendaId:guid}/dipendenti",
    async (Guid aziendaId, AnagraficheLavoroService svc, ClaimsPrincipal user) =>
{
    var rapporti = await svc.ElencoRapportiAsync(TenantCorrente(user), aziendaId);
    return Results.Ok(rapporti.Select(r => new
    {
        rapportoId = r.Id,
        r.DipendenteId,
        nominativo = $"{r.Dipendente?.Cognome} {r.Dipendente?.Nome}".Trim(),
        r.CodiceCcnl,
        r.Livello,
        r.Qualifica
    }));
});

// GDPR — diritto all'oblio: anonimizza i dati personali del dipendente (i cedolini restano).
secured.MapPost("/api/dipendenti/{id:guid}/anonimizza",
    async (Guid id, AnagraficheLavoroService svc, ClaimsPrincipal user) =>
{
    var fatto = await svc.AnonimizzaDipendenteAsync(TenantCorrente(user), id);
    return fatto ? Results.Ok(new { anonimizzato = true }) : Results.NotFound();
});

// Ravvedimento operoso: sanzione ridotta + interessi legali per un versamento tardivo.
// Usa gli scaglioni di ESEMPIO (storico, sanzione base 30%): da validare con un Consulente del Lavoro.
secured.MapPost("/api/ravvedimento/calcola", (RavvedimentoRequest req) =>
{
    try
    {
        var r = Paghe.Core.Calcolo.Ravvedimento.Calcola(
            req.Importo, req.Scadenza, req.Versamento, req.TassoLegaleAnnuo,
            Paghe.Core.Calcolo.Ravvedimento.ScaglioniEsempioStorico);
        return Results.Ok(new
        {
            r.GiorniRitardo,
            r.ScaglioneApplicato,
            r.Tributo,
            r.Sanzione,
            r.Interessi,
            r.Totale,
            nota = "Scaglioni di esempio (storico 30%): validare con un Consulente del Lavoro.",
        });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

// Conguaglio fiscale IRPEF di fine anno per un rapporto (aggrega i cedolini archiviati dell'anno).
secured.MapGet("/api/rapporti/{rapportoId:guid}/conguaglio",
    async (Guid rapportoId, int anno, CedoliniService svc, ClaimsPrincipal user) =>
{
    try
    {
        return Results.Ok(await svc.CalcolaConguaglioAnnualeAsync(TenantCorrente(user), rapportoId, anno));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

// Rivalutazione annua del fondo TFR (variazione ISTAT dalla query o dai parametri dell'anno).
secured.MapGet("/api/rapporti/{rapportoId:guid}/tfr/rivalutazione",
    async (Guid rapportoId, int anno, decimal? variazioneIstat, CedoliniService svc, ClaimsPrincipal user) =>
{
    try
    {
        return Results.Ok(await svc.CalcolaRivalutazioneTfrAsync(
            TenantCorrente(user), rapportoId, anno, variazioneIstat));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

// --- Cedolini calcolati e archiviati -----------------------------------------------
// ?forza=true: consente il ricalcolo di un mese con F24 già inviato (correzione di un errore
// confermata dall'utente; l'F24 del mese andrà rigenerato).
secured.MapPost("/api/rapporti/{rapportoId:guid}/cedolini",
    async (Guid rapportoId, Cartellino cartellino, CedoliniService svc, ClaimsPrincipal user,
           bool forza = false) =>
{
    try
    {
        var c = await svc.CalcolaEArchiviaAsync(
            TenantCorrente(user), rapportoId, cartellino, forzaRicalcolo: forza);
        return Results.Created($"/api/cedolini/{c.Id}",
            new
            {
                c.Id,
                c.Anno,
                c.Mese,
                c.NettoInBusta,
                c.CostoAzienda,
                c.FerieResidue,
                c.PermessiResidui
            });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

secured.MapGet("/api/rapporti/{rapportoId:guid}/cedolini",
    async (Guid rapportoId, CedoliniService svc, ClaimsPrincipal user) =>
        Results.Ok(await svc.ElencoPerRapportoAsync(TenantCorrente(user), rapportoId)));

secured.MapGet("/api/cedolini/{id:guid}", async (Guid id, CedoliniService svc, ClaimsPrincipal user) =>
{
    var c = await svc.GetAsync(TenantCorrente(user), id);
    return c is null ? Results.NotFound(new { errore = "Cedolino non trovato." }) : Results.Ok(c);
});

// PDF di un cedolino archiviato: rigenerato dal backend (ricalcolo read-only), isolato per tenant.
secured.MapGet("/api/cedolini/{id:guid}/pdf", async (Guid id, CedoliniService svc, ClaimsPrincipal user) =>
{
    var dati = await svc.RicalcolaPerPdfAsync(TenantCorrente(user), id);
    if (dati is null) return Results.NotFound(new { errore = "Cedolino non trovato." });
    byte[] pdf = CedolinoPdf.Genera(dati.Cedolino, dati.Intestazione, dati.Cartellino);
    return Results.File(pdf, "application/pdf",
        $"cedolino-{dati.Cedolino.Anno}-{dati.Cedolino.Mese:00}.pdf");
});

// Genera l'F24 del mese pescando in automatico i cedolini archiviati dell'azienda.
secured.MapPost("/api/aziende/{aziendaId:guid}/f24/mese",
    async (Guid aziendaId, F24MeseRequest req, CedoliniService svc, ClaimsPrincipal user) =>
{
    try
    {
        var f24 = await svc.GeneraF24DelMeseAsync(
            TenantCorrente(user), aziendaId, req.Anno, req.Mese, req.MatricolaInps, req.CodiceSedeInps ?? "");
        var esito = ValidazioneF24.Verifica(f24);
        return Results.Ok(new
        {
            f24.DataVersamento,
            f24.TotaleDebiti,
            f24.TotaleCrediti,
            f24.SaldoFinale,
            valido = esito.Valido,
            errori = esito.Errori,
            f24.Erario,
            f24.Inps,
            f24.Regioni,
            f24.TributiLocali,
            bozza = new BozzaLeggibileF24Writer().Genera(f24),
        });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

// Genera il flusso UniEmens (denuncia mensile INPS) dai cedolini archiviati dell'azienda.
// BOZZA: il file va validato col software di controllo ufficiale INPS prima dell'invio.
secured.MapPost("/api/aziende/{aziendaId:guid}/uniemens/mese",
    async (Guid aziendaId, F24MeseRequest req, UniEmensService svc, ClaimsPrincipal user) =>
{
    try
    {
        var flusso = await svc.GeneraDelMeseAsync(
            TenantCorrente(user), aziendaId, req.Anno, req.Mese, req.MatricolaInps);
        var xml = Paghe.Core.AdeTelematico.UniEmens.UniEmensXmlWriter.GeneraStringa(flusso);
        return Results.Ok(new
        {
            req.Anno,
            req.Mese,
            lavoratori = flusso.Lavoratori.Count,
            totaleImponibile = flusso.Lavoratori.Sum(l => l.Imponibile),
            totaleContributo = flusso.Lavoratori.Sum(l => l.Contributo),
            avvertenza = "Bozza UniEmens NON validata: passare dal software di controllo ufficiale INPS prima dell'invio.",
            xml,
        });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

// --- F24: archiviazione pratica, stato ed export per Desktop Telematico -------------
secured.MapPost("/api/aziende/{aziendaId:guid}/f24/mese/salva",
    async (Guid aziendaId, F24MeseRequest req, PraticheF24Service svc, ClaimsPrincipal user) =>
{
    try
    {
        var p = await svc.GeneraEArchiviaDelMeseAsync(
            TenantCorrente(user), aziendaId, req.Anno, req.Mese, req.MatricolaInps, req.CodiceSedeInps ?? "");
        return Results.Created($"/api/f24/{p.Id}", new
        {
            p.Id,
            p.Anno,
            p.Mese,
            p.DataVersamento,
            p.TotaleDebiti,
            p.SaldoFinale,
            p.Stato,
            p.Valido,
        });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

secured.MapGet("/api/aziende/{aziendaId:guid}/f24",
    async (Guid aziendaId, PraticheF24Service svc, ClaimsPrincipal user) =>
        Results.Ok((await svc.ElencoAsync(TenantCorrente(user), aziendaId)).Select(p => new
        {
            p.Id,
            p.Anno,
            p.Mese,
            p.DataVersamento,
            p.TotaleDebiti,
            p.SaldoFinale,
            p.Stato,
            p.Valido,
        })));

secured.MapGet("/api/f24/{id:guid}", async (Guid id, PraticheF24Service svc, ClaimsPrincipal user) =>
{
    var p = await svc.GetAsync(TenantCorrente(user), id);
    if (p is null)
        return Results.NotFound(new { errore = "Pratica F24 non trovata." });
    return Results.Ok(new
    {
        p.Id,
        p.Anno,
        p.Mese,
        p.DataVersamento,
        p.TotaleDebiti,
        p.TotaleCrediti,
        p.SaldoFinale,
        p.Stato,
        p.Valido,
        p.Bozza,
        modello = JsonSerializer.Deserialize<JsonElement>(p.ModelloJson),
    });
});

secured.MapPost("/api/f24/{id:guid}/stato",
    async (Guid id, CambiaStatoF24Request req, PraticheF24Service svc, ClaimsPrincipal user) =>
{
    if (!Enum.TryParse<StatoPraticaTelematica>(req.Stato, out var nuovo))
        return Results.BadRequest(new { errore = $"Stato non valido: {req.Stato}." });
    try
    {
        var p = await svc.CambiaStatoAsync(TenantCorrente(user), id, nuovo);
        return Results.Ok(new { p.Id, p.Stato });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

secured.MapPost("/api/f24/{id:guid}/esporta", async (Guid id, PraticheF24Service svc, ClaimsPrincipal user) =>
{
    try
    {
        string file = await svc.EsportaAsync(TenantCorrente(user), id, "export");
        return Results.Ok(new { file = Path.GetFullPath(file) });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

// --- Documenti PDF -----------------------------------------------------------------
secured.MapPost("/api/cedolini/pdf", async (CedolinoPdfRequest req, ParametriNormativiService svc) =>
{
    try
    {
        var motore = await CostruisciMotore(svc, req.Rapporto, req.Cartellino.Anno);
        var cedolino = motore.Calcola(req.Rapporto, req.Cartellino);
        byte[] pdf = CedolinoPdf.Genera(
            cedolino,
            new IntestazioneCedolino { Azienda = req.Azienda, Dipendente = req.Dipendente },
            req.Cartellino);
        return Results.File(pdf, "application/pdf", $"cedolino-{cedolino.Anno}-{cedolino.Mese:00}.pdf");
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

secured.MapPost("/api/fatture/pdf", (Fattura fattura) =>
{
    if (fattura.Righe.Count == 0)
        return Results.BadRequest(new { errore = "La fattura non ha righe." });
    byte[] pdf = FatturaPdf.Genera(fattura);
    return Results.File(pdf, "application/pdf", $"fattura-{fattura.Numero.Replace('/', '-')}.pdf");
});

secured.MapGet("/api/fatture/{id:guid}/pdf", async (Guid id, FattureService svc, ClaimsPrincipal user) =>
{
    var fattura = await svc.GetDominioAsync(TenantCorrente(user), id);
    if (fattura is null)
        return Results.NotFound(new { errore = "Fattura non trovata." });
    byte[] pdf = FatturaPdf.Genera(fattura);
    return Results.File(pdf, "application/pdf", $"fattura-{fattura.Numero.Replace('/', '-')}.pdf");
});

// --- Trasmissione fattura elettronica (SdI) ----------------------------------------
secured.MapPost("/api/fatture/{id:guid}/trasmetti",
    async (Guid id, TrasmissioneFattureService svc, ClaimsPrincipal user) =>
{
    try
    {
        var t = await svc.TrasmettiAsync(TenantCorrente(user), id);
        return Results.Created($"/api/trasmissioni/{t.Id}", new
        {
            t.Id,
            t.Stato,
            t.IdentificativoSdi,
            t.NomeFileXml,
            t.Firmato,
            t.Provider,
            t.MessaggioErrore
        });
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { errore = ex.Message }); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { errore = ex.Message }); }
});

secured.MapGet("/api/fatture/{id:guid}/trasmissione",
    async (Guid id, TrasmissioneFattureService svc, ClaimsPrincipal user) =>
{
    var t = await svc.GetPerFatturaAsync(TenantCorrente(user), id);
    return t is null
        ? Results.NotFound(new { errore = "Nessuna trasmissione per questa fattura." })
        : Results.Ok(t);
});

// XML della fattura persistita: utile per firma/caricamento manuale sul portale ufficiale.
secured.MapGet("/api/fatture/{id:guid}/xml", async (Guid id, FattureService svc, ClaimsPrincipal user) =>
{
    var fattura = await svc.GetDominioAsync(TenantCorrente(user), id);
    if (fattura is null)
        return Results.NotFound(new { errore = "Fattura non trovata." });
    string xml = FatturaPaXml.Genera(fattura);
    return Results.Text(xml, "application/xml");
});

secured.MapGet("/api/trasmissioni",
    async (Guid? aziendaId, TrasmissioneFattureService svc, ClaimsPrincipal user) =>
        Results.Ok(await svc.ElencoAsync(TenantCorrente(user), aziendaId)));

secured.MapGet("/api/trasmissioni/{id:guid}",
    async (Guid id, TrasmissioneFattureService svc, ClaimsPrincipal user) =>
{
    var t = await svc.GetAsync(TenantCorrente(user), id);
    return t is null ? Results.NotFound(new { errore = "Trasmissione non trovata." }) : Results.Ok(t);
});

// Registrazione ricevuta/notifica SdI (RC/MC/NS/NE/DT): da webhook del provider o manuale.
secured.MapPost("/api/trasmissioni/{id:guid}/ricevute",
    async (Guid id, RegistraRicevutaSdiRequest req, TrasmissioneFattureService svc, ClaimsPrincipal user) =>
{
    try
    {
        var t = await svc.RegistraRicevutaAsync(TenantCorrente(user), id,
            new RicevutaSdiInput(req.Tipo, req.CodiceEsito, req.Descrizione, req.ContenutoGrezzo));
        return Results.Ok(new { t.Id, t.Stato, t.UltimoAggiornamento });
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { errore = ex.Message }); }
});

// Interroga il provider per l'esito aggiornato (per i canali che lo supportano).
secured.MapPost("/api/trasmissioni/{id:guid}/aggiorna-esito",
    async (Guid id, TrasmissioneFattureService svc, ClaimsPrincipal user) =>
{
    try
    {
        var t = await svc.AggiornaEsitoAsync(TenantCorrente(user), id);
        return Results.Ok(new { t.Id, t.Stato, t.IdentificativoSdi, t.UltimoAggiornamento });
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { errore = ex.Message }); }
});

// --- Deleghe intermediario ---------------------------------------------------------
secured.MapPost("/api/deleghe", async (CreaDelegaRequest req, DelegheService svc, ClaimsPrincipal user) =>
{
    try
    {
        var d = await svc.CreaAsync(TenantCorrente(user), new DelegaInput(
            req.AziendaId, req.TipoAdempimento, req.DataInizio, req.DataFine, req.Riferimento, req.Note));
        return Results.Created($"/api/deleghe/{d.Id}", d);
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { errore = ex.Message }); }
});

secured.MapGet("/api/deleghe", async (Guid? aziendaId, DelegheService svc, ClaimsPrincipal user) =>
    Results.Ok(await svc.ElencoAsync(TenantCorrente(user), aziendaId)));

secured.MapPost("/api/deleghe/{id:guid}/revoca", async (Guid id, DelegheService svc, ClaimsPrincipal user) =>
{
    try { return Results.Ok(await svc.RevocaAsync(TenantCorrente(user), id)); }
    catch (ArgumentException ex) { return Results.BadRequest(new { errore = ex.Message }); }
});

// --- Avvisi pagamento clienti / promemoria commercialista -------------------------
secured.MapPost("/api/aziende/{aziendaId:guid}/avvisi-pagamento",
    async (Guid aziendaId, CreaAvvisoPagamentoRequest req, AvvisiPagamentoService svc, ClaimsPrincipal user) =>
{
    try
    {
        var avviso = await svc.CreaAsync(TenantCorrente(user), new AvvisoPagamentoInput(
            aziendaId,
            req.Anno,
            req.Mese,
            req.Importo,
            req.DataScadenza,
            req.Descrizione,
            req.DataEmissione,
            req.MetodoPagamento,
            req.Iban,
            req.Causale,
            req.Note));
        return Results.Created($"/api/avvisi-pagamento/{avviso.Id}", AvvisoPagamentoDto(avviso));
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { errore = ex.Message }); }
});

secured.MapGet("/api/aziende/{aziendaId:guid}/avvisi-pagamento",
    async (Guid aziendaId, string? stato, bool? scaduti, AvvisiPagamentoService svc, ClaimsPrincipal user) =>
        Results.Ok((await svc.ElencoAsync(
            TenantCorrente(user), aziendaId, stato, scaduti ?? false)).Select(AvvisoPagamentoDto)));

secured.MapGet("/api/avvisi-pagamento",
    async (Guid? aziendaId, string? stato, bool? scaduti, AvvisiPagamentoService svc, ClaimsPrincipal user) =>
        Results.Ok((await svc.ElencoAsync(
            TenantCorrente(user), aziendaId, stato, scaduti ?? false)).Select(AvvisoPagamentoDto)));

secured.MapPost("/api/avvisi-pagamento/mese",
    async (CreaAvvisiPagamentoMeseRequest req, AvvisiPagamentoService svc, ClaimsPrincipal user) =>
{
    try
    {
        var creati = await svc.CreaMensilePerTuttiAsync(TenantCorrente(user), new AvvisiPagamentoMeseInput(
            req.Anno,
            req.Mese,
            req.Importo,
            req.DataScadenza,
            req.Descrizione,
            req.DataEmissione,
            req.MetodoPagamento,
            req.Iban,
            req.Causale,
            req.Note));
        return Results.Created("/api/avvisi-pagamento", new
        {
            creati = creati.Count,
            avvisi = creati.Select(AvvisoPagamentoDto),
        });
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { errore = ex.Message }); }
});

secured.MapGet("/api/avvisi-pagamento/promemoria",
    async (int? giorni, AvvisiPagamentoService svc, ClaimsPrincipal user) =>
{
    try
    {
        return Results.Ok((await svc.PromemoriaDaFareAsync(
            TenantCorrente(user), giorni ?? 7)).Select(AvvisoPagamentoDto));
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { errore = ex.Message }); }
});

secured.MapGet("/api/avvisi-pagamento/{id:guid}/avviso",
    async (Guid id, AvvisiPagamentoService svc, ClaimsPrincipal user) =>
{
    var avviso = await svc.GetAsync(TenantCorrente(user), id);
    return avviso is null
        ? Results.NotFound(new { errore = "Avviso di pagamento non trovato." })
        : Results.Ok(new
        {
            avviso = AvvisoPagamentoDto(avviso),
            testo = AvvisiPagamentoService.TestoAvviso(avviso),
        });
});

secured.MapPost("/api/avvisi-pagamento/{id:guid}/promemoria",
    async (Guid id, AvvisiPagamentoService svc, ClaimsPrincipal user) =>
{
    try
    {
        var avviso = await svc.RegistraPromemoriaAsync(TenantCorrente(user), id);
        return Results.Ok(new
        {
            avviso = AvvisoPagamentoDto(avviso),
            testo = AvvisiPagamentoService.TestoPromemoria(avviso),
        });
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { errore = ex.Message }); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { errore = ex.Message }); }
});

secured.MapPost("/api/avvisi-pagamento/{id:guid}/stato",
    async (Guid id, CambiaStatoAvvisoPagamentoRequest req, AvvisiPagamentoService svc, ClaimsPrincipal user) =>
{
    try
    {
        var tenantId = TenantCorrente(user);
        var avviso = await svc.CambiaStatoAsync(tenantId, id, new StatoAvvisoPagamentoInput(
            req.Stato,
            req.PagatoIl,
            req.RiferimentoPagamento,
            req.Note));
        var aggiornato = await svc.GetAsync(tenantId, avviso.Id) ?? avviso;
        return Results.Ok(AvvisoPagamentoDto(aggiornato));
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { errore = ex.Message }); }
});

// --- Scadenzario / promemoria (DURC, F24, manuali) --------------------------------
// Registra un DURC di un'azienda: la sua scadenza alimenta il promemoria di rinnovo.
secured.MapPost("/api/aziende/{aziendaId:guid}/durc",
    async (Guid aziendaId, RegistraDurcRequest req, ScadenzarioService svc, ClaimsPrincipal user) =>
{
    try
    {
        var d = await svc.RegistraDurcAsync(TenantCorrente(user), new DurcInput(
            aziendaId, req.Numero, req.DataEmissione, req.DataScadenza, req.Regolare, req.Note));
        return Results.Created($"/api/aziende/{aziendaId}/durc", new
        {
            d.Id,
            d.AziendaId,
            d.Numero,
            d.DataEmissione,
            d.DataScadenza,
            d.Regolare,
            d.Note,
        });
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { errore = ex.Message }); }
});

secured.MapGet("/api/aziende/{aziendaId:guid}/durc",
    async (Guid aziendaId, ScadenzarioService svc, ClaimsPrincipal user) =>
        Results.Ok((await svc.ElencoDurcAsync(TenantCorrente(user), aziendaId)).Select(d => new
        {
            d.Id,
            d.AziendaId,
            d.Numero,
            d.DataEmissione,
            d.DataScadenza,
            d.Regolare,
            d.Note,
        })));

// Elenco dei promemoria: solo lettura. L'invio automatico resta esplicito su POST /api/scadenzario/invia-automatiche.
secured.MapGet("/api/scadenzario",
    async (ScadenzarioService svc, ClaimsPrincipal user) =>
{
    var tenantId = TenantCorrente(user);
    var lista = await svc.ElencoAsync(tenantId, rigenera: false);
    return Results.Ok(lista.Select(PromemoriaDto));
});

// Forza la rigenerazione dei promemoria dalle fonti e restituisce quanti ne ha creati/aggiornati.
secured.MapPost("/api/scadenzario/rigenera",
    async (ScadenzarioService svc, ClaimsPrincipal user) =>
        Results.Ok(new { aggiornati = await svc.RigeneraAsync(TenantCorrente(user)) }));

// Genera promemoria per tutte le scadenze fiscali/contributive dell'anno (idempotente).
secured.MapPost("/api/scadenzario/rigenera-calendario",
    async (ScadenzarioService svc, ClaimsPrincipal user, int? anno = null) =>
        Results.Ok(new { aggiornati = await svc.RigeneraCalendarioAsync(TenantCorrente(user), anno) }));

// Invia automaticamente i promemoria urgenti usando il canale/destinatario dell'azienda.
secured.MapPost("/api/scadenzario/invia-automatiche",
    async (ScadenzarioService svc, NotificheService notifiche, ClaimsPrincipal user) =>
{
    var (inviati, saltati) = await svc.InviaAutomaticheAsync(TenantCorrente(user), notifiche);
    return Results.Ok(new { inviati, saltati });
});

// Inserisce un promemoria a mano.
secured.MapPost("/api/scadenzario",
    async (CreaPromemoriaRequest req, ScadenzarioService svc, ClaimsPrincipal user) =>
{
    try
    {
        var p = await svc.CreaManualeAsync(TenantCorrente(user), new PromemoriaManualeInput(
            req.Titolo, req.DataScadenza, req.AziendaId, req.Descrizione, req.GiorniPreavviso));
        return Results.Created($"/api/scadenzario/{p.Id}", PromemoriaDto(p));
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { errore = ex.Message }); }
});

secured.MapGet("/api/scadenzario/{id:guid}",
    async (Guid id, ScadenzarioService svc, ClaimsPrincipal user) =>
{
    var p = await svc.GetAsync(TenantCorrente(user), id);
    return p is null ? Results.NotFound(new { errore = "Promemoria non trovato." }) : Results.Ok(PromemoriaDto(p));
});

secured.MapPost("/api/scadenzario/{id:guid}/stato",
    async (Guid id, CambiaStatoPromemoriaRequest req, ScadenzarioService svc, ClaimsPrincipal user) =>
{
    try
    {
        var p = await svc.CambiaStatoAsync(TenantCorrente(user), id, req.Stato);
        return Results.Ok(PromemoriaDto(p));
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { errore = ex.Message }); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { errore = ex.Message }); }
});

// Invia il promemoria su un canale (Email/Sms/WhatsApp) o lo prepara (Manuale).
// Se l'invio va a buon fine e il promemoria è ancora Aperto, lo segna Inviato.
secured.MapPost("/api/scadenzario/{id:guid}/invia",
    async (Guid id, InviaPromemoriaRequest req, ScadenzarioService scad, NotificheService notifiche, ClaimsPrincipal user) =>
{
    var tenantId = TenantCorrente(user);
    var p = await scad.GetAsync(tenantId, id);
    if (p is null)
        return Results.NotFound(new { errore = "Promemoria non trovato." });

    var canale = string.IsNullOrWhiteSpace(req.Canale) ? (CanaleNotifica?)null : Canali.Parse(req.Canale);
    var messaggio = FormattatoreNotifiche.DaTesto(p.Titolo, p.Messaggio);
    var esito = await notifiche.InviaAsync(canale, req.Destinatario, messaggio);

    if (esito.Inviato && p.Stato == "Aperto")
        await scad.CambiaStatoAsync(tenantId, id, "Inviato");

    return Results.Ok(new
    {
        esito.Inviato,
        canale = esito.Canale.ToString(),
        esito.Destinatario,
        esito.Dettaglio,
        messaggio = messaggio.Corpo,
        testoBreve = messaggio.TestoBreve,
    });
});

// Carica un documento scadenza (es. DURC PDF): estrae P.IVA + date, collega l'azienda e crea il promemoria.
secured.MapPost("/api/documenti-scadenze",
    async (HttpRequest request, DocumentiScadenzeService svc, ClaimsPrincipal user) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { errore = "Richiesta multipart/form-data attesa." });
    var form = await request.ReadFormAsync();
    var file = form.Files["file"];
    if (file is null || file.Length == 0)
        return Results.BadRequest(new { errore = "Allegare un file (campo 'file')." });
    if (file.Length > 10 * 1024 * 1024)
        return Results.BadRequest(new { errore = "Il PDF è troppo grande: massimo 10 MB." });
    if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { errore = "Caricare un PDF DURC testuale. Le foto/scansioni vanno prima convertite con OCR." });

    Guid? aziendaId = form["aziendaId"].FirstOrDefault() is { Length: > 0 } s && Guid.TryParse(s, out var g) ? g : null;

    byte[] bytes;
    using (var ms = new MemoryStream())
    {
        await file.CopyToAsync(ms);
        bytes = ms.ToArray();
    }

    // Sicurezza (§10): il tipo dichiarato dal client non è una prova. Verifica dai byte reali
    // che sia davvero un PDF ("%PDF-") prima di darlo in pasto al parser.
    if (bytes.Length < 5 || bytes[0] != 0x25 || bytes[1] != 0x50
        || bytes[2] != 0x44 || bytes[3] != 0x46 || bytes[4] != 0x2D)
        return Results.BadRequest(new { errore = "Il file non è un PDF valido (contenuto non riconosciuto)." });

    string testo;
    try
    {
        // Estrai testo con PdfPig (testo vuoto per PDF scansionati: l'esito lo segnala).
        testo = Paghe.Documenti.EstrattoreTestoPdf.EstraiTesto(bytes);
    }
    catch
    {
        return Results.BadRequest(new { errore = "PDF non leggibile: caricare un DURC PDF testuale valido." });
    }

    var esito = await svc.ParseDocumentoAsync(testo, TenantCorrente(user), aziendaId);
    return Results.Ok(new
    {
        esito.AziendaTrovata,
        esito.RagioneSociale,
        esito.PartitaIva,
        scadenza = esito.DataScadenza?.ToString("dd/MM/yyyy"),
        esito.NumeroDocumento,
        esito.PromemoriaCreatoId,
        esito.Errore,
    });
});

// Collegamento self-service del provider SdI (configurazione per tenant).
app.MapConfigurazioneSdi();
app.MapPosta();
app.MapCertificazioneUnica();
app.MapAi();
app.MapLipe();
app.MapModello770();
app.MapRapportoDettaglio();
app.MapPresenze();
app.MapRedditiPf();
app.MapIrap();
app.MapIres();
app.MapImu();
app.MapInail();
app.MapIntrastat();
app.MapImportUnilavPdf(); // UNILAV: carica il PDF → registra datore + lavoratore + rapporto
app.MapImportUnilavTesto(); // UNILAV: incolla il testo → stessi dati registrati, senza file
app.MapImportVisuraPdf(); // Visura camerale: carica il PDF → estrae i dati azienda (da confermare)
app.MapModuli();          // archivio Persone + modello ufficiale AA7/10 compilato (PDF AdE)
app.MapCcnl();            // elenco dei CCNL configurati (menu busta paga)
app.MapRegistroAttivita(); // registro GDPR: accessi e modifiche, consultabile solo dall'admin
app.MapRetention();        // policy, blocchi, anteprime e ricevute di conservazione GDPR
app.MapAiGovernor();       // Governor AI (G2): mandati, preflight, revoca, catena audit

// Un URL API inesistente deve restituire un vero 404 JSON: non deve essere
// trasformato nel file index.html dal fallback della SPA.
app.MapMethods(
    "/api/{**percorso}",
    ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS", "HEAD"],
    () => Results.NotFound(new { errore = "Endpoint API non trovato." }));

// Fallback SPA: rotte client-side e hash possono essere aperti direttamente senza 404.
// È registrato solo quando l'immagine contiene davvero il frontend compilato.
if (frontendIntegrato)
    app.MapFallbackToFile("index.html");

app.Run();

// Reso pubblico per gli eventuali test di integrazione (WebApplicationFactory).
public partial class Program { }
