# Guida: collegare la Fatturazione Elettronica (da soli, passo-passo)

Questa guida serve all'**utente finale** (azienda o commercialista) per collegare l'app a un
canale che invia davvero le fatture al Sistema di Interscambio (SdI), **senza aiuto tecnico**.

> In breve: l'app **prepara e controlla** la fattura (XML valido). Per **inviarla davvero**
> bisogna collegarla una volta sola a un "postino accreditato" (un provider). Sono ~15 minuti.

---

## Prima di iniziare: cosa ti serve
- La **PEC** dell'azienda.
- **SPID/CIE** dell'azienda o del titolare (per iscriverti ai portali).
- Una **carta** o IBAN per l'abbonamento al provider (pochi euro/mese o a consumo).
- 15 minuti.

---

## PASSO 1 — Scegli con chi collegarti
Hai due strade. Per quasi tutti conviene la **Strada A**.

| | Strada A: un Provider | Strada B: canale proprio |
|---|---|---|
| Cos'è | Un servizio accreditato che fa da postino | Ti accrediti tu con l'Agenzia Entrate |
| Tempi | Subito (minuti/giorni) | Mesi |
| Firma inclusa | Sì (firma il provider) | No, devi gestirla tu |
| Per chi | Micro-imprese, studi, la maggior parte | Solo grandi volumi |

**Scegli un provider (Strada A).** I più usati in Italia:

- **Aruba Fatturazione Elettronica** — diffusissimo, economico, assistenza in italiano.
- **Fatture in Cloud (TeamSystem)** — molto usato da micro-imprese e studi.
- **Acube / openapi.it** — pensato per chi integra via software (come questa app).

> 💬 **Da chiedere al commercialista (1 domanda):**
> *"Per le fatture elettroniche mi appoggio a un provider come Aruba o Fatture in Cloud, oppure
> tu hai già un canale tuo dove mando i file?"*
> Se dice "ho il mio canale", salta al **PASSO 5** (gli mandi i file pronti dall'app).

---

## PASSO 2 — Crea l'account dal provider
1. Vai sul sito del provider scelto e registrati con i dati dell'azienda (P.IVA, PEC, SPID).
2. Attiva il servizio **"Fatturazione Elettronica"** (e, se richiesto, **"API"** o **"accesso per software/sviluppatori"**).
3. Verifica che sia incluso l'**invio a SdI con firma** (di solito sì).

Riferimenti rapidi:
- Aruba → cerca *"Aruba fatturazione elettronica"* → piano base.
- Fatture in Cloud → *"Fatture in Cloud API"* (serve un piano che include le API).
- Acube → *"Acube fatturazione elettronica API"* → registrazione sviluppatore.

---

## PASSO 3 — Prendi le credenziali (la "chiave")
Nel pannello del provider cerca la sezione **API / Sviluppatori / Integrazioni** e copia:

- l'**API Key** (o **Token**), **oppure**
- la coppia **Client ID + Client Secret** (alcuni provider usano questa).

Annota anche l'**indirizzo base delle API** (lo trovi nella loro documentazione, es. `https://api.provider.it/...`).

> 🔒 Tratta queste credenziali come una password: non condividerle via email/chat.

---

## PASSO 4 — Inseriscile nell'app
1. Apri l'app → sezione **Fatturazione** → **"Collega provider"** (Impostazioni fatturazione).
2. Scegli il provider dall'elenco.
3. Incolla **API Key** (o Client ID + Secret) e l'indirizzo base se richiesto.
4. Premi **Salva** e poi **Verifica connessione**: l'app fa una prova e ti dice se la chiave funziona. ✅

> ℹ️ **Stato attuale:** la schermata di collegamento self-service è in fase di completamento
> (il supporto ai provider è in aggiunta nell'app). Finché non è disponibile, puoi:
> usare la **modalità Simulata** per provare tutto il flusso, **oppure** passare le credenziali
> a chi gestisce il server, che le inserisce nella configurazione (sezione `Sdi`). Appena la
> schermata è pronta, questo passo lo farai interamente da solo.

---

## PASSO 5 — Fai una prova prima di usarlo sul serio
1. In **Fatturazione** crea una fattura di prova (cliente di test).
2. Premi **Trasmetti**.
3. Controlla lo stato: deve passare a **Inviata** → poi **Consegnata (RC)** o, se ci sono errori,
   **Scartata (NS)** con il motivo.

> 🧪 **Consiglio:** prova prima in **modalità Simulata** (nell'app): riproduce invio → ricevuta →
> stato senza consumare invii reali né credito del provider. Quando funziona, passa al provider vero.

Se il commercialista usa un **canale proprio** (Strada B): in Fatturazione scarica il **file pronto**
(XML/`.p7m`) e gli mandi quello — lui lo invia dal suo gestionale.

---

## Se qualcosa non va (problemi comuni)
| Messaggio | Cosa significa | Cosa fare |
|---|---|---|
| "Chiave non valida / 401" | API Key sbagliata o servizio API non attivo | Ricontrolla la chiave; attiva le API nel pannello provider |
| "Scartata (NS) 00xxx" | La fattura ha un errore formale | Apri il dettaglio errore, correggi il dato indicato, ritrasmetti |
| "Errore di rete" | Indirizzo base errato o provider non raggiungibile | Verifica l'URL base dalla doc del provider |
| Nessuna ricevuta dopo l'invio | Lo SdI ci mette da minuti a 5 giorni | Premi **Aggiorna esito** più tardi |

---

## Riepilogo in 5 righe (da tenere a portata)
1. Scegli un provider (Aruba / Fatture in Cloud / Acube).
2. Crea l'account e attiva "Fatturazione + API".
3. Copia l'**API Key** dal pannello.
4. Incollala nell'app → **Collega provider** → **Verifica connessione**.
5. Fai una **fattura di prova** (prima in Simulato, poi reale).
