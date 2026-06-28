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
1. 🔨 **Cifratura dati sensibili** — value converter EF Core (AES-GCM) su IBAN, dati malattia.
   - Dove: `Paghe.Persistence/Entities` + `PagheDbContext`
   - Chiave da secret/variabile d'ambiente (mai nel codice)
2. 🔨 **Audit log** — interceptor EF + middleware API → tabella audit (già nello schema, non usata).
   - Dove: `Paghe.Persistence` (interceptor) + `Paghe.Api`
   - Registra: chi, cosa, quando, su quale tenant
3. 🔨 **2FA/MFA** — TOTP sopra al JWT esistente.
   - Dove: `Paghe.Persistence/AuthService.cs` + `Paghe.Api`
4. 🔨 **Retention base** — job di cancellazione/anonimizzazione + flag su entità.

### Serve da te
- 👤 Decidere dove sta la **chiave di cifratura** in produzione (variabile d'ambiente / KMS).

### Fatto quando
- [x] **IBAN salvato cifrato** (AES-256-GCM, `ProtettoreDati`, verificato in DB con test) · [x] **ogni azione sensibile lascia traccia in audit** (`AuditSaveChangesInterceptor` + middleware API)
- [x] **login con secondo fattore** (TOTP RFC 6238, `Totp` + endpoint `/api/auth/2fa/*`, segreto cifrato) · [x] build + test verdi (202)
- [x] **diritto all'oblio** (anonimizzazione dipendente, cedolini conservati, `POST /api/dipendenti/{id}/anonimizza`)

> ✅ **Fase 1 GDPR completata** (cifratura + audit + 2FA + diritto all'oblio).

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
- [ ] cedolino di dicembre conguaglia · [ ] TFR maturato e tassato alla cessazione
- [ ] LUL stampabile · [ ] almeno 2 CCNL funzionanti e validati ⚖️

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
- [ ] CU e 770 passano il controllo ufficiale JAR · [ ] XBRL valido per il deposito
- [ ] estratto conto si riconcilia con le fatture · [ ] build + test verdi

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
