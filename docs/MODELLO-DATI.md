# Modello dati — Paghe SaaS

Descrizione delle entità definite in [`db/schema.sql`](../db/schema.sql). Tutte le tabelle
di dominio portano `tenant_id` per l'isolamento multi-tenant.

## Mappa delle relazioni

```
tenant ──< azienda ──< sede_lavoro
   │          │
   │          └──< rapporto_lavoro >── persona
   │                     │  │   │
   │                     │  │   └──< familiare_carico
   │                     │  └────── ccnl ──< ccnl_livello
   │                     │                ──< ccnl_scatto_anzianita
   │                     │                ──< ccnl_maggiorazione
   │                     │
   │              cartellino (presenze mensili)
   │                     │
   │                     ▼
   │                 cedolino ──< cedolino_riga >── voce_paga
   │                     │
   │                     ├──> registrazione_contabile ──< registrazione_riga >── piano_conti
   │                     ├──> uniemens / f24 / certificazione_unica
   │                     └──> tfr_movimento
   │
   └──< utente >── ruolo

Parametri normativi nazionali (versionati per anno, non legati al tenant):
  inps_aliquota · inail_tasso · irpef_scaglione · irpef_detrazione_param
  addizionale_regionale · addizionale_comunale · tfr_coeff_rivalutazione
```

## Gruppi di entità

### 1. Tenancy e sicurezza
- **tenant** — account che sottoscrive il SaaS; può gestire più aziende.
- **utente / ruolo** — accesso e autorizzazioni.
- **audit_log** — traccia immutabile delle azioni (richiesto da GDPR e verifiche).

### 2. Anagrafiche
- **azienda** — datore di lavoro (CF, P.IVA, matricola INPS, PAT INAIL, CCNL di default).
- **sede_lavoro** — unità produttiva; rileva per addizionale comunale e INAIL.
- **persona** — lavoratore (dati anagrafici + residenza + IBAN). Separata dal rapporto.
- **rapporto_lavoro** — collega persona e azienda; inquadramento, livello CCNL, retribuzione.
- **familiare_carico** — per le detrazioni IRPEF.

### 3. CCNL (tabelle contrattuali, versionate per data)
- **ccnl** — il contratto collettivo.
- **ccnl_livello** — minimi tabellari, contingenza, EDR per livello e periodo.
- **ccnl_scatto_anzianita** — importo e maturazione degli scatti.
- **ccnl_maggiorazione** — percentuali per straordinario/notturno/festivo.

### 4. Parametri normativi nazionali (versionati per anno)
Tabelle aggiornate **per inserimento**, mai per update distruttivo, così i cedolini storici
restano ricalcolabili con i parametri dell'epoca:
- **inps_aliquota**, **inail_tasso**
- **irpef_scaglione**, **irpef_detrazione_param**
- **addizionale_regionale**, **addizionale_comunale**
- **tfr_coeff_rivalutazione**

### 5. Presenze
- **cartellino** — riepilogo mensile (ore ordinarie/straordinario, ferie, permessi,
  malattia, infortunio, maternità). È l'input principale del calcolo.

### 6. Cedolino
- **voce_paga** — catalogo delle voci; ogni voce dichiara come incide su imponibile INPS,
  imponibile IRPEF e TFR. È il vocabolario del motore di calcolo.
- **cedolino** — testata con i totali e lo `snapshot_calcolo` (jsonb) che fotografa come è
  stato calcolato, per ricalcolabilità e audit.
- **cedolino_riga** — le voci movimentate (segno + competenza / − trattenuta), immutabili
  a cedolino chiuso.
- **tfr_movimento** — accantonamento, rivalutazione e saldo progressivo del TFR.

### 7. Adempimenti
- **uniemens** — flusso XML mensile INPS (con stato e protocollo).
- **f24 / f24_riga** — delega di pagamento per codice tributo.
- **certificazione_unica** — CU annuale per lavoratore.
- **pratica_apertura_partita_iva** — prepara e traccia apertura P.IVA, Comunicazione
  Unica, Camera di Commercio/REA, INPS, INAIL e SUAP.
- **adempimento_pratica** — checklist operativa della pratica, con stato e note per
  ogni ente/adempimento.

### 8. Contabilità del personale
- **piano_conti** — conti per la partita doppia.
- **registrazione_contabile / registrazione_riga** — scritture dare/avere generate dal
  cedolino (vincolo applicativo: totale dare = totale avere).
- **spesa** — fattura passiva/nota spese con IVA detraibile, totale e conto costo.
- **liquidazione_iva / riga_liquidazione_iva** — liquidazione IVA salvata per periodo,
  con righe registro vendite/acquisti usate nel calcolo.

### 9. Documenti
- **documento** — metadati di PDF / XML su object storage, con `hash_sha256` per integrità
  e flag di conservazione a norma.

## Scelte di modellazione rilevanti

- **Persona ≠ rapporto di lavoro**: una persona può avere più rapporti nel tempo (riassunzioni,
  più datori), mantenendo storia anagrafica unica.
- **Parametri normativi fuori dal tenant**: aliquote/scaglioni sono nazionali e condivisi;
  versionati per anno/validità.
- **Snapshot di calcolo sul cedolino**: si conserva *come* è stato calcolato, non solo il
  risultato — fondamentale per verifiche fiscali retroattive.
- **Importi sempre `numeric`**, mai `float`, per evitare errori di arrotondamento.

## Prossimi passi sul dato (fasi successive)
- Tabelle per apprendistato e contratti a termine (agevolazioni contributive).
- Conguaglio di fine anno e gestione 770.
- Cessazione del rapporto con liquidazione TFR e relativa tassazione separata.
- Storicizzazione delle variazioni del rapporto (versioning del `rapporto_lavoro`).
