// Aiuto semplice, una scheda per ogni sezione/servizio.
// Lingua volutamente facile: l'utente non è tecnico.
// Ogni scheda: a cosa serve (1 frase) + passi numerati + domande frequenti.

export interface SchedaAiuto {
  titolo: string;        // nome chiaro della sezione
  aCosaServe: string;    // una frase semplice
  passi: string[];       // come si fa, passo per passo
  faq?: { domanda: string; risposta: string }[];
}

// La chiave coincide con la SectionKey della Dashboard.
export const aiutoPerSezione: Record<string, SchedaAiuto> = {
  audit: {
    titolo: "Registro attività",
    aCosaServe:
      "Mostra chi ha consultato o modificato i dati dello studio e se l'operazione è riuscita.",
    passi: [
      "Usa Tipo attività per separare consultazioni e modifiche ai dati.",
      "Scegli un intervallo di date se devi controllare un periodo preciso.",
      "Leggi Utente, Risorsa ed Esito: il registro non mostra i contenuti o i valori cambiati.",
      "Se trovi un accesso negato o un errore inatteso, annota data e ora e fallo verificare all'amministratore tecnico.",
    ],
    faq: [
      {
        domanda: "Posso cambiare o cancellare una riga?",
        risposta: "No. Il registro è in sola lettura per preservare la tracciabilità.",
      },
    ],
  },

  dashboard: {
    titolo: "Home studio",
    aCosaServe:
      "È il punto di partenza: prima scegli l'azienda cliente, poi apri gli strumenti e controlli il suo lavoro.",
    passi: [
      "Se non hai ancora clienti, premi 'Crea azienda'.",
      "Se hai già clienti, cerca per nome, partita IVA, codice fiscale o comune e premi 'Scegli'.",
      "Apri uno dei Prossimi passi: Dipendenti, Presenze, Elabora cedolini o Scadenze.",
      "Nella colonna 'Riprendi il lavoro' ritrovi gli ultimi strumenti aperti insieme al cliente corretto.",
    ],
    faq: [
      {
        domanda: "I numeri sono veri?",
        risposta:
          "Sì. Compaiono soltanto dopo la scelta dell'azienda e arrivano dai suoi dati. Se il caricamento fallisce, la Home mostra un errore invece di numeri finti.",
      },
      {
        domanda: "Che cosa significa 'Riprendi il lavoro'?",
        risposta:
          "Sono scorciatoie agli ultimi strumenti che hai aperto. Ogni riga ricorda anche l'azienda, così riapri il cliente giusto.",
      },
    ],
  },

  aziende: {
    titolo: "Aziende clienti",
    aCosaServe:
      "Qui tieni l'elenco delle aziende per cui lavori, con i loro dati (nome, partita IVA, indirizzo).",
    passi: [
      "Premi 'Aggiungi azienda' per inserirne una nuova.",
      "Scrivi nome, partita IVA e indirizzo. La partita IVA serve per fatture e adempimenti.",
      "Salva. Da quel momento puoi scegliere quell'azienda in alto e lavorarci.",
      "Per cambiare azienda usa il pulsante con il nome dell'azienda in alto a sinistra.",
    ],
    faq: [
      {
        domanda: "Devo per forza inserire la partita IVA?",
        risposta:
          "Sì, per emettere fatture e preparare F24/contributi serve. Senza, alcune funzioni non partono.",
      },
    ],
  },

  dipendenti: {
    titolo: "Dipendenti",
    aCosaServe:
      "Qui ci sono tutte le persone che lavorano nell'azienda. Da qui si parte per fare la busta paga.",
    passi: [
      "Premi 'Nuovo dipendente' per aggiungere una persona.",
      "Inserisci nome, codice fiscale, data di assunzione e il contratto (CCNL e livello).",
      "Salva: la persona compare nell'elenco.",
      "Clicca sul nome per vedere la sua scheda e i suoi cedolini.",
    ],
    faq: [
      {
        domanda: "Cos'è il 'livello'?",
        risposta:
          "È l'inquadramento previsto dal contratto (CCNL). Decide lo stipendio minimo. In caso di dubbio chiedi al Consulente del Lavoro.",
      },
      {
        domanda: "Posso importare da un file?",
        risposta:
          "Sì, se hai la comunicazione UNILAV (XML o PDF) puoi caricarla e il dipendente viene creato da solo.",
      },
    ],
  },

  dipendente: {
    titolo: "Scheda del dipendente",
    aCosaServe:
      "È la pagina di una singola persona: dati, contratto e tutti i suoi cedolini.",
    passi: [
      "Controlla che i dati (codice fiscale, contratto, paga) siano giusti.",
      "Da qui puoi creare la busta paga di questa persona.",
      "Trovi anche lo storico dei cedolini già fatti.",
    ],
  },

  presenze: {
    titolo: "Presenze (cartellino)",
    aCosaServe:
      "Qui segni i giorni lavorati, le ferie, le malattie e i permessi del mese. Serve prima di fare la busta paga.",
    passi: [
      "Scegli il mese e il dipendente.",
      "Segna i giorni: lavorato, ferie, malattia, permesso.",
      "Controlla che i totali del mese tornino.",
      "Quando è completo, sei pronto per calcolare il cedolino.",
    ],
    faq: [
      {
        domanda: "Perché alcuni cartellini sono 'incompleti'?",
        risposta:
          "Manca qualche giorno o c'è un'anomalia. Apri il cartellino segnalato e completa i giorni mancanti.",
      },
    ],
  },

  cedolini: {
    titolo: "Cedolini (buste paga)",
    aCosaServe:
      "È l'elenco delle buste paga. Qui vedi quelle fatte e ne puoi aprire o stampare una.",
    passi: [
      "Scegli il mese.",
      "Clicca su un dipendente per vedere la sua busta paga.",
      "Controlla lordo, trattenute e netto in busta.",
      "Da qui puoi scaricare/stampare il PDF da consegnare.",
    ],
    faq: [
      {
        domanda: "Il netto è quello che ricevo davvero?",
        risposta:
          "Il 'netto in busta' è quanto spetta al dipendente. È calcolato dal contratto e dalle tasse del mese.",
      },
    ],
  },

  agricoltura: {
    titolo: "Anteprima busta agricola",
    aCosaServe:
      "Calcola una bozza per operai agricoli OTD usando la tariffa del contratto provinciale e tenendo separate ore e giornate INPS. I rapporti OTI sono riconosciuti ma bloccati finche non esistono tabelle dedicate validate.",
    passi: [
      "Scegli un lavoratore con CCNL AGRICOLTURA_OPERAI.",
      "Controlla anno, provincia CPL e livello: devono avere una tabella disponibile con la fonte indicata.",
      "Inserisci ore effettive, giornate lavorate, giorni di detrazione e reddito annuo presunto reale.",
      "Calcola l'anteprima, controlla tutte le voci e scarica il PDF bozza.",
      "Non usare la bozza come LUL, PosAgri o F24: questi passaggi non sono ancora generati dalla funzione.",
    ],
    faq: [
      {
        domanda: "Perché servono sia ore sia giornate?",
        risposta: "Le ore determinano la paga; le giornate sono il dato previdenziale comunicato all'INPS. Non sono la stessa cosa e il programma non le ricava una dall'altra.",
      },
      {
        domanda: "Il costo azienda è completo?",
        risposta: "No. Il prospetto mostra solo le componenti note; i contributi datoriali definitivi sono determinati dall'INPS dopo la denuncia PosAgri.",
      },
    ],
  },

  calcolo: {
    titolo: "Calcolo della busta paga",
    aCosaServe:
      "Qui il programma fa i conti della busta paga: stipendio, contributi e tasse, fino al netto.",
    passi: [
      "Controlla che presenze e contratto siano giusti.",
      "Premi per calcolare: vedi lordo, contributi, tasse e netto.",
      "Se torna tutto, la busta viene salvata e la trovi tra i Cedolini.",
    ],
    faq: [
      {
        domanda: "I conti sono garantiti?",
        risposta:
          "Il calcolo segue le regole inserite, ma vanno sempre validate con un Consulente del Lavoro prima di usarle sul serio.",
      },
    ],
  },

  elaborazioni: {
    titolo: "Elaborazione cedolini (tutti insieme)",
    aCosaServe:
      "Serve a calcolare le buste paga di tutti i dipendenti del mese in un colpo solo, invece che una alla volta.",
    passi: [
      "Scegli il mese.",
      "Controlla che le presenze di tutti siano complete.",
      "Premi 'Elabora': il programma calcola tutte le buste.",
      "Alla fine le trovi nella sezione Cedolini.",
    ],
  },

  adempimenti: {
    titolo: "Adempimenti",
    aCosaServe:
      "È l'elenco delle cose obbligatorie da mandare allo Stato (tasse, contributi, comunicazioni) con le loro scadenze.",
    passi: [
      "Guarda la colonna 'Scadenza' per sapere cosa è urgente.",
      "Lo stato (in scadenza, bloccato, completato) ti dice a che punto sei.",
      "Da qui puoi preparare l'F24 da pagare.",
    ],
    faq: [
      {
        domanda: "Cosa vuol dire 'bloccato'?",
        risposta:
          "C'è qualcosa che impedisce di proseguire (un dato mancante o un errore). Apri la riga per capire cosa sistemare.",
      },
    ],
  },

  pratiche: {
    titolo: "Pratiche ComUnica (apri, cambia o chiudi un'attività)",
    aCosaServe:
      "Prepara le pratiche per aprire una partita IVA o un'impresa, cambiare i dati o cessare l'attività: il sistema ti dice quali enti coinvolgere (Agenzia Entrate, Registro Imprese, INPS, INAIL, SUAP) e prepara il riepilogo pronto da consegnare.",
    passi: [
      "Strada veloce: incolla i dati che hai (email, messaggio, appunti) nell'Assistente compilazione e premi 'Analizza e compila' — il modulo si riempie da solo e ti viene chiesto solo quello che manca.",
      "Oppure premi '+ Nuova pratica' e scegli il tipo: nuova impresa, variazione o cessazione.",
      "Compila o controlla i dati (nome, codice fiscale, attività ATECO, sede) e spunta le caselle giuste (commercio, artigiana, agricola, dipendenti, INAIL).",
      "Premi 'Simula piano adempimenti' per vedere in anticipo cosa serve, poi 'Crea pratica'.",
      "Lavora la checklist: segna ogni adempimento quando è pronto o presentato.",
      "Premi 'Genera distinta': ottieni il documento con tutti i dati, da copiare su ComunicaStarweb/DIRE o da dare al commercialista.",
      "Aggiorna lo stato della pratica man mano: Pronta → Presentata → Protocollata → Evasa.",
    ],
    faq: [
      {
        domanda: "La pratica parte da sola verso la Camera di Commercio?",
        risposta:
          "No. Il software prepara tutti i dati e la distinta, ma l'invio vero si fa sui canali ufficiali InfoCamere (ComunicaStarweb/DIRE con utenza Telemaco) o tramite un intermediario abilitato, ad esempio il commercialista.",
      },
      {
        domanda: "Quale modello Agenzia Entrate viene usato?",
        risposta:
          "Lo sceglie il sistema: AA9/12 per persone fisiche e professionisti, AA7/10 per società. Per variazione e cessazione serve indicare la partita IVA.",
      },
    ],
  },

  scadenzario: {
    titolo: "Scadenzario e promemoria",
    aCosaServe:
      "Ti ricorda le scadenze (F24, DURC...) prima che sia tardi e prepara già il messaggio da inviare al cliente.",
    passi: [
      "Apri la sezione: i promemoria si aggiornano da soli.",
      "Vedi cosa è 'scaduto' e cosa è 'in scadenza'.",
      "Copia il messaggio già pronto, oppure invialo per Email/SMS/WhatsApp.",
      "Quando hai fatto, segna il promemoria come 'completato'.",
    ],
    faq: [
      {
        domanda: "Manda i messaggi da solo?",
        risposta:
          "Prepara sempre il testo. Per inviarlo davvero servono i collegamenti a email o WhatsApp; altrimenti copi e incolli a mano.",
      },
      {
        domanda: "Posso richiedere il DURC direttamente dall'app?",
        risposta:
          "No: la richiesta si fa solo sul portale ufficiale 'Durc On Line' (INPS/INAIL) entrando con SPID, CIE o CNS — lo Stato non offre un canale per i software esterni. L'app però ti avvisa da sola quando è ora (banner in alto e campanella), il pulsante 'Richiedi Durc On Line' ti porta al portale giusto, e quando ricevi il nuovo DURC lo registri qui: vale 120 giorni e il promemoria riparte.",
      },
    ],
  },

  fatture: {
    titolo: "Fatturazione elettronica",
    aCosaServe:
      "Qui crei le fatture e le controlli prima di mandarle allo Stato (Sistema di Interscambio, SdI).",
    passi: [
      "Premi 'Nuova fattura' e inserisci cliente, righe e importi.",
      "Premi 'Valida': il programma controlla che sia giusta.",
      "Se è a posto, la firmi e la trasmetti allo SdI.",
      "Controlla l'esito (consegnata, scartata) e conservala.",
    ],
    faq: [
      {
        domanda: "Posso già inviarla allo Stato?",
        risposta:
          "Per l'invio reale serve collegare un provider accreditato (vedi 'Collega Fatturazione'). Senza, la fattura viene preparata ma non spedita.",
      },
    ],
  },

  pagamenti: {
    titolo: "Pagamenti clienti",
    aCosaServe:
      "Tieni il conto di chi ti ha pagato e chi no, e prepari i solleciti per chi è in ritardo.",
    passi: [
      "Vedi l'elenco delle fatture e se sono pagate o no.",
      "Segna come 'pagata' quando ricevi i soldi.",
      "Per chi è in ritardo, prepara un sollecito già scritto.",
    ],
  },

  firma: {
    titolo: "Firma digitale",
    aCosaServe:
      "Serve a firmare i documenti (es. fatture) in modo ufficiale, come una firma a mano ma valida via computer.",
    passi: [
      "Carica il documento da firmare.",
      "Scegli il tipo di firma e il dispositivo (file con password, oppure smart card/CNS).",
      "Firma: ottieni il documento firmato da scaricare.",
      "Puoi anche verificare una firma già fatta da altri.",
    ],
    faq: [
      {
        domanda: "Mi serve qualcosa di particolare?",
        risposta:
          "Sì: un certificato di firma (un file con password) oppure una smart card/CNS con il suo lettore collegato al computer.",
      },
    ],
  },

  contabilita: {
    titolo: "Contabilità",
    aCosaServe:
      "Mette insieme i costi del lavoro e le scritture del mese, così vedi quanto ti costa davvero il personale.",
    passi: [
      "Guarda il 'Costo azienda stimato' del mese.",
      "Controlla le voci: stipendi, contributi, TFR, ritenute.",
      "Usa i 'Controlli audit' per vedere se i conti tornano.",
    ],
  },

  lipe: {
    titolo: "LIPE / IVA periodica",
    aCosaServe:
      "Prepara il riepilogo dell'IVA del trimestre (quanta ne hai incassata e quanta ne devi versare).",
    passi: [
      "Scegli anno e trimestre.",
      "Il programma calcola l'IVA dovuta dalle fatture e dalle spese.",
      "Controlla il totale e prepara l'invio.",
    ],
  },

  "iva-annuale": {
    titolo: "Dichiarazione IVA annuale",
    aCosaServe:
      "È il riepilogo dell'IVA di tutto l'anno, da presentare una volta all'anno.",
    passi: [
      "Scegli l'anno.",
      "Controlla i totali calcolati dalle fatture e dalle spese.",
      "Verifica e prepara la dichiarazione.",
    ],
  },

  bilancio: {
    titolo: "Bilancio d'esercizio",
    aCosaServe:
      "È la fotografia annuale dei conti dell'azienda: cosa possiede, cosa deve e se ha guadagnato o perso.",
    passi: [
      "Scegli l'anno.",
      "Usa 'Precompila' per pescare ricavi e costi da fatture, spese e cedolini.",
      "Controlla che il bilancio 'quadri' (i totali devono tornare).",
      "Quando è pronto, passalo da Bozza ad Approvato.",
    ],
    faq: [
      {
        domanda: "Posso già depositarlo?",
        risposta:
          "Il deposito ufficiale (formato XBRL) non è ancora generato. Per ora prepari e approvi il bilancio internamente.",
      },
    ],
  },

  documenti: {
    titolo: "Archivio documenti",
    aCosaServe:
      "È il cassetto dove trovi tutti i file creati: buste paga, F24, CU, fatture, flussi XML e ricevute.",
    passi: [
      "Cerca il documento che ti serve nell'elenco.",
      "Guarda lo stato (generato, da firmare, in bozza...).",
      "Apri o scarica il file.",
    ],
  },

  f24: {
    titolo: "Modello F24",
    aCosaServe:
      "È il modulo con cui si pagano tasse e contributi allo Stato. Qui il programma lo prepara dai cedolini.",
    passi: [
      "Scegli il mese: vengono presi i dati dalle buste paga (ritenute, addizionali, INPS).",
      "Controlla i codici tributo e gli importi.",
      "Genera il PDF e/o il file da inviare.",
      "Segna lo stato fino al pagamento avvenuto.",
    ],
    faq: [
      {
        domanda: "Gli importi sono giusti?",
        risposta:
          "Sono calcolati dai cedolini del mese. Controlla sempre prima di pagare e, in caso di dubbio, chiedi al Consulente del Lavoro.",
      },
    ],
  },

  "fine-anno": {
    titolo: "Fine anno (conguagli, TFR, INAIL)",
    aCosaServe:
      "Le tre operazioni di chiusura dell'anno: il conguaglio delle tasse dei dipendenti, la rivalutazione del TFR e il premio INAIL.",
    passi: [
      "Conguaglio: scegli il dipendente e premi 'Calcola'. Quando archivi la busta di dicembre, il conguaglio ci finisce dentro da solo.",
      "TFR: inserisci la variazione ISTAT dell'anno (la trovi sul sito ISTAT o te la dà il consulente) e premi 'Calcola'.",
      "INAIL: copia PAT e tasso dalla lettera 'basi di calcolo' che l'INAIL manda all'azienda, poi premi 'Calcola'.",
    ],
    faq: [
      {
        domanda: "L'INAIL si paga da qui?",
        risposta:
          "No: qui vedi gli importi. Il pagamento va fatto con F24 e l'invio della dichiarazione sui servizi online INAIL.",
      },
    ],
  },

  cu: {
    titolo: "Certificazione Unica (CU / CUD)",
    aCosaServe:
      "È il documento annuale che certifica quanto ha guadagnato e quanto ha pagato di tasse ogni dipendente.",
    passi: [
      "Scegli l'anno d'imposta.",
      "Il programma prepara una CU per ogni dipendente.",
      "Controlla i totali e genera i PDF da consegnare.",
    ],
  },

  mod770: {
    titolo: "Modello 770",
    aCosaServe:
      "È la dichiarazione annuale con cui l'azienda comunica allo Stato le ritenute fatte ai dipendenti.",
    passi: [
      "Scegli l'anno.",
      "Controlla i dati riepilogati dalle buste paga.",
      "Verifica e prepara il modello.",
    ],
  },

  "redditi-pf": {
    titolo: "Redditi PF / 730",
    aCosaServe:
      "Sono le dichiarazioni dei redditi delle persone fisiche (la 'dichiarazione dei redditi').",
    passi: [
      "Scegli la persona e l'anno.",
      "Inserisci o controlla i redditi e le spese da scaricare.",
      "Verifica il risultato (a credito o a debito).",
    ],
  },

  irap: {
    titolo: "IRAP",
    aCosaServe:
      "È una tassa regionale sulle attività produttive. Qui si prepara il relativo calcolo.",
    passi: [
      "Scegli l'anno.",
      "Controlla la base di calcolo e l'aliquota della regione.",
      "Verifica l'importo dovuto.",
    ],
  },

  "collega-sdi": {
    titolo: "Collega Fatturazione elettronica",
    aCosaServe:
      "Serve a collegare il programma a un servizio che invia davvero le fatture allo Stato (SdI).",
    passi: [
      "Scegli il provider/servizio che usi.",
      "Inserisci i dati di accesso (account e chiave/ApiKey).",
      "Salva e fai una prova di invio.",
    ],
    faq: [
      {
        domanda: "Perché serve?",
        risposta:
          "Senza un collegamento, il programma prepara la fattura ma non può spedirla allo Stato. Questo collega il 'modem'.",
      },
    ],
  },

  impostazioni: {
    titolo: "Impostazioni",
    aCosaServe:
      "Qui trovi cinque aree ordinate: Documenti, Interfaccia, Aurora, Sicurezza e Gestione studio. Puoi scegliere uno dei 15 stili di busta, lo stile del software (Moderno oppure Classico, come un gestionale desktop), il tema (chiaro, scuro o automatico) con sei colori d'accento, regolare la leggibilità, proteggere l'account e raggiungere le configurazioni consentite dal tuo ruolo. Le buste già chiuse non cambiano.",
    passi: [
      "Scegli una categoria dal menu laterale; sul telefono scorri le categorie in orizzontale.",
      "In Interfaccia scegli prima lo stile: Moderno (vetro e rilievi) o Classico (grigio, bordi netti, niente animazioni). Poi tocca una tessera del tema o un colore: l'aspetto cambia subito, su tutte le pagine, e ti segue sugli altri dispositivi. Dal computer puoi anche passare da giorno a notte con il sole/luna nella barra in alto.",
      "Le preferenze si sincronizzano con il tuo account quando il server è disponibile; offline restano salvate sul dispositivo.",
      "Per i dati condivisi, apri Gestione studio e poi la pagina dedicata.",
    ],
  },

  utenti: {
    titolo: "Utenti e permessi",
    aCosaServe:
      "Decidi chi può entrare nel programma e cosa può fare (chi comanda, chi opera, chi solo guarda).",
    passi: [
      "Premi 'Invita utente' e inserisci la sua email.",
      "Scegli il ruolo: Admin (tutto), Operatore (lavora), Lettura (solo guarda).",
      "L'utente riceve l'invito e può entrare.",
    ],
  },
};
