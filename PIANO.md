# PIANO — Da MVP a software vendibile ai commercialisti

> Documento guida del progetto. Scritto in modo semplice: **non serve essere del mestiere
> per seguirlo.** Ogni fase dice *cosa fai tu* e *cosa fa l'agente AI (il codice)*.
> Aggiornato al: 2026-06-27.

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
- ✅ Autenticazione, multi-azienda (multi-tenant), 158 test automatici verdi.

**Manca per essere un prodotto vero da vendere:**
- ❌ Invio reale allo SdI (serve account provider).
- ❌ Certificato di firma reale.
- ❌ **UniEmens INPS** e **INAIL** (grosso buco lato paghe).
- ❌ Controlli ufficiali AdE eseguiti (Entratel + moduli JAR) su F24/CU/LIPE/770.
- ❌ Conservazione a norma.
- ❌ GDPR (cifratura dati sensibili, log degli accessi, retention).
- ❌ Fondamenta di produzione (migrazioni database, segreti, backup, deploy stabile).
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
- [ ] 🔧 L'agente prepara: pagina di presentazione, testo del disclaimer ("valori da validare
  con professionista"), bozza di Termini di Servizio.
- [ ] 🧑 Fai rivedere Termini/Privacy a un legale o usa template affidabili. 💶 ~0–500 €.

⏱️ 1 settimana · 💶 0–500 € · ✅ **Fatto quando:** hai un foglio con offerta, prezzo,
disclaimer e termini.

---

### Fase 2 — Fondamenta tecniche di produzione
**Obiettivo:** il software regge clienti veri senza perdere dati.
**Perché:** oggi il database si crea "in modo da sviluppo"; in produzione serve il metodo giusto.

- [ ] 🔧 Migrazioni EF Core (sostituire `EnsureCreated`) — aggiornamenti DB sicuri.
- [ ] 🔧 Chiave JWT e password DB come **segreti** (non nel codice).
- [ ] 🔧 Deploy stabile (server/hosting) + **backup automatici** del database.
- [ ] 🔧 Log di base e pagina "stato del sistema".
- [ ] 🧑 Scegli dove ospitarlo (un server cloud). 💶 ~50–300 €/anno.

⏱️ 1–1,5 mesi (🔧) · 💶 ~50–300 €/anno hosting · ✅ **Fatto quando:** il software è online,
con backup, e si aggiorna senza cancellare i dati.

---

### Fase 3 — GDPR / privacy (obbligatorio)
**Obiettivo:** proteggere i dati personali come impone la legge.
**Perché:** gestisci dati di dipendenti (IBAN, malattia): senza questo non puoi vendere.

- [ ] 🔧 Cifratura dei dati sensibili nel database (IBAN, dati sanitari/malattia).
- [ ] 🔧 **Log degli accessi e delle modifiche** (chi ha visto/cambiato cosa).
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

### Fase 6 — UniEmens INPS + INAIL (il buco grosso delle paghe)
**Obiettivo:** generare il flusso mensile contributi INPS (UniEmens) e l'autoliquidazione INAIL.
**Perché:** senza UniEmens un consulente del lavoro non può usarlo per le paghe.

- [ ] 🔧 Modello dati e generatore XML **UniEmens** dai cedolini archiviati.
- [ ] 🔧 Far passare il file dal **software di controllo ufficiale INPS** (validazione).
- [ ] 🔧 Autoliquidazione **INAIL** (calcolo e file).
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
