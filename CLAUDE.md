# CLAUDE.md

Contesto per rifinire questo progetto con Claude/Codex.

## Progetto

SaaS italiano per buste paga, fatturazione e contabilita per micro-imprese.
Stack attuale:

- .NET 10 / C#
- ASP.NET Core Minimal API in `backend/src/Paghe.Api`
- Motore dominio in `backend/src/Paghe.Core`
- EF Core SQLite/PostgreSQL-ready in `backend/src/Paghe.Persistence`
- PDF in `backend/src/Paghe.Documenti`
- Pratiche P.IVA/ComUnica in `backend/src/Paghe.Core/PartiteIva`
- Adempimenti telematici (F24, controlli interni, wrapper JAR ufficiali, stati pratica) in `backend/src/Paghe.Core/AdeTelematico`
- Liquidazione IVA in `backend/src/Paghe.Core/Contabilita/LiquidazioneIva.cs`
- Persistenza spese/liquidazioni IVA in `backend/src/Paghe.Persistence/ContabilitaIvaService.cs`
- Test xUnit in `backend/tests/Paghe.Tests`

## Comandi

```bash
cd backend
dotnet test Paghe.slnx -c Release
dotnet run --project src/Paghe.Api -c Release
dotnet run --project src/Paghe.Demo -c Release
```

## Regole importanti

- Dominio paghe = alta complessita normativa. I valori in `Seed.cs` sono esempi, non dati certificati: servono solo a inizializzare il DB e vanno validati con un Consulente del Lavoro.
- I parametri normativi sono dati versionati per anno su DB (tabelle ParametriAnno/InpsAliquote/IrpefScaglioni/Addizionali*/Ccnl/CcnlLivelli), caricati da `ParametriNormativiService`. Non reintrodurre valori cablati nel calcolo.
- Usare sempre `decimal` per importi e aliquote, mai `float`/`double`.
- Mantenere isolamento multi-tenant: tutte le query persistite devono filtrare per `tenantId`.
- Prima di importare codice GitHub, verificare licenza e manutenzione. Vedi `docs/GITHUB_REPOS_RIUSO.md`.
- Non usare codice AGPL/GPL dentro il SaaS senza decisione esplicita.
- Apertura P.IVA/Camera di Commercio: il software prepara checklist e dati; l'invio reale richiede canali ufficiali o intermediario abilitato.

## Fatto

- Swagger/OpenAPI attivo: UI su `/swagger`, spec su `/swagger/v1/swagger.json`.
- Validazione FatturaPA contro XSD ufficiale (Schema_VFPR12 1.2.3) in `FatturaPaValidator`,
  XSD incorporati come risorse, validazione offline.
- Controlli semantici SdI in `ControlliSdi` (00400/00401 aliquota-Natura, 00417 cessionario,
  00422 coerenza imposta). L'endpoint `POST /api/fatture/valida` restituisce `erroriXsd` + `erroriSdi`.
- Parametri normativi (aliquote, scaglioni, addizionali, CCNL) ora **su database versionati
  per anno**, caricati da `ParametriNormativiService`. `Seed.cs` serve solo a inizializzare il DB.
- Persistenza del lavoro: **dipendenti, rapporti, cartellini e cedolini archiviati** su DB
  (`AnagraficheLavoroService`, `CedoliniService`). Il calcolo di un cedolino lo archivia e si rilegge.
- **Autenticazione JWT**: `POST /api/auth/registra` e `/api/auth/login` emettono un token; gli
  endpoint dati sono nel gruppo `secured` (`RequireAuthorization`). Il tenant deriva dal claim
  `tenant_id` del token (helper `TenantCorrente(ClaimsPrincipal)`), non più da un header.
  Password con PBKDF2 (`PasswordHasher`). Chiave JWT in `Jwt:Key` (config) con default di sviluppo.
- Pratiche apertura P.IVA / Camera di Commercio / ComUnica con checklist adempimenti e
  persistenza tenant-isolata (`PraticheAperturaPartitaIvaService`).
- Liquidazione IVA vendite/acquisti con registri di periodo (`CalcoloLiquidazioneIva`) e
  salvataggio di spese/liquidazioni per azienda (`ContabilitaIvaService`).
- **Import UNILAV**: `ImportazioneUnilav` legge la Comunicazione Obbligatoria XML (lettura
  namespace-agnostica) e crea in automatico dipendente + rapporto. Endpoint
  `POST /api/aziende/{id}/dipendenti/da-unilav`.
- **F24 dalle paghe**: `F24DaCedolini` costruisce l'F24 da versare dai cedolini (ritenute
  IRPEF, addizionali, INPS). Endpoint `POST /api/f24/da-paghe`. Controlli interni F24
  (`ValidazioneF24`, `ValidatoreFiscale`). Tracciato ufficiale a lunghezza fissa ancora da fare.
- **Trasmissione Fattura → SdI** (il "modem"): astrazioni `IFatturaSdiGateway` /
  `IFirmaFatturaElettronica` in `Paghe.Core/Fatturazione/Trasmissione`, canali
  `SdiGatewayManuale` (default, prepara il file) e `SdiGatewayProviderRest` (provider accreditato
  via HttpClient), orchestrazione + persistenza stato/ricevute in `TrasmissioneFattureService`
  (`TrasmissioneFatturaEntity`, `RicevutaSdiEntity`), deleghe intermediario in `DelegheService`
  (`DelegaEntity`). Endpoint `POST /api/fatture/{id}/trasmetti`, `/ricevute`, `/deleghe`. Genera
  l'XML, esegue i `ControlliSdi`, firma/delega, traccia stati RC/MC/NS/NE/DT, blocca i doppi invii.
  Configurazione canale in sezione `Sdi` (default Manuale). Vedi
  `docs/integrazione-ufficiale/MODULO-TRASMISSIONE-SDI.md`. Restano da fornire: account/ApiKey del
  provider, firma CAdES locale reale (se non delegata), webhook ricevute, conservazione a norma.
- **Firma digitale CAdES-BES** (busta `.xml.p7m`): modulo `backend/src/Paghe.Firma`. Engine
  `FirmaCades` su `SignedCms` (SHA-256 + attributo `SigningCertificateV2`), nessuna dipendenza
  GPL. Firmatari dietro `IFirmatoreDigitale`: `FirmatorePfx` (certificato software/firma remota)
  e **`FirmatoreCns`** (smart card / **CNS** via PKCS#11 con `Pkcs11Interop.X509Store`, Apache-2.0;
  la chiave resta sulla carta). Adapter `FirmaCadesLocale` collegato al seam
  `IFirmaFatturaElettronica` della trasmissione SdI. Endpoint `POST /api/firma/cades` (upload PFX
  o PIN CNS) e `POST /api/fatture/firma` (XML FatturaPA → valida → `.xml.p7m`). Config sezione
  `Firma` (`PfxPath`/`PfxPassword` oppure `Cns:Pkcs11LibraryPath`). La firma CNS richiede il
  middleware PKCS#11 + carta sulla macchina che firma (desktop/agente locale, non backend cloud).

## Prossimi lavori utili

Piano di completamento dettagliato (gap analysis, priorità, effort, sequenza): vedi
`docs/PIANO-COMPLETAMENTO.md`. In sintesi, prossimi passi 🔴:

1. Frontend minimo (SPA) per usare il sistema senza curl.
2. Migrazioni EF Core (oggi `EnsureCreated`) + chiave JWT da secret in produzione.
3. GDPR: cifratura dati sensibili (IBAN, malattia), audit log, retention.
4. Generazione file ufficiali AA9/12, AA7/10, ComUnica e LIPE da specifiche pubbliche.
5. Fattura SdI: infrastruttura di trasmissione **fatta** (gateway/ricevute/deleghe) e **firma
   CAdES locale reale fatta** (`Paghe.Firma`, PFX + CNS/smart card); restano account provider
   accreditato + ApiKey, marche temporali (CAdES-T), webhook ricevute e conservazione a norma.

Integrazione telematica ufficiale (Agenzia Entrate, ComUnica/Registro Imprese, INAIL,
INPS, SUAP): fonti, kit, modello dati e backlog a fasi in `docs/integrazione-ufficiale/`.
