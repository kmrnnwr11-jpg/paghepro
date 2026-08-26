# Guida per chi collabora a PaghePro

Benvenuto/a! Questa guida contiene tutto quello che serve per iniziare a lavorare
su questo progetto. Seguila dall'inizio alla fine la prima volta; poi ti servirà
solo la sezione «Il ritmo di lavoro quotidiano».

## Cos'è questo progetto

SaaS italiano per buste paga, fatturazione e contabilità per micro-imprese.

- **Backend**: .NET 10 / C#, ASP.NET Core Minimal API (`backend/`)
- **Frontend**: React + TypeScript + Vite (`frontend/`)
- **Database**: SQLite in sviluppo, PostgreSQL in produzione
- Il contesto completo per assistenti AI è in `CLAUDE.md` e `AGENTS.md`

## Prima volta: installazione (una tantum)

Servono tre programmi gratuiti:

1. **Git** — <https://git-scm.com>
2. **.NET 10 SDK** — <https://dotnet.microsoft.com/download>
3. **Node.js 22** — <https://nodejs.org>

Poi scarica il progetto (Git chiederà il login GitHub la prima volta):

```bash
git clone https://github.com/kmrnnwr11-jpg/paghe-saas.git
cd paghe-saas
```

### Verifica che tutto funzioni

```bash
# Backend: build + tutti i test (deve finire con 0 non superati)
cd backend
dotnet test Paghe.slnx -c Release

# Frontend
cd ../frontend
npm install
npm test
npm run dev        # apre l'app su http://localhost:5173
```

Per avviare l'API in un altro terminale:

```bash
cd backend
dotnet run --project src/Paghe.Api
```

In sviluppo non servono chiavi o segreti: il backend usa SQLite e la
configurazione di default. **Le chiavi di produzione (Render, Supabase, API AI,
chiave dati) non stanno nel repository e non vanno mai chieste né committate.**

## Il ritmo di lavoro quotidiano

La regola d'oro: **nessuno lavora direttamente su `main`.** Ogni lavoro passa da
un ramo proprio e da una Pull Request.

```bash
# 1. Parti sempre dall'ultima versione
git checkout main && git pull

# 2. Crea il tuo ramo per questo lavoro
git checkout -b feature/nome-del-lavoro

# 3. Lavora e committa (controlla sempre git status prima!)
git status          # occhio alle righe "??": file nuovi da aggiungere
git add <file...>
git commit -m "Descrizione chiara di cosa fa la modifica"

# 4. Pubblica il ramo
git push -u origin feature/nome-del-lavoro
```

Poi vai su GitHub e apri la **Pull Request** verso `main`. Da lì:

1. La **CI parte da sola** ed esegue build e test di backend e frontend.
2. Un'altra persona guarda le modifiche e approva (o chiede correzioni).
3. Il **Merge** si fa solo con la CI verde ✅ e l'approvazione.

## Regole del progetto (importanti)

- **Test verdi prima del merge**: la CI li esegue comunque, ma lanciarli in
  locale prima del push fa risparmiare giri
  (`dotnet test Paghe.slnx -c Release` + `npm test` nel frontend).
- **`git status` prima di ogni commit**: i file nuovi (`??`) dimenticati fuori
  dai commit hanno già rotto un deploy in passato.
- **Importi e aliquote sempre `decimal`**, mai `float`/`double`.
- **Isolamento multi-tenant**: ogni query persistita deve filtrare per
  `tenantId`.
- **Parametri normativi su database** (versionati per anno): mai reintrodurre
  valori cablati nel codice di calcolo.
- **Migrazioni EF**: una modifica al modello richiede la migrazione per
  **entrambi** i provider (SQLite e PostgreSQL — comandi in
  `docs/PRODUZIONE.md` §2). Se stai per fare una migrazione, avvisa gli altri
  prima del merge: due migrazioni parallele fatte alla cieca sono il conflitto
  più fastidioso.
- **Dividetevi le zone**: concordare a voce chi tocca cosa (es. uno frontend,
  uno backend) evita quasi tutti i conflitti.
- **Licenze**: prima di importare codice da GitHub verificare licenza e
  manutenzione (`docs/GITHUB_REPOS_RIUSO.md`); niente AGPL/GPL senza decisione
  esplicita.
- **Il deploy non è automatico**: il merge su `main` non pubblica nulla; la
  pubblicazione su Render è un'azione manuale del fondatore.

## Dove trovare le cose

| Cosa | Dove |
| --- | --- |
| Piano operativo e priorità | `PIANO.md` |
| Contesto per assistenti AI | `CLAUDE.md`, `AGENTS.md` |
| Comandi produzione e migrazioni | `docs/PRODUZIONE.md` |
| Documentazione di dominio | `docs/` |
| Test backend | `backend/tests/Paghe.Tests` |
| Test frontend | `frontend/tests` |

Se qualcosa in questa guida non torna (un comando fallisce, un passaggio è
poco chiaro), segnalalo: la guida si corregge con una Pull Request come tutto
il resto.
