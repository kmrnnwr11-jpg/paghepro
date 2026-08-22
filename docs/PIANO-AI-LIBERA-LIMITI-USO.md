# PaghePro Personale — AI libera nel perimetro, limiti d'uso

> Versione: **15 luglio 2026**
> Stato: **piano operativo approvato; nessuna implementazione autorizzata da questo documento**
> Profilo: un operatore locale, con AI interna a PaghePro e nessuna lettura delle app personali
> Decisione: l'AI può lavorare su **tutti i dati e moduli professionali dentro PaghePro**; ogni
> attività è però governata da un mandato limitato per finalità, ambito, tempo, quantità,
> sensibilità, operazioni e destinazione.

---

## 0. Decisione in parole semplici

Il limite non sarà più «l'AI può leggere soltanto UNILAV» o «può usare soltanto un modulo».
Questi limiti renderebbero il lavoro artificiosamente difficile.

La nuova regola è:

```text
LIBERA DENTRO PAGHEPRO
L'AI può leggere, collegare, controllare, spiegare e preparare lavoro
su qualunque dato professionale ammesso nella cassaforte PaghePro.

LIMITATA FUORI E NELLE AZIONI
Non vede il resto del computer e, per ogni lavoro, riceve soltanto
il tempo, i dati e i permessi necessari. Non può allargarseli da sola.
```

In pratica:

- sì a paghe, dipendenti, aziende, contratti, presenze, fatture, contabilità, F24, UniEmens,
  INAIL, dichiarativi, scadenze, diagnostici ufficiali e comunicazioni di lavoro importate;
- sì a un assistente conversazionale **dentro PaghePro**, vincolato alla pratica o allo spazio di
  lavoro visibile;
- sì ad analisi trasversali fra moduli, quando l'utente abilita una modalità Workspace;
- sì a lavori interni programmati, dopo che la singola ricetta è stata approvata;
- no a una AI che osserva il Mac, legge cartelle personali, entra in altre app o usa chat esterne;
- no a permessi permanenti, invisibili o ampliati dal modello;
- no a calcoli normativi inventati dal modello;
- no a invii, firme, pagamenti, cancellazioni definitive o decisioni sui lavoratori senza il
  controllo esatto previsto per quell'azione.

«Tutti i dati professionali» non significa che ogni modulo sia già pronto oggi. Significa che
nessun modulo viene escluso per principio: la lettura e la proposta si attivano quando il relativo
adapter è sicuro; scritture, automazioni e azioni esterne si attivano solo dopo i gate tecnici,
ufficiali e professionali del master plan.

---

## 1. Quale piano prevale

I tre documenti hanno ruoli diversi:

| Documento | Ruolo attuale |
|---|---|
| [`PIANO-MASTER-AI-AUTONOMO.md`](PIANO-MASTER-AI-AUTONOMO.md) | Catalogo completo delle capacità, dei moduli e dei gate di dominio. |
| **Questo documento** | Regola canonica su cosa l'AI può vedere, per quanto tempo e con quali azioni nel profilo personale. |
| [`PIANO-PERSONALE-PRIVACY-FIRST.md`](PIANO-PERSONALE-PRIVACY-FIRST.md) | Baseline di sicurezza locale e definizione della modalità **Stretta**; non limita più tutto il prodotto al solo UNILAV. |

In caso di conflitto sul perimetro AI e sui limiti d'uso, prevale questo documento. Restano invece
invariati i gate di correttezza paghe, i controlli ufficiali, le validazioni professionali e le
regole tecniche del master.

### 1.1 Cosa non autorizza questo piano

Questo documento non autorizza:

- modifiche al codice;
- uso con dipendenti o clienti reali;
- dichiarazioni di conformità legale o certificazione;
- attivazione di provider cloud, connettori, invii o pagamenti;
- superamento dei gate già previsti nel master.

Ogni implementazione partirà soltanto con una richiesta separata e seguirà il primo gate non
completato.

---

## 2. I limiti giusti: non sul lavoro, ma sul mandato

Ogni esecuzione AI deve rispondere a sette domande:

1. **Origine:** il dato è già in PaghePro o è stato importato volontariamente?
2. **Finalità:** quale attività professionale si sta svolgendo?
3. **Ambito:** quali aziende, persone, periodi, pratiche e documenti può usare?
4. **Durata:** per quanto resta valido il mandato?
5. **Quantità:** quante risorse e quanta elaborazione può consumare prima di un checkpoint?
6. **Operazioni:** può leggere, proporre, scrivere in modo reversibile o eseguire una ricetta?
7. **Destinazione:** il risultato resta interno, viene esportato o produce un effetto esterno?

Il sistema non domanda più «questo è uno dei pochissimi tipi documentali permessi?». Domanda:
«questo è materiale professionale, per questa finalità, dentro questo mandato?».

### 2.1 Nessun tetto commerciale o giornaliero artificiale

I limiti d'uso non sono una licenza a consumo e non devono interrompere il lavoro normale:

- nessun numero massimo giornaliero di domande;
- nessun limite permanente al numero di aziende, dipendenti o moduli;
- nessuna perdita del lavoro quando si raggiunge una soglia;
- checkpoint morbido: il progresso viene salvato e l'utente può scegliere `Continua`;
- il proseguimento conserva scopo e finalità, senza ricominciare da zero;
- i massimi tecnici servono soltanto a evitare blocco del Mac, loop o elaborazioni accidentali;
- i valori iniziali vengono calibrati con benchmark sul Mac reale e possono essere aumentati senza
  modificare l'identità del prodotto.

### 2.2 Due assi distinti

L'ampiezza dei dati e l'autonomia delle azioni non sono la stessa cosa.

#### Ambito dati

| Livello | Dati raggiungibili |
|---|---|
| `S0 — Selezione` | Record, documento o elemento attualmente selezionato. |
| `S1 — Pratica` | Pratica, dipendente, fattura o adempimento corrente. |
| `S2 — Azienda/periodo` | Azienda e intervallo, mese paghe o dossier nominato. |
| `S3 — Workspace` | Tutti i dati professionali inclusi nello spazio di lavoro dichiarato. |
| `S4 — Archivio professionale` | Intera cassaforte professionale del tenant; breve e normalmente sola lettura. |

#### Autonomia

| Livello | Capacità |
|---|---|
| `A0` | AI spenta. |
| `A1` | Legge, cerca, collega, cita e spiega. |
| `A2` | Produce analisi, checklist, anomalie e bozze. |
| `A3` | Applica modifiche interne reversibili dopo anteprima/conferma. |
| `A4` | Esegue una ricetta interna programmata, versionata e revocabile. |
| `A5` | Esegue una singola azione esterna esatta dopo conferma forte; non disponibile finché il relativo gate non è approvato. |

Un accesso ampio può quindi essere `S3+A1`, mentre una correzione puntuale può essere `S1+A3`.
Non si concede mai automaticamente `S3+A3` o `S4+A3`.

---

## 3. Confini duri che restano sempre

Questi non sono limiti di comodità: sono il muro fra software professionale e computer personale.

### 3.1 Fuori dal perimetro

L'AI non può accedere direttamente a:

- Desktop, Documenti, Download, cartella Home, dischi, cartelle di rete o backup generici;
- Mail, PEC, WhatsApp, Teams, Slack, messaggi, contatti o calendario;
- Drive, OneDrive, Dropbox, iCloud o altri cloud;
- browser, cronologia, cookie, password o estensioni;
- clipboard globale, tastiera, microfono, videocamera, foto o localizzazione;
- software paghe/contabili di terzi;
- banca, carte o portali pubblici;
- shell, SQL libero, filesystem generico, HTTP o automazione browser;
- chiavi, PIN, password, certificati privati e credenziali degli enti.

Un file esterno entra soltanto con selezione, trascinamento o importazione esplicita. Dopo la copia
nella cassaforte, il permesso temporaneo al file originale viene revocato.

### 3.2 AI e rete

Nel profilo personale iniziale:

- modello, OCR, embedding, indice e Copilot sono locali;
- nessun provider AI cloud e nessun fallback silenzioso;
- nessun invio di prompt, documento, telemetria o crash report a terzi;
- processi AI senza entitlement di rete e senza TCP/UDP;
- IPC locale tipizzato via XPC/socket Unix;
- aggiornamento separato, firmato e senza accesso alla cassaforte;
- nessun training, fine-tuning o apprendimento online sui dati dell'utente.

### 3.3 Dati, calcoli e responsabilità

- ogni query persistita e ogni ricerca semantica filtra obbligatoriamente per `tenantId`;
- il modello non accede direttamente a EF/SQLite/PostgreSQL;
- importi e aliquote restano `decimal` nei motori deterministici;
- regole normative provengono dal database versionato per periodo;
- l'AI può spiegare un calcolo, ma non sostituirlo con un numero generato;
- output non certificati restano `BOZZA DA VALIDARE`;
- l'AI non decide assunzione, licenziamento, disciplina, affidabilità, salute o ranking dei
  lavoratori;
- nessuna inferenza sensibile non necessaria alla finalità dichiarata;
- il software fondamentale continua a funzionare con AI spenta.

### 3.4 Nuovo codice e nuovi strumenti

Ogni nuova tabella, risorsa, API o tool nasce `DENY` per l'AI. Diventa utilizzabile solo dopo:

1. registrazione nel catalogo professionale;
2. classificazione dei dati e degli effetti;
3. policy d'uso versionata;
4. test di tenant, scope, revoca, audit e injection;
5. gate del modulo previsto dal master.

Il modello non può creare, modificare, firmare, rinnovare o estendere una policy o un mandato.

---

## 4. La cassaforte professionale

La `Professional Vault` è l'unico universo di dati raggiungibile dall'AI.

### 4.1 Cosa può contenere

Senza una lista semantica chiusa, può contenere materiale professionale necessario a:

- anagrafiche, aziende, dipendenti, collaboratori e rapporti;
- CCNL, contratti, lettere, UNILAV e pratiche;
- presenze, variabili, assenze e documentazione amministrativa pertinente;
- cedolini, riepiloghi e prospetti paghe;
- F24, CU, 770, LIPE, IVA, UniEmens, INAIL e diagnostici ufficiali;
- fatture attive/passive, note di credito, prima nota, bilancio e riconciliazioni;
- visure, documenti societari, deleghe e scadenze;
- esportazioni bancarie importate manualmente per una riconciliazione autorizzata;
- email o comunicazioni di lavoro esportate e importate volontariamente;
- fogli di calcolo, note, PDF, immagini e report collegati alla pratica;
- output e bozze creati da PaghePro.

Il contenuto è eleggibile perché professionale; la capacità effettiva dipende dall'esistenza di un
parser sicuro e di un adapter di modulo. Un documento sconosciuto può essere letto o riassunto in
bozza, ma non può aggiornare campi definitivi senza schema, validazione e conferma.

### 4.2 Classi di dato

| Classe | Esempi | Regola AI |
|---|---|---|
| `D0 — Normativo/pubblico` | fonti, tabelle e istruzioni ufficiali | Lettura nei periodi validi, fonte sempre citata. |
| `D1 — Aziendale` | fatture, scadenze, contabilità, dati societari | Ammesso nel mandato azienda/pratica. |
| `D2 — Personale di lavoro` | anagrafiche, rapporto, paga, presenze | Ammesso per finalità professionale e scope esatto. |
| `D3A — Particolare, art. 9` | salute, appartenenza sindacale e altre categorie particolari | Grant separato, legal basis/condizione esatta, campi minimi, niente memoria durevole o confronti. |
| `D3B — Penale, art. 10` | condanne e reati, se davvero pertinenti | Disciplina e autorizzazione distinte da D3A; grant separato e nessun uso implicito. |
| `D4 — Segreto operativo` | password, PIN, chiavi, token, chiavi private | Mai nel contesto del modello. |
| `DX — Privato/irrilevante` | foto familiari, posta privata, documenti domestici | Rifiutato e cancellato dalla quarantena. |

I dati `D3A/D3B` non vengono esclusi quando sono davvero necessari alla paga: vengono trattati con un
mandato più stretto. La diagnosi o dettagli non necessari restano esclusi per minimizzazione.

### 4.3 Inbox e importazione

Il flusso previsto è:

```text
selezione/drag-and-drop volontario
        ↓
quarantena locale cifrata
        ↓
controllo magic/MIME, struttura, contenuto attivo, macro, decompressione e limiti
        ↓
classificazione locale: professionale / misto / particolare / privato / non sicuro
        ↓
associazione ad azienda, pratica, periodo e finalità
        ↓
cassaforte + OCR/indice locale cifrato + provenienza
```

Regole operative:

- `Inbox PaghePro` è un'area interna dell'app, non un watcher del Mac;
- importare o trascinare un batch è un atto esplicito dell'utente;
- per file chiaramente professionali nel mandato attivo non serve confermare ogni pagina;
- un batch mostra un solo manifest prima dell'acquisizione definitiva;
- file dubbi, misti, `D3A` o `D3B` chiedono una decisione separata;
- parti private vengono escluse o il file viene rifiutato;
- per documenti misti si preferiscono pagine/zone/fogli minimizzati;
- originali completi si conservano solo se esiste una necessità documentata;
- nessun accesso ricorsivo permanente a una cartella esterna;
- un'importazione di cartella, se introdotta, è one-shot, mostra il manifest e revoca subito il
  bookmark dopo la copia.

### 4.4 Formato non significa finalità

Non esiste una allowlist chiusa di **significati professionali**. Esiste invece una allowlist
tecnica di parser sicuri:

- eseguibili, script e contenuto attivo non vengono eseguiti;
- macro sono disabilitate;
- archivi cifrati, annidati o con decompressione anomala restano in quarantena;
- symlink, alias e path traversal vengono rifiutati;
- un formato non supportato può essere conservato cifrato ma non letto dall'AI;
- l'aggiunta di un parser richiede sandbox, limiti e corpus avversariale.

Uno scanner malware offline è opzionale: entra nel prodotto soltanto dopo POC, verifica di
licenza, manutenzione e supply chain nel Gate G0. Non si introduce codice AGPL/GPL nel SaaS senza
una decisione esplicita e documentata.

Questa restrizione evita malware; non riduce le categorie professionali che il prodotto può
gestire.

### 4.5 Segreti e control plane restano fuori dalla Vault AI

Prima di qualunque modello, embedding o OCR reso indicizzabile opera un
`LocalSecretBoundaryFilter` deterministico. Riconosce formati di chiavi/certificati, token noti,
pattern di credenziali, segmenti ad alta entropia e campi marcati `secret`; separa o redige il
segmento `D4`. Il contenuto originale può restare nella vault sigillata quando necessario, ma:

- non viene indicizzato, inserito in prompt/memoria o restituito dal Query Broker;
- l'utente non può forzare un override verso il modello;
- una credenziale viene usata soltanto da un broker deterministico dedicato;
- un rilevamento incerto blocca il segmento e chiede gestione manuale.

Anche il `Security Control Plane` è fuori dalla `Professional Vault`: policy, grant record/lease,
revoche, nonce, firme, system prompt, secret store, retention ledger, metriche di sicurezza e audit
grezzo non sono risorse AI. Il modello riceve soltanto il manifest sanitizzato del grant. La UI
`Cosa vede l'AI` legge una vista deterministica e minimizzata dell'audit, con ID opachi e senza
payload sensibili.

---

## 5. Quattro modalità semplici

La persona sceglie un preset. Il software mostra ciò che verrà letto e fatto; non chiede una
conferma per ogni operazione innocua.

### 5.1 Modalità Stretta

Uso: spiegare una voce, leggere pochi documenti, controllare un dipendente o usare dati
`D3A/D3B`.

| Proprietà | Default iniziale |
|---|---|
| Scope | `S0` o `S1`, elementi esatti |
| Durata | 30 minuti assoluti; 10 minuti di inattività |
| Checkpoint | 25 documenti, 2.000 record o 200 MB |
| Autonomia | `A1-A2`: lettura, controllo, spiegazione e bozza |
| Scritture | nessuna |
| Memoria | solo task, eliminata alla chiusura/revoca |

### 5.2 Modalità Guidata — predefinita

Uso: onboarding, preparazione mese, pratica, fattura, dichiarativo o flusso aziendale.

| Proprietà | Default iniziale |
|---|---|
| Scope | `S1` o `S2`: pratica, azienda e periodo scelti |
| Sessione UI | massimo 8 ore; conserva soltanto la selezione visiva |
| Grant dati | 30 minuti di inattività, massimo 2 ore assolute; nessun rinnovo automatico |
| Checkpoint morbido per job | 250 documenti, 25.000 record o 30 minuti di elaborazione AI |
| Autonomia | `A1-A2`; `A3` solo per un lotto confermato |
| Scritture | massimo 100 reversibili per lotto; poi nuovo diff |
| Memoria | task cifrata; massimo 24 ore, poi cancellazione o salvataggio esplicito di una sintesi |

La modalità Guidata non interrompe il lavoro ogni ora. La sessione può durare il giorno lavorativo;
sono i singoli job pesanti a mostrare un checkpoint riprendibile. Lock, sleep, cambio utente e
logout revocano sempre il grant dati: al ritorno la UI può riproporre lo stesso scope, ma emette un
nuovo grant soltanto dopo conferma.

### 5.3 Modalità Workspace

Uso: chiusura mensile, dossier, riconciliazione paghe–F24–UniEmens–contabilità o analisi fra
moduli.

| Proprietà | Default iniziale |
|---|---|
| Scope | `S3`, workspace nominato con moduli/aziende/periodi visibili |
| Durata | 15 minuti di inattività, 2 ore assolute; nuovo grant esplicito senza ricreare la selezione |
| Checkpoint morbido | 500 documenti, 50.000 record o 45 minuti di elaborazione AI |
| Autonomia | `A1-A2` sull'intero scope |
| Scritture | vietate sullo scope globale; si crea una proposta e si restringe a un lotto `S1/S2` |
| Memoria | indice locale cifrato del workspace, massimo 7 giorni e cancellabile subito |

L'accesso all'intera `S4 — Archivio professionale` è una variante Workspace straordinaria:

- autenticazione rafforzata;
- finalità visibile;
- 15 minuti per una singola analisi;
- sola lettura;
- nessun dato `D3A/D3B`;
- nessun rinnovo automatico;
- nuovo grant per superare il checkpoint.

### 5.4 Modalità Programmata

Uso: controlli dati mancanti, anomalie, scadenze, bozze e report interni ripetitivi.

| Proprietà | Default iniziale |
|---|---|
| Scope | ricetta versionata con risorse e selettori immutabili |
| Durata | massimo 2 ore per run |
| Checkpoint | 1.000 documenti, 100.000 record o budget hardware definito |
| Concorrenza | 1 job pesante + 3 job leggeri; coda sicura oltre soglia |
| Autonomia | `A4`, soltanto operazioni interne e reversibili previste dalla ricetta |
| Memoria | cancellata al termine; restano risultato approvato e audit minimo |

Non esiste un limite fisso di run giornalieri. Il scheduler usa coda, temperatura, RAM, batteria e
finestre orarie. Un cambiamento a ricetta, policy, modello, regola o scope sospende il job e chiede
una nuova approvazione.

Una ricetta `A4` senza mandato di scrittura produce soltanto bozze. L'utente può concedere una
`standing approval` vincolata a `recipeVersion`, scope, tool, campi, massimo numero di effetti e
scadenza. Questa vale come conferma per i soli run perfettamente conformi; ogni run conserva diff e
undo. Qualsiasi cambiamento, superamento o anomalia sospende la ricetta e richiede una nuova
approvazione.

La `ScheduledRecipeAuthorization` scade dopo 30 giorni o 30 run, secondo quale soglia arriva
prima. Mantiene immutabili selector, policy, schema, tool, modello e classificazione; ogni
esecuzione riceve un grant separato di massimo 2 ore. Nuovi record sono ammessi soltanto se
rientrano in un selector dichiarato, per esempio `creati dopo sourceCheckpoint`; un cambio di
schema, classe, scope, policy, tool o modello sospende il lavoro.

Per default lock e sleep mettono in pausa un run programmato e ne revocano il grant. Un futuro
`run while locked` richiede opt-in separato, può essere solo `A1-A2` read/draft, non usa
`D3A/D3B` e non scrive.

### 5.5 Dati particolari: grant `GS`

Per dati `D3A/D3B` ogni grant richiede ri-autenticazione locale, `purposeCode`, `legalBasisRef`,
record/campi esatti e conferma separata. Non è rinnovabile automaticamente.

Sono previsti due casi:

- `GS-X`, accesso eccezionale o a frammento grezzo: massimo 5 minuti e una singola operazione;
- `GS-W`, workflow paghe con governance/policy `D3A/D3B` già approvata: lotto omogeneo `S1/S2`, massimo
  30 minuti attivi o fino al checkpoint, con un solo preflight e solo campi minimizzati.

Per entrambi:

- record e campi esatti;
- una finalità specifica;
- niente Workspace globale;
- niente task programmati finché non esiste una policy professionale approvata;
- niente confronto, ranking o riepilogo fra lavoratori;
- niente memoria durevole, benchmark o analytics;
- output minimizzato ai soli campi necessari.

### 5.6 Azione esterna: grant `GX` one-shot

Nel primo profilo personale gli effetti esterni restano disabilitati. Se in futuro un modulo supera
i gate del master, ogni invio/firma/pagamento richiede comunque un token separato:

- azione, payload, importo, destinatario e canale esatti;
- anteprima finale;
- autenticazione forte e approvazione della persona/professionista previsto;
- durata massima 5 minuti;
- uso singolo, nonce anti-replay;
- qualsiasi variazione invalida il token;
- ricevuta e audit obbligatori;
- nessun wildcard, rinnovo o approvazione in blocco.

### 5.7 Cosa succede quando si raggiunge un limite

Il sistema:

1. termina il passo corrente in modo coerente o lo annulla in transazione;
2. salva checkpoint, fonti già lette, proposte e lavoro rimanente;
3. mostra il motivo della pausa e il consumo reale;
4. offre `Continua con lo stesso scope`, `Riduci`, `Passa al manuale` o `Termina`;
5. richiede un nuovo preflight solo se cambiano dati, finalità o operazioni;
6. non tronca silenziosamente la risposta e non perde il lavoro.

Un massimale tecnico iniziale di sicurezza, da validare con benchmark, divide automaticamente un
job oltre 5.000 documenti o 10 GB in lotti. Non vieta il lavoro: impedisce un'unica esecuzione
monolitica non controllabile.

---

## 6. Il mandato `AiScopeGrant`

Il mandato ha due parti distinte:

- `AiScopeGrantRecord`: record server-side autorevole per contenuto, stato, versioni, quote,
  contatori e revoca;
- `AiScopeLease`: handle opaco firmato con soli `grantId`, `nonce`, `issuedAt` ed `expiresAt`.

Il modello vede esclusivamente un manifest descrittivo e non riceve la lease. Ogni tool risolve la
lease lato server, ricontrolla la policy e aggiorna atomicamente il record prima di accedere ai
dati.

```text
AiScopeGrantRecord
  grantId
  ownerId
  tenantId
  purposeCode
  governanceRecordId / legalBasisRef
  mode: strict | guided | workspace | scheduled | external_one_shot
  scopeLevel: S0 | S1 | S2 | S3 | S4
  resourceSelectors
    moduleIds
    exactResourceIds
    aziendaIds
    dipendenteIds
    praticaIds
    periodi
    documentIds / pageRanges / fieldClasses
  allowedOperations
    read | search | compare | derive | draft
    writeReversible | scheduleInternal | externalOneShot
  allowedToolIds
  autonomyMax
  sensitivityCeiling
  validFrom / expiresAt / idleTimeout
  quotas
    maxDocuments / maxPages / maxRecords / maxBytes
    maxModelTokens / maxToolCalls / maxComputeSeconds
    maxProposals / maxReversibleWrites
  outputPolicy: ephemeral | saveDraft | saveConfirmed
  memoryPolicy: none | currentTask | workspaceTtl
  policyVersion / schemaVersion / classificationVersion
  recipeVersion / sourceCheckpoint
  counters / status / revokedAt

AiScopeLease
  grantId / nonce / issuedAt / expiresAt / signature
```

### 6.1 Regole di enforcement

- il grant nasce da UI e policy server, non dal prompt;
- ogni retrieval e tool call ricontrolla firma, tenant, finalità, scope, tool, operazione, quota,
  scadenza, revoca e versioni;
- un diniego in qualunque livello prevale;
- contatori atomici impediscono di aggirare una quota con paginazione o chiamate concorrenti;
- selettori tipizzati costruiscono le query; mai SQL generato dal modello;
- cambiare azienda, periodo, destinatario o payload richiede un nuovo grant;
- il modello può **chiedere** un ampliamento spiegandone il motivo, ma solo l'utente può concederlo;
- chiusura task, lock, cambio utente o `Arresta AI` revocano i grant applicabili;
- ogni run conserva lo snapshot immutabile del grant usato.

### 6.2 Contratto `AiUsagePolicy`

La policy stabilisce il massimo concedibile; il grant può soltanto restringerla.

```text
AiUsagePolicy
  policyId / version / effectiveFrom
  purposeCodes / operatorRoles
  moduleIds / resourceTypes / dataClasses
  maximumScope / maximumAutonomy
  allowedOperations / allowedToolIds
  defaultQuotas / hardQuotas / maximumTtl
  approvalRules / sensitiveDataRules
  deterministicPreconditions
  outputPolicy / memoryPolicy / retentionPolicy
  prohibitedEffects
  rollbackRequirement
  requiredTechnicalGate
  requiredOfficialEvidence
  requiredProfessionalValidation
  supersedesPolicyId
```

Una policy nuova non eredita permessi impliciti. La versione precedente resta collegata per audit,
ma non può autorizzare nuovi run dopo la propria sostituzione.

### 6.3 Formula della decisione

```text
confine locale
∩ permessi dell'operatore
∩ AiUsagePolicy versionata
∩ AiScopeGrant attivo
∩ contratto tipizzato del tool
∩ classificazione del dato
∩ stato e gate del modulo
= accesso o azione concessa
```

Non basta che il modello «prometta» di rispettare il perimetro.

---

## 7. Componenti da costruire

### 7.1 Control plane

| Componente | Responsabilità |
|---|---|
| `ProfessionalResourceCatalog` | Censisce risorse, tenant, classi dati, retention e sensibilità. |
| `AiCapabilityRegistry` | Elenca capability, tool, modalità e gate del master. |
| `AiUsagePolicyStore` | Conserva policy versionate per modulo, finalità e classe dati. |
| `AiGrantService` | Preflight, emissione, rinnovo esplicito, restringimento e revoca. |
| `AiPolicyEvaluator` | Unica autorità per consentire o negare accessi e azioni. |
| `ProfessionalQueryBroker` | Restituisce soltanto record autorizzati, paginati e conteggiati. |
| `AiToolRegistry` | Espone tool tipizzati e classificati per effetto. |
| `AiContextBuilder` | Costruisce il minimo contesto utile senza riversare tutto l'archivio. |
| `AiUsageMeter` | Misura documenti, byte, record, token, tool, durata e compute. |
| `AiRevocationService` | Arresto globale e revoca immediata per grant/run/ricetta/tool. |

### 7.2 Piano delle azioni

| Componente | Responsabilità |
|---|---|
| `AiActionProposalStore` | Separa proposta, diff, conferma, applicazione e undo. |
| `AiApprovalStore` | Registra chi ha approvato esattamente cosa e quando. |
| `AiRunStore` | Stato, checkpoint, idempotenza, tentativi e ripresa. |
| `AiScheduler` | Esegue ricette interne entro finestra, budget e concorrenza. |
| `Paghe.ExternalBroker` | Futuro e separato: consuma un payload `GX` sigillato, usa un solo canale allowlisted e restituisce la ricevuta. |

`Paghe.ExternalBroker` non fa parte del pacchetto `PersonalLocal` finché un piano separato non
approva canale, DPIA, credenziali, licenze e threat model. Nel profilo futuro:

- è un processo sandboxato distinto dal data path AI;
- non monta database, vault o indice e non può eseguire query;
- riceve esclusivamente un payload normalizzato, minimizzato e firmato dal `GX`;
- custodisce le credenziali nel Keychain/keystore accessibile soltanto al broker;
- consente egress solo verso endpoint e certificati ufficiali allowlisted;
- nega URL dinamici, redirect, DNS non pinning, SSRF e payload modificati;
- restituisce soltanto una ricevuta firmata e riconciliabile.

### 7.3 Audit

`AiAudit` registra senza duplicare il contenuto sensibile:

- utente, tenant, finalità e grant;
- scope, classi e ID delle risorse consultate;
- quantità, pagine/campi e digest della query;
- modello/runtime/versione e tool usati;
- decisioni allow/deny e motivo;
- proposta, conferma, effetto, undo e risultato;
- revoca, checkpoint, ripresa e anomalia;
- correlation ID e catena tamper-evident.

Prompt, OCR integrale, CF, IBAN, diagnosi, password e documenti non vengono copiati nell'audit.
La retention dei metadati viene decisa nel pacchetto GDPR; 24 mesi è soltanto l'ipotesi da validare,
non un valore legale già approvato.

---

## 8. Memoria e ricerca su tutto il lavoro

### 8.1 Indice locale trasversale

È ammesso un indice semantico dell'intera cassaforte professionale, a queste condizioni:

- embedding locali, mai inviati a provider;
- cifratura e chiavi-oggetto;
- partizioni e label obbligatorie per tenant, azienda, persona, periodo, modulo, categoria e fonte;
- filtro del grant applicato **prima** del retrieval;
- nessuna ricerca globale diretta del modello;
- cancellazione di OCR, chunk, embedding e sintesi insieme alla fonte;
- documenti `DX` e quarantena mai indicizzati;
- i dati `D3A/D3B` non entrano in chunk, embedding, sintesi o Workspace memory: vengono
  recuperati on-demand dai soli campi/frammenti esatti autorizzati da `GS`, restano nella working
  memory effimera e vengono eliminati alla chiusura o revoca;
- manifest esatto delle fonti restituite al modello.

Un futuro indice `D3A/D3B` richiede un nuovo piano, una DPIA e un gate separato; non è autorizzato
implicitamente da questo documento.

### 8.2 Tre tipi di memoria

| Memoria | Regola |
|---|---|
| `Working memory` | Locale, cifrata, isolata per grant; si elimina a chiusura/revoca o al TTL. |
| `Memoria pratica` | Sintesi strutturata salvata solo dopo conferma, con fonti, finalità, autore e retention. |
| `Workspace memory` | Indice temporaneo locale, visibile e cancellabile; massimo 7 giorni iniziali. |

Non esiste una memoria personale globale nascosta. La chat completa non si conserva per default.
Un «ricordo» duraturo diventa un normale record professionale visibile e correggibile, non uno stato
segreto del modello.

Il TTL effettivo di ogni derivato è il minimo fra TTL della memoria, retention/scadenza della
fonte, scadenza del workspace e cancellazione/restrizione applicabile. La working memory usa una
chiave per grant; la Workspace memory usa una chiave e una ACL immutabile per workspace. Un nuovo
workspace non eredita memoria precedente: può riusare soltanto sintesi confermate selezionate
esplicitamente. Alla scadenza vengono distrutti chiavi, chunk, sintesi e cache; il retention ledger
impedisce che un restore da backup li renda nuovamente visibili.

### 8.3 Prompt injection

Ogni documento, nota, email esportata e pagina web importata è **dato non fidato**, mai istruzione.

- il contenuto non può cambiare grant, system prompt, tool o policy;
- tool e output usano schemi chiusi;
- istruzioni trovate nei documenti vengono ignorate e segnalate;
- encoding specifico per UI, CSV, HTML, template, log e path;
- nessun contenuto attivo;
- confronto fra richiesta dell'utente e finalità del grant prima di ogni azione;
- corpus avversariale obbligatorio per ogni nuovo parser e adapter.

---

## 9. Cosa può fare l'AI nei moduli

L'AI è trasversale, ma la profondità dipende dallo stato del modulo.

### 9.1 Personale e paghe

- leggere documenti e proporre anagrafiche;
- controllare completezza e incoerenze;
- preparare onboarding e pratica mensile;
- collegare presenze, eventi, voci e cedolino;
- spiegare risultati del motore deterministico con fonte e periodo;
- rilevare anomalie e proporre correzioni;
- preparare comunicazioni di lavoro;
- confrontare bozze, mesi e golden case.

Non decide su lavoratori e non produce direttamente importi autoritativi.

### 9.2 Telematici e adempimenti

- preparare dati e checklist per F24, CU, 770, LIPE, UniEmens e INAIL;
- eseguire parser e controlli deterministici disponibili;
- interpretare il diagnostico ufficiale e proporre la correzione;
- verificare scadenze e allegati mancanti;
- generare una bozza/export etichettato;
- riconciliare esito, ricevuta importata e pratica.

Invio e firma restano fuori fino ai rispettivi gate; poi usano soltanto `GX` one-shot.

### 9.3 Fatturazione e contabilità

- classificare documenti e movimenti importati;
- proporre registrazioni, conti e riconciliazioni;
- collegare fatture, pagamenti, IVA e prima nota;
- segnalare duplicati, squilibri e documenti mancanti;
- preparare chiusure, report e spiegazioni;
- leggere i risultati dei motori contabili verificabili.

Le scritture definitive partono da diff reversibili e controlli di quadratura.

### 9.4 Controllo di gestione e assistenza

- preparare cruscotti, scostamenti e previsioni spiegate;
- cercare opportunità e rischi nei soli dati interni autorizzati;
- creare checklist e agenda operativa;
- rispondere nel Copilot interno citando documenti, record e regole usati;
- comporre bozze di email/PEC o richieste, senza inviarle;
- spiegare errori e guidare l'utente nel flusso manuale.

Previsioni e suggerimenti non diventano decisioni automatiche.

### 9.5 Registro di maturità per capability

Ogni capability del master deve dichiarare:

```yaml
capability_id:
module:
personal_local_state: unavailable | read | draft | reversible_write | scheduled | external_one_shot
supported_modes: [strict, guided, workspace, scheduled]
allowed_tools: []
data_classes: []
external_effects: false
required_technical_gate:
required_official_evidence:
required_professional_validation:
default_budgets:
sensitive_data_policy:
rollback_strategy:
```

La libertà dell'AI non permette di dichiarare `scheduled` o `external_one_shot` se la capability è
ancora soltanto `draft`.

---

## 10. Azioni, conferme e responsabilità

### 10.1 Matrice

| Azione | Regola |
|---|---|
| Leggere/confrontare nello scope | Nessuna conferma ripetitiva dopo il preflight. |
| Spiegare, cercare anomalie, creare checklist | Libero nel grant attivo, con fonti. |
| Creare bozza | Libero nel grant; sempre etichettata e tracciata. |
| Scrittura interna reversibile | Diff + conferma del lotto, oppure standing approval `A4` valida + transazione + undo. |
| Scrittura globale Workspace | Vietata; restringere a pratica/lotto. |
| Export sul Mac | Azione manuale esplicita e destinazione scelta dall'utente. |
| Cancellazione definitiva | Mai eseguita dal modello; procedura manuale con impatto/retention. |
| Firma | Solo futuro `GX`, documento e certificato esatti, conferma forte. |
| Email/PEC/invio telematico | Bozza di default; futuro `GX` solo dopo gate del canale. |
| Posta (email/PEC) del prodotto: lettura e triage | Modulo di prodotto (non l'agente autonomo): la casella si collega via IMAP self-service, l'AI propone soltanto urgente/pubblicità e la proposta diventa definitiva solo con conferma esplicita dell'utente. Nessun invio: restare qui non richiede un gate `GX` dedicato, che serve solo per l'invio autonomo. |
| Pagamento | Futuro `GX`, importo/beneficiario esatti e doppio controllo. |
| Decisione sul lavoratore | Proibita; l'AI può fornire dati e spiegazioni alla persona competente. |

### 10.2 Conferme senza fatica

- un solo preflight per letture e proposte dentro lo stesso mandato;
- una sola conferma per un lotto omogeneo di scritture reversibili;
- per `A4`, la standing approval vale solo entro versione, scope, campi, tool, massimo effetti e
  scadenza già mostrati; non è un permesso generale o permanente;
- checkpoint ogni 100 modifiche o prima di un cambio di effetto;
- nessun popup per tool read-only già mostrati nel preflight;
- nuova conferma solo se cambia scope, classe sensibile, destinazione o irreversibilità;
- modalità e accesso non cambiano mai in silenzio.

---

## 11. UX dell'assistente interno

### 11.1 Barra AI sempre visibile

```text
AI per questa attività      Modalità: Guidata ▼
Scope: Azienda Alfa · luglio 2026 · 8 dipendenti · 12 documenti
Può: leggere, controllare, preparare bozze · Scade: fine sessione
[Controlla e prepara] [Cambia scope] [Cosa vede l'AI] [Arresta]
```

### 11.2 Preflight semplice

```text
Leggerò: 12 documenti e 8 rapporti di Azienda Alfa, luglio 2026
Farò: controllo dati, anomalie e bozze
Non farò: invii, pagamenti o modifiche senza conferma
[Avvia]
```

`Dettagli avanzati` mostra grant, tool, budget, memoria e retention. La vista semplice rimane
comprensibile per una persona non tecnica.

### 11.3 Copilot interno

Il Copilot può ricevere istruzioni naturali, ma soltanto dentro PaghePro:

- eredita lo scope mostrato nella barra;
- trasforma una richiesta ampia in un piano visibile;
- indica eventuali dati aggiuntivi richiesti prima di chiedere l'ampliamento;
- cita fonti e risorse usate in ogni risposta importante;
- distingue fatti, calcoli deterministici, ipotesi e suggerimenti;
- non usa chat esterne e non può uscire dall'app;
- offre sempre `Continua manualmente`.

### 11.4 Drawer del lavoro

Mostra:

- fase corrente e passaggi rimanenti;
- tempo e budget consumato;
- documenti, record e moduli consultati;
- proposte, modifiche, elementi bloccati e astensioni;
- `Pausa`, `Ferma`, `Revoca`, `Continua` e `Apri audit`;
- temperatura/RAM solo quando serve a spiegare una coda locale.

L'utente può continuare a usare altre schermate durante un job locale. Hash, parser e risultati
deterministici validi vengono riutilizzati; il modello viene chiamato solo dove aggiunge valore.

---

## 12. Roadmap a gate

Nessuna fase successiva parte prima che il gate precedente sia provato.

### Gate G-1 — Adozione del nuovo perimetro

- [x] dichiarare che tutti i dati professionali interni sono eleggibili;
- [x] sostituire i limiti permanenti di tipo documento con limiti d'uso;
- [x] mantenere fuori Mac personale, app, cloud, rete e chat esterne;
- [x] definire quattro modalità e azioni critiche separate;
- [x] collegare master, profilo stretto e nuovo overlay.
- [x] registrare il nuovo ordine di precedenza in `AGENTS.md`, `PIANO.md` e `CLAUDE.md`;
- [x] marcare P-1–P6 del vecchio piano ristretto come storici e non eseguibili.

**Uscita:** questo documento è la policy di piano corrente. Nessun codice è autorizzato.

### Fase G0 — Inventario e threat model

> **Gate chiuso il 16 luglio 2026** con approvazione del fondatore. Deliverable in
> [`ai-governance/`](ai-governance/README.md): catalogo risorse, purpose + Governance Pack,
> threat model. Le basi giuridiche/DPIA restano **bozze da validare con DPO/legale e CdL prima
> del pilota con dati reali** (non bloccano G1/G2 tecnici).

- [x] censire moduli, tabelle, documenti, cache, indici, export e tool;
- [x] classificare ogni risorsa `D0-D2`, `D3A`, `D3B`, `D4` o `DX`;
- [x] censire effetti read/draft/write/schedule/external;
- [x] definire purpose code ammessi;
- [x] modellare grant troppo ampi, purpose laundering, replay, race e privilege escalation;
- [x] censire processi, entitlement, IPC, file access e supply chain;
- [x] produrre una matrice capability × modalità × gate.
- [x] creare un `GDPR/AI Governance Pack` bloccante per ogni `purposeCode`;
- [x] registrare titolare/responsabile, base art. 6, eventuale condizione art. 9 o disciplina art.
  10, categorie/interessati, necessità/proporzionalità, destinatari, retention/legal hold, diritti,
  informativa, ROPA, screening DPIA/AI Act e responsabile dell'approvazione;
- [x] approvare separatamente le policy `D3A`, `D3B` e i relativi `legalBasisRef` (bozza approvata
  dal fondatore; validazione professionale richiesta prima del pilota).

**Gate G0:** 100% delle risorse e azioni note sono nel catalogo; ogni elemento sconosciuto è
negato. Il 100% dei purpose ha un governance record approvato; un purpose mancante blocca
build/release. Nessuna risorsa `D2/D3A/D3B` entra in adapter G3, indice G4 o grant `GS` senza il
record applicabile. Threat model e Governance Pack sono approvati da sicurezza/privacy,
architettura e responsabile competente.

### Fase G1 — Fondamenta local-first

- [ ] riutilizzare PersonalDesktop/Bridge/Host/Worker separati;
- [ ] XPC/socket Unix autenticato, nessuna porta TCP;
- [ ] sandbox e assenza entitlement rete per l'intero data path;
- [ ] database e vault cifrati con Keychain/recovery;
- [ ] migrazioni e rollback senza perdita dati;
- [ ] backup locale cifrato e restore provato;
- [ ] updater separato e supply chain firmata;
- [ ] interruttore AI e modalità manuale completa.

**Gate G1:** zero file-esca fuori vault letti, zero egress, restore cifrato riuscito e nessun
processo AI con credenziali o accesso generico.

### Fase G2 — Usage Governor

> **Emendamento del 16 luglio 2026 (decisione del fondatore, opzione B del
> [rapporto G1](ai-governance/G1-RAPPORTO-FONDAMENTA.md))**: G2 è anticipato rispetto alla
> chiusura completa di G1 e viene costruito nell'app attuale con capability solo
> deterministiche/mock. Resta vincolante: **nessun modello AI viene acceso prima che
> l'isolamento G1 sia provato** per il profilo in cui il modello girerebbe.

- [x] `ProfessionalResourceCatalog` e `AiCapabilityRegistry`;
- [x] `AiUsagePolicyStore` versionato;
- [x] `AiScopeGrantRecord` autorevole + `AiScopeLease` opaca e firmata;
- [ ] `AiPolicyEvaluator` centralizzato;
- [x] query broker tipizzato e tenant-scoped;
- [x] contatori atomici e checkpoint;
- [ ] revoca, stop, resume e idempotenza — revoca e stop sono chiusi per A2 read-only;
      resume e idempotenza delle scritture restano gate A3/G5;
- [x] audit append-only/tamper-evident per grant, accessi e run A2;
- [x] UI barra, preflight, drawer e `Cosa vede l'AI`.

Si parte con `S0-S2`, `A1-A2`, senza scritture.

**Gate G2:** 100% dei run/tool/retrieval richiede un grant valido; zero bypass in test concorrenti,
property-based e fuzz; revoca e quote rispettate.

**Stato verificato 22 agosto 2026:** il verticale A2 read-only soddisfa grant obbligatorio,
tenant/scope, revoca, quota atomica, audit e UI. G2 complessivo resta aperto finché non esiste
un `AiPolicyEvaluator` unico e finché resume/idempotenza delle scritture non sono provati; il
completamento parziale non autorizza A3 né l'accensione di un provider reale.

### Fase G3 — Lettura e bozze su tutti i moduli già pronti

- [ ] adapter read/draft per ogni modulo esistente;
- [ ] Copilot interno contestuale;
- [ ] fonti e provenance in ogni risposta importante;
- [ ] modalità Stretta e Guidata universali;
- [ ] errori/astensioni e fallback manuale;
- [ ] registro di maturità per capability.

**Gate G3:** tutti gli adapter rispettano tenant e grant; l'AI spenta non cambia alcun calcolo;
ogni bozza è distinguibile da un output validato.

### Fase G4 — Inbox professionale e indice locale

- [ ] quarantena cifrata e parser sandboxati;
- [ ] import esplicito singolo/batch/cartella one-shot;
- [ ] classificazione professionale/mista/particolare/privata;
- [ ] segmentazione e minimizzazione;
- [ ] OCR/embedding locali e cifrati, con `D3A/D3B` esclusi dall'indice;
- [ ] indice trasversale con filtro pre-retrieval;
- [ ] cancellazione cascata e retention;
- [ ] modalità Workspace `S3` e accesso straordinario `S4` read-only.

**Gate G4:** zero documenti privati critici indicizzati, zero cross-scope, manifest delle fonti
uguale all'audit e cancellazione completa di tutti i derivati.

### Fase G5 — Scritture reversibili

- [ ] proposta strutturata e validazione schema;
- [ ] diff leggibile;
- [ ] conferma per lotto o standing approval `A4` limitata e a scadenza;
- [ ] transazione, idempotenza e undo;
- [ ] restrizione da Workspace a `S1/S2`;
- [ ] limiti e checkpoint batch;
- [ ] riconciliazione fra proposta, applicazione e audit.

**Gate G5:** 100% delle scritture AI ha diff, transazione e undo ed è coperta da conferma del lotto
o standing approval `A4` valida; zero scritture globali o fuori scope; ripetere la stessa richiesta
non duplica effetti.

### Fase G6 — Lavori programmati interni

- [ ] ricetta versionata con scope immutabile;
- [ ] `ScheduledRecipeAuthorization` con scadenza 30 giorni/30 run;
- [ ] selector incrementale dichiarato e versioni policy/schema/tool/modello/classificazione;
- [ ] dry-run e anteprima;
- [ ] scheduler locale con coda e budget hardware;
- [ ] pause su lock, cambio policy/versione o anomalia;
- [ ] notifiche interne e report del lavoro;
- [ ] revoca prima della tool call successiva;
- [ ] rollback delle modifiche interne.
- [ ] politica lock/sleep e opt-in separato per eventuale read/draft mentre il Mac è bloccato.

**Gate G6:** nessuna ricetta supera scope, tempo, volume o azioni; ogni variazione sospende il run;
revoca e crash recovery sono provati.

### Fase G7 — Espansione secondo il master

Attivare progressivamente:

- F4-F6: regole, paghe e controlli deterministici;
- F7: tutti i documenti professionali importati;
- F8-F9: anagrafiche, pratiche e autopilota interno;
- F10: paghe, F24, UniEmens e INAIL;
- F11-F13: fatture, contabilità e dichiarativi;
- F14: Copilot/Control Plane locale governato dai grant;
- F15: analisi e suggerimenti interni spiegabili;
- F16: soltanto adapter esterni che superano i gate e usano `GX`.

**Gate per ogni capability:** tecnica testata + evidenza ufficiale richiesta + validazione
professionale richiesta + rollout shadow. Un modulo non pronto resta `read/draft`, non viene
escluso dal progetto.

### Fase G8 — Azioni esterne one-shot, opzionali

- [ ] approvare con un piano separato `Paghe.ExternalBroker`, canale, DPIA e threat model;
- [ ] scegliere soltanto canali/provider ufficiali;
- [ ] contratto, DPA, credenziali e deleghe;
- [ ] preflight esatto payload/destinatario/importo;
- [ ] `GX` 5 minuti, single-use e anti-replay;
- [ ] autenticazione forte e separazione dei ruoli;
- [ ] ricevuta, riconciliazione e gestione errore;
- [ ] nessuna browser automation o credenziale al modello.
- [ ] testare assenza dal package local-only, SSRF, redirect, DNS rebinding, host/certificato,
  payload alterato, replay, doppio invio e impossibilità di leggere il vault.

**Gate G8:** ogni variazione invalida il token; zero invii duplicati; ricevuta e audit completi;
approvazione professionale/ufficiale del singolo flusso. Questa fase non è necessaria per rendere
utile l'AI locale.

### Fase G9 — Shadow e pilota

G9 è un workstream continuo e parte con G3. Ogni coppia `capability + modalità` completa i propri
tre cicli shadow prima di passare allo stato successivo del registro di maturità. G8 richiede anche
uno shadow specifico del singolo canale `GX`; il report finale aggrega prove già completate e non
rimanda tutta la validazione alla fine.

- [ ] almeno tre cicli completi per ogni capability+modalità;
- [ ] confronto con golden case e professionista;
- [ ] test avversariali di scope, injection, revoca e privacy;
- [ ] misurazione tempo risparmiato e fatica delle conferme;
- [ ] run AI e manuale confrontabili;
- [ ] incident response e rollback provati.

**Gate G9:** zero incidenti P0/P1, zero accessi esterni/cross-scope, differenze monetarie spiegate e
trasformate in test, utenti non tecnici comprendono scope e azioni prima dell'avvio.

---

## 13. Test quantitativi obbligatori

### 13.1 Grant e tenant

- `100%` di run, retrieval e tool call legati a grant valido;
- `100%` dei purpose con governance record approvato; purpose mancante blocca build/release;
- `0` successi con token scaduto, revocato, alterato, replay o wrong tenant/purpose/resource;
- almeno `10.000` tentativi cross-tenant/cross-azienda/cross-periodo/cross-classe con `0` successi;
- `0` accessi oltre quota tramite paginazione, concorrenza o retry;
- nuove letture bloccate entro 1 secondo dalla revoca;
- job e cache fermati/distrutti entro 5 secondi dalla revoca;
- `100%` delle nuove risorse non catalogate negate;
- lock, sleep, logout e cambio utente revocano il grant; il vecchio nonce è sempre respinto dopo
  unlock;
- idle e TTL assoluto verificati per Stretta, Guidata, Workspace, S4, `GS` e run programmati;
- `10.000` tentativi di leggere policy, grant, nonce, audit grezzo, secret store o retention ledger
  restituiscono `0` risultati; nessun tool model-facing referenzia lo schema del control plane.

### 13.2 Mac e rete

- file-esca in Desktop, Documenti, Download, iCloud, Mail e Foto: `0 open/stat/read`;
- test symlink, alias, path traversal e TOCTOU: `0` bypass;
- `0` entitlement network client/server nell'intero process tree;
- `0` socket o byte esterni durante avvio, import, OCR, indice, Copilot, errore, crash e shutdown;
- nessun fallback cloud quando il modello locale è indisponibile.

### 13.3 Intake e dati sensibili

- corpus minimo: 1.000 documenti professionali, 1.000 negativi, 200 misti/D3A/D3B/injection;
- `0` file privati critici oltre quarantena o disponibili al retrieval;
- falso «sicuro» con upper bound statistico concordato non superiore allo 0,5%;
- senza `GS`: `0` campi `D3A/D3B` restituiti;
- `0` emissioni `GS` senza ri-autenticazione, conferma separata, purpose e `legalBasisRef`;
- con `GS-X`: soltanto record/campi esatti, TTL 5 minuti/una operazione e `0` confronti;
- con `GS-W`: soltanto lotto/campi minimizzati della policy approvata, TTL 30 minuti/checkpoint e
  `0` rinnovi automatici o confronti cross-lavoratore;
- `0` record, chunk, embedding o sintesi `D3A/D3B` negli indici e nelle memorie Workspace;
- corpus di almeno 500 segreti sintetici, incluse varianti spezzate, Unicode e allegati: `0` D4 in
  prompt, OCR indicizzato, chunk, embedding, memoria, audit o export AI;
- accesso diretto `D4` sempre negato e nessun override dell'utente;
- `0` CF, IBAN, salute, password o testo privato in log, trace, crash e audit payload;
- cancellazione cascata di originale, OCR, chunk, embedding, sintesi e cache verificata.

Un singolo documento privato critico indicizzato blocca la release.

### 13.4 Azioni e injection

- corpus critico di prompt injection: `100%` incapace di ampliare grant o invocare tool vietati;
- `100%` delle scritture con schema, diff, transazione, audit e undo, coperte da conferma del lotto
  o standing approval `A4` ancora valida;
- `0` scritture `S3/S4` globali;
- replay della stessa richiesta: `0` duplicazioni;
- task programmato sospeso al cambio di policy, modello, schema, classificazione, ricetta o regola;
- autorizzazione programmata scaduta a 30 giorni/30 run; nuovi record nel selector ammessi, record
  fuori selector negati, cambio schema/classe/scope sospende e lock policy verificata;
- futuro `GX`: azione/payload/destinatario esatti, single-use, scadenza 5 minuti e `0` replay;
- futuro broker: assente dal package local-only; SSRF, redirect, DNS rebinding, host/certificato
  errato, payload alterato e replay sempre bloccati, con `0` letture del vault e `0` invii doppi;
- nessuna decisione automatica o ranking dei lavoratori.

### 13.5 Audit e memoria

- `100%` delle risorse viste riconciliabile con grant e run;
- `100%` di accessi `S4`, `GS`, scritture e approvazioni presenti nell'audit;
- manomissione, riordino o buco nella catena sempre rilevato;
- working memory cancellata entro 60 secondi da chiusura/revoca;
- nessun recupero da conversazione, pratica o tenant privo di grant;
- nessun prompt/chat integrale conservato per default;
- nessun dato dell'utente in training o benchmark;
- scadenza autonoma provata a 24 ore e 7 giorni, cancellazione fonte prima del TTL, nuovo
  grant/workspace, legal hold e restore da backup;
- `0` recuperi se non coincidono grant e workspace; retention ledger impedisce la resurrezione.

### 13.6 Uso reale e prestazioni

- `100%` dei run mostra modalità, scope e capacità prima dell'avvio;
- p95 apertura/preflight sotto 2 secondi; elaborazione pesante in background;
- `100%` dei checkpoint è riprendibile senza perdita o duplicazione;
- almeno 90% degli utenti non tecnici sceglie correttamente la modalità nei test;
- `0` conferme ripetitive per letture già autorizzate;
- calcoli deterministici identici con AI accesa o spenta;
- benchmark RAM, CPU/GPU, temperatura, secondi/pagina e durata batteria sul Mac target;
- fallback manuale disponibile in ogni flusso fondamentale.

---

## 14. Rischi residui e contromisure

| Rischio | Contromisura |
|---|---|
| L'utente concede spesso scope troppo ampio | Guidata predefinita, Workspace breve/read-only, barra visibile, report d'uso e revoca. |
| Troppi popup spingono verso chat esterne | Preflight unico, conferme per lotto, checkpoint morbidi e UX a un clic. |
| Un file privato viene importato per errore | Quarantena, classifier locale, anteprima e astensione sui dubbi. |
| L'indice globale amplia il danno potenziale | Cifratura, partizioni, filtro pre-retrieval, chiavi-oggetto e cancellazione cascata. |
| Prompt injection contamina una proposta | Contenuto come dato, tool esterni al modello, schemi chiusi e corpus avversariale. |
| Il modello riversa troppo archivio nel contesto | Query broker, relevance filter, paginazione, limite byte/record e manifest. |
| Il grant diventa obsoleto | Controllo policy/schema/classificazione/recipe e checkpoint a ogni tool call. |
| Le quote vengono aggirate | Contatori atomici gerarchici per grant, run e risorsa. |
| Una scrittura è errata | Diff, validazione, transazione, idempotenza, checkpoint e undo. |
| Una ricetta programmata continua su dati cambiati | Version pin, stop-on-change, dry-run e riapprovazione. |
| Dati D3A/D3B generano inferenze sui lavoratori | Grant esatto, niente memoria/ranking/confronto e output minimizzato. |
| L'AI sembra più autorevole del motore | Fonti, distinzione fatti/ipotesi, calcolo deterministico e bozza da validare. |
| Il Mac non regge il carico | Coda, streaming, benchmark, budget hardware e split automatico dei job. |
| Il sistema operativo/utente root è compromesso | FileVault, OS aggiornato, account non-admin e supply chain firmata come prerequisiti. |

---

## 15. Riconciliazione con il piano ristretto precedente

Le seguenti limitazioni non sono più l'identità permanente del prodotto:

- unico documento UNILAV;
- unica azienda o massimo fisso di dipendenti;
- unico CCNL come limite definitivo;
- nessun indice semantico;
- nessun assistente conversazionale interno;
- rinvio permanente di fatturazione, contabilità, dichiarativi e controllo di gestione.

Restano utili come **primo corpus tecnico o modalità Stretta**, non come confine finale.

Restano invece integralmente validi:

- architettura locale e separazione dei processi;
- assenza di rete/cloud/chat esterne;
- importazione volontaria e nessuna scansione del computer;
- sandbox, cifratura, recovery, backup e cancellazione;
- tenant isolation e default deny;
- calcoli deterministici e regole versionate;
- output da validare e gate professionali/ufficiali;
- interruttore AI e fallback manuale;
- audit e test avversariali.

---

## 16. Fonti e criterio di conformità

Il modello di grant applica in concreto limitazione della finalità, minimizzazione e protezione per
impostazione predefinita. Non vieta tutti i dati professionali: limita quelli realmente usati da
ogni attività. È coerente con il principio tecnico del minimo privilegio, secondo cui un processo
riceve soltanto risorse e autorizzazioni necessarie al compito.

Fonti ufficiali consultate il 15 luglio 2026:

- [Regolamento (UE) 2016/679 — GDPR, artt. 5, 22 e 25](https://eur-lex.europa.eu/legal-content/IT/TXT/?uri=CELEX%3A32016R0679)
- [Regolamento (UE) 2024/1689 — AI Act](https://eur-lex.europa.eu/eli/reg/2024/1689/oj/ita)
- [NIST — least privilege](https://csrc.nist.gov/glossary/term/least_privilege)
- [NIST SP 800-53 Rev. 5 — AC-6](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)

Questo piano non determina da solo base giuridica, retention, ruolo privacy, DPIA, qualificazione
AI Act o liceità di uno specifico trattamento. Questi elementi devono essere verificati sul caso
reale da DPO/legale e professionista di dominio prima del pilota.

---

## 17. Verdetto del panel

**APPROVATO COME PIANO** da architettura/fattibilità, sicurezza/GDPR e scope/UX.

Il panel ha verificato che:

- l'AI non è più limitata artificialmente a un tipo di documento o modulo;
- l'accesso ampio resta interno, locale, temporaneo, misurato e revocabile;
- la modalità Guidata riduce la fatica delle conferme;
- Workspace è ampio ma read-only e breve;
- dati particolari e azioni esterne richiedono grant separati;
- i calcoli e i gate di dominio non vengono sostituiti dal modello;
- test e roadmap rendono verificabile ogni confine.

Le due tornate di revisione hanno corretto: precedenza delle fonti operative, separazione
`GrantRecord/Lease`, schema delle policy, governance `D3A/D3B`, filtro dei segreti, esclusione del
control plane, scadenza/revoca dei grant, standing approval limitate, memoria/indice, broker
esterno separato, shadow continuo e UX senza conferme ripetitive. Nell'ultima revisione tutti e tre
i revisori hanno restituito `ok`; non restano finding alti o medi aperti.

L'approvazione riguarda il piano, non certifica software, calcoli, privacy o uso reale e non
autorizza implementazione.

---

## 18. Decisione dei tre ruoli post-lavoro

| Ruolo | Decisione | Motivo | Prossima azione |
|---|---|---|---|
| Controllore Consegna | **APPROVATO CON NOTA DI ATTRIBUZIONE** | Le modifiche di questa consegna sono solo documentali/governance. Il worktree contiene codice firma/API concorrente, estraneo al piano. | Consegnare i documenti senza attribuire o modificare quel codice. |
| Direttore Prossimo Incarico | Prima eventuale attività: **solo G0**. | Catalogo, Governance Pack e threat model devono precedere fondazioni e Governor. | Dopo richiesta separata, chiudere G0; soltanto dopo pianificare G1/G2. |
| Ricercatore Mercato/GAP | **Nessuna nuova integrazione ora.** | Il gap prioritario è verificare che grant e modalità siano chiari senza rallentare il lavoro. | Testare l'UX Guidata; rinviare cloud, connettori e `GX`. |

Esperimento consigliato: prototipo locale con 6-8 operatori non tecnici su cinque compiti
(Stretta, Guidata, Workspace, Programmata e revoca). Gate: almeno 90% sceglie correttamente
modalità/scope, avvio entro 30 secondi, zero concessioni accidentali `S3/S4`, massimo una conferma
inutile per compito e 100% degli utenti capace di capire, fermare e revocare il lavoro AI.

---

## 19. Scelta finale

La configurazione consigliata è:

```text
Default quotidiano: GUIDATA
Intervento puntuale o dato particolare: STRETTA
Analisi trasversale scelta dall'utente: WORKSPACE read-only
Controlli ripetitivi già provati: PROGRAMMATA
Effetto esterno futuro: GX esatto, 5 minuti, uso singolo
```

Quindi PaghePro non limita **quali lavori professionali** l'AI può aiutare a svolgere. Limita
**quanto potere riceve in una singola esecuzione**. È il compromesso che mantiene alta l'utilità
senza trasformare il software in un osservatore permanente del computer.
