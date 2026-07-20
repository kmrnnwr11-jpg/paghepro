# CLAUDE.md

Contesto per rifinire questo progetto con Claude/Codex.

> Per lavoro sull'AI applicativa personale leggere prima
> [`docs/PIANO-AI-LIBERA-LIMITI-USO.md`](docs/PIANO-AI-LIBERA-LIMITI-USO.md). È la fonte corrente
> per scope, grant e limiti d'uso. Le fasi P-1–P6 del precedente piano ristretto sono storiche e non
> vanno implementate. Nessun codice AI parte prima di G0/G1/G2 e di una richiesta separata.

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

- **Base applicazioni mobili iOS/Android**: aggiunti Capacitor 8, progetti Xcode e Android
  Studio, build mobile separata con gate obbligatori per URL API HTTPS e app ID definitivo,
  safe area, esclusione del service worker e del login Google/demo nel contenitore nativo,
  condivisione documenti tramite share sheet e hardening Android (niente cleartext/backup,
  FileProvider ristretto). Icone, manifesto privacy iOS e protezione delle chiavi di firma inclusi.
  Il piano di pubblicazione, i gate ancora aperti e le bozze store/privacy sono in
  `docs/PIANO-MOBILE-STORE.md` e `docs/mobile-store/`. La pubblicazione non è ancora autorizzata:
  mancano dominio reale, cancellazione account e account Organization.
- **Sessioni web/mobile revocabili**: JWT v2 di 15 minuti con issuer/audience/jti/versione e `sid`
  validato a ogni richiesta contro utente, tenant e famiglia server; refresh opaco selector+secret
  monouso, rotazione atomica, rilevamento replay e hash-only nel DB. Il web usa cookie `HttpOnly`
  SameSite e coordinamento multi-scheda; il mobile usa un plugin interno con Keychain
  `ThisDeviceOnly` e Android Keystore AES-GCM. Logout revoca la famiglia, lo studio sospeso è
  bloccato immediatamente e i vecchi JWT annuali non sono accettati. Build iOS/Android verificate.
- **Inserimento azienda da testo/PDF e modalità prova contributiva**: il parser deterministico
  riconosce anche CF/P.IVA combinati, sede senza etichetta `CAP`, PEC, ATECO, settore agricolo e
  amministratore delegato; il caricamento PDF non perde più codice fiscale, ATECO e rappresentante.
  L'IBAN resta facoltativo. Per le prove si può salvare l'anagrafica con INPS/INAIL/CCNL mancanti,
  lasciandoli vuoti e visibilmente da completare; i codici contributivi composti solo da zeri sono
  rifiutati dalla UI e normalizzati a `null` dal backend. CCNL e livello non vengono inventati
  dall'ATECO; F24 e UniEmens sono bloccati lato server finché l'anagrafica contributiva resta
  provvisoria. Verificati 12 test frontend, build SPA, build backend e suite **615/615** verde.
- **Ridisegno completo ricerca e inserimento aziende**: la pagina Aziende usa una barra comandi con
  modalità Archivio/Anagrafica, ricerca immediata per denominazione, P.IVA, codice fiscale, comune,
  ATECO e settore, tabella responsive con sede/dipendenti/azioni e scheda completa con indice
  laterale attivo delle sezioni, import visura e azioni di salvataggio persistenti. I 28 controlli
  della nuova azienda sono stati provati in browser desktop/mobile: testata non coperta, navigazione
  alle sezioni corretta, nessun overflow e salvataggio sopra la barra mobile. Rimossi il pannello
  laterale duplicato e la doppia lista, senza modificare API o regole di validazione.
- **Ridisegno completo area Dipendenti**: archivio, inserimento manuale e importazione UNILAV sono
  riuniti in una postazione con azienda esplicita, ricerca, riepiloghi e stati vuoti operativi. La
  scheda del lavoratore offre navigazione Anagrafica/Rapporto/Storico, ritorno all'archivio, accesso
  ai cedolini e correzione strutturata dei dati. Verificati in browser desktop/mobile tutti i flussi
  e assenza di overflow; build frontend, build backend e suite **579/579** verdi.
- **Railway — pacchetto demo full-stack verificato**: `Dockerfile.railway` compila React/Vite e
  .NET 10 in un solo container, con SPA e API sulla stessa origine, ascolto su `$PORT`, healthcheck
  `/health`, fallback SPA e 404 JSON per API inesistenti. `railway.json` configura build e restart;
  la guida richiede volume SQLite `/data`, una sola replica, `Jwt__Key` e `PAGHE_DATA_KEY` stabili,
  AI spenta e SdI simulato. Immagine Docker costruita e avviata realmente in Production: Home,
  API, healthcheck e persistenza di 5 aziende dopo riavvio verificati. È approvata solo per dati
  fittizi; dati reali restano bloccati da migrazioni, backup/restore, auth hardening e GDPR.
- **Fase 3 — cifratura dati sensibili completata (perimetro tecnico)**: IBAN azienda/dipendente/
  avvisi, terna malattia-infortunio, voci cedolino, TOTP e ApiKey sono protetti con AES-256-GCM
  versionato e rotazione chiavi; i payload principali sono legati tramite AAD a tenant, tabella,
  record e campo. Il runner one-shot usa transazione atomica, fencing writer SQLite / `ACCESS
  EXCLUSIVE` PostgreSQL, controlli tenant/orfani, rimozione delle colonne legacy e sentinella
  ruotata per ultima; `--verifica-dati-sensibili` è read-only. Backup SQLite/PostgreSQL cifrati
  con `age`, fail-closed e `0600`. Cutover e gate provati realmente su SQLite e PostgreSQL.
- **Fase 3 — registro accessi e modifiche completato (perimetro tecnico)**: middleware API per
  letture/operazioni/esiti, audit esplicito dell'autenticazione e interceptor EF transazionale per
  inserimenti/modifiche/cancellazioni. Non conserva body, query, email o valori sensibili; usa nomi
  semantici dei campi protetti, UTC, correlation ID, RBAC admin/super-admin e filtro obbligatorio
  sul tenant del token. Append-only a livello applicativo, retry e mismatch tenant sono testati;
  schermata SPA **Registro attività** inclusa. Suite completa **579/579 verde** e build frontend
  verde. Dettagli e limiti in `docs/GDPR-REGISTRO-ATTIVITA.md`; restano retention e documenti GDPR.
- **Home studio azienda-prima**: all'accesso nessuna azienda viene scelta o creata in silenzio.
  Il professionista cerca, crea o seleziona il cliente; solo dopo si aprono i moduli operativi,
  sempre con il relativo `aziendaId`. Un tentativo di navigazione anticipato viene ripreso dopo
  la scelta. La panoramica usa dati reali della sola azienda corrente e non trasforma errori API
  in zeri finti. “Riprendi il lavoro” conserva al massimo quattro coppie strumento-cliente,
  isolate per tenant; la scelta corrente vale per la sessione e l'ultima azienda è solo
  evidenziata. Verificati build SPA e 4 test frontend su gate, storage e recenti.
- **Logica Completa Onboarding Azienda & Anagrafica Contributiva/Fiscale (Multi-Tenant)**: Estesa e completata la logica di aggiunta e correzione anagrafica aziendale sia nel backend (`AziendaEntity.cs`, `Dtos.cs`, `Program.cs`, `AziendeService`) sia nel frontend (`api.ts`, `Aziende.tsx`). Supporta ora tutti i dati normativi, fiscali, bancari e contributivi previsti dagli standard nazionali e internazionali (P.IVA, Codice Fiscale, PEC, Codice Destinatario SdI, Codice Catastale Comune per addizionali IRPEF, CSC INPS 5 cifre, P.A.T. INAIL 8 cifre/cc, Matricola INPS, Codice Sede INPS, CCNL e Livello di default, IBAN aziendale per SEPA/F24, dati del Legale Rappresentante). Verifica duplicati P.IVA/CF isolata per tenant. Test unitari e di integrazione aggiornati in `AziendaCreazioneTests.cs` (416 test verdi).
- **Redesign UI ibrido skeuomorfismo + glass**: il frontend usa superfici in vetro satinato ad alto contrasto con fallback opaco, su un piano neutro freddo/caldo; bottoni in rilievo, campi incassati, tab fisiche e risposta alla pressione mantengono la componente skeuomorfica. Navigazione, barra mobile, comandi rapidi e KPI usano icone Lucide coerenti al posto delle emoji. Il sistema resta responsive e accessibile (`prefers-reduced-motion`, focus visibile).
- Swagger/OpenAPI attivo: UI su `/swagger`, spec su `/swagger/v1/swagger.json`.
- Validazione FatturaPA contro XSD ufficiale (Schema_VFPR12 1.2.3) in `FatturaPaValidator`,
  XSD incorporati come risorse, validazione offline.
- Controlli semantici SdI in `ControlliSdi` (00400/00401 aliquota-Natura, 00417 cessionario,
  00422 coerenza imposta). L'endpoint `POST /api/fatture/valida` restituisce `erroriXsd` + `erroriSdi`.
- Parametri normativi (aliquote, scaglioni, addizionali, CCNL) ora **su database versionati
  per anno**, caricati da `ParametriNormativiService`. `Seed.cs` serve solo a inizializzare il DB.
- Persistenza del lavoro: **dipendenti, rapporti, cartellini e cedolini archiviati** su DB
  (`AnagraficheLavoroService`, `CedoliniService`). Il calcolo di un cedolino lo archivia e si rilegge.
- **Dataset demo ripetibile e tenant-isolato**: gli account `@paghe.local` possono generare
  5 aziende di settori/CCNL diversi e un dipendente per azienda tramite l'endpoint esplicito
  `POST /api/demo/dataset` (`DemoDatasetService`). Le pagine non lo richiamano automaticamente:
  un archivio vuoto resta vuoto finché l'utente crea o importa un cliente reale. La navigazione
  SPA rispetta l'hash (es. `#dipendenti`) e, se manca il contesto azienda, porta prima alla scelta
  e poi apre la sezione richiesta.
- **Autenticazione JWT v2**: `POST /api/auth/registra` e `/api/auth/login` emettono un access token
  breve e un refresh soltanto nel cookie web protetto; gli endpoint `/api/auth/mobile/*` restituiscono
  il refresh al solo contenitore nativo, che lo salva nel portachiavi del sistema. Gli
  endpoint dati sono nel gruppo `secured` (`RequireAuthorization`). Il tenant deriva dal claim
  `tenant_id` del token (helper `TenantCorrente(ClaimsPrincipal)`), non più da un header.
  Password con PBKDF2 (`PasswordHasher`). Chiave JWT in `Jwt:Key`: **in ambiente Production**
  `TokenService.ChiaveJwt` rifiuta il default di sviluppo e le chiavi < 32 byte (avvio bloccato,
  verificato a runtime); in sviluppo usa il default. Da fornire via secret/`Jwt__Key`.
- **Fondamenta produzione (Fase 2) + commerciale (Fase 1)**: infrastruttura migrazioni EF pronta
  (`PagheDbContextFactory` + pacchetti Design/Sqlite; `dotnet ef` ok) — snapshot da generare a
  schema stabile; script `scripts/backup-db.sh` (SQLite/PostgreSQL, rotazione); checklist
  `docs/PRODUZIONE.md`; documenti commerciali in `docs/commerciale/` (posizionamento, disclaimer,
  bozza Termini di Servizio, listino). Piano completo in `PIANO.md`, guida agente in `AGENTS.md`.
- **Pratiche ComUnica complete (apertura, variazione, cessazione)**: `TipoPraticaComUnica`
  (NuovaImpresa/Variazione/Cessazione) con adempimenti per ente dedotti per tipo (AE AA9/AA7,
  Registro Imprese/REA, INPS commercianti/artigiani/agricoli DA/datori, INAIL, SUAP, Albo
  Artigiani), macchina a stati pratica (`StatiPraticaComUnica`:
  Bozza→Pronta→Presentata→Protocollata→Evasa/Scartata/Annullata) e **distinta di riepilogo**
  (`DistintaComUnica`) pronta per ComunicaStarweb/DIRE o intermediario. Persistenza
  tenant-isolata (`PraticheAperturaPartitaIvaService`), endpoint
  `POST /api/pratiche/apertura-partita-iva/{id}/stato` e `GET .../{id}/distinta`, pagina SPA
  **Pratiche ComUnica** (wizard con simulazione piano, checklist adempimenti, distinta
  copiabile/scaricabile). **Assistente compilazione a chat**: l'utente incolla testo libero
  (email/appunti), `EstrattoreTestoPratica` (euristico, riusa `VisuraCameraleParser`) estrae
  i campi via `POST .../analizza-testo`, il modulo si precompila e la chat elenca i campi
  mancanti da fornire. L'invio telematico reale resta sui canali ufficiali InfoCamere
  (utenza Telemaco) o intermediario abilitato: il backlog per il web service ufficiale è in
  `docs/integrazione-ufficiale/TODO-COMUNICA-CAMERA-INAIL-INPS.md` (gli asset XSD/WSDL/JAR
  citati lì NON sono nel repo).
- Liquidazione IVA vendite/acquisti con registri di periodo (`CalcoloLiquidazioneIva`) e
  salvataggio di spese/liquidazioni per azienda (`ContabilitaIvaService`).
- **Bilancio d'esercizio persistito** (Stato Patrimoniale + Conto Economico, schema CEE
  art. 2424/2425 c.c.): oltre alla riclassificazione stateless (`RiclassificazioneBilancio`,
  `POST /api/bilancio/riclassifica`), ora **archiviato per azienda e anno** con totali,
  utile/perdita, quadratura, righe (piano conti + saldi auto-contenuti), bozza leggibile e
  macchina a stati Bozza→Approvato→Depositato/Annullato (`BilancioService`, `BilancioEntity`,
  `StatoBilancio`). Endpoint: `POST /api/aziende/{id}/bilanci`,
  `POST /api/aziende/{id}/bilanci/esempio` (demo SRLS quadrato),
  `GET /api/aziende/{id}/bilanci/precompila?anno=` (ricavi/costi pescati da fatture, spese e
  cedolini), `GET /api/aziende/{id}/bilanci`, `GET /api/bilanci/{id}`, `POST /api/bilanci/{id}/stato`.
  Tutto isolato per tenant. Lo schema di deposito ufficiale XBRL non è ancora generato.
- **Scadenzario / promemoria** (`ScadenzarioService`, `DurcEntity`, `PromemoriaEntity`): tiene i
  **DURC** delle aziende (scadenza calcolata a 120 gg dall'emissione) e **genera in automatico** i
  promemoria dalle fonti — DURC in scadenza (preavviso 30 gg) e versamenti F24 archiviati (preavviso
  7 gg) — con il **testo del messaggio già precompilato** (oggetto + corpo). La rigenerazione è
  idempotente (chiave Tipo+RiferimentoId) e **preserva lo stato deciso dall'utente**
  (Aperto→Inviato→Completato/Annullato, finali bloccati). Endpoint: `GET /api/scadenzario`
  (solo elenco con `giorniAllaScadenza`/`urgenza`), `POST /api/scadenzario/rigenera`,
  `POST /api/scadenzario` (manuale), `GET /api/scadenzario/{id}`, `POST /api/scadenzario/{id}/stato`,
  `POST /api/aziende/{id}/durc`, `GET /api/aziende/{id}/durc`, `POST /api/documenti-scadenze`
  (upload DURC PDF testuale con controllo P.IVA/azienda). UI: pagina **Scadenzario** nella SPA
  (contatori scaduti/in scadenza, messaggio copiabile, form registrazione DURC, promemoria manuale,
  upload PDF, invio multi-canale).
  Isolato per tenant.
- **Canali di notifica** (`Paghe.Core/Notifiche`): astrazione `INotificatore` + `NotificheService`
  per recapitare promemoria/solleciti su **Email** (SMTP, `NotificatoreEmailSmtp`), **SMS**
  (gateway sagomato Twilio, `NotificatoreSms`) e **WhatsApp** (Meta Cloud API, `NotificatoreWhatsApp`),
  con default **`NotificatoreManuale`** (non invia, prepara solo il testo). `FormattatoreNotifiche`
  produce la versione breve per SMS/WhatsApp. Canale e credenziali in sezione config `Notifiche`
  (default `Manuale`); i sender HTTP sono typed-client (testabili con handler finto, senza rete).
  Endpoint `POST /api/scadenzario/{id}/invia` (`canale` + `destinatario`): invia o prepara, e segna
  il promemoria `Inviato`. **Da fornire per l'invio reale**: SMTP, account Twilio (SMS), WhatsApp
  Business Meta + template approvati (i messaggi business-initiated fuori finestra 24h li richiedono).
- **Import UNILAV**: `ImportazioneUnilav` legge la Comunicazione Obbligatoria XML (lettura
  namespace-agnostica) e crea in automatico dipendente + rapporto. Endpoint
  `POST /api/aziende/{id}/dipendenti/da-unilav`.
- **UniEmens INPS (Fase 6, prima versione)**: modulo `Paghe.Core/AdeTelematico/UniEmens`
  (`FlussoUniEmens`, `UniEmensDaCedolini`, `UniEmensXmlWriter`) + `UniEmensService` che genera
  il flusso mensile dai cedolini archiviati (anagrafica lavoratori + imponibile/contributi).
  Endpoint `POST /api/aziende/{id}/uniemens/mese` → XML + sintesi. **BOZZA non validata**:
  struttura allineata allo schema pubblico, da far passare dal software di controllo ufficiale
  INPS prima dell'invio; Qualifica e GiorniRetribuiti/Settimane da affinare. Vedi `PIANO.md` Fase 6.
- **F24 dalle paghe**: `F24DaCedolini` costruisce l'F24 da versare dai cedolini (ritenute
  IRPEF, addizionali, INPS). Endpoint `POST /api/f24/da-paghe` (importi nella request) e
  `POST /api/aziende/{id}/f24/mese` (pesca i cedolini archiviati del periodo via
  `CedoliniService.GeneraF24DelMeseAsync`). Controlli interni F24 (`ValidazioneF24`,
  `ValidatoreFiscale`). **Pratiche F24 persistite** con stato (macchina a stati) ed export su
  cartella (`PraticheF24Service`, `PraticaF24Entity`): `POST .../f24/mese/salva`,
  `GET .../f24`, `GET /api/f24/{id}`, `POST /api/f24/{id}/stato`, `POST /api/f24/{id}/esporta`.
  **Tracciato telematico ufficiale a lunghezza fissa** (record A/M/V/Z, 1900 char, sez. IMU
  v.2013) in `TracciatoF24Ufficiale`, da Allegato 1 specifiche AdE: generato e archiviato in
  pratica, è il file scritto da `/esporta`. Costanti del controllo ufficiale F24A0 in
  `ControlloF24A0` (`finanze.BQJCB102.main.CtrlFile`). **Validato col controllo ufficiale
  `ControlliF24A0.jar` → ESITO POSITIVO** per F24 Erario+Regioni+IMU/locali (.dcm prodotto,
  zero errori). La sola sezione INPS richiede la matricola nel formato INPS "F" (Tabella
  formati matricole), da verificare con matricola reale. Vedi `docs/integrazione-ufficiale/`.
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
- **Firma digitale** (modulo `backend/src/Paghe.Firma`): due formati dietro `MotoreFirma`/`TipoFirma`.
  **CAdES-BES** (`.p7m`) con `FirmaCades` su `SignedCms`, e **XAdES-BES** (XML enveloped) con
  `FirmaXades` su `SignedXml` — entrambi SHA-256 + `SigningCertificateV2`, nessuna dipendenza GPL.
  Firmatari dietro `IFirmatoreDigitale`: `FirmatorePfx` (certificato software/firma remota) e
  **`FirmatoreCns`** (smart card / **CNS** via PKCS#11 con `Pkcs11Interop.X509Store`, Apache-2.0;
  la chiave resta sulla carta). Adapter `FirmaCadesLocale` collegato al seam
  `IFirmaFatturaElettronica` della trasmissione SdI. Endpoint: `POST /api/firma/cades`
  (campo `formato=cades|xades`, upload PFX o PIN CNS), `POST /api/fatture/firma`
  (XML FatturaPA → valida → firma, `?formato=`), `POST /api/firma/verifica` (validità + firmatario
  + data). UI: pagina **Firma digitale** nella SPA (anteprima documento, scelta formato/dispositivo,
  risultato firmato verificato e scaricabile). Config sezione `Firma`. La firma CNS richiede il
  middleware PKCS#11 + carta sulla macchina che firma (desktop/agente locale, non backend cloud).
  **Hardening endpoint firma**: tutti e tre gli endpoint richiedono autenticazione e rate limit;
  gli upload hanno limiti espliciti (documento 10 MB, PFX 1 MB, verifica 15 MB), riconoscimento
  minimo dal contenuto, scelta esclusiva PFX/CNS e whitelist `cades|xades`. Gli errori crittografici
  restano nei log con TraceId e il client riceve un messaggio neutro; il PFX temporaneo viene
  azzerato dalla memoria e il parser XAdES vieta DTD/entità e limita la dimensione XML. Test
  unitari + HTTP (incluso `401`, limite upload e `429`) in
  `SicurezzaUploadFirmaTests` e `SicurezzaFirmaApiTests`.
- **Primo blocco cedolino professionale (ancora bozza)**: la pagina **Presenze** della SPA e
  l'unico editor delle variabili mensili (straordinari normale/notturno/festivo, ferie,
  permessi ROL/ex-festività, malattia con n° evento nell'anno, infortunio INAIL, assenze non
  retribuite). Il cruscotto Presenze rilegge l'intero cartellino, richiede conferma esplicita e il
  backend blocca quantità negative o giornate incompatibili col mese. Malattia e infortunio
  stornano la paga ordinaria prima delle indennità, evitando il precedente doppio pagamento. Il
  PDF (`CedolinoPdf`) usa un modulo tabellare classico A4, verificato visivamente contro due
  cedolini reali di mesi diversi; la pagina **Cedolini** e
  solo archivio/download per evitare ricalcoli concorrenti che azzerino le causali. Il PDF ha testata ditta
  con P.IVA e matricola INPS/P.A.T. INAIL, riquadro inquadramento dipendente (CF, qualifica,
  livello, CCNL, tipo contratto, mensilità, orario e IBAN), corpo voci con colonne
  quantità/base/competenze/trattenute e griglia estesa fino ai riepiloghi, dati
  previdenziali e fiscali, riepilogo causali, saldi ferie/ROL, TFR e costo azienda, netto.
  L'anagrafica arriva da `IntestazioneCedolino` (Paghe.Core/Domain) costruita in
  `CedoliniService.RicalcolaPerPdfAsync`. Resta **BOZZA DA VALIDARE**: mancano calendario
  giornaliero, regole CCNL tutte versionate, saldi storici, codice unico CNEL 2026, snapshot
  immutabile e validazione del Consulente del Lavoro. Vedi
  `docs/RICERCA-CEDOLINO-PROFESSIONALE-2026.md`.
- **Allineamento schema automatico** (`PagheDbContext.AllineaColonneMancanti`, chiamato in
  `Program.cs` all'avvio): aggiunge le colonne del modello EF mancanti nel database — SQLite e
  PostgreSQL/Supabase — come rete di sicurezza per gli schemi creati con `EnsureCreated` ed
  evoluti nel tempo (es. `Aziende.AssoggettataIva`). Le colonne NOT NULL ricevono un DEFAULT
  neutro; i casi in cui il riempimento neutro sarebbe sbagliato vanno gestiti con un ALTER
  esplicito PRIMA del passaggio generico (pattern esistente in `Program.cs` per Postgres e in
  `EnsureSchemaSviluppoCompatibile` per SQLite).
- **Archivio Persone + modello AA7/10 ufficiale compilato**: anagrafica persone fisiche
  (`PersonaEntity`/`PersoneService`, tenant-isolata, CRUD `/api/persone`, CF unico per tenant) —
  si registrano una volta rappresentanti/soci e si riusano nei moduli. `ModuloAa710`
  (`Paghe.Core/AdeTelematico`) modella testata + quadri A/B/C con le tabelle ufficiali natura
  giuridica e codici carica (istruzioni 2025) e validazione interna; `Aa710PdfUfficiale`
  (`Paghe.Documenti`, PDFsharp MIT) scrive i dati **sul PDF ufficiale AdE 2025 incorporato come
  risorsa** (overlay Courier con coordinate calibrate cella per cella: NON è un fac-simile, il
  modello resta l'originale). Endpoint: `GET /api/moduli/aa7-10/tabelle`,
  `POST /api/moduli/aa7-10/pdf` (pesca azienda + persona dagli archivi, 400 con `errori` se
  incompleto). UI: pagina **Moduli ufficiali** nella SPA (archivio persone, compilazione guidata,
  download). Verificato end-to-end (API viva → PDF renderizzato) + test `ModuloAa710Tests`.
  La presentazione resta sui canali ufficiali AdE/intermediario; per le nuove imprese iscritte
  al Registro Imprese l'apertura IVA passa da ComUnica, l'AA7/10 da solo copre variazioni,
  cessazioni ed enti non-impresa. Fonte modello/specifiche: kit in
  `~/ade-official-integration-kit/agenzia-entrate-partita-iva/` (JAR controllo
  `Ctrl_AA7_AA9_AT6VA.jar` non ancora integrato: serve per il file telematico, non per il PDF).

- **Logo aziendale sulla busta paga**: ogni azienda può caricare un logo facoltativo (PNG/JPEG,
  max 1 MB, riconoscimento dal contenuto) con `POST/GET/DELETE /api/aziende/{id}/logo`
  (`AziendaEntity.Logo`, escluso dal JSON). Il PDF del cedolino lo mostra in testata a sinistra
  del blocco datore (`IntestazioneCedolino.Logo`, reso da `CedolinoPdf.Testata`). UI: pannello
  carica/sostituisci/rimuovi con anteprima nella scheda «Correggi azienda» della pagina Aziende.
- **Certificazione Unica sul modello ufficiale AdE**: oltre alla bozza JSON/CSV
  (`CuBozza`), `CertificazioneUnicaService.CostruisciModelloAsync` costruisce la CU completa di
  un rapporto/anno dai cedolini archiviati (anagrafiche sostituto/percipiente con sesso-nascita
  decodificati dal codice fiscale via `AnagraficaCodiceFiscale` — omocodia gestita — e comune da
  `ComuniCatastali` sul CSV MEF; parte B fiscale con totali annui ricalcolati con gli scaglioni
  dell'anno; sezione previdenziale INPS; annotazioni con quota TFR e conguaglio).
  `CuPdfUfficiale` (Paghe.Documenti) scrive i dati **sul PDF ufficiale CU sintetico 2026
  incorporato come risorsa** (overlay Courier calibrato e verificato visivamente pagina per
  pagina: anagrafica, dati fiscali punti 1/6/8/10/21/22/27/361/367/374/375, previdenza INPS con
  mesi UniEmens, annotazioni con a-capo automatico). L'overlay condiviso con l'AA7/10 è in
  `OverlayModuloUfficiale`. Endpoint `GET /api/rapporti/{id}/cu/{anno}/pdf`; UI: blocco
  «Certificazione Unica» nella pagina Cedolini (scelta anno + download). NOTE: il modello 2026
  certifica i redditi 2025 — per anni successivi la stampa è una bozza in attesa del modello
  definitivo; resta BOZZA DA VALIDARE (annotazione nel PDF) e manca il file telematico
  (specifiche + `ModuliControlloCUR26.jar` nel kit). Esempi renderizzati in `output/pdf/`.

- **Regole anti-errore paghe** (chieste dal fondatore per proteggere il commercialista).
  Prima: *niente buste paga di mesi futuri* — `CedoliniService.BloccaPeriodiFuturi` blocca
  l'archiviazione oltre il mese corrente (orologio `TimeProvider` iniettabile: i test che
  caricano l'anno intero usano `OrologioFisso`; il simulatore stateless resta libero); la
  pagina Presenze disabilita i mesi futuri nel selettore. Seconda: *niente busta prima
  dell'assunzione* — il periodo che precede `DataAssunzione` è rifiutato con messaggio chiaro.
  Terza: *anti-doppione dipendente* — `CreaDipendenteERapportoAsync` normalizza il CF e: stesso
  CF già assunto nella STESSA azienda → errore (eviterebbe doppia busta); stesso CF in un'ALTRA
  azienda del tenant → riusa l'anagrafica esistente e crea solo il nuovo rapporto (il vincolo
  unico (TenantId, CodiceFiscale) su Dipendenti resta rispettato). La busta paga non può
  finire sull'azienda sbagliata per costruzione: nasce dal rapporto, che è legato all'azienda
  di assunzione. Test in `BloccoPeriodiFuturiTests`; verificato anche dal vivo (400 + messaggio).
- **Regole anti-errore paghe, secondo blocco**: 4) *cessazione rapporto* —
  `RapportoLavoroEntity.DataCessazione` (nullable; PATCH rapporto con `dataCessazione` /
  `rimuoviDataCessazione`, validata ≥ assunzione, campo in scheda dipendente UI): i mesi
  interamente successivi alla cessazione sono bloccati; la CU compila la data cessazione
  (punto 9) e barra «In forza al 31/12» solo se il rapporto non è cessato nell'anno.
  5) *mesi con F24 inviato protetti* — se il periodo ha una pratica F24 in stato
  `InviataManualmente`/`RicevutaAcquisita`, il ricalcolo del cedolino è rifiutato; il
  commercialista può correggere un errore col **ricalcolo forzato** (`?forza=true`, in UI
  Presenze un confirm + promemoria di rigenerare l'F24). 6) *avviso ferie/permessi negativi* —
  il motore non azzera più i residui sotto zero (ferie anticipate = informazione vera, anche sul
  PDF); la risposta dell'archiviazione espone `ferieResidue`/`permessiResidui` e la pagina
  Presenze mostra un avviso non bloccante. Test in `BloccoPeriodiFuturiTests` +
  `MotoreCalcoloCedolinoTests`; verificato dal vivo (catena stati F24 completa).

- **Livello AI di lettura documenti (AI-first, multi-provider)**: seam `IEstrattoreAiDocumenti`
  in `Paghe.Core/Ai` con default `EstrattoreAiSpento` (zero chiamate esterne — il prodotto è
  completo anche senza AI). Provider a scelta in sezione config `Ai`: `Claude` (SDK ufficiale
  Anthropic, structured output con schema JSON), `DeepSeek`/`Glm` (default endpoint/modello
  precompilati) o `Compatibile` (qualsiasi endpoint OpenAI-compatibile, incluso Ollama locale)
  via `EstrattoreAiCompatibile` (typed HttpClient, testato con handler finto). Contratto comune
  `{campi:[{nome,valore,confidenza}]}` interpretato da `PropostaAiJson`. Regole architetturali
  (le stesse dei piani di finanziamento): l'AI legge e PROPONE soltanto, il parser deterministico
  vince sempre, l'AI riempie solo i campi vuoti sopra `SogliaConfidenza` (default 0,6), la
  conferma resta all'utente e i calcoli nel codice. Primo innesto: `/api/aziende/da-visura`
  (la nota elenca i campi proposti dall'AI); errore del provider ⇒ fallback silenzioso
  sull'euristica. Piano completo e fasi successive in `docs/PIANO-AI-FIRST.md`.
  **Modello "AI inclusa nel canone"**: la chiave del provider è centrale (config del server,
  il cliente non configura nulla); consumi conteggiati per studio/mese (`ConsumiAi` +
  `ConsumoAiService`, limite `Ai:LimiteMensileChiamate`, default 300, 0 = illimitato per AI
  locale); oltre il limite si torna alla sola euristica con nota esplicita. Percorso verso
  l'AI locale senza costi API (Ollama/vLLM su server proprio, provider `Compatibile`):
  `docs/PIANO-AI-INCLUSA.md`. **Percorso "vista" per le foto** (OCR di nuova generazione):
  `EstraiDaImmagineAsync`/`VedeImmagini` sul seam — il modello vision legge direttamente
  l'immagine (layout compreso) invece del giro Tesseract→testo. `Ai:ModelloVisione`
  (default: GLM → `glm-4v-flash`; Claude vede col modello principale; DeepSeek senza vista;
  Ollama es. `qwen2.5vl:7b`). Nella visura: foto → vista se disponibile (l'assenza di
  Tesseract non è più bloccante), PDF testuali → AI sul testo; `PropostaAiJson` tollera le
  cornici ```json```. Endpoint di servizio: `GET /api/ai/stato` (provider, visione, consumo
  mese/limite) e `POST /api/ai/prova` (test di configurazione, non conteggiato).

## Prossimi lavori utili

Piano operativo corrente (priorità, dipendenze e caselle): vedi `PIANO.md`.
`docs/PIANO-COMPLETAMENTO.md` resta soltanto una fotografia storica del 26 giugno 2026.
In sintesi, prossimi passi 🔴:

1. GDPR: completare retention/cancellazione (cifratura e registro accessi/modifiche fatti), poi
   documenti e decisioni organizzative con il professionista privacy.
2. Introdurre migrazioni EF versionate quando lo schema è stabilizzato; completare deploy,
   secret reali, backup/restore e monitoraggio prima del go-live.
3. Generazione file ufficiali AA9/12, AA7/10, ComUnica e LIPE da specifiche pubbliche.
4. Fattura SdI: infrastruttura di trasmissione **fatta** (gateway/ricevute/deleghe) e **firma
   CAdES locale reale fatta** (`Paghe.Firma`, PFX + CNS/smart card); restano account provider
   accreditato + ApiKey, marche temporali (CAdES-T), webhook ricevute e conservazione a norma.

Integrazione telematica ufficiale (Agenzia Entrate, ComUnica/Registro Imprese, INAIL,
INPS, SUAP): fonti, kit, modello dati e backlog a fasi in `docs/integrazione-ufficiale/`.
File telematico AA7/10-AA9/12 (apertura/variazione/cessazione P.IVA): piano completo e
de-rischiato in `docs/integrazione-ufficiale/PIANO-AA7-AA9-TELEMATICO.md` — tracciato 3503
char (record A/B/C/D/Z), JAR `Ctrl_AA7_AA9_AT6VA.jar` già provato in locale (entrypoint
`finanze.anag.main.GestoreControlli`, stessa CLI dell'F24), criterio = esito positivo del
controllo ufficiale.

## Linee guida di comportamento (stile Karpathy)

Regole per ridurre gli errori tipici degli assistenti AI nel programmare. Da fonte MIT
`multica-ai/andrej-karpathy-skills`. Per compiti banali (refuso, one-liner ovvio): usa buon senso.

### 1. Pensa prima di scrivere codice

**Non dare per scontato. Non nascondere i dubbi. Esponi i compromessi.**

- Dichiara le assunzioni in modo esplicito. Se sei incerto, chiedi.
- Se ci sono più interpretazioni, presentale — non sceglierne una in silenzio.
- Se esiste un approccio più semplice, dillo. Obietta quando ha senso.
- Se qualcosa non è chiaro, fermati. Di' cosa ti confonde. Chiedi.

### 2. Prima la semplicità

**Il minimo codice che risolve il problema. Niente di speculativo.**

- Nessuna funzionalità oltre a quanto richiesto.
- Nessuna astrazione per codice usato una sola volta.
- Nessuna "flessibilità"/"configurabilità" non richiesta.
- Nessuna gestione di errori per scenari impossibili.
- Se scrivi 200 righe e ne bastavano 50, riscrivi.
- Test: "Un ingegnere senior direbbe che è troppo complicato?" Se sì, semplifica.

### 3. Modifiche chirurgiche

**Tocca solo ciò che devi. Pulisci solo il disordine che crei tu.**

- Non "migliorare" codice, commenti o formattazione adiacenti.
- Non rifattorizzare cose che non sono rotte.
- Rispetta lo stile esistente, anche se lo faresti diversamente.
- Se noti codice morto non collegato, segnalalo — non cancellarlo.
- Rimuovi import/variabili/funzioni resi inutili dalle TUE modifiche; non rimuovere
  codice morto preesistente se non richiesto.
- Test: ogni riga modificata deve ricondursi direttamente alla richiesta dell'utente.

### 4. Esecuzione guidata dall'obiettivo

**Definisci criteri di successo. Itera finché non è verificato.**

- "Aggiungi validazione" → "Scrivi test per input non validi, poi falli passare".
- "Correggi il bug" → "Scrivi un test che lo riproduce, poi fallo passare".
- "Rifattorizza X" → "Assicura che i test passino prima e dopo".
- Per compiti multi-step, dichiara un piano breve: passo → verifica.
