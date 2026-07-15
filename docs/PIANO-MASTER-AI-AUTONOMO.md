# PaghePro — Master plan AI-first a massima automazione consentita

> Piano di prodotto, architettura, conformità, sviluppo e lancio.
> Data di redazione: **15 luglio 2026**.
> Stato: **piano soltanto; nessuna fase è autorizzata o implementata da questo documento**.
> Ambizione: far svolgere all'AI tutto ciò che il software può svolgere in modo affidabile,
> lasciando alle persone soltanto approvazioni, deleghe, firme, validazioni professionali e
> attività che la legge o un ente non permettono di automatizzare.

> **Profilo scelto per la prima versione:** il piano ristretto
> [`PIANO-PERSONALE-PRIVACY-FIRST.md`](PIANO-PERSONALE-PRIVACY-FIRST.md) prevale su questo master.
> Disabilita chat generale, AI cloud, connettori esterni e lettura non esplicita dei file. Questo
> master resta una mappa di possibilità future, da non eseguire fuori dal profilo personale.

---

## 0. Risposta diretta: cosa significa «completamente AI»

PaghePro può diventare un ufficio digitale quasi autonomo: legge documenti, trova dati mancanti,
prepara paghe e adempimenti, controlla anomalie, compone comunicazioni, esegue controlli ufficiali,
organizza le approvazioni e segue ricevute e scadenze.

Non deve però diventare un sistema in cui un modello linguistico inventa importi o prende decisioni
con effetti legali. La forma corretta è:

```text
AI legge, organizza, spiega e propone
        ↓
codice deterministico calcola e valida
        ↓
controllore ufficiale verifica il file
        ↓
datore per sé o professionista abilitato per terzi approva quando necessario
        ↓
connettore ufficiale invia e acquisisce la ricevuta
        ↓
audit e conservazione rendono tutto dimostrabile
```

Quindi l'obiettivo non è «zero persone in assoluto», ma **massima riduzione misurata del lavoro
ripetitivo tramite gestione per eccezioni** e **nessuna azione critica senza controllo, delega e
prova**.

### 0.1 Due programmi distinti nello stesso piano

1. **AI che costruisce PaghePro**: agenti di sviluppo analizzano il repository, scrivono codice,
   testano, revisionano e preparano release. Il fondatore interviene solo per scelte di prodotto,
   credenziali, contratti e accettazione finale.
2. **AI dentro PaghePro**: agenti applicativi aiutano studi e aziende a svolgere il lavoro
   quotidiano, usando strumenti interni limitati e sempre tracciati.

### 0.2 Regole che non si possono negoziare

- Importi e aliquote: `decimal`, mai `float` o `double`.
- Ogni dato persistito e ogni ricerca semantica sono isolati per `tenantId`.
- Parametri normativi da database versionato per periodo, mai dal prompt e mai cablati nel calcolo.
- Il modello non scrive direttamente nel database e non possiede credenziali di enti o provider.
- Ogni strumento dell'agente applica permessi, validazione, idempotenza e audit lato server.
- Cedolini, F24, CU, 770, LIPE, UniEmens, INAIL e bilanci restano **bozza da validare** finché
  non superano i controlli richiesti e la validazione professionale prevista.
- Nessun invio reale tramite automazione del browser sui portali pubblici: solo API/provider/canali
  ufficiali, oppure pacchetto pronto per caricamento manuale.
- Nessun codice AGPL/GPL nel SaaS senza decisione esplicita e documentata.
- Con AI disattivata, i flussi fondamentali continuano a funzionare in modalità deterministica.

---

## 1. Legenda operativa

| Simbolo | Responsabile | Significato |
|---|---|---|
| 🤖 | Agente AI di sviluppo | Può analizzare, scrivere codice, documentare e testare nel repository. |
| ⚙️ | Software deterministico | Regola, calcolo o validazione eseguiti da codice verificabile. |
| 🧠 | AI applicativa | Legge, classifica, estrae, spiega, cerca anomalie e propone. |
| 👤 | Fondatore/cliente | Scelta commerciale, account, credenziale, mandato o approvazione. |
| ⚖️ | Professionista | Consulente del lavoro, commercialista, legale, DPO o revisore qualificato. |
| 🔑 | Fornitore/ente | Account, API, certificato, abilitazione, kit o ambiente esterno. |
| ⏳ | Attesa esterna | Tempi non controllabili dal team software. |
| `[x]` | Osservato | Presente nel repository o documentato, ma non implica certificazione normativa. |
| `[~]` | Parziale | Esiste una base, manca uno o più gate tecnici, ufficiali o professionali. |
| `[ ]` | Da fare | Non verificato come completo. |

### 1.1 Assi obbligatori di stato per ogni capacità

Un solo enum lineare confonderebbe dimensioni che avanzano in modo diverso. Il registro canonico
deve quindi conservare almeno questi assi indipendenti:

| Asse | Valori indicativi |
|---|---|
| `maturitaTecnica` | assente / prototipo / implementata / testata / operabile |
| `evidenzaUfficiale` | non applicabile / non eseguita / negativa / positiva / scaduta |
| `validazioneProfessionale` | non applicabile / richiesta / in corso / approvata / revocata |
| `rollout` | spento / interno / shadow / pilota / produzione limitata / generale |
| `validitaNormativa` | bozza / attiva / in scadenza / sospesa / sostituita |
| `rischioAi` | non AI / da classificare / non alto rischio / alto rischio / vietato |

Lo stato commerciale `utilizzabile` viene derivato da una policy sui sei assi e sul perimetro del
cliente; non viene impostato manualmente. Non si userà più una sola parola come «fatto» per
confondere codice presente, file formalmente valido e funzione legalmente utilizzabile.

---

## 2. Stato iniziale osservato e problema di fonte unica

Il repository è più avanti di diversi documenti storici. Sono osservabili almeno:

- [x] backend .NET, API, persistenza EF Core e frontend React;
- [x] autenticazione JWT, 2FA, ruoli e tenant derivato dal token;
- [x] parametri annuali su database e numerosi motori paghe/fiscali;
- [x] cifratura di campi sensibili, audit EF e diritto all'oblio di base;
- [x] cedolini archiviati, presenze mensili, F24, CU PDF, UniEmens in bozza e INAIL iniziale;
- [x] FatturaPA, controlli XSD/SdI, gateway provider e firma digitale;
- [x] pratiche, scadenzario, notifiche, contabilità IVA e bilancio;
- [x] livello AI multi-provider per completare l'estrazione da visura;
- [x] numerosi test automatici e dataset demo tenant-isolato.

Restano parziali o non dimostrati come pronti per uso reale:

- [~] schema di produzione: migrazioni definitive, PostgreSQL/RLS, restore provato, storage oggetti;
- [~] cartellino giornaliero, snapshot immutabili e copertura completa delle regole paghe;
- [~] UniEmens, INAIL, CU/770/LIPE/IVA e altri tracciati sottoposti ai validatori correnti;
- [~] account provider SdI, webhook, fatture passive e conservazione;
- [~] log di accesso in lettura, retention completa, DPIA e governance AI;
- [~] misurazione dell'accuratezza AI su un corpus reale e anonimizzato;
- [~] onboarding prima-del-salvataggio, Inbox documentale e revisione side-by-side;
- [~] prima nota completa, riconciliazione bancaria, portali cliente/dipendente;
- [~] validazione professionale di calcoli, CCNL, assenze e documenti;
- [~] pilotaggio, contratti, assicurazione, assistenza e processo di aggiornamento normativo.

### 2.1 Contraddizioni da risolvere prima dello sviluppo

`PIANO.md`, `CLAUDE.md`, `docs/COSA-MANCA.md`, `docs/PIANO-COMPLETAMENTO.md` e i piani AI hanno
date e livelli di avanzamento diversi. Alcuni elementi indicati come mancanti risultano presenti;
altri sono descritti come completi ma restano bozze non validate.

**Decisione:** la Fase 0 crea una matrice generata da codice, test e prove. Fino a quella matrice,
questo documento usa `osservato`, `dedotto` e `da verificare`, non certifica il prodotto attuale.

---

## 3. Prodotto finale desiderato

PaghePro diventa una **AI Payroll, Tax & Accounting Operations Console** per studi piccoli e
micro-imprese italiane. La schermata principale non mostra solo grafici: mostra cosa deve essere
fatto oggi e porta ogni pratica da input a ricevuta.

### 3.1 Promessa commerciale corretta

> «PaghePro legge i documenti, prepara le lavorazioni, controlla errori e scadenze e accompagna
> ogni adempimento fino alla ricevuta. I calcoli sono eseguiti da motori versionati; l'AI propone
> e spiega; il professionista mantiene il controllo delle decisioni e degli invii previsti.»

### 3.2 Risultati misurabili

Prima del pilota si preregistrano attività, inizio/fine, campione, mediana e p90. Solo dopo la
baseline time-and-motion, gli obiettivi iniziali da validare sono:

- ridurre fino al 70% il tempo manuale mediano di acquisizione dei documenti nel pilota;
- ridurre fino al 60% il tempo umano mediano di preparazione del ciclo paghe nella nicchia scelta;
- Zero scritture o invii non autorizzati nei test e in produzione.
- Zero fughe cross-tenant nei test automatici, nei test di penetrazione e negli eval AI.
- 100% dei file telematici rilasciati al cliente passati dal controllo ufficiale disponibile,
  misurando separatamente first-pass yield, rilavorazioni, ricevute accolte e scarti;
- 100% dei numeri esposti con origine, periodo, regola e documento riconciliabili.
- 100% delle proposte AI distinguibili dai dati deterministici e dalle conferme umane.
- riduzione progressiva del costo AI per pratica senza peggiorare gli eval di qualità;
- margine unitario positivo dopo AI, provider, onboarding e supporto prima della scala.

### 3.3 Nicchia iniziale

Il primo rilascio non tenta di coprire tutti i CCNL e tutte le aziende italiane. Gate consigliato:

- uno studio pilota;
- 5-20 aziende;
- un solo CCNL già modellato e validato;
- lavoratori dipendenti standard, con eccezioni dichiarate fuori perimetro;
- fatturazione e adempimenti realmente usati dal pilota;
- estensione ad altri CCNL solo dopo il Gate F17: almeno tre cicli mensili consecutivi, una
  scadenza significativa e tutte le evidenze previste superate.

---

## 4. Matrice: cosa fa l'AI, cosa fa il codice, cosa resta umano

| Attività | AI | Codice deterministico | Persona/ente | Regola finale |
|---|---|---|---|---|
| Riconoscere un documento | classifica e propone | controlla MIME, hash e schema | corregge se incerto | AI ammessa |
| Estrarre P.IVA, CF, date, importi | estrae con confidenza | valida formato e quadrature | conferma campi critici | AI ammessa |
| Calcolare cedolino | spiega input e anomalie | calcola interamente | valida casi reali | AI non calcola |
| Scegliere aliquota/codice tributo | cerca regola candidata | usa solo regola DB attiva | professionista valida regola | niente regole dal prompt |
| Preparare F24/UniEmens/CU | raccoglie dati mancanti | genera file e diagnostica | approva | bozza fino ai gate |
| Inviare a SdI/ente | prepara e monitora | connettore firmato/idempotente | datore per sé o professionista abilitato per terzi | mai browser automation |
| Classificare una spesa | propone conto e IVA | applica vincoli fiscali | commercialista conferma | storico correzioni |
| Riconciliare banca/fatture | propone abbinamenti | verifica importi/date/valuta | conferma ambigui | auto solo soglia alta |
| Suggerire risparmio lecito | trova opportunità | stima con regole versionate | commercialista approva | fonte obbligatoria |
| Decidere assunzione/licenziamento/paga | non decide | non automatizza | datore/professionista | fuori perimetro |
| Valutare performance dipendente | non esegue | non implementa | datore con processo legale | fuori perimetro iniziale |
| Rispondere al cliente | compone bozza | applica template/privacy | invio automatico solo su mandato | audit obbligatorio |
| Scrivere codice PaghePro | implementa e testa | CI applica gate | fondatore accetta release | niente auto-prod iniziale |

---

## 5. Livelli di autonomia del prodotto

### L0 — Informazione

L'AI legge dati autorizzati e risponde. Nessuna scrittura. Esempio: «quali aziende hanno dati
UniEmens mancanti?».

### L1 — Bozza

L'AI crea una proposta separata dai dati definitivi. Esempio: estrazione di una visura o bozza di
messaggio. L'utente può correggere, confermare o scartare.

### L2 — Scrittura interna reversibile

Il sistema può salvare automaticamente classificazioni a rischio basso quando confidenza, regole
e permessi superano la soglia. Deve esistere undo, cronologia e compensazione.

### L3 — Esecuzione interna programmata

Il sistema avvia controlli, genera bozze, crea task e solleciti in base a un mandato configurato.
Non firma, non paga e non invia dichiarazioni.

### L4 — Effetto esterno con approvazione forte

Invio fattura, documento telematico, email sensibile, firma o pagamento. Richiede anteprima,
ruolo autorizzato, MFA recente, idempotency key e registrazione dell'approvazione.
L'approvazione tecnica non sostituisce mai l'abilitazione o il giudizio professionale richiesti.

### L5 — Mandato esterno limitato

Possibile solo dopo pilota, parere legale e configurazione esplicita per singolo tipo di azione.
Esempio: invio automatico di un promemoria non sensibile già approvato come template. Mai per
cedolini, dichiarazioni, variazioni del rapporto, pagamenti o decisioni sui lavoratori nella
prima versione.

### 5.1 Politica predefinita

- Nuova funzione: parte sempre da L0/L1.
- Passaggio a L2/L3: richiede eval, audit, undo e 30 giorni senza incidente nel pilota.
- Passaggio a L4: richiede controllo ufficiale, approvazione forte e prova end-to-end.
- Passaggio a L5: decisione distinta, DPIA/risk assessment aggiornati e possibilità di revoca.

---

## 6. Architettura obiettivo

```text
Frontend studio / portale cliente / portale dipendente
                 │
                 ▼
API + autenticazione + autorizzazione + tenant context
                 │
      ┌──────────┴───────────┐
      ▼                      ▼
Motori deterministici     AI Orchestrator
paghe/fisco/file          piano + agenti specializzati
      │                      │
      │                Tool Registry sicuro
      │                read / draft / approve / execute
      └──────────┬───────────┘
                 ▼
Workflow engine + code + outbox + idempotenza
                 │
      ┌──────────┼──────────────┐
      ▼          ▼              ▼
PostgreSQL    Object storage   Connettori ufficiali
+ RLS         cifrato/hash     SdI/INPS/AdE/INAIL/banca
      │          │              │
      └──────────┼──────────────┘
                 ▼
Audit append-only + trace AI + ricevute + conservazione
```

### 6.1 Confini obbligatori e ADR prima delle feature

- `Paghe.Core`: calcoli e modelli puri; nessun SDK AI e nessuna rete.
- `Paghe.Persistence`: dati tenant-isolati, transazioni, outbox e snapshot.
- `Paghe.Api`: autenticazione, policy, contratti HTTP, model gateway e tool adapter.
- Prima di implementare feature AI, un ADR decide se creare `Paghe.Ai`; se approvato, contiene
  orchestrazione, schemi, routing e guardrail, senza logica normativa propria.
- Prima di processi asincroni, un ADR decide il worker definitivo (`Paghe.Worker` o equivalente)
  per OCR, code, validatori JAR, webhook e lavori lunghi.
- Frontend: revisione, approvazione e spiegazione; nessuna chiave AI o provider nel browser.
- Endpoint, DTO, configurazioni EF e client frontend vengono separati per feature; `Program.cs`,
  `PagheDbContext.cs`, `Dtos.cs` e `api.ts` restano composition root/registri sottili.
- Ogni feature ha owner di file dichiarato; gli hotspot condivisi vengono modificati soltanto nel
  cablaggio finale serializzato.

---

## 7. Piattaforma AI da costruire una volta sola

### 7.1 Model Gateway con estensione progressiva

- [ ] 🤖 Definire `IModelGateway` separato dall'attuale estrattore documentale.
- [ ] 🤖 Definire un'interfaccia provider-neutra, ma qualificare inizialmente **un solo provider**
  più il fallback deterministico; secondo provider e modello locale entrano solo su esigenza
  privacy, resilienza o costo dimostrata.
- [ ] 🤖 Supportare capacità dichiarate: testo, visione, structured output, tool calling,
  contesto massimo, residenza/retention, costo e latenza.
- [ ] 🤖 Configurare modelli per compito, non un solo modello globale.
- [ ] 🤖 Usare prima parser/XML/OCR deterministici, poi il modello solo sui campi irrisolti.
- [ ] 🤖 Prevedere provider cloud, modello locale e modalità completamente spenta.
- [ ] 🤖 Pin della versione in produzione; upgrade soltanto dopo eval di regressione.
- [ ] 🤖 Timeout, retry limitato, circuit breaker e fallback senza duplicare azioni.
- [ ] 🤖 Budget giornaliero/mensile per tenant, alert e blocco morbido.
- [ ] 🤖 Non inviare dati sanitari/sindacali a un provider cloud finché DPA, DPIA e configurazione
  di retention non sono approvati; preferire estrazione locale o minimizzata.

Per OpenAI, alla data del piano, la documentazione ufficiale indica Responses API, Structured
Outputs e function calling. La famiglia corrente documentata è GPT-5.6 (`sol`, `terra`, `luna`).
Questi nomi restano configurazione, non una dipendenza di dominio. Il routing iniziale proposto è:

- `luna` o equivalente: classificazione semplice ad alto volume;
- `terra` o equivalente: estrazione e assistenza ordinaria;
- `sol` o equivalente: casi complessi, solo quando gli eval mostrano un vantaggio;
- modello locale: documenti sensibili o funzionamento offline;
- codice deterministico: calcolo, controllo formale e scelta della regola attiva.

### 7.2 Tool Registry e controllo azioni

Ogni tool deve dichiarare:

- nome e versione;
- schema JSON di input e output;
- livello di autonomia massimo;
- permessi/ruoli richiesti;
- tenant ricavato dal server, mai accettato dal modello;
- operazione read-only, bozza, scrittura reversibile o effetto esterno;
- precondizioni e validatori;
- idempotency key;
- dry-run e anteprima;
- strategia di undo/compensazione;
- dati letti e modificati;
- motivazione e prove da salvare;
- approvazione e MFA richieste;
- limite di chiamate/costo;
- comportamento in timeout o risposta ambigua.

Tool vietati: SQL libero, shell, accesso filesystem generico, richiesta HTTP arbitraria, recupero
segreti, cambio tenant, disabilitazione audit, invio/cancellazione generici.

### 7.3 Invarianti e primitive workflow

Non si costruisce subito un workflow engine universale. F2 fornisce coda, outbox, inbox,
idempotenza e retry; ogni dominio mantiene la propria macchina a stati esplicita. Un'astrazione
generica nasce soltanto dopo almeno due workflow reali che dimostrino la stessa struttura.

- [ ] 🤖 Modellare ogni processo lungo come macchina a stati persistita **nel proprio dominio**.
- [ ] 🤖 Aggiungere in F2 coda, retry con backoff, dead-letter queue e dashboard errori.
- [ ] 🤖 Implementare in F2 transactional outbox/inbox.
- [ ] 🤖 Bloccare doppio invio con chiave idempotente del documento/pratica.
- [ ] 🤖 Aggiungere pausa, ripresa, annullamento o compensazione solo al dominio che ne ha bisogno.
- [ ] 🤖 Versionare lo stato/contratto del processo specifico quando deve sopravvivere ai deploy.
- [ ] 🤖 Separare nel dominio `Proposto`, `Approvato`, `Eseguito`, `Ricevuto`, `Conservato`.

### 7.4 RAG normativo controllato

- [ ] 🤖 Registro fonti ufficiali con ente, URL, titolo, data pubblicazione e data acquisizione.
- [ ] 🤖 Salvataggio hash del documento e versione del parser.
- [ ] 🤖 Periodo di validità `validoDal`/`validoAl` e anno di competenza.
- [ ] 🤖 Workflow bozza → doppia revisione → attiva → sostituita/ritirata.
- [ ] 🤖 Citazione puntuale dalla risposta alla fonte/regola usata.
- [ ] 🤖 Alert se una fonte cambia o un file ufficiale viene sostituito.
- [ ] 🤖 Nessuna pagina web commerciale usata come fonte normativa primaria.
- [ ] 🤖 Nessun retrieval cross-tenant; fonti pubbliche separate dai documenti del cliente.
- [ ] 🤖 Rifiuto esplicito se manca una regola approvata per periodo e caso.

### 7.5 Sicurezza specifica per agenti

- [ ] 🤖 Trattare ogni documento caricato come contenuto non fidato: le istruzioni scritte dentro
  un PDF non possono cambiare il comportamento dell'agente.
- [ ] 🤖 Scansione malware, verifica firma MIME, limiti dimensione/pagine e sandbox OCR.
- [ ] 🤖 Protezione da prompt injection, tool injection, data exfiltration e RAG poisoning.
- [ ] 🤖 Redazione dei dati non necessari prima della chiamata AI.
- [ ] 🤖 Segreti in secret manager/KMS, mai in prompt, log o stack trace.
- [ ] 🤖 Safety identifier pseudonimo per utente, se supportato dal provider.
- [ ] 🤖 Kill switch per provider, modello, tenant, tool e singolo workflow.
- [ ] 🤖 Test automatici di abuso: cambio tenant, tool non consentito, istruzioni nel documento,
  conferma simulata, replay webhook e duplicazione invio.

### 7.6 Evals e qualità AI

Prima di usare un modello in produzione servono:

- gold set anonimizzato e con base giuridica/provenienza documentata; il consenso del lavoratore
  non è la base predefinita;
- almeno 100 documenti per scoprire errori nel prototipo, senza usarli per dimostrare affidabilità
  estrema; il campione di rilascio viene dimensionato per tipo documento, campo e frequenza;
- suddivisione train/dev/test senza duplicati;
- metriche campo per campo e per documento: precision, recall, exact match, coverage, calibration,
  tasso di astensione, false acceptance e false rejection;
- casi fotografati male, scansioni, layout diversi e valori limite;
- eval di rifiuto, sicurezza, tenant isolation e azioni non autorizzate;
- confronto parser-only, modello locale e provider cloud;
- soglie diverse per campi critici e non critici;
- canary prima dell'upgrade modello;
- registrazione di modello, prompt, tool, regola e corpus usati.

Soglie-obiettivo iniziali da validare statisticamente:

| Categoria | Gate proposto |
|---|---|
| CF, P.IVA, IBAN, matricole, importi e date critiche | lower bound concordato sull'intervallo di confidenza; fino ad allora revisione obbligatoria |
| Campi descrittivi | almeno 97% exact/semantic match |
| Documento classificato | almeno 99% sul corpus in-scope |
| Azioni esterne non autorizzate | 0 su tutti i test |
| Fughe cross-tenant | 0 su tutti i test |
| File ufficiali | 100% passaggio schema + validatore disponibile |
| Risposte normative senza fonte | 0 nel percorso operativo |

Le soglie non trasformano l'AI in fonte di verità. Dichiarare 99,5% richiede migliaia di osservazioni
indipendenti per i campi critici e adeguata copertura di layout/casi rari; zero errori su 100 o 500
documenti non basta. Se il lower bound statistico non è raggiunto, la revisione dei campi critici
resta obbligatoria. I campi critici restano comunque verificati da regole deterministiche quando
possibile.

### 7.7 Ownership della piattaforma AI

La piattaforma non viene costruita come big-bang generico:

| Componente | Fase owner | Primo deliverable |
|---|---|---|
| ADR progetti/confini e worker | F2 | decisione + skeleton minimale |
| coda/outbox/idempotenza | F2 | primitive infrastrutturali |
| Model Gateway | F7A | un provider qualificato + fallback deterministico |
| sicurezza documenti/prompt injection | F1-F3/F7A | policy + test Inbox |
| eval framework documentale | F7A | suite per UNILAV/visura |
| workflow di dominio | fase del dominio | macchina a stati specifica, non framework astratto |
| RAG/fonti e lineage | F4 | registry regole/fonti |
| Tool Registry | F14 | tool read-only del Copilot |
| eval tool/agent | F14 | abuso, tenant, astensione e fonti |
| budget/model routing avanzato | dopo F7A | solo su metriche reali |

Ogni fase implementa soltanto il minimo componente necessario al proprio vertical slice.

---

## 8. Agenti applicativi finali

| Agente | Compito | Tool consentiti all'inizio | Autonomia massima iniziale |
|---|---|---|---|
| Intake documenti | classifica, estrae, deduplica e instrada | upload, OCR, parser, proposta | L1 |
| Azienda 360 | completa anagrafica e segnala dati mancanti | lettura/proposta anagrafica | L1 |
| Dipendente 360 | interpreta UNILAV e verifica rapporto | lettura/proposta rapporto | L1 |
| Preparatore paghe | costruisce checklist del mese | letture presenze/rapporti | L1/L3 |
| Controllore paghe | cerca anomalie e differenze storiche | diagnostica read-only | L0 |
| Telematico | prepara pacchetti e avvia validatori | genera/valida bozza | L3 |
| Fatture | gestisce attive/passive e ricevute | bozza/instradamento | L1/L3 |
| Contabilità | propone prima nota e piano conti | proposta registrazione | L1 |
| Riconciliazione | abbina banca, fatture e pagamenti | proposta matching | L1/L2 |
| Scadenze | crea task, priorità e solleciti | task/notifiche bozza | L3 |
| CFO e tax opportunity | spiega trend e opportunità lecite | letture + simulazioni | L0/L1 |
| Comunicazioni | prepara email/SMS/WhatsApp | template e bozza | L1/L3 |
| Supporto | guida utente e diagnostica errori | knowledge base/read-only | L0 |
| Supervisore | orchestra agenti, budget e approvazioni | nessun tool di dominio diretto | livello del workflow |

Il Supervisore non eredita automaticamente tutti i permessi: può solo delegare a tool che il ruolo
dell'utente e la policy del workflow autorizzano.

---

## 9. Workflow finali da rendere quasi autonomi

### 9.1 Onboarding di una nuova azienda

1. Documento o delega arriva nell'Inbox.
2. Antivirus, hash e deduplica.
3. Parser + AI estraggono dati con provenienza per pagina e coordinate.
4. Il sistema cerca P.IVA/CF nel tenant e blocca duplicati.
5. L'agente Azienda 360 mostra dati presenti, mancanti e incoerenti.
6. L'utente conferma; solo allora viene creata l'anagrafica.
7. Si generano checklist per INPS, INAIL, SdI, regime, CCNL e deleghe.
8. Ogni passo apre un task con responsabile e scadenza deterministica.
9. Le credenziali esterne vengono collegate dal titolare, mai lette dall'AI.
10. Il dossier finale contiene documenti, basi/informative applicabili, deleghe, fonti e audit.

**Gate:** nessuna azienda pronta per le paghe finché mancano i dati minimi del perimetro scelto.

### 9.2 Onboarding di un dipendente

1. UNILAV XML/PDF, contratto e anagrafica vengono caricati.
2. L'AI propone i campi senza salvare.
3. CF, date, azienda, CCNL, livello, orario e tipo rapporto vengono validati.
4. Il sistema cerca la persona nel tenant e propone riuso o nuovo rapporto.
5. Dati sensibili vengono minimizzati e cifrati.
6. Il professionista conferma inquadramento e decorrenze.
7. Si genera la checklist delle informazioni mancanti per il primo cedolino.
8. Il dipendente riceve accesso al portale solo dopo verifica dell'identità.

**Gate:** nessun rapporto attivo senza azienda, data assunzione, inquadramento approvato e prova
della provenienza dei dati.

### 9.3 Chiusura paghe mensile

```text
Apertura periodo
→ raccolta variabili
→ sollecito automatico dati mancanti
→ cartellino giornaliero e quadratura
→ pre-calcolo deterministico
→ controllo anomalie multiperiodo
→ correzioni proposte
→ calcolo definitivo e snapshot
→ approvazione professionale
→ cedolino/LUL/F24/UniEmens/contabilità
→ controlli ufficiali
→ seconda approvazione per invii/pagamenti
→ pubblicazione e trasmissione
→ ricevute, riconciliazione e conservazione
```

Ogni azienda/periodo ha una macchina a stati. Un errore su un dipendente non deve cancellare il
lavoro già validato sugli altri; deve creare un'eccezione visibile. Il sistema deve spiegare la
differenza rispetto al mese precedente, non solo mostrare un semaforo.

**Gate:** zero differenze inspiegate rispetto al gold set o al software del professionista nel
parallel run.

### 9.4 Fattura passiva e chiusura contabile

1. XML/PDF/ricevuta arrivano da provider, upload, email o portale.
2. Deduplica per hash, identificativo SdI, fornitore, numero e data.
3. Parser deterministico legge XML; AI interviene su allegati non strutturati.
4. Viene proposto conto, partitario, competenza e trattamento IVA.
5. Regole fiscali bloccano proposte incompatibili.
6. Sopra soglia e dopo periodo di apprendimento, il matching può passare a L2.
7. Movimenti bancari vengono abbinati con prova e confidenza.
8. Le eccezioni arrivano al commercialista; il resto viene riconciliato.
9. La chiusura produce quadrature, documenti mancanti e dossier di controllo.

**Gate:** ogni registrazione automatica è riconciliabile al documento e reversibile.

### 9.5 Adempimento telematico

1. Dati provengono esclusivamente da snapshot approvati.
2. Il writer genera il file della versione corretta.
3. Validazioni interne e schema vengono eseguiti.
4. Il wrapper avvia il software di controllo ufficiale corrente.
5. L'AI traduce il diagnostico in linguaggio semplice, ma non può ignorarlo.
6. La correzione crea una nuova versione, non sovrascrive la precedente.
7. Con esito positivo, utente autorizzato approva con MFA.
8. Il canale ufficiale/provider invia in modo idempotente.
9. Il sistema acquisisce la ricevuta, che determina lo stato reale.
10. File, hash, diagnostico, protocollo, ricevuta e rettifiche vanno nel fascicolo.

**Gate:** il protocollo di upload non è successo; solo la ricevuta prevista chiude la pratica.

### 9.6 Recupero automatico dei dati mancanti

1. Gli eventi di dominio aprono richieste strutturate.
2. L'agente sceglie un template già approvato e il canale consentito.
3. Il messaggio contiene soltanto i dati necessari.
4. Le risposte vengono classificate e collegate alla richiesta.
5. Allegati e campi vengono proposti nell'Inbox.
6. Reminder ed escalation seguono SLA configurati.
7. PEC, contestazioni e comunicazioni formali restano L1/L4.

**Gate:** destinatario, base giuridica/preferenza, template e audit sempre presenti.

---

## 10. Roadmap completa a gate

Le stime sono fasce **P50** assumendo un flusso equivalente a un ingegnere senior focalizzato,
agenti AI e disponibilità rapida dei revisori; per pianificazione prudente usare **P80 = 1,5-2×**.
La capacità effettiva viene ricalcolata dopo quattro settimane usando throughput reale. Non sono
promesse di calendario. Le fasi esterne dipendono da professionisti, enti e provider. Le fasi
possono sovrapporsi solo se le dipendenze indicate sono rispettate.

Critical path del vertical slice:

```text
F-1 → F0/F0B → F1 → F2/F3 → F4 → F5
                                  ├→ F7A → F8A/F8M → F9 → rolling shadow
                F2/F4 → F6A ─────┴──────────────────→ F10 → invio nel pilot
```

### Gate F-1 — Stabilizzare il lavoro concorrente

**Obiettivo:** creare una baseline attribuibile prima di qualsiasi mutazione del master plan.

- [x] 👤 Identificare owner e scopo di tutte le modifiche tracciate e dei nuovi file presenti.
- [x] 👤 Decidere per ogni lavoro: completare, mettere in pausa su branch/worktree o accantonare
  esplicitamente; nessuna cancellazione implicita.
- [x] 🤖 Evitare merge manuali negli hotspot finché gli owner non hanno concluso.
- [x] 🤖 Ottenere un commit/tag baseline pulito con build, test e frontend verificati.
- [ ] 🤖 Creare branch/worktree separato per ogni epic e ownership esclusiva temporanea di
  `Program.cs`, `PagheDbContext.cs`, `Dtos.cs`, `api.ts` e altri hotspot.
  *(da applicare all'apertura della prossima epic: nessuna epic aperta al momento del tag)*
- [x] 🤖 Registrare hash commit, schema DB, toolchain e risultati della baseline.

**Gate F-1: SUPERATO il 15/07/2026** — commit `fcf9e8d`, tag `baseline-f1-2026-07-15`,
487/487 test verdi, attribuzione completa in `docs/baseline/BASELINE-F1-2026-07-15.md`.
Le fasi successive restano non autorizzate finché il fondatore non le richiede.

### Fase 0 — Truth baseline e blocco del perimetro

**Durata indicativa:** 3-5 giorni AI + 1 riunione col fondatore.

**Obiettivo:** una sola verità sullo stato e un primo segmento vendibile.

- [ ] 🤖 Generare inventario di moduli, endpoint, pagine, entità, test e configurazioni.
- [ ] 🤖 Creare `capabilities.yaml` (nome indicativo) come **unico registro canonico
  machine-readable**, con assi di stato, evidenze, owner, dipendenze e gate.
- [ ] 🤖 Generare dal registro le viste Markdown per fondatore e agenti; non aggiornare manualmente
  le stesse informazioni in più documenti.
- [ ] 🤖 Popolare per ogni funzione i sei assi di §1.1 e derivare tramite policy le viste e lo
  stato commerciale; non memorizzare una seconda scala aggregata lineare.
- [ ] 🤖 Collegare ogni affermazione «fatto» a file, test e prova.
- [ ] 🤖 Marcare i piani storici come `superseded` dopo adozione esplicita, senza cancellarli.
- [ ] 🤖 Aggiornare `AGENTS.md` per puntare al registro canonico e alla vista operativa generata;
  finché questo non avviene, restano valide le istruzioni correnti di `PIANO.md`.
- [ ] 🤖 Allineare numeri di test e data dell'ultima verifica.
- [ ] 👤 Scegliere il cliente ideale iniziale.
- [ ] 👤 Scegliere il solo CCNL e le casistiche incluse nel primo pilota.
- [ ] ⚖️ Nominare consulente del lavoro e commercialista revisori.
- [ ] 👤 Decidere se PaghePro opera come software per il professionista, centro elaborazione dati
  assistito o strumento interno del datore; farlo verificare legalmente.
- [ ] ⚖️ Tradurre il modello scelto in policy server-side: chi può curare adempimenti propri, chi
  può agire per terzi e quali atti richiedono un professionista abilitato.
- [ ] ⚖️ Vietare che un mandato generico o un'approvazione tecnica sostituiscano il giudizio
  professionale richiesto; rendere obbligatoria l'informativa AI prevista dalla legge 132/2025.
- [ ] 🤖 Creare matrice `prepara / controlla / approva / firma / invia / conserva`.
- [ ] 🤖 Creare registro dei blocchi esterni e relative date di avvio.
- [ ] 🤖 Versionare nel registro canonico soglie di confronto, severità incidente, finestra di
  osservazione ed evidenze richieste per ogni gate del pilota.

#### Contratto di misurazione dei gate

Le soglie non saranno un numero generico applicato a tutto. F0 crea un registro versionato per
`artefatto + campo + periodo normativo + software di confronto`, contenente unità, normalizzazione,
regola di arrotondamento, tolleranza assoluta/relativa, motivazione, owner professionale e data di
approvazione. Valgono queste regole minime:

- identificativi, tenant, destinatari, periodi, stati, IBAN, codici fiscali, matricole e riferimenti
  di protocollo devono coincidere esattamente dopo una normalizzazione dichiarata;
- importi monetari, imponibili, contributi, ritenute, netti e quadrature devono coincidere al
  centesimo quando input, regole e arrotondamenti sono gli stessi;
- una tolleranza diversa da zero è ammessa soltanto per uno specifico output quando sistemi con
  regole o arrotondamenti diversi la rendono inevitabile, con approvazione professionale preventiva;
- differenze solo grafiche vengono escluse esclusivamente dopo confronto del modello dati canonico,
  non osservando a occhio PDF diversi;
- una differenza oltre soglia è `spiegata` soltanto se possiede causa, prova, decisione nominativa
  del professionista e test di regressione; una semplice nota dell'AI non è una spiegazione valida;
- ogni report espone numeratore, denominatore, campione, aziende/periodi coperti, valori esclusi e
  motivo dell'esclusione.

La severità degli incidenti usa un vocabolario distinto dalle priorità di backlog:

| Severità | Definizione minima | Effetto sul gate |
|---|---|---|
| `INC-P0` critica | fuga cross-tenant; takeover amministrativo; pagamento/invio esterno non autorizzato; perdita o corruzione non recuperabile; compromissione sistemica di segreti | blocco immediato, rollback/containment e azzeramento della finestra |
| `INC-P1` alta | esposizione significativa confinata; documento o dato errato rilasciato con impatto reale; controllo critico aggirato; indisponibilità oltre SLO senza alternativa sicura | blocco, remediation verificata e azzeramento della finestra |
| `INC-P2` media | errore contenuto prima dell'effetto esterno, degradazione locale o controllo secondario fallito senza violazione dati | chiusura, retest e accettazione del risk owner prima del gate |
| `INC-P3` bassa | difetto cosmetico o operativo senza impatto su dati, calcoli, permessi, invii o scadenze | tracciamento; blocca se ricorrente oltre la soglia concordata |

La finestra F9 è di almeno **due cicli mensili consecutivi completi per ogni azienda inclusa**. La
finestra F17 è di almeno **tre cicli mensili consecutivi per ogni azienda e una scadenza
significativa in-scope**. Un `INC-P0`, `INC-P1` o una differenza oltre soglia non spiegata fa
ripartire la finestra dopo la correzione verificata. Report diff firmati, registro incidenti,
ricevute, audit, risultati dei validatori e tempi umani sono le evidenze minime.

**Deliverable:** registro canonico + viste generate, perimetro, risk register e backlog ordinato.

**Gate F0:** nessuna contraddizione materiale; ogni funzione ha stato e prova; perimetro firmato.

### Fase 0B — Product discovery e prova del problema

**Durata indicativa:** 2-4 settimane, in parallelo con F1-F3 ma prima di investire in F4-F9.

**Obiettivo:** scegliere un solo buyer iniziale e dimostrare che pagherà per cambiare processo.

- [ ] 👤 Scegliere il buyer primario: studio/Consulente del Lavoro **oppure** micro-impresa;
  l'altro rimane utente secondario, non secondo ICP iniziale.
- [ ] 👤 Condurre 10-15 interviste strutturate su processo corrente, errori, tempi e costi.
- [ ] 👤 Identificare software attuale, alternative, vincoli contrattuali e costo di switching.
- [ ] 👤 Reclutare 2-3 design partner coerenti con lo stesso perimetro.
- [ ] 👤 Ottenere almeno una LOI o impegno equivalente condizionato a criteri espliciti.
- [ ] 👤 Testare pacchetto, prezzo, unità di fatturazione e disponibilità a pagare.
- [ ] 🤖 Misurare una baseline time-and-motion su onboarding e ciclo paghe.
- [ ] 👤 Definire criteri di abbandono/pivot: nessuna LOI, dati indisponibili, costo migrazione o
  margine incompatibile.
- [ ] 🤖 Decidere se il primo prodotto è un **layer operativo sopra il gestionale esistente**
  invece di una sostituzione completa; questa opzione riduce rischio e costo di migrazione.

**Gate F0B:** buyer primario unico, 2-3 design partner, almeno una LOI, baseline misurata, prezzo
testato e decisione go/no-go. Senza gate, F4-F9 restano ricerca tecnica e non roadmap commerciale.

### Fase 1 — Correzioni di sicurezza P0

**Durata indicativa:** 1-2 settimane AI; prima di dati o account reali.

**Obiettivo:** eliminare i rischi che rendono pericoloso anche un semplice pilota.

- [x] 🤖 Rimuovere l'assegnazione super-admin basata sulla sola corrispondenza con un'email
  configurata e registrabile pubblicamente.
- [x] 🤖 Creare bootstrap amministratore one-time con token segreto e disabilitazione (uso singolo:
  si chiude quando esiste già un super-admin). *Scadenza temporale del token: non implementata,
  valutata non necessaria dato l'uso singolo — rivedibile.*
- [ ] 👤 Assicurare che `ASPNETCORE_ENVIRONMENT=Production` sia effettivo in deploy. *(config di
  deploy, a carico del fondatore)*
- [x] 🤖 Rendere obbligatoria una chiave dati forte in produzione; fail-fast se assente.
  *(già presente: `TokenService.ChiaveJwt` e `ProtettoreDati`, verificato)*
- [x] 🤖 Impedire l'uso della passphrase di sviluppo in qualsiasi deploy pubblico. *(già presente)*
- [x] 🤖 Cifrare `ConfigurazioneSdi.ApiKey`. *(cifratura a riposo; envelope/rotazione KMS → F2)*
- [x] 🤖 Cifrare/minimizzare dati particolari. *(nessuna diagnosi memorizzata: minimizzazione già
  soddisfatta; i contatori giorni restano in chiaro perché servono al calcolo e sono già sul
  cedolino — vedi `docs/COSE-NON-FATTE-PER-SICUREZZA.md` §5)*
- [x] 🤖 Proteggere endpoint stateless di calcolo/firma (spostati sul gruppo autenticato).
- [x] 🤖 Vietare upload di PFX/PIN su endpoint pubblici; sessione autorizzata e memoria effimera,
  senza log.
- [x] 🤖 Rate limit per operazioni sensibili (policy `sensibili` su firma). *Limiti dedicati per
  upload/AI: parzialmente coperti (contatore consumi AI per tenant già presente).*
- [x] 🤖 Test automatici per escalation ruolo, bootstrap e comportamento sicuro.
  *Endpoint anonimi via `WebApplicationFactory`: rimandati a F2 (test API integrati).*
- [ ] 🤖 Ruotare ogni segreto di test eventualmente riusato fuori dallo sviluppo. *(nessun segreto
  di produzione nel repo; da verificare all'atto del deploy)*

**Fase 1: SOSTANZIALMENTE COMPLETA il 15/07/2026** — ramo `epic/f1-sicurezza-p0`, commit
`c4ddf61`, 490/490 test verdi. Dettaglio in `docs/baseline/FASE1-SICUREZZA-2026-07-15.md`.
Voci `[ ]` rimaste: config di deploy (fondatore) e test API anonimi (spostati a F2 con
`WebApplicationFactory`).

**Gate F1:** nessun admin ottenibile via registrazione; nessun segreto o dato particolare in chiaro;
produzione non parte con chiavi note/deboli; security test verdi.

### Fase 2 — Fondamenta di produzione

**Durata indicativa:** 3-6 settimane AI + scelta hosting.

**Dipendenza:** F1.

#### Database e tenant

- [ ] 🤖 Congelare una versione dello schema.
- [ ] 🤖 Generare migrazione iniziale e percorso di upgrade da una copia realistica del DB.
- [ ] 🤖 In produzione usare `Database.Migrate`, mai `EnsureCreated` o patch generiche.
- [ ] 🤖 Passare il target multiutente a PostgreSQL.
- [ ] 🤖 Approvare un ADR/prototipo RLS prima della migrazione definitiva.
- [ ] 🤖 Rendere obbligatorie due difese complementari: RLS PostgreSQL **e** filtro applicativo
  centralizzato; il filtro applicativo da solo non è accettabile.
- [ ] 🤖 Usare transazione/unit-of-work obbligatoria, `SET LOCAL` del tenant, interceptor e
  `FORCE ROW LEVEL SECURITY`.
- [ ] 🤖 Ruolo database applicativo senza `BYPASSRLS`; ruoli amministrativi e worker separati,
  con policy esplicite per tabelle globali e operazioni cross-tenant autorizzate.
- [ ] 🤖 Testare connection pooling Npgsql, transazione assente/fallita, raw SQL, export, worker,
  job pianificati e riuso della connessione fra tenant.
- [ ] 🤖 Applicare `tenantId` e vincoli a ogni nuova tabella, indice e relazione.
- [ ] 🤖 Test cross-tenant via API per ogni famiglia di risorse.
- [ ] 🤖 Test di migrazione fresh install, upgrade e rollback/forward-fix.

#### Storage e code

- [ ] 🤖 Object storage S3-compatibile cifrato.
- [ ] 🤖 In F2 predisporre solo blob store, coda, antivirus e primitive di cifratura; il modello
  dati documentale definitivo appartiene a F7A, evitando uno schema provvisorio e backfill doppi.
- [ ] 🤖 Antivirus/quarantena e verifica del contenuto reale.
- [ ] 🤖 Worker separato, coda persistente, retry e dead-letter queue.
- [ ] 🤖 Transactional outbox e inbox per webhook.

#### CI/CD e affidabilità

- [ ] 🤖 Pipeline: tutte le suite obbligatorie verdi, frontend build e lint senza nuovi warning;
  non cablare nel gate un numero di test destinato a cambiare.
- [ ] 🤖 Test API con `WebApplicationFactory` e database reale/containerizzato.
- [ ] 🤖 E2E browser sui flussi critici.
- [ ] 🤖 SAST, dependency scan, secret scan, licenze, SBOM e container scan.
- [ ] 🤖 Ambienti dev/test/staging/prod separati.
- [ ] 🤖 Deploy blue-green/canary e rollback documentato.
- [ ] 🤖 Backup cifrati fuori server e restore drill automatizzato.
- [ ] 🤖 SLO, metriche, trace, alert e runbook incidenti.
- [ ] 🤖 RPO/RTO misurati, non solo dichiarati.

#### Decomposizione degli hotspot

- [ ] 🤖 ADR su progetti `Ai`/`Worker`, dipendenze ammesse e composition root.
- [ ] 🤖 Decomporre inizialmente **solo** endpoint, DTO, configurazioni EF e client toccati dal
  vertical slice; il resto viene estratto opportunisticamente dalla fase che ne diventa owner.
- [ ] 🤖 Usare PR meccaniche piccole con budget di diff concordato, senza riscrittura big-bang.
- [ ] 🤖 Nelle aree in-scope, mantenere `PagheDbContext` come registro e `api.ts` come facade/indice
  sottile; non è gate F2 assottigliare l'intero repository.
- [ ] 🤖 Test di caratterizzazione prima di ogni estrazione.
- [ ] 🤖 Ownership esclusiva degli hotspot durante il cablaggio.

**Gate F2:** migrazione e restore riusciti; prototipo RLS+filtro superato su pooling/raw SQL/worker;
CI obbligatoria; staging per almeno 7 giorni senza errore P0/P1; RPO obiettivo ≤24 ore e RTO
obiettivo ≤4 ore per il pilota, entrambi misurati; nessun dato reale prima del superamento.

### Fase 3 — GDPR, governance AI e documentazione legale

**Durata indicativa:** 2-4 settimane in parallelo con F2; richiede ⚖️.

- [ ] ⚖️ Mappare titolare, responsabile, sub-responsabili e finalità.
- [ ] ⚖️ Per ogni trattamento, approvare una matrice con finalità, ruolo, base art. 6 GDPR,
  condizione art. 9 o art. 10 quando applicabile, categorie dati/interessati, destinatari,
  trasferimenti, retention, informative artt. 13/14 e registro art. 30.
- [ ] ⚖️ Non usare il consenso del lavoratore come base predefinita: ammetterlo solo per finalità
  realmente facoltative, separabili e revocabili senza conseguenze.
- [ ] ⚖️ Redigere DPIA per dati dei lavoratori e funzioni AI.
- [ ] ⚖️ DPA art. 28 con provider cloud/AI/storage/notifiche.
- [ ] ⚖️ Valutare trasferimenti extra SEE e misure supplementari.
- [ ] 🤖 Scheda obbligatoria per ogni funzione AI: intended purpose, ruolo PaghePro
  provider/deployer/distributore, utenti, dati, soggetti impattati, modello, tool e rischio.
- [ ] ⚖️ Documentare classificazione AI Act e motivazione dell'eventuale eccezione art. 6(3),
  influenza materiale, profiling, registrazione e obblighi applicabili.
- [ ] ⚖️ Se una funzione è high-risk, richiedere prima dell'abilitazione: QMS, risk management,
  data governance, documentazione tecnica, logging, supervisione umana sostanziale,
  accuratezza/robustezza/cybersecurity, conformità, post-market monitoring e incident reporting.
- [ ] 🤖 Se classificazione o obblighi non sono chiusi, capability flag `spento` e stato `bloccato`.
- [ ] 🤖 Vietare esplicitamente emozioni, scoring HR, monitoraggio occulto e profiling.
- [ ] 🤖 Gate art. 50 per ciascun output: interazione con AI, marcatura/rilevabilità e disclosure
  del contenuto quando applicabili; non basta un badge globale.
- [ ] 🤖 Aggiungere informativa semplice sull'uso dell'AI nei servizi professionali.
- [ ] 👤 Programma di alfabetizzazione AI per fondatore, operatori e revisori.
- [ ] ⚖️ Valutazione preventiva di ogni uso nel lavoro ai sensi degli artt. 11 e 13 della legge
  132/2025, D.Lgs. 152/1997 art. 1-bis e Statuto dei lavoratori art. 4; informative a lavoratori e,
  quando richiesto, rappresentanze/autorizzazioni.
- [ ] ⚖️ Test antidiscriminazione e prova che la supervisione umana sia sostanziale, non un clic
  automatico; anomalie paghe non diventano valutazioni del lavoratore.
- [ ] 🤖 Registro accessi in lettura per risorse sensibili, non solo modifiche EF.
- [ ] 🤖 Audit append-only/tamper-evident e verifica periodica dell'integrità.
- [ ] 🤖 Retention distinta per archivio operativo, conservazione a norma, audit/log AI, backup e
  legal hold, con categoria, finalità, dies a quo, termine e responsabile.
- [ ] 🤖 Payload audit minimizzati/pseudonimi, cifratura e crypto-shredding dove compatibile;
  procedura di cancellazione dai backup e sospensione per contenzioso.
- [ ] 🤖 Export dati, rettifica, limitazione e cancellazione/anonimizzazione verificati.
- [ ] 🤖 Procedura data breach e incidente AI con simulazione tabletop.
- [ ] 🤖 Portale elenco provider/subprocessor e versioni di policy.

**Urgenza normativa:** alfabetizzazione AI già applicabile; obblighi art. 50 dal 2 agosto 2026
secondo il caso d'uso. Mantenere un registro delle date effettive basato sulla Gazzetta UE/EUR-Lex:
il calendario high-risk è in transizione nel luglio 2026 e nessun rinvio va applicato prima della
pubblicazione ed entrata in vigore dell'atto definitivo.

**Gate F3:** matrice basi giuridiche e registro art. 30 approvati, DPIA/DPA, schede AI Act per
capability, art. 50, informative lavoro, retention e diritti testati. Nessun dato reale o funzione
AI attiva se un elemento applicabile manca.

### Fase 4 — Motore normativo e snapshot riproducibile

**Durata indicativa:** 4-8 settimane per il primo perimetro; manutenzione permanente.

**Dipendenze:** F0 e F2; coinvolgimento ⚖️ continuo.

- [ ] 🤖 Cercare tutte le costanti normative rimaste nel codice.
- [ ] 🤖 Distinguere costanti tecniche da aliquote, soglie, divisori e regole temporali.
- [ ] 🤖 Modello generale: fonte, hash, validità dal/al, ambito, priorità, stato, revisore.
- [ ] 🤖 Import ufficiali idempotenti e diff tra versioni.
- [ ] 🤖 Workflow proposta → revisione → approvazione → pubblicazione → ritiro.
- [ ] 🤖 Test golden per ogni regola e anno supportati.
- [ ] 🤖 Snapshot immutabile di anagrafica rilevante, cartellino, parametri, regole, voci e output.
- [ ] 🤖 Hash dello snapshot e collegamento a documento/PDF/file telematico.
- [ ] 🤖 Ristampa storica solo dallo snapshot, mai dai parametri correnti.
- [ ] 🤖 Motore di spiegazione deterministico: formula, valori, arrotondamenti e fonte.
- [ ] 🤖 Blocco di calcolo se manca la regola approvata del periodo.
- [ ] 🤖 Monitor fonti ufficiali con revisione umana prima di attivare aggiornamenti.

**Gate F4:** l'artefatto originale viene conservato e restituito bit-for-bit con il proprio hash;
una eventuale rigenerazione supera confronto canonico/semantico di dati, lineage e resa visuale con
renderer/versione registrati. Nessuna costante normativa fuori allowlist; ogni risultato spiega
fonte e versione.

### Fase 5 — Cedolino professionale per la nicchia scelta

**Durata indicativa:** 2-4 mesi AI + validazione professionale, incrementale.

**Dipendenza:** F4.

- [ ] 🤖 Cartellino giornaliero: ore teoriche/effettive, causali, intervalli e sovrapposizioni.
- [ ] 🤖 Quadratura giorno, settimana, mese e orario contrattuale.
- [ ] 🤖 PUC/eventi necessari senza esporre diagnosi.
- [ ] 🤖 Stati `Bozza → Controllata → Validata → Chiusa → Rettificata`.
- [ ] 🤖 Lock e concorrenza per evitare ricalcoli paralleli.
- [ ] 🤖 Ferie/ROL/ex-festività con saldi storici e regole CCNL.
- [ ] 🤖 Malattia/infortunio per i casi inclusi, con carenza, indennità e integrazione.
- [ ] 🤖 Maternità/congedi soltanto se inclusi nel perimetro e validati.
- [ ] 🤖 Mensilità aggiuntive realmente erogate, non solo ratei.
- [ ] 🤖 Cessazione completa: competenze residue, TFR e tassazione separata.
- [ ] 🤖 Conguaglio, addizionali, detrazioni e trattamento integrativo per anno.
- [ ] 🤖 Tipi rapporto inclusi: indeterminato; poi determinato, part-time e apprendistato.
- [ ] 🤖 Codice CNEL e versione del CCNL in snapshot.
- [ ] 🤖 PDF cedolino e LUL derivati dallo snapshot.
- [ ] ⚖️ Golden cases forniti/approvati dal Consulente del Lavoro.
- [ ] 🤖 Confronto voce per voce, imponibili, netto, costo, ratei e contabilità.
- [ ] 🤖 Test property-based su arrotondamenti, confini e invarianti.

**Gate F5:** tutti i casi in-scope riconciliati; firma di validazione professionale; fuori perimetro
bloccato con messaggio chiaro.

### Fase 6A — Infrastruttura Official Controls Factory

**Durata indicativa:** 3-6 settimane per il runner condiviso; non possiede writer di dominio.

**Dipendenze:** F2 e F4 per infrastruttura, fonti e audit. Non dipende da F5; snapshot F5 e output
F9 sono prerequisiti dei soli adapter paghe di F10.

- [ ] 🤖 Interfaccia comune per validatore XSD, CLI, JAR o servizio ufficiale.
- [ ] 🤖 Registry per ente, adempimento, anno, versione e checksum del kit.
- [ ] 🤖 Esecuzione isolata con timeout, limiti risorse e output catturato.
- [ ] 🤖 Parser dei diagnostici in formato strutturato.
- [ ] 🧠 Spiegazione semplice collegata al campo e alla fonte, senza sopprimere l'originale.
- [ ] 🤖 Archiviazione hash input/output, versione validatore e risultato.
- [ ] 🤖 Casi positivi, negativi e warning forzabili.
- [ ] 🤖 Policy: nessun file esportabile come trasmissibile senza esito richiesto.
- [ ] 🤖 Contratto adapter con ownership separata: writer/modello restano nel dominio verticale.

Ownership unica:

| Adapter | Owner esclusivo | Dipendenza |
|---|---|---|
| F24, UniEmens, INAIL | F10 | F6A runner |
| CU, LIPE, 770, IVA e dichiarativi fiscali | F13 | F6A runner |
| AA7/AA9/ComUnica | epic separata dopo F13 | F6A + canale esterno |

**Gate F6A:** runner isolato e versionato, fixture fittizia positiva/negativa, diagnostico/hash
archiviato e API adapter stabile. Il superamento non dichiara pronto alcun adempimento reale.

---

### Fase 7A — AI Document Platform e Inbox del vertical slice

**Durata indicativa:** 4-8 settimane AI.

**Dipendenze:** F2 e F3. Può iniziare dopo i gate sicurezza, non prima.

#### Backend e storage

- [ ] 🤖 Entità `Documento`, `VersioneDocumento`, `Estrazione`, `CampoEstratto`, `Proposta`.
- [ ] 🤖 Provenienza campo: parser/AI/utente, pagina, area, testo sorgente, confidenza.
- [ ] 🤖 Stato: caricato, quarantena, classificato, da rivedere, confermato, scartato, conservato.
- [ ] 🤖 Upload a flusso, limiti, antivirus, hash, deduplica e object storage.
- [ ] 🤖 OCR asincrono e sandbox; nessuna lettura integrale in memoria per file grandi.
- [ ] 🤖 Schemi versionati per tipo documento.
- [ ] 🤖 Classificatore documento/azienda/persona/periodo con fallback manuale.
- [ ] 🤖 Redazione PII prima del cloud quando il compito non richiede il dato completo.
- [ ] 🤖 Audit AI con modello, prompt, costo, latenza, output e decisione dell'utente.

#### Frontend

- [ ] 🤖 Inbox con drag-and-drop e sorgenti esterne.
- [ ] 🤖 Vista documento a sinistra e campi a destra.
- [ ] 🤖 Evidenza visiva di provenienza e confidenza.
- [ ] 🤖 Navigazione al punto esatto del documento.
- [ ] 🤖 Filtri per tenant, azienda, tipo, stato, rischio e assegnatario.
- [ ] 🤖 Azioni conferma/correggi/scarta/richiedi dato.
- [ ] 🤖 Coda eccezioni, bulk review solo per elementi a rischio basso.

#### Tipi documentali del slice

1. [ ] UNILAV in modalità proposta prima del salvataggio;
2. [ ] visura camerale, migrando il prototipo esistente;

**Gate F7A:** benchmark UNILAV/visura; parser prevale; timeout provider non blocca; zero salvataggi senza
conferma; prompt injection e tenant tests verdi.

### Fase 7B — Espansione Inbox per dominio

**Dipendenze:** F7A e readiness/privacy del dominio ricevente. Non è prerequisito del primo shadow.

Ordine incrementale:

1. [ ] FatturaPA XML e fattura passiva PDF;
2. [ ] DURC;
3. [ ] F24 quietanzato;
4. [ ] estratto conto;
5. [ ] contratti e comunicazioni, soltanto dopo DPIA specifica;
6. [ ] giustificativi di assenza, con minimizzazione rafforzata.

Ogni tipo ha schema, corpus, gate e owner propri; non si attende il completamento dell'intera lista
per rilasciare il tipo precedente.

### Fase 8A — Azienda/Dipendente e portale minimo del vertical slice

**Durata indicativa:** 4-8 settimane AI.

**Dipendenza:** F7A per onboarding documentale, F5 per dati paghe affidabili.

- [ ] 🤖 Riepilogo azienda con dati obbligatori presenti/mancanti e azione collegata.
- [ ] 🤖 Riepilogo dipendente/rapporto in-scope e dati mancanti senza scoring.
- [ ] 🤖 Accesso dipendente limitato alla propria identità e ai documenti autorizzati.
- [ ] 🤖 Upload documenti e risposta alle richieste.
- [ ] 🤖 Raccolta presenze/variabili con validazione.
- [ ] 🤖 Download cedolini/CU e ricevute di lettura dove appropriate.
- [ ] 🤖 MVP recupero dati: template approvati, richieste strutturate, reminder, stato risposta
  ed escalation; il workflow completo e multi-canale resta in F16.
- [ ] 🤖 MVP radar: eventi del vertical slice trasformati in task da regole deterministiche;
  l'AI può soltanto spiegare e prioritizzare.
- [ ] 🤖 Accessibilità WCAG, mobile/PWA e lingua semplice.

**Gate F8A:** test E2E per i ruoli del slice; nessun dato di altro dipendente/azienda; raccolta,
download, reminder ed escalation minimi funzionanti.

### Fase 8B — Espansione 360 e self-service

**Dipendenze:** F8A. Non è prerequisito del primo rolling shadow.

- [ ] 🤖 Salute azienda anagrafica, fiscale, contributiva, documentale e operativa separata.
- [ ] 🤖 Timeline documenti, pratiche, invii, ricevute e override.
- [ ] 🤖 Deleghe/scadenze visibili senza segreti e ricerca globale tenant-scoped.
- [ ] 🤖 Storico rapporti/variazioni, ferie/ROL/TFR e documenti autorizzati.
- [ ] 🤖 Spiegazione del cedolino da lineage deterministico.
- [ ] 🧠 Assistente self-service read-only con passaggio all'operatore.
- [ ] 🤖 Ticket strutturati, SLA, stato e notifiche.
- [ ] 🤖 Verifica forte per cambio IBAN, contatto o dato sensibile.

**Gate F8B:** E2E completi per i ruoli estesi, spiegazioni riconciliabili e handoff con contesto.

### Fase 8M — Migration Kit e interoperabilità

**Durata indicativa:** 3-8 settimane per il primo gestionale/formato.

**Dipendenze:** F2, F4 e schema del vertical slice.

- [ ] 👤 Identificare il gestionale/formato realmente usato dai design partner.
- [ ] 🤖 Definire formati supportati e dati non importabili.
- [ ] 🤖 Area staging separata dai dati definitivi.
- [ ] 🤖 Import anagrafiche, aziende, rapporti, progressivi, saldi ferie/ROL/TFR e storico minimo.
- [ ] 🤖 Mapping codici CCNL, voci, causali, matricole e identificativi.
- [ ] 🤖 Report errori per riga/campo, correzione e ri-esecuzione idempotente.
- [ ] 🤖 Riconciliazione totali per azienda, dipendente, periodo e progressivo.
- [ ] 🤖 Firma del report di riconciliazione da parte del professionista.
- [ ] 🤖 Rollback completo dell'import e isolamento del batch.
- [ ] 🤖 Parallel run con riferimenti al record sorgente.
- [ ] 🤖 Export aperto dei dati PaghePro per ridurre il lock-in.

**Gate F8M:** import ripetibile, totali riconciliati, errori espliciti, rollback provato e firma del
professionista. Senza questo gate il costo di switching rende il pilota non rappresentativo.

### Fase 9 — Autopilota paghe e controllo anomalie

**Durata indicativa:** 4-8 settimane AI + almeno 2-3 cicli shadow.

**Dipendenze:** F5, F7A, F8A e F8M; F7B/F8B non sono prerequisiti. Per output trasmissibili serve
anche l'adapter ufficiale pronto.

- [ ] 🤖 Workflow per azienda/periodo con step, owner, scadenza e dipendenze.
- [ ] 🤖 Apertura automatica del periodo e checklist specifica.
- [ ] 🤖 Raccolta variabili da portale/documenti/API.
- [ ] 🧠 Identificazione dati mancanti e uso del recupero dati MVP di F8A.
- [ ] 🤖 Chiusura automatica delle richieste completate.
- [ ] ⚙️ Pre-calcolo e diagnostica deterministica.
- [ ] 🧠 Confronto mese precedente, anno precedente, contratto, presenze e storico.
- [ ] 🧠 Causa probabile, gravità, prova e correzione proposta.
- [ ] ⚙️ Ricalcolo, diff prima/dopo e nuovo snapshot.
- [ ] 🤖 Coda eccezioni ordinata per impatto e scadenza.
- [ ] 👤/⚖️ Approvazione nominativa della chiusura.
- [ ] ⚙️ Generazione coordinata di cedolino e LUL; F24/UniEmens soltanto come **bozze non
  trasmissibili** finché i relativi adapter F10 non superano i gate ufficiali.
- [ ] 🤖 Pubblicazione documenti soltanto dopo lo stato previsto.
- [ ] 🤖 Dashboard percentuale completamento studio e blocchi.

Metriche:

- tempo per azienda/dipendente;
- richieste dati evitate e reminder necessari;
- anomalie vere, falsi positivi e falsi negativi;
- override per regola/modello;
- differenze rispetto al software di confronto;
- percentuale completata senza intervento prima dell'approvazione.

**Gate F9:** almeno due cicli mensili consecutivi completi per ogni azienda in shadow; report sul
100% degli output; zero differenze oltre le soglie F0 rimaste inspiegate; nessun `INC-P0/P1` nella
finestra, tutti gli `INC-P2` chiusi, ritestati e accettati; nessuna propagazione silenziosa di input
errati. Un evento bloccante fa ripartire la finestra dopo la remediation verificata.

### Fase 10 — UniEmens, INAIL, F24 e pagamenti controllati

**Durata indicativa:** 1-3 mesi incrementali + account/deleghe.

**Dipendenze:** F6A e F9. F10 è l'unico owner di writer, modello e adapter F24/UniEmens/INAIL.

#### UniEmens

- [ ] 🤖 Adeguare modello alle specifiche correnti del periodo.
- [ ] 🤖 Mappare qualifica, tipo contribuzione, settimane, giorni, eventi e variabili.
- [ ] 🤖 Integrare Validatore INPS CLI corrente.
- [ ] 🤖 Gestire denunce individuali/aziendali, variazioni e regolarizzazioni in-scope.
- [ ] 🤖 Acquisire diagnostici e bloccare invio in caso di errore.
- [ ] ⚖️ Confronto con flussi reali del pilota.

#### INAIL

- [ ] 🤖 Importare basi di calcolo JSON ufficiali per PAT.
- [ ] 🤖 Modellare voci di tariffa, oscillazioni, retribuzioni, premio, rata e regolazione.
- [ ] 🤖 Autoliquidazione e dichiarazione salari per il perimetro scelto.
- [ ] ⚖️ Confronto con conteggi INAIL reali.

#### F24 e pagamenti

- [ ] 🤖 Superare limite singolo record V e tutti i casi del pilota.
- [ ] 🤖 Matricole/formati INPS reali, crediti, compensazioni e sezioni incluse.
- [ ] 🤖 Catena cedolino → debito → F24 → quietanza → riconciliazione.
- [ ] 🤖 Preparazione file SEPA/netti, se richiesta, con verifica IBAN.
- [ ] 👤 Doppio controllo e SCA gestiti dal partner bancario.
- [ ] 🤖 L'AI non vede né firma credenziali bancarie.

**Gate F10:** controllo ufficiale positivo, approvazione nominativa, ricevuta/quietanza acquisita e
riconciliazione completa.

### Fase 11 — Fatturazione elettronica reale e conservazione

**Durata indicativa:** 4-8 settimane AI + 2-8 settimane fornitore.

**Dipendenze:** F2, F3 e account 🔑.

- [ ] 👤 Aprire/attivare sandbox provider e definire contratto/DPA.
- [ ] 🤖 Test A-Cube o provider scelto con API reali, non solo adapter generico.
- [ ] 🤖 Segreti per tenant cifrati, versionati e ruotabili.
- [ ] 🤖 Webhook autenticato, replay-safe e inbox idempotente.
- [ ] 🤖 Gestione callback duplicate, ritardate e fuori ordine.
- [ ] 🤖 Reconciliation job provider↔PaghePro.
- [ ] 🤖 Ricezione fatture passive e allegati.
- [ ] 🤖 Note di credito/debito, reverse charge/autofatture, bollo, ritenute e casse nel perimetro.
- [ ] 👤 Decidere firma provider o firma locale.
- [ ] 🤖 Se locale: catena certificato, OCSP/CRL, scadenza, rinnovo e timestamp quando richiesto.
- [ ] 🤖 Se CNS: desktop agent isolato; PIN mai inviato al cloud o conservato.
- [ ] 👤 Scegliere conservatore e responsabile della conservazione.
- [ ] ⚖️ Manuale/piano di conservazione e ruoli.
- [ ] 🤖 Invio automatico in conservazione, ricevuta, ricerca, esibizione e pacchetto di uscita.

**Gate F11:** E2E sandbox invio→ricevuta→conservazione; idempotenza provata; segreti ruotati;
recupero di un documento conservato riuscito.

### Fase 12 — Contabilità operativa e riconciliazione bancaria

**Durata indicativa:** 2-4 mesi AI + provider open banking.

**Dipendenze:** F7A, gli extractor F7B necessari ai documenti contabili e F11. È prerequisito del
Tax Saving Engine.

- [ ] 🤖 Piano dei conti operativo, versioni e mapping per cliente.
- [ ] 🤖 Prima nota a partita doppia con quadratura obbligatoria.
- [ ] 🤖 Partitari clienti/fornitori e scadenzario incassi/pagamenti.
- [ ] 🤖 Registrazioni da fatture attive/passive e paghe.
- [ ] 🤖 Ratei, risconti, cespiti e ammortamenti nel perimetro.
- [ ] 🔑 Collegamento a un AISP/open banking autorizzato; nessuno scraping credenziali.
- [ ] 🤖 Import movimenti con idempotenza e saldo di controllo.
- [ ] 🧠 Proposta matching movimento↔fattura/F24/stipendio.
- [ ] ⚙️ Regole deterministiche per match esatti e quadrature.
- [ ] 🤖 Apprendimento soltanto dalle conferme del singolo tenant.
- [ ] 🤖 Chiusura periodo exception-first e dossier di controllo.
- [ ] ⚖️ Validazione commercialista su registrazioni e chiusura.

**Gate F12:** bilancio di verifica quadrato; movimenti riconciliati o motivatamente aperti; ogni
registrazione ha documento, regola, autore e undo.

### Fase 13 — Dichiarativi e chiusura annuale

**Durata indicativa:** 2-6 mesi per il perimetro, allineato alle versioni annuali.

**Dipendenze:** F6A, F10 e F12. F13 è l'unico owner di writer, modello e adapter CU/LIPE/770/IVA.

- [ ] 🤖 CU telematica completa e modello sintetico dell'anno corretto.
- [ ] 🤖 770 completo per i quadri scelti.
- [ ] 🤖 LIPE e dichiarazione IVA annuale da registri riconciliati.
- [ ] 🤖 Redditi/IRAP/IRES/IMU/Intrastat solo se inclusi e con fonte ufficiale.
- [ ] 🤖 XBRL e deposito bilancio soltanto dopo contabilità completa.
- [ ] 🤖 Orchestrazione fine anno con checklist, responsabili e scadenze.
- [ ] 🤖 Controlli ufficiali, firme, invii e ricevute per ogni artefatto.
- [ ] ⚖️ Approvazione professionale e gestione rettifiche.

**Gate F13:** nessun CSV/JSON interno presentato come file ufficiale; ogni modello annuale passa il
controllo della stessa annualità ed è riconciliato ai dati di origine.

### Fase 14 — Copilot operativo controllato

**Durata indicativa:** 4-8 settimane dopo che i tool sottostanti sono affidabili.

**Dipendenze:** F3, F4, F7A e tool dei moduli interessati.

- [ ] 🤖 Endpoint backend autenticato, streaming e trace.
- [ ] 🤖 Tool registry allowlist; niente SQL/HTTP/file generici.
- [ ] 🤖 Schema risposta: tipo azione, fonti, dati usati, rischi, conferma e prossimo passo.
- [ ] 🤖 Command palette e pannello laterale con contesto esplicito.
- [ ] 🤖 Citazioni a documento, regola e record autorizzato.
- [ ] 🤖 Conferme non falsificabili dal modello: token server-side a scadenza.
- [ ] 🤖 Separazione read-only, draft e propose-action.
- [ ] 🤖 Nessuna cancellazione, firma, pagamento o invio nella prima release.
- [ ] 🤖 Limiti costo, quota, timeout e modalità provider spento.
- [ ] 🤖 Evals su domande reali, astensione, fonti, tenant e tool abuse.

Ordine dei tool:

1. cerca e spiega dati già presenti;
2. trova campi mancanti;
3. spiega diagnostici e differenze;
4. crea checklist/task;
5. prepara messaggio;
6. prepara bozza documento/pratica;
7. propone modifica reversibile;
8. azione esterna solo in una fase successiva e con gate L4.

**Gate F14:** 100% tool call allowlisted e tenant-scoped; nessuna azione senza token di conferma;
risposte operative senza fonte pari a zero nel benchmark.

### Fase 15 — Tax Opportunity Engine e CFO assistito

**Durata indicativa:** 2-4 mesi dopo contabilità riconciliata.

**Dipendenze:** F4, F12 e prova di product-market fit del vertical slice in F17; commercialista
⚖️. È deliberatamente successiva al prodotto paghe vendibile e non fa parte del primo MVP.

- [ ] 🤖 Catalogo opportunità versionate con fonte, periodo, condizioni ed esclusioni.
- [ ] ⚙️ Individuazione e stima effettuate da regole deterministiche.
- [ ] 🧠 AI usata per spiegare, ordinare e chiedere documenti mancanti.
- [ ] 🤖 Ogni opportunità mostra importo stimato, prova, rischio e responsabile.
- [ ] 🤖 Stati suggerita / in verifica / confermata / scartata / realizzata.
- [ ] 🤖 Tracciamento di falsi positivi, valore confermato e motivi di scarto.
- [ ] 🤖 Simulazioni di cash flow fiscale con scenari, mai come certezza.
- [ ] 🤖 Dashboard CFO riconciliata a prima nota, banca, paghe e scadenze.
- [ ] ⚖️ Approvazione del commercialista prima di azioni o comunicazioni al cliente.

**Gate F15:** nessuna opportunità inventata dal modello; ogni importo riconciliabile; campione
professionale validato; linguaggio commerciale senza promessa di risparmio garantito.

### Fase 16 — Comunicazioni, scadenze e supporto autonomo limitato

**Durata indicativa:** 3-6 settimane AI.

**Dipendenze:** F2, F3 e F4; per portali/destinatari F8A/F8B; per canali reali relativo provider e
preferenze/basi approvate. L'MVP strettamente necessario all'autopilota è già in F8A; F16 lo
estende.

- [ ] 🤖 Eventi di dominio → scadenze da regole versionate.
- [ ] 🧠 Priorità e spiegazione AI, data normativa sempre deterministica.
- [ ] 🤖 Template per portale, email, SMS, WhatsApp e PEC separati.
- [ ] 🤖 Consenso/preferenza, quiet hours, opt-out e delivery status.
- [ ] 🧠 Interpretazione risposte e collegamento allegati.
- [ ] 🤖 Escalation a persona su ambiguità, contestazione o tono sensibile.
- [ ] 🤖 Knowledge base supporto con versione prodotto e runbook.
- [ ] 🧠 Diagnosi read-only; nessuna modifica segreta o distruttiva.
- [ ] 🤖 Metriche SLA, first-contact resolution e riaperture.

**Gate F16:** invii a destinatario errato pari a zero; PEC/formali con approvazione; task normativi
sempre originati da regola approvata.

### Fase 17 — Workstream continuo di pilota, shadow run e produzione limitata

**Durata minima:** 3 cicli mensili + una scadenza significativa; idealmente chiusura annuale.
Non attende il completamento delle Fasi 10-16: parte in rolling shadow appena il vertical slice
minimo supera i suoi prerequisiti e si amplia modulo per modulo.

**Vertical pilot slice iniziale:** un buyer primario, un CCNL, casi standard dichiarati, Migration
Kit, cartellino, snapshot, cedolino/LUL, Inbox UNILAV/visura minima, recupero dati MVP e bozze
F24/UniEmens. Un adempimento entra nel pilota trasmissibile solo dopo il proprio adapter F10 e il
controllo ufficiale; contabilità avanzata, banking, dichiarativi estesi e Tax/CFO restano fuori.

**Prerequisiti del slice:** F-1, F0/F0B, F1-F5, F7A, F8A, F8M e le parti F6A/F10 applicabili agli
output inclusi; F7B/F8B non sono prerequisiti. F2/F3 sono sempre obbligatorie per dati reali.

- [ ] 👤 Contratto pilota, DPA, perimetro e responsabilità.
- [ ] ⚖️ Dataset/golden cases e software di confronto.
- [ ] 🤖 Import iniziale e riconciliazione dati.
- [ ] 🤖 Eseguire PaghePro in parallelo senza sostituire il processo ufficiale.
- [ ] ⚖️ Confronto voce per voce e firma delle discrepanze risolte.
- [ ] 🤖 Incidenti, override, falsi positivi e tempi registrati.
- [ ] 🤖 Doppio controllo per tutte le azioni L4.
- [ ] 🤖 Test carico, sicurezza, restore e disaster recovery.
- [ ] ⚖️ Termini, privacy, informativa AI e licenze finali.
- [ ] 👤 Assicurazione RC/prodotto e processo di assistenza.
- [ ] 🤖 Go/no-go per singolo modulo e CCNL, non per il prodotto intero.

Gate commerciali progressivi:

- `problem-solution fit`: LOI + baseline + workflow accettato;
- `pilot fit`: tre cicli, accuratezza, tempo umano e switching cost misurati;
- `paid fit`: almeno due design partner disposti a pagare il pacchetto;
- `scale fit`: margine positivo dopo AI/provider/supporto e onboarding ripetibile.

**Gate F17:** almeno tre cicli mensili consecutivi per ogni azienda e una scadenza significativa
in-scope; report sul 100% degli output; zero differenze oltre le soglie F0 rimaste inspiegate; zero
`INC-P0/P1` nella finestra; tutti gli `INC-P2` chiusi, ritestati e accettati; trend `INC-P3` entro
la soglia concordata. Professionisti e pilota firmano il perimetro e i report diff; runbook,
ricevute, restore e coperture operative sono provati. Un evento bloccante fa ripartire la finestra.

### Fase 18 — Scala controllata

- [ ] Aggiungere un CCNL alla volta con corpus, regole, test e firma professionale.
- [ ] Aggiungere casistiche soltanto dopo priorità dei clienti reali.
- [ ] Distillare/ridurre costi AI dopo avere raggiunto la qualità con il modello più capace.
- [ ] Promuovere task da L1 a L2/L3 soltanto con dati di produzione e rollback.
- [ ] Valutare L5 solo per reminder/template a basso rischio.
- [ ] Programma mensile aggiornamenti normativi e trimestrale AI/security review.
- [ ] Audit annuale licenze, DPIA, fornitori, incidenti, accessi e business continuity.
- [ ] Espansione a studi più grandi solo dopo SLO e segregazione ruoli maturi.

---

## 11. Mappa delle schermate finali

### 11.1 Oggi

La home deve rispondere a quattro domande:

1. cosa scade;
2. cosa è bloccato;
3. cosa l'AI ha preparato;
4. cosa richiede la mia approvazione.

Componenti:

- KPI operativi, non vanity metrics;
- coda approvazioni;
- aziende/periodi bloccati;
- documenti da rivedere;
- scadenze con fonte e responsabile;
- incidenti/provider/validatori;
- costo e disponibilità AI;
- scorciatoie alla prossima azione reale.

### 11.2 Inbox AI

- caricamento e sorgenti;
- anteprima sicura;
- documento/campo side-by-side;
- provenienza e confidenza;
- deduplica/matching;
- azione proposta;
- assegnatario e SLA;
- audit della decisione.

### 11.3 Centro operazioni paghe

- matrice aziende × periodo × stato;
- percentuale raccolta dati;
- anomalie per gravità;
- confronto storico;
- snapshot e versioni;
- approvazione professionale;
- stato cedolino/LUL/F24/UniEmens/contabilità;
- ricevute e rettifiche.

### 11.4 Centro adempimenti

- file/versione annuale;
- validazioni interne;
- validatore ufficiale e versione;
- diagnostici originali + spiegazione;
- approvazione;
- invio/protocollo/ricevuta;
- conservazione;
- azione correttiva.

### 11.5 Centro AI e audit

- funzioni abilitate per tenant;
- provider/modello e local/cloud;
- limiti costo;
- log richieste e tool call;
- prompt/versione/eval;
- approvazioni e override;
- tasso errore, astensione e drift;
- kill switch;
- export audit per ispezione.

### 11.6 Impostazioni ruoli

Ruoli minimi:

- titolare studio;
- amministratore tecnico senza accesso automatico ai contenuti;
- consulente del lavoro;
- commercialista;
- operatore paghe;
- operatore contabile;
- revisore/approvatore;
- sola lettura;
- cliente azienda;
- dipendente;
- servizio/worker.

Ogni permesso distingue lettura, bozza, modifica, approvazione, invio, firma, export e accesso ai
dati particolari.

La policy distingue inoltre il **contesto dell'atto**:

- datore/titolare che cura i propri adempimenti;
- dipendente interno espressamente incaricato dal datore;
- professionista abilitato che opera per terzi;
- CED/operatore strumentale sotto assistenza professionale;
- amministratore tecnico, che non acquisisce per questo accesso ai contenuti o potere di firma.

Il server verifica contesto, delega e qualifica prima di mostrare o accettare un'azione; un ruolo
generico `approvatore` non è sufficiente per atti professionali riservati.

---

## 12. Modello dati pianificato

Nomi indicativi; la progettazione concreta avverrà nella relativa fase.

### 12.1 Documenti

- `DocumentoEntity`: tenant, proprietario logico, hash, MIME, dimensione, stato, retention.
- `VersioneDocumentoEntity`: storage key, hash, origine, timestamp, versione precedente.
- `EstrazioneDocumentoEntity`: schema, parser, modello, prompt, stato e metriche.
- `CampoEstrattoEntity`: nome, valore, provenienza, confidenza, pagina/coordinate, conferma.
- `CollegamentoDocumentoEntity`: azienda, persona, rapporto, pratica, periodo o fattura.

### 12.2 Agenti e workflow

- entità di stato specifiche (`PeriodoPagaWorkflow`, `Trasmissione...`, ecc.) nel relativo dominio;
- primitive condivise soltanto per esecuzione: job, tentativo, scadenza, errore e correlation id;
- `PropostaAzioneEntity`: rischio, diff, prove, stato e scadenza.
- `ApprovazioneEntity`: utente, ruolo, MFA, motivazione, hash contenuto approvato.
- `EsecuzioneAzioneEntity`: idempotency key, esito, compensazione, ricevuta.
- `OutboxEventEntity` e `InboxMessageEntity`.

Non creare `WorkflowDefinizioneEntity` o un DSL generico finché almeno due workflow reali non
dimostrano che l'astrazione riduce duplicazione senza nascondere le regole di dominio.

### 12.3 AI governance

- `ConfigurazioneModelloEntity`: provider, modello, capacità, regione e stato.
- `PromptVersionEntity`: template, hash, autore, approvazione e eval associato.
- `AiRunEntity`: tenant, utente, finalità, input hash, output, token, costo e latenza.
- `ToolCallEntity`: tool/versione, argomenti redatti, esito e risorse toccate.
- `EvalSuiteEntity`, `EvalRunEntity`, `EvalCasoEntity`.
- `BudgetAiTenantEntity` e `AiIncidentEntity`.

### 12.4 Normativa e lineage

- `FonteNormativaEntity`: ente, URL, titolo, data, hash e licenza.
- `RegolaNormativaEntity`: tipo, ambito, periodo, formula/dati e stato.
- `RevisioneRegolaEntity`: diff, revisore, test e motivazione.
- `SnapshotCalcoloEntity`: input, parametri, regole, versione software e hash.
- `LineageValoreEntity`: output → formula → input → documento/fonte.
- `ControlloUfficialeEntity`: kit/versione/checksum, diagnostico ed esito.

### 12.5 Sicurezza e privacy

- `AccessoSensibileEntity`: chi ha letto cosa, scopo e base/ruolo.
- `ConsensoPreferenzaEntity`: canale, finalità, versione informativa.
- `RetentionPolicyEntity` e `RetentionExecutionEntity`.
- `DataSubjectRequestEntity`.
- `KeyVersionEntity` senza materiale segreto.
- `SecurityIncidentEntity` e `DataBreachEntity`.

Tutte le tabelle persistenti devono avere `TenantId` quando contengono dati cliente; le tabelle
globali normative devono essere esplicitamente read-only per i tenant e amministrate separatamente.

### 12.6 Matrice retention da approvare legalmente

Valori iniziali di progetto, non parere legale definitivo:

| Categoria | Dies a quo | Baseline | Trattamento |
|---|---|---|---|
| LUL e documenti lavoro | chiusura/ultima registrazione | almeno 5 anni, salvo obblighi maggiori | conservazione separata dall'operativo |
| scritture/fatture/documenti fiscali | data documento/chiusura esercizio | almeno 10 anni e legal hold | conservazione a norma quando richiesta |
| ricevute/protocolli/rettifiche | chiusura pratica | allineata al documento principale | immutabile con hash |
| trace AI ordinari | data esecuzione | minimo necessario, breve e configurato | payload minimizzato/pseudonimo |
| log high-risk AI | data generazione | minimo legale applicabile, baseline ≥6 mesi | accesso ristretto e integrity check |
| audit sicurezza/accessi | data evento | risk-based e documentato | pseudonimo ove possibile |
| backup | data backup | finestra rotante distinta | scadenza automatica e restore controllato |
| contenzioso/legal hold | apertura hold | fino a chiusura autorizzata | sospensione selettiva della cancellazione |

La policy deve descrivere cancellazione/anonimizzazione nell'operativo, scadenza dei backup,
crypto-shredding, eccezioni per legal hold e prova dell'esecuzione. L'audit append-only non giustifica
la conservazione indefinita di prompt o documenti personali completi.

---

## 13. Contratti API pianificati

Pattern, non specifiche definitive:

```text
POST /api/documenti                         carica
GET  /api/documenti/{id}                    stato e metadati
GET  /api/documenti/{id}/contenuto          accesso autorizzato/temporaneo
POST /api/documenti/{id}/classifica         avvia job
GET  /api/documenti/{id}/estrazioni         versioni/provenienza
POST /api/documenti/{id}/conferma           salva dopo revisione

GET  /api/workflows                         coda operativa
GET  /api/workflows/{id}                    passi/evidenze
POST /api/workflows/{id}/riprendi            retry controllato
POST /api/proposte/{id}/approva              approvazione server-side
POST /api/proposte/{id}/rifiuta              motivo
POST /api/azioni/{id}/esegui                 usa token di approvazione

POST /api/ai/risposte                        copilot
GET  /api/ai/runs/{id}                       audit autorizzato
GET  /api/ai/capacita                        provider/modelli disponibili
POST /api/ai/kill-switch                     solo amministratore autorizzato

GET  /api/periodi-paga/{azienda}/{periodo}   stato completo
POST /api/periodi-paga/{id}/prepara          workflow
POST /api/periodi-paga/{id}/controlla        diagnostica/anomalie
POST /api/periodi-paga/{id}/approva           snapshot approvato
POST /api/periodi-paga/{id}/chiudi            genera pacchetto
```

Regole API:

- tenant solo dal token/contesto server;
- `ETag`/versione per aggiornamenti concorrenti;
- idempotency key per ogni comando ripetibile;
- errori in italiano semplice + codice stabile;
- nessun segreto in request/response persistita nei log;
- link all'audit e al documento approvato;
- autorizzazione su oggetto, non soltanto su rotta;
- download tramite URL breve e firmato, non path pubblici.

---

## 14. Strategia completa di test

### 14.1 Piramide tecnica

- unit test per formule, parser, writer e macchine a stati;
- property-based test per arrotondamenti e invarianti;
- integration test EF con PostgreSQL;
- API test con autenticazione e ruoli reali;
- contract test provider/webhook;
- test dei validatori ufficiali con fixture versionate;
- E2E browser per flussi e approvazioni;
- visual regression per cedolino, CU, moduli e schermate critiche;
- load test per batch paghe, upload e webhook;
- chaos test su timeout provider, coda e database;
- restore/disaster recovery test;
- penetration test prima del pilota e del lancio.

### 14.2 Matrice minima per ogni dato persistito

- crea/leggi/modifica/cancella secondo ruolo;
- accesso da altro tenant negato;
- export e retention;
- cifratura raw DB dove richiesta;
- audit lettura/scrittura;
- concorrenza e retry;
- migrazione da versione precedente;
- backup e restore.

### 14.3 Matrice minima per ogni funzione AI

- output conforme allo schema;
- astensione su dati insufficienti;
- parser deterministico prevalente;
- fonte obbligatoria per claim normativi;
- documento con prompt injection;
- richiesta di cambio tenant;
- tool non autorizzato;
- falso messaggio di approvazione;
- timeout/rate limit/provider offline;
- regressione modello/prompt;
- costo massimo;
- PII nei log;
- lingua semplice e accessibilità.

### 14.4 Matrice minima per ogni invio esterno

- dry-run;
- payload valido/invalid;
- approvazione assente/scaduta/corretta;
- doppio clic e retry;
- timeout dopo invio ma prima della risposta;
- callback duplicato/fuori ordine/falso;
- ricevuta positiva/negativa/intermedia;
- rettifica/annullamento;
- conservazione e recupero;
- audit completo.

---

## 15. Definition of Done rafforzata

Prima dell'esecuzione, ogni casella riceve una matrice `gate → applicabile/non applicabile →
evidenza`. Un gate non pertinente non resta eternamente aperto, ma `non applicabile` deve avere una
motivazione revisionata. Una casella 🤖 è completata soltanto se:

- [ ] requisito e rischio sono scritti;
- [ ] test nuovo fallisce prima della correzione quando applicabile;
- [ ] implementazione rispetta `decimal`, tenant e parametri versionati;
- [ ] build Release: zero errori;
- [ ] test backend: tutti verdi;
- [ ] frontend build verde se toccato;
- [ ] nessun nuovo warning lint senza motivazione;
- [ ] test tenant per ogni query nuova;
- [ ] autorizzazione e audit per ogni azione nuova;
- [ ] migrazione e rollback/forward-fix verificati se modifica dati/schema;
- [ ] sicurezza, privacy e licenza dipendenze revisionate;
- [ ] UX desktop/mobile/accessibile verificata;
- [ ] documentazione e runbook aggiornati;
- [ ] output sensibile etichettato correttamente;
- [ ] controllo ufficiale eseguito se applicabile e disponibile;
- [ ] validazione professionale eseguita se applicabile;
- [ ] evidenze collegate alla casella nel registro capacità;
- [ ] registro canonico aggiornato e viste Markdown rigenerate; vietati aggiornamenti manuali
  duplicati dopo l'adozione della nuova governance.

Un'intera fase è completata soltanto quando il relativo Gate è superato, non quando il codice è
stato scritto.

---

## 16. Fabbrica di sviluppo interamente assistita da AI

### 16.1 Flusso per ogni incarico

1. Il Coordinatore legge il registro capacità e prende la prima attività P0/P1 sbloccata.
2. Un agente ricerca fonti ufficiali se il dominio è normativo o corrente.
3. L'Architetto definisce confini, dati, endpoint e rischi.
4. Sicurezza/GDPR eseguono threat model e privacy check.
5. QA scrive criteri e casi di test prima dell'implementazione.
6. Un solo Esecutore modifica i file; gli altri restano read-only.
7. Build e test girano automaticamente.
8. Revisore e meta-revisore cercano regressioni e prove mancanti.
9. Browser test e render documentale verificano la UX/output.
10. Validatore decide `APPROVATO`, `DA CORREGGERE` o `BLOCCATO`.
11. Solo dopo approvazione si aggiorna lo stato.
12. Deploy staging automatico; produzione richiede approvazione fino a maturità dimostrata.

### 16.2 Limiti dell'agente di sviluppo

- niente commit/push/deploy senza incarico;
- niente modifica silenziosa di lavoro altrui;
- niente refactor estraneo;
- nessuna fonte normativa inventata;
- nessun segreto reale nei test;
- nessun dato cliente reale nel prompt/corpus;
- nessun aggiornamento automatico delle regole normative in produzione;
- una sola mutazione sui file condivisi alla volta;
- stop se il build rosso dipende da lavoro concorrente non completo.

### 16.3 Automazioni CI dell'agente

- issue/backlog con criteri di accettazione;
- branch/worktree isolato per incarico;
- generazione test e diff;
- review multi-agente;
- SBOM/licenze/vulnerabilità;
- changelog e migration note;
- staging e smoke test;
- report di release leggibile dal fondatore;
- rollback pronto;
- monitor post-release e apertura automatica incidente, senza fix distruttivi autonomi.

---

## 17. Piano 30/60/90 giorni

Le finestre partono quando il fondatore autorizzerà l'esecuzione e le modifiche concorrenti saranno
stabilizzate. Sono obiettivi condizionati ai gate, non un impegno cumulativo. Se un gate slitta, il
lavoro successivo non viene anticipato per rispettare la data.

### Giorni 1-30 — Rendere sicuro e misurabile

Priorità assolute:

1. Gate F-1: stabilizzazione del worktree e baseline pulita.
2. Fase 0: registro canonico, nicchia e modello giuridico.
3. Fase 0B: interviste, design partner, LOI, prezzo e baseline time-and-motion.
4. Fase 1: super-admin, chiave dati, segreti SdI, endpoint pubblici, dati particolari.
5. Avvio DPIA/DPA, basi giuridiche, classificazioni AI e informativa.
6. CI, API integration test e prototipo RLS.
7. Schema/migrazioni e restore drill.
8. Progettazione snapshot e registro regole.
9. Avvio account/provider/professionisti in parallelo.

**Risultato al giorno 30:** nessuna promessa di piena autonomia; staging sicuro, backlog vero,
professionisti coinvolti e misure iniziali disponibili.

### Giorni 31-60 — Costruire le fondamenta operative

1. Completare F2/F3.
2. Snapshot immutabile e ristampa storica.
3. Cartellino giornaliero minimo per il CCNL pilota.
4. Official Controls Factory base.
5. UniEmens/F24: gap principali e validatori correnti.
6. Document storage, coda e Inbox MVP.
7. UNILAV modalità proposta e visura side-by-side.
8. Corpus iniziale 100 documenti autorizzati/anonimizzati.
9. Migration Kit per il formato del primo design partner.

**Risultato al giorno 60:** pipeline documento→proposta affidabile e primo ciclo paghe riproducibile,
ancora in ambiente controllato.

### Giorni 61-90 — Demo shadow condizionale

Se e solo se F-1, F0/F0B, F1-F5 e i componenti minimi F7A/F8A/F8M sono verdi:

1. controllo anomalie e workflow periodo paghe sul vertical slice;
2. primo **demo shadow** su poche aziende, distinto da uno shadow normativamente rappresentativo;
3. F24/UniEmens come bozze; diventano ufficialmente testabili soltanto dopo adapter F10;
4. sandbox SdI/webhook se account e scope fatture sono disponibili, senza bloccare il payroll;
5. portale minimo per raccolta dati/documenti;
6. Copilot solo read-only: dati mancanti e spiegazione diagnostici;
7. misurazione tempi, errori, costi, falsi positivi e override;
8. piano del vero shadow rappresentativo, non go-live automatico.

Se i prerequisiti non sono verdi, i giorni 61-90 chiudono F2-F5/F7A/F8M e preparano il corpus: non si
finge un pilot. **Risultato possibile al giorno 90:** demo shadow e baseline di valore; lo shadow
rappresentativo e la produzione limitata seguono i gate F10/F17, non la data.

---

## 18. Orizzonte 3-24 mesi

| Orizzonte | Traguardo | Condizione |
|---|---|---|
| 0-3 mesi | staging sicuro, Inbox/snapshot; demo shadow solo se gate verdi | professionisti, dati e critical path disponibili |
| 3-6 mesi | pilota limitato su un CCNL, F24/UniEmens/INAIL e SdI sandbox | controlli ufficiali e provider |
| 6-9 mesi | produzione limitata, portale cliente, autopilota paghe L1/L3 | Gate F17: 3 cicli e una scadenza senza differenze oltre soglia |
| 9-12 mesi | fatture passive, prima nota, banca, chiusura contabile assistita | AISP/provider e commercialista |
| 12-18 mesi | dichiarativi in-scope, Tax Opportunity Engine, conservazione completa | dati annuali e validatori correnti |
| 18-24 mesi | secondo/terzo CCNL, scala e automazioni L2 selezionate | eval, supporto e unit economics |
| continuo | aggiornamenti normativi, security, AI eval, supporto e audit | non termina mai |

Un prodotto nazionale paragonabile per ampiezza ai maggiori gestionali richiede anni e manutenzione
permanente. L'AI riduce il tempo di sviluppo e il lavoro ripetitivo; non riduce il numero delle norme,
dei casi limite o delle prove ufficiali necessarie.

---

## 19. Blocchi esterni: ciò che l'AI non può procurare

Da avviare presto, in questo ordine:

1. [ ] 👤/⚖️ Consulente del Lavoro pilota, con accordo su casi e validazione.
2. [ ] 👤/⚖️ Commercialista pilota per contabilità, IVA e dichiarativi.
3. [ ] 👤/⚖️ Legale/DPO per forma del servizio, DPIA, DPA, informative e contratti.
4. [ ] 👤 Definizione nicchia e CCNL del pilota.
5. [ ] 🔑 Account provider SdI sandbox e contratto produzione.
6. [ ] 🔑 Deleghe/identità per servizi INPS, INAIL, Entratel e InfoCamere pertinenti.
7. [ ] 🔑 Certificato/firma o delega al provider, solo se necessaria.
8. [ ] 🔑 Conservatore e responsabile della conservazione.
9. [ ] 🔑 Provider open banking/AISP, se si attiva la riconciliazione.
10. [ ] 🔑 Canali notifiche e template approvati.
11. [ ] 👤 Documenti reali anonimizzati e autorizzati per corpus/golden cases.
12. [ ] 👤 Hosting, domini, email operative e secret manager.
13. [ ] 👤 Assicurazione RC/prodotto prima della vendita.

L'agente può preparare checklist, moduli, email, confronto fornitori e istruzioni; non può firmare
contratti, superare riconoscimenti, ottenere deleghe o assumere responsabilità professionali.

---

## 20. Gap competitivi da trasformare in vantaggio

La ricerca di mercato del 15 luglio 2026 mostra che OCR fatture, chatbot normativo e semplice
riconciliazione sono ormai funzioni comuni. PaghePro deve differenziarsi con una catena completa,
spiegabile e verificabile.

Le priorità usano due assi distinti: `release blocker/table stake` e `vantaggio competitivo`.

| Gap | Blocco rilascio | Vantaggio competitivo | Perché conta | Fase |
|---|---|---|---|---|
| sicurezza, snapshot e controlli ufficiali | P0 | base | prerequisito, non differenziatore | F1-F6A/F10 |
| Migration Kit riconciliato | P0 | table stake | riduce il costo di switching | F8M |
| cockpit telematico fino alla ricevuta | P0 per il modulo venduto | P1 | rende il prodotto operativo | F6A/F10/F13 |
| controllo anomalie causale/multiperiodo | P1 | P0 | riduce l'errore più costoso | F9 |
| autopilota chiusura paghe | P1 | P0 | principale risparmio mensile | F9-F10 |
| Inbox universale con provenienza | P1 | P0 | alimenta tutti i workflow | F7A/F7B |
| portale e recupero dati MVP | P1 | table stake/P1 | atteso dal mercato e necessario al flusso | F8A |
| radar scadenze event-to-action | P1 | P1 | trasforma calendario in lavoro concluso | F8A/F16 |
| chiusura contabile exception-first | P2 nel payroll MVP | table stake | necessaria nella seconda release | F12 |
| pagamenti con doppio controllo | P2 | P1 | colma il gap con piattaforme integrate | F10 |
| audit “show your work” | P1 | P0 | differenzia il copilot normativo commodity | F4/F14 |

Benchmark osservati dalle pagine ufficiali:

- TeamSystem: catena payroll, automazioni contabili, riconciliazione e assistente;
- Wolters Kluwer Genya: AI nei dichiarativi, banca e portale cliente;
- Sistemi PROFIS/JOB: proposte contabili, anomalie, workflow e portale;
- Zucchetti: portale HR, cedolini/CU, presenze, giustificativi e self-service;
- Jet HR: UX integrata per payroll, scadenze, pubblicazione e pagamenti;
- Factorial: agente HR e raccolta/controllo variabili;
- GBsoftware: assistente fiscale, documenti scansionati e console telematica;
- Passepartout/Datev: prima nota e banca assistite;
- Fatture in Cloud/Qonto: OCR, matching, scadenze e pagamenti per micro-imprese.

Le affermazioni dei concorrenti sono descrizioni commerciali, non prove indipendenti di accuratezza.

---

## 21. KPI e cruscotto di governo

### 21.1 Prodotto

- tempo onboarding azienda/dipendente: baseline, mediana e p90;
- tempo umano ciclo paghe per dipendente/azienda: baseline, mediana e p90;
- percentuale workflow completata prima dell'approvazione senza intervento;
- documenti/campi confermati senza correzione;
- richieste dati mancanti e tempo di risposta;
- pratiche chiuse entro scadenza;
- first-pass yield del validatore, rilavorazioni, ricevute accolte, scarti e rettifiche;
- ticket per 100 aziende e tempo di risoluzione.

### 21.2 Qualità paghe/fisco

- differenze rispetto al golden case per voce;
- file passati/falliti al controllo ufficiale;
- anomalie vere, false positive e false negative;
- override per regola, operatore e periodo;
- riconciliazione cedolino→F24→UniEmens→contabilità;
- artefatti originali recuperati con hash identico e rigenerazioni semanticamente equivalenti;
- aggiornamenti normativi completati prima della decorrenza.

### 21.3 AI

- exact match/precision/recall per campo e documento;
- tasso astensione corretto;
- risposta con fonte;
- tool call autorizzate/rifiutate;
- regressioni per modello/prompt;
- latenza p50/p95;
- costo per documento, dipendente e workflow;
- fallback e indisponibilità provider;
- correzioni utente riutilizzate nel tenant;
- incidenti di privacy/sicurezza: obiettivo zero.

### 21.4 Affidabilità e sicurezza

- uptime/SLO;
- error budget;
- RPO/RTO effettivi;
- restore drill riusciti;
- webhook duplicati correttamente assorbiti;
- vulnerabilità aperte per severità/età;
- accessi sensibili anomali;
- test cross-tenant falliti: obiettivo zero;
- tempo rilevazione/contenimento incidente.

### 21.5 Business

- costo operativo per azienda/dipendente;
- margine dopo AI/provider/supporto;
- attivazione pilota→pagante;
- retention/churn;
- moduli realmente usati;
- valore confermato delle opportunità fiscali, mai soltanto stimato;
- tempo fondatore richiesto per cliente.

---

## 22. Budget: categorie da pianificare

Non fissare prezzi pluriennali nel codice o nel piano: cambiano. Preparare un foglio aggiornabile con:

- hosting applicazione, PostgreSQL, storage, backup e traffico;
- provider AI per token/immagini e modello locale/hardware;
- OCR/antivirus/queue/observability;
- provider SdI e conservazione;
- firma, timestamp e desktop agent;
- open banking/pagamenti;
- email/SMS/WhatsApp/PEC;
- consulente del lavoro, commercialista, legale/DPO e penetration test;
- assicurazione;
- assistenza e manutenzione normativa;
- ambienti staging e corpus/eval;
- margine di rischio per picchi e incidenti.

Guardrail economici nel prodotto:

- budget per tenant e funzione;
- stima prima del job massivo;
- routing al modello minimo che supera gli eval;
- prompt caching/batch soltanto se compatibili con privacy e latenza;
- limite file/pagine e deduplica;
- nessun retry AI infinito;
- report costo→valore mensile.

---

## 23. Rischi principali e mitigazioni

| Rischio | Severità | Mitigazione |
|---|---|---|
| super-admin ottenibile alla prima registrazione | critica | bootstrap one-time fuori dal flusso pubblico |
| chiave cifratura di sviluppo usata in deploy | critica | fail-fast e KMS/secret obbligatorio |
| API key SdI/dati particolari in chiaro | alta | envelope encryption e rotazione |
| fuga cross-tenant da filtro dimenticato | alta | RLS + test API + policy strutturale |
| PDF storico cambia dopo aggiornamento | alta | snapshot immutabile e ristampa dallo snapshot |
| regola normativa ancora cablata | alta | registry fonti/regole, scansione e allowlist |
| AI inventa una norma o un importo | alta | RAG approvato, fonte obbligatoria, calcolo deterministico |
| prompt injection da documento | alta | contenuto non fidato, parser separato, tool policy |
| invio duplicato dopo timeout | alta | outbox, idempotenza e riconciliazione |
| decisione sui lavoratori classificabile high-risk | alta | escludere scoring/monitoraggio, classificazione legale |
| provider usa/ritiene dati in modo incompatibile | alta | DPA, retention, minimizzazione, local AI |
| validatore positivo scambiato per correttezza sostanziale | alta | revisione professionale e riconciliazione |
| UI promette AI non realmente disponibile | media | capability flags e stato onesto |
| dipendenza da un solo modello/provider | media | gateway, eval e fallback |
| costi AI incontrollati | media | budget, quota, routing, cache e report |
| codice generato rapidamente ma fragile | media | CI, review multi-agente, E2E e release gate |
| copertura “tutto subito” | alta | nicchia, CCNL e moduli a gate |

---

## 24. Decisioni che il fondatore dovrà prendere, una per volta

Non servono oggi per approvare il piano; serviranno prima delle rispettive fasi:

1. primo tipo di cliente;
2. CCNL e casi inclusi;
3. ruolo giuridico/commerciale di PaghePro;
4. professionisti pilota;
5. cloud e regione dati;
6. provider AI ammessi e opzione locale;
7. provider SdI;
8. firma provider o locale;
9. conservatore;
10. provider open banking/pagamenti;
11. canali di comunicazione;
12. livello di autonomia consentito per ciascun workflow;
13. prezzo e SLA;
14. criterio go-live e assicurazione.

Il metodo corretto resta: proporre una decisione con 2-3 alternative, spiegare costi/rischi e
chiedere una sola scelta alla volta.

---

## 25. Fonti ufficiali principali consultate

Consultazione: **15 luglio 2026**. Le versioni operative vanno ricontrollate all'inizio di ogni
fase e prima di ogni rilascio annuale.

### AI, lavoro e privacy

- [Regolamento UE 2024/1689 — AI Act](https://eur-lex.europa.eu/legal-content/IT/TXT/?uri=CELEX%3A32024R1689)
- [Commissione europea — quadro e calendario AI Act](https://digital-strategy.ec.europa.eu/en/policies/regulatory-framework-ai)
- [Commissione europea — AI literacy](https://digital-strategy.ec.europa.eu/en/faqs/ai-literacy-questions-answers)
- [Commissione europea — trasparenza AI](https://digital-strategy.ec.europa.eu/en/news/commission-publishes-code-practice-marking-and-labelling-ai-generated-content)
- [GDPR](https://eur-lex.europa.eu/legal-content/IT/TXT/?uri=CELEX%3A32016R0679)
- [Garante — registro dei trattamenti](https://www.garanteprivacy.it/registro-delle-attivita-di-trattamento)
- [Garante — data breach](https://www.garanteprivacy.it/data-breach)
- [Garante — elenco trattamenti soggetti a DPIA](https://www.garanteprivacy.it/home/docweb/-/docweb-display/docweb/9058979)
- [Legge 132/2025, uso dell'AI nelle professioni](https://www.gazzettaufficiale.it/eli/gu/2025/09/25/223/sg/pdf)
- [Legge 12/1979, attività in materia di lavoro](https://www.normattiva.it/uri-res/N2Ls?urn%3Anir%3Astato%3Alegge%3A1979-01-11%3B12=)
- [MLPS — Linee guida AI nel mondo del lavoro](https://www.lavoro.gov.it/stampa-e-media/mlps-linee-guida-ia.pdf)
- [Statuto dei lavoratori, art. 4](https://www.normattiva.it/atto/caricaDettaglioAtto?atto.articolo.numero=4&atto.codiceRedazionale=070U0300&atto.dataPubblicazioneGazzetta=1970-05-27)
- [D.Lgs. 152/1997, art. 1-bis — sistemi automatizzati nel rapporto di lavoro](https://www.normattiva.it/uri-res/N2Ls?urn%3Anir%3Astato%3Adecreto.legislativo%3A1997-05-26%3B152~art1bis=)

### Paghe, fisco e documenti

- [INPS — trasmissione UniEmens](https://www.inps.it/it/it/dettaglio-scheda.it.schede-servizio-strumento.schede-servizi.50252.trasmissione-uniemens-per-datori-di-lavoro-di-aziende-private.html)
- [INPS — software e validatori](https://www.inps.it/it/it/software.html)
- [INAIL — autoliquidazione](https://www.inail.it/portale/assicurazione/it/Datore-di-Lavoro/Impresa-con-dipendenti-industria-artigianato-terziario-altre-attivita/pagamento-premio-assicurativo-e-regolarita-impresa-con-dipendenti/autoliquidazione-impresa-con-dipendenti.html)
- [INAIL — basi di calcolo JSON 2025/2026](https://www.inail.it/portale/it/inail-comunica/avvisi/avviso.2025.07.avviso-autoliquidazione-2025-basi-calcolo.html)
- [Agenzia Entrate — software di controllo](https://telematici.agenziaentrate.gov.it/Main/Softcontr.jsp)
- [Agenzia Entrate — Desktop Telematico](https://telematici.agenziaentrate.gov.it/Main/Desktop.jsp)
- [Agenzia Entrate — presentazione e ricevute](https://telematici.agenziaentrate.gov.it/Main/Presentazione.jsp)
- [Agenzia Entrate — fatturazione elettronica](https://www.agenziaentrate.gov.it/portale/aree-tematiche/fatturazione-elettronica)
- [FatturaPA — accreditamento canale](https://accreditamento.fatturapa.gov.it/)
- [AgID — linee guida documento informatico](https://www.agid.gov.it/sites/agid/files/2024-06/Linee_guida_sul_documento_informatico.pdf)
- [DM 9 luglio 2008 — conservazione del Libro Unico](https://www.gazzettaufficiale.it/atto/vediMenuHTML?atto.codiceRedazionale=08A06099&atto.dataPubblicazioneGazzetta=2008-08-27&tipoSerie=serie_generale&tipoVigenza=originario)
- [CNEL — Archivio contratti](https://www.cnel.it/Archivio-Contratti)
- [CNEL — contratti open data](https://www.cnel.it/archivio-contratti/contratti-open-data)

### OpenAI, se scelto come provider

- [Model guidance corrente](https://developers.openai.com/api/docs/guides/latest-model)
- [Responses API](https://developers.openai.com/api/docs/guides/migrate-to-responses)
- [Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs)
- [Function calling](https://developers.openai.com/api/docs/guides/function-calling)
- [Data controls](https://developers.openai.com/api/docs/guides/your-data)
- [Model selection ed eval-first](https://developers.openai.com/api/docs/guides/model-selection)

### Concorrenti: fonti prodotto, non fonti normative

- [TeamSystem Studio](https://www.teamsystem.com/commercialisti/teamsystem-studio/)
- [Wolters Kluwer Genya](https://www.wolterskluwer.com/it-it/solutions/genya/genya-per-commercialisti)
- [Sistemi AI per PROFIS](https://www.sistemi.com/software-gestionali/profis/sistemi-ai-profis/)
- [Zucchetti Portale di Studio](https://www.zucchetti.it/it/cms/professionisti/software-paghe-hr/software-paghe-moduli-aggiuntivi-consulenti-del-lavoro/hr-portal-studio/portale-di-studio-paghe-consulenti-del-lavoro.html)
- [Jet HR](https://www.jethr.com/cose-jet-hr)
- [Factorial](https://factorial.it/hrsoftware)
- [GBsoftware](https://www.softwaregb.it/landing-guido-ia-pro-assistente-virtuale/)
- [Passepartout Passcom AI](https://www.passepartout.net/funzioni/intelligenza-artificiale-passcom)
- [Fatture in Cloud](https://www.fattureincloud.it/software-fatturazione/riconciliazione/)
- [Qonto](https://qonto.com/it/)

---

## 26. Verifiche eseguite per preparare il piano

Audit read-only sul worktree esistente:

- backend Release: **0 errori, 0 warning**;
- test backend Release: **482/482 verdi**;
- frontend build: verde;
- frontend lint: completato con **8 warning**;
- nessun file di codice modificato da questo lavoro;
- `PIANO.md` e `CLAUDE.md` lasciati intatti perché già modificati da altri lavori;
- creato soltanto questo documento nuovo.

Queste verifiche provano la baseline tecnica corrente, non la correttezza normativa o la readiness
di produzione.

---

## 27. Verdetto del piano

**Verdetto finale del panel: APPROVATO come piano; non è un'approvazione al go-live del prodotto.**

Il primo esame ha richiesto correzioni su discovery, migrazione, misure statistiche, confini
professionali, GDPR/AI Act, RLS, dipendenze, granularità delle fasi e gate del pilota. Dopo le
correzioni, i riesami indipendenti di mercato/scope, normativa/sicurezza e
architettura/fattibilità non hanno rilevato finding alti o medi aperti.

Il piano è eseguibile soltanto a gate. La priorità non è aggiungere subito un grande chatbot o il
Tax Saving Engine: è correggere sicurezza P0, creare verità di stato, migrazioni/RLS, snapshot e
motore normativo riproducibile. Solo dopo si estendono Inbox, agenti e automazioni.

Il prodotto finale può raggiungere un'automazione molto alta, ma deve restare uno strumento del
datore/professionista e non presentarsi come sostituto autonomo del professionista abilitato.
L'esecuzione non è autorizzata da questo documento: dopo una richiesta separata, il primo gate è
F-1, perché il worktree contiene lavori concorrenti da attribuire e stabilizzare.

---

## 28. Decisione dei tre ruoli post-lavoro

| Ruolo | Decisione | Motivo | Prossima azione |
|---|---|---|---|
| Controllore Consegna | **APPROVATO COME PIANO** | La richiesta è coperta in dettaglio; piano, certificazione e go-live sono separati; è stato creato solo questo documento. | Consegnare il piano senza avviare fasi. |
| Direttore Prossimo Incarico | Il primo incarico futuro è **Gate F-1**. | Il worktree contiene modifiche concorrenti e hotspot condivisi; serve una baseline attribuibile prima di sviluppare. | Solo dopo autorizzazione esplicita dell'utente: preservare tutto, attribuire i lavori e creare la baseline verde. |
| Ricercatore Mercato/GAP | Congelare il vertical slice su tre differenziatori. | Autopilota paghe causale, Inbox con provenienza e catena auditabile fino alla ricevuta sono più difendibili di OCR/chatbot generici già comuni. | Misurare nel pilota F17: F9-F10, F7A/F7B-F8A/F8M e F4-F6A-F10/F13-F14. |

I tre gap derivano dal confronto con le pagine prodotto ufficiali elencate in §25, fra cui
[TeamSystem](https://www.teamsystem.com/commercialisti/teamsystem-studio/),
[Jet HR](https://www.jethr.com/cose-jet-hr),
[Factorial](https://factorial.it/hrsoftware),
[Wolters Kluwer Genya](https://www.wolterskluwer.com/it-it/solutions/genya/genya-per-commercialisti),
[GBsoftware](https://www.softwaregb.it/landing-guido-ia-pro-assistente-virtuale/) e
[Fatture in Cloud](https://www.fattureincloud.it/software-fatturazione/riconciliazione/).
