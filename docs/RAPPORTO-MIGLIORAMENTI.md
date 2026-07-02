# Rapporto miglioramenti — PaghePro (2 luglio 2026)

Scritto in linguaggio semplice per chi non è tecnico. Quattro aree di lavoro:
sicurezza, accesso, affidabilità dei calcoli, nuova interfaccia. Tutto è stato
verificato: 380 test automatici verdi e prova diretta dell'app nel browser
(desktop e telefono).

---

## 1. Sicurezza — cosa è stato rinforzato

Dettagli tecnici completi in `docs/AUDIT-SICUREZZA.md`. In sintesi:

- **Prima qualunque sito web poteva chiamare la tua API** dal browser di un utente.
  Ora in produzione possono farlo solo i siti che autorizzi tu (vedi "Cosa devi fare tu").
- **Un difetto serio corretto**: chiunque avesse un account poteva modificare le
  tabelle dei contratti (CCNL) usate nei calcoli **di tutti gli altri clienti**.
  Ora può farlo solo il proprietario della piattaforma (tu).
- **Un secondo difetto serio corretto**: se in un server si dimenticava una certa
  variabile di configurazione, l'app partiva con la chiave di firma "di sviluppo"
  (nota pubblicamente, perché è nel codice). Ora in quel caso l'app **si rifiuta
  di partire** e spiega cosa manca.
- La pagina che elenca tutti gli indirizzi dell'API (Swagger) non è più pubblica
  in produzione.
- Chi carica file (visura camerale, UNILAV) ora ha limiti: massimo 10 MB e solo
  PDF/immagini.
- La creazione di account è protetta dagli abusi (massimo 10 tentativi al minuto
  per indirizzo, come già il login).
- Header di sicurezza standard su ogni risposta del server (protezioni del browser
  contro clickjacking e simili) + HSTS in produzione.
- **Isolamento dei dati tra clienti verificato con test automatici**: un cliente che
  conosce gli ID di un altro NON può leggere né modificare i suoi cedolini, fatture,
  bilanci, F24 e Certificazioni Uniche. Nessun segreto (password, chiavi) è salvato
  nel codice.

## 2. Accesso — ora senza intoppi

- Password sbagliata: ora l'app dice chiaramente "Email o password non corretti"
  (prima mostrava "Errore 401").
- **Chi attiva la verifica in due passaggi (2FA) ora può davvero entrare dall'app**:
  compare il campo per il codice a 6 cifre. Prima il campo non esisteva e l'utente
  restava bloccato.
- Accesso con Google: il codice è pronto e gestisce bene token falsi o scaduti
  (provato). Se non è ancora configurato, il bottone spiega la situazione invece
  di dare errore.
- Chi è entrato solo con Google non può essere "scavalcato" da un login con
  password vuota (verificato con un test dedicato).
- Provati dal vivo: registrazione, email doppia, password corta, password errata,
  blocco anti-forza-bruta (dall'11° tentativo in un minuto risponde "troppe
  richieste"), studio sospeso, super-admin.

## 3. Calcoli — coerenza verificata da capo a fondo

- Nuovo test "anno intero": 18 cedolini (un dipendente full-time tutto l'anno +
  un part-time 50% assunto a luglio). Verifica che **cedolini archiviati,
  Certificazione Unica, Modello 770 e i 12 F24 mensili raccontino esattamente gli
  stessi numeri**, al centesimo: IRPEF, addizionali regionale/comunale, contributi INPS.
- Verificato che ogni importo salvato è arrotondato al centesimo e che
  netto = competenze − trattenute, sempre.
- I golden test esistenti su buste paga reali (INPS, IRPEF 2026, minimi CCNL dal
  CNEL, voci variabili di 5 CCNL) restano tutti verdi.
- Quello che il software **non calcola ancora da solo** è scritto nero su bianco in
  `docs/TODO-DA-VERIFICARE.md` invece di essere inventato: assunzione/cessazione a
  metà mese, tredicesima dentro il cedolino di dicembre, conguaglio automatico,
  malattia con integrazione INPS. Da definire con un Consulente del Lavoro.

## 4. Nuova interfaccia "vetro" — pronta da mostrare

- Tema scuro elegante con effetto vetro (sfocatura, bordi luminosi sottili, ombre
  morbide) su **tutte le pagine**, guidato da un unico design system: cambiare un
  colore in un punto lo cambia ovunque.
- Animazioni leggere: le card entrano con un effetto 3D sottile e a cascata,
  i bottoni rispondono al passaggio del mouse. Tutto in CSS puro (zero librerie
  aggiunte, l'app resta leggera: ~99 KB compressa). Chi ha impostato "riduci
  movimento" sul telefono non riceve animazioni.
- **Telefono**: il menu ora è una barra compatta scorrevole in alto — prima
  occupava tutto lo schermo e bisognava scorrere 28 voci per vedere il contenuto.
  Icona e colori dell'app installata (PWA) allineati al tema scuro.
- Verificato dal vivo nel browser che i flussi principali funzionano con la nuova
  veste: login, calcolo e archiviazione di un cedolino vero, CU, scadenzario,
  fatturazione, bilancio.

## 5. Inserimento automatico da file (OCR) — verificato e completato

- **Visura camerale**: carichi il PDF o una **foto/scansione** (letta con OCR Tesseract
  locale, nessun servizio esterno) e il form azienda **si compila da solo**; i campi
  restano modificabili e salvi solo dopo averli controllati. Provato dal vivo con una
  visura di test: ragione sociale, P.IVA, CF, indirizzo, CAP, comune, ATECO e legale
  rappresentante estratti correttamente. Migliorato il lettore per il layout
  "COMUNE: MILANO (MI)" tipico delle scansioni.
- **UNILAV**: carichi il PDF della Comunicazione Obbligatoria e vengono creati azienda,
  lavoratore e rapporto. **Novità**: se l'import legge male un dato, nella scheda del
  dipendente c'è ora il bottone **"✏️ Correggi dati"** — nome, cognome, codice fiscale,
  IBAN, qualifica, livello, mensilità, part-time, scatti, superminimo e data assunzione
  si correggono e si salvano (prima non era possibile). Protetto: ogni studio corregge
  solo i propri dipendenti; un dipendente anonimizzato (GDPR) non è modificabile.
- **UNILAV anche da foto**: ora la Comunicazione Obbligatoria si può caricare anche come
  foto/scansione (JPG, PNG), non solo come PDF testuale — la legge lo stesso OCR locale.
  Provato dal vivo: datore, lavoratore, codice fiscale, data assunzione e qualifica
  estratti correttamente, con avvertenze chiare su cosa completare a mano.
- **Correzione anche per le aziende**: come per i dipendenti, ogni azienda salvata ha ora
  il bottone "✏️ Modifica" — se la visura è stata letta male (o l'UNILAV non aveva
  l'indirizzo), si corregge e si salva. Protetto per studio (tenant).
- **PDF busta paga e PDF fattura** verificati: si generano correttamente.
- Requisito per l'OCR da foto sul server: Tesseract installato (su questo computer c'è
  già; su un server Linux: `apt-get install tesseract-ocr tesseract-ocr-ita`). Senza,
  i PDF testuali funzionano comunque e le foto danno un messaggio chiaro.

---

## Cosa devi fare tu (quando vuoi andare online)

1. **Google login** (5 minuti): segui `docs/GUIDA-GOOGLE-LOGIN.md` — crei l'ID
   client su Google Cloud e lo incolli in `Google:ClientId` (backend) e
   `VITE_GOOGLE_CLIENT_ID` (frontend). Senza, l'app funziona comunque con
   email + password.
2. **Domini autorizzati (CORS)**: sul servizio backend imposta la variabile
   `Cors__Origini__0` = indirizzo pubblico del tuo frontend
   (es. `https://paghe-frontend.onrender.com`). In `render.yaml` è già predisposta.
3. **Account demo**: la "Prova la demo" è uno spazio condiviso tra tutti i
   visitatori. Va benissimo per la vetrina; quando arrivano clienti veri,
   decidere se azzerarlo periodicamente o toglierlo.
4. **Prima di dati reali di dipendenti**: attivare il disco persistente su Render
   (istruzioni già in `render.yaml`), pianificare i backup
   (`scripts/backup-db.sh`) e completare la parte GDPR (cifratura campi sensibili,
   retention) — è nel piano, non ancora completa.
5. **Validazione professionale**: i valori normativi sono verificati sulle fonti
   pubbliche e su buste reali, ma prima dell'uso commerciale serve la revisione di
   un Consulente del Lavoro (elenco preciso in `docs/TODO-DA-VERIFICARE.md`).
