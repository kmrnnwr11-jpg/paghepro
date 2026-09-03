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

- **Strada A3 del consenso: Aurora propone modifiche dalla chat (3 settembre 2026)**:
  richiesta del fondatore («Aurora deve fare tutto»). Col consenso pieno la coppia
  tenant:utente entra nel rollout A3 senza coppie di collaudo in config:
  `AiRolloutPolicy` tiene un registro dei consensi (caricato all'avvio dal DB, aggiornato
  dagli endpoint del consenso; kill-switch `Ai:A3ConConsensoPieno=false`), così emissione,
  effetti, conferma e runtime si abilitano dai controlli già esistenti. Il mandato
  automatico della chat usa la finalità dedicata **ASSISTENTE_CHAT** (stesse risorse di
  PREPARAZIONE_MESE_PAGHE, quote da sessione di lavoro: 10 proposte/10 effetti default,
  max 50/25) e chiede anche draft/apply su Cartellini e Cedolini quando la strada A3 è
  attiva, con ripiego automatico in sola lettura (per esempio se lo slot A3 è occupato dal
  pannello); un mandato read-only in cache viene sostituito quando arrivano le scritture.
  **Ogni modifica resta una proposta firmata con card di conferma e undo** — il consenso
  vale come Confirmed, mai applicazione silenziosa. Il pannello riconosce i mandati
  automatici dalla finalità (`mandatoAutomaticoAurora` in `auroraCedolini.ts`) e li revoca
  da sé prima di «Prepara il mese». Test: chat 25/25 (strumenti di modifica offerti,
  mandato ASSISTENTE_CHAT con `read,draft,apply_reversible`), gate A3/Governor 77/77,
  frontend 100/100.

- **Netto concordato con conguaglio + trasferta nel motore agricolo (1 settembre 2026)**:
  due incrementi nati da un caso cliente reale. (1) **Netto concordato**
  (docs/NETTO-CONCORDATO-CONGUAGLIO.md): sul rapporto si fissa il netto mensile pattuito e a
  ogni elaborazione il superminimo del mese viene ricalcolato dal calcolo inverso
  (`CalcoloInversoNetto`) così il netto in busta è quello pattuito — mai sotto il minimo CCNL
  (elaborazione rifiutata), mai retroattivo (decorrenza dal mese corrente in avanti, PATCH
  rifiuta date passate: le buste dei mesi già trascorsi non si ricostruiscono da un netto),
  voce di trasparenza `NETTO_CONC` in busta. Campi `NettoConcordatoMensile`/`Decorrenza`,
  migrazioni `NettoConcordato` per entrambi i provider, 6 test in `NettoConcordatoTests`.
  (2) **Trasferta e rimborsi documentati nel motore agricolo**: `CartellinoAgricolo` porta
  giorni/indennità di trasferta (esente entro il limite dell'anno, eccedenza imponibile,
  stesse regole di `CalcoloElementiAgevolati`) e rimborsi spese documentate; le somme esenti
  restano fuori da imponibile INPS e IRPEF; input esposto sugli endpoint agricoli del
  rapporto. Caricata inoltre la **tabella CPL Piacenza 2026** (fonte E.B.A.P., OTD dal
  01/06/2026, 6 livelli; trattenute FAP 8,84% + FIMI 0,3333% + CAC 0,25% dagli artt. 53-54
  del CPL 2024-27) nel solo DB di sviluppo, marcata «da validare col consulente». Suite
  completa **1.098/1.098** verde. Limite etico confermato due volte in questo lavoro: il
  software non genera buste retroattive costruite sui netti pagati e non inventa
  presenze/trasferte; le liste presenze «riconciliate a ritroso» vengono rifiutate.
  Ritocchi UI della stessa giornata: campi trasferta/rimborsi nella pagina «Agricoltura OTD»;
  «Tipo contribuente» non più bloccante nella scheda azienda (resta tra i «da completare»);
  il controllo Aurora «Prepara il mese» classifica i rapporti `AGRICOLTURA_OPERAI` con lo
  stato `agricolo` (si elaborano dalla pagina agricola) invece di «bloccato» per dati
  aziendali INPS/CCNL che non riguardano il loro ciclo — il gate per gli ordinari non cambia.
  **Archivio buste agricole** (sera): tabella dedicata `CedoliniAgricoli` SEPARATA dai cedolini
  ordinari (una busta agricola in `Cedolini` finirebbe in F24/UniEmens/CU producendo
  adempimenti sbagliati — invariante coperta da test); PDF conservato cifrato con hash
  verificato alla rilettura, upsert per rapporto+periodo con versione, endpoint
  archivia/elenco/riscarica + pulsante e lista nella pagina SPA; migrazioni
  `ArchivioBusteAgricole` per entrambi i provider; 5 test nuovi in `AgricolturaApiTests`
  (15/15). Resta un prospetto «da validare»: la macchina a stati dei cedolini ordinari non
  si applica (i suoi controlli sono del ciclo ordinario) e PosAgri resta il punto aperto.
  **Trasferta a gruppi di giorni** (stessa sera): `CartellinoAgricolo.Trasferte`
  (lista giorni×importo, limite esente applicato per singolo giorno gruppo per gruppo,
  una voce `AGR_TRASF` per gruppo), input `Trasferte` sugli endpoint del rapporto,
  colonna `TrasferteJson` nell'archivio (migrazioni `TrasferteBatchAgricole`), tabella
  editabile nella pagina SPA che sostituisce i due campi singoli quando compilata; la
  somma unica mensile documentata resta sui «Rimborsi spese documentati».
  **Scelta dello stile busta** (notte): `GET /api/cedolini/{id}/pdf?stile=` esponeva i tre
  layout originari (classico/colori/editoriale, numeri identici) e la pagina Cedolini ha
  la tendina «Stile della busta»; un cedolino CHIUSO resta il PDF congelato alla chiusura.
  La busta agricola mantiene il suo stile dedicato (variante classica = lavoro futuro).
  **Pagina Impostazioni vera** (Studio → Impostazioni): sostituito il mockup statico con
  preferenze funzionanti sul dispositivo (`preferenze.ts`: stile busta predefinito riletto
  dalla pagina Cedolini; toggle risposte brevi Aurora sulla stessa chiave del pannello),
  stato AI dal vivo (`api.statoAi`) e tessere che aprono davvero le pagine di
  configurazione via hash; guida aggiornata; test `preferenze.test.ts`.
  **Personalizzazione completa (2 settembre, /goal)**: preferenze utente sul SERVER
  (`PreferenzeUtenti` con whitelist e validatori per chiave, migrazioni `PreferenzeUtente`,
  GET/PUT `/api/preferenze`, cache locale come fallback offline); **font dei PDF a scelta**
  (Carlito e IBM Plex Sans incorporati come risorsa OFL accanto a Lato — Font/LICENZE.md —
  registrati in `DocumentiConfig`, famiglia parametrica in tutti e 4 i renderer);
  **dicitura dello studio** a piè di pagina (mai al posto delle diciture obbligatorie:
  coperto da test); endpoint PDF (ordinari e agricoli) leggono font+dicitura dalle
  preferenze server; **aspetto interfaccia** (taglia testo, font UI Lato, densità) applicato
  via classi body da `applicaAspetto()`; **cambio password** (`/api/auth/cambia-password`,
  verifica della attuale, min 8) e **UI 2FA** (attiva/disattiva sugli endpoint esistenti)
  nella pagina Impostazioni. `colore_accento` esiste ed è validato ma non è esposto come
  preferenza libera: le palette entrano soltanto attraverso il catalogo chiuso degli stili.
  Pagina agricola «dal contratto»: le ore al giorno arrivano dalle ore settimanali del rapporto
  (÷ 6, settimana agricola), le ore del mese si propongono come giornate × ore/giorno e i giorni di
  detrazione dal periodo di assunzione/cessazione — tutte proposte modificabili (helper puri in
  `agricoltura.ts` con test; `OreSettimanali` aggiunto a `RapportoDettaglioDto`).

- **Quindici stili completi per il cedolino ordinario (2 settembre 2026, `/goal`)**:
  catalogo stabile di **3 impaginazioni × 5 temi** — Classico Ufficio/Blu/Verde/Bordeaux/
  Antracite, Modulare Blu/Verde/Bordeaux/Viola/Petrolio, Editoriale Rame/Blu/Verde/Bordeaux/
  Grafite. `CatalogoStiliCedolino` è l'unica whitelist backend; rifiuta chiavi ed enum numerici
  sconosciuti e pilota renderer e palette immutabili. Cedolini e Impostazioni mostrano selezione
  raggruppata e anteprima; **Modulare Blu (`colori`) è il default di prodotto** in assenza di una
  scelta, mentre preferenze valide già salvate (compreso Classico) non vengono sovrascritte e un
  valore legacy corrotto ricade sul Classico. La preferenza server vale per le anteprime, mentre la chiusura registra
  `cedolino-v2:<stile>:<font>` e conserva i byte cifrati, quindi ristampe e cambi preferenza non
  alterano il documento. Tutti i layout espongono anche i progressivi annui e mantengono le
  diciture «PROSPETTO PAGA DI SIMULAZIONE – NON COSTITUISCE LUL» e «BOZZA / NON CERTIFICATO».
  Test parametrizzati coprono le 15 combinazioni, contenuto, contrasto, API, tenant, preferenze e
  immutabilità; verifica visuale A4 effettuata sui 15 PDF (Classico/Modulare una pagina,
  Editoriale tre pagine senza continuazioni vuote).

- **Pagina Impostazioni organizzata (2 settembre 2026)**: workspace in cinque categorie —
  Documenti, Interfaccia, Aurora, Sicurezza e Studio — con menu laterale desktop e categorie
  orizzontali su mobile. Un solo pannello resta visibile; Documenti include una mini-anteprima A4
  senza dati reali. Le scorciatoie riusano la policy per ruolo di `navigazione.ts`; password e 2FA
  hanno etichette, busy state, feedback distinti e stato iniziale letto dal server con isolamento
  tenant. `salvaPreferenzaConEsito` conserva il fallback locale, ritenta le modifiche offline,
  impedisce alle code di attraversare la sessione e serializza le scritture per chiave; il badge
  aggrega anche chiavi parallele. Test frontend e verifica browser coprono categorie, ruoli, gare
  di salvataggio, cambio sessione e reflow a 320/375 px; aggiornata anche la guida illustrata.

- **CU telematica: il file passa il controllo ufficiale AdE (29 agosto 2026)**: primo pezzo
  del «gate di vendibilità» del dossier concorrenza. `TracciatoCuUfficiale` (Paghe.Core/
  AdeTelematico) scrive la fornitura CUR26 (redditi 2025) dalle specifiche tecniche ufficiali:
  record da 1.900 caratteri, A/B/Z posizionali, C/D/G a struttura variabile con la tabella di
  75 elementi «codice campo + valore», quadri DA/DB/DC, quadro CT e coda Z. Tre forniture
  generate dai test (anno intero con quadro CT; cessazione a metà anno con mesi UniEmens
  parziali; due certificazioni) sono state **elaborate dal modulo di controllo ufficiale
  `ModuliControlloCUR26.jar` v1.1.0 con esito positivo**: file `.dcm` prodotto, zero errori nel
  diagnostico — stessa strada dell'F24. Regole vere emerse dal controllo e ora nel codice: i
  posizionali numerici assenti si riempiono a zeri (non spazi), un CF con checksum errato
  scarta la singola CU, e con redditi presenti sono obbligatori DB001390 (trattamento
  integrativo, cod. 2), DB001718 (cod. 2) e la tipologia reddituale DB001751=1. Il modello
  `SostitutoCu` porta ora il legale rappresentante (record B); il servizio
  `CostruisciTracciatoTelematicoAsync` costruisce il file di tutta l'azienda con gli avvisi sui
  dati mancanti senza inventarli; endpoint `GET /api/aziende/{id}/cu/{anno}/telematico`
  (solo redditi 2025); `ControlloCur26` affianca `ControlloF24A0` nel runner Java. Dichiarati
  fuori perimetro: lavoro autonomo (H), locazioni (L), TFR a tassazione separata (DF),
  assistenza fiscale 730, familiari a carico, annotazioni codificate, persistenza pratica e
  schermata SPA. Dettagli e procedura in `docs/integrazione-ufficiale/README.md`; test golden
  in `TracciatoCuUfficialeTests` (dump con `CU_DUMP*`).

- **Aurora vede l'archivio clienti e agisce sull'interfaccia (29 agosto 2026, sera)**:
  seconda richiesta del fondatore sul consenso pieno («Aurora deve vedere le aziende e
  portarmi sulle pagine che chiedo»). Col consenso attivo ogni turno riceve nel prompt
  l'**archivio clienti** (nome, comune, id; max 100 con taglio dichiarato, niente
  P.IVA/importi) letto dal server: «quali aziende ho?» funziona anche senza cliente
  selezionato. Il modello propone **azioni d'interfaccia** con marcatori `[[APRI:chiave]]`
  (whitelist di pagine operative, identica tra server e SPA:
  `SezioniApribili`/`SEZIONI_APRIBILI_DA_AURORA`) e `[[CLIENTE:id]]`; il server
  (`EstraiAzioniUi` + rivalidazione tenant) toglie SEMPRE i marcatori dal testo ed
  esegue solo col consenso; la SPA riceve `apriSezione`/`selezionaCliente` già validati,
  naviga con `vaiA` e seleziona il cliente **migrando la conversazione** al nuovo scope
  senza azzerarla. Senza consenso: né archivio né azioni. Test: 23/23 in
  `AiChatApiTests` (4 nuovi su archivio/azioni), frontend 71/71, lint pulito.

- **Consenso pieno ad Aurora: auto-mandato di lettura in chat (29 agosto 2026)**: richiesta
  esplicita del fondatore («voglio dare tutti i permessi ad Aurora, un avviso alla prima
  apertura, l'utente conferma e Aurora ha accesso completo»). Alla prima apertura la shell
  propone il dialogo «Dai pieno accesso ad Aurora?»; il consenso vive sul server (tabella
  `ConsensiAurora`, migrazioni `ConsensoPienoAurora` per entrambi i provider — scritte a
  mano con `[Migration]` sulla classe perché la CLI `dotnet ef` non era disponibile:
  snapshot aggiornati, prossima migrazione da generare normalmente), endpoint
  `GET/POST/DELETE /api/ai/consenso`, sempre revocabile dal pannello. Con il consenso
  attivo e un cliente selezionato la chat **si procura da sola il mandato di lettura**
  (`MandatoAutomaticoAsync`: Guidata/S2, `PREPARAZIONE_MESE_PAGHE`, sei risorse read-only,
  mese corrente, 60', stesso Governor/policy/audit del pannello, lease mai esposta al
  browser, cache di processo con riuso) e il turno diventa governato con gli strumenti di
  lettura; senza cliente selezionato o a emissione fallita resta la conversazione libera,
  senza errori. Il riconoscimento deterministico delle card gira ora in **ogni** turno
  (libero e governato: `IstruzioniCard` separata da `IstruzioniTurnoLibero`). Le
  **scritture non cambiano**: A3 con proposta+conferma+undo e card premute dall'utente.
  Nel pannello i mandati read-only automatici non sono «residui» e non bloccano «Prepara
  il mese» (vengono revocati all'avvio, `mandatoSoloLettura` in `auroraCedolini.ts`); la
  revoca del consenso revoca subito i mandati automatici. Deroga al piano («nessun
  permesso permanente») registrata in `docs/AURORA-REGISTRAZIONI-DALLA-CHAT.md`. Test:
  4 nuovi casi in `AiChatApiTests` (19/19), frontend 69/69, build e lint verdi.

- **Aurora sa se il cliente è già registrato e guida un passo alla volta (27 agosto
  2026)**: richiesta del fondatore — «Aurora deve capire da sola se l'azienda è
  registrata, fare quello che riesce senza mille domande e rispondere solo con quello che
  manca». L'esito delle due letture deterministiche di sola esistenza del turno libero ora
  entra **anche nelle istruzioni del modello** come singolo bit per entità («già
  registrata sì/no», «già assunto sì/no», più «cliente selezionato sì/no»): Aurora smette
  di rispondere per ipotesi («se la card non compare, significa che…») e dice il passo
  giusto con certezza — cliente nuovo → premi la card; cliente già in archivio → scegli da
  «Scegli il cliente» e avanti col dipendente; dipendente senza cliente selezionato → i
  due passi in ordine, senza altre domande; già assunto → non si ripropone. Le istruzioni
  del turno libero impongono risposte da 2-3 frasi senza riepiloghi (i campi obbligatori
  mancanti restano segnalati in rosso dalla scheda, il modello non li inventa). Deroga di
  governance decisa dal fondatore e registrata in
  `docs/AURORA-REGISTRAZIONI-DALLA-CHAT.md`: l'unico dato d'archivio in più che raggiunge
  il provider è quel bit di esistenza, calcolato su P.IVA/CF già presenti nel messaggio
  dell'utente. In parallelo la SPA ha il pulsante **«Risposte brevi»** (preferenza per
  browser, `RispostaBreve` nella richiesta chat): cambia solo lo stile — massimo 2-3
  frasi — mai permessi o strumenti. Test aggiornati in `AiChatApiTests` (15 casi, incluse
  le asserzioni sul prompt per «già registrata», «già assunto», due passi e stile).

- **Aurora registra aziende e dipendenti dalla chat (27 agosto 2026)**: nel turno libero
  (senza lease) i **parser deterministici** riconoscono nei messaggi dell'utente i dati di
  un'azienda (`VisuraCameraleParser`) o di un lavoratore (`ImportazioneUnilavPdf`) e la
  risposta della chat allega le card «Registra questo cliente» / «Registra questo
  dipendente» (`AziendaRiconosciutaAi`/`LavoratoreRiconosciutoAi` in Paghe.Api). La
  scrittura resta un click dell'utente su API ordinarie (`POST /api/aziende`,
  `POST /api/unilav/testo` con `aziendaId` del cliente selezionato): per il piano AI è un
  «modulo di prodotto», non un'azione dell'agente. Dopo la registrazione la shell apre da
  sola la scheda (azienda: prop `apriAziendaId` su Aziende con campi vuoti in rosso «Da
  completare.» via `campiDaCompletareAzienda`; dipendente: `apriDipendente(rapportoId)` +
  card rossa dei `daCompletare` nel pannello). Regole di sicurezza emerse dalla review
  avversariale e implementate: riconoscimento **messaggio per messaggio** (mai sulla
  conversazione concatenata, che mescolerebbe campi di fonti diverse), checksum P.IVA/CF
  obbligatorio in chat, il CF del titolare in una visura non diventa un'assunzione, il
  dedupe «già in archivio/già assunto» copre anche i tempi determinati con fine futura, e
  la SPA congela il testo d'origine dentro la proposta. **Precisazione governance**: il
  turno libero esegue due letture deterministiche di sola esistenza (cliente e
  lavoratore, tenant-isolate); in origine l'esito arrivava soltanto alla SPA, dal 27/8
  entra anche nel prompt come singolo bit di esistenza (vedi la voce sopra — deroga
  decisa dal fondatore). Il documento allegato via `/api/ai/documento` restituisce la stessa
  proposta tipizzata. Presenze/assenze da chat restano sul protocollo A3
  (`compila_presenze`, gate fail-closed): mappa completa dei tre flussi in
  `docs/AURORA-REGISTRAZIONI-DALLA-CHAT.md`. Verifiche: backend completo verde (19 test
  chat/documenti inclusi), frontend verde, lint invariato, build ok.

- **Aurora conversa senza mandato + riparazione repository e suite (26 agosto 2026)**: la chat
  `/api/ai/chat` senza lease ora esegue un **turno di conversazione libera** — nessuno strumento,
  nessuna lettura d'archivio, istruzioni «solo spiegazione», contatore `ConsumiAi` dello studio,
  filtro segreti e limiti messaggio invariati; una tool-call spontanea del provider viene ignorata
  (coperto da `Chat_SenzaLease_ConversaSenzaStrumentiENonToccaIlDominio`). La SPA invia i messaggi
  anche senza mandato; il mandato resta necessario per leggere l'archivio e per «Prepara il mese».
  Contesto: il deploy Render falliva perché i commit del 23-25/8 avevano dimenticato **77 file
  nuovi** (riparato con `19f1ae5`), e il lavoro non committato aveva lasciato **131 test rossi**.
  Riallineati ai criteri della revisione consulente: P.IVA/CF di prova con checksum e codice
  ufficio validi, profilo contributivo completo nelle fixture (stesse aliquote del seed, totali
  invariati), progressivi settimanali e riporto autorevole nei calendari di test, golden del
  motore aggiornati ai valori veri verificati per composizione (lordo liv.3 1.983,91 =
  1.453,94+527,90+2,07; Milano 1.993,20; trattenute con Fondo Est 2,00 + EBiTer 0,99; costo con fondi
  datoriali e solidarietà 16,28; conguaglio dicembre a credito col cuneo). Due difetti veri
  corretti nel codice: `AnagraficheLavoroService` accettava solo CF a 16 caratteri (ora ammette
  il numerico provvisorio a 11 cifre e il CF assente dell'import guidato, che resta nei
  «daCompletare»); il rifiuto del rapporto agricolo ora precede il gate requisiti in
  `CalcolaEArchiviaVerificatoAsync` (helper `RapportoOrdinarioAsync`). Pacchetto locale macOS in
  `~/PaghePro` (self-contained arm64, SQLite, backup automatico, Aurora cloud-connessa).

- **Stato autorevole Officina Nord / luglio 2026 (25 agosto 2026)**: il caso resta una
  **simulazione non validabile**, non un LUL. La revisione corrente sostituisce i numeri delle
  revisioni storiche sotto: fisso H011 livello III **2.015,15 €** (1.453,94 paga base + 527,90
  contingenza con EDR incluso + 21,95 uno scatto dal 01/10/2025 + 11,36 terzo elemento Milano
  soltanto con fonte), tariffa oraria **11,9949**, giornaliera **77,5058**, TFR per codice
  **162,40**, ROL/ex festivita separati **16,33/18,67 h** (oppure 21,00/18,67 oltre 15
  dipendenti). La quattordicesima e separata. Il gate blocca archiviazione/elaborazione senza
  applicabilita H011 documentata, calendario e progressivi settimanali, metadati evento,
  domicilio e conguagli fiscali, profilo contributivo reale e checksum fiscali validi. Malattia
  INPS e INAIL sono distinte dalle quote azienda e dalle prestazioni recuperabili; i PDF
  riportano sempre «Prospetto paga di simulazione - non costituisce LUL» e «Bozza / non
  certificato». Fonte di dettaglio: `docs/buste-reali/validazione-06-commercio-2026-07.md`.
  Le voci datate 24 agosto e le cifre **2.037,10 / 164,16 / 51,33** conservate piu sotto sono
  cronologia di sviluppo e non vanno riutilizzate per questo caso.

- **Busta Commercio rivista riga per riga e correzioni implementate (24 agosto 2026)**: la busta
  di prova H011 di luglio 2026 è stata rivista dal fondatore in veste di consulente
  (`docs/buste-reali/validazione-06-commercio-2026-07.md`: risposte, esito «da rifare», busta
  rigenerata). Correzioni entrate nel motore, tutte coperte da `RevisioneConsulenteCommercioTests`:
  **paga base e contingenza (EDR incluso) esposte come voci distinte** (colonna `Contingenza` sui
  livelli CCNL, dato fornito per il solo liv. 3 H011: 1.453,94+527,90); **terzo elemento
  provinciale** (tabella `CcnlTerzoElemento` per anno+provincia, `ProvinciaLavoro` sul rapporto con
  default sede azienda, Milano 11,36 / nazionale 2,07, mai ereditato tra province); **scatti di
  anzianità maturati dalla data di assunzione** (periodicità e massimo nelle regole CCNL, decorrenza
  dal compimento del periodo); **13ª/14ª erogate nel mese del contratto** (Terziario: dicembre e
  luglio, finestra 14ª 1/7→30/6, maturazione per dodicesimi, tassazione separata senza detrazioni ex
  art. 23 c.2 DPR 600/73); **detrazione e addizionali proiettate sulle mensilità contrattuali**
  (l'IRPEF lorda mensile resta a scaglioni ragguagliati ×12: confermata da 26 buste reali);
  **ulteriore detrazione cuneo fiscale** L. 207/2024 (1.000 €/anno, 20–32k, décalage a 40k) come
  parametri dell'anno, applicata anche al conguaglio di dicembre; **ROL+ex-festività per dimensione
  aziendale** (Terziario 88/104 h/anno); **aliquota INPS c/datore mai cablata**: il profilo
  contributivo reale si imposta sull'azienda (`AliquotaInpsDatore`) e vince sulla tabella per
  qualifica; **costo del mese e costo medio annualizzato distinti** (formula verificata sui numeri
  della revisione). Migrazioni `CorrezioniRevisioneCommercio` per entrambi i provider; i dataset
  `minimi-ccnl-2026.json` e `voci-variabili-ccnl-2026.json` portano i dati H011. Il collaudo PDF
  finale copre sia lo stile classico sia quello colori e conferma: totale mensile **2.037,10 €**,
  TFR **164,16 €**, costo del mese **5.432,64 €**, costo medio annuo/12 **3.237,44 €** e
  ROL/ex-festività **51,33 ore**; il classico è un A4 di una pagina con font incorporati. Il codec
  dello snapshot conserva anche il costo medio annualizzato, quindi il PDF definitivo dopo la
  chiusura non perde il secondo costo; gli snapshot V1 precedenti restano leggibili. Verifica finale:
  modello EF senza variazioni pendenti per SQLite/PostgreSQL, build backend Release a 0 errori,
  **1.000/1.000 test backend**, **48/48 frontend**, lint senza errori e build SPA riuscita. La **dicitura
  generale di non certificazione resta** (verdetto della revisione): mancano progressivi in busta,
  conguagli infrannuali delle addizionali, FIS dedotto dalla dimensione, coerenza CF/sesso e la
  scomposizione degli altri livelli H011. Test aggiornati ai nuovi criteri (`MotoreCalcoloCedolino`,
  `VerificaApprofondita`, `ParametriNormativiService`); i test sugli schemi storici ora inseriscono
  anche le aziende con l'helper legacy, così una colonna nuova non li rompe più.

  **Seconda revisione della busta rigenerata (25 agosto 2026)**: nove rilievi, tutti chiusi o
  dichiarati (tabella completa nel documento di validazione). Nel motore: aliquote INPS
  stampate con tutti i decimali significativi (9,3567%, mai 9,36%); **detrazione art. 13 e
  ulteriore detrazione cuneo in caselle separate** (colonna `UlterioreDetrazioneCuneo` sui
  cedolini, esposta nei tre stili PDF); **addizionali a rate dal conguaglio** quando gli
  importi annui sono caricati sul rapporto (regionale e saldo comunale in 11 rate
  gennaio–novembre con ultima a recupero, acconto 30% in 9 rate da marzo; senza dati resta
  l'anteprima, dichiarata nella voce; campi in scheda dipendente); **Fondo Est** (2 €
  lavoratore + 13 € azienda) ed **EBiTer** (0,05%/0,10% su paga base + contingenza,
  mensilità aggiuntive comprese) come regole del CCNL nel dataset voci variabili;
  quattordicesima esposta come mensilità intera (niente «12 × 169,76 = 2.037,10»); TFR
  etichettato «media del mese, ratei 13ª/14ª inclusi»; costi marcati «(parziale)» e
  comprensivi dei fondi datoriali (busta Milano: netto 3.074,27, costo mese 5.449,60, costo
  medio annuo/12 3.252,76); scenario con CF maschile valido (VRDLGI90A01F205J) e dicitura
  presenze non più contraddittoria quando le variabili sono tutte a zero. Migrazioni
  `ConguagliAddizionaliECuneoCedolino` per entrambi i provider. Verifiche di quel giro:
  suite backend e frontend verdi, build e lint verdi; il verdetto «tabella H011 Milano
  verificata, busta completa non ancora» è registrato nel documento e la dicitura
  generale resta.

  **Chiusura dei punti aperti implementabili (25 agosto 2026, /goal)**: **progressivi annui
  in busta** (imponibile previdenziale/IRPEF, IRPEF pagata e detrazioni col cuneo, sommati
  dai mesi elaborati dell'anno — chiusi e storici pre-workflow, mai le bozze — stampati
  nella fascia dedicata del PDF classico e nel quadro 05 dei colori, congelati nello
  snapshot alla chiusura insieme all'ulteriore detrazione cuneo che al codec mancava);
  **contributo di solidarietà 10%** sul fondo sanitario c/azienda (art. 9-bis L. 166/1991,
  parametro dell'anno, voce figurativa nel costo: busta Milano 5.450,90 / 3.254,06);
  **campo sesso in anagrafica** (M/F facoltativo) con rifiuto al salvataggio se il giorno
  di nascita del codice fiscale dice il contrario, anche quando cambia il solo CF; **flag
  «aderisce ai fondi contrattuali»** sull'azienda (default sì, anche per gli archivi
  esistenti via default di colonna) che spegne Fondo Est/EBiTer/solidarietà quando l'azienda
  non aderisce. Migrazioni `CoerenzaSessoEFondiContrattuali` per entrambi i provider; campi
  esposti in scheda dipendente e scheda azienda. Restano dichiarati aperti: INAIL nel costo
  ed elementi sostitutivi per le aziende non aderenti. Verifiche: suite backend completa e
  **53/53 frontend** verdi, build e lint puliti, busta rigenerata.

- **UniEmens: il flusso passa il controllo ufficiale INPS (24 agosto 2026)**. Il generatore è
  stato riscritto sul tracciato vero (radice `DenunceMensili`, schema di validazione 4.32.0,
  allegato tecnico 4.32.3): la bozza precedente aveva una struttura inventata. Lo XSD ufficiale
  è **incorporato come risorsa** (`Paghe.Core/AdeTelematico/UniEmens/Schema`) e
  `UniEmensValidator` valida offline a ogni generazione, come per la FatturaPA. `SettimaneInps`
  implementa la numerazione INPS delle settimane (progressivo annuale da domenica a sabato, 1-54:
  **non** è la ISO 8601), riscontrata su un flusso reale. `UniEmensService` costruisce il flusso
  da cedolini, anagrafica e cartellino, e restituisce l'elenco dei dati che il tracciato pretende
  e l'archivio non ha (`daCompletare`), senza inventarli. Un file generato da qui è stato
  elaborato dal **validatore automatico ufficiale INPS 4.2.0**: `RC=OK`, 0 errori bloccanti,
  file `.UniEM` e ricevuta prodotti. Le regole emerse dai controlli reali (unità
  operativa/produttiva obbligatorie a zero, `GiorniContribuiti` alternativo a `OreContribuite`,
  `RetribTeorica` obbligatoria, calendario giornaliero sempre esposto, codici fiscali verificati
  nel carattere di controllo, matricola verificata sugli archivi INPS) sono nel codice e nei
  test. Restano fuori: inquadramento contributivo in anagrafica (tipo contribuzione,
  cittadinanza, CNEL, professione ISTAT), eventi tutelati con codici evento, conguagli, gestioni
  speciali, archiviazione della pratica e schermata SPA. Procedura ripetibile in
  `docs/UNIEMENS-CONTROLLO-UFFICIALE-INPS.md`.

  **Anagrafica contributiva (24 agosto 2026)**: `RapportoLavoroEntity` tiene ora cittadinanza
  (codifica Comunicazioni Obbligatorie), tipo di part-time (`P`/`V`/`M` → Qualifica2), qualifica
  professionale ISTAT e tipo di contribuzione; migrazioni `AnagraficaContributivaUniEmens` per
  entrambi i provider. I formati sono validati in `AnagraficaContributiva` (Paghe.Core) e
  rifiutati al salvataggio con messaggi in italiano; la scheda del dipendente ha la sezione
  «Dati per la denuncia INPS». Il codice contratto CNEL non si digita: arriva dal CCNL
  dell'anno via cedolino. Difetto trovato dai test e corretto: il tracciato ammette solo
  maiuscole senza accenti nei nomi, quindi `UniEmensXmlWriter` li adatta **solo nel file**
  mentre l'archivio conserva la grafia vera.

- **Aurora — DeepSeek + Google Gemini con fallback esplicito (24 agosto 2026)**: il profilo
  cloud può usare DeepSeek come principale e Gemini come riserva senza duplicare le richieste
  riuscite. Chat ed estrazione riprovano una sola volta su Gemini dopo errore/timeout/risposta
  illeggibile di DeepSeek; foto e scansioni vanno direttamente a Gemini, che fornisce la vista.
  Endpoint e modelli sono centralizzati in `CatalogoProviderAiCompatibili`, con allowlist esatta,
  niente proxy/redirect, header client Google e chiavi separate (`Ai__ApiKey` e
  `Ai__GeminiApiKey`) mai restituite dalla UI. Il pannello mostra provider principale, riserva e
  l'eventuale chiave secondaria mancante. `render.yaml` prepara il segreto Gemini e lo script
  locale richiede `.chiavi/deepseek.txt` + `.chiavi/gemini.txt`. Verifiche: **65/65 test AI/API**,
  **945/945 backend**, **48/48 frontend**, build Release completa e SPA verdi.

- **Paghe agricole — motore OTD validato su buste reali (23 agosto 2026)**: primo blocco del
  modulo agricoltura. `MotoreCalcoloCedolinoAgricolo` è separato dal motore ordinario perché il
  settore paga le **ore effettive alla tariffa del contratto provinciale** (salario provinciale +
  terzo elemento), non una mensilità divisa per 26 o 168. Riproduce **al centesimo** tre buste
  paga reali di un OTD di Cuneo (ott/nov/dic 2025, INAZ): lordo, imponibile contributivo
  arrotondato all'euro, quattro trattenute provinciali (FAP 8,84% + FAVLA 0,30% + CAC prov 0,20%
  + CAC naz 0,20% = 9,54%), IRPEF e netto. Le **39 giornate** coincidono con l'estratto conto
  previdenziale INPS del lavoratore. Regole vere emerse dalle buste e ora nel codice: giornate e
  ore sono dati distinti (le giornate vanno all'INPS, non si ricavano dividendo le ore); il
  reddito annuo presunto è una stima del rapporto, non il mese × 12; la detrazione minima da
  tempo determinato (1.380 €, art. 13 c.1 TUIR, aggiunta come parametro dell'anno) azzera l'IRPEF
  dello stagionale; il netto si arrotonda all'euro **per eccesso** e l'ultima busta recupera gli
  arrotondamenti anticipati. I contributi c/azienda NON sono calcolati: in agricoltura li liquida
  l'INPS sulla denuncia mensile PosAgri. Punto aperto dichiarato: la quota deducibile ai fini fiscali
  osservata è 7,80% e resta un parametro esplicito (`AliquotaContributiDeducibili`), non una
  formula. Busta di riferimento e domande per il consulente in
  `docs/buste-reali/busta-05-agricoltura-otd-cuneo-2025.md`.

  **Tabelle provinciali su database e API (24 agosto 2026)**: i parametri agricoli sono
  versionati per anno **e per provincia** (`ParametriAgricoli`/`TariffeAgricole`/
  `ContributiAgricoli`, migrazioni `TabelleAgricole` per SQLite e PostgreSQL), con paghe orarie a
  cinque decimali. `ParametriNormativiService` li carica e li importa in upsert per anno+provincia;
  il seed inserisce la sola tabella osservata di Cuneo 2025 e tace appena arriva il CPL vero.
  Endpoint: `GET /api/agricoltura/province`, `GET /api/agricoltura/tabelle/{anno}/{provincia}`,
  `POST /api/agricoltura/tabelle/importa` (riservato al super-admin, come i CCNL) e
  `POST /api/agricoltura/cedolino/anteprima`, che calcola la busta **senza archiviarla** e
  restituisce la fonte delle tabelle usate. Una provincia senza tabelle è rifiutata con un
  messaggio esplicito invece di ereditare le tariffe di un'altra. Guida in
  `docs/AGRICOLTURA-TABELLE-CPL.md`.

  **Anteprima operativa collegata al rapporto e PDF dedicato (25 agosto 2026)**: la pagina SPA
  **Agricoltura OTD** usa un rapporto `AGRICOLTURA_OPERAI` dell'azienda corrente. Il backend
  ricava dal rapporto tenant-scoped provincia CPL, livello, tipo, flag detrazioni e
  residenza; il browser invia soltanto i dati variabili del mese e deve indicare il reddito annuo
  presunto reale. Gli endpoint
  `POST /api/agricoltura/rapporti/{rapportoId}/cedolino/anteprima` e `/pdf` restituiscono una
  bozza non archiviata; `/province` dichiara anche se per quell'anno esistono i parametri nazionali,
  evitando una tabella apparentemente utilizzabile ma impossibile da calcolare. Il renderer
  `CedolinoAgricoloPdf` è separato da quello ordinario: conserva `AGR_GG`, `AGR_ARROT` e le voci
  datoriali informative, mostra tariffa a cinque decimali, ore/giornate distinte, fonte e avviso
  **non LUL/PosAgri/F24**, senza inventare TFR o ROL. Il flusso ordinario rifiuta esplicitamente un
  rapporto agricolo, così non può archiviare una busta col motore sbagliato. PDF campione:
  `output/pdf/busta-agricola-otd-cuneo-2025-10.pdf`, verificato A4, una pagina, font Lato
  incorporati e controllo visivo completo.

  OTI è riconosciuto dal rapporto ma resta intenzionalmente **fail-closed** nel motore, nell'API
  e nella SPA: la tabella e i tre casi golden disponibili sono OTD, quindi non possono essere
  riutilizzati per salario/terzo elemento OTI. La UI spiega il blocco e disabilita calcolo/PDF.

  Verifiche finali: build Release 0 errori, **1.012/1.012 test backend** e **53/53 frontend**
  verdi, lint senza errori bloccanti, build SPA riuscita, modelli EF senza variazioni pendenti per
  SQLite/PostgreSQL. Restano aperti e dichiarati: archiviazione/snapshot/workflow agricolo,
  cartellino giornaliero e progressivi persistiti, CPL ufficiale completo e livelli verificati,
  golden OTI, contribuzione datoriale complessiva, denuncia mensile UniEmens/PosAgri, riscontro
  F24 e CU agricola; la quota deducibile osservata del 7,80% e lo scarto di 0,01 € di novembre
  richiedono ancora validazione professionale.

- **Aurora — protocollo A3 di proposta, conferma e undo (23 agosto 2026)**: le scritture
  reversibili passano da un intento firmato completo (comando canonico, undo, precondizione/
  postcondizione, diff) cifrato tenant-bound, con token v2 tipizzati: una conferma non vale mai
  come undo né come lease. Conferma, riverifica del grant, quota effetto, applicazione di dominio
  e audit condividono la stessa transazione serializable: il replay non duplica, il rifiuto del
  dominio marca `Conflitto` senza effetti, annullo e ripristino (finestra 24 ore) sono idempotenti.
  `AiRolloutPolicy` tiene il gate fail-closed: default `A2`/`Off` ovunque, Render compreso; il
  collaudo A3 richiede coppie esatte `tenant:utente` e modalità `DryRun`/`Confirmed` esplicite.
  Primo strumento allowlisted `compila_presenze` sul cartellino-bozza versionato. Migrazioni
  `ProtocolloPropostaA3` per entrambi i provider; l'adozione dei database legacy crea gli indici
  dopo l'allineamento delle colonne, così un indice su colonne introdotte dopo la `CreateTable`
  non blocca più l'avvio. Endpoint `conferma`/`annulla`/`undo`/`stato` reali: token non firmato
  dal server → `400` senza toccare provider né dominio. Restano per il gate G5: limiti e
  checkpoint batch, riconciliazione proposta-applicazione-audit, card conferma/undo nella SPA e
  cicli shadow. Verifiche: build Release 0 errori, **905/905 test backend** più il fuzz da
  10.000 tentativi ostili, verde in questa sessione.

- **Fondazione cartellino-bozza per Aurora A3 (22 agosto 2026)**: il calendario presenze può
  essere creato o aggiornato separatamente dal cedolino attraverso un servizio tenant-scoped e
  una API autenticata/rate-limited. Ogni riga ha ID e versione ottimistica; una copia obsoleta
  riceve `409` senza sovrascrivere dati. Il salvataggio ricalcola i totali dalle giornate, rilegge
  il risultato autorevole e rifiuta calendario incompleto, periodo futuro, enum/importi non
  validi, rapporto estraneo, qualunque cedolino già presente e F24 inviato o acquisito. Le
  migrazioni SQLite/PostgreSQL inizializzano anche gli storici a versione 1 e l'adozione senza
  history usa lo stesso default. La capability A3 non è ancora esposta al modello: servono ancora
  patch allowlisted, proposta/conferma/effetto/audit atomici, riverifica del grant e undo sicuro.
  Verifiche del checkpoint: build Release 0 errori e **901/901 test backend** escluso soltanto il
  fuzz lungo da 10.000 tentativi, che resta nel collaudo finale.

- **Aurora — fase A0 chiusa e runtime read-only A2 verificato (22 agosto 2026)**: il
  runtime C# usa `AiToolRegistry` a plugin e default DENY; la SPA invia la lease soltanto
  per lo stesso utente/tenant/azienda/mese e ogni lettura passa dal
  `ProfessionalQueryBroker`. Sono attivi cinque tool di lettura minimizzati; tre descrittori
  di scrittura restano al gate A3 e non vengono offerti. Il SessionLog conserva digest,
  esiti e durate senza payload; guardie server limitano giri, tool call, argomenti, timeout,
  ripetizioni, quota e revoca. Il pannello riconcilia l'inventario dei grant prima
  dell'emissione, revoca anche emissioni tardive/perse, invalida chat e risultati al cambio
  scope e rilegge dal server la quota dopo ogni turno. Il budget complessivo viene deciso e
  mostrato dal server (250 record per la policy corrente); ogni proiezione cedolini resta un
  lotto massimo di 10. Lo scaffold di proposta cifrata/token HMAC esiste, ma
  `/api/ai/chat/conferma` resta **409 al gate A2**: idempotenza crash-safe, riverifica del
  grant e transazione della prima scrittura sono prerequisiti A3, non funzionalità dichiarate
  pronte. Provider locale/cloud e Render restano spenti finché il fondatore non chiude DPA e
  G1. Verifiche: build Release 0 errori, backend **888/888**, frontend **35/35**, modelli EF
  SQLite/PostgreSQL allineati e smoke Production su copia isolata (13 migrazioni,
  `integrity_check=ok`, zero violazioni FK, `/health` 200, anonimo 401, provider spento,
  azione/conferma 409). Il database originale è rimasto invariato.

- **Aurora — primo verticale governato sui cedolini, lotto 10 (verifica finale 22 agosto 2026)**:
  il pannello esegue un controllo deterministico e in sola lettura sui primi 10 rapporti del
  mese scelto. Prima mostra il preflight, poi emette un grant Guidata/S2 legato a utente,
  tenant, azienda, periodo, finalità, risorsa e quota; espone manifest, contatore, «Dati letti
  dal controllo» e «Revoca mandato». Il broker restituisce solo nominativo e stato operativo
  (cartellino presente/da compilare/già elaborato/bloccato/protetto), mai importi, codice fiscale, IBAN o
  dati salute. La quota è per riga restituita. Le policy limitano le risorse per finalità e il
  registry delle capability è applicato in emissione e accesso; S0 senza selettori è negato.
  Provider cloud e locale restano spenti; con una lease valida il runtime può offrire soltanto
  i tool compatibili col manifest, mentre senza lease non chiama mai il provider.
  `/api/ai/chat/azione` resta fail-closed. La suite completa passa **888/888**, inclusi i test
  su zero rapporti, revoca
  tra query e risposta, endpoint 409/no-tools, cambio scope, selezione multi-range, migrazione
  dello schema ibrido reale e integrità delle evidenze del workflow; verdi anche **35/35 test
  frontend**, build completa backend e build frontend. Lo smoke Production su copia isolata del
  database reale conferma provider/chat spenti, azioni `409`, tre storici `PreWorkflow`, 13
  migrazioni e zero violazioni FK; il database originale resta invariato. Le FK composite
  proteggono anche Rapporto/Dipendente, DURC/Azienda e Promemoria/Azienda; l'adozione legacy è
  idempotente, atomica nello storico e fail-closed davanti a orfani, tenant discordanti, indici
  parziali o colonne SQLite sconosciute. È un checkpoint da 10, non un limite di prodotto.

- **Aurora personale: cloud sospeso e spiegazione del testo selezionato (10 agosto 2026)**:
  DeepSeek/GLM/Claude sono spenti nel profilo predefinito e in `render.yaml`; i deploy Render
  automatici sono disabilitati. `Ai:SoloLocale=true` accetta per ora soltanto `Provider=Spento`:
  anche Ollama sul loopback canonico è bloccato nel codice finché il gate G1 non è provato.
  L'icona Aurora è disponibile in
  ogni schermata autenticata e come pulsante mobile flottante: l'utente seleziona il testo nella
  parte centrale dell'app, vede un'anteprima rimovibile e lo invia una sola volta. Il payload è
  separato, limitato a 4.000 caratteri e marcato come non fidato; non entra nella cronologia.
  Selezioni che attraversano aree private e testi con password, chiavi o token sono rifiutati
  prima di qualunque modello. La storia proveniente dal browser conserva solo domande `user`.
  Finché G1 non è chiuso, il modello locale resta spento. Anche dopo l'attivazione Aurora parte
  in modalità sola spiegazione: nessun catalogo strumenti, nessuna lettura archivio, nessuna
  scrittura; l'endpoint storico `/api/ai/chat/azione` risponde 409. La baseline di questo
  incremento era **52 test AI** e **18 test frontend**; i conteggi aggiornati sono nella voce
  del 17 agosto. Guida e stato in
  `docs/CHAT-AI-DEEPSEEK.md`.

- **Webapp pubblicata online (6 agosto 2026)**: <https://paghepro.onrender.com>, un solo servizio
  Render Free costruito da `Dockerfile.railway` (SPA e API sulla stessa origine, niente CORS né
  `VITE_API_URL`) con database PostgreSQL su Supabase in Irlanda. Il database sta fuori dal
  container perché il piano Free di Render non ha disco persistente. `render.yaml` descrive il
  servizio e tiene i tre segreti come `sync: false`. Verificato dal vivo: `/health`, migrazioni EF
  applicate su PostgreSQL, registrazione, creazione azienda e **dati ancora presenti dopo il
  riavvio del servizio**. Limiti accettati: il servizio si addormenta dopo 15 minuti di inattività
  (risveglio ~60 s) e OCR, JAR di controllo AdE, firma CNS e invio SdI reale non sono disponibili
  nel container. Procedura in `docs/DEPLOY-GRATIS.md`. **Non è ancora un ambiente validato per
  cedolini consegnati a clienti**: il motore resta BOZZA DA VALIDARE e mancano retention GDPR e
  prova di ripristino del backup.
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
- **UNILAV incollato + guida ai dati mancanti**: oltre al PDF/foto (`POST /api/unilav/pdf`) si può
  incollare il testo della comunicazione (`POST /api/unilav/testo`), anche in forma di riepilogo con
  elenchi puntati e grassetti. Il parser riconosce le sezioni (datore, lavoratore, rapporto,
  rappresentante legale) e l'import **scrive davvero** l'anagrafica letta: sede, telefono, ATECO,
  matricola INPS, P.A.T. INAIL, legale rappresentante sull'azienda (solo campi vuoti e solo se è lo
  stesso datore), tipo contratto, ore settimanali, fine rapporto per il tempo determinato e
  **percentuale di part-time dedotta dalle ore** (il motore calcola su `PercentualePartTime`: senza,
  un part-time verrebbe pagato intero). Il livello UNILAV («000008 - VII») è riportato al codice del
  CCNL solo se quel livello esiste davvero. La risposta include `daCompletare[]` (campo, dove si
  completa, perché serve, dove trovarlo) e la SPA lo mostra come popup guidato che porta nella
  sezione giusta con l'azienda già scelta. Non vengono dedotti: qualifica contrattuale (l'UNILAV dà
  la professione ISTAT, cambia le aliquote INPS) e regione/comune di residenza.
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
  PDF (`CedolinoPdf`) usa `CedolinoProfessionalePdf`: un prospetto A4 bianco, compatto e operativo,
  con testata azienda/periodo, griglia anagrafica e rapporto, tabella voci a righe alternate, quadro
  contributivo-fiscale, ratei e netto conclusivo. Il caso ordinario resta su una pagina; l'allegato
  presenze viene creato solo con calendario giornaliero o variabili aggiuntive. Con molte voci la
  tabella continua senza troncamenti e ogni pagina ripete azienda, periodo e titolo. Il renderer usa
  testo selezionabile, font incorporati, IBAN mascherato e mantiene gli storni di competenza nella colonna corretta;
  elimina la falsa quietanza e non presenta come LUL l'allegato presenze. La pagina **Cedolini** è
  solo archivio/download per evitare ricalcoli concorrenti che azzerino le causali. Il PDF ha testata ditta
  con P.IVA e matricola INPS/P.A.T. INAIL, riquadro inquadramento dipendente (CF, qualifica,
  livello, CCNL, codice alfanumerico unico CNEL quando fornito, tipo contratto, mensilità, orario
  e IBAN mascherato), corpo voci con colonne quantità/base/competenze/trattenute, dati
  previdenziali e fiscali, riepilogo causali, saldi ferie/ROL, TFR e costo azienda, netto.
  I cedolini storici privi del cartellino non interrompono più la stampa: presenze, calendario e
  ANF vengono dichiarati non disponibili senza inventare valori. Verificati 9 test PDF del
  cedolino: ordinario su una pagina, scenario denso di 40 voci su due pagine con testata ripetuta e
  calendario completo di 31 giorni su un allegato dedicato; tutti gli artefatti sono A4 e usano Lato incorporato.
  L'anagrafica arriva da `IntestazioneCedolino` (Paghe.Core/Domain) costruita in
  `CedoliniService.RicalcolaPerPdfAsync`. Fuori dal nuovo workflow resta **BOZZA DA VALIDARE**:
  mancano calendario giornaliero sempre disponibile, regole CCNL tutte versionate, saldi storici
  e alimentazione persistita del codice CNEL. Il percorso
  `Bozza -> Controllata -> Validata -> Chiusa` crea invece snapshot e documento immutabili e
  richiede la validazione esplicita del professionista prima dello stato definitivo. Vedi
  `docs/RICERCA-CEDOLINO-PROFESSIONALE-2026.md`.
- **Migrazioni EF versionate (schema del database)**: lo schema è gestito da migrazioni EF, non
  più da `EnsureCreated()` + DDL scritto a mano. EF ammette un solo assembly di migrazioni per
  contesto, quindi ce n'è uno per provider: `Paghe.Migrazioni.Sqlite` e `Paghe.Migrazioni.Postgres`
  (scelti in `Program.cs` con `MigrationsAssembly`). All'avvio `SchemaDatabase.Applica` applica le
  migrazioni mancanti e, sui database creati **prima** delle migrazioni, li **adotta**: completa
  colonne, tabelle, indici e vincoli usando le operazioni versionate, preserva i valori e applica
  soltanto i backfill semantici espliciti (per esempio `PreWorkflow` e ANF `DaVerificare`). Schemi
  parziali, orfani o tenant discordanti restano fail-closed e non ricevono uno storico falso.
  L'esito è nel log
  (`Schema database: ...`). Verificato dal vivo su SQLite nuovo, SQLite di sviluppo esistente
  (40→52 tabelle, dati intatti), PostgreSQL nuovo, PostgreSQL tipo Supabase (schema `public` con
  tabelle estranee) e PostgreSQL con schema preesistente senza storico. **Una modifica al modello
  richiede una migrazione per entrambi i provider**: comandi in `docs/PRODUZIONE.md` §2.
  `AllineaColonneMancanti` resta solo come passo interno dell'adozione.
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
  Anthropic, structured output con schema JSON), `DeepSeek`/`Gemini`/`Glm` (default endpoint/modello
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
2. Migrazioni EF versionate **fatte**; restano RLS PostgreSQL, deploy, secret reali,
   backup/restore e monitoraggio prima del go-live.
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
