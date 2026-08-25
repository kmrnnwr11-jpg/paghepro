# PIANO — Da MVP a software vendibile ai commercialisti

> Documento guida del progetto. Scritto in modo semplice: **non serve essere del mestiere
> per seguirlo.** Ogni fase dice *cosa fai tu* e *cosa fa l'agente AI (il codice)*.
> Aggiornato al: 2026-08-25.

> **Governance AI personale aggiornata il 15 luglio 2026:** per qualunque lavoro sull'AI dentro
> PaghePro prevale
> [`docs/PIANO-AI-LIBERA-LIMITI-USO.md`](docs/PIANO-AI-LIBERA-LIMITI-USO.md). L'AI può coprire tutti
> i dati e moduli professionali interni, ma ogni esecuzione usa un mandato limitato. Prima di
> implementare nuove capability AI si eseguono G0, G1 e G2 del nuovo piano. Le checklist P-1–P6 di
> `docs/PIANO-PERSONALE-PRIVACY-FIRST.md` sono storiche e non eseguibili. Questa precedenza non
> autorizza codice e non cambia l'ordine delle attività non-AI già presenti qui.

> **Stato Aurora al 24 agosto 2026:** completato e verificato il runtime A2 read-only: chat e
> cinque strumenti passano dal Governor con lease S2, scope esatto, proiezioni minimizzate,
> quota server-side, revoca e SessionLog. Il controllo cedolini legge lotti massimi di 10 dentro
> un budget di mandato esplicitato dal preflight. La SPA blocca grant concorrenti/orfani e
> invalida ogni risposta al cambio azienda, periodo o pagina. Per decisione separata del titolare,
> il profilo cloud di collaudo usa DeepSeek come provider principale e Google Gemini come riserva
> automatica; Gemini riceve la richiesta solo se DeepSeek fallisce e legge direttamente le immagini.
> Le chiavi restano segreti server-side e la UI segnala separatamente quella mancante. La riserva
> è stata provata davvero (24 agosto 2026) mettendo fuori uso DeepSeek: emerso e corretto un
> difetto per cui Google rifiutava con 400 i giri con strumenti se non riceveva indietro la
> propria firma opaca (`thought_signature`); ora Aurora la conserva nel ciclo del server e la
> rimanda solo a Google. La riserva è fissata su `gemini-3.5-flash` perché `gemini-3.7-flash`
> rispondeva 503 «high demand». G1/DPA e
> validazione con dati reali restano aperti. Il primo effetto A3 (`compila_presenze`) è disponibile
> soltanto per coppie tenant:utente esplicite, con proposta, conferma, transazione e undo; il default
> di produzione resta A2 e le scritture restano spente senza allowlist. Verifica del supporto
> dual-provider e dei moduli paghe correnti: **1.012/1.012 backend**, **53/53 frontend** e build
> Release/SPA verdi.

---

## 0. Come si legge questo piano

Ogni attività è marcata con un simbolo, così sai sempre **chi fa cosa**:

| Simbolo | Significato |
|---|---|
| 🔧 | **Lo fa l'agente AI** (scrive il codice). Tu non devi fare nulla di tecnico. |
| 🧑 | **Lo fai TU** (un'azione pratica: aprire un account, comprare un certificato, firmare un contratto). |
| ⏳ | **Attesa di terzi** (un fornitore, un ente: tempi non dipendono da noi). |
| 💶 | **Costo** indicativo (prezzi Italia 2026, possono variare). |
| ⏱️ | **Tempo** stimato. |
| ✅ | **"Fatto quando…"**: come capisci che la fase è davvero finita. |

**Regola d'oro:** le cose con ⏳ (attese) vanno **avviate subito**, anche se la loro fase è
più avanti nel piano. Sono il collo di bottiglia. Vedi la sezione "Da avviare oggi".

---

## 1. Glossario (in parole semplici)

- **SdI** = Sistema di Interscambio: il "postino" dell'Agenzia delle Entrate che riceve e
  smista tutte le fatture elettroniche. Una fattura è valida solo se passa dallo SdI.
- **FatturaPA** = il formato XML ufficiale della fattura elettronica italiana.
- **Provider accreditato** (es. A-Cube, Aruba) = azienda autorizzata a spedire le fatture
  allo SdI per conto tuo, tramite internet (API). Tu paghi loro, loro parlano con lo SdI.
- **Firma digitale (CAdES/XAdES)** = la "firma elettronica" che certifica chi ha emesso un
  file. Serve un **certificato** vero (una specie di carta d'identità digitale).
- **Entratel / Fisconline** = i canali telematici dell'Agenzia delle Entrate per inviare
  F24, dichiarazioni, Certificazione Unica, ecc. Di solito il commercialista è già abilitato.
- **UniEmens** = il flusso mensile che si manda all'**INPS** con stipendi e contributi dei
  dipendenti. È il cuore delle paghe. Oggi **manca**.
- **INAIL** = ente per l'assicurazione infortuni; autoliquidazione annuale dei premi.
- **Conservazione a norma** = archiviare i documenti fiscali per 10 anni in modo legalmente
  valido (non basta salvarli su un disco).
- **GDPR** = la legge europea sulla privacy. Obbliga a proteggere i dati personali (es. IBAN,
  dati di salute). Per software che gestisce dipendenti è **obbligatorio**.
- **DPA** = contratto sulla privacy tra te (fornitore software) e il commercialista (cliente).
- **Migrazioni EF** = il modo "professionale" di aggiornare la struttura del database senza
  perdere i dati. Oggi il progetto usa un metodo da sviluppo, non adatto alla produzione.

---

## 2. Dov'è il progetto OGGI (onesto)

**Già pronto e funzionante (livello demo/MVP):**
- ✅ Calcolo cedolino, contributi, IRPEF, TFR (valori da **validare** con un consulente).
- ✅ Fattura elettronica: genera l'XML FatturaPA, lo valida contro lo schema ufficiale,
  esegue i controlli SdI.
- ✅ F24 con tracciato ufficiale a lunghezza fissa.
- ✅ Bilancio d'esercizio (Stato Patrimoniale + Conto Economico) persistito.
- ✅ Liquidazione IVA, spese, pratiche apertura P.IVA.
- ✅ Motore di firma CAdES/XAdES (manca solo il certificato vero).
- ✅ Infrastruttura per spedire allo SdI, incluso il connettore **A-Cube** (manca l'account).
- ✅ Frontend (SPA) per usare tutto senza comandi tecnici.
- ✅ **Home studio azienda-prima**: il professionista crea, cerca o sceglie esplicitamente il
  cliente prima di aprire gli strumenti; panoramica, moduli e “Riprendi il lavoro” mantengono
  il relativo contesto. Nessun cliente fittizio viene creato automaticamente dalle pagine.
- ✅ **Inserimento azienda da testo/PDF più robusto**: riconosce CF/P.IVA combinati, sede senza
  la parola “CAP”, PEC, ATECO, settore agricolo e rappresentante; l'IBAN resta facoltativo. Una
  modalità prova consente di salvare l'anagrafica con dati INPS/INAIL/CCNL ancora vuoti e da
  completare, senza trasformare codici `000000` in posizioni ufficiali.
- ✅ Dataset di prova tenant-isolato disponibile tramite endpoint demo esplicito, senza
  sostituire la scelta consapevole dell'azienda nell'interfaccia.
- ✅ Pacchetto Railway per **prove con dati fittizi**: SPA e API nello stesso container, porta
  dinamica, healthcheck, volume SQLite `/data` e persistenza dopo riavvio verificati. La messa
  online richiede ancora account, commit/push e configurazione del servizio da parte dell'utente.
- ✅ Base mobile iOS/Android compilabile: progetti Xcode/Android Studio, condivisione nativa dei
  documenti, hardening e gate build HTTPS/app ID. Piano e schede store in
  `docs/PIANO-MOBILE-STORE.md`; invio agli store ancora bloccato dai gate indicati sotto.
- ✅ Autenticazione, multi-azienda (multi-tenant), registro attività, 615 test backend e 12 test
  frontend automatici verdi.

**Manca per essere un prodotto vero da vendere:**
- ❌ Invio reale allo SdI (serve account provider).
- ❌ Certificato di firma reale.
- ⚠️ **UniEmens INPS**: il flusso mensile ordinario passa il controllo ufficiale INPS (24 agosto
  2026); mancano i dati di inquadramento contributivo in anagrafica, gli eventi tutelati
  (malattia, maternità, CIG) e le gestioni speciali. **INAIL**: calcolo e pratica ci sono, manca
  il tracciato telematico.
- ❌ Controlli ufficiali AdE eseguiti (Entratel + moduli JAR) su F24/CU/LIPE/770.
- ❌ Conservazione a norma.
- ⚠️ GDPR parziale: cifratura dati sensibili e registro di accessi/modifiche fatti; restano
  retention e adempimenti organizzativi/documentali.
- ❌ Fondamenta di produzione (migrazioni database, segreti, backup, deploy stabile).
- ❌ Pubblicazione mobile: sessioni brevi/revocabili e custodia Keychain/Keystore sono completate;
  servono ancora dominio HTTPS stabile, cancellazione account, informative/assistenza pubbliche
  e account Apple/Google dell'organizzazione.
- ❌ Validazione dei calcoli da un commercialista + contratto + assicurazione.

---

## 3. Obiettivo finale

Un software che un commercialista/consulente del lavoro può **comprare e usare** per gestire
i clienti: paghe, fatture, adempimenti — preparando i file corretti e **inviandoli davvero**
agli enti (direttamente o tramite il suo canale), in regola con privacy e conservazione.

---

## 4. ⚡ Da avviare OGGI (in parallelo, sono le attese lunghe)

Queste tre cose non costano quasi nulla e sbloccano tutto. **Falle partire subito**, mentre
l'agente lavora sul codice:

1. 🧑 **Apri un account A-Cube in modalità sandbox** (gratis) → serve alla Fase 4.
   Vai su acubeapi.com, registrati, chiedi l'accesso "sandbox" (di prova).
2. 🧑 **Ordina un certificato di firma digitale** (firma remota o CNS) da Aruba/InfoCert/
   Namirial → serve alla Fase 5. 💶 ~30–60 €.
3. 🧑 **Trova un commercialista "pilota"** (anche quella a cui vuoi vendere): sarà il primo
   utente di prova e ti **valida i calcoli**. È la risorsa più preziosa e gratuita.

---

## 5. LE FASI (in ordine consigliato)

> Ogni fase è indipendente abbastanza da essere completata e "spuntata". Alcune attività dei
> clienti/fornitori (⏳) si avviano prima. Le caselle `[ ]` si spuntano a fase conclusa.

### Fase 1 — Posizionamento onesto e basi commerciali
**Obiettivo:** poter vendere/presentare il software senza promesse false.
**Perché:** ti protegge legalmente e definisce cosa stai vendendo.

- [ ] 🧑 Scrivi una frase di posizionamento: *"software che prepara e gestisce gli
  adempimenti; l'invio agli enti avviene tramite provider accreditato o il canale del
  professionista."*
- [ ] 🧑 Decidi il prezzo (es. canone mensile per studio + per azienda gestita).
- [x] 🔧 L'agente prepara: posizionamento, disclaimer, bozza Termini di Servizio, listino.
  *(fatto: cartella `docs/commerciale/` — POSIZIONAMENTO, DISCLAIMER, TERMINI-DI-SERVIZIO-BOZZA, LISTINO-PREZZI)*
- [ ] 🧑 Fai rivedere Termini/Privacy a un legale o usa template affidabili. 💶 ~0–500 €.

⏱️ 1 settimana · 💶 0–500 € · ✅ **Fatto quando:** hai un foglio con offerta, prezzo,
disclaimer e termini.

---

### Fase 2 — Fondamenta tecniche di produzione
**Obiettivo:** il software regge clienti veri senza perdere dati.
**Perché:** oggi il database si crea "in modo da sviluppo"; in produzione serve il metodo giusto.

- [x] 🔧 Migrazioni EF Core al posto di `EnsureCreated` — un assembly per provider
  (`Paghe.Migrazioni.Sqlite`, `Paghe.Migrazioni.Postgres`), migrazione iniziale generata e DDL
  scritto a mano rimosso. I database già esistenti vengono adottati all'avvio senza perdere dati
  (provato su SQLite e PostgreSQL). Come si aggiunge una migrazione: `docs/PRODUZIONE.md` §2.
- [x] 🔧 Chiave JWT come **segreto**: in Production rifiuta default/chiavi deboli (verificato a
  runtime). Stringa di connessione da variabile d'ambiente. Vedi `docs/PRODUZIONE.md` §1.
- [x] 🔧 **Backup automatici**: script `scripts/backup-db.sh` (SQLite/PostgreSQL, rotazione 14).
- [x] 🔧 Pagina "stato del sistema": endpoint `GET /health` (già presente). Log: vedi §5.
- [ ] 🧑 Scegli dove ospitarlo (un server cloud) e segui la checklist `docs/PRODUZIONE.md`. 💶 ~50–300 €/anno.

⏱️ 1–1,5 mesi (🔧) · 💶 ~50–300 €/anno hosting · ✅ **Fatto quando:** il software è online,
con backup, e si aggiorna senza cancellare i dati.

---

### Fase 3 — GDPR / privacy (obbligatorio)
**Obiettivo:** proteggere i dati personali come impone la legge.
**Perché:** gestisci dati di dipendenti (IBAN, malattia): senza questo non puoi vendere.

- [x] 🔧 Cifratura dei dati sensibili nel database (IBAN, dati sanitari/malattia). Codice e
  cutover verificati su SQLite e PostgreSQL; l'attivazione reale richiede secret manager,
  finestra senza traffico e restore di un backup cifrato provato.
- [x] 🔧 **Log degli accessi e delle modifiche**: richieste autenticate, accessi, errori e
  modifiche persistite sono registrati senza valori sensibili, isolati per tenant e consultabili
  solo da admin/super-admin. Verificati append-only applicativo, UTC, RBAC e 559 test; limiti e
  gestione operativa in `docs/GDPR-REGISTRO-ATTIVITA.md`.
- [ ] 🔧 Cancellazione/retention dei dati (quanto si tengono, poi si rimuovono).
- [ ] 🧑 Privacy policy + **DPA** (contratto privacy verso lo studio cliente). 💶 ~0–500 €.
- [ ] 🧑 Registro dei trattamenti (lo prepara l'agente, tu lo firmi).

⏱️ 1–1,5 mesi · 💶 ~0–500 € · ✅ **Fatto quando:** dati sensibili cifrati, c'è il log accessi
e hai i documenti privacy pronti.

---

### Fase 4 — Invio reale delle fatture allo SdI (provider A-Cube)
**Obiettivo:** le fatture vengono **spedite davvero**, non solo preparate.
**Perché:** è la funzione più richiesta e quella che rende il prodotto "vero".

- [ ] 🧑 (già avviato) Account A-Cube **sandbox** → poi attiva il piano di **produzione**.
  ⏳ contratto 2–4 settimane.
- [ ] 🔧 Completare e collaudare il connettore A-Cube in sandbox (invio + ricezione esiti).
- [ ] 🔧 Gestione automatica delle ricevute SdI (consegnata, scartata, ecc.).
- [ ] 🔧 Inserire le credenziali come **segreti** (mai nel codice).
- [ ] 🧑 Test con una fattura reale in sandbox, poi passaggio a produzione.

⏱️ 2 settimane 🔧 + ⏳ 2–4 sett. contratto · 💶 ~50–300 €/anno secondo i volumi ·
✅ **Fatto quando:** invii una fattura di prova e ricevi l'esito "consegnata" dallo SdI.

---

### Fase 5 — Firma digitale reale (SOLO se serve)
**Obiettivo:** firmare davvero fatture e documenti.
**Perché:** il motore di firma c'è già; manca solo il certificato e il collaudo.

> ⚠️ **Importante — forse non ti serve, non comprare subito:**
> - La firma sulla fattura è **obbligatoria SOLO verso la Pubblica Amministrazione (PA)**.
>   Per le fatture B2B/B2C lo SdI **non** la richiede.
> - **A-Cube può firmare per te** (quando serve), così non compri nessun certificato.
> - Il certificato dura **~3 anni** (non è "per sempre"), poi si rinnova. Quello personale
>   chiede un OTP a ogni firma → non adatto a firmare tante fatture in automatico; per quello
>   serve la "firma automatica/sigillo", che costa di più ed è **annuale**.
> - **Decisione rimandata:** comprare solo se servirai clienti verso la PA e se A-Cube non firma.

- [ ] 🧑 (solo se necessario) Procurati il certificato di firma. ⏳ riconoscimento 1–2 settimane.
- [ ] 🔧 Configurare il certificato nel software e collaudare la firma su una fattura vera.
- [ ] 🔧 (se usi smart card/CNS) verificare il funzionamento sul PC che firma.

⏱️ 2–3 giorni 🔧 + ⏳ 1–2 sett. · 💶 ~30–60 € ogni ~3 anni (se serve) · ✅ **Fatto quando:**
firmi un file e la firma risulta valida — oppure hai deciso che firma A-Cube e non serve.

---

### Fase 5-bis — Presenze e cedolino professionale
**Obiettivo:** sostituire il PDF dimostrativo con un flusso paghe tracciabile, senza presentarlo
come ufficiale prima dei controlli e della validazione professionale.

- [x] 🔧 **Primo blocco mensile**: il cruscotto Presenze raccoglie e conserva straordinari,
  ferie, ROL, ex-festività, malattia, infortunio e assenze non retribuite; richiede conferma prima
  dell'elaborazione, valida quantità e calendario e riporta le causali nel PDF. Il PDF usa ora
  un prospetto A4 professionale e compatto, con griglia anagrafica/rapporto, tabella voci dominante,
  quadri contributivi/fiscali, ratei e netto conclusivo; l'allegato presenze viene aggiunto solo
  quando esistono calendario giornaliero o variabili da dettagliare. IBAN mascherato, font incorporati,
  testata ripetuta nelle continuazioni e controlli su storni/valori mancanti; resta marcato “bozza da
  validare” e non viene qualificato come LUL.
- [x] 🔧 Corretto il doppio pagamento di malattia/infortunio: la quota ordinaria delle giornate
  viene stornata prima di applicare indennità e integrazioni (ancora da validare per CCNL).
- [x] 🔧 **Revisione fail-closed Officina Nord, luglio 2026 (25 agosto 2026)**: H011 richiede
  ATECO, visura e stato Albo artigiani documentati; il livello III espone paga base **1.453,94 €**
  e contingenza comprensiva di EDR **527,90 €**, uno scatto **21,95 €** dal 01/10/2025 e il
  terzo elemento Milano **11,36 €** solo con fonte territoriale. Il fisso di simulazione è
  **2.015,15 €**, tariffa oraria **11,9949 €**, giornaliera **77,5058 €** e TFR per codice
  **162,40 €**. La quattordicesima resta separata; calendario, progressivi, eventi, domicilio,
  conguagli e profilo CSC/CA/FIS sono bloccanti. Ferie, ROL (**16,33 h**) ed ex festività
  (**18,67 h**) sono distinti; prestazioni recuperabili e costi parziali stimati sono separati.
  CF/P.IVA passano il checksum e tutti i PDF restano sempre «Prospetto paga di simulazione –
  non costituisce LUL» con «Bozza / non certificato».
- [x] 🔧 **Anteprima busta agricola OTD (25 agosto 2026)**: pagina SPA dedicata, calcolo
  collegato al rapporto del tenant e PDF A4 agricolo con ore e giornate INPS distinte, tariffa
  CPL a cinque decimali, contributi, arrotondamenti, fonte e avviso di bozza. Provincia, livello,
  tipo e residenza arrivano dal rapporto, non dal browser. OTI è riconosciuto ma resta fail-closed
  in motore, API e UI: le tabelle/golden oggi validati sono solo OTD e non vengono riusati. Il
  flusso cedolini ordinario rifiuta
  `AGRICOLTURA_OPERAI` per non produrre TFR/ROL/UniEmens ordinari errati. Il blocco non archivia,
  non chiude e non genera LUL, PosAgri o F24 finché non esiste il workflow agricolo completo.
- [ ] 🔧 **Cartellino giornaliero**: ore ordinarie, causale per data/intervallo, PUC malattia,
  controlli di sovrapposizione e quadratura con l'orario teorico; salvataggio separato dal calcolo.
- [ ] 🔧 Rendere tutte le regole di maturazione ferie/ROL e malattia/infortunio versionate per
  anno e CCNL nel DB; eliminare i valori ancora cablati nel motore.
- [ ] 🔧 Ciclo bozza → controllata → validata → chiusa, snapshot immutabile di dati/voci/parametri
  e PDF dallo snapshot; aggiungere codice unico CNEL, firma/sigla/timbro e calendario.
- [ ] 🧑 Far validare casi reali, regole e fac-simile da un Consulente del Lavoro; scegliere e
  formalizzare la modalità di tenuta del Libro Unico del Lavoro.

Ricerca, confronto Zucchetti/TeamSystem/INAZ e backlog: `docs/RICERCA-CEDOLINO-PROFESSIONALE-2026.md`.

⏱️ 2–4 mesi 🔧 + validazione professionale · ✅ **Fatto quando:** il caso mensile completo passa
diagnostica, calcolo e confronto con cedolino campione; documento e LUL rispettano il processo
scelto e validato dal professionista.

---

### Fase 6 — UniEmens INPS + INAIL (il buco grosso delle paghe)
**Obiettivo:** generare il flusso mensile contributi INPS (UniEmens) e l'autoliquidazione INAIL.
**Perché:** senza UniEmens un consulente del lavoro non può usarlo per le paghe.

- [x] 🔧 Modello dati e generatore XML **UniEmens** dai cedolini archiviati. *(fatto: prima
  versione — `Paghe.Core/AdeTelematico/UniEmens` + `UniEmensService` + endpoint
  `POST /api/aziende/{id}/uniemens/mese`; bozza da validare, Qualifica/Giorni da affinare)*
- [x] 🔧 Far passare il file dal **software di controllo ufficiale INPS** (validazione).
  *(fatto il 24 agosto 2026: il generatore è stato riscritto sul tracciato ufficiale — radice
  `DenunceMensili`, schema 4.32.0 incorporato e validato a ogni build — e un flusso prodotto da
  PaghePro è stato elaborato dal validatore automatico INPS 4.2.0 con esito **RC=OK, 0 errori
  bloccanti**, con produzione del file `.UniEM` e della ricevuta. Le regole emerse dai controlli
  reali (unità operativa/produttiva obbligatorie, giorni contribuiti, retribuzione teorica,
  calendario giornaliero sempre esposto) sono nel codice e nei test. Restano da raccogliere in
  anagrafica i dati di inquadramento contributivo. Procedura ripetibile in
  `docs/UNIEMENS-CONTROLLO-UFFICIALE-INPS.md`)*
- [x] 🔧 **Anagrafica contributiva del lavoratore** *(fatto il 24 agosto 2026: cittadinanza,
  tipo di part-time, qualifica professionale ISTAT e tipo di contribuzione si registrano nella
  scheda del dipendente, con i formati validati al salvataggio e migrazioni per SQLite e
  PostgreSQL. Il codice contratto CNEL arriva dal CCNL dell'anno. Quel che manca resta
  dichiarato in `daCompletare`, mai inventato. Corretto un difetto emerso dai test: nomi e
  ragioni sociali vanno nel file in maiuscolo e senza accenti, altrimenti l'INPS li scarta —
  l'archivio continua a conservare la grafia vera. Restano fuori eventi tutelati, conguagli,
  gestioni speciali e la schermata SPA)*
- [x] 🔧 Autoliquidazione **INAIL** (calcolo e file). *(fatto: calcolo già esisteva
  (`AutoliquidazioneInail` + `InailService`, pagina Fine anno); aggiunta la pratica archiviata
  con stato — `PraticaInailEntity`/`PraticheInailService`, endpoint salva/elenco/get/stato/bozza
  — e il pulsante "Salva pratica"/"Scarica bozza" in Fine anno. Bozza leggibile, non un tracciato
  telematico ufficiale: l'invio resta sui servizi online INAIL, nessuna specifica tecnica del
  file di autoliquidazione trovata nel kit ufficiale locale. Base imponibile = imponibile INPS
  (semplificazione preesistente); niente multi-PAT)*
- [ ] 🧑 L'invio avviene dal cassetto previdenziale INPS / canale del consulente.

⏱️ 1,5–2,5 mesi · 💶 0 € (strumenti INPS gratuiti) · ✅ **Fatto quando:** il file UniEmens di
un mese passa il controllo ufficiale INPS senza errori.

---

### Fase 7 — Controlli ufficiali Agenzia Entrate (F24, CU, LIPE, 770)
**Obiettivo:** i file fiscali superano i controlli ufficiali prima dell'invio.
**Perché:** l'AdE scarta i file non conformi; il controllo va fatto **prima**.

- [ ] 🔧 Integrare i **moduli di controllo ufficiali (JAR)** dell'AdE (richiede Java).
- [ ] 🔧 Generare e validare: F24, Certificazione Unica (CU), LIPE (IVA), 770.
- [ ] 🧑 Procurati il **kit ufficiale AdE** (download gratuito) e Java sul server.
- [ ] 🧑 L'invio avviene da **Entratel** (di solito il commercialista è già abilitato).

⏱️ 1–1,5 mesi · 💶 0 € · ✅ **Fatto quando:** un F24 e una CU passano i controlli ufficiali AdE.

---

### Fase 8 — Conservazione a norma
**Obiettivo:** archiviare fatture e documenti per 10 anni in modo legale.
**Perché:** è obbligatorio per le fatture elettroniche.

- [ ] 🧑 Scegli un servizio di conservazione accreditato (es. Aruba, A-Cube spesso lo include).
- [ ] 🔧 Collegare il software al servizio di conservazione (invio automatico dei documenti).

⏱️ 2–3 settimane · 💶 ~30–120 €/anno (piccoli volumi) · ✅ **Fatto quando:** una fattura
inviata risulta presa in conservazione con ricevuta.

---

### Fase 9 — Validazione professionale, contratto, assicurazione, lancio
**Obiettivo:** poter vendere in sicurezza.
**Perché:** vendi software fiscale: se sbaglia, il danno è del cliente. Serve copertura.

- [ ] 🧑 Il **commercialista pilota** valida i calcoli (cedolini, F24, ecc.).
- [ ] 🧑 Contratto di licenza/servizio con limitazione di responsabilità (con un legale).
- [ ] 🧑 **Assicurazione RC professionale/prodotto** software. 💶 ~da qualche centinaio €/anno.
- [ ] 🧑 Definisci l'assistenza clienti (almeno email + tempi di risposta).
- [ ] 🔧 Sistemare le ultime segnalazioni emerse dal pilota.

⏱️ 1–1,5 mesi (in parallelo) · 💶 contratto + assicurazione · ✅ **Fatto quando:** hai un
cliente pilota soddisfatto, contratto e polizza.

---

## 6. Riepilogo tempi e tappe

| Tappa | Cosa ottieni | Fasi | Tempo cumulato (realistico) |
|---|---|---|---|
| **Tappa A — MVP vendibile** | Fatture inviate davvero, firma reale, online e a norma privacy | 1→5 | **~3–4 mesi** |
| **Tappa B — "come gli altri"** | Ciclo paghe completo (UniEmens) + adempimenti controllati + conservazione + validato | 6→9 | **~9–15 mesi totali** |

**Attenzione (verità):** dopo il lancio resta un **costo permanente** = manutenzione
normativa ogni anno (aliquote, CCNL, tracciati cambiano) + assistenza clienti. Non è un
lavoro "una volta e basta".

---

## 7. Stima costi (indicativa, primo anno)

| Voce | Costo |
|---|---|
| Hosting/server + backup | ~50–300 €/anno |
| Provider SdI (A-Cube) | ~50–300 €/anno (secondo volumi) |
| Certificato di firma | ~30–60 € |
| Conservazione a norma | ~30–120 €/anno |
| Legale (termini, privacy, DPA, contratto) | ~0–1.000 € una tantum |
| Assicurazione RC | ~da qualche centinaio €/anno |
| Strumenti INPS/AdE (controlli, Entratel) | 0 € |
| **Totale primo anno (ordine di grandezza)** | **~500–2.500 €** |

---

## 8. Come procediamo (metodo di lavoro)

1. Le attività 🔧 le fa l'agente AI, una fase alla volta, con build e test verdi.
2. Le attività 🧑/⏳ le fai tu: te le indico **una per volta**, con istruzioni passo-passo.
3. A ogni fase conclusa si spuntano le caselle qui sopra e si aggiorna `CLAUDE.md`.
4. Avanziamo nell'ordine, ma le **attese lunghe** (sezione 4) partono subito.

➡️ **Prossimo passo operativo:** vedi `AGENTS.md` per come l'agente esegue, e dimmi quando
hai avviato i 3 punti della sezione 4. Intanto l'agente può partire dalla **Fase 2** o dalla
**Fase 6 (UniEmens)** — entrambe sono solo codice e non aspettano nessuno.
