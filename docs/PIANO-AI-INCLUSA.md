# Piano: AI inclusa nel canone (il cliente non configura niente)

Decisione del fondatore: l'AI è **compresa nel prodotto**. Il cliente si abbona, entra e
la lettura AI dei documenti funziona — nessuna chiave API da chiedere ai clienti, nessuna
configurazione. I costi dell'AI sono a carico dello sviluppatore e vanno tenuti sotto
controllo; l'obiettivo di lungo periodo è servirla da un **server proprio, senza costi API**.

## Come funziona già oggi (architettura pronta)

La sezione `Ai` di appsettings è **configurazione del server**, non dell'utente: la chiave
sta solo lì (o nella variabile d'ambiente), tutti gli studi passano dallo stesso motore, il
cliente non vede né chiavi né provider. Con il contatore consumi (sotto) è già attivabile.

```json
"Ai": { "Provider": "DeepSeek", "ApiKey": "sk-…", "LimiteMensileChiamate": 300 }
```

## Controllo dei costi (fatto il 15/07/2026)

- **Contatore per studio/mese** (`ConsumiAi` + `ConsumoAiService`): ogni lettura AI viene
  conteggiata per tenant; superato `Ai:LimiteMensileChiamate` (default 300) il software
  continua con la sola estrazione euristica e lo dice chiaramente nella nota. `0` = illimitato
  (da usare quando il motore è locale e non costa nulla a chiamata).
- Il limite diventa una **leva commerciale**: es. Start 100 letture AI/mese, Pro 500,
  Plus illimitate — coerente col listino dei piani di finanziamento.

## Quanto costa davvero l'AI cloud (perché partire subito così)

Una lettura documento ≈ 3-5.000 token in ingresso + ~500 in uscita. Ordini di grandezza
(prezzi 2026, da riverificare): con DeepSeek/GLM una lettura costa **frazioni di centesimo**;
1.000 letture/mese ≈ **1-3 €**. Anche con 35 clienti attivi (obiettivo anno 1 del piano) il
costo AI totale è di qualche decina di euro al mese, contro canoni da €149-699: **il margine
non è il problema — il problema sarebbe solo un abuso, ed è ciò che il contatore impedisce.**

## AI locale: il percorso per azzerare i costi API

Il provider `Compatibile` parla con qualsiasi endpoint OpenAI-compatibile, quindi anche con
un modello **open source servito da te**. Passare da cloud a locale = cambiare 2 righe di
config, zero codice.

### Fase L1 — Prova in locale (subito, gratis, sul tuo Mac)

1. Installa [Ollama](https://ollama.com) → `ollama pull qwen3:14b` (o `llama3.1:8b`).
2. Config: `"Ai": { "Provider": "Compatibile", "BaseUrl": "http://localhost:11434/v1",
   "Modello": "qwen3:14b", "LimiteMensileChiamate": 0 }`.
3. Misura la qualità sull'estrazione visure/UNILAV con il banco di prova (Fase C del
   PIANO-AI-FIRST): per l'estrazione di campi da documenti i modelli 8-14B sono spesso
   sufficienti — è un compito di lettura, non di ragionamento.

### Fase L2 — Server AI di produzione (quando i clienti arrivano)

Opzioni in ordine di costo:

| Opzione | Costo indicativo | Note |
|---|---|---|
| **GPU VPS noleggiata** (es. Hetzner GX/Contabo GPU, RTX 4000-classe) | ~€150-250/mese | Zero hardware da gestire; Ollama o vLLM sopra; scalabile |
| **Macchina propria** (1× RTX 4090 usata/nuova + PC) | ~€1.800-2.500 una tantum + corrente | Ammortizzo in ~1 anno vs VPS; sta in ufficio; serve UPS e banda |
| **Runtime serverless GPU** (RunPod/Modal, paghi al secondo) | variabile | Buono per picchi, latenza di avvio |

Software consigliato in produzione: **vLLM** (throughput alto, OpenAI-compatibile nativo)
oppure Ollama se il volume è basso. Il backend punta `Ai:BaseUrl` al server GPU: fine.

### Fase L3 — Privacy come argomento di vendita

Con l'AI locale **i documenti dei clienti non escono mai dalla tua infrastruttura**: niente
trasferimento dati a fornitori USA/Cina, DPA più semplice, GDPR molto più difendibile.
Da mettere nero su bianco nel materiale commerciale (i piani lo promettono già: "DPA
fornitori per dati sensibili").

## Percorso consigliato (ibrido, a rischio zero)

1. **Oggi**: chiave DeepSeek centrale (5 minuti, pochi €/mese) + limiti per piano → l'AI
   inclusa è vendibile da subito nei piloti.
2. **In parallelo**: prova Ollama sul Mac (L1) e misura la qualità col banco documenti.
3. **A 10+ clienti attivi**: accendi il server GPU (L2), sposta `BaseUrl`, alza i limiti
   (`0` = illimitato) e vendi la privacy totale (L3). Il cloud resta come riserva.

## Regole (invariate)

L'AI legge e propone soltanto; il parser deterministico vince; conferma umana obbligatoria;
col motore spento o oltre il limite il prodotto resta completo. Chiavi mai nel repository:
solo appsettings di produzione o variabili d'ambiente.
