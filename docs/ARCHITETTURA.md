# Architettura — Paghe SaaS

## 1. Principi guida

1. **Separazione netta tra dati anagrafici e parametri normativi.**
   Aliquote, scaglioni, detrazioni, addizionali sono *parametri con validità temporale*
   (anno/mese), versionati. Mai costanti nel codice.
2. **Motore di calcolo a regole, non logica cablata.**
   Le regole del CCNL e del calcolo cedolino sono dati configurabili, non `if` sparsi.
3. **Tutto è ricalcolabile e tracciabile.**
   Un cedolino deve poter essere rielaborato e deve conservare *come* è stato calcolato
   (audit del calcolo), perché le verifiche fiscali guardano indietro nel tempo.
4. **Multi-tenant fin dall'inizio.** Isolamento dei dati per tenant a livello di riga.

## 2. Architettura applicativa (alto livello)

```
┌────────────────────────────────────────────────────────────────┐
│                     Frontend SPA (React)                        │
│      Anagrafiche · Presenze · Cedolini · Adempimenti · Conta.  │
└───────────────────────────┬────────────────────────────────────┘
                            │ HTTPS / REST + JWT
┌───────────────────────────▼────────────────────────────────────┐
│                     API Gateway / Backend                       │
│                   (Java Spring Boot o .NET)                     │
│                                                                 │
│  ┌──────────────┐ ┌──────────────┐ ┌─────────────────────────┐ │
│  │  Anagrafiche │ │  Presenze    │ │  MOTORE DI CALCOLO PAGA │ │
│  │  (aziende,   │ │  (cartellino)│ │  lordo→netto, contributi│ │
│  │   dipendenti)│ │              │ │  IRPEF, addizionali,TFR │ │
│  └──────────────┘ └──────────────┘ └─────────────────────────┘ │
│  ┌──────────────┐ ┌──────────────┐ ┌─────────────────────────┐ │
│  │ Adempimenti  │ │ Contabilità  │ │ Documenti (PDF/LUL,     │ │
│  │ UniEmens/F24 │ │ partita      │ │ conservazione a norma)  │ │
│  │ CU           │ │ doppia       │ │                         │ │
│  └──────────────┘ └──────────────┘ └─────────────────────────┘ │
└───────────────────────────┬────────────────────────────────────┘
                            │
        ┌───────────────────┼────────────────────┐
        ▼                   ▼                    ▼
┌───────────────┐  ┌─────────────────┐  ┌──────────────────┐
│  PostgreSQL   │  │ Object storage  │  │  Code asincrone  │
│  (dati +      │  │ (PDF, flussi,   │  │  (generazione    │
│   parametri)  │  │  conservazione) │  │   massiva, invii)│
└───────────────┘  └─────────────────┘  └──────────────────┘
```

## 3. Multi-tenancy

**Modello scelto: shared database, shared schema con `tenant_id` su ogni tabella.**

- Ogni riga porta `tenant_id`. Tutte le query filtrano per tenant.
- Consigliato attivare **Row-Level Security (RLS)** di PostgreSQL: il backend imposta
  `SET app.current_tenant = '<uuid>'` a inizio transazione, e le policy RLS garantiscono
  l'isolamento anche in caso di bug applicativo.
- `tenant` = account che paga il SaaS. Un tenant può gestire **una o più aziende**
  (datori di lavoro) — utile per il micro-commercialista o l'imprenditore con più P.IVA.

Evoluzione futura per clienti grandi: schema-per-tenant o database dedicato.

## 4. Il motore di calcolo del cedolino

È il cuore del sistema. Flusso di elaborazione mensile per ciascun rapporto di lavoro:

```
Input:  rapporto + cartellino presenze + parametri normativi del periodo
   │
   1. Determina retribuzione di base (minimo CCNL del livello + scatti + voci fisse)
   2. Applica presenze: ordinario, straordinario, ferie/permessi, malattia, ecc.
   3. Calcola imponibile previdenziale → contributi INPS (quota dipendente + datore)
   4. Determina imponibile fiscale (lordo − contributi c/dipendente)
   5. IRPEF lorda per scaglioni → detrazioni → IRPEF netta
   6. Addizionali regionale e comunale (su imponibile, con regole rateazione)
   7. Altre trattenute (sindacali, cessioni, conguagli)
   8. Netto in busta
   9. Accantonamento TFR (quota annua / 13,5 + rivalutazione)
   │
Output: cedolino (testata + righe voci) + dati per UniEmens, F24, contabilità
```

**Design del motore:**
- Catalogo **voci di paga** (`voce_paga`): ogni voce dichiara se è competenza/trattenuta
  e come incide su imponibile INPS / imponibile IRPEF / TFR.
- Il calcolo produce un `cedolino` con righe `cedolino_riga` immutabili una volta chiuso.
- Le **regole CCNL** (maggiorazioni straordinario, scatti, mensilità aggiuntive) sono
  tabellate e versionate per data.
- Implementabile con un rules engine (es. Drools) o un DSL interno valutato dal backend.
  Per l'MVP va bene un motore procedurale ben isolato dietro un'interfaccia
  `MotoreCalcoloCedolino`, da rendere configurabile in seguito.

## 5. Parametri normativi versionati

Tabelle con `anno` / `valido_dal` / `valido_al`:
`inps_aliquota`, `inail_tasso`, `irpef_scaglione`, `irpef_detrazione_param`,
`addizionale_regionale`, `addizionale_comunale`, `tfr_coeff_rivalutazione`,
`ccnl_livello` (minimi tabellari per data).

Aggiornamento annuale = inserimento di nuove righe, **mai update distruttivo**: i cedolini
storici devono restare ricalcolabili con i parametri dell'epoca.

## 6. Adempimenti telematici

Il software **genera i file conformi**; la **trasmissione** richiede credenziali da
intermediario abilitato (Entratel per Agenzia Entrate, accreditamento INPS per UniEmens).

- **UniEmens**: flusso XML mensile INPS (denuncia retributiva/contributiva).
- **F24**: delega di pagamento (contributi + ritenute IRPEF + addizionali).
- **Certificazione Unica (CU)**: annuale, tracciato Agenzia delle Entrate.

Ogni adempimento ha tabella di testata con stato (`bozza → generato → trasmesso → ricevuta`).

## 7. Documenti e conservazione

- Generazione **cedolino PDF** e **LUL** (Libro Unico del Lavoro).
- File su **object storage** (S3-compatibile), referenziati in DB (`documento`).
- Conservazione sostitutiva a norma (firma digitale + marca temporale) — integrabile con
  un conservatore accreditato AgID nelle fasi successive.

## 8. Sicurezza e compliance

- **GDPR**: si trattano dati particolari (malattia → dati sanitari; trattenute sindacali →
  appartenenza sindacale). Cifratura at-rest e in-transit, minimizzazione, registro
  trattamenti, gestione data breach, retention configurabile.
- Cifratura colonne sensibili (IBAN, dati sanitari) o cifratura at-rest del DB.
- **Audit log** immutabile per ogni modifica rilevante (`audit_log`).
- Autenticazione: JWT + ruoli (`ruolo`, `utente`). MFA consigliato per gli amministratori.

## 9. Stack tecnologico consigliato

| Livello | Scelta | Motivo |
|---|---|---|
| Backend | Java/Spring Boot o C#/.NET | Dominio transazionale, di lungo periodo, robusto |
| DB | PostgreSQL | Affidabile, RLS, tipi `numeric` esatti per importi |
| Frontend | React + TypeScript | Ecosistema maturo |
| Storage | S3-compatibile (MinIO/AWS) | PDF e flussi |
| Code/job | Coda (es. RabbitMQ) + worker | Elaborazioni massive, invii |
| Documenti | Motore PDF (es. JasperReports) | Cedolini/LUL |

> **Importi**: sempre `numeric(15,2)` (o `numeric` con scala adeguata). **Mai** `float`.
