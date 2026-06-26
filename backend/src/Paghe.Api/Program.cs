using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Paghe.Api;
using Paghe.Core.AdeTelematico;
using Paghe.Core.Calcolo;
using Paghe.Core.Contabilita;
using Paghe.Core.Domain;
using Paghe.Core.Fatturazione;
using Paghe.Core.Fatturazione.Trasmissione;
using Paghe.Core.Parametri;
using Paghe.Documenti;
using Paghe.Firma;
using Paghe.Persistence;
using Paghe.Persistence.Trasmissione;

var builder = WebApplication.CreateBuilder(args);

// Gli enum (es. Qualifica) viaggiano come stringhe nel JSON ("Impiegato").
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Persistenza: in sviluppo SQLite (file locale), in produzione PostgreSQL.
// Per Postgres: sostituire UseSqlite con o.UseNpgsql(connectionString).
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=paghe.db";
builder.Services.AddDbContext<PagheDbContext>(o => o.UseSqlite(connectionString));
builder.Services.AddScoped<AziendeService>();
builder.Services.AddScoped<FattureService>();

// Parametri normativi e CCNL caricati dal database, versionati per anno (non più cablati).
builder.Services.AddScoped<ParametriNormativiService>();
builder.Services.AddScoped<AnagraficheLavoroService>();
builder.Services.AddScoped<CedoliniService>();
builder.Services.AddScoped<PraticheF24Service>();
builder.Services.AddScoped<PraticheAperturaPartitaIvaService>();
builder.Services.AddScoped<ContabilitaIvaService>();

// Trasmissione fattura elettronica (SdI) e deleghe intermediario.
// Canale configurabile in "Sdi": "Manuale" (default, prepara il file) oppure "Rest" (provider accreditato).
var sdiOptions = new SdiProviderOptions();
builder.Configuration.GetSection("Sdi").Bind(sdiOptions);
builder.Services.AddSingleton(sdiOptions);

// Certificato di firma digitale CAdES (sezione "Firma"): PFX lato server o smart card/CNS via PKCS#11.
var firmaOptions = new FirmaOptions();
builder.Configuration.GetSection("Firma").Bind(firmaOptions);
builder.Services.AddSingleton(firmaOptions);

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

// Autenticazione JWT: il tenant è derivato dai claim del token firmato, non da un header.
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TokenService>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var chiave = Encoding.UTF8.GetBytes(TokenService.ChiaveJwt(builder.Configuration));
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(chiave),
        };
    });
builder.Services.AddAuthorization();

// Documentazione interattiva degli endpoint (Swagger / OpenAPI).
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS per il frontend SPA (in sviluppo). In produzione restringere agli origin reali.
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Swagger UI su /swagger e spec OpenAPI su /swagger/v1/swagger.json.
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Gruppo di endpoint che richiede un token valido (i dati di un tenant).
var secured = app.MapGroup("").RequireAuthorization();

// Crea lo schema del database se non esiste (in produzione usare le migrazioni EF)
// e carica i parametri normativi di esempio se le tabelle sono vuote.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
    db.Database.EnsureCreated();
    db.EnsureSchemaSviluppoCompatibile();
    scope.ServiceProvider.GetRequiredService<ParametriNormativiService>()
        .ImportaSeedSeVuotoAsync().GetAwaiter().GetResult();
}

// Tenant corrente derivato dal claim "tenant_id" del token JWT (utente autenticato).
static Guid TenantCorrente(ClaimsPrincipal user) =>
    Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id)
        ? id
        : throw new InvalidOperationException("Tenant non presente nel token.");

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

app.MapGet("/", () => Results.Ok(new
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
        "POST /api/fatture/firma         (CAdES .xml.p7m)",
        "POST /api/firma/cades           (firma file: PFX o CNS/smart card)",
        "POST /api/bilancio/riclassifica",
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
        "POST /api/cedolini/pdf",
        "POST /api/fatture/pdf",
        "GET  /api/fatture/{id}/pdf"
    }
}));

app.MapGet("/health", () => Results.Ok(new { stato = "ok" }));

// --- Autenticazione (pubblici) -----------------------------------------------------
app.MapPost("/api/auth/registra", async (RegistraRequest req, AuthService auth, TokenService tokens) =>
{
    try
    {
        var utente = await auth.RegistraAsync(req.RagioneSociale, req.Email, req.Password);
        return Results.Ok(new { token = tokens.Crea(utente), tenantId = utente.TenantId, ruolo = utente.Ruolo });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

app.MapPost("/api/auth/login", async (LoginRequest req, AuthService auth, TokenService tokens) =>
{
    var utente = await auth.AutenticaAsync(req.Email, req.Password);
    return utente is null
        ? Results.Unauthorized()
        : Results.Ok(new { token = tokens.Crea(utente), tenantId = utente.TenantId, ruolo = utente.Ruolo });
});

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
app.MapPost("/api/cedolini/calcola", async (CalcolaCedolinoRequest req, ParametriNormativiService svc) =>
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
app.MapPost("/api/f24/da-paghe", (F24DaPagheRequest req) =>
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
app.MapPost("/api/fatture/calcola", (Fattura fattura) =>
{
    if (fattura.Righe.Count == 0)
        return Results.BadRequest(new { errore = "La fattura non ha righe." });
    return Results.Ok(CalcoloFattura.Calcola(fattura));
});

// Generazione del tracciato FatturaPA XML (subset FPR12).
app.MapPost("/api/fatture/xml", (Fattura fattura) =>
{
    if (fattura.Righe.Count == 0)
        return Results.BadRequest(new { errore = "La fattura non ha righe." });
    string xml = FatturaPaXml.Genera(fattura);
    return Results.Text(xml, "application/xml");
});

// Validazione FatturaPA: schema XSD ufficiale + controlli semantici SdI.
app.MapPost("/api/fatture/valida", (Fattura fattura) =>
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
app.MapPost("/api/firma/cades", async (HttpRequest http, FirmaOptions firma) =>
{
    if (!http.HasFormContentType)
        return Results.BadRequest(new { errore = "Inviare multipart/form-data con il campo 'file'." });

    var form = await http.ReadFormAsync();
    var file = form.Files["file"];
    if (file is null || file.Length == 0)
        return Results.BadRequest(new { errore = "File da firmare mancante (campo 'file')." });

    byte[] contenuto = await LeggiFile(file);
    var pfxFile = form.Files["pfx"];
    string? password = form["password"];
    string? pin = form["pin"];

    IFirmatoreDigitale? firmatore = null;
    try
    {
        if (pfxFile is not null && pfxFile.Length > 0)
            firmatore = FirmatorePfx.DaBytes(await LeggiFile(pfxFile), password ?? "");
        else
            firmatore = FirmaCadesLocale.CreaFirmatore(firma, pin);

        var risultato = firmatore.Firma(contenuto, file.FileName);
        return Results.File(risultato.ContenutoFirmato, "application/pkcs7-mime", risultato.NomeFile);
    }
    catch (NotSupportedException ex) { return Results.BadRequest(new { errore = ex.Message }); }
    catch (Exception ex) { return Results.BadRequest(new { errore = $"Firma non riuscita: {ex.Message}" }); }
    finally { (firmatore as IDisposable)?.Dispose(); }
}).DisableAntiforgery();

// Genera l'XML FatturaPA, lo valida e lo firma in CAdES (.xml.p7m) con il certificato configurato
// nella sezione "Firma". Per la CNS il PIN può essere passato nell'header "X-Firma-Pin".
app.MapPost("/api/fatture/firma", async (Fattura fattura, HttpRequest http, FirmaOptions firma) =>
{
    if (fattura.Righe.Count == 0)
        return Results.BadRequest(new { errore = "La fattura non ha righe." });

    string xml = FatturaPaXml.Genera(fattura);
    var erroriXsd = FatturaPaValidator.Valida(xml);
    var erroriSdi = ControlliSdi.Verifica(fattura);
    if (erroriXsd.Count > 0 || erroriSdi.Count > 0)
        return Results.BadRequest(new { errore = "Fattura non valida: firma annullata.", erroriXsd, erroriSdi });

    IFirmatoreDigitale? firmatore = null;
    try
    {
        firmatore = FirmaCadesLocale.CreaFirmatore(firma, http.Headers["X-Firma-Pin"]);
        string idCodice = fattura.Cedente.PartitaIva ?? fattura.Cedente.CodiceFiscale ?? "00000000000";
        string nomeXml = NomeFileFatturaPa.Genera(
            fattura.Cedente.Nazione, idCodice, NomeFileFatturaPa.ProgressivoCasuale());

        var risultato = firmatore.Firma(Encoding.UTF8.GetBytes(xml), nomeXml);
        return Results.File(risultato.ContenutoFirmato, "application/pkcs7-mime", risultato.NomeFile);
    }
    catch (NotSupportedException ex) { return Results.BadRequest(new { errore = ex.Message }); }
    catch (Exception ex) { return Results.BadRequest(new { errore = $"Firma non riuscita: {ex.Message}" }); }
    finally { (firmatore as IDisposable)?.Dispose(); }
});

// Riclassificazione del bilancio (Stato Patrimoniale + Conto Economico, schema CEE).
app.MapPost("/api/bilancio/riclassifica", (RiclassificaRequest req) =>
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

app.MapPost("/api/contabilita/iva/liquidazione", (LiquidazioneIvaRequest req) =>
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
    });
    return Results.Created($"/api/aziende/{azienda.Id}", new { azienda.Id, azienda.RagioneSociale });
});

secured.MapGet("/api/aziende", async (AziendeService svc, ClaimsPrincipal user) =>
    Results.Ok(await svc.ElencoAsync(TenantCorrente(user))));

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
    req.AziendaId);

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
            CodiceFiscale = req.CodiceFiscale, Nome = req.Nome, Cognome = req.Cognome, Iban = req.Iban,
        };
        var rapporto = new Paghe.Persistence.Entities.RapportoLavoroEntity
        {
            CodiceCcnl = req.CodiceCcnl, Livello = req.Livello, Qualifica = req.Qualifica,
            NumeroMensilita = req.NumeroMensilita, PartTime = req.PartTime,
            PercentualePartTime = req.PercentualePartTime, ScattiAnzianita = req.ScattiAnzianita,
            Superminimo = req.Superminimo, Regione = req.Regione,
            CodiceCatastaleComune = req.CodiceCatastaleComune,
            DetrazioniLavoroDipendente = req.DetrazioniLavoroDipendente,
            GiorniDetrazioneAnno = req.GiorniDetrazioneAnno,
            DataAssunzione = req.DataAssunzione ?? DateOnly.FromDateTime(DateTime.UtcNow),
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
        rapportoId = r.Id, r.DipendenteId,
        nominativo = $"{r.Dipendente?.Cognome} {r.Dipendente?.Nome}".Trim(),
        r.CodiceCcnl, r.Livello, r.Qualifica
    }));
});

// --- Cedolini calcolati e archiviati -----------------------------------------------
secured.MapPost("/api/rapporti/{rapportoId:guid}/cedolini",
    async (Guid rapportoId, Cartellino cartellino, CedoliniService svc, ClaimsPrincipal user) =>
{
    try
    {
        var c = await svc.CalcolaEArchiviaAsync(TenantCorrente(user), rapportoId, cartellino);
        return Results.Created($"/api/cedolini/{c.Id}",
            new { c.Id, c.Anno, c.Mese, c.NettoInBusta, c.CostoAzienda });
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
            p.Id, p.Anno, p.Mese, p.DataVersamento, p.TotaleDebiti, p.SaldoFinale, p.Stato, p.Valido,
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
            p.Id, p.Anno, p.Mese, p.DataVersamento, p.TotaleDebiti, p.SaldoFinale, p.Stato, p.Valido,
        })));

secured.MapGet("/api/f24/{id:guid}", async (Guid id, PraticheF24Service svc, ClaimsPrincipal user) =>
{
    var p = await svc.GetAsync(TenantCorrente(user), id);
    if (p is null)
        return Results.NotFound(new { errore = "Pratica F24 non trovata." });
    return Results.Ok(new
    {
        p.Id, p.Anno, p.Mese, p.DataVersamento,
        p.TotaleDebiti, p.TotaleCrediti, p.SaldoFinale,
        p.Stato, p.Valido, p.Bozza,
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
app.MapPost("/api/cedolini/pdf", async (CedolinoPdfRequest req, ParametriNormativiService svc) =>
{
    try
    {
        var motore = await CostruisciMotore(svc, req.Rapporto, req.Cartellino.Anno);
        var cedolino = motore.Calcola(req.Rapporto, req.Cartellino);
        byte[] pdf = CedolinoPdf.Genera(cedolino, req.Azienda, req.Dipendente);
        return Results.File(pdf, "application/pdf", $"cedolino-{cedolino.Anno}-{cedolino.Mese:00}.pdf");
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errore = ex.Message });
    }
});

app.MapPost("/api/fatture/pdf", (Fattura fattura) =>
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
            t.Id, t.Stato, t.IdentificativoSdi, t.NomeFileXml, t.Firmato, t.Provider, t.MessaggioErrore
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

app.Run();

// Reso pubblico per gli eventuali test di integrazione (WebApplicationFactory).
public partial class Program { }
