# Piano di integrazione — come collegare tutto nel software

Come integrare nel software **tutti i servizi** (lista funzionale) **+ le nuove integrazioni**
(canali ufficiali, identità, adempimenti 2026, dichiarazioni, paghe avanzate, sicurezza,
funzioni trasversali). Per ogni voce: **come** si integra, **dove** nel codice, **cosa serve
da fuori** (account/accrediti) e lo **sforzo**.

Aggiornato al 2026-06-27. Da leggere insieme a:
- [docs/PIANO-COMPLETAMENTO.md](PIANO-COMPLETAMENTO.md) — gap analysis fatto/da fare
- [docs/integrazione-ufficiale/](integrazione-ufficiale/README.md) — canali ufficiali AdE/InfoCamere
- [docs/integrazione-ufficiale/GUIDA-ACUBE.md](integrazione-ufficiale/GUIDA-ACUBE.md) — provider SdI già scelto

Legenda sforzo (team piccolo): **S** = giorni · **M** = settimane · **L** = mesi.
Priorità: 🔴 alta · 🟠 media · ⚪ dopo.

---

## 0. Principio guida (non si negozia)

> **Genera → valida → controllo ufficiale → firma → invia tramite canali ufficiali.**
> Non si simula mai il login a un portale (Agenzia Entrate, INPS, banca) pilotando il browser.
> Tutto ciò che esce verso lo Stato o le banche passa da: **(a)** un **provider accreditato**
> con API, **(b)** il **Desktop Telematico / SID** ufficiale, oppure **(c)** un **file** che
> l'utente carica sul portale. Questo tiene il software legale e fuori dai guai.

Conseguenza pratica: **gran parte delle integrazioni "difficili" non si costruisce da zero,
si sblocca aprendo 4 "porte" (provider).** Vedi sezione 1.

---

## 1. Le 4 "porte" da aprire (provider) — sbloccano l'80% del blocco A

Invece di reimplementare protocolli statali, ci si appoggia a soggetti **già accreditati** che
espongono API. Sono 4 accordi commerciali, non 4 mesi di codice.

| # | Porta | Provider tipici | Cosa sblocca |
|---|---|---|---|
| 1 | **Provider SdI + fatture + conservazione** | **A-Cube** (già scelto), TS Digital, Aruba | Invio/ricezione FatturaPA, **scarico fatture attive/passive**, **corrispettivi**, **conservazione a norma** |
| 2 | **Aggregatore Open Banking (PSD2)** | Fabrick, CBI Globe, Salt Edge, Tink, Yapily | **Estratti conto**, movimenti, **riconciliazione bancaria** (senza diventare TPP) |
| 3 | **Identità + firma + PEC** | InfoCert, Aruba, Namirial | **Login SPID/CIE/CNS**, **firma remota**, **PEC** |
| 4 | **Invii fiscali ufficiali** | Desktop Telematico AdE + (eventuale) **accreditamento SID**; **InfoCamere** per ComUnica | Invio **CU, 770, F24, IVA, 730, ISA/CPB**; pratiche **Registro Imprese** |

> Regola: prima si prova **sandbox** del provider, poi go-live. Le credenziali stanno in
> *user-secrets* / variabili d'ambiente, **mai** nel codice (come già fatto per A-Cube).

---

## 2. Blocco A — Integrazioni con i canali ufficiali

| Funzione | Come integrarla | Dove nel codice | Serve da fuori | Sforzo |
|---|---|---|---|---|
| 🔴 Portale **Fatture e Corrispettivi** (scarico attive/passive) | API del provider SdI: registra l'azienda come ricevente, leggi le fatture in arrivo e i corrispettivi | `Paghe.Persistence/Trasmissione/` (estende `SdiGatewayAcube`), nuovo `FatturePassiveService` | Account A-Cube + registrazione P.IVA | M |
| 🔴 **Corrispettivi telematici / RT** | Letti dal Portale via provider (li manda il registratore di cassa, non il software) | come sopra | RT già attivo lato cliente | S |
| 🟠 **Cassetto fiscale** (posizione cliente) | Non esiste API libera per terzi: **delega** dell'intermediario + **import** dei dati scaricati (es. precompilata, RCU). Niente login pilotato | nuovo `Paghe.Core/AdeTelematico/CassettoFiscale` (import file) | Delega cliente su AdE | M |
| 🟠 **Cassetto previdenziale INPS / DURC** | DURC via Sportello Unico Previdenziale (no API aperta) → **richiesta + import esito**; archivio già pronto | `ScadenzarioService` (`DurcEntity` esiste già), nuovo import | Credenziali INPS / delega | M |
| 🔴 **Canale invii (SID / Entratel / Desktop Telematico)** | File generato → controllo JAR ufficiale → **handoff** a Desktop Telematico o **SID** se accreditati | `Paghe.Core/AdeTelematico` (già imposta export cartelle e wrapper JAR) | Desktop Telematico (gratis) o accreditamento SID | M→L |
| 🔴 **Open Banking / PSD2** (estratti conto + riconciliazione) | Consumi le API di un **aggregatore già licenziato** (AISP): OAuth verso la banca, poi leggi movimenti e li abbini a fatture/F24 | nuovo `Paghe.Core/Contabilita/Riconciliazione` + `RiconciliazioneService` | Account aggregatore (Fabrick/Salt Edge…) | M |
| 🟠 **CBI / flussi bancari** (pagamento F24, bonifici stipendi) | Generi il **file SEPA/CBI** (`pain.001` per bonifici, F24 CBI) che l'utente carica sul remote banking, o invii via canale CBI | nuovo `Paghe.Core/Contabilita/FlussiBancari` (writer XML) | Conto + (per invio diretto) accordo CBI con la banca | M |
| 🟠 **INAIL** (autoliquidazione, denunce) | Genera il **file autoliquidazione** + controllo, poi Punto Cliente/handoff | `Paghe.Core/AdeTelematico/Inail` (nuovo) | Credenziali INAIL Punto Cliente | M→L |

---

## 3. Blocco B — Accesso e identità digitale

| Funzione | Come integrarla | Dove nel codice | Serve da fuori | Sforzo |
|---|---|---|---|---|
| 🟠 Login **SPID / CIE / CNS** | SPID = Service Provider **SAML2** (via aggregatore SPID); CIE = OIDC CieID; CNS = certificato client TLS | `Paghe.Api` (middleware auth) + `AuthService` | Onboarding SPID (AgID o aggregatore) | M→L |
| 🔴 **2FA / MFA** | TOTP (app authenticator) sopra al JWT esistente | `Paghe.Persistence/AuthService.cs` + `Paghe.Api` | nessuno | S |
| 🟠 **SPID professionale / delega** | Lato organizzativo (l'intermediario usa il suo SPID sui portali); il software **traccia** la delega | `DelegheService` (esiste già) — estendere | nessuno (è una procedura) | S |
| 🟠 **Deleghe intermediario** (Entratel/cassetto) | Estendi l'entità delega con tipo/canale e scadenza | `Paghe.Persistence/DelegheService.cs` | nessuno | S |

---

## 4. Blocco C — Adempimenti fiscali 2026 (calcolo + parametri)

Questi sono soprattutto **motore di calcolo** + **parametri versionati per anno**. La parte
"ufficiale" (valore vincolante ISA/CPB) passa dal software AdE; noi produciamo la **bozza** e i dati.

| Funzione | Come integrarla | Dove nel codice | Serve da fuori | Sforzo |
|---|---|---|---|---|
| 🟠 **CPB 2026-2027** (proposta + adesione) | Calcolo bozza dai dati reddito; valore vincolante dal software "Il tuo ISA"; tracciato adesione su modello Redditi | nuovo `Paghe.Core/Dichiarativi/Cpb` + `Parametri` | JAR/SW ufficiale AdE | M |
| 🟠 **ISA** (173 modelli + dati precalcolati) | Acquisizione "ulteriori dati" + compilazione modello + controllo JAR | `Paghe.Core/AdeTelematico/Isa` (nuovo) + wrapper JAR | Software ISA AdE | L |
| 🔴 **Ravvedimento operoso** | Calcolo sanzioni/interessi per giorni di ritardo → righe **F24** (modulo F24 già c'è) | `Paghe.Core/Calcolo/Ravvedimento` (nuovo), riusa `F24DaCedolini`/`TracciatoF24Ufficiale` | nessuno | S |
| 🔴 **Scaglioni IRPEF + detrazioni aggiornate** | Sono **dati per anno** su DB, non codice | `Parametri` (`IrpefScaglioni`, `Addizionali*`) + `ParametriNormativiService` | validazione Consulente | S |
| 🟠 **Regime forfettario** (soglie + flat tax) | Profilo azienda "forfettario" → calcolo imposta sostitutiva e niente IVA | `Paghe.Core/Contabilita` + `Parametri` | nessuno | M |

---

## 5. Blocco D — Dichiarazioni/comunicazioni da coprire

Schema unico: **genera file → controllo JAR ufficiale → firma → Desktop Telematico/SID**.
Riusa l'impianto `AdeTelematico` già presente (export cartelle, wrapper JAR, stati pratica).

| Funzione | Come integrarla | Dove nel codice | Serve da fuori | Sforzo |
|---|---|---|---|---|
| 🟠 **Dichiarazione IVA annuale** | Da registri IVA + liquidazioni già persistite → modello + controllo | `Paghe.Core/AdeTelematico/Iva` + `ContabilitaIvaService` | JAR controllo IVA | L |
| 🟢 **Esterometro** | **Già coperto** dallo SdI con codici **TD17/TD18/TD19** in fattura | `Paghe.Core/Fatturazione` (esiste) — verificare TD | nessuno | S |
| 🟠 **Intrastat** | Writer tracciato Intrastat da fatture intra-UE | `Paghe.Core/AdeTelematico/Intrastat` (nuovo) | adesione Dogane/SID | M |
| 🟠 **Modello 730 / 730-4** | Precompilata (import) + risultato 730/4 da applicare al cedolino | `Paghe.Core/Dichiarativi/Mod730` + `CedoliniService` (conguaglio) | accesso precompilata | L |
| 🟠 **CU lavoro autonomo / provvigioni** | Estende la CU già presente con quadri autonomo | `CertificazioneUnicaService.cs` (esiste già) | JAR `CUR26` (già scaricato) | M |
| ⚪ **Comunicazioni enti** (spese sanitarie, oneri) | Writer tracciato + invio | `Paghe.Core/AdeTelematico` (nuovo sotto-modulo) | adesione Sistema TS | M |
| 🟠 **Deposito XBRL** (bilancio) | Generatore XBRL dalla tassonomia su `BilancioEntity` già persistito | `Paghe.Documenti` o nuovo `Paghe.Core/Bilancio/Xbrl` | tassonomia ITCC + InfoCamere | L |

---

## 6. Blocco E — Paghe avanzate (motore di calcolo)

Quasi tutto **interno** al motore: dipende da `Calcolo` + tabelle CCNL in `Parametri`, non da
provider esterni. È il lavoro più grosso ma è "nostro" e differenziante.

| Funzione | Come integrarla | Dove nel codice | Serve da fuori | Sforzo |
|---|---|---|---|---|
| 🔴 **Conguaglio fiscale fine anno + 730/4** | Ricalcolo IRPEF annuo sul cedolino di dicembre + applica 730/4 | `Paghe.Core/Calcolo` + `CedoliniService` | nessuno | M |
| 🔴 **TFR** (accantonamento, rivalutazione, tassazione) | Coeff. ISTAT (tabella c'è) + tassazione separata alla cessazione | `Paghe.Core/Calcolo/Tfr` (nuovo) | indice ISTAT mensile | M |
| 🔴 **LUL** (Libro Unico vidimato) | Generazione PDF/stampa LUL da cartellini e cedolini | `Paghe.Documenti` (nuovo template) | nessuno | M |
| 🟠 **Autoliquidazione INAIL** | Calcolo premio + file → controllo → invio | `Paghe.Core/AdeTelematico/Inail` (vedi blocco A) | Punto Cliente INAIL | M |
| 🟠 **Malattia / maternità / infortunio** | Indennità INPS/INAIL + conguaglio + integrazione CCNL | `Paghe.Core/Calcolo/Assenze` (nuovo) | nessuno | L |
| 🟠 **CIG / CIGS / FIS** | Domande + conguagli + flusso UniEmens | `Paghe.Core/Calcolo` + `UniEmensService` | nessuno | L |
| 🟠 **ANF / Assegno Unico** | Calcolo su cedolino + conguaglio | `Paghe.Core/Calcolo` + `Parametri` | tabelle ANF/AU | M |
| 🟠 **Detassazione / decontribuzione / premi** | Regole fiscali agevolate sul cedolino | `Paghe.Core/Calcolo` + `Parametri` | nessuno | M |
| 🟠 **ROL, permessi, ferie, banca ore, straordinari** | Maturazione/godimento su cartellino | `Paghe.Core/Domain` (cartellino) + `Calcolo` | nessuno | M |
| 🔴 **Multi-CCNL + aggiornamento tabelle** | Tabelle CCNL versionate, oggi solo Commercio | `Parametri` (`Ccnl`, `CcnlLivelli`) + `ParametriNormativiService` | fonte dati CCNL (vedi blocco G) | L |
| 🟠 **Conto individuale / costo del lavoro** | Aggregazione costi per dipendente/periodo | `CedoliniService` (vista costi) | nessuno | M |

---

## 7. Blocco F — Sicurezza, GDPR, conservazione

| Funzione | Come integrarla | Dove nel codice | Serve da fuori | Sforzo |
|---|---|---|---|---|
| 🔴 **Cifratura dati sensibili** (IBAN, malattia) | EF Core value converter / cifratura colonna (AES-GCM) con chiave da KMS/secret | `Paghe.Persistence/Entities` + `PagheDbContext` | gestore segreti (KMS/Vault) | S |
| 🔴 **Audit log** | Interceptor EF + middleware API → tabella audit (già nello schema) | `Paghe.Persistence` (interceptor) + `Paghe.Api` | nessuno | M |
| 🟠 **Retention / diritto all'oblio** | Job periodici di cancellazione/anonimizzazione | `Paghe.Persistence` + scheduler | nessuno | M |
| 🔴 **Conservazione a norma AgID** | Servizio del provider (A-Cube/InfoCert/Aruba): invii il documento + hash, ricevi il pacchetto | `Paghe.Persistence/Trasmissione` (estendi provider) | account conservazione | M |
| 🟠 **Backup automatici + DR** | Infra: backup DB + storage + restore testato | DevOps (non codice app) | hosting/DB gestito | S |
| ⚪ **Accessibilità WCAG** | Frontend conforme (PA) | SPA `frontend/` | audit accessibilità | M |

---

## 8. Blocco G — Funzioni operative trasversali

| Funzione | Come integrarla | Dove nel codice | Serve da fuori | Sforzo |
|---|---|---|---|---|
| 🟠 **Import/export tracciati** (migrazione clienti) | Parser/writer per i formati dei gestionali concorrenti | nuovo `Paghe.Core/Interscambio` | tracciati dei concorrenti | M |
| 🔴 **Multi-tenant + ruoli/permessi** | Filtro `tenantId` ovunque (regola già nel progetto) + ruoli/RBAC | `Paghe.Persistence` (query) + `AuthService` | nessuno | M |
| 🟠 **Portale cliente** | Area dove l'azienda carica documenti e scarica cedolini | `Paghe.Api` (endpoint) + SPA | nessuno | M |
| 🟢 **Notifiche multi-canale** | **Già fatto**: Email/SMS/WhatsApp dietro `INotificatore`; aggiungere **PEC** | `Paghe.Core/Notifiche` (esiste) | SMTP/Twilio/Meta + PEC | S |
| 🔴 **Aggiornamento normativo automatico** | Pipeline annuale che carica aliquote/CCNL su `Parametri`, validata da CdL | `ParametriNormativiService` + processo import | **fonte dati normativi** (abbonamento o CdL) | M |
| 🟠 **API esterne** (e-commerce, CRM, banche) | API key + webhook sul REST già esistente | `Paghe.Api` | nessuno | M |
| ⚪ **App mobile** (timbrature/approvazioni) | App separata che consuma le API | nuovo progetto mobile | nessuno | L |

---

## 9. Sequenza consigliata (fasi)

**Fase 1 — Fondamenta + sblocco fatture (🔴, mesi 1-2)**
1. **2FA**, **cifratura IBAN**, **audit log**, **multi-tenant/ruoli** (blocchi B/F/G)
2. Apri **porta 1 (A-Cube)** → **scarico fatture passive + corrispettivi + conservazione** (blocco A)
3. **Scaglioni IRPEF / parametri su DB** + **ravvedimento → F24** (blocco C)

**Fase 2 — Banca e contabilità viva (🟠, mesi 2-4)**
4. Apri **porta 2 (Open Banking)** → **riconciliazione bancaria** (blocco A)
5. **Flussi CBI** per F24 e bonifici stipendi (blocco A)
6. **Registri IVA → liquidazione → IVA annuale** (blocco D)

**Fase 3 — Paghe complete (🔴/🟠, mesi 4-8)**
7. **Conguaglio fine anno + 730/4**, **TFR**, **LUL** (blocco E)
8. **Malattia/maternità/CIG**, **ANF/AU**, **detassazione** (blocco E)
9. **Autoliquidazione INAIL**, **CU autonomo** (blocchi A/D)
10. **Multi-CCNL** + **aggiornamento normativo automatico** (blocchi E/G)

**Fase 4 — Dichiarativi e identità (🟠, mesi 6-12)**
11. **CPB/ISA**, **Intrastat**, **730**, **XBRL** (blocchi C/D)
12. Apri **porta 3** → **login SPID/CIE** + **firma remota** + **PEC** (blocco B)
13. **Portale cliente**, **import tracciati** (migrazione), **API/webhook** (blocco G)

---

## 10. Note oneste (cosa NON si fa senza accrediti)

- **Niente login pilotato** ai portali AdE/INPS/banche: o provider accreditato, o Desktop
  Telematico/SID, o file caricato dall'utente. Tutto il resto è a rischio legale.
- **Cassetto fiscale e cassetto INPS**: non hanno API aperte a terzi; si lavora con **delega +
  import**, non con scraping.
- **ISA/CPB, CU, 770, F24, IVA**: il **valore vincolante** e l'invio passano dal **controllo
  ufficiale** (JAR AdE, già in parte scaricati) e dal **Desktop Telematico/SID**. Noi prepariamo
  e validiamo, lo Stato certifica.
- **Open Banking**: per leggere i conti serve un **AISP licenziato**; usiamo un **aggregatore**,
  non diventiamo noi banca.
- **Parametri normativi (CCNL, aliquote)**: vanno **validati da un Consulente del Lavoro** prima
  della produzione — è il vincolo bloccante numero uno, non un dettaglio.

> Conclusione strategica: il **motore di calcolo paghe** (blocco E) è il vero lavoro
> differenziante e "nostro". Le integrazioni con lo Stato e le banche (blocco A) si **comprano**
> aprendo 4 porte/provider, non si reinventano. Concentrare le energie lì.
