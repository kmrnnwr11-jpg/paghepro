# AGENTS.md — Guida per l'agente AI sul codice

> Istruzioni operative per chi (Claude/Codex o altro agente) sviluppa questo progetto.
> Leggere SEMPRE insieme a: [`PIANO.md`](PIANO.md) (cosa fare e in che ordine) e
> [`CLAUDE.md`](CLAUDE.md) (convenzioni di dominio e stato "Fatto").

---

## 1. Contesto in una riga

SaaS italiano per buste paga, fatturazione e contabilità per micro-imprese, da portare da
MVP a prodotto vendibile ai commercialisti. Il percorso è in `PIANO.md` (9 fasi).

## 2. Cosa fa l'agente vs cosa fa l'utente

- L'agente esegue SOLO le attività marcate 🔧 in `PIANO.md` (codice).
- Le attività 🧑 (aprire account, comprare certificati, firmare contratti) e ⏳ (attese di
  terzi) **NON** le fa l'agente: le indica all'utente, **una per volta, passo-passo**.
- L'utente **non è tecnico**: spiegare in modo semplice, niente gergo non spiegato.

## 3. Come prendere in carico il lavoro

1. Apri `PIANO.md`, trova la **prima casella `[ ]` con 🔧 non spuntata** nella fase corrente.
2. Fai quel pezzo (codice + test).
3. Spunta la casella in `PIANO.md` e aggiorna la sezione "Fatto" di `CLAUDE.md`.
4. Se il prossimo passo è 🧑/⏳, fermati e dì all'utente esattamente cosa fare.

Fasi che si possono fare **subito, solo codice, senza attese**: Fase 2 (fondamenta),
Fase 3 (GDPR), Fase 6 (UniEmens/INAIL). Fase 4/5/7/8 dipendono da account/certificati/kit
dell'utente.

## 4. Regole tecniche invariabili (da `CLAUDE.md`)

- **`decimal`** per importi e aliquote, mai `float`/`double`.
- **Multi-tenant**: ogni query persistita filtra per `tenantId`.
- **Parametri normativi dal DB** versionati per anno: non reintrodurre valori cablati nel calcolo.
- **Niente codice AGPL/GPL** nel SaaS senza decisione esplicita; verificare licenze.
- Output non certificati (cedolini, F24, bilancio, UniEmens…) vanno **etichettati come da
  validare** con un professionista, finché non passano i controlli ufficiali.
- Nuovi moduli "telematici" seguono il pattern esistente: modello in `Paghe.Core`, persistenza
  + macchina a stati in `Paghe.Persistence`, endpoint in `Paghe.Api`, pagina nel frontend.

## 5. Definition of Done (per ogni pezzo 🔧)

Un pezzo è "fatto" solo se:
- [ ] Compila: `cd backend && dotnet build Paghe.slnx -c Release` → 0 errori.
- [ ] Test verdi: `dotnet test Paghe.slnx -c Release` (aggiungere test per il nuovo codice).
- [ ] Frontend (se toccato): `cd frontend && npm run build` ok.
- [ ] Isolamento per tenant rispettato e verificato in un test.
- [ ] `PIANO.md` aggiornato (casella spuntata) e `CLAUDE.md` "Fatto" aggiornato.

## 6. ⚠️ Attenzione: editing concorrente

Questo repository può essere modificato da **più agenti in parallelo**. Prima di toccare file
condivisi (`PagheDbContext.cs`, `Program.cs`, `Dtos.cs`, `Dashboard.tsx`, `api.ts`):
- Rileggi il file appena prima di modificarlo (potrebbe essere cambiato).
- Preferisci **nuovi file/moduli isolati**; tocca i file condivisi solo per il "cablaggio"
  finale, con modifiche piccole e ancorate a stringhe univoche.
- Se il build è rosso per file non tuoi (lavoro altrui incompleto), **non sistemarli in
  silenzio**: segnalalo e verifica il tuo modulo in isolamento.

## 7. Comandi utili

```bash
# Backend: build + test
cd backend && dotnet build Paghe.slnx -c Release
cd backend && dotnet test  Paghe.slnx -c Release

# Backend: avvio API (porta 5171) — smoke test manuale
cd backend && dotnet run --project src/Paghe.Api -c Release

# Frontend: build di verifica e dev server
cd frontend && npm run build
cd frontend && npm run dev
```

## 8. Mappa codice → fasi del piano

| Fase | Dove si lavora (orientativo) |
|---|---|
| 2 Fondamenta | `Paghe.Persistence` (Migrations), `Paghe.Api/Program.cs`, config segreti, deploy |
| 3 GDPR | `Paghe.Persistence` (cifratura campi, entità `AuditLog`), value converters EF |
| 4 SdI reale | `Paghe.Persistence/Trasmissione/SdiGatewayAcube.cs`, config `Sdi`/`Acube` |
| 5 Firma reale | `Paghe.Firma`, config sezione `Firma` |
| 6 UniEmens/INAIL | nuovo `Paghe.Core/AdeTelematico/UniEmens/` + service + endpoint |
| 7 Controlli AdE | `Paghe.Core/AdeTelematico` (CU/LIPE/770) + wrapper JAR ufficiali |
| 8 Conservazione | nuovo adapter verso provider di conservazione |
| 9 Validazione | correzioni puntuali dal feedback del commercialista pilota |

## 9. Stile

- Codice e commenti in **italiano**, coerenti con il resto del repo.
- Commenti che spiegano il *perché* normativo, non il *cosa* ovvio.
- Test xUnit in `backend/tests/Paghe.Tests`, sullo stile dei file esistenti.
