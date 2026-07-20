# Mappa dati privacy per App Store e Google Play — bozza tecnica

Questa mappa deriva dal codice e serve a compilare i moduli con il titolare/privacy advisor. Non è
un'informativa legale e non sostituisce la verifica dei dati realmente trattati in produzione.

## Dati potenzialmente raccolti e collegati all'account

| Categoria store | Esempi PaghePro | Finalità prevista | Note da verificare |
|---|---|---|---|
| Dati di contatto | nome, email, PEC, telefono, indirizzo | account e funzionalità dell'app | distinguere utente, cliente, dipendente |
| Identificativi | ID utente, tenant, azienda, dipendente | autenticazione e separazione studi | nessun advertising ID intenzionale |
| Informazioni finanziarie | IBAN, retribuzioni, fatture, imposte, contributi | paghe, contabilità e adempimenti | dati ad alta sensibilità operativa |
| Informazioni occupazionali | contratto, livello, presenze, assenze, rapporto | elaborazione paghe | collegate al lavoratore |
| Informazioni sensibili/sanitarie | eventi di malattia e assenze correlate | obblighi paghe/previdenziali | minimizzare; possibile DPIA |
| Documenti e contenuti utente | PDF, XML, visure, loghi, documenti caricati | funzioni richieste dall'utente | possono contenere ulteriori dati personali |
| Messaggi | email/PEC collegate dall'utente | posta e triage richiesti | dichiarare se la funzione entra nel rilascio mobile |
| Attività nell'app | registro accessi e modifiche | sicurezza, audit e conformità | definire tempi di conservazione |
| Diagnostica | log server, esiti e correlation ID | sicurezza e correzione errori | verificare hosting e subprocessori |

## Dichiarazioni tecniche correnti

- Trasporto: la build mobile accetta soltanto API HTTPS e Android vieta traffico in chiaro.
- Tracking pubblicitario: non implementato.
- Pubblicità: non implementata.
- Vendita/condivisione commerciale dei dati: non prevista dal codice; deve essere confermata dal
  titolare e dai contratti con fornitori.
- SDK nativi inclusi: Capacitor Core, App, Browser, Filesystem e Share.
- Google Identity Services: escluso dalla build mobile.
- File condivisi: copiati nella cache privata, passati al pannello nativo e rimossi dopo la
  richiesta di condivisione.
- Backup Android dell'app: disabilitato.

## Questioni aperte che impediscono dichiarazioni definitive

- Elenco reale di hosting, email, AI, firma, SdI, notifiche e altri subprocessori abilitati in
  produzione.
- Paesi di trattamento e trasferimenti extra SEE.
- Tempi di conservazione per ogni classe di dato e gestione degli obblighi fiscali/lavoristici.
- Processo verificato di accesso, esportazione, rettifica e cancellazione.
- Politica dei log e dati diagnostici del provider di hosting.
- Eventuale telemetria futura: richiede aggiornamento preventivo dei moduli store e
  dell'informativa.

## Risposte da non selezionare finché il gate non è chiuso

- Non dichiarare che l'utente può cancellare i dati finché flusso in-app e URL pubblico non sono
  attivi e collaudati.
- Non dichiarare “nessun dato raccolto”: PaghePro tratta dati account e dati professionali.
- Non dichiarare cifratura completa a riposo senza verifica di database, backup, file e segreti.
- Non usare la scheda privacy di sviluppo come prova dell'esattezza del modulo store.

