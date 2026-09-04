# Piano completo — Impostazioni e personalizzazione

Richiesta del fondatore (2 settembre 2026): una pagina Impostazioni «come nei telefoni»,
dove si personalizza tutto il personalizzabile — font compresi. La base esiste già
(Studio → Impostazioni, rifatta il 1/9 con preferenze vere sul dispositivo): questo piano
la porta a sistema completo, per fasi, ognuna consegnabile da sola.

## Principi non negoziabili

1. **Le preferenze non toccano mai i numeri.** Stili, font e colori cambiano solo
   l'impaginazione: lordo, contributi e netto restano identici in ogni combinazione
   (è verificato sui quindici stili busta, e resta il vincolo di ogni voce futura).
2. **Font solo con licenza incorporabile.** Nei PDF il font viaggia dentro il file
   (obbligo per documenti consegnati): si ammettono solo famiglie open (SIL OFL)
   incorporate come risorsa — oggi Lato, Carlito e IBM Plex Sans; ogni aggiunta porta la sua
   licenza nel repo.
3. **Tre livelli di preferenza, con precedenza chiara**:
   dispositivo (localStorage) → utente/studio (server, fatto) → azienda
   (già su AziendaEntity: logo, canale notifiche, CCNL default). La più specifica vince.
4. **Un documento chiuso non cambia mai**: qualunque preferenza vale dalla prossima
   stampa; i PDF congelati alla chiusura restano quelli.
5. **Niente flessibilità speculativa**: ogni voce entra quando c'è un uso reale
   (regola Karpathy del progetto). Questo piano elenca il possibile; si implementa
   ciò che serve, nell'ordine delle fasi.

## Stato attuale (verificato sul codice, 2/9/2026)

- Pagina Impostazioni reale con: stile busta predefinito sincronizzato sul server, risposte brevi
  Aurora, stato AI live e collegamenti verso le pagine di configurazione. L'interfaccia è ora
  organizzata in cinque categorie (Documenti, Interfaccia, Aurora, Sicurezza, Studio), con menu
  laterale su desktop e scorrimento orizzontale su mobile, un solo pannello alla volta e anteprima
  grafica A4 priva di dati reali. I collegamenti rispettano la visibilità per ruolo.
- Il salvataggio distingue risposta server e sola copia locale; le scritture rapide della stessa
  preferenza sono serializzate, così l'ultima scelta non può essere superata da una risposta lenta.
- PDF: **Lato, Carlito e IBM Plex Sans** incorporati e selezionabili via `DocumentiConfig`.
  Il catalogo offre **15 stili**: Classico, Modulare ed Editoriale in cinque temi ciascuno;
  **Modulare Blu è il predefinito** per gli account senza una scelta salvata. Le preferenze
  esplicite restano valide; la busta agricola mantiene uno stile dedicato e separato.
- Interfaccia: token CSS unici in `:root` (accento, neutri, vetri, campi, stati), tema chiaro /
  scuro / automatico e sei accenti scelti in Impostazioni → Interfaccia (dal 4/9/2026).
- Server: tabella e API preferenze utente/studio con whitelist e isolamento tenant.

## Fase 1 — Preferenze sul server (fondazione)

**Stato: completata il 2 settembre 2026.**

Le preferenze del dispositivo non seguono l'utente tra computer e telefono.

- Tabella `PreferenzeUtente` (TenantId, UtenteId, Chiave, Valore; unique su terna) +
  migrazioni per entrambi i provider; servizio tenant-isolato con whitelist delle chiavi
  (mai un sacco libero di dati); `GET/PUT /api/preferenze`.
- La pagina Impostazioni legge dal server con fallback al dispositivo (offline-friendly:
  il pacchetto locale funziona anche senza rete).
- Migrano subito: stile busta, risposte brevi Aurora. Test: isolamento tenant, whitelist,
  precedenza dispositivo/server.
- Stima: mezza giornata. Prerequisito per tutto il resto.

## Fase 2 — Personalizzazione documenti (il pezzo che si vede coi clienti)

**Stato: font, dicitura e catalogo palette completati; default per azienda ancora aperto.**

- **Font dei PDF**: 2-3 famiglie alternative a Lato, tutte OFL e incorporate (candidate:
  Inter, Source Sans 3, IBM Plex Sans; per lo stile Classico eventuale variante serif
  tipo Source Serif). `DocumentiConfig` registra le famiglie; i renderer leggono la
  famiglia dalla preferenza (default Lato). Tendina «Font dei documenti» in Impostazioni
  con anteprima. Vincolo: verifica visiva su busta densa (40 voci) per ogni font — le
  metriche cambiano gli a-capo, mai i numeri.
- **Dicitura dello studio a piè di pagina** (es. «Elaborato da Studio X») su buste e
  prospetti: testo libero corto, sanificato, mai sopra le diciture obbligatorie
  (BOZZA/non-LUL restano intoccabili).
- **Palette documenti**: cinque temi predefiniti per ciascuna delle tre impaginazioni, con
  contrasto verificato e nessun color-picker libero. L'eventuale colore separato della busta
  agricola resta fuori da questo catalogo.
- **Default per azienda**: stile e font per singolo cliente (colonna su AziendaEntity),
  che vince sulla preferenza di studio.
- Stima: 1 giornata (font) + mezza (dicitura + accento + default azienda). Test golden
  PDF per ogni famiglia (una pagina, font incorporati, testo estraibile).

## Fase 3 — Aspetto dell'interfaccia

**Stato: completata, tema scuro e accenti compresi (4 settembre 2026).**

- **Dimensione del testo**: tre taglie (Normale / Grande / Molto grande) via
  `--scala-testo` sulle variabili esistenti — accessibilità prima di tutto.
- **Font dell'interfaccia**: variabile `--font-ui` (Sistema / Lato / le famiglie della
  Fase 2 servite come webfont locali, niente CDN).
- **Densità** (Comoda / Compatta): spaziature via variabile.
- **Tema e accento (fatto il 4/9/2026)**: `index.css` è stato tokenizzato (un solo `:root`
  in testa al file, sezione «TEMI» in fondo che ridefinisce solo token). Preferenze server
  `tema_interfaccia` (chiaro / scuro / automatico) e `accento_interfaccia` (Salvia, Oceano,
  Indaco, Bordeaux, Rame, Grafite), catalogo chiuso identico tra `PreferenzeUtenteService` e
  `preferenze.ts`. Applicazione prima del primo disegno, `theme-color` PWA aggiornato,
  «automatico» segue il dispositivo dal vivo, cambio con dissolvenza (View Transitions, ripiego
  CSS, «riduci movimento» rispettato). Verifica visiva su 16 pagine in chiaro e scuro, desktop e
  telefono. I documenti PDF non cambiano: il tema riguarda solo l'interfaccia.
- Stima (senza tema scuro): mezza giornata.

## Fase 4 — Notifiche e promemoria

- Anticipi configurabili per studio (oggi fissi: DURC 30 gg, F24 7 gg).
- Credenziali dei canali (SMTP / Twilio / WhatsApp Business) inserite dallo studio in
  Impostazioni, cifrate col protettore esistente (stesso schema della ApiKey SdI);
  pulsante «Invia messaggio di prova».
- Stima: 1 giornata (la cifratura c'è già; serve UI + test).

## Fase 5 — Account e sicurezza nella pagina

**Stato: cambio password e gestione 2FA completati; elenco sessioni e revoca restano aperti.**

- Cambio password e attivazione/disattivazione 2FA usano gli endpoint `/api/auth/2fa/*`, con
  stato iniziale letto dal server, etichette accessibili, codice a sei cifre, blocco dei doppi
  invii ed esiti distinti. Restano da aggiungere QR ed elenco sessioni attive con revoca.
- Stima: 1 giornata.

## Fuori perimetro dichiarato

- Lingue diverse dall'italiano (progetto a sé, tutta l'app è in italiano).
- Font caricati dall'utente (licenze non verificabili: no).
- Personalizzazioni che alterano contenuti obbligatori dei documenti (diciture di legge,
  progressivi, dati previdenziali): mai.

## Ordine consigliato e criterio di uscita

1 → 2 → 3 (tema scuro compreso) → 4 → 5.
Ogni fase esce solo con: suite backend e frontend verdi, lint pulito, guida in-app
aggiornata, voce in CLAUDE.md. Le preferenze nuove compaiono SOLO in Impostazioni:
la pagina resta l'unico posto da imparare.
