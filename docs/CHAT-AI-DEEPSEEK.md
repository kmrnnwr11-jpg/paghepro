# Aurora: la chat con l'AI (DeepSeek)

Aurora è l'assistente a chat dentro PaghePro. Non è più un elenco di controlli fissi: è un
modello linguistico vero, che parla, legge l'archivio dello studio con i propri strumenti e
prepara le operazioni. **Le scritture non partono mai da sola: le confermi tu con un bottone.**

## 1. Accendere l'AI (una volta sola)

Serve una chiave del provider. Con DeepSeek: <https://platform.deepseek.com> → *API keys* →
crea la chiave (comincia con `sk-`) e ricarica qualche euro di credito.

**Sull'app online (paghepro.onrender.com)** il provider è già impostato su DeepSeek in
[`render.yaml`](../render.yaml): manca soltanto la chiave, che non può stare nel repository.

> Render → servizio **paghepro** → **Environment** → la voce `Ai__ApiKey` (vuota) → incolla la
> chiave → **Save**. Il servizio si riavvia da solo, un paio di minuti.

**In locale** (Mac), dalla cartella `backend`:

```bash
Ai__Provider=DeepSeek Ai__ApiKey=sk-... dotnet run --project src/Paghe.Api -c Release
```

La chiave **non va scritta nei file del progetto**: finirebbe su GitHub.

Controllo: apri Aurora, in alto deve comparire `deepseek-chat` invece di «da configurare».
Oppure chiama `GET /api/ai/chat/stato`. Finché la chiave manca la chat resta spenta di
proposito e lo scrive: meglio dirlo prima che prendere un errore a ogni messaggio.

### Altri provider

Lo stesso canale funziona con qualsiasi endpoint OpenAI-compatibile:

| Provider | `Ai__Provider` | Note |
| --- | --- | --- |
| DeepSeek | `DeepSeek` | default `deepseek-chat`, economico |
| GLM / Zhipu | `Glm` | default `glm-4-flash` |
| Ollama sul tuo server | `Compatibile` | + `Ai__BaseUrl=http://localhost:11434/v1` e `Ai__Modello=qwen2.5:14b` — nessun dato esce dalla tua macchina |

`Ai__ModelloChat` cambia solo il modello della chat (la lettura documenti resta su `Ai__Modello`).

## 2. Come si usa

Scrivi come parleresti a un collega e incolla i dati che hai:

> registra questa azienda: Panificio Aurora Srl, P.IVA 12345678903, Via Torino 14, 20123
> Milano MI, pec aurora@pec.it

Aurora compila la scheda, ti mostra **cosa ha messo** e **cosa manca** (con il perché: la
matricola INPS serve per UniEmens, il codice catastale per le addizionali IRPEF…) e aspetta.
Tu premi *Conferma ed esegui* — o le dai i dati mancanti e la fai rifare.

Vale lo stesso per l'assunzione di un dipendente e per la correzione di un'anagrafica.

## 3. Cosa può e cosa non può

Legge da sola (nessuna conferma): elenco aziende, scheda azienda con i dati mancanti,
dipendenti di un'azienda, scadenze dello studio.

Scrive solo dopo la tua conferma: registrare un'azienda, correggere un'azienda, assumere un
dipendente.

Regole scritte nelle istruzioni del modello:

- non inventa P.IVA, codici fiscali, matricole INPS, P.A.T. INAIL, ATECO, IBAN, CCNL o livelli:
  se non glieli hai dati, li chiede;
- i calcoli di cedolini, contributi e imposte restano al motore del programma;
- vede solo i dati del tuo studio (il tenant arriva dal token, non dal modello).

Le stesse protezioni del programma valgono anche quando scrive Aurora: niente doppioni di
P.IVA o codice fiscale, niente buste paga di mesi futuri, niente dipendente assunto due volte
nella stessa azienda.

## 4. Da sapere prima di usarla su clienti veri

- **I dati che Aurora legge escono verso il provider.** Con DeepSeek finiscono su server in
  Cina. Per lavorare su dati reali di clienti la strada pulita è un modello locale (Ollama sul
  tuo server, provider `Compatibile`): stessa chat, nessun dato fuori.
- Il consumo è conteggiato per studio nel pannello AI, ma **non blocca** la chat.
- Il motore dei cedolini resta **BOZZA DA VALIDARE**: l'assistente non cambia questo stato.

## 5. Dove sta nel codice

| Cosa | File |
| --- | --- |
| Dialogo con il provider (function calling) | `backend/src/Paghe.Api/AiChatClient.cs` |
| Strumenti (letture + scritture proposte) | `backend/src/Paghe.Api/AiChatStrumenti.cs` |
| Endpoint `/api/ai/chat`, `/api/ai/chat/azione`, `/api/ai/chat/stato` | `backend/src/Paghe.Api/AiChatEndpoints.cs` |
| Pannello a chat | `frontend/src/Aurora.tsx` |
| Test | `backend/tests/Paghe.Tests/AiChatTests.cs` |

Il Governatore AI (mandati, quote, lease) resta nel codice e nei suoi endpoint
`/api/ai/governor/*`, ma la chat non ci passa più.
