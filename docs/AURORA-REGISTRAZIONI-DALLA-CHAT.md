# Aurora registra dalla chat — mappa completa dei tre flussi

Aggiornato al 29 agosto 2026. Richiesta del fondatore: «non voglio la guida, Aurora deve
agire» — se l'utente chiede di registrare un'azienda o un dipendente dando i dati in chat,
Aurora prepara la registrazione, l'utente conferma con un tocco, la scheda si apre da sola
e i dati mancanti sono evidenziati in rosso.

## Consenso pieno (29 agosto 2026, richiesta esplicita del fondatore)

Alla prima apertura la shell propone **«Dai pieno accesso ad Aurora?»**. Se l'utente
acconsente (`POST /api/ai/consenso`, tabella `ConsensiAurora`, coppia esatta
tenant+utente, sempre revocabile dal pannello o con `DELETE /api/ai/consenso`):

- a ogni turno di chat **senza lease** con un cliente selezionato, il **server stesso** si
  procura il mandato di lettura (`AiChatEndpoints.MandatoAutomaticoAsync`): Guidata/S2,
  finalità `PREPARAZIONE_MESE_PAGHE`, sei risorse in **sola lettura** (Aziende,
  Dipendenti, Rapporti, Cartellini, Cedolini, PraticheF24), mese corrente, 60 minuti.
  L'emissione passa dallo **stesso Governor del pannello**: policy, quote, audit,
  trasparenza e revoca identici. La lease resta sul server (cache di processo, riusata tra
  i messaggi); il browser non la vede mai.
- il turno diventa quindi **governato** (runtime A2 con gli strumenti di lettura), e le
  card di registrazione continuano a funzionare: il riconoscimento deterministico gira in
  ogni turno.
- «Non ora» viene ricordato sul dispositivo; la concessione/revoca vive sul server. La
  revoca del consenso revoca subito anche i mandati automatici ancora attivi.
- le **scritture non cambiano**: restano il protocollo A3 (proposta firmata + conferma +
  undo) e le card di registrazione premute dall'utente. Il consenso pieno non abilita
  alcuna scrittura nuova.
- nel pannello i mandati di sola lettura automatici non contano come «mandati residui» e
  non bloccano «Prepara il mese»: vengono revocati automaticamente all'avvio del
  controllo.

Rispetto a `docs/PIANO-AI-LIBERA-LIMITI-USO.md` questa è una **deroga decisa dal
fondatore** (29/8/2026) al principio «nessun permesso permanente»: il consenso è
permanente finché non revocato, ma ogni singolo mandato resta a scadenza, in sola lettura
e dentro le stesse policy del Governor.

### Archivio clienti nel contesto e azioni d'interfaccia (29 agosto 2026, sera)

Sempre col consenso pieno (richiesta del fondatore: «Aurora deve vedere le aziende e
portarmi sulle pagine che chiedo»):

- a ogni turno il modello riceve **l'archivio clienti dello studio** (ragione sociale,
  comune e id; massimo 100 righe, il taglio è dichiarato; niente P.IVA né importi), letto
  dal server in quell'istante e marcato come elenco esaustivo. Così «quali aziende ho?»
  riceve una risposta vera anche **senza cliente selezionato**, dove prima Aurora era
  cieca.
- il modello può proporre **azioni d'interfaccia** con marcatori nel testo:
  `[[APRI:chiave]]` apre una schermata (whitelist di sole pagine operative, uguale tra
  server `SezioniApribili` e SPA `SEZIONI_APRIBILI_DA_AURORA`) e `[[CLIENTE:id]]`
  seleziona un cliente. Il **server valida ed esegue**: sezione fuori whitelist o id non
  del tenant = nessuna azione; i marcatori vengono comunque sempre tolti dal testo (anche
  senza consenso, così non compaiono mai all'utente). La SPA riceve `apriSezione` /
  `selezionaCliente` già validati e naviga o seleziona; la conversazione **migra** al
  nuovo cliente senza azzerarsi.
- senza consenso pieno: nessun archivio nel prompt, nessuna azione eseguita.

## Regola architetturale comune (conforme a `docs/PIANO-AI-LIBERA-LIMITI-USO.md`)

- Il **riconoscimento** dei dati nei messaggi è di **parser deterministici** lato server
  (`VisuraCameraleParser`, `ImportazioneUnilavPdf`), non del modello: nel turno libero il
  modello continua a non ricevere strumenti né archivio. Il modello viene solo avvisato che
  la card esiste, così invita a usarla invece di dettare passaggi manuali.
- Il riconoscimento gira **messaggio per messaggio** (dal più recente), mai sulla
  conversazione concatenata: campi di turni diversi non si mescolano nella stessa card
  (per esempio il CF del dipendente di un turno sull'azienda del turno dopo).
- In chat vale il **checksum**: una P.IVA o un CF formalmente errati (un protocollo, un
  numero citato per caso) non producono card. Il CF a 16 caratteri in una visura di ditta
  individuale è il titolare, non una persona da assumere.
- Le due letture di sola esistenza (cliente/lavoratore già in archivio) decidono **cosa
  riceve la SPA** (card sì/no) e — dal 27 agosto 2026, su richiesta esplicita del
  fondatore — anche **un singolo bit per entità nelle istruzioni del modello**: «già
  registrata sì/no», «già assunto sì/no». Prima il modello era cieco sull'esito e doveva
  rispondere per ipotesi («se la card non compare, significa che…»); ora risponde con
  certezza e guida il passo giusto. La P.IVA/il CF su cui il bit è calcolato stanno già
  nel messaggio dell'utente inviato al provider: l'unica informazione d'archivio in più
  che il provider riceve è quel bit di esistenza. Nessun altro dato d'archivio (importi,
  anagrafiche, elenchi) raggiunge il provider senza mandato.
- La SPA **congela il testo d'origine dentro la proposta**: la registrazione funziona anche
  se gli allegati sono stati consumati o la conversazione è cambiata nel frattempo.
- La **scrittura** è sempre un click dell'utente su una **API ordinaria** già esistente
  (`POST /api/aziende`, `POST /api/unilav/testo`): per il piano AI è un «modulo di
  prodotto» (§10.1), non un'azione autonoma dell'agente — nessun gate GX richiesto.
  Valgono tutte le validazioni di dominio (checksum, duplicati per tenant, modalità prova).
- Un cliente/lavoratore **già in archivio non viene riproposto** (controllo tenant-isolato
  lato server), e una card scartata con la X non ricompare nel pannello.
- Filtro segreti, limiti messaggio e contatore `ConsumiAi` restano invariati.

## Flusso 1 — Registra AZIENDA dalla chat ✅ costruito

1. L'utente incolla i dati (anche con grassetti `**…**` ed elenchi) o trascina un
   documento; chiede di registrare.
2. Il server allega alla risposta della chat la proposta (`azienda` in
   `RispostaChatEndpoint`); la SPA mostra la card **«Registra questo cliente»** con tutti i
   campi che verranno salvati (il parser locale aggiunge PEC/email/telefono/CF
   dell'amministratore dallo stesso testo — `aziendaDaProposta` in `aziendaForm.ts`).
3. Un tocco su «Registra questo cliente» → `POST /api/aziende` → la shell apre la **scheda
   del cliente** (pagina Aziende, prop `apriAziendaId`), con banner rosso e i campi vuoti
   segnati **«Da completare.»** in rosso (`campiDaCompletareAzienda`): codice catastale,
   PEC, settore, ATECO, tipo contribuente e — se ha dipendenti — matricola INPS, sede INPS,
   CSC, P.A.T. INAIL, CCNL e livello. Il rosso si spegne campo per campo mentre si scrive.
4. Nessun dato viene inventato: ciò che il testo non contiene resta vuoto. REA, forma
   giuridica e data di costituzione **non esistono** nell'anagrafica azienda: se serviranno,
   sono una modifica di schema separata (entity + DTO + migrazioni due provider).

## Flusso 2 — Registra DIPENDENTE dalla chat ✅ costruito

1. L'utente scrive i dati del lavoratore (nome, cognome, CF a 16 caratteri, data
   assunzione, livello…). Il parser UNILAV li riconosce anche da testo libero/markdown.
2. La chat allega `lavoratore` alla risposta; card **«Registra questo dipendente»** con il
   riepilogo. Serve il **cliente selezionato**: senza, il pulsante è spento e la card lo dice.
3. Un tocco → `POST /api/unilav/testo` con `aziendaId` del cliente corrente (e CCNL/livello
   di default dell'azienda): crea dipendente + rapporto con le regole anti-doppione già
   esistenti (stesso CF stessa azienda = errore; altra azienda = riusa l'anagrafica).
4. La shell apre la **scheda del dipendente** (`apriDipendente(rapportoId)`), e nel
   pannello Aurora resta la card rossa **«Dati del dipendente ancora da inserire»** con
   l'elenco `daCompletare` del server (campo, perché serve, dove trovarlo).
5. Prossimo passo possibile (non ancora fatto): evidenziare in rosso i singoli campi anche
   dentro la scheda dipendente, come già avviene nella scheda azienda.

## Flusso 3 — Presenze/assenze del cedolino dalla chat 🔒 esiste, dietro il gate A3

Qui la strada giusta **non è** un parser di regex: i giorni di presenza/assenza finiscono
nella busta paga, e un testo libero interpretato male («assente il 12 e 13, il resto
presente») produrrebbe stipendi sbagliati in silenzio. Per questo esiste già il protocollo
A3, costruito e testato:

- **Strumento `compila_presenze`** (più `elabora_cedolino`): nel turno di chat **con
  mandato**, il modello lo chiama, il server prepara una **proposta firmata** con il diff
  giorno per giorno («12/08: 8 ore lavorate → 8 ore di assenza non retribuita»), e la card
  di conferma/annulla/undo è **già renderizzata** nel pannello Aurora
  (`PropostaAzioneAurora`). Niente si applica senza il tocco dell'utente; l'undo resta
  disponibile 24 ore.
- **Per accenderlo in locale** servono (oltre al provider chat attivo):
  `Ai:CoppieShadowA3` con le coppie esatte `tenantId:utenteId` (GUID dal DB) e la modalità
  (`DryRun` per vedere le proposte senza token, `Confirmed` per poterle applicare). Default
  ovunque: spento (fail-closed). La quota effetti è 1 per mandato (scelta pilota).
- **Limiti attuali dichiarati** dello strumento: converte solo giornate «Lavorate» complete
  in «Assenza non retribuita», rifiuta mesi con malattia/infortunio (per non rivelare dati
  sanitari) e mesi con cedolino o F24 finale. Estenderlo ad altre causali (ferie, permessi,
  presenze) è il passo successivo naturale: per il piano AI è un ampliamento di uno
  strumento A3 esistente e va chiesto/deciso esplicitamente (gate G5 ancora aperto:
  mancano card batch, riconciliazione completa e cicli shadow).
- Nota tecnica: la pagina Presenze salva oggi con l'archiviazione del cedolino; il
  cartellino-bozza versionato (`PUT /api/rapporti/{id}/cartellino-bozza`, 409 su versione
  vecchia) è scritto solo da Aurora A3.

## Dove sta il codice (per chi riprende il lavoro)

| Pezzo | File |
| --- | --- |
| Riconoscimento azienda/lavoratore nel turno libero | `backend/src/Paghe.Api/AiChatEndpoints.cs` (+ `AziendaRiconosciutaAi.cs`, `LavoratoreRiconosciutoAi.cs`) |
| Card e registrazioni nella SPA | `frontend/src/Aurora.tsx` |
| Fusione proposta+testo e campi da completare | `frontend/src/aziendaForm.ts` (`aziendaDaProposta`, `campiDaCompletareAzienda`) |
| Apertura scheda azienda con rosso | `frontend/src/pages/Aziende.tsx` (prop `apriAziendaId`) |
| Navigazione dalla shell | `frontend/src/pages/Dashboard.tsx` (`onAziendaCreata`, `onApriDipendente`) |
| Test | `backend/tests/Paghe.Tests/AiChatApiTests.cs`, `frontend/tests/aziendaForm.test.ts` |
