# G1 — Rapporto sulle fondamenta local-first

> Versione: 16 luglio 2026 · Stato: **GATE APERTO — completato in parte, con prove reali**
> Riferimento: [`../PIANO-AI-LIBERA-LIMITI-USO.md`](../PIANO-AI-LIBERA-LIMITI-USO.md) §12, Fase G1.
> Questo rapporto separa ciò che è già vero e PROVATO oggi da ciò che richiede il guscio
> desktop del profilo personale (che non esiste ancora nel repo).

## 1. Stato delle otto caselle G1

| Casella del piano | Stato | Evidenza |
|---|---|---|
| Backup locale cifrato e **restore provato** | ✅ **PROVATO il 16/07/2026** | vedi §2: fail-closed senza chiave, backup `age` con permessi `0600`, nessun dato in chiaro nel file, ripristino bit-per-bit identico |
| **Interruttore AI** e modalità manuale completa | ✅ **PROVATO il 16/07/2026** | `GET /api/ai/stato` → `provider: Spento, attivo: false`; con AI spenta il motore ha calcolato un cedolino reale (lordo 2.648,52 € → netto 1.967,95 €, CCNL Chimico-Farmaceutico A1) |
| Database e vault cifrati con Keychain/recovery | 🟡 PARZIALE | i campi sensibili sono già cifrati at-rest AES-256-GCM con AAD e rotazione (Fase 3 GDPR, 528 test verdi); la cifratura dell'INTERO database con chiave in Keychain e recovery è compito del guscio desktop |
| Migrazioni e rollback senza perdita dati | 🟡 PARZIALE | infrastruttura EF pronta (`PagheDbContextFactory`, `dotnet ef` funzionante); lo snapshot iniziale è rinviato di proposito: lo schema è in evoluzione attiva (lavoro F3 audit/retention in corso sullo stesso repo) — generarlo ora produrrebbe una migrazione subito obsoleta |
| Processi PersonalDesktop / Bridge / Host / Worker separati | ⬜ DA FARE | richiede il guscio desktop macOS: oggi il prodotto è un backend ASP.NET + SPA |
| XPC/socket Unix autenticato, nessuna porta TCP | ⬜ DA FARE | idem (oggi: HTTP locale 5171 per natura dell'app attuale) |
| Sandbox e assenza entitlement rete sul data path AI | ⬜ DA FARE | idem; nota: oggi NESSUN processo AI esiste (provider `Spento`), quindi non c'è data path AI da isolare |
| Updater separato e supply chain firmata | ⬜ DA FARE | idem |

**Criterio d'uscita del gate** («zero file-esca letti, zero egress, restore riuscito, nessun
processo AI con credenziali»): il *restore riuscito* è provato; le prove di isolamento
(file-esca, egress) hanno senso solo quando esisterà il processo AI locale da misurare.
**Quindi G1 NON è chiuso** e le sue caselle nel piano restano vuote.

## 2. Verbale delle prove eseguite (riproducibili)

Ambiente: macOS, `age`/`age-keygen` Homebrew, `sqlite3` di sistema, script del repo
`scripts/backup-db.sh`, DB di prova usa-e-getta (2 tabelle, dati finti) in scratchpad.

1. **Fail-closed**: esecuzione SENZA `PAGHE_BACKUP_AGE_RECIPIENT` → uscita 1 con messaggio
   «backup annullato, nessun file in chiaro creato»; nessun file prodotto. ✅
2. **Backup cifrato**: con chiave pubblica age di prova → `paghe-20260716-….db.gz.age`,
   cartella `0700`, file `0600`; `strings` sui primi 200 byte non rivela CF né nomi tabella. ✅
3. **Restore**: `age -d` con la chiave privata + `gunzip` → `PRAGMA integrity_check = ok`;
   `diff` fra dump SQL dell'originale e del ripristinato: **identici**; query di controllo
   (`COUNT`, `SUM`) corrette. ✅
4. **Interruttore AI**: config di default → `{"provider":"Spento","attivo":false}`; calcolo
   cedolino via `POST /api/cedolini/calcola` riuscito con AI spenta. ✅

## 3. Cosa serve per chiudere G1 (decisione di prodotto)

Le quattro caselle ⬜ sono un unico lavoro: **il guscio desktop del profilo personale**
(app macOS con processi separati, IPC locale, sandbox senza rete sul data path AI, vault
Keychain, updater firmato). È un progetto a sé, da pianificare come tale.

Osservazione architetturale per la decisione: i componenti del Gate **G2 (Usage Governor:
catalogo, policy, grant/lease, evaluator, broker, contatori, audit, UI barra/preflight)** sono
**identici nei due profili** (desktop locale o app attuale) e non toccano alcun data path AI —
oggi non esiste alcun modello in esecuzione. Costruirli nell'app attuale non riduce l'isolamento
promesso da G1, purché resti vero il vincolo: **nessun modello AI viene acceso prima che
l'isolamento G1 sia provato** (o, per il profilo server «AI inclusa», prima di un piano
equivalente per il server).

Le due strade possibili, entrambe coerenti con lo spirito del piano:

- **A. Ordine stretto**: costruire prima il guscio desktop (G1 completo), poi G2. Più fedele
  alla lettera; tempi lunghi prima di vedere il Copilot governato.
- **B. G2 anticipato senza modello** (raccomandata): costruire e testare il Governor nell'app
  attuale con capability solo deterministiche/mock, mantenendo il vincolo «modello acceso solo
  dopo isolamento provato». Richiede una riga di emendamento al piano §12, decisa dal fondatore.

La scelta spetta al fondatore ed è registrata qui sotto.

## 4. Decisione del fondatore

- Data: 16 luglio 2026
- Scelta: **B — G2 anticipato senza modello** (Usage Governor costruito e testato nell'app
  attuale con capability solo deterministiche/mock).
- Nota: vincolo confermato — **nessun modello AI viene acceso prima che l'isolamento G1 sia
  provato** (guscio desktop per il profilo personale, o piano equivalente lato server per il
  profilo «AI inclusa»). Le 4 caselle desktop di G1 restano aperte e vincolanti.
