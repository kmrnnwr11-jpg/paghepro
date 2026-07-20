# Piano a fasi — i 4 gap per vendere al commercialista

Roadmap operativa che mette **in ordine** i 4 gap critici emersi dal confronto con i concorrenti,
con **passi concreti**, tempi e **cosa serve da te** (account, validazioni). È il documento "da
dove parto domani".

Aggiornato al 2026-06-28. Da leggere con:
- [docs/commerciale/CONCORRENTI.md](commerciale/CONCORRENTI.md) — perché questi 4 gap
- [docs/PIANO-INTEGRAZIONE.md](PIANO-INTEGRAZIONE.md) — come si collega ogni cosa
- [docs/PIANO-COMPLETAMENTO.md](PIANO-COMPLETAMENTO.md) — gap analysis estesa

Legenda sforzo: **S** = giorni · **M** = settimane · **L** = mesi.
Simboli: 🔨 codice nostro · 🤝 serve partner/account · 👤 serve azione tua · ⚖️ serve Consulente del Lavoro.

> **Principio di ordine:** prima ciò che è **bloccante e veloce** (GDPR), poi ciò che **sblocca
> valore subito** (fatture passive, ravvedimento), infine i **lavori lunghi** (paghe, dichiarativi).
> Si rilascia a piccoli passi verdi (build + test ok), mai un big-bang.

---

## 📌 Quadro d'insieme

| Fase | Tema | Perché ora | Sforzo |
|---|---|---|---|
| **1** | GDPR base + sicurezza | Bloccante per usare dati reali; piccolo e tutto interno | **M** |
| **2** | Fatture passive + ravvedimento | Sblocca contabilità vera e un quick-win; riusa moduli esistenti | **M** |
| **3** | Paghe avanzate + multi-CCNL | Il cuore mancante per il commercialista | **L** |
| **4** | Dichiarativi + integrazioni ufficiali | Completa l'offerta; dipende da provider/accrediti | **L** |

Traguardo realistico per un **MVP vendibile a microimprese su 1 nicchia**: fasi 1-2 complete +
inizio fase 3 → **3-5 mesi** con un team piccolo e un Consulente del Lavoro a supporto.

---

## 🔵 FASE 1 — GDPR base + sicurezza (🔨, sforzo M)

**Obiettivo:** poter trattare dati reali (IBAN, dati sanitari) senza rischi legali. Tutto interno,
nessun account esterno.

### Passi
1. 🔨 **Cifratura dati sensibili** — envelope AES-GCM versionati su tutti gli IBAN, payload salute
   e voci cedolino, con AAD tenant/record e cutover one-shot delle colonne legacy.
   - Dove: `Paghe.Persistence/Gdpr` + entità/interceptor + `PagheDbContext`
   - Chiave casuale Base64 da 32 byte in secret manager (mai nel codice o coi backup)
2. ✅ **Audit log** — interceptor EF + middleware API, consultazione admin e minimizzazione dati.
   - Dove: `Paghe.Persistence` (interceptor) + `Paghe.Api`
   - Registra: chi, cosa, quando, su quale tenant
3. 🔨 **2FA/MFA** — TOTP sopra al JWT esistente.
   - Dove: `Paghe.Persistence/AuthService.cs` + `Paghe.Api`
4. 🔨 **Retention base** — job di cancellazione/anonimizzazione + flag su entità.

### Serve da te
- 👤 Decidere dove sta la **chiave di cifratura** in produzione (variabile d'ambiente / KMS).

### Fatto quando
- [x] **IBAN e salute salvati solo in payload cifrati** (AES-256-GCM, AAD tenant-bound,
  migrazione/strict gate verificati su SQLite e PostgreSQL).
- [x] **Audit applicativo completato**: letture, operazioni, autenticazione ed esiti HTTP, più
  modifiche EF nella stessa transazione; consultazione tenant-safe e senza valori sensibili.
- [x] **login con secondo fattore** (TOTP RFC 6238, `Totp` + endpoint `/api/auth/2fa/*`, segreto cifrato) · [x] build + **559 test verdi**
- [x] **diritto all'oblio** (anonimizzazione dipendente, cedolini conservati, `POST /api/dipendenti/{id}/anonimizza`)

> ✅ **Cifratura e registro attività applicativo completati.** ⚠️ La fase GDPR complessiva non è
> completa: restano retention e documenti/azioni organizzative del piano principale.

---

## 🟢 FASE 2 — Fatture passive + ravvedimento (🔨🤝, sforzo M)

**Obiettivo:** chiudere il ciclo acquisti (oggi c'è solo l'attivo) e aggiungere un quick-win utile.

### Passi
1. 🤝 **Attivare ricezione su A-Cube** — registrare la P.IVA come ricevente (canale/indirizzo telematico).
2. 🔨 **Scarico fatture passive** — leggere le fatture in arrivo dal provider e archiviarle.
   - Dove: `Paghe.Persistence/Trasmissione/` (estende `SdiGatewayAcube`) + nuovo `FatturePassiveService`
3. 🔨 **Registrazione + riconoscimento** — registrare la fattura passiva nei registri IVA acquisti.
   - Dove: `ContabilitaIvaService` (già gestisce spese/registri)
4. 🔨 **Ravvedimento operoso → F24** — calcolo sanzioni/interessi per giorni di ritardo → righe F24.
   - Dove: nuovo `Paghe.Core/Calcolo/Ravvedimento`, riusa `F24DaCedolini` + `TracciatoF24Ufficiale`

### Serve da te
- 🤝 **Account A-Cube** (anche sandbox) con la P.IVA registrata — vedi [GUIDA-ACUBE.md](integrazione-ufficiale/GUIDA-ACUBE.md).

### Fatto quando
- [ ] una fattura passiva di test arriva e si registra · [ ] entra nei registri IVA acquisti
- [x] **ravvedimento calcola sanzione+interessi e produce le righe F24** (`Ravvedimento` + `RavvedimentoF24` + `POST /api/ravvedimento/calcola`) · [x] build + test verdi (208)

> Nota: il ravvedimento è un **calcolatore parametrizzato** — aliquote/tasso legale sono input. Gli scaglioni di default sono **storici (30%), da validare** ⚖️ (riforma sanzioni dal 1/9/2024 → base 25%).

---

## 🟡 FASE 3 — Paghe avanzate + multi-CCNL (🔨⚖️, sforzo L)

**Obiettivo:** il vero cuore per il commercialista. È lungo: si rilascia a pezzi, un CCNL alla volta.

### Passi (in ordine)
1. 🔨⚖️ **Conguaglio fiscale fine anno** — ricalcolo IRPEF annuo sul cedolino di dicembre.
   - Dove: `Paghe.Core/Calcolo` + `CedoliniService`
2. 🔨 **TFR** — accantonamento, rivalutazione (coeff. ISTAT, tabella già presente), tassazione separata.
   - Dove: nuovo `Paghe.Core/Calcolo/Tfr`
3. 🔨 **13ª/14ª** — ratei ed erogazione.
4. 🔨 **LUL (Libro Unico)** — generazione stampa da cartellini e cedolini.
   - Dove: `Paghe.Documenti` (nuovo template)
5. 🔨⚖️ **Malattia/maternità/infortunio** — indennità INPS/INAIL + conguaglio.
   - Dove: nuovo `Paghe.Core/Calcolo/Assenze`
6. 🔨⚖️ **Multi-CCNL** — tabelle CCNL versionate (oggi solo Commercio).
   - Dove: `Parametri` (`Ccnl`, `CcnlLivelli`) + `ParametriNormativiService`

### Serve da te
- ⚖️ **Consulente del Lavoro** che valida ogni regola e ogni tabella CCNL (bloccante per la produzione).
- 👤 Scegliere il **2°/3° CCNL** da coprire (la nicchia: es. Metalmeccanici, Edilizia, Turismo).

### Fatto quando
- [x] **TFR** maturato (quota mensile), **rivalutato** (1,5%+75% ISTAT + imposta sostitutiva) e **tassato** alla cessazione (tassazione separata, `CalcoloTfr`)
- [x] **conguaglio fiscale** di fine anno: motore `ConguaglioFiscale` + **agganciato ai cedolini archiviati** (`CedoliniService.CalcolaConguaglioAnnualeAsync` + `GET /api/rapporti/{id}/conguaglio`)
- [x] **ratei 13ª/14ª** (`MensilitaAggiuntive`)
- [x] **LUL** stampabile (sintesi mensile PDF, `LibroUnicoPdf`) · [x] build + test verdi (227)
- [ ] almeno 2 CCNL funzionanti e validati ⚖️ (servono dati CCNL + CdL)

> Motori interni e parametrizzati pronti. **Stop** ai pezzi che richiedono dati CCNL esterni o
> validazione bloccante del Consulente del Lavoro (multi-CCNL, malattia/CIG/ANF) e a quelli con
> account esterni (Fatture passive→A-Cube, Open Banking).

---

## 🟠 FASE 4 — Dichiarativi + integrazioni ufficiali (🔨🤝, sforzo L)

**Obiettivo:** completare l'offerta. Schema unico: genera → controllo JAR ufficiale → firma →
Desktop Telematico/SID. Riusa l'impianto `AdeTelematico` già presente.

### Passi
1. 🔨 **Certificazione Unica** — completare il `CertificazioneUnicaService` già avviato + JAR `CUR26`.
2. 🔨 **770** — quadri sostituto d'imposta + JAR `77026` (già scaricato).
3. 🔨 **Dichiarazione IVA annuale** — da registri/liquidazioni già persistiti.
4. 🔨 **Deposito XBRL** del bilancio — generatore da `BilancioEntity` già persistito.
5. 🔗 **Cassetto fiscale / Fatture e Corrispettivi** — import dati via provider/delega.
6. 🔗 **Open Banking / riconciliazione bancaria** — via aggregatore PSD2.

### Serve da te
- 🤝 **Desktop Telematico** installato (gratis) o **accreditamento SID**.
- 🤝 Account **aggregatore Open Banking** (Fabrick/Salt Edge…) per la riconciliazione.
- 👤 Eventuale **adesione InfoCamere** se vuoi inviare ComUnica direttamente.

### Fatto quando
- [x] **CU per azienda** + **export CSV** (`CertificazioneUnicaService` + `GET /api/aziende/{id}/cu/{anno}[/export.csv]`)
- [x] **770 per azienda** (bozza percipienti) + **export CSV** (`Modello770Service.EsportaCsv` + `.../modello770/{anno}/export.csv`)
- [x] **Dichiarazione IVA annuale** (bozza, aggrega le liquidazioni dell'anno) + **export CSV** (`ContabilitaIvaService` + `.../iva/dichiarazione-annuale/{anno}[/export.csv]`)
- [ ] CU/770/IVA passano il **controllo ufficiale JAR** · [ ] XBRL valido per il deposito — *bozze pronte, controllo vincolante ai JAR AE*
- [ ] estratto conto si riconcilia con le fatture · [x] build + test verdi (240)

---

## 🗺️ Riepilogo "cosa serve da te" (per non bloccarti)

| Quando | Cosa procurare | Per |
|---|---|---|
| Subito | Decidere il **2°/3° CCNL** (la nicchia) | Fase 3 |
| Fase 1 | Dove tieni la **chiave di cifratura** in produzione | GDPR |
| Fase 2 | **Account A-Cube** (sandbox) | Fatture passive |
| Fase 3 | **Consulente del Lavoro** che valida le regole ⚖️ | Paghe (bloccante) |
| Fase 4 | **Desktop Telematico** + **aggregatore Open Banking** | Invii e banca |

> Nota: le fasi 1 e 2 si possono fare **subito** senza dipendere da nessuno (a parte l'account
> A-Cube per il test). Le fasi 3 e 4 dipendono dal Consulente del Lavoro e dai provider: vanno
> avviate in parallelo procurando per tempo le persone/account giusti.
