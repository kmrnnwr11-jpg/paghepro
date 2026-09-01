# Piano completo — Impostazioni e personalizzazione

Richiesta del fondatore (2 settembre 2026): una pagina Impostazioni «come nei telefoni»,
dove si personalizza tutto il personalizzabile — font compresi. La base esiste già
(Studio → Impostazioni, rifatta il 1/9 con preferenze vere sul dispositivo): questo piano
la porta a sistema completo, per fasi, ognuna consegnabile da sola.

## Principi non negoziabili

1. **Le preferenze non toccano mai i numeri.** Stili, font e colori cambiano solo
   l'impaginazione: lordo, contributi e netto restano identici in ogni combinazione
   (è già così per i tre stili busta, e resta il vincolo di ogni voce futura).
2. **Font solo con licenza incorporabile.** Nei PDF il font viaggia dentro il file
   (obbligo per documenti consegnati): si ammettono solo famiglie open (SIL OFL)
   incorporate come risorsa — oggi Lato; ogni aggiunta porta la sua licenza nel repo.
3. **Tre livelli di preferenza, con precedenza chiara**:
   dispositivo (localStorage, già fatto) → utente/studio (server, da fare) → azienda
   (già su AziendaEntity: logo, canale notifiche, CCNL default). La più specifica vince.
4. **Un documento chiuso non cambia mai**: qualunque preferenza vale dalla prossima
   stampa; i PDF congelati alla chiusura restano quelli.
5. **Niente flessibilità speculativa**: ogni voce entra quando c'è un uso reale
   (regola Karpathy del progetto). Questo piano elenca il possibile; si implementa
   ciò che serve, nell'ordine delle fasi.

## Stato di partenza (verificato sul codice, 1-2/9/2026)

- Pagina Impostazioni reale con: stile busta predefinito (dispositivo), risposte brevi
  Aurora, stato AI live, tessere verso le pagine di configurazione.
- PDF: famiglia **Lato completa incorporata** (9 pesi + corsivi) via `DocumentiConfig`;
  renderer con font cablato. Stili busta: Classico / Colori / Editoriale già scelti da
  tendina; busta agricola con stile dedicato.
- Interfaccia: 41 variabili CSS già in uso, ma `font-family` del corpo **cablato**
  (`system-ui`) e tema scuro assente (design glass non tokenizzato per il dark).
- Server: nessuna tabella preferenze utente/studio.

## Fase 1 — Preferenze sul server (fondazione)

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

- **Font dei PDF**: 2-3 famiglie alternative a Lato, tutte OFL e incorporate (candidate:
  Inter, Source Sans 3, IBM Plex Sans; per lo stile Classico eventuale variante serif
  tipo Source Serif). `DocumentiConfig` registra le famiglie; i renderer leggono la
  famiglia dalla preferenza (default Lato). Tendina «Font dei documenti» in Impostazioni
  con anteprima. Vincolo: verifica visiva su busta densa (40 voci) per ogni font — le
  metriche cambiano gli a-capo, mai i numeri.
- **Dicitura dello studio a piè di pagina** (es. «Elaborato da Studio X») su buste e
  prospetti: testo libero corto, sanificato, mai sopra le diciture obbligatorie
  (BOZZA/non-LUL restano intoccabili).
- **Colore d'accento dello stile Colori** (e della busta agricola): palette limitata
  proposta (6-8 tinte verificate per contrasto/stampa B/N), non color-picker libero.
- **Default per azienda**: stile e font per singolo cliente (colonna su AziendaEntity),
  che vince sulla preferenza di studio.
- Stima: 1 giornata (font) + mezza (dicitura + accento + default azienda). Test golden
  PDF per ogni famiglia (una pagina, font incorporati, testo estraibile).

## Fase 3 — Aspetto dell'interfaccia

- **Dimensione del testo**: tre taglie (Normale / Grande / Molto grande) via
  `--scala-testo` sulle variabili esistenti — accessibilità prima di tutto.
- **Font dell'interfaccia**: variabile `--font-ui` (Sistema / Lato / le famiglie della
  Fase 2 servite come webfont locali, niente CDN).
- **Densità** (Comoda / Compatta): spaziature via variabile.
- **Tema scuro**: progetto a sé — il glass design va tokenizzato per il dark (contrasti,
  vetri, ombre su ~6.000 righe di CSS). Non si promette «gratis»: entra come fase
  dedicata solo se il fondatore la vuole davvero. Stima onesta: 2-3 giornate + revisione
  visiva completa.
- Stima (senza tema scuro): mezza giornata.

## Fase 4 — Notifiche e promemoria

- Anticipi configurabili per studio (oggi fissi: DURC 30 gg, F24 7 gg).
- Credenziali dei canali (SMTP / Twilio / WhatsApp Business) inserite dallo studio in
  Impostazioni, cifrate col protettore esistente (stesso schema della ApiKey SdI);
  pulsante «Invia messaggio di prova».
- Stima: 1 giornata (la cifratura c'è già; serve UI + test).

## Fase 5 — Account e sicurezza nella pagina

- Cambio password, attivazione 2FA con QR (endpoint `/api/auth/2fa/*` già esistenti,
  manca la UI), elenco sessioni attive con revoca.
- Stima: 1 giornata.

## Fuori perimetro dichiarato

- Lingue diverse dall'italiano (progetto a sé, tutta l'app è in italiano).
- Font caricati dall'utente (licenze non verificabili: no).
- Personalizzazioni che alterano contenuti obbligatori dei documenti (diciture di legge,
  progressivi, dati previdenziali): mai.

## Ordine consigliato e criterio di uscita

1 → 2 → 3 (senza tema scuro) → 4 → 5; il tema scuro si decide dopo la Fase 3.
Ogni fase esce solo con: suite backend e frontend verdi, lint pulito, guida in-app
aggiornata, voce in CLAUDE.md. Le preferenze nuove compaiono SOLO in Impostazioni:
la pagina resta l'unico posto da imparare.
