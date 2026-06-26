# Paghe SaaS — Software buste paga & contabilità per micro-imprese

SaaS multi-tenant per l'elaborazione di buste paga, adempimenti (UniEmens, F24, CU)
e contabilità del personale, pensato per **micro e piccole imprese** italiane.

> ⚠️ **Dominio ad alta complessità normativa.** Il calcolo di una busta paga corretta
> dipende dal CCNL applicato, dalle aliquote INPS/INAIL, dagli scaglioni IRPEF e dalle
> addizionali regionali/comunali, che **cambiano ogni anno**. Tutto ciò che è "parametro
> normativo" è tabellato per anno di validità, mai cablato nel codice.
> È **indispensabile** avere a bordo un Consulente del Lavoro come domain expert.

## Stato del progetto

- **Fase 1 — Modello dati e architettura** ✅
  - Architettura applicativa (`docs/ARCHITETTURA.md`)
  - Modello dati PostgreSQL (`db/schema.sql`, descritto in `docs/MODELLO-DATI.md`)
- **Fase 2 — Motore di calcolo cedolino (lordo → netto)** ✅
  - Implementazione .NET 10 / C# in `backend/` (vedi `backend/README.md`)
  - CCNL Commercio, contributi INPS, IRPEF + detrazioni, addizionali, TFR
  - **21 test xUnit verdi** + demo console che stampa cedolini completi
- **Fase 3 — API REST** ✅
  - ASP.NET Core (`backend/src/Paghe.Api`) che espone il motore via HTTP/JSON
  - Endpoint: livelli CCNL e calcolo cedolino, con gestione errori (400/404)
- **Fase 4 — Fatturazione e IVA** ✅
  - Modulo fatture attive: calcolo imponibili/IVA per aliquota, totali documento
  - Costi/spese con IVA detraibile
  - Generatore **FatturaPA XML** (subset FPR12, standard pubblico Agenzia Entrate)
- **Fase 5 — Bilancio SRL** ✅
  - Riclassificazione del piano dei conti nello schema civilistico **CEE**
    (Stato Patrimoniale art. 2424 + Conto Economico art. 2425)
  - Calcolo del risultato d'esercizio e verifica di **quadratura Attivo = Passivo**
- **Fase 6 — Persistenza dati (EF Core)** ✅
  - Database via Entity Framework Core: **SQLite** in sviluppo, **PostgreSQL** in produzione
  - Salvataggio aziende e **fatture con numerazione automatica** per azienda/anno
  - Isolamento **multi-tenant** verificato con test di integrazione su SQLite reale
- **Fase 7 — Documenti PDF base** ✅
  - Generazione PDF di cedolino e fattura di cortesia in `backend/src/Paghe.Documenti`
  - Endpoint API per scaricare PDF calcolati o persistiti
  - **67 test xUnit verdi** complessivi (calcoli, DB, PDF, validazione XSD FatturaPA)
- **Ricerca GitHub per riuso** ✅
  - Shortlist tecnica e licenze in `docs/GITHUB_REPOS_RIUSO.md`
  - Istruzioni operative per Claude/Codex in `CLAUDE.md`
- **Partita IVA, Camera di Commercio e contabilita IVA** ✅
  - Pratiche apertura P.IVA / Comunicazione Unica / Camera di Commercio
  - Tracking adempimenti Agenzia Entrate, Registro Imprese, INPS, INAIL, SUAP
  - Spese/fatture passive e liquidazioni IVA persistite per azienda
  - Calcolo liquidazione IVA vendite/acquisti da dati inseriti o persistiti
  - **74 test xUnit verdi** complessivi

### Avvio rapido

```bash
cd backend
dotnet test Paghe.slnx -c Release                # 74 test
dotnet run --project src/Paghe.Demo -c Release   # stampa due cedolini di esempio
dotnet run --project src/Paghe.Api  -c Release   # API REST su http://localhost:5171
```

## Perimetro MVP (Fase pilota)

Per arrivare a un prodotto credibile in tempi ragionevoli, l'MVP copre:

- **1 CCNL pilota** (es. Commercio / Terziario)
- Rapporto a **tempo indeterminato**, full-time e part-time
- Calcolo cedolino **lordo → netto** (contributi INPS, IRPEF, addizionali, detrazioni)
- Mensilità aggiuntive (13ª, 14ª)
- TFR (accantonamento + rivalutazione)
- Generazione **cedolino PDF** e **Libro Unico del Lavoro (LUL)**
- Generazione flusso **UniEmens**, delega **F24**, **Certificazione Unica (CU)**
- Scritture di **contabilità del personale** (partita doppia)

Fuori dall'MVP (fasi successive): multi-CCNL esteso, apprendistato, contratti a termine
complessi, somministrazione, 770, cassa integrazione, agricoli (DMAG).

## Struttura repository

```text
.
├── README.md
├── docs/
│   ├── ARCHITETTURA.md      # Architettura, multi-tenancy, motore di calcolo, sicurezza
│   ├── PARTITA_IVA_CONTABILITA.md
│   └── MODELLO-DATI.md      # Descrizione delle entità e relazioni
├── db/
│   └── schema.sql           # Schema PostgreSQL (DDL)
└── backend/                 # Motore di calcolo cedolino (.NET 10 / C#)
    ├── src/Paghe.Core/      # Dominio + motore di calcolo
    ├── src/Paghe.Documenti/ # Generazione PDF cedolini/fatture
    ├── src/Paghe.Demo/      # Demo console
    └── tests/Paghe.Tests/   # Test xUnit
```
