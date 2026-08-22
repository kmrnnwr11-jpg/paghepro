# PIANO — Aurora agente: l'AI che guida e opera tutto PaghePro

> Versione: **22 agosto 2026**
> Stato: **A0 chiusa: baseline confermata e checkpoint Git creati il 22 agosto 2026 sulla
> richiesta del fondatore di proseguire il piano dal punto di sospensione. Sono in lavorazione le
> parti tecniche di A1 e A2; verifica DPA del provider e accensione su Render restano decisioni
> del fondatore.**
> Ogni fase parte solo con approvazione esplicita del fondatore sulla singola fase.
> Precedenza: per perimetro, grant, limiti e classi dati prevale sempre
> [`PIANO-AI-LIBERA-LIMITI-USO.md`](PIANO-AI-LIBERA-LIMITI-USO.md). Per architettura multi-agente
> e memoria vale [`PIANO-AGENTI-AI-MEMORIA-CONOSCENZA.md`](PIANO-AGENTI-AI-MEMORIA-CONOSCENZA.md).
> Questo documento è il piano **operativo** che li mette in fila per costruire davvero
> "Aurora agente", su richiesta del fondatore del 17 agosto 2026.

---

## 0. Decisione in parole semplici

Il fondatore ha chiesto quattro cose, con parole sue:

1. **«Aurora fai questo»** — l'utente non deve più fare tutto a mano: scrive in chat
   «questi giorni sono assenze» e Aurora prepara l'operazione, la mostra, e la esegue
   dopo la conferma.
2. **Consigli veri** — quando si fa una busta paga Aurora suggerisce come fare meglio e
   come risparmiare *legalmente*, citando i riferimenti.
3. **«C'è un errore, ti porto lì»** — se qualcosa è sbagliato, Aurora porta l'utente sulla
   pagina esatta, spiega l'errore in parole semplici e aiuta a correggerlo.
4. **Nessuna fatica** — l'utente non deve studiare il software: Aurora lo guida in tutto,
   con la potenza di un assistente di classe Google/OpenAI/Claude.

Questo piano realizza tutte e quattro le cose. Con due regole che **restano** sempre,
perché proteggono il fondatore e i suoi clienti (commercialisti con dati di lavoratori veri):

```text
AURORA PUÒ FARE TUTTO QUELLO CHE PUÒ FARE L'UTENTE DALLO SCHERMO,
sullo stesso tenant, con lo stesso ruolo, passando dagli stessi controlli.

AURORA NON DECIDE DA SOLA GLI EFFETTI CHE NON SI POSSONO ANNULLARE:
le scritture si applicano dopo un "Conferma" dell'utente,
gli invii esterni (SdI, email, F24) restano fuori finché i gate non li aprono,
i calcoli li fa sempre il motore deterministico, mai il modello.
```

Non sono vincoli commerciali: sono la differenza fra un software che un Consulente del
Lavoro può usare con dati veri e uno che nessuno studio può permettersi di usare.
Tutto il resto — leggere ogni archivio, compilare presenze, registrare dipendenti,
preparare F24, spiegare errori, portare l'utente sulla pagina giusta, consigliare —
**Aurora lo farà**.

---

## 1. Stato reale di partenza (verificato sul codice il 22 agosto 2026)

### 1.1 Stato corrente: checkpoint governato, modello ancora spento

La ricognizione iniziale aveva trovato una chat con sette strumenti che bypassava il Governor.
Quel percorso è ora chiuso nel codice corrente:

- `POST /api/ai/chat` passa sempre un catalogo vuoto (`tool_choice=none`); una tool-call
  spontanea non raggiunge servizi applicativi;
- `POST /api/ai/chat/azione` risponde sempre `409`; il vecchio client di conferma è stato
  rimosso dalla SPA e `AiChatStrumenti` non è registrato nel contenitore applicativo;
- provider cloud e locale sono fail-closed: `Provider=Spento`, allowlist host esatta,
  niente proxy/redirect, approvazione G1-SaaS o G1 locale obbligatoria;
- il pannello offre separatamente un controllo deterministico read-only dei cedolini:
  preflight, grant S2 per utente/tenant/azienda/mese, lotto massimo di dieci rapporti,
  quota per riga, audit consultabile e revoca;
- la selezione monouso (≤ 4.000 caratteri) rifiuta aree private e selezioni multi-range;
  il filtro server blocca password, chiavi e token prima di qualunque modello.

Il checkpoint non è «Aurora agente completo»: non esegue scritture, non calcola cedolini e
non invia al modello nominativi o stati letti dal broker.

### 1.2 Il Governor G2 governa il primo data path, non ancora un runtime agente

In `/api/ai/governor/*` sono attivi `AiScopeGrantEntity`, lease HMAC-SHA256, owner check,
scope S1/S2, policy finalità→risorse→operazioni, capability registry, quote senza overflow,
revoca atomica e audit a catena hash. `ProfessionalQueryBroker` espone cinque proiezioni
minimizzate e il percorso `Cedolini` effettua una verifica prima della query e una seconda
autorizzazione/consumo subito prima di restituire le righe. I campi salute D3A non sono
materializzati né serializzati; la proiezione Dipendenti filtra e minimizza già nel SQL, prima
di materializzare o decifrare un record. Le FK composite impediscono riferimenti cross-tenant
fra rapporti/dipendenti e scadenzario/aziende. Il fuzz da 10.000 richieste ostili resta a zero
accessi.

Restano fuori dal checkpoint il loop del modello, il registro dinamico dei tool, il log dei
run, le proposte persistite e firmate, replay/idempotenza e le scritture. Per questo G1 e la
fase A2 complessiva restano aperti anche se il primo verticale G2 è utilizzabile.

### 1.3 Altri fatti che il piano sfrutta

- **Navigazione SPA a hash** con chiavi tipizzate (`SectionKey`) e gate azienda che
  *riprende* la destinazione dopo la scelta (`sezioneDopoScelta`): un deep link
  «#presenze con azienda X» è già quasi possibile (`salvaAziendaCorrenteId(id)` + hash).
- Il tipo legacy `AzionePropostaAi` resta quarantinato e non è raggiungibile dalla SPA:
  potrà essere sostituito dalla proposta persistita e firmata prevista in A2.
- La superficie API si divide in tre fasce naturali: **letture e simulatori stateless**
  (rischio zero), **scritture ordinarie** già protette dalle regole anti-errore nei
  servizi (mesi futuri, pre-assunzione, F24 inviato, anti-doppione CF, macchine a stati),
  **operazioni pericolose** (hard delete aziende, anonimizza, retention, importa CCNL,
  trasmissione SdI, invii email/SMS, firma, admin) da escludere o proteggere a parte.
- L'RBAC esistente (`RuoloPolicy`) vale anche per Aurora se agisce col token dell'utente:
  `sola_lettura` non può scrivere, le operazioni admin restano admin.
- Il working tree contiene numerosi file modificati/non tracciati di lavori concorrenti (inclusa questa
  evoluzione di Aurora): prima di costruire, va riconciliato (fase A0).

---

## 2. DeepSeek Harness: cosa prendiamo e cosa no

Il fondatore ha indicato <https://github.com/deepseek-ai/deepseek-harness> (dsh).
Verificato il 17 agosto 2026: esiste, pubblicato il 13 agosto 2026, licenza **MIT**
(compatibile col SaaS), TypeScript/Node.js, in *developer preview* con cambi
incompatibili annunciati. Architettura: «tutto è un plugin» — adapter del modello,
registro strumenti, log di sessione, sandbox, approvazioni e perfino il loop dell'agente
sono componenti sostituibili («capability seams»: definizione del servizio / fornitore /
consumatore).

### 2.1 Decisione: adottare i concetti, non incorporare il runtime

**Non** si porta il runtime Node.js di dsh dentro il backend .NET del SaaS. Motivi:

1. è in developer preview con breaking changes dichiarati: non si àncora un prodotto
   paghe a un'API instabile;
2. aggiungerebbe un secondo runtime (Node) nel container, con superficie di attacco e
   operatività doppie;
3. il punto di enforcement (Governor, tenant, RBAC, regole anti-errore) **deve** restare
   nel backend C#: un harness esterno che chiama le nostre API non può essere lui
   l'autorità sui permessi;
4. la licenza MIT permette di riusare liberamente le **idee**, che è la parte di valore.

### 2.2 I cinque concetti di dsh che il piano adotta in C#

| Concetto dsh | Traduzione in PaghePro |
|---|---|
| Tool registry a plugin | `AiToolRegistry` dinamico: ogni strumento è una classe registrata con schema, classificazione dell'effetto e gate richiesto (oggi il catalogo è una proprietà statica) |
| «Model-visible ⟺ logged» (la sessione è la fonte di verità) | Log di sessione persistito: tutto ciò che entra nel contesto del modello e ogni tool call è ricostruibile da `AiRunEntity`/`ToolCallEntity` (già modellate nel master plan) |
| `interaction/` (approvazioni, ask-user) | Flusso proposta→conferma con **token server-side firmato** legato all'hash degli argomenti (oggi la conferma rimanda gli argomenti dal browser: da irrobustire) |
| `guard/` (igiene del loop, timeout) | Guardie del loop: massimo giri, timeout per tool, rilevamento loop (stesso tool + stessi argomenti), interruzione pulita su revoca/quota |
| Intento di resa del tool (`generic`/`terminal`/`diff`) | Ogni strumento dichiara come si mostra in UI: testo, tabella, **diff** per le scritture, **navigazione** per i salti di pagina |

Facoltativo, fuori dal prodotto: usare dsh come banco di prova locale del fondatore
(esperimenti di prompt/strumenti contro l'API di sviluppo), senza mai collegarlo a dati
reali. Non è un prerequisito di nessuna fase.

---

## 3. Architettura di Aurora agente

```text
Utente (chat Aurora, in ogni schermata)
        |
        v
POST /api/ai/chat  — auth JWT, tenant dal token, filtro segreti, limiti
        |
        v
AuroraAgentRuntime (C#, nel backend)
  ├─ SessionLog: ogni messaggio/tool call/esito persistito (model-visible ⟺ logged)
  ├─ Guard: max giri, timeout, anti-loop, stop su revoca/quota
  ├─ AiToolRegistry: strumenti a plugin, default DENY, filtrati per ruolo+gate+grant
  ├─ Governor G2: grant/lease per sessione; OGNI tool call passa da /accesso o dal broker
  └─ ModelAdapter: DeepSeek / GLM / OpenAI-compatibile (Ollama) — Claude come adapter futuro
        |
        v
  Letture  → eseguite subito via ProfessionalQueryBroker / servizi (righe minimizzate)
  Scritture → AzionePropostaAi con diff + token firmato → l'utente conferma → servizi normali
  Navigazione → AzionePropostaAi di tipo "vai_a" → la SPA salta a sezione+azienda+dettaglio
        |
        v
  Audit a catena hash (Governor) + registro attività GDPR già esistente
```

Principi non negoziabili (dai piani prevalenti, qui solo richiamati):

- il tenant viene **sempre** dal claim del token; nessuno strumento ha un parametro tenant;
- il modello non riceve mai lease, credenziali, SQL, shell, HTTP libero;
- ogni strumento nasce DENY e si attiva con classificazione + policy + test (§3.4 del
  piano limiti d'uso);
- i calcoli restano nei motori deterministici: Aurora li invoca e li spiega, non li
  sostituisce;
- le scritture sono sempre reversibili o confermabili; le azioni esterne restano chiuse
  fino ai gate loro (G8/GX);
- kill switch: `Provider=Spento` spegne tutto; «Arresta AI» del Governor revoca i mandati.

### 3.1 Il contratto del singolo strumento (`ToolDescriptor`)

Ogni strumento del registry dichiara (contratto §7.2 del master, esteso con l'intento di
resa di dsh):

```yaml
id: presenze.compila_giorni.v1
descrizione_utente: "Segna assenze, ferie o permessi sui giorni indicati"
schema_argomenti: {...}          # JSON Schema chiuso
effetto: lettura | scrittura_reversibile | scrittura_protetta | navigazione
resa_ui: testo | tabella | diff | navigazione
ruolo_minimo: operatore | admin
risorse_catalogo: [cartellini]    # ProfessionalResourceCatalog, default DENY
gate_richiesto: A3                # fase di questo piano che lo attiva
autonomia_max: A2                 # A2 = solo proposta; A3 = applicabile dopo conferma
idempotency: true                 # ripetere la conferma non duplica l'effetto
undo: descrizione di come si annulla (o "rileggi e ricorregi")
```

### 3.2 La conferma non falsificabile

Oggi `POST /api/ai/chat/azione` riesegue lo strumento con gli argomenti JSON rimandati
dal browser. Funziona, ma il master plan (F14) chiede di meglio, e il piano lo adotta:

1. quando Aurora propone una scrittura, il server salva la proposta
   (`PropostaAzioneEntity`: strumento, argomenti, hash, scadenza ~10 minuti, stato) ed
   emette un **token firmato** con lo stesso schema HMAC della lease del Governor;
2. la SPA mostra il **diff leggibile** («Giorni 12–14 di marzo: da Lavorato a Assenza
   non retribuita») e i campi mancanti;
3. la conferma invia solo il token: il server riverifica firma, scadenza, tenant, utente,
   ruolo, hash degli argomenti, e **riesegue le validazioni di dominio** al momento
   dell'applicazione;
4. esito e undo finiscono nel SessionLog e nell'audit. Ripetere la conferma non duplica
   (idempotency key).

Così né il modello né un browser manomesso possono eseguire qualcosa di diverso da ciò
che l'utente ha visto e approvato.

### 3.3 Sessioni e memoria (prima versione)

- La cronologia resta **stateless lato client** come oggi (solo domande utente), finché
  il SessionLog server-side non è maturo; niente memoria durevole nascosta.
- Il SessionLog server è **operativo** (audit/replay/debug), non una memoria del modello:
  ciò che rientra nel contesto di un turno successivo è deciso dal runtime secondo le
  regole del piano limiti (working memory effimera per grant).
- La regola attuale «dopo un turno con testo selezionato la storia non torna» resta
  finché non c'è un meccanismo che reinietti il contesto **come dato non fidato** senza
  perdere l'intento anti-injection (lavoro della fase A2).
- La memoria ricca (sintesi confermate, indice semantico) resta al piano agenti/G4:
  fuori da questo documento.

---

## 4. Il catalogo strumenti, a onde

Il catalogo cresce per onde. Ogni onda è un elenco chiuso, testato e approvato; uno
strumento non elencato **non esiste** per il modello.

### Onda A — Letture e simulatori (rischio zero, `effetto: lettura`)

Estende le 4 letture attuali. Tutte passano dal Governor (grant di sessione, proiezioni
minimizzate, quota) e rispettano l'RBAC:

| Area | Strumenti (esempi) |
|---|---|
| Aziende/dipendenti | scheda completa, elenco rapporti, dati contributivi mancanti |
| Presenze | cartellino del mese, `verifica_cartellino` (il POST di controllo puro) |
| Cedolini | elenco/dettaglio archiviati, **simulazione calcolo** (stateless), lordo↔netto |
| Fisco/contributi | F24 del mese (senza salvare), stato pratiche F24, scadenzario con urgenze, LIPE/IVA/IRAP (letture), CU |
| Fatture | elenco/dettaglio, **valida** (XSD+SdI), calcolo totali |
| Diagnosi | «cosa manca per generare X» (anagrafica contributiva provvisoria, campi vuoti, DURC scaduto) |

Nota tecnica: il `ProfessionalQueryBroker` oggi copre cinque proiezioni minimizzate
(`Scadenzario`, `Aziende`, `Dipendenti`, `Cartellini`, `Cedolini`); l'onda A aggiunge quelle
ancora mancanti, incluse F24, fatture e pratiche, **prima** di esporre i relativi strumenti.

### Onda B — Scritture ordinarie (`scrittura_reversibile`, sempre proposta+conferma)

Il cuore della richiesta del fondatore («questi giorni sono assenze»):

| Strumento | Cosa propone | Guardrail già esistenti che restano |
|---|---|---|
| `compila_presenze` | assenze/ferie/permessi/straordinari su giorni indicati, con diff giorno per giorno | verifica cartellino, blocco mesi futuri, conferma esplicita già richiesta dalla pagina Presenze |
| `calcola_cedolino` | archiviazione del cedolino del mese | blocco mesi futuri/pre-assunzione, blocco F24 inviato (il `forza=true` resta **solo umano**) |
| `registra_dipendente` / `da_unilav_testo` | creazione dipendente+rapporto da dati o testo UNILAV incollato | anti-doppione CF, campi `daCompletare` |
| `correggi_azienda` / `registra_azienda` | (già esistenti) | verifica duplicati P.IVA/CF |
| `salva_f24_mese` | pratica F24 del periodo | macchina a stati validata |
| `aggiorna_rapporto` | cessazione, part-time, livello | cessazione ≥ assunzione, 409 su anonimizzati |
| `prepara_promemoria` | promemoria scadenza con testo pronto | stati finali bloccati; **l'invio non è in quest'onda** |

Regole dell'onda B: massimo un lotto omogeneo per conferma (es. un mese di un dipendente),
mai scritture «su tutto l'archivio» (S3/S4 vietati in scrittura dal piano limiti), diff
sempre visibile, esito sempre riletto dai servizi (non dal modello).

### Onda C — Mai nel catalogo, o solo con percorso dedicato

| Categoria | Esempi | Destino |
|---|---|---|
| Irreversibili | DELETE azienda / elimina-bulk, anonimizza dipendente, `retention/esegui` | **mai strumenti Aurora**; restano flussi manuali con le loro conferme rafforzate |
| Effetti esterni | trasmetti fattura a SdI, invio email/SMS/WhatsApp promemoria, esporta F24 su disco | chiusi fino a G8/GX; Aurora può **preparare** (bozza, testo, file pronto) e portare l'utente sul bottone |
| Parametri del motore | importa CCNL, modifica parametri normativi | mai da chat; solo flusso manuale admin |
| Sicurezza/piattaforma | auth, 2FA, admin studi, firma (PFX/PIN), audit/retention | mai strumenti Aurora |

Questa tabella è la risposta onesta al «senza vincoli»: i vincoli che restano sono
esattamente questi, sono pochi, e ognuno protegge da un danno che nessuna conferma in
chat può riparare.

---

## 5. «C'è un errore: ti porto lì» — navigazione guidata

Terza richiesta del fondatore, realizzata con tre pezzi:

### 5.1 Azione di navigazione

Nuovo tipo di `AzionePropostaAi` con `effetto: navigazione` che usa il campo `sezione`
già esistente, esteso con contesto:

```json
{ "tipo": "vai_a", "sezione": "presenze", "aziendaId": "…", "rapportoId": "…", "periodo": "2026-03" }
```

La SPA: salva l'azienda corrente (`salvaAziendaCorrenteId`, che emette già l'evento di
aggiornamento shell), cambia hash, e — novità — apre il dettaglio. Serve rendere
indirizzabili i dettagli oggi solo in memoria: hash esteso tipo `#dipendente/<rapportoId>`
e `#presenze/<rapportoId>/<aaaa-mm>` (oggi `rapportoSel` è solo stato React). Le guardie
di ruolo esistenti in `sezioneDaHash` continuano a valere; il gate azienda continua a
riprendere la destinazione.

La navigazione è un'azione **a conferma leggera**: un click («Portami lì»), nessun rischio.

### 5.2 Compagno di errore

Oggi ogni pagina mostra gli errori API inline (`ApiError`, ~282 punti). Si aggiunge un
punto unico nel client API: quando una chiamata fallisce con un errore applicativo, la UI
offre **«Chiedi ad Aurora»**. Il click apre il pannello con un contesto strutturato non
fidato (endpoint, status, messaggio `corpo.errore`, sezione corrente, azienda corrente
— niente payload sensibili). Aurora:

1. spiega l'errore in parole semplici («Non puoi calcolare marzo: il rapporto risulta
   cessato a febbraio»);
2. dice dove si corregge e propone `vai_a` con il contesto giusto;
3. se la correzione è uno strumento dell'onda B, la propone direttamente
   («Vuoi che riapra il rapporto con la data giusta? Ecco il diff»).

I messaggi di errore del backend sono già in italiano e parlanti (regole anti-errore):
sono l'input perfetto per questa spiegazione.

### 5.3 Controlli proattivi

Aurora può eseguire in lettura i controlli che il backend già offre (verifica cartellino,
valida fattura, controlli F24, `daCompletare` delle anagrafiche, DURC in scadenza) e
presentare un «cosa sistemare prima di procedere» con un bottone di navigazione per voce.
È il primo passo del Consigliere (fase A5) e non richiede nessuna scrittura.

---

## 6. Il Consigliere: consigli con fonti, risparmio lecito

Seconda richiesta del fondatore. Regola madre già scritta in
[`PIANO-AI-FIRST-RISPARMIO-FISCALE.md`](PIANO-AI-FIRST-RISPARMIO-FISCALE.md) e adottata
qui integralmente: **ogni consiglio ha fonte (norma o regola interna versionata),
motivazione semplice, documenti necessari, rischio, azione proposta, stato
(suggerito/da verificare/confermato/scartato) e audit di chi ha confermato**.

Prima ondata di consigli (tutti calcolabili dai motori deterministici e dai parametri
già su DB — elementi agevolati 2026: trasferte, fringe benefit, buoni pasto, premi):

- «Questo importo può passare come trasferta esente entro il limite X del 2026 (fonte:
  parametri anno 2026); serve il giustificativo Y» — mai inventare la trasferta: solo
  segnalare il trattamento corretto di dati che l'utente dichiara;
- «Il fringe benefit supera la soglia: sopra X diventa tutto imponibile, valuta Y»;
- «Mancano i minimi CCNL per questo livello: il netto richiesto scende sotto il minimo»
  (il calcolo inverso già rifiuta, Aurora lo spiega prima);
- scadenze e adempimenti in arrivo con costo del ritardo (ravvedimento già calcolabile).

Limiti permanenti del Consigliere (dal piano risparmio e dal piano limiti): niente
suggerimenti di evasione o artifici, niente promesse generiche di «meno tasse», niente
decisioni sui lavoratori, distinzione visiva fra fatto/calcolo/ipotesi/suggerimento,
sempre «da verificare con il professionista» finché un CdL non valida la regola.

---

## 7. La decisione di profilo: dove gira il modello

Il piano limiti d'uso descrive il profilo **personale locale** (nessun cloud AI). Il
prodotto reale del fondatore è un **SaaS su Render** e la sua richiesta («potente come
Google/OpenAI») implica un modello serio. Il piano agenti (§1.4) prevede esattamente
questa scelta e chiede, per il SaaS, un **piano equivalente a G1** lato server. Eccolo,
come capitolo di questo piano (fase A1):

**G1-SaaS — condizioni per accendere un provider cloud nel SaaS:**

1. **Egress allowlist**: il backend può parlare solo con l'endpoint del provider scelto
   (es. `api.deepseek.com`); niente URL dal modello, niente redirect (già spento nel
   handler AI), niente proxy;
2. **Minimizzazione verso il provider**: nel contesto vanno solo i dati necessari al
   turno (proiezioni del broker, non record integrali); il filtro segreti resta davanti
   a tutto; niente dati D3A/D3B (salute, sindacato) nel contesto cloud finché non esiste
   il grant GS — la terna malattia è già esclusa dalla proiezione cartellini;
3. **Contratto e DPA del provider**: verifica termini DeepSeek/GLM su uso dei dati
   (no training sui dati inviati, retention, sede del trattamento). Se non accettabili
   per dati personali, il piano B è un provider con DPA europeo o il modello locale;
   questa verifica è **bloccante** e la fa il fondatore col supporto legale;
4. **Consumi e costi**: `LimiteMensileChiamate` applicato anche alla chat (oggi il
   contatore chat gira con limite 0 = mai bloccante), contatore per tenant già esistente
   (`ConsumiAi`), messaggio chiaro a limite raggiunto («si continua a mano»);
5. **Kill switch provato**: `Provider=Spento` da config spegne tutto senza rompere nulla
   (già vero: il prodotto è completo con AI spenta);
6. **Trasparenza**: informativa aggiornata (l'utente sa che il testo della chat va al
   provider X), pagina «Cosa vede l'AI» del Governor collegata.

Con G1-SaaS approvato, la scelta del modello diventa configurazione: DeepSeek
(function calling, economico, già integrato), GLM, o un endpoint OpenAI-compatibile.
L'adapter Claude per la chat (oggi Claude serve solo l'estrazione documenti) è un lavoro
opzionale della fase A2 se il fondatore vuole quel livello di qualità sul ragionamento.
Il modello **locale** (Ollama/qwen) resta la strada del profilo personale e segue il G1
originale: non è un prerequisito del SaaS.

> Nota di governance: per il profilo personale locale resta integralmente valido il
> percorso G1 del piano limiti. Questo capitolo non lo sostituisce: aggiunge il percorso
> equivalente per il profilo SaaS, come richiesto dal piano agenti §1.4. L'approvazione
> della fase A1 da parte del fondatore vale come emendamento formale che autorizza il
> profilo «SaaS con AI server-side» accanto al profilo personale.

---

## 8. Fasi operative

### Stato incrementale — 17 agosto 2026

Completato un sottoinsieme sicuro di A2/A3, senza anticipare G1: controllo deterministico
`PREPARAZIONE_MESE_PAGHE` sulla risorsa `Cedolini`, in sola lettura, con preflight, grant S2
utente/tenant/azienda/periodo, quota esatta per record, lotto di 10, manifest, «Dati letti dal controllo»
e «Arresta». Rimossi i dati salute dalla proiezione Cartellini; provider e strumenti chat restano
spenti. Non sono implementati calcolo batch, scritture, proposta firmata, replay protection delle
proposte o runtime LLM. I gate G1 e A2 complessivo restano aperti.

Ogni fase: caselle → prova → approvazione del fondatore per la successiva. Nessuna fase
parte senza richiesta esplicita. I criteri quantitativi di accettazione sono quelli del
§13 del piano limiti (100% tool call con grant, zero bypass, revoca < 1 s, corpus
injection, calcoli identici con AI accesa/spenta), applicati a ogni onda.

### Fase A0 — Riconciliazione (prerequisito, solo ordine e verità)

- [x] attribuire e committare i percorsi non committati secondo l'inventario di
      `A0-AURORA-BASELINE.md`: otto checkpoint funzionali (pacchetti crittografici; B0 workflow
      cedolino/schema; B1 chat fail-closed; B2 Governor/broker/RBAC; B3 frontend workflow;
      B3 pannello Aurora; B4 render.yaml; documentazione), con i file misti separati per hunk
      e gli artefatti di §5 della baseline lasciati fuori dal versionamento;
- [x] aggiornare CLAUDE.md e `docs/CHAT-AI-DEEPSEEK.md` allo stato vero del codice
      (modello/tool chat spenti, endpoint azione fail-closed, controllo deterministico separato);
- [x] suite completa verde e build SPA/API verificate (**843/843 backend**,
      **27/27 frontend**; build Release e SPA riuscite; migrazioni coerenti per entrambi i
      provider e smoke Production fail-closed verde su copia isolata del database reale);
- [x] baseline decisa: si parte dal codice attuale (raccomandazione accettata con la
      richiesta del fondatore del 22 agosto 2026 di proseguire il piano).

**Uscita:** repo pulito, documenti veri, baseline dichiarata. **Chiusa il 22 agosto 2026.**

### Fase A1 — Profilo e accensione governata (decisioni, poca scrittura di codice)

- [ ] verifica termini/DPA del provider scelto (bloccante, fondatore + legale);
- [x] egress allowlist e conferma che l'handler AI resti senza proxy/redirect;
- [x] `LimiteMensileChiamate` applicato alla chat; messaggio di limite raggiunto in UI;
- [ ] informativa/trasparenza aggiornata; «Cosa vede l'AI» raggiungibile dal pannello;
- [ ] prova kill switch e fallback manuale completo;
- [ ] accensione **in sviluppo** col provider scelto; Render resta spento finché A2 non
      è provata.

**Uscita:** G1-SaaS approvato dal fondatore; Aurora accesa in sviluppo.

### Fase A2 — Il runtime harness: registry, Governor, sessione, conferme (il cuore)

- [ ] `AiToolRegistry` dinamico a plugin (da proprietà statica a registro con
      `ToolDescriptor`, filtro per ruolo/gate/grant);
- [ ] **aggancio al Governor**: all'apertura del pannello si emette (o riusa) un grant
      di sessione in modalità Guidata (S1/S2: azienda corrente + periodo); ogni tool
      call passa da `/accesso` o dal broker; quota esaurita = checkpoint con
      «Continua?» che rinnova il grant; revoca/«Arresta AI» interrompe il loop;
- [ ] SessionLog persistito (`AiRunEntity`, `ToolCallEntity` del master): model-visible
      ⟺ logged; niente payload sensibili duplicati (l'audit Governor resta la catena);
- [ ] conferma non falsificabile (proposta persistita + token HMAC + riverifica, §3.2),
      in sostituzione del rinvio argomenti dal browser;
- [ ] guardie del loop: timeout per tool, anti-ripetizione, interruzione pulita;
- [ ] proiezioni broker aggiuntive necessarie all'onda A;
- [ ] (opzionale, decisione fondatore) adapter Claude per la chat;
- [ ] test: matrice minima del master (cambio tenant, tool non consentito, conferma
      contraffatta, replay, timeout) + corpus injection su ogni strumento nuovo.

**Uscita:** le sette capacità legacy, riclassificate, dentro il runtime nuovo con Governor e
conferme firmate; zero regressioni sui 52+18 test esistenti.

### Fase A3 — Onda A + Onda B del catalogo (Aurora opera davvero)

- [ ] strumenti di lettura/simulazione dell'onda A, a gruppi, ognuno con proiezione
      minimizzata e test;
- [ ] strumenti di scrittura dell'onda B con diff leggibile per ciascuno
      (`compila_presenze` per primo: è la richiesta esplicita del fondatore);
- [ ] UX: resa `diff` nel pannello, lotto unico per conferma, esito riletto dai servizi;
- [ ] rollout shadow prima (strumenti attivi solo per gli account `@paghe.local`),
      poi per il tenant del fondatore, poi generale.

**Uscita:** «questi giorni sono assenze» funziona end-to-end con conferma; catalogo
onda C dimostrato irraggiungibile (test negativi).

### Fase A4 — Navigazione guidata e compagno di errore

- [ ] hash estesi per i dettagli (`#dipendente/<rapportoId>`, `#presenze/...`) con
      guardie di ruolo invariate;
- [ ] azione `vai_a` end-to-end (sezione+azienda+dettaglio+periodo) con conferma leggera;
- [ ] «Chiedi ad Aurora» sugli `ApiError` applicativi con contesto strutturato non fidato;
- [ ] apertura programmatica del pannello (`commutaAurora` esposto dalla shell);
- [ ] prova con utente non tecnico: dall'errore alla correzione senza aiuto esterno.

**Uscita:** dall'errore alla pagina giusta in un click, con spiegazione.

### Fase A5 — Consigliere proattivo

- [ ] schema del consiglio (fonte, motivazione, rischio, stato, audit) come dati
      strutturati, non testo libero;
- [ ] prima ondata di controlli/consigli deterministici (elementi agevolati, minimi
      CCNL, scadenze, `daCompletare`, DURC);
- [ ] «cosa devo fare oggi» dallo scadenzario con navigazione per voce;
- [ ] etichetta permanente «da verificare con il professionista» finché il CdL non
      valida la singola regola.

**Uscita:** durante la preparazione di una busta Aurora segnala risparmi leciti e
mancanze, con fonti, senza inventare nulla.

### Fase A6 — Verso il piano agenti (fuori da questo documento)

Specialisti dietro Aurora, memoria confermata, indice semantico, ricette programmate
(A4/G6), azioni esterne (G8/GX): seguono `PIANO-AGENTI-AI-MEMORIA-CONOSCENZA.md` e i
gate del piano limiti. Questo piano si ferma dove iniziano quei documenti.

---

## 9. Costi e consumi

- Modello «AI inclusa nel canone» già deciso: chiave centrale del server, consumi per
  studio/mese (`ConsumiAi`), limite configurabile; oltre il limite si continua a mano
  con nota esplicita.
- DeepSeek/GLM hanno costi per token molto bassi; il costo va comunque **misurato** in
  fase A2 (il SessionLog registra token e chiamate per turno) prima di fissare il
  listino; l'adapter Claude, se attivato, va prezzato a parte.
- Nessun tetto artificiale che interrompa il lavoro (regola §2.1 del piano limiti): i
  limiti sono checkpoint morbidi con «Continua».

## 10. Decisioni richieste al fondatore (prima di ogni riga di codice)

| # | Decisione | Opzioni | Raccomandazione |
|---|---|---|---|
| 1 | Baseline codice | tenere l'Aurora attuale con strumenti / tornare a HEAD | tenere e committare (A0) |
| 2 | Profilo di esecuzione | SaaS cloud (G1-SaaS) / solo locale (G1 originale) | SaaS cloud: è il prodotto reale |
| 3 | Provider e DPA | DeepSeek / GLM / OpenAI-compatibile EU / + adapter Claude | DeepSeek per partire, **dopo** verifica termini; Claude opzionale per qualità |
| 4 | Limite consumi chat | numero chiamate/mese per studio | attivare (es. 300) e misurare |
| 5 | Ordine onde | quali strumenti dell'onda B per primi | `compila_presenze` primo |
| 6 | Rollout | shadow → tenant fondatore → generale | sì, in quest'ordine |

## 11. Rapporto con gli altri piani

- **Prevale** `PIANO-AI-LIBERA-LIMITI-USO.md` per perimetro, grant, classi dati, divieti.
  Questo piano ne è l'attuazione operativa per Aurora nel profilo SaaS; il capitolo 7
  (G1-SaaS) è l'emendamento di profilo che quel piano prevedeva di dover scrivere.
- **Si appoggia** al master `PIANO-MASTER-AI-AUTONOMO.md` per L0–L5, Tool Registry,
  modello dati (PropostaAzione/Approvazione/Esecuzione, AiRun, ToolCall) e Copilot F14:
  le fasi A2–A5 realizzano il Copilot F14 in versione L0→L2.
- **Prepara** `PIANO-AGENTI-AI-MEMORIA-CONOSCENZA.md`: Aurora resta l'unica voce; gli
  specialisti arrivano dopo, sopra lo stesso runtime.
- `docs/CHAT-AI-DEEPSEEK.md` va aggiornato in A0 e diventa la guida operativa dello
  stato corrente.

---

## 12. Cosa risponde questo piano alle quattro richieste

| Richiesta del fondatore | Dove nel piano | Quando |
|---|---|---|
| «Aurora fai questo: questi giorni sono assenze» | Onda B, `compila_presenze`, conferma con diff | Fase A3 |
| Consigli e risparmio con riferimenti | Consigliere con schema fonte/rischio/stato | Fase A5 (prime diagnosi già in A3) |
| Errore → pagina esatta → correzione guidata | `vai_a` + compagno di errore | Fase A4 |
| Guida completa, zero fatica | letture ovunque, diagnosi «cosa manca», navigazione, onboarding guidato | da A2 in poi, cresce a ogni onda |
| «Potente come Google/OpenAI/Claude» | G1-SaaS + scelta modello (DeepSeek/adapter Claude) | Fase A1–A2 |
| «Senza vincoli» | tutto tranne l'onda C (irreversibili, invii esterni, parametri motore): quei vincoli restano e proteggono lo studio | permanente |
