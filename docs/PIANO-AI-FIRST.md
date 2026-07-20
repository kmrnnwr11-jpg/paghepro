# Piano AI-first: allineare PaghePro ai piani di finanziamento

Obiettivo del fondatore: presentare PaghePro come "console AI-first" (piano banca €150k e
piano Invitalia Smart&Start €200k) e rendere il software coerente con quella promessa:
**documento → estrazione (parser deterministico, AI solo dove serve) → validazione con
confidenza → conferma umana**. Questo documento mappa le promesse dei piani sullo stato
reale del software e definisce le fasi per chiudere i buchi.

## La promessa dei piani, punto per punto

| Promessa nelle slide | Stato nel software | Gap |
|---|---|---|
| «Azienda 360 da visura» | ✅ Visura PDF/foto → OCR/PdfPig → parser euristico → form precompilato da confermare | — |
| «Dipendente 360 da UNILAV» | ✅ UNILAV XML e PDF → dipendente+rapporto; correzione scheda dopo l'import | Proposta *prima* del salvataggio (oggi salva e poi si corregge) |
| «Parser deterministico o AI solo dove serve» | ✅ **Fatto (15/07/2026)**: livello AI pluggabile — l'euristica vince, l'AI riempie solo i campi vuoti | Estendere oltre la visura (UNILAV, fatture, DURC) |
| «Validazione: campi, duplicati e confidenza» | ✅ Confidenza per campo con soglia configurabile; duplicati P.IVA/CF per tenant già bloccati | Punteggio di confidenza anche per l'euristica |
| «Conferma umana obbligatoria» | ✅ Regola architetturale: l'AI propone, l'utente conferma; calcoli solo in codice deterministico | — |
| «AI senza decisioni fiscali» | ✅ Motore cedolino/F24/CU è 100% deterministico e versionato per anno | — |
| «Audit log, tenant isolation, GDPR» | ✅ Registro applicativo esteso, isolamento tenant, cifratura IBAN, diritto all'oblio | Retention e immutabilità/audit infrastrutturali |
| «Test su 500 documenti reali e precisione misurata» (gate mese 5) | ❌ | Costruire il banco di misura (vedi Fase C) |
| «Interfaccia side-by-side documento/dati» | Parziale (form precompilato + nota provenienza) | UI con documento affiancato ai campi estratti |

## Com'è fatto il livello AI (deciso e costruito)

- **Seam neutro** `IEstrattoreAiDocumenti` in `Paghe.Core/Ai/`: il resto del software non sa
  quale AI c'è dietro. Default **Spento**: zero chiamate esterne, tutto funziona come prima.
- **Provider a scelta dell'utente** (sezione config `Ai`), nessun lock-in:
  - `Claude` — SDK ufficiale Anthropic, structured output (JSON garantito da schema);
  - `DeepSeek` / `Glm` — endpoint OpenAI-compatibili con default già impostati;
  - `Compatibile` — qualsiasi endpoint OpenAI-compatibile (`BaseUrl`+`Modello`), incluso
    **Ollama in locale** (gratis, i documenti non escono dal computer — argomento GDPR forte).
- **Contratto comune**: l'AI restituisce `{campi:[{nome, valore, confidenza}]}`; la fusione
  (`CompletamentoVisuraAi`) fa vincere sempre il parser deterministico, usa l'AI solo per i
  campi vuoti e solo sopra `SogliaConfidenza` (default 0,6). La nota di risposta elenca i
  campi proposti dall'AI, così l'utente sa cosa controllare con più attenzione.
- **Robustezza**: errore/timeout del provider ⇒ si resta sull'euristica, mai un blocco.
- Config esempio:

```json
"Ai": { "Provider": "DeepSeek", "ApiKey": "sk-...", "SogliaConfidenza": 0.6 }
```

## Fasi successive (in ordine di valore per i piloti)

| Fase | Cosa | Effort |
|---|---|---|
| A | **UNILAV in modalità proposta**: l'import mostra i dati estratti (euristica+AI) da confermare PRIMA di creare dipendente+rapporto, come già fa la visura | 1–2 g |
| B | **UI side-by-side**: documento a sinistra, campi a destra con badge di provenienza (euristica/AI) e confidenza; il pezzo demo dei piloti | 2–3 g |
| C | **Banco di misura precisione** (gate "500 documenti, precisione misurata" del mese 5): cartella di documenti reali anonimizzati + script che confronta estratto vs atteso e produce il report campo per campo | 2 g |
| D | **Altri documenti**: fatture fornitore (PDF non SdI), DURC, F24 quietanzati → stesso seam, stessi pattern | 2–3 g per tipo |
| E | **Assistente a chat esteso**: la chat delle Pratiche ComUnica (oggi euristica) passa dal seam AI per capire testo libero; stessa conferma umana | 1–2 g |

## Cosa serve dal fondatore (non è codice)

1. **Chiave API di un provider** (o Ollama installato in locale): senza, il livello resta
   Spento — il software funziona comunque, solo senza il riempimento AI dei campi mancanti.
2. **Documenti reali per il banco di misura** (visure/UNILAV vari, anche fotografati male):
   sono il carburante della Fase C e la prova per Invitalia.
3. **Requisito Smart&Start**: la domanda richiede lo stato di **startup innovativa**
   (slide 12 del piano) — verifica societaria da fare col commercialista; il software può
   solo documentare il requisito di innovazione (questo piano + il registro delle regole
   versionate aiutano l'istruttoria).

## Regole fisse (da non violare mai, sono la tesi dei piani)

1. L'AI **legge e propone**; non salva, non invia, non calcola imposte.
2. Ogni valore proposto dall'AI è **marcato come tale** e passa dalla conferma umana.
3. Il parser deterministico ha **sempre la precedenza** sull'AI.
4. Col livello AI spento il prodotto resta completo: l'AI è un acceleratore, non una dipendenza.
