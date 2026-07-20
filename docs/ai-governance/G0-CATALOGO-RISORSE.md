# G0 — Catalogo delle risorse professionali e degli effetti

> Versione: 15 luglio 2026 · Stato: **BOZZA IN ATTESA DI APPROVAZIONE** (fondatore; parti GDPR da
> validare con DPO/legale prima del pilota)
> Fa parte del Gate G0 di [`../PIANO-AI-LIBERA-LIMITI-USO.md`](../PIANO-AI-LIBERA-LIMITI-USO.md)
> (sez. 12). Regola fondante: **ogni risorsa non presente in questo catalogo è NEGATA all'AI**
> (default deny, piano §3.4).

Il censimento è stato fatto sul codice reale alla data indicata: 31 tabelle nel
`PagheDbContext`, ~150 endpoint API, file generati/importati, cache e strumenti. Chi aggiunge una
tabella, un endpoint o un formato DEVE aggiornare questo catalogo prima che la risorsa possa
essere considerata dai futuri adapter AI (G3+).

## 1. Legenda delle classi di dato (dal piano §4.2)

| Classe | Significato | Regola AI |
|---|---|---|
| `D0` | Normativo/pubblico | Lettura libera nei periodi validi, fonte citata |
| `D1` | Aziendale | Ammesso nel mandato azienda/pratica |
| `D2` | Personale di lavoro | Ammesso per finalità professionale e scope esatto |
| `D3A` | Particolare (art. 9 GDPR) | Solo grant `GS` dedicato, campi minimi |
| `D3B` | Penale (art. 10 GDPR) | Grant separato; oggi NESSUNA risorsa in questa classe |
| `D4` | Segreto operativo | MAI nel contesto del modello |
| `DX` | Privato/irrilevante | Rifiutato in quarantena |
| `CP` | **Control plane** (fuori Vault) | Non è una risorsa AI: il modello non la vede mai |

## 2. Tabelle del database (31 DbSet in `PagheDbContext`)

### 2.1 Parametri normativi — `D0`

| Tabella | Contenuto | Note AI |
|---|---|---|
| `ParametriAnno` | parametri per anno | lettura con anno/periodo citato |
| `InpsAliquote` | aliquote INPS | idem |
| `IrpefScaglioni` | scaglioni IRPEF | idem |
| `AddizionaliRegionali` / `AddizionaliComunali` | addizionali | idem |
| `Ccnl` / `CcnlLivelli` | contratti e livelli | idem |

L'AI può citarli e spiegarli, **mai sostituirli con valori generati** (piano §3.3).

### 2.2 Dati aziendali — `D1`

| Tabella | Contenuto sensibile rilevante |
|---|---|
| `Aziende` | anagrafica fiscale/contributiva; `IbanProtetto` (cifrato F3); `Logo` (bytea, escluso dal JSON) |
| `Fatture` | fatture attive, XML FatturaPA |
| `Spese` / `LiquidazioniIva` | contabilità IVA |
| `Bilanci` / `RigheBilancio` | bilancio con stati Bozza→Approvato→Depositato |
| `TrasmissioniFatture` / `RicevuteSdi` | stati e ricevute SdI |
| `Deleghe` | deleghe a intermediario |
| `PraticheF24` | pratiche F24 con tracciato telematico archiviato |
| `AvvisiPagamento` | crediti verso clienti; IBAN protetto (F3) |
| `Durc` / `Promemoria` | scadenze DURC/F24/manuali |
| `PraticheAperturaPartitaIva` | pratiche ComUnica |
| `Immobili` | dati IMU |
| `PosizioniInail` | P.A.T., tassi |
| `DichiarazioniRedditiPf` | redditi PF del percipiente — vedi nota D2 sotto |

### 2.3 Dati personali di lavoro — `D2`

| Tabella | Campi critici |
|---|---|
| `Dipendenti` | CF, nome/cognome, `IbanProtetto` (cifrato F3), flag `Anonimizzato` |
| `Rapporti` | inquadramento, date assunzione/cessazione, retribuzione |
| `Cedolini` | importi, imponibili, trattenute, `VociProtette` (cifrate F3) |
| `Persone` | archivio persone fisiche (rappresentanti, soci) |
| `DichiarazioniRedditiPf` | redditi della persona: trattare come `D2` anche se elencata fra i moduli fiscali |

### 2.4 Dati particolari — `D3A`

| Risorsa | Dove sta | Regola |
|---|---|---|
| Terna malattia/infortunio del cartellino | `Cartellini.DatiSaluteProtetti` (cifrata F3: giorni malattia, n° evento, giorni infortunio) | Solo grant `GS-W` paghe (piano §5.5); mai in indici, memorie, confronti fra lavoratori |
| Assenze qualificabili come salute in documenti importati (certificati, UNILAV con note) | quarantena import | classificazione al momento dell'import; se D3A → decisione separata |

Il resto del cartellino (straordinari, ferie, permessi) è `D2`.
**`D3B` (penale): nessuna risorsa presente oggi.** Se un modulo futuro la introduce (es.
casellario per appalti), serve disciplina e grant dedicati PRIMA del censimento.

### 2.5 Segreti operativi — `D4` (mai al modello)

| Risorsa | Dove sta |
|---|---|
| `Utenti.PasswordHash`, `Utenti.SegretoTotp` (cifrato F3) | AuthEntities |
| `ConfigurazioniSdi` ApiKey provider (cifrata F1-sicurezza) | ConfigurazioneSdiEntity |
| Chiave JWT (`Jwt__Key`), stringhe di connessione DB, chiavi provider AI | config/secret del server |
| Chiavi di cifratura F3 (AES-256-GCM) e chiave `age` dei backup | secret store / file `0600` |
| PFX caricati per firma, PIN CNS | transitori negli endpoint firma (azzerati in memoria) |

### 2.6 Control plane — `CP` (fuori dalla Vault AI, piano §4.5)

| Risorsa | Motivo |
|---|---|
| `Tenants`, `Utenti` (ruoli, stato) | governo della piattaforma |
| `AuditLog` (registro attività GDPR) | audit grezzo: l'AI non lo legge; la UI «Cosa vede l'AI» userà una vista minimizzata |
| `ConsumiAi` | contatori d'uso |
| Futuri `AiUsagePolicyStore`, `AiScopeGrantRecord`, nonce, retention ledger (G2) | per definizione |

## 3. Risorse fuori dal database

### 3.1 File generati (output) — classe della fonte

| Artefatto | Classe | Note |
|---|---|---|
| PDF cedolino, CU, bilancio, AA7/10 | `D2`/`D1` | etichettati BOZZA DA VALIDARE dove previsto |
| XML FatturaPA, flusso UniEmens, tracciato F24 (1900 char) | `D1`/`D2` | export = destinazione «esterna» solo con azione manuale |
| CSV export (CU, 770, IVA annuale) | come fonte | |
| Esempi in `output/pdf/` | `D2` demo | dati dimostrativi |

### 3.2 File importati (input volontario)

| Ingresso | Endpoint | Classe attesa |
|---|---|---|
| Visura camerale (PDF/foto) | `POST /api/aziende/da-visura` | `D1` |
| UNILAV (XML/PDF) | `/api/aziende/{id}/dipendenti/da-unilav`, `/api/unilav/pdf` | `D2` |
| DURC PDF | `POST /api/documenti-scadenze` | `D1` |
| Logo azienda (PNG/JPEG ≤1MB) | `POST /api/aziende/{id}/logo` | `D1` |
| PFX / documenti da firmare | endpoint firma | `D4`/fonte |

Regola G4 (futura): tutto l'import AI passa da quarantena cifrata + classificazione; oggi i parser
sono deterministici e già limitati (magic/MIME, dimensioni).

### 3.3 Cache, indici, log, backup

| Risorsa | Classe | Stato |
|---|---|---|
| Indice semantico / embedding | — | **NON ESISTE** (nasce solo in G4, cifrato, D3A escluso) |
| Log applicativi ASP.NET | `CP` | senza payload sensibili; errori firma solo con TraceId |
| Backup DB (`scripts/backup-db.sh`) | come DB | cifrati `age`, fail-closed, `0600` |
| LocalStorage frontend (token JWT, email demo, azienda corrente) | `D4`/`CP` | mai risorsa AI |

### 3.4 Strumenti esterni e supply chain

| Strumento | Ruolo | Nota licenza/fiducia |
|---|---|---|
| JAR controlli ufficiali (F24 `ControlliF24A0`, AA7/AA9, CU) in `~/ade-official-integration-kit/` | validatori deterministici, FUORI repo | ufficiali AdE; solo CLI locale |
| PDFsharp (MIT), Pkcs11Interop (Apache-2.0), EF Core, Npgsql | librerie backend | verificate; niente AGPL/GPL nel SaaS |
| React/Vite/lucide (frontend) | UI | lockfile presente |
| Provider AI configurabili (`Ai:Provider` = Claude/DeepSeek/GLM/Compatibile, default **Spento**) | seam `IEstrattoreAiDocumenti` | nel profilo personale i provider cloud restano vietati (piano §3.2) |
| Futuro modello locale (Ollama/vLLM) | G1+ | updater firmato, senza accesso vault (piano §3.2) |

Gate: ogni nuova dipendenza va censita qui con licenza e manutenzione
(`docs/GITHUB_REPOS_RIUSO.md` resta la procedura).

## 4. Censimento effetti (per famiglia di endpoint)

Metodo: ~150 endpoint censiti dal codice. Classificazione per effetto; il dettaglio per singola
rotta sta nel codice (`Program.cs` + estensioni `Map*`).

| Effetto | Famiglie | Esempi | Futuro livello AI max |
|---|---|---|---|
| **read** | elenchi, dettagli, bozze calcolate al volo | `GET /api/aziende…`, presenze, LIPE/IRAP/IRES/IMU bozza, conguaglio, scadenzario | `A1-A2` |
| **draft** (genera senza persistere) | PDF/XML/CSV, simulazioni | `POST /api/cedolini/calcola`, `/api/fatture/pdf`, `/api/f24/da-paghe`, `/api/bilancio/riclassifica`, `/api/ravvedimento/calcola` | `A2` |
| **write reversibile** | crea/aggiorna record con stato | `POST /api/aziende`, dipendenti, cartellini→cedolini, promemoria, pratiche, stati macchina | `A3` (diff+conferma, G5) |
| **write critico** | cancellazioni, anonimizzazione | `DELETE /api/aziende/{id}`, `/api/aziende/elimina-bulk`, `POST /api/dipendenti/{id}/anonimizza` | MAI dal modello (procedura manuale, piano §10.1) |
| **external** | invii/firma/notifiche | `POST /api/fatture/{id}/trasmetti`, `/api/firma/cades`, `/api/scadenzario/{id}/invia`, `/invia-automatiche`, esporta F24 | solo futuro `GX` one-shot (G8); oggi canale Manuale di default |
| **auth/admin** | login, 2FA, super-admin, audit | `/api/auth/*`, `/api/admin/*`, `GET /api/audit` | `CP`: mai strumenti AI |
| **AI di servizio** | stato/prova provider | `GET /api/ai/stato`, `POST /api/ai/prova` | `CP` (diagnostica) |

Nota: `POST /api/pratiche/apertura-partita-iva/analizza-testo` e `/api/aziende/da-visura` sono i
due punti dove il seam AI **già esiste** (proposta campi, euristica vince): restano gli unici
finché G2 non è attivo, e con provider `Spento` non chiamano nessuno.

## 5. Processi, entitlement, IPC (stato attuale vs profilo personale)

**Oggi (SaaS di sviluppo):** un processo `Paghe.Api` (ASP.NET, rete necessaria per natura SaaS,
porta 5171), SPA Vite (5173), DB SQLite o PostgreSQL/Supabase. Nessun processo AI residente:
il seam AI è spento per default e non esiste runtime locale.

**Profilo personale (target G1):** processi separati PersonalDesktop / Bridge / Host / Worker,
IPC XPC/socket Unix autenticato, **zero entitlement di rete sul data path AI**, vault cifrata con
Keychain, updater separato firmato. Questo catalogo è l'elenco che quei processi dovranno
rispettare: qualunque risorsa non elencata = negata.

## 6. Matrice capability × modalità × gate (registro di maturità iniziale)

Formato dal piano §9.5. Stato oggi: **tutte le capability AI sono `unavailable`** (runtime AI
assente); il Copilot in app è deterministico e NON è una capability AI. `external_effects: false`
ovunque fino a G8.

| capability_id | Modulo | Modalità previste | Classi dati | Gate tecnico | Evidenza ufficiale | Validazione professionale |
|---|---|---|---|---|---|---|
| `paghe.leggi_spiega` | cedolini/presenze | strict, guided | D0-D2 | G3 | — | CdL sul pilota |
| `paghe.controlla_mese` | presenze/cedolini | guided | D0-D2 (D3A via GS-W) | G3 | — | CdL |
| `paghe.onboarding_dipendente` | UNILAV/anagrafiche | guided | D2 | G3 (read/draft), G5 (write) | — | CdL |
| `paghe.malattia_infortunio` | cartellino | strict+`GS-W` | D3A | G3 + policy GS approvata | — | CdL + DPO |
| `telematici.prepara_f24` | F24 | guided | D1-D2 | G3; G5 per salva | esito `ControlliF24A0` | CdL/commercialista |
| `telematici.prepara_cu_770_uniemens` | CU/770/UniEmens | guided | D2 | G3 | controlli ufficiali quando integrati | CdL |
| `telematici.interpreta_diagnostico` | tutti | strict, guided | D0-D2 | G3 | diagnostico ufficiale in input | CdL |
| `fatture.classifica_registra` | fatturazione/spese | guided | D1 | G3; G5 per scritture | XSD/ControlliSdi | commercialista |
| `contabilita.riconcilia` | prima nota/IVA | workspace→lotto S1/S2 | D1 | G4 (indice) + G5 | — | commercialista |
| `bilancio.prepara_chiusura` | bilancio | guided, workspace | D1 | G3-G5 | quadratura interna | commercialista |
| `scadenze.prepara_promemoria` | scadenzario | guided, scheduled | D1 | G3; G6 per ricette | — | — |
| `documenti.leggi_proponi` | import generico | strict, guided | classe della fonte | G4 (quarantena/parser) | — | — |
| `pratiche.compila_assisti` | ComUnica/AA7-10 | guided | D1-D2 | G3 | JAR AA7/AA9 | intermediario |
| `gestione.report_interno` | KPI/report | workspace read-only | D1 | G4 | — | — |
| `copilot.chat_contestuale` | trasversale | strict, guided | da grant | **G2+G3** | — | test utenti §18 piano |
| `esterni.invio_qualsiasi` | SdI/PEC/pagamenti | — | — | **G8 + piano separato** | canale ufficiale | professionista |

## 7. Criterio di chiusura del censimento (dal Gate G0)

- [x] tabelle, documenti, cache, export e tool censiti (questo file);
- [x] classificazione `D0-D2/D3A/D3B/D4/DX/CP` assegnata a ogni risorsa nota;
- [x] effetti read/draft/write/external censiti per famiglia di endpoint;
- [x] matrice capability × modalità × gate prodotta;
- [ ] approvazione del fondatore (responsabile competente);
- [ ] presa d'atto che ogni risorsa NUOVA nasce `DENY` finché non entra qui.

Documenti gemelli: [`G0-PURPOSE-GOVERNANCE.md`](G0-PURPOSE-GOVERNANCE.md) (finalità e registri
GDPR), [`G0-THREAT-MODEL.md`](G0-THREAT-MODEL.md) (minacce e contromisure).
