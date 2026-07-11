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

- **Logica Completa Onboarding Azienda & Anagrafica Contributiva/Fiscale (Multi-Tenant)**: Estesa e completata la logica di aggiunta e correzione anagrafica aziendale sia nel backend (`AziendaEntity.cs`, `Dtos.cs`, `Program.cs`, `AziendeService`) sia nel frontend (`api.ts`, `Aziende.tsx`). Supporta ora tutti i dati normativi, fiscali, bancari e contributivi previsti dagli standard nazionali e internazionali (P.IVA, Codice Fiscale, PEC, Codice Destinatario SdI, Codice Catastale Comune per addizionali IRPEF, CSC INPS 5 cifre, P.A.T. INAIL 8 cifre/cc, Matricola INPS, Codice Sede INPS, CCNL e Livello di default, IBAN aziendale per SEPA/F24, dati del Legale Rappresentante). Verifica duplicati P.IVA/CF isolata per tenant. Test unitari e di integrazione aggiornati in `AziendaCreazioneTests.cs` (416 test verdi).
- **Redesign UI Skeuomorfico "Scrivania del Consulente"**: riprogettazione completa dell'estetica del frontend (`frontend/src/index.css`) sostituendo il glassmorphism con materiali fisici: scrivania in tessuto caldo con grana SVG e vignettatura, pannelli/card in carta opaca (`--panel #fcf8ef` + `--grana`), bottoni 3D in rilievo con pressione fisica su `:active` (token `--rilievo`/`--bottone`), campi incassati come incisioni (`--incavo`), tab a linguetta da classificatore, tabelle-registro con righe a penna, pill/etichette timbrate, kbd a keycap, busta paga e foglio fiscale come veri fogli di carta (righe + margine rosso), inchiostro seppia caldo (`#2e2618`). Zero `backdrop-filter`; PWA `theme_color #f2eada`.
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
- **Cedolino mensile completo + busta paga professionale**: la pagina **Cedolini** della SPA
  raccoglie tutte le variabili del mese (straordinari normale/notturno/festivo, ferie, permessi
  ROL/ex-festività, malattia con n° evento nell'anno, infortunio INAIL, assenze non retribuite)
  e le passa al motore già esistente via `POST /api/rapporti/{id}/cedolini`. Il PDF
  (`CedolinoPdf`) è ridisegnato con il layout classico della busta paga italiana: testata ditta
  con P.IVA e matricola INPS/P.A.T. INAIL, riquadro inquadramento dipendente (CF, qualifica,
  livello, CCNL, mensilità, orario), corpo voci con colonne competenze/trattenute, dati
  previdenziali e fiscali, saldi ferie/ROL, TFR e costo azienda, netto del mese in evidenza.
  L'anagrafica arriva da `IntestazioneCedolino` (Paghe.Core/Domain) costruita in
  `CedoliniService.RicalcolaPerPdfAsync`.
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
