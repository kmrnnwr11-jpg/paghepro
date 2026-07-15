# PaghePro Personale — Piano ristretto privacy-first

> Versione: 15 luglio 2026
> Stato: **solo piano; nessuna implementazione autorizzata**
> Scopo attuale: baseline di sicurezza locale e definizione della modalità **Stretta**. La policy
> operativa generale è ora
> [`PIANO-AI-LIBERA-LIMITI-USO.md`](PIANO-AI-LIBERA-LIMITI-USO.md): l'AI può lavorare su tutti i
> dati professionali dentro PaghePro, con limiti di scopo, durata, quantità e azioni.

`Personale` significa **un solo operatore e nessun accesso alle sue app private**. Non significa
uso domestico: quando contiene dati di dipendenti o clienti, restano applicabili privacy, sicurezza
e responsabilità professionali previste per quel trattamento.

Questo documento non è più il limite funzionale generale del prodotto. Conserva l'architettura
privacy-first, i divieti verso Mac/app personali/rete/cloud e il preset più prudente. In caso di
conflitto sul tipo di documento, sui moduli o sull'ampiezza professionale prevale il nuovo piano
dei limiti d'uso; il master
[`PIANO-MASTER-AI-AUTONOMO.md`](PIANO-MASTER-AI-AUTONOMO.md) resta il catalogo delle capacità e dei
gate di dominio.

> **Nota di lettura:** nei capitoli seguenti, `solo UNILAV`, `nessun indice semantico`, `nessuna
> chat interna`, `un solo CCNL` e gli altri limiti funzionali descrivono esclusivamente il vecchio
> pilot ristretto/modalità Stretta. Non prevalgono sulla policy generale attuale.

---

## 0. Risposta diretta

Sì: il progetto migliora molto se viene limitato. PaghePro non deve diventare un agente generale
che osserva il computer. Deve essere un **programma paghe personale con piccoli assistenti
specializzati**.

La regola principale è:

```text
PaghePro vede solo ciò che la persona seleziona dentro PaghePro.
Non esplora il computer, non entra in altre app e non invia dati a chat esterne.
```

Il primo prodotto non avrà un chatbot libero. Avrà operazioni guidate:

- `Carica un documento paghe`;
- `Controlla se è pertinente`;
- `Proponi i campi da inserire`;
- `Mostra cosa manca`;
- `Calcola con il motore verificabile`;
- `Spiega questo errore`;
- `Prepara una bozza da validare`.

Se l'AI locale non è disponibile o non è sicura del risultato, il programma torna al modulo
manuale. Nel profilo personale non usa alcun servizio AI cloud.

---

## 1. Preset storico della modalità Stretta — non è la policy globale

I punti 7 e 8 e ogni altro limite di tipo documento/modulo qui sotto valgono soltanto per il pilot
storico Stretto. I confini verso Mac, app personali, rete e cloud restano invarianti; l'ampiezza
professionale segue invece il piano AI con limiti d'uso.

1. **Una sola cassaforte di lavoro:** un proprietario, un accesso locale e nessun portale esterno
   nella prima versione.
2. **Importazione esplicita:** il programma vede un file soltanto dopo che l'utente lo sceglie con
   il selettore file di PaghePro.
3. **Nessuna scansione del computer:** niente accesso automatico a Desktop, Download, Documenti,
   cartella personale, dischi esterni o cartelle di rete.
4. **Nessuna chat esterna:** niente ChatGPT web, chatbot di terzi o copia automatica dei documenti
   verso servizi AI online.
5. **AI esclusivamente locale nella prima versione:** OCR e modello operano sul Mac;
   se non possono operare, la funzione si ferma o diventa manuale.
6. **Rete bloccata per default:** nessun collegamento a email, PEC, Drive, WhatsApp, banca, social,
   browser, calendario o altre applicazioni.
7. **Solo documenti paghe ammessi:** una lista chiusa decide i tipi accettabili. Un file non
   pertinente non viene indicizzato né conservato.
8. **Nessuna memoria generale:** ogni pratica e dipendente ha un contesto separato; l'AI non può
   cercare liberamente in tutto l'archivio.
9. **Nessuna azione esterna:** l'AI non invia, firma, paga, cancella o trasmette. Prepara soltanto
   bozze e controlli locali.
10. **Interruttore totale:** il proprietario può spegnere l'AI; calcoli, archivio ed esportazione
    devono continuare a funzionare senza modello.

Questi limiti sono controlli nel sistema operativo, nel backend e nella rete, non semplici frasi
nel prompt.

---

## 2. Perimetro dei dati

### 2.1 Fonti consentite nella prima versione

| Fonte | Accesso | Condizione |
|---|---|---|
| Dati digitati nei moduli PaghePro | consentito | soltanto campi del flusso paghe corrente |
| File scelto con `Carica documento` | temporaneo | analisi locale in area isolata; salvataggio solo dopo pertinenza e conferma |
| Record già salvati nella pratica corrente | consentito | filtro `tenantId`, azienda, dipendente e periodo applicato dal server |
| Regole normative installate in PaghePro | sola lettura | pacchetto versionato, fonte e periodo verificabili |
| Output creati da PaghePro | consentito | etichetta di bozza finché manca validazione prevista |
| Backup scelto dall'utente | scrittura cifrata | solo volume locale/rimovibile non sincronizzato, con restore provato |

### 2.2 Fonti tecnicamente vietate

| Fonte | Stato iniziale | Come viene bloccata |
|---|---|---|
| Intero disco o cartella personale | vietata | nessun permesso filesystem generale |
| Desktop, Download e Documenti | vietata | nessun watcher o scansione in background |
| Email, PEC e calendari | vietata | nessun connettore e nessun token OAuth |
| Chat, WhatsApp, Teams, Slack e social | vietata | nessuna integrazione |
| Google Drive, OneDrive, Dropbox e iCloud | vietata | nessun SDK o credenziale |
| Browser, cronologia, cookie e password | vietata | nessuna estensione o automazione browser |
| Clipboard e ciò che viene digitato in altre app | vietata | nessun listener globale |
| Foto, contatti, microfono e videocamera | vietata | permessi non richiesti |
| Software paghe o contabili di terzi | vietata | nessun accesso diretto; solo export scelto manualmente |
| Conti bancari e carte | vietata | nessun open banking o scraping |
| Internet e ricerca web | vietata all'AI | firewall in uscita `default deny` |
| Modelli AI cloud | vietata | nessuna chiave provider installata nel profilo personale |

### 2.3 Pilot storico: un solo tipo documentale iniziale

Il primo e unico tipo letto dall'AI nell'MVP è **UNILAV relativo al rapporto da registrare**. Le
presenze vengono inserite nel modulo PaghePro o importate da un unico CSV con parser deterministico;
non sono un secondo documento AI.

Restano nel backlog, tecnicamente disabilitati fino a una nuova approvazione:

- lettera/contratto di assunzione;
- cartellino o prospetto presenze non strutturato;
- prospetto delle variabili mensili;
- cedolino precedente;
- visura/anagrafica azienda;
- diagnostico prodotto da un validatore ufficiale.

Rimangono fuori dalla prima versione:

- documenti personali generici;
- fotografie e documenti d'identità completi;
- corrispondenza privata;
- estratti conto;
- cartelle sanitarie e referti;
- archivi compressi contenenti file eterogenei;
- intere caselle email o cartelle del computer;
- qualsiasi tipo non presente nella lista approvata.

Per assenze, malattia, infortunio o altri dati particolari si progetterà in seguito un flusso
specifico che acquisisca solo i campi strettamente necessari. Il sistema non deve conservare un
referto medico perché contiene un dato utile alla paga.

Anche un UNILAV formalmente ammesso può avere allegati, note o pagine non necessarie. Il
`PurposeManifest` autorizza pagine, zone e campi, non l'intero documento in modo indiscriminato.
Il classificatore locale in quarantena si astiene se trova una categoria non prevista. Quando
possibile viene conservata una copia derivata minimizzata/redatta; l'originale completo resta solo
se una finalità e un obbligo documentati lo richiedono. Un dato particolare fuori schema blocca
l'acquisizione e non viene trasmesso all'estrattore.

---

## 3. Architettura personale consigliata

### 3.1 Forma del programma

La prima scelta consigliata è una **applicazione locale**:

```text
Paghe.PersonalDesktop — WebView + asset incorporati, nessuna rete
        ↓ bridge nativo/XPC tipizzato e autenticato
Paghe.PersonalHost .NET — socket Unix, nessuna porta TCP
        ↓
database cifrato + cassaforte documenti cifrata
        ↓
Paghe.PersonalWorker — socket locale, senza rete

Rete in uscita: BLOCCATA, salvo aggiornamento manuale scelto dall'utente
```

Si riusano il dominio .NET e i componenti React necessari. Non si ricostruiscono i motori, ma non
si espone il backend come sito locale: il pacchetto personale non apre porte TCP/UDP.

Un server privato o un altro sistema operativo richiedono un piano successivo; non sono varianti
configurabili della prima versione.

#### Target tecnico del primo rilascio

Per evitare una promessa multipiattaforma non verificabile, il primo rilascio pianifica **macOS su
Apple Silicon**, coerente con la postazione attuale: sistema ancora supportato e aggiornato,
FileVault attivo, account quotidiano non amministratore, almeno 16 GB di RAM e inizialmente almeno
50 GB liberi, poi sostituiti dal preflight dimensionale P0. Se il
computer di destinazione è diverso, G0/G1 devono riaprire la scelta e nessuna garanzia di sandbox viene
riutilizzata automaticamente.

Il pacchetto è una app firmata e notarizzata con App Sandbox, composta nello stesso repository da:

- `Paghe.PersonalDesktop`: shell WebView dedicata, senza estensioni, sincronizzazione, autofill,
  remote debugging o navigazione web; carica un entrypoint/build React personale con route e menu
  in allowlist tramite schema locale/WKURLSchemeHandler, non l'intera SPA nascosta da feature flag;
- `Paghe.PersonalBridge`: piccolo bridge nativo con messaggi tipizzati e allowlisted; valida firma,
  audit token XPC e nonce per avvio, poi inoltra comandi al socket Unix del servizio .NET;
- `Paghe.PersonalHost`: composition root .NET separata e minimale; riusa Core/Persistence e i soli
  command handler del vertical slice, ma non referenzia SDK AI cloud, Google login, SdI,
  notifiche, firma, open banking o altri connettori;
- `Paghe.PersonalWorker`: OCR/parser/modello locale, non pubblicato come servizio di rete;
- `Paghe.PersonalUpdater`: processo separato, eseguito solo su richiesta e privo di accesso o mount
  alla cassaforte.

Non si crea un fork del prodotto: i motori deterministici restano condivisi, mentre host,
dipendenze e pacchetto personale sono separati per costruzione. G0/G1 includono un prototipo firmato
dei permessi macOS; se App Sandbox, helper e WebView non rispettano i confini, il gate fallisce.

### 3.2 Dati e isolamento

- Una sola area visibile, denominata `Cassaforte Paghe`.
- `tenantId` resta obbligatorio nel codice anche con un solo proprietario: protegge le query e
  permette una futura migrazione senza rimuovere un controllo già esistente.
- Ogni accesso richiede sempre `tenantId`, `purposeId` e gli ID esatti delle risorse. Azienda,
  dipendente e periodo sono obbligatori soltanto quando lo schema di quel purpose li prevede; non
  vengono creati valori fittizi e non esiste `cerca ovunque`.
- Documenti originali e campi estratti sono separati.
- Nel primo prodotto non esistono indice semantico né embedding: parser/OCR lavorano sul singolo
  file e le query strutturate sulla pratica corrente.
- Le chiavi di cifratura non sono salvate insieme al database e usano la cassaforte credenziali del
  sistema operativo quando disponibile.
- API e worker sono sandboxati con mount esclusivo su cassaforte e quarantena; non vedono la home.
- Il selettore trasferisce il contenuto del singolo file autorizzato: il backend non accetta un
  percorso arbitrario del computer come istruzione di lettura.
- Ogni operazione AI richiede un `PurposeManifest` creato dal backend: finalità, utente, tenant,
  risorse tipizzate, documenti/pagine/zone, campi, tool, limite quantitativo e scadenza.
- Il modello riceve il manifesto ma non può modificarlo; un dato fuori manifesto viene negato dal
  server e il tentativo entra nell'audit.

### 3.3 AI locale

L'AI personale è composta da moduli piccoli:

- OCR locale;
- classificatore di pertinenza;
- estrattore strutturato per ciascun tipo documentale;
- spiegatore di un diagnostico già prodotto;
- rilevatore di anomalie sui dati della pratica corrente.

Non viene creato un agente generale con accesso a terminale, filesystem, browser, SQL o rete. I
componenti locali e le relative licenze devono essere verificati prima dell'adozione; niente codice
AGPL/GPL nel SaaS o nella distribuzione senza decisione esplicita.

Se l'hardware non raggiunge tempi e accuratezza minimi:

1. si usa il parser deterministico disponibile;
2. si mostra il modulo manuale;
3. si segnala che l'AI locale non è disponibile;
4. non si attiva silenziosamente un provider esterno.

### 3.4 IPC, assenza di rete e aggiornamenti

- Gli asset React sono nel bundle e usano uno schema locale; `connect-src 'none'` e navigation
  policy negano HTTP/HTTPS.
- `PersonalDesktop`, Bridge, Host e Worker non possiedono entitlement macOS `network.client` o
  `network.server` e non aprono socket TCP/UDP.
- La UI invoca soltanto messaggi nativi tipizzati; il Bridge autentica client e sessione tramite
  firma/audit token/nonce e comunica con Host/Worker via XPC/socket Unix con permessi stretti.
- `PersonalWorker`, OCR, parser PDF e runtime modello non possiedono rete.
- Nessun runtime esegue model pull, controllo licenza online, sample upload o cloud protection.
- Le destinazioni Internet non sono configurabili dal modello.
- Il profilo personale fallisce l'avvio se trova binding pubblico, URL di provider cloud, API key
  esterne, entitlement di rete o un qualunque processo dati con socket Internet disponibile.
- IPv4, IPv6, DNS, UDP, proxy di sistema e condizioni di crash/primo avvio sono inclusi nei test
  egress attribuiti all'intero process tree PaghePro.
- Aggiornamenti del software e pacchetti normativi vengono scaricati manualmente dall'utente o da
  un updater separato, firmato e disattivabile.
- Prima dell'installazione vengono mostrati versione, fonte, checksum e modifiche.
- Telemetria, crash report online e analytics non fanno parte della prima versione. La diagnostica
  resta locale ed esportabile manualmente dopo mascheramento.

---

## 4. Pilot storico Stretto: niente chatbot generale, UX a compiti

### 4.1 Schermate minime

#### Home

Mostra al massimo quattro azioni principali:

1. `Prepara il mese`;
2. `Carica il documento previsto`;
3. `Controlla le bozze`;
4. `Backup e sicurezza`.

Sotto le azioni mostra soltanto:

- pratiche aperte;
- documenti da confermare;
- dati mancanti;
- errori da correggere;
- bozze pronte da controllare;
- ultimo backup riuscito.

Gli stati visibili sono quattro: `Da completare`, `Pronto da controllare`, `Bloccato` e
`Completato`. Ogni schermata ha una decisione principale e non più di cinque azioni visibili.

#### Importa documento

Un pulsante apre il selettore file. La pagina spiega prima dell'importazione:

- quali tipi sono ammessi;
- cosa verrà letto;
- quali campi verranno proposti;
- dove resterà il file;
- come cancellarlo.

#### Revisione documento

Visualizzazione affiancata:

- pagina originale a sinistra;
- campi proposti a destra;
- evidenziazione della posizione da cui arriva ogni valore;
- livello di confidenza;
- `Conferma`, `Correggi` o `Rifiuta e cancella`.

#### Pratica paghe

Checklist guidata, senza chat libera:

1. dati anagrafici;
2. rapporto e regola applicata;
3. presenze e variabili;
4. dati mancanti;
5. calcolo deterministico;
6. differenze dal periodo precedente;
7. bozza e controlli;
8. esportazione manuale.

#### Cosa ha visto l'AI

Per ogni operazione mostra:

- documento e sole pagine usate;
- campi letti e motivo;
- risultato proposto;
- dati salvati e dati scartati;
- autore della conferma;
- pulsante per cancellare i dati derivati consentiti.

Versione del modello/parser, hash, tempi e diagnostica di rete restano sotto `Dettagli avanzati`:
sono disponibili per l'audit ma non ostacolano il flusso normale.

#### Privacy e accessi

La vista normale mostra in linguaggio semplice `Funziona solo su questo computer`, `Non legge altre
cartelle`, stato AI e ultimo backup. Sotto `Dettagli avanzati` mostra:

- AI locale attiva/disattiva;
- rete AI bloccata;
- cartelle autorizzate: sempre `nessuna` nella prima versione;
- integrazioni esterne: sempre `nessuna`;
- spazio occupato e retention;
- esporta audit;
- cancella cache AI;
- `Arresta subito AI`.

### 4.2 Spiegazioni limitate

`Spiega questo errore` riceve soltanto:

- il diagnostico selezionato;
- i campi strettamente collegati;
- le regole versionate pertinenti;
- nessun altro dipendente o documento.

La risposta non viene conservata come memoria generale. Se diventa una decisione della pratica,
viene salvata come nota con fonte e conferma della persona.

---

## 5. Flusso sicuro per leggere un documento

```text
1. La persona seleziona un singolo file
2. PaghePro lo copia nella quarantena cifrata con una chiave effimera per quel file
3. Controlla magic/MIME, dimensione e struttura in parser sandboxato, senza contenuto attivo
4. Legge localmente il minimo necessario per classificare pertinenza e categorie impreviste
5a. Non pertinente/incerto → mostra motivo e distrugge subito chiave e copia temporanea
5b. Pertinente → seleziona solo pagine/zone/campi del PurposeManifest e mostra l'anteprima
6. La persona autorizza l'estrazione
7. Parser/AI locale propone campi con provenienza
8. La persona conferma o corregge
9. Solo i dati confermati entrano nella pratica
10. Audit registra il processo senza copiare il contenuto sensibile nei log
```

Regole obbligatorie:

- default `rifiutato`, non default `accettato`;
- soglia alta non significa salvataggio automatico;
- un documento incerto richiede scelta umana;
- un file scelto viene necessariamente letto in quarantena quanto basta per classificarlo; la
  garanzia è che PaghePro non cerca file da solo, non invia nulla e non conserva ciò che rifiuta;
- testo nel documento è sempre dato, mai istruzione per l'agente;
- frasi come “ignora le regole” dentro un PDF non possono cambiare tool o permessi;
- non vengono creati indice semantico o embedding nemmeno dopo l'accettazione nell'MVP;
- quarantena e cache usano solo directory cifrate dell'app, mai temp generici del sistema;
- nessuna macro o contenuto attivo viene eseguito; pagine, decompressione, RAM e tempo hanno limiti;
- ogni campo estratto ha tipo, lunghezza, caratteri/valori ammessi e normalizzazione Unicode;
- la UI usa escaping React e vieta HTML non sanificato; template/PDF applicano encoding per sink;
- CSV neutralizza formule che iniziano con `=`, `+`, `-`, `@`, tab o ritorno a capo;
- nomi file e percorsi sono generati dal server; testo documentale non entra in path o log;
- in caso di rifiuto la crypto-erasure è immediata; dopo crash/reboot la pulizia termina entro lo
  SLA registrato in P0;
- il file originale non viene alterato;
- hash e provenienza collegano ogni campo al file confermato;
- l'originale accettato viene conservato soltanto con finalità documentata; altrimenti resta la
  copia derivata minimizzata necessaria alla pratica.

---

## 6. Cosa può e non può fare l'AI

| Attività | Stato nel prodotto personale | Controllo |
|---|---|---|
| Classificare fra tipi ammessi | consentita come proposta | conferma o regola deterministica |
| Estrarre campi da documento scelto | consentita come proposta | provenienza + conferma |
| Trovare campi mancanti | consentita | schema deterministico |
| Confrontare due periodi della stessa pratica | consentita | query server-side limitata |
| Spiegare diagnostico selezionato | consentita | fonti installate e contesto minimo |
| Preparare checklist interna | consentita | nessuna azione esterna |
| Calcolare importi/aliquote | vietata all'AI | solo motore `decimal` versionato |
| Inventare una regola mancante | vietata | blocco e richiesta dato/fonte |
| Cercare su Internet | vietata | nessun tool e rete negata |
| Leggere altre cartelle/app | vietata | nessun permesso/tool |
| Cercare in tutti i dipendenti | vietata | scope obbligatorio della pratica |
| Addestrarsi sui documenti dell'utente | vietata | nessun training/telemetria |
| Inviare email, PEC o messaggi | vietata | nessun connettore |
| Firmare, pagare o trasmettere | vietata | export manuale soltanto |
| Cancellare archivi | vietata all'AI | azione proprietario con conferma forte |

---

## 7. Funzioni del prodotto ristretto

### 7.1 Incluse nell'MVP

Il vertical slice è congelato su **un proprietario, una azienda, un CCNL, da 1 a 10 dipendenti
standard, un periodo mensile per volta, un formato CSV presenze, UNILAV come unico documento AI e
un export manuale**. Una seconda azienda, un altro CCNL, un altro documento o un caso non standard
restano fuori anche se il codice esistente sembra già supportarli.

`Standard` significa inizialmente: rapporto subordinato a tempo indeterminato, full-time, retribuito
mensilmente, attivo per tutto il mese, senza eventi o variabili. Sono bloccati: assunzione/cessazione
nel mese, part-time, tempo determinato, apprendistato, dirigenti, lavoro intermittente, straordinari,
premi, trasferte, fringe benefit, malattia, infortunio, maternità, ferie/permessi, aspettative,
trattenute sindacali, pignoramenti, più rapporti, arretrati, conguagli e mese di dicembre/fine anno.
G0 trasforma questo elenco storico in una matrice positiva/negativa aggiornata; un caso non
classificato è fuori,
non viene interpretato liberamente dall'AI.

- accesso locale del proprietario;
- cassaforte cifrata per documenti paghe selezionati;
- anagrafica azienda e dipendente essenziale;
- importazione esplicita dei soli tipi approvati;
- revisione affiancata e provenienza campo-per-campo;
- presenze tramite griglia manuale o unico CSV con parser deterministico;
- motore paghe deterministico per un CCNL e casi standard;
- snapshot immutabile del calcolo;
- bozza cedolino/LUL etichettata da validare;
- confronto con periodo precedente;
- elenco dati mancanti e anomalie;
- spiegazione locale di un errore selezionato;
- audit leggibile dalla persona;
- export manuale della pratica;
- backup cifrato e ripristino provato;
- interruttore AI e funzionamento manuale.

### 7.2 Incluse soltanto dopo l'MVP locale

- generazione bozza F24/UniEmens per il perimetro validato;
- esecuzione locale di validatori ufficiali installati manualmente;
- import da un solo formato del software precedente;
- pacchetto di esportazione pronto per caricamento manuale;
- gestione minimale di malattia/infortunio con dati ridotti;
- proposta di un secondo tipo documentale soltanto dopo P6; non viene abilitato dall'MVP.

### 7.3 Rinviate senza data

- multiutente e ruoli di studio;
- portale azienda/dipendente;
- email, PEC, SMS, WhatsApp e calendario;
- Google Drive, Microsoft 365 e altri cloud documentali;
- invio reale a INPS, INAIL, Agenzia Entrate o SdI;
- fatturazione elettronica reale;
- firma digitale;
- banca, pagamenti e riconciliazione;
- contabilità completa e dichiarativi annuali;
- Tax Opportunity Engine e CFO;
- assistenza clienti autonoma;
- RAG web o ricerca normativa libera;
- marketplace e plugin di terze parti;
- modello AI cloud;
- app mobile con accesso a foto/contatti;
- espansione a più CCNL o più studi.

### 7.4 Escluse per principio

- scansione silenziosa di file o comunicazioni;
- sorveglianza del lavoratore;
- profilazione emotiva o comportamentale;
- decisioni automatiche su assunzione, licenziamento, disciplina o valutazione;
- credenziali di enti consegnate al modello;
- automazione browser dei portali pubblici;
- azioni esterne senza conferma e prova;
- vendita o riuso dei dati per addestrare modelli.

### 7.5 Filtro anti-allargamento

Una futura funzione entra nel profilo personale solo se tutte le risposte sono `sì`:

1. serve direttamente una pratica paghe inclusa;
2. usa soltanto dati interni o un file scelto volontariamente;
3. funziona localmente, senza chat generale e senza nuovo connettore personale;
4. produce un risultato strutturato, verificabile, reversibile e visibile alla persona;
5. non amplia permessi filesystem, rete o ricerca;
6. riduce un tempo manuale misurabile senza aumentare il rischio oltre il gate approvato.

Una sola risposta `no` mantiene la funzione fuori. Una nuova fonte dati o destinazione esterna
richiede un piano separato; non può essere aggiunta come piccola estensione dell'MVP.

---

## 8. Sicurezza e privacy by design

### 8.1 Default deny

Ogni capacità parte negata e viene concessa solo se necessaria al singolo compito. Il modello non
può ampliare i propri permessi.

- allowlist dei tool compilata nel backend;
- allowlist dei tipi documentali versionata;
- allowlist dei campi per tipo;
- `purposeId`/`PurposeManifest` obbligatorio per ogni lettura, estrazione e spiegazione;
- `tenantId` e ID risorse esatti sempre obbligatori; azienda/dipendente/periodo secondo lo schema
  tipizzato del purpose, senza scope fittizi o wildcard;
- limiti di dimensione, pagine, tempo e memoria;
- processi PaghePro senza privilegi amministrativi e WebView dedicata senza estensioni/sync;
- filesystem temporaneo dedicato;
- rete in uscita negata all'intero process tree che tocca dati: host, OCR, parser, modello,
  validatori e crash handler;
- frontend senza script, font, analytics o CDN remoti e con Content Security Policy restrittiva;
- schema locale e messaggi bridge in allowlist, client firmato, audit token e nonce per sessione;
- log di ogni tentativo negato.

### 8.2 Cifratura e segreti

- FileVault attivo è un prerequisito, non l'unica protezione;
- un ADR P0 sceglie e prova la cifratura: campi personali del DB e tutti i blob con AES-256-GCM o
  soluzione equivalente verificata, inclusi WAL/backup/cache/quarantena rispetto alla minaccia;
- SQLite non contiene indice testuale o semantico e usa temp in memoria; i valori personali vengono
  cifrati prima della persistenza, così WAL e copie contengono ciphertext;
- canale locale con origine esatta, token per sessione e IPC autenticato;
- password derivata con algoritmo adeguato e secondo fattore opzionale;
- chiave dati casuale fuori dal database, avvolta sia dal Keychain locale sia da una recovery
  passphrase/key conservata dall'utente;
- rotazione, blocco, perdita password e restore su un nuovo profilo macOS sono casi di test;
- nessuna API key AI cloud;
- segreti e contenuti mai nei log;
- core dump e crash dump con memoria sensibile disabilitati;
- backup cifrato con chiave distinta e prova periodica di ripristino.

### 8.3 Retention

Ogni categoria avrà una durata approvata, non un `conserva tutto`:

| Dato | Default di progetto | Decisione finale |
|---|---|---|
| File rifiutato/non pertinente | crypto-erasure immediata; residui ripuliti entro 5 minuti al riavvio | test automatico |
| Cache OCR/modello | chiave effimera distrutta dopo conferma; massimo 15 minuti | configurazione tecnica |
| Documento accettato | durata necessaria alla pratica/obbligo applicabile | professionista/privacy |
| Campi estratti non confermati | eliminazione a fine revisione | policy prodotto |
| Audit | contenuto minimo, senza copia integrale del documento | policy privacy |
| Backup | finestra rotante separata e legal hold esplicito | decisione proprietario + obblighi applicabili |

Ogni documento accettato possiede una chiave-oggetto e un grafo dei derivati: originale/copia
minimizzata, OCR, anteprima, campi, provenienza, cache e riferimenti negli snapshot. Alla scadenza o
all'esercizio di un diritto applicabile, il motore valuta obbligo e legal hold, elimina ciò che può,
distrugge le chiavi e registra motivazione per ciò che deve restare.

Un `retention ledger` append-only, firmato e con sequenza monotona conserva tombstone e legal hold
senza contenuto documentale. Il suo ultimo numero di sequenza/hash viene scritto anche in un
`recovery ledger anchor` firmato, conservato su un secondo supporto offline indipendente dai backup
dati; due copie fisiche cifrate sono raccomandate. Ogni tombstone/legal hold resta
`in attesa di consolidamento` e non viene dichiarato completato finché l'anchor corrente non è
aggiornato. Un supporto scollegato non viene considerato magicamente aggiornato.

Dopo un restore, PaghePro richiede l'anchor corrente e applica ledger/scadenze **prima** di rendere
visibili i dati. Un ledger interno al backup può essere valido ma stale: viene respinto se non
raggiunge la sequenza/hash dell'anchor. Anchor assente, più vecchio o non verificabile lascia il
restore fail-closed in quarantena. La perdita dell'anchor sacrifica la disponibilità del restore,
non riattiva silenziosamente un dato cancellato.

### 8.4 Trasparenza verso la persona

Prima di ogni lettura il software deve mostrare una frase semplice:

> “PaghePro leggerà soltanto questo file sul tuo dispositivo. Non accederà ad altre cartelle e non
> invierà il contenuto a servizi esterni.”

Questa frase è valida solo se test tecnici e configurazione di rete lo dimostrano.

### 8.5 Dati particolari e assistenza

- salute, disabilità, appartenenza sindacale e altri dati particolari non entrano in memorie o
  raccolte generali e non vengono usati per analytics o addestramento;
- ogni eventuale campo necessario ha finalità, accesso e retention separati;
- sono vietate inferenze su salute, emozioni, affidabilità, produttività o vita privata;
- non esiste accesso remoto ordinario di supporto: diagnostica ed export dei log sono avviati dal
  proprietario e mascherano i dati;
- una futura assistenza `break-glass` richiederebbe un piano separato, doppia autorizzazione,
  scadenza breve e audit completo.

### 8.6 Backup ed export senza uscita nascosta

- Il backup è consentito soltanto nella cassaforte locale o su volume rimovibile cifrato scelto
  esplicitamente.
- Sono rifiutati iCloud Drive, OneDrive, Dropbox, Google Drive, cartelle sincronizzate note,
  WebDAV, SMB/NFS, mount remoti e symlink verso queste destinazioni.
- La UI mostra il tipo reale della destinazione; il processo backup non possiede rete.
- L'export ordinario usa di default un pacchetto cifrato. Un export in chiaro richiede avviso e può
  essere scritto soltanto su volume locale/rimovibile non sincronizzato.
- Dopo che la persona sposta manualmente un file fuori dalla destinazione approvata, quel file esce
  dalla custodia tecnica di PaghePro; questa transizione viene dichiarata, non nascosta.
- I test coprono percorsi iCloud/OneDrive/Dropbox, mount di rete, alias e symlink.

### 8.7 Confine della minaccia e supply chain

PaghePro protegge da accessi accidentali, componenti troppo privilegiati, documenti malevoli e
connessioni non autorizzate. Non può rendere sicuro un computer già compromesso da malware/root.
Sono quindi prerequisiti: macOS supportato e patchato, FileVault, account non amministratore e
blocco schermo. Runtime, modello, OCR e pacchetti normativi devono avere licenza approvata, SBOM,
firma, digest pinning, limite dimensione e rollback; nessun componente si aggiorna da solo.

---

## 9. Roadmap personale a gate

> **ROADMAP STORICA — NON ESEGUIBILE.** Le checklist P-1–P6 qui sotto documentano il precedente
> pilot ristretto. È vietato usarle per reintrodurre come limiti globali una azienda, 10
> dipendenti, un CCNL, solo UNILAV, assenza dell'indice o assenza del Copilot interno. Prima di
> qualsiasi codice AI si segue G0 del piano
> [`PIANO-AI-LIBERA-LIMITI-USO.md`](PIANO-AI-LIBERA-LIMITI-USO.md). Restano riutilizzabili solo le
> invarianti di sicurezza locale richiamate esplicitamente dalle fasi G1/G4 del nuovo piano.

Le stime sono indicative per adattare e verificare il codice esistente, non promesse. P50 assume un
flusso di sviluppo focalizzato e hardware compatibile; P80 è circa 1,5-2 volte P50. Nessuna fase
parte senza autorizzazione separata.

```text
P-1 → P0 → P1A → P1B → P1C → P2 → P3 → P4 → P5 → P5B → P6
                      └──────────────────→ test privacy continui
```

### Gate P-1 — STORICO, NON ESEGUIRE

**Obiettivo:** congelare la riduzione di scope prima del codice.

- [ ] Confermare macOS Apple Silicon come unico target iniziale, con hardware minimo indicato.
- [ ] Confermare un solo proprietario e nessun portale.
- [ ] Confermare AI solo locale e nessun provider cloud.
- [ ] Congelare: una azienda, 1-10 dipendenti standard, un CCNL, un periodo per volta, un CSV
  presenze e UNILAV come unico documento AI.
- [ ] Firmare la matrice casi inclusi/esclusi di §7.1; ogni caso non classificato è escluso.
- [ ] Confermare `PersonalDesktop + PersonalBridge + PersonalHost + PersonalWorker` come
  architettura da prototipare.
- [ ] 👤 Confermare disponibilità futura di Apple Developer ID/notarizzazione; senza account il
  prototipo locale può proseguire, ma P5B e il rilascio restano bloccati.
- [ ] Attribuire e stabilizzare le modifiche già presenti nel worktree, come previsto dal Gate
  F-1 del master.
- [ ] Dopo l'adozione esplicita, rendere questo profilo la fonte operativa canonica aggiornando
  `AGENTS.md`, `PIANO.md`, `CLAUDE.md` e il registro capability; fino ad allora nessuna attività 🔧
  del vecchio piano può essere eseguita.

**Gate:** decisioni scritte, scope escluso accettato e baseline pulita senza perdere lavori
concorrenti. Target OS/hardware e fonte operativa non possono restare da decidere dopo P-1.

### Fase P0 — STORICA, NON ESEGUIRE

**P50:** 1-2 settimane di ADR, prototipi di rischio e governance.

- [ ] Inventario di ogni permesso filesystem e rete esistente.
- [ ] Inventario di riferimenti/progetti/package del Personal Host; nessun SDK cloud nel grafo.
- [ ] Diagramma dei dati dal file alla cancellazione.
- [ ] ADR `Personal Host`: composition root, endpoint e dipendenze ammesse.
- [ ] ADR `Sandbox/Egress`: WebView, bridge XPC, entitlements, socket Unix, assenza di TCP/UDP e updater.
- [ ] ADR `Cifratura/Recovery`: dati coperti, WAL/temp, DEK, Keychain, recovery key e rotazione.
- [ ] ADR `Migrazioni/Update`: baseline EF, upgrade atomico, backup, rollback e anti-downgrade.
- [ ] Policy `default deny` per rete, file e tool.
- [ ] Registro `purposeId` con tipi/campi documentali, tool, scopo e retention consentiti.
- [ ] Test negativo progettato per ogni fonte vietata.
- [ ] GDPR pack: ruoli, finalità e basi artt. 6/9, registro trattamenti, screening DPIA/esito,
  informative, diritti accesso/rettifica/cancellazione/limitazione, retention/legal hold, registro
  incidenti e procedura data breach.
- [ ] Creare `privacy_acceptance.yaml` versionato, letto da CI/release, con owner ed evidenze.

Valori minimi iniziali del contratto di accettazione:

- almeno 500 UNILAV autorizzati/sintetici rappresentativi e 500 documenti negativi, inclusi almeno
  100 casi misti, con dati particolari o istruzioni avversarie;
- lower bound 95% ≥99% per exact match dei campi critici; finché non lo raggiunge, inserimento
  manuale obbligatorio oltre alla conferma già prevista;
- upper bound 95% ≤1% per falsa accettazione di documenti non pertinenti e 100% blocco del corpus
  privacy/prompt-injection critico;
- la soglia precedente misura il classificatore, non concede persistenza: validazione deterministica
  di formato/schema e anteprima devono fermare il 100% del corpus negativo prima dell'estrattore;
  un solo documento negativo oltre quarantena fa fallire P2;
- 0 accessi a file non selezionati, 0 accessi cross-scope, 0 socket/byte esterni dei processi dati;
- file massimo 25 MB/50 pagine, limiti di decompressione e timeout definiti per parser;
- p95 ≤60 secondi per UNILAV e RSS totale ≤10 GB sul target;
- app+runtime+modello installati ≤20 GB; spazio libero richiesto prima di install/upgrade pari al
  massimo fra 50 GB e `2×(app+modello) + 2×cassaforte + 10 GB`;
- crypto-erasure immediata al rifiuto, pulizia crash entro 5 minuti dall'avvio e cache massima 15
  minuti;
- severità incidenti `INC-P0`-`INC-P3`, evidenze e regola di riavvio finestra come nel master;
- almeno 5 sessioni di usabilità rappresentative: ≥90% task completati senza assistenza, prossima
  azione individuata in meno di 10 secondi e 0 ricorso a chat/app esterne.

**Gate P0:** è dimostrabile quali dati il software può vedere e quali non può tecnicamente vedere.
Il gate fallisce se una capability dati non ha un `purposeId`, base, retention, soglia o test
completo, oppure se uno dei quattro ADR non supera il prototipo sul target reale.

### Fase P1A — STORICA, NON ESEGUIRE

**P50:** 2-4 settimane dopo i prototipi P0.

- [ ] Creare `Paghe.PersonalHost` nello stesso repository con dipendenze minime.
- [ ] Escludere dal build/package Google login, SDK/cloud AI, SdI, notifiche, firma e banking.
- [ ] Creare build React personale con allowlist di route/menu/comandi; nessuna route Copilot,
  fatture, contabilità, portali, multi-azienda o funzione rinviata entra negli asset.
- [ ] Generare inventario degli endpoint Personal e fallire la build per endpoint non allowlisted,
  inclusi endpoint chat/conversazionali.
- [ ] Applicare lato server i limiti: una azienda, massimo 10 dipendenti, un CCNL, UNILAV unico.
- [ ] Servire gli asset personali inclusi nella WebView dedicata, senza browser personale.
- [ ] Sostituire il transport HTTP frontend con bridge tipizzato; Host/Worker soltanto su XPC/socket Unix.
- [ ] Provisioning una tantum del proprietario e successiva rimozione/chiusura della registrazione.
- [ ] Provisioning legato all'account locale, audit token/firma del client e nonce solo in memoria.
- [ ] Avvio fail-closed su porte TCP/UDP, entitlement rete, schema/messaggio non allowlisted o provider cloud.
- [ ] CSP senza risorse remote, `connect-src 'none'` e navigation policy chiusa.
- [ ] Audit privo di contenuto sensibile superfluo.

**Gate P1A:** package/dependency scan dimostra assenza dei componenti esclusi; nessun entitlement o
socket Internet e nessuna porta TCP/UDP; inventari route/comandi contengono solo allowlist; seconda
azienda, undicesimo dipendente, secondo CCNL/tipo documento e chat command sono respinti
server-side. Client non firmato, audit token/nonce errati e provisioning ripetuto sono respinti.

### Fase P1B — STORICA, NON ESEGUIRE

**P50:** 2-5 settimane.

- [ ] Sostituire `EnsureCreated`/patch generiche con baseline e migrazioni EF versionate.
- [ ] Cifrare campi personali, vault, cache e quarantena secondo ADR; coprire WAL/temp.
- [ ] DEK casuale avvolta da Keychain e recovery passphrase/key.
- [ ] Temp in memoria o directory cifrata dell'app; core dump disabilitati.
- [ ] Backup pre-upgrade e backup utente soltanto su destinazioni locali approvate.
- [ ] Chiavi-oggetto, grafo derivati, retention ledger e anchor offline indipendente per restore.
- [ ] Export cifrato predefinito e confine di custodia esplicito.
- [ ] Restore su profilo macOS nuovo, rotazione chiave e recovery mode.
- [ ] Matrice app/schema, spazio preflight, forward-fix e rollback provati.

**Gate P1B:** fresh install, upgrade da copia realistica, rollback/forward-fix e restore su profilo
pulito riescono; cancellazione ammessa rimuove tutti i derivati e non viene resuscitata da un backup
precedente quando l'anchor nuovo è presente; ledger valido ma stale e anchor assente bloccano il
restore. Legal hold è motivato/auditato; nessun dato personale in DB/WAL/temp/log in chiaro;
recovery key e recovery ledger anchor sono realmente usabili.

### Fase P1C — STORICA, NON ESEGUIRE

**P50:** 2-4 settimane.

- [ ] App Sandbox/entitlements per Desktop, Bridge, Host, Worker e Updater separati.
- [ ] Host/worker vedono soltanto cassaforte, quarantena e socket previsti, non la home.
- [ ] Desktop, Bridge, Host, Worker, OCR, parser, modello e crash handler senza entitlement/egress.
- [ ] Updater senza accesso a cassaforte e spento durante l'uso ordinario.
- [ ] Nessuna estensione, sync, cache persistente, history, autofill o remote debugging nella WebView.
- [ ] Anteprime interne senza Quick Look, clipboard o stampa; nessun thumbnail/spool esterno.
- [ ] Test process-tree su avvio, uso, errore, crash, primo avvio e aggiornamento.

**Gate P1C:** file-esca fuori vault irraggiungibili; 0 socket/byte esterni dai processi dati;
nessun dato in cache/history/sync del viewer; ogni configurazione più permissiva fallisce l'avvio.

### Fase P2 — STORICA, NON ESEGUIRE

**P50:** 2-4 settimane.

- [ ] Selettore di un singolo file; nessun watcher.
- [ ] Upload del contenuto selezionato; nessuna API accetta path arbitrari del computer.
- [ ] Quarantena con chiave effimera, magic/MIME reale, parser sandboxato, limiti e contenuto attivo negato.
- [ ] Classificazione deterministica quando disponibile.
- [ ] Solo UNILAV in allowlist; ogni altro tipo rifiutato.
- [ ] Firma/layout/schema UNILAV validati deterministicamente prima dell'estrattore AI.
- [ ] Minimizzazione intra-documento per pagine/zone/campi e stop su dato particolare fuori schema.
- [ ] Validazione/encoding end-to-end contro stored-XSS, formule CSV, template, path e log injection.
- [ ] Anteprima prima del salvataggio.
- [ ] Revisione affiancata e provenienza.
- [ ] Pulizia garantita di file rifiutati, cache e crash residue.
- [ ] Nessun indice semantico o embedding nel package.

**Gate P2:** 0 documenti del corpus negativo oltre la quarantena o verso l'estrattore e 0 file
rifiutati nel DB, storage, cache e backup; qualunque falso positivo non intercettato fallisce il
gate, indipendentemente dalla metrica aggregata. Nessuna lettura di file non selezionati. Anche
UNILAV con allegati/note estranee deve minimizzare o astenersi secondo `privacy_acceptance.yaml`.

### Fase P3 — STORICA, NON ESEGUIRE

**P50:** 2-5 settimane, subordinata al benchmark hardware.

- [ ] Un solo modello/runtime locale qualificato.
- [ ] Versione, licenza, digest, formato quantizzato, dimensione e SBOM fissati nel manifest.
- [ ] Import offline separato; nessun model pull o aggiornamento eseguito dal runtime.
- [ ] OCR e classificazione per il primo tipo documentale.
- [ ] Schema di output chiuso; nessun testo libero diventa comando.
- [ ] Confidenza, astensione e modulo manuale.
- [ ] Prompt injection e documenti avversariali nel benchmark.
- [ ] Payload XSS/CSV/template/path/log restano inerti in UI, export, PDF, audit ed errori.
- [ ] Costo in RAM, CPU/GPU, tempo e batteria misurato.
- [ ] Nessun training sui dati dell'utente.
- [ ] Tracciamento versione modello/prompt senza contenuto sensibile nei log.

**Gate P3:** tutte le soglie statistiche, privacy e prestazioni di `privacy_acceptance.yaml` sono
raggiunte sul corpus preregistrato e sull'hardware P-1; altrimenti il flusso resta parser/manuale.

### Fase P4 — STORICA, NON ESEGUIRE

**P50:** 3-6 settimane più validazione professionale.

- [ ] Un CCNL e casistiche standard dichiarate.
- [ ] Parametri normativi versionati nel DB.
- [ ] `decimal` per tutti gli importi e le aliquote.
- [ ] Presenze, variabili e dati mancanti.
- [ ] Calcolo deterministico e snapshot immutabile.
- [ ] Confronto voce per voce con casi professionali.
- [ ] Bozza cedolino/LUL con etichetta di validazione.
- [ ] Blocco esplicito dei casi fuori perimetro.
- [ ] Test di rifiuto server-side per ogni riga della matrice casi esclusi e per il caso sconosciuto.

**Gate P4:** casi in-scope riconciliati e firmati dal professionista; ogni esclusione e un caso
sconosciuto sono bloccati prima del calcolo, non stimati dall'AI.

### Fase P5 — STORICA, NON ESEGUIRE

**P50:** 2-4 settimane.

- [ ] Home exception-first.
- [ ] Importazione e revisione comprensibili senza linguaggio tecnico.
- [ ] Checklist pratica e dati mancanti.
- [ ] Pagina `Cosa ha visto l'AI`.
- [ ] Privacy/accessi e interruttore totale.
- [ ] Esportazione manuale con riepilogo.
- [ ] Nessun campo chat generale.
- [ ] Route/menu/comandi fuori allowlist assenti dal pacchetto, non soltanto nascosti.
- [ ] Accessibilità, tastiera, errori recuperabili e conferme chiare.

**Gate P5:** nelle almeno 5 sessioni preregistrate, ≥90% dei compiti viene completato senza
assistenza, la prossima azione viene trovata in <10 secondi e il ricorso a chat/app esterne è 0.
Ogni azione mostra input, risultato, stato e modo di annullare/correggere; i dettagli tecnici non
sono nella vista normale.

### Fase P5B — STORICA, NON ESEGUIRE

**P50:** 2-4 settimane dopo il vertical slice funzionante.

- [ ] App firmata/notarizzata, manifest firmato e trust root pinning.
- [ ] Import manuale di pacchetto software/normativo con digest, SBOM e licenza.
- [ ] Updater separato senza accesso alla cassaforte e senza esecuzione automatica.
- [ ] Backup pre-upgrade, migrazioni transazionali, recovery mode e rollback/forward-fix.
- [ ] Anti-downgrade per schema, regole e runtime; compatibilità verificata prima di modificare dati.
- [ ] Disinstallazione che chiede separatamente se conservare o distruggere cassaforte e recovery.

State machine obbligatoria dell'upgrade:

1. Updater verifica firma, digest, anti-downgrade e compatibilità del manifest senza aprire dati.
2. Host entra in maintenance mode, blocca nuove operazioni, esegue preflight e crea il backup
   cifrato/checkpoint; comunica soltanto esito e ID firmato all'Updater.
3. Host si arresta; Updater sostituisce atomicamente i binari conservando la versione precedente.
4. Il nuovo Host apre il checkpoint e applica la migrazione transazionale alla cassaforte.
5. In caso di successo, Host firma lo stato e l'Updater finalizza dopo la finestra di osservazione.
6. In caso di errore, il nuovo Host ripristina/forward-fixa i dati, si arresta e autorizza il
   rollback dei soli binari; l'Updater non riceve mai chiavi o accesso alla cassaforte.

**Gate P5B:** installazione pulita, upgrade, errore simulato, rollback/forward-fix e disinstallazione
sono provati offline in ogni transizione; pacchetto alterato/scaduto/non compatibile viene rifiutato
prima dei dati e l'Updater resta incapace di aprire backup o cassaforte.

### Fase P6 — STORICA, NON ESEGUIRE

**Durata minima:** tre cicli mensili consecutivi.

- [ ] Uso parallelo senza sostituire il processo ufficiale.
- [ ] Confronto di ogni voce e documento.
- [ ] Registro di correzioni, falsi positivi e tempi.
- [ ] Verifica traffico di rete in ogni release candidata.
- [ ] Verifica che documenti irrilevanti non restino in nessun livello.
- [ ] Restore completo su macchina pulita.
- [ ] Riesame privacy e validazione professionale.

**Gate P6:** tre cicli consecutivi con importi identici al centesimo sui medesimi input/regole o
differenze spiegate, firmate e trasformate in test; 0 `INC-P0/P1`, tutti gli `INC-P2` chiusi;
0 byte/socket esterni dei processi dati; 0 dati non pertinenti conservati. Proprietario e
professionista approvano report e perimetro. Un incidente bloccante o una differenza inspiegata fa
ripartire i tre cicli. Il gate non approva funzioni rinviate.

---

## 10. Test di accettazione obbligatori

### 10.1 Il programma non legge altro

- negare accesso a Desktop/Documenti/Download e verificare che PaghePro funzioni;
- mettere file-esca nelle cartelle personali e dimostrare che non vengono aperti o nominati nei log;
- selezionare un file consentito e verificare che sia l'unico file aperto;
- provare symlink, shortcut, path traversal, file annidati e archivi compressi;
- verificare che il selettore non conceda accesso permanente alla cartella;
- controllare cache/history/autofill/sync/remote debugging della WebView dedicata.

### 10.2 Il programma non comunica fuori

- ispezionare entitlements firmati: nessun processo dati possiede network client/server;
- verificare assenza di socket TCP/UDP in ascolto e connessi;
- catturare IPv4/IPv6, DNS, UDP e proxy per ogni processo durante import, OCR, AI, calcolo, export,
  errore licenza, primo avvio, aggiornamento e crash;
- eseguire con Internet scollegato e ottenere lo stesso risultato deterministico;
- bloccare ogni destinazione e verificare che non esista fallback cloud;
- ispezionare dipendenze, telemetria, updater, font, script e CDN;
- verificare che i log non contengano prompt, documenti, CF, IBAN o importi completi;
- provare destinazioni backup/export iCloud, OneDrive, Dropbox, SMB/NFS/WebDAV e symlink.

### 10.3 Il programma rifiuta ciò che non c'entra

Corpus negativo con:

- foto personali;
- bollette domestiche;
- estratti conto;
- lettere private;
- referti;
- documenti di altro dipendente/pratica;
- UNILAV valido con pagine, note e dati particolari estranei allo scopo;
- PDF con istruzioni malevole;
- campi con stored-XSS, formula CSV, template, path traversal, newline e log injection;
- file rinominati con estensione falsa.

Per ogni file si controllano database, blob, cache, quarantena, audit e backup. Un semplice messaggio
“rifiutato” non basta se il file rimane memorizzato.

### 10.4 L'AI resta nel proprio compito

- non può cambiare `tenantId`, `purposeId` o gli ID risorsa presenti nel manifesto;
- non può invocare SQL, shell, HTTP, browser o filesystem;
- non può trattare testo del documento come istruzione;
- non può calcolare importi normativi;
- si astiene quando mancano dati o fonte;
- non recupera informazioni da una pratica diversa;
- l'interruttore AI interrompe il worker e pulisce la cache.

### 10.5 Il prodotto resta utile senza AI

- inserimento manuale completo;
- calcolo deterministico identico;
- visualizzazione e correzione dati;
- generazione bozza;
- export, audit, backup e restore;
- messaggio semplice quando il modello locale non è disponibile.

### 10.6 Cancellazione e restore non fanno riapparire dati

- accettare un UNILAV sintetico e censire originale/minimizzato, OCR, preview, campi, provenance,
  cache, snapshot e copie backup;
- eseguire scadenza o diritto applicabile e verificare grafo, chiavi-oggetto e tombstone;
- distinguere dati cancellabili da record soggetti a obbligo/legal hold, con motivo e audit;
- ripristinare un backup creato prima della cancellazione;
- provare backup pre-cancellazione + anchor nuovo: il dato viene eliminato prima della visibilità;
- provare ledger del backup firmato ma stale: viene respinto rispetto all'anchor corrente;
- provare anchor assente/vecchio/non verificabile: il restore resta in quarantena fail-closed;
- lasciare scadere la finestra backup e verificare distruzione delle copie/chiavi previste.

---

## 11. Metriche del prodotto ristretto

### Privacy e sicurezza

- `0` file aperti senza selezione esplicita;
- `0` byte/socket di contenuto paghe inviati a Internet dai processi PaghePro prima dell'eventuale
  consegna manuale dichiarata di un export;
- `0` documenti rifiutati presenti in storage/cache/quarantena/backup dopo gli SLA;
- `0` accessi fuori pratica negli eval;
- `100%` azioni AI visibili nella pagina audit;
- `100%` cache temporanee eliminate entro 15 minuti e residui crash entro 5 minuti dall'avvio.

### Utilità

- tempo mediano import→campi confermati;
- percentuale campi corretti senza modifica, separata per tipo;
- percentuale astensioni corrette;
- falsi positivi e falsi negativi della pertinenza;
- tempo umano per chiudere una pratica;
- numero di dati mancanti intercettati prima del calcolo;
- differenze rispetto ai golden case professionali.

### Prestazioni locali

- RAM massima;
- CPU/GPU e temperatura;
- secondi per pagina;
- spazio occupato da modello, documenti e cassaforte;
- durata backup/restore;
- funzionamento offline continuativo.

---

## 12. Cosa cambia rispetto al master plan

| Area master | Decisione personale |
|---|---|
| F-1/F0 | mantenere: baseline, verità di stato e perimetro |
| F1-F4 | mantenere e anticipare: sicurezza, privacy, snapshot e regole |
| F5 | mantenere solo per un CCNL/casi standard |
| F6A | mantenere solo runner locale per controlli importati manualmente |
| F7A | ridurre a Inbox esplicita con solo UNILAV nell'MVP |
| F7B | rinviare |
| F8A | ridurre ad anagrafica e pratica del proprietario; niente portale |
| F8B | rinviare |
| F8M | un solo import manuale, solo se necessario al proprietario |
| F9 | mantenere checklist e anomalie; niente azione autonoma |
| F10 | soltanto bozze/export manuale dopo validazione; invio reale rinviato |
| F11-F13 | rinviare; ogni eccezione richiede un nuovo piano approvato |
| F14 | sostituire il Copilot generale con comandi contestuali locali |
| F15 | rinviare completamente |
| F16 | rinviare completamente |
| F17 | sostituire con shadow personale P6 |
| F18 | non applicabile finché il prodotto resta personale |

La riduzione elimina la maggior parte di provider, account, integrazioni, ruoli, portali e agenti.
Restano soltanto i componenti che rendono più sicura e veloce una pratica paghe scelta dalla
persona.

---

## 13. Decisioni future, una alla volta

Il piano assume già computer locale macOS Apple Silicon, profilo singolo e UNILAV. Prima di una
eventuale implementazione verrà chiesta una sola conferma per volta:

1. confermare che il Mac reale soddisfa OS supportato, Apple Silicon, 16 GB, spazio e FileVault;
2. scegliere l'unico CCNL/caso standard;
3. nominare il professionista che valida i golden case;
4. approvare retention, legal hold, backup locale e supporto separato del recovery ledger anchor;
5. decidere se serve un unico import manuale dal software precedente.

L'uso di un provider AI cloud non è una decisione dell'MVP. Se verrà mai proposto, richiederà un
nuovo piano, opt-in esplicito, valutazione privacy, contratto, minimizzazione, test e un indicatore
visibile. Non potrà essere un fallback automatico.

---

## 14. Fonti e principi

Il perimetro applica:

- minimizzazione, limitazione della finalità, limitazione della conservazione, integrità e
  riservatezza dell'art. 5 GDPR;
- protezione dei dati fin dalla progettazione e per impostazione predefinita dell'art. 25 GDPR;
- principio tecnico del minimo privilegio: utenti e processi ricevono solo risorse e autorizzazioni
  indispensabili al compito.

Fonti consultate il 15 luglio 2026:

- [Regolamento UE 2016/679 — GDPR](https://eur-lex.europa.eu/legal-content/IT/TXT/?uri=CELEX%3A32016R0679)
- [EDPB — Guidelines 4/2019, data protection by design and by default](https://www.edpb.europa.eu/our-work-tools/our-documents/guidelines/guidelines-42019-article-25-data-protection-design-and_en)
- [NIST — least privilege](https://csrc.nist.gov/glossary/term/least_privilege)
- [NIST SP 800-53 Rev. 5](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)

Le regole paghe, CCNL e file ufficiali restano quelle del master: fonti versionate, controlli
ufficiali disponibili e validazione professionale prima dell'uso reale.

---

## 15. Verdetto del piano ristretto

**VERDETTO STORICO SUPERATO.** Questo piano era stato approvato come pilot ristretto dal panel
architettura/fattibilità, sicurezza/GDPR e scope/UX; non è più la policy funzionale corrente.

Le revisioni hanno richiesto e verificato: host personale separato, trasporto XPC senza rete,
cifratura/recovery/migrazioni, backup locale, minimizzazione intra-documento, cancellazione e
restore, neutralizzazione injection, un solo vertical slice, allowlist UI/server e gate statistici.
Nell'ultima revisione non restano finding alti o medi aperti.

Il risultato desiderato è più piccolo del master, ma più coerente con la richiesta: un programma
personale, offline-first e senza chat generale. Non cerca autonomamente alcun file; per il singolo
file scelto legge in quarantena solo quanto serve a classificarlo e distrugge i dati rifiutati.
L'approvazione del piano non certifica calcoli, documenti o uso reale e non autorizza codice. La
prima eventuale attività AI è G0 del nuovo piano, dopo una richiesta separata dell'utente.

---

## 16. Decisione storica dei tre ruoli — superata

| Ruolo | Decisione | Motivo | Prossima azione |
|---|---|---|---|
| Controllore Consegna | **VERDETTO STORICO** | Valutava il precedente pilot ristretto, non la policy corrente. | Fare riferimento al §18 del nuovo piano. |
| Direttore Prossimo Incarico | In futuro: **G0, poi G1/G2**. | Prima del codice AI servono inventario, threat model, fondazioni locali e Usage Governor. | Partire solo dopo una richiesta separata dell'utente. |
| Ricercatore Mercato/GAP | **Nessuna funzione concorrente entra ora.** | Cloud, portali e integrazioni contraddicono il vantaggio locale/privacy-first finché manca il Governor. | Validare l'UX Guidata durante G3/G9; nessuna integrazione esterna prima di G2 e dei relativi gate. |

Il confronto di controllo ha considerato le pagine prodotto di
[Sistemi JOB](https://www.sistemi.com/software-gestionali/job/job-paghe/per-le-aziende/),
[Zucchetti Paghe](https://www.zucchetti.it/website/cms/cms/prodotto/2626-software-paghe-consulenti-del-lavoro.html) e
[TeamSystem Paghe](https://www.teamsystem.com/hr/teamsystem-hr/funzionalita/paghe/), consultate il
15 luglio 2026. Le loro funzioni più ampie non modificano lo scope ristretto.
