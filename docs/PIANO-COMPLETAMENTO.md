# Piano di completamento — Paghe SaaS

Gap analysis tra ciò che è **già fatto** e ciò che **manca** per arrivare a un prodotto
vendibile. Aggiornato al 2026-06-26.

Legenda effort (con un team piccolo): **S** = giorni · **M** = settimane · **L** = mesi.
Priorità: 🔴 alta · 🟠 media · ⚪ bassa/dopo.

---

## ✅ Già fatto (10 fasi)

| Modulo | Stato |
|---|---|
| Architettura + schema DB PostgreSQL | ✅ |
| Motore cedolino lordo→netto (1 CCNL Commercio) | ✅ |
| Fatturazione attiva + IVA + FatturaPA XML | ✅ |
| Validazione FatturaPA (XSD ufficiale + controlli SdI) | ✅ |
| Bilancio SRL (riclassificazione CEE) | ✅ |
| Persistenza EF Core (aziende + fatture) | ✅ |
| PDF cedolino + fattura | ✅ |
| API REST + Swagger | ✅ |

**6 progetti .NET · 51 test verdi.** Tutto gira su dati seed/in-memory: il sistema *calcola*
e *valida* correttamente, ma non è ancora *configurabile*, *sicuro* o *usabile da UI*.

---

## ❌ Cosa manca (per area)

### 1. Motore Paghe — completare il dominio 🔴
- [ ] **Conguaglio fiscale di fine anno** (oggi l'IRPEF è col metodo presuntivo mensile) — **M**
- [ ] **13ª/14ª**: ratei e erogazione nel cedolino — **S**
- [ ] **Cessazione rapporto**: liquidazione TFR con tassazione separata, ferie/permessi residui — **M**
- [ ] **Malattia / maternità / infortunio** con indennità INPS/INAIL e integrazioni CCNL — **L**
- [ ] **Detrazioni familiari a carico** e **trattamento integrativo** (ex bonus 100€) — **M**
- [ ] **Multi-CCNL** (oggi solo Commercio), **apprendistato**, **tempo determinato** — **L**
- [ ] **Rivalutazione TFR** annuale (coefficiente ISTAT — tabella già presente) — **S**
- [ ] **Rateazione reale addizionali** (saldo + 11 rate anno successivo) — **M**

### 2. Parametri normativi su database 🔴
- [ ] Spostare aliquote/scaglioni/detrazioni/CCNL da `Seed.cs` a **tabelle versionate per anno** — **M**
- [ ] Import controllato + **validazione con un Consulente del Lavoro** (bloccante per la produzione) — **M**

> È il "finto" più grosso del sistema attuale: oggi i valori sono esempi cablati nel codice.

### 3. Fatturazione — completare il ciclo 🔴
- [x] **Firma digitale** CAdES-BES (.p7m) e **XAdES-BES** (XML enveloped) — modulo `Paghe.Firma` (`SignedCms`/`SignedXml` + SHA-256 + SigningCertificateV2). Firmatari: PFX/firma remota (`FirmatorePfx`) e **smart card/CNS** via PKCS#11 (`FirmatoreCns`, libreria Apache-2.0). Endpoint `POST /api/firma/cades` (formato cades/xades), `POST /api/fatture/firma`, `POST /api/firma/verifica`; pagina UI con anteprima e risultato verificato; integra il seam `IFirmaFatturaElettronica` della trasmissione SdI. Restano: marche temporali (CAdES-T/XAdES-T), validazione catena/OCSP, certificato qualificato reale in produzione.
- [ ] **Trasmissione/ricezione SdI** (canale SdICoop web service, o via intermediario) — **L**
- [ ] **Fatture passive** (ciclo acquisti): ricezione, registrazione, riconciliazione — **M**
- [ ] **Note di credito/debito**, **autofattura/reverse charge**, **bollo**, **ritenute**, **cassa previdenziale** — **M**
- [ ] **Conservazione sostitutiva a norma** (AgID) con hash + marca temporale — **M**

### 4. IVA e registri 🟠
- [ ] **Registri IVA** vendite / acquisti / corrispettivi — **M**
- [ ] **Liquidazione IVA periodica** + **LIPE** — **M**
- [ ] **Dichiarazione IVA annuale**, **esterometro** — **L**

### 5. Contabilità — da riclassificazione a contabilità vera 🟠
- [ ] **Prima nota** + **registrazioni automatiche** da cedolini e fatture — **M**
- [ ] **Piano dei conti** standard, **scadenzario**, **partitari** clienti/fornitori — **M**
- [ ] **Cespiti e ammortamenti** — **M**
- [ ] **Nota integrativa** + deposito **XBRL** al Registro Imprese — **L**

### 6. Adempimenti dichiarativi (lavoro) 🟠
- [ ] **UniEmens** (XML mensile INPS) da specifiche ufficiali — **L**
- [ ] **F24** (tracciato telematico) — **M**
- [ ] **Certificazione Unica (CU)** annuale — **M**
- [ ] **770**, **autoliquidazione INAIL** — **L**

> Generazione file ufficiali (F24, CU, AA7/AA9, ComUnica) e flusso ufficiale di
> controllo/invio (JAR Agenzia Entrate, web service InfoCamere, Desktop Telematico):
> pianificazione in [docs/integrazione-ufficiale/](integrazione-ufficiale/README.md).
> Principio: generare → validare → controllo ufficiale → firmare → inviare via canali
> ufficiali, **mai** simulando login o interfacce web.

### 7. Persistenza completa 🔴
- [ ] Persistere **dipendenti/rapporti, cedolini, cartellini, spese, piano conti, registrazioni** — **M**
- [ ] **Migrazioni EF Core** (oggi `EnsureCreated`) + allineare/sostituire `db/schema.sql` — **S**
- [ ] **Storage documenti** (S3/MinIO) con hash SHA-256, tabella `documento` — **M**

### 8. Sicurezza, Auth, GDPR 🔴 (oggi assente)
- [ ] **Autenticazione/autorizzazione** reale: login, JWT, ruoli, MFA (oggi tenant da header demo) — **M**
- [ ] **Hardening multi-tenant**: il tenant non deve fidarsi dell'header (RLS o Finbuckle) — **M**
- [ ] **Cifratura** dati sensibili (IBAN, dati sanitari malattia) — **S**
- [ ] **GDPR**: registro trattamenti, retention, data breach, **audit log** (tabella già nello schema, non usata) — **M**
- [ ] Rate limiting, log strutturati, gestione segreti — **S**

### 9. Frontend 🟠 (oggi assente)
- [ ] **SPA React/Angular**: anagrafiche, presenze, cedolini, fatture, bilancio, dashboard — **L**
- [ ] Login, multi-azienda, download PDF/XML — incluso sopra

### 10. DevOps e qualità 🟠
- [ ] **CI/CD** (build + test + lint), **Docker**, deploy — **M**
- [ ] **Validazione input** centralizzata (FluentValidation) — **S**
- [ ] **Test di integrazione API** (WebApplicationFactory) ed end-to-end — **M**
- [ ] **Observability** (log, metriche, tracing) — **M**

### 11. Legale / business (non codice) 🔴
- [ ] Validazione di **tutti** i parametri con un **Consulente del Lavoro**
- [ ] Accordo con **conservatore accreditato** e **canale SdI**
- [ ] Compliance GDPR documentale, condizioni d'uso, DPA, licenze librerie (es. QuestPDF)

---

## 🎯 Sequenza consigliata

### Traguardo A — "MVP utilizzabile internamente" (priorità 🔴)
1. **Parametri normativi su DB** (area 2) — togliere i valori finti
2. **Persistenza completa** (area 7) — salvare dipendenti, cedolini, cartellini
3. **Auth + multi-tenant sicuro + GDPR base** (area 8) — niente header demo
4. **Frontend minimo** (area 9) — far usare il sistema senza curl
5. **Validazione input** + **CI/CD** (area 10)

### Traguardo B — "Fatturazione elettronica reale"
6. **Firma digitale** + **trasmissione SdI** + **fatture passive** (area 3)
7. **Registri IVA + liquidazione** (area 4)
8. **Conservazione a norma**

### Traguardo C — "Paghe e adempimenti completi"
9. Conguaglio, 13ª/14ª, cessazione, malattia (area 1)
10. **UniEmens, F24, CU** (area 6)
11. Multi-CCNL

### Traguardo D — "Contabilità e bilancio completi"
12. Prima nota + registrazioni automatiche, cespiti (area 5)
13. Nota integrativa + deposito XBRL

---

## Stima complessiva

Un MVP **vendibile a micro-imprese** (Traguardo A + B su un solo CCNL) è realistico in
**6–12 mesi** con un team piccolo che include un Consulente del Lavoro. La copertura
"alla Zucchetti/TeamSystem" (tutti i CCNL, tutti gli adempimenti) è lavoro di **anni**:
conviene restare su una **nicchia verticale** e crescere da lì.
