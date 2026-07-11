# PIANO — PaghePro AI-first per contabilità, paghe e risparmio fiscale lecito

> Obiettivo: trasformare PaghePro da gestionale tradizionale a **Business Operating Console
> AI-first** per commercialisti e piccole aziende. Il prodotto deve aiutare l'utente a lavorare
> meglio, ridurre errori, non perdere detrazioni/deduzioni/IVA detraibile dove consentito e
> pagare il meno possibile **legalmente**, sempre con conferma umana e professionale.
>
> Stato documento: piano prima dell'implementazione. Non sostituisce consulenza fiscale,
> tributaria, del lavoro o legale.

---

## 0. Regola madre

PaghePro non deve promettere "meno tasse" in modo generico. Deve promettere:

- meno errori;
- meno documenti dimenticati;
- meno scadenze perse;
- più IVA detraibile recuperata quando la legge lo consente;
- più costi correttamente classificati;
- più opportunità fiscali mostrate in modo spiegabile;
- sempre conferma dell'utente o del professionista prima di salvare/inviare.

Ogni consiglio AI deve avere:

- **fonte normativa o regola interna versionata**;
- **motivazione semplice**;
- **documenti necessari**;
- **rischio**: basso / medio / alto;
- **azione proposta**;
- **stato**: suggerito / da verificare / confermato / scartato;
- **audit log**: chi ha confermato e quando.

---

## 1. Ricerca: cosa farebbe una top company

Fonti e ispirazioni:

- Google Material 3: componenti coerenti, ricerca globale, pattern responsive, AI assistente.
- Apple Human Interface Guidelines: chiarezza, toolbar pulite, pochi elementi rumorosi.
- Microsoft Fluent e Inclusive Design: accessibilità, command bar, ruoli, permessi, team.
- IBM Carbon: data grid robuste, audit, prodotti enterprise, tabelle complesse.
- Salesforce Lightning: scheda cliente 360, timeline, relazioni e workflow commerciale.
- SAP Fiori: shell enterprise sempre visibile, ruoli, processi guidati, scadenze.
- Shopify Polaris: flussi semplici per utenti non tecnici, azioni chiare.
- Samsung One UI: mobile comodo, pannelli raggiungibili, bottom bar, grandi superfici.
- Stripe Dashboard: precisione finanziaria, riconciliazione, importi leggibili, errori chiari.
- GOV.UK Design System: form accessibili, errori spiegati in parole semplici.
- OpenAI API: visione per immagini/documenti, function calling/tool calling e structured outputs
  per trasformare documenti non strutturati in dati validabili.

Sintesi per PaghePro:

- Google farebbe ricerca globale + AI che capisce documenti e comandi.
- Apple toglierebbe schermate inutili e renderebbe ogni flusso elegante.
- Microsoft farebbe prodotto enterprise con permessi, team e command bar.
- IBM renderebbe i dati verificabili, auditabili e tabellari.
- Salesforce metterebbe al centro l'azienda cliente 360.
- SAP organizzerebbe adempimenti e scadenze per ruolo e processo.
- Shopify semplificherebbe: "cosa devo fare oggi?".
- Stripe curerebbe soldi, IVA, incassi, riconciliazioni e stati.
- Samsung renderebbe mobile e tablet davvero usabili.
- GOV.UK renderebbe impossibile sbagliare i form.

---

## 2. Visione prodotto

PaghePro diventa:

**AI Tax & Payroll Operating Console per micro-imprese, PMI e studi professionali.**

La home non è una dashboard passiva. È una centrale operativa:

1. carico documento;
2. AI riconosce tipo documento;
3. AI estrae dati;
4. AI propone azienda/lavoratore/fattura/scadenza/registrazione;
5. AI controlla errori e opportunità di risparmio;
6. utente conferma;
7. sistema salva;
8. audit log registra tutto.

---

## 3. Principio AI-first

L'AI non è un chatbot decorativo. È il livello principale del prodotto.

### 3.1 AI deve fare

- Riconoscere documenti da PDF, foto, XML, ricevute e scansioni.
- Estrarre campi strutturati.
- Capire se un documento appartiene a una azienda già esistente.
- Evitare duplicati usando P.IVA, codice fiscale, IBAN, numero documento.
- Suggerire classificazione contabile.
- Suggerire IVA detraibile/non detraibile da verificare.
- Segnalare documenti mancanti.
- Segnalare scadenze.
- Segnalare opportunità di risparmio fiscale lecito.
- Preparare azioni ma non inviarle senza conferma.

### 3.2 AI non deve fare

- Non deve inventare norme.
- Non deve dire "puoi scaricare tutto".
- Non deve consigliare evasione, artifici o documenti falsi.
- Non deve inviare F24, fatture, dichiarazioni o comunicazioni senza conferma.
- Non deve modificare dati fiscali critici senza audit.
- Non deve usare dati cliente per training senza consenso esplicito.

---

## 4. Obiettivo centrale: risparmio fiscale lecito per ogni azienda

Il software deve essere migliore di un semplice gestionale perché ogni azienda vede:

- cosa può recuperare;
- cosa sta perdendo;
- cosa manca per recuperarlo;
- cosa rischia;
- quale azione fare oggi.

### 4.1 Motore "Risparmio lecito"

Nuovo modulo:

`AI Tax Saving Engine`

Funzione:

- analizza fatture, spese, paghe, IVA, incassi, scadenze, settore, regime fiscale;
- genera consigli personalizzati;
- distingue "opportunità sicura" da "da validare";
- mostra importo stimato;
- collega ogni consiglio ai documenti.

Formato di ogni consiglio:

```text
Titolo: Recupera IVA su acquisti con fattura mancante
Azienda: Rossi Srl
Impatto stimato: 420,00 €
Perché: ci sono pagamenti a fornitori senza fattura registrata
Documenti richiesti: fattura elettronica / ricevuta / estratto conto
Rischio: medio
Fonte: regola IVA detrazione + documento contabile
Azione: chiedi fattura al fornitore / importa fattura passiva
Conferma richiesta: commercialista
```

### 4.2 Aree di risparmio da coprire

- IVA detraibile su acquisti registrati correttamente.
- Fatture passive mancanti.
- Spese classificabili meglio.
- Regime fiscale più adatto: forfettario / semplificato / ordinario / società.
- Costi deducibili dimenticati.
- Ammortamenti e cespiti.
- Ratei, risconti e competenze.
- Scadenze rispettate per evitare sanzioni.
- Ravvedimento operoso quando c'è ritardo.
- Welfare aziendale e costo del personale, solo se validato.
- INAIL/INPS: dati mancanti che bloccano corretto calcolo.
- F24: debiti/crediti e compensazioni da verificare.
- Liquidazione IVA: previsioni e allarmi prima della scadenza.
- Incassi insoluti: sollecito clienti e cashflow.

### 4.3 Consigli per tipo di azienda

**Forfettario / professionista**

- Ricordare che di norma non addebita IVA e non detrae IVA sugli acquisti.
- Simulare convenienza: se molti acquisti con IVA, valutare con professionista se il regime resta adatto.
- Monitorare soglie, ricavi, costi collaboratori/dipendenti e cause ostative.

**Ditta individuale ordinaria o semplificata**

- Controllo fatture passive e costi deducibili.
- Separare spese personali e aziendali.
- Controllo IVA periodica.
- Scadenze F24, INPS, INAIL.

**SRL / società**

- Dashboard CFO: utile, IVA, debiti fiscali, costo personale, margini.
- Prima nota e riconciliazione bancaria.
- Cespiti, ammortamenti, bilancio, IRES/IRAP.
- Compensi amministratore e costo del lavoro da simulare con professionista.

**Azienda con dipendenti**

- Costo del personale mensile e annuale.
- Alert su cedolini, UniEmens, F24, INAIL, TFR.
- Consigli su dati mancanti che possono generare errori.
- Simulazioni costo assunzione, apprendistato, part-time, welfare, solo con regole validate.

**E-commerce / estero**

- Alert su operazioni UE/extra UE.
- Intrastat/esterometro/reverse charge da verificare.
- Riconciliazione incassi marketplace / Stripe / banca.

---

## 5. Moduli AI fondamentali

### 5.1 AI Document Inbox

La schermata più importante.

Input:

- fatture elettroniche XML;
- fatture PDF;
- foto scontrini/ricevute;
- UNILAV;
- DURC;
- F24;
- estratti conto;
- buste paga vecchie;
- lettere INAIL;
- visure camerali;
- contratti.

Output:

- tipo documento;
- azienda collegata;
- dati estratti;
- livello confidenza;
- azioni suggerite;
- errori o campi mancanti;
- stato conferma.

Checklist:

- [ ] Upload drag-and-drop.
- [ ] Classificazione documento.
- [ ] Estrazione dati strutturata.
- [ ] Anteprima documento.
- [ ] Dati estratti modificabili.
- [ ] Conferma prima del salvataggio.
- [ ] Audit log.
- [ ] Test con PDF/foto reali.

### 5.2 AI Azienda 360

Obiettivo: ogni azienda ha una scheda completa e intelligente.

Tab:

- Panoramica;
- Dati fiscali;
- Dati contributivi;
- Dipendenti;
- Documenti;
- Scadenze;
- Consigli AI;
- Audit.

Checklist dati:

- [ ] Ragione sociale.
- [ ] P.IVA.
- [ ] Codice fiscale.
- [ ] PEC.
- [ ] Codice ATECO.
- [ ] Regime fiscale.
- [ ] Indirizzo sede.
- [ ] Matricola INPS.
- [ ] Codice sede INPS.
- [ ] PAT INAIL.
- [ ] CCNL default.
- [ ] Livello default.
- [ ] Stato dati completi / incompleti.

### 5.3 AI Dipendente 360

Obiettivo: lavoratore e rapporto sempre collegati all'azienda giusta.

Checklist:

- [ ] Crea o aggancia lavoratore da codice fiscale.
- [ ] Evita duplicati nello stesso tenant.
- [ ] Evita doppio rapporto attivo nella stessa azienda se non confermato.
- [ ] Estrae dati da UNILAV.
- [ ] Mostra dati mancanti per cedolino corretto.
- [ ] Storico cedolini.
- [ ] TFR, addizionali, residenza, IBAN, contratto.

### 5.4 AI Contabilità Assistita

Obiettivo: da documento a registrazione proposta.

Funzioni:

- riconosce costo/ricavo;
- propone conto contabile;
- separa imponibile/IVA/totale;
- segnala IVA detraibile da verificare;
- segnala documento non valido o incompleto;
- prepara prima nota.

Checklist:

- [ ] Piano conti minimo.
- [ ] Regole di classificazione.
- [ ] Apprendimento da conferme utente.
- [ ] Suggerimenti con confidenza.
- [ ] Revisione manuale obbligatoria per importi importanti.

### 5.5 AI Controllo Errori

Esempi:

- P.IVA non valida.
- Codice fiscale incoerente.
- IBAN mancante.
- Fattura duplicata.
- F24 senza matricola INPS.
- UniEmens senza dati rapporto completi.
- IVA indicata ma regime forfettario.
- Spesa pagata ma fattura mancante.
- Scadenza vicina senza documento pronto.

Checklist:

- [ ] Errori bloccanti.
- [ ] Avvisi.
- [ ] Suggerimenti.
- [ ] Spiegazione semplice.
- [ ] Link al punto da correggere.

### 5.6 AI Copilot controllato con OpenAI GPT-5.6 Sol

Scelta OpenAI:

- Provider: OpenAI API.
- Modello principale: `gpt-5.6-sol` (alias `gpt-5.6`) per ragionamento complesso,
  controlli paghe/fiscali e supporto al codice.
- API consigliata: Responses API, chiamata solo dal backend.
- Orchestrazione futura: Agents SDK solo quando servono piu agenti applicativi stabili,
  con ruoli, strumenti, approvazioni e tracing.
- Output obbligatorio: Structured Outputs con JSON Schema, non testo libero per azioni operative.
- Strumenti: function/tool calling verso funzioni interne gia autorizzate, mai accesso diretto al DB
  dal frontend e mai chiave OpenAI nel browser.

Comandi supportati:

- "Aggiungi azienda da questa visura".
- "Prepara F24 di giugno".
- "Mostrami dipendenti senza IBAN".
- "Cosa manca per UniEmens?".
- "Quali spese posso recuperare?".
- "Quanto IVA dovrò pagare questo trimestre?".
- "Fammi vedere opportunità di risparmio per Rossi Srl".

Regole:

- L'AI può leggere e proporre.
- L'AI può preparare bozze.
- L'AI non invia e non conferma da sola.
- Ogni azione eseguita passa da anteprima e conferma.
- L'AI non inventa aliquote, codici tributo, scadenze, regole INPS/INAIL/AdE o CCNL.
- Per paghe, fisco, GDPR e adempimenti, la risposta deve indicare se servono fonti ufficiali
  o validazione professionale.
- Ogni risposta operativa deve essere etichettata come bozza finche non passa i controlli
  ufficiali o la revisione del professionista.
- Tutte le letture/scritture devono restare isolate per `tenantId`.

Azioni ammesse:

- `READ_ONLY`: risponde leggendo dati gia presenti.
- `DRAFT`: prepara bozza di F24, promemoria, messaggio, checklist, cedolino o documento.
- `PROPOSE_ACTION`: propone una modifica, ma attende conferma utente.
- `BLOCKED`: si ferma quando mancano fonte ufficiale, dati obbligatori, permesso o validazione.

Schema minimo di risposta AI:

```json
{
  "tipoAzione": "READ_ONLY | DRAFT | PROPOSE_ACTION | BLOCKED",
  "titolo": "string",
  "rispostaSemplice": "string",
  "datiUsati": ["string"],
  "bozzeCreate": ["string"],
  "fontiDaVerificare": ["string"],
  "rischi": ["string"],
  "richiedeConferma": true,
  "prossimaAzioneUtente": "string"
}
```

Esempio flusso sicuro:

1. Utente chiede: "Prepara F24 di giugno".
2. Backend raccoglie cedolini, azienda, matricola INPS e pratiche F24 del tenant.
3. OpenAI `gpt-5.6-sol` riceve solo i dati necessari e restituisce JSON strutturato.
4. Il sistema mostra anteprima con importi, fonti dati e rischi.
5. Utente conferma.
6. Backend crea la bozza, marcata "da validare".
7. Invio o esportazione ufficiale resta bloccata finche non passa il controllo ufficiale.

Checklist implementazione:

- [ ] Configurazione backend `OpenAI:ApiKey` via secret, mai in `frontend`.
- [ ] Configurazione modello default `gpt-5.6-sol`.
- [ ] Endpoint backend `POST /api/ai/copilot` protetto da JWT.
- [ ] Prompt di sistema con regole PaghePro: `decimal`, `tenantId`, fonti ufficiali, bozza da validare.
- [ ] JSON Schema per lo schema minimo sopra.
- [ ] Tool interni separati per sola lettura e per creazione bozze.
- [ ] Registro audit di ogni richiesta AI: utente, tenant, azione, conferma, esito.
- [ ] Rate limit e limite costo per tenant.
- [ ] Test: nessuna azione reale senza `richiedeConferma=true` e conferma utente.

Fonti OpenAI ufficiali da usare per questa parte:

- Models: https://developers.openai.com/api/docs/models
- Responses API: https://developers.openai.com/api/reference/responses/overview
- Structured Outputs: https://developers.openai.com/api/docs/guides/structured-outputs
- Function calling: https://developers.openai.com/api/docs/guides/function-calling
- Agents SDK: https://developers.openai.com/api/docs/guides/agents

### 5.7 AI Scadenzario

Funzioni:

- genera scadenze da documenti;
- collega F24, DURC, IVA, CU, UniEmens;
- suggerisce cosa fare prima;
- prepara messaggi al cliente.

### 5.8 AI Riconciliazione bancaria

Funzioni:

- legge estratto conto;
- abbina incassi a fatture;
- abbina pagamenti a fornitori;
- individua F24 pagati;
- segnala movimenti senza documento.

### 5.9 AI Report e CFO

Report automatici:

- IVA prevista;
- imposte/scadenze prossime;
- fatture scadute;
- costo personale;
- margine stimato;
- consigli di risparmio;
- documenti mancanti.

### 5.10 AI Sicura

Checklist:

- [ ] Tenant isolato.
- [ ] Dati sensibili cifrati.
- [ ] Audit log su letture/modifiche.
- [ ] Nessun training sui dati cliente senza consenso.
- [ ] Prompt e output salvati solo se necessario.
- [ ] Mascheramento dati in log.
- [ ] Conferma umana prima di azioni fiscali.
- [ ] Ruoli: admin, operatore, sola lettura, consulente.

---

## 6. UI finale AI-first

### 6.1 Shell

- Topbar sempre visibile.
- Mini bar sinistra 56px.
- Slidebar apribile.
- Command palette con ricerca AI.
- Copilot laterale.
- Bottom bar su mobile.

### 6.2 Home

La home deve contenere:

- pulsante grande **Carica documento**;
- AI Document Inbox;
- coda "da confermare";
- alert "dati mancanti";
- consigli "risparmio possibile";
- scadenze urgenti;
- Copilot.

### 6.3 Concept da generare prima

Generare 10 immagini UI:

1. PaghePro AI Enterprise Console.
2. Apple-like Clean AI Studio.
3. Google Workspace AI Paghe.
4. Microsoft Fluent AI Studio.
5. Salesforce AI Azienda 360.
6. SAP Fiori AI Compliance.
7. Stripe AI Finance Console.
8. Samsung AI Mobile Companion.
9. IBM AI Data Control Room.
10. GOV/Legal AI Form Wizard.

Ogni concept deve mostrare:

- topbar;
- mini bar;
- slidebar o command palette;
- AI Document Inbox;
- azienda 360 o dipendenti;
- consigli fiscali AI;
- stato conferma umana.

---

## 7. Architettura AI

### 7.1 Pipeline documentale

```text
Upload documento
  -> antivirus / controlli file
  -> OCR / parsing XML / vision AI
  -> classificazione tipo documento
  -> estrazione dati strutturata
  -> validazione deterministica
  -> matching azienda/lavoratore/fattura
  -> proposta azione
  -> conferma utente
  -> salvataggio
  -> audit log
```

### 7.2 Componenti tecnici

- Vision/OCR per immagini e PDF scansionati.
- Parser XML per fatture elettroniche.
- Structured Outputs JSON schema per dati estratti.
- Function calling/tool calling per far usare al Copilot API interne.
- Rules engine fiscale versionato.
- Database norme/regole per anno.
- Audit log.
- Coda lavori per elaborazioni asincrone.
- Storage documenti con hash.
- RAG normativo solo da fonti approvate.

### 7.3 Regola fondamentale

AI propone. Il motore deterministico valida. L'utente conferma. Il sistema salva.

---

## 8. Motore normativo e fonti

Non hardcodare consigli fiscali nel prompt.

Serve un database versionato:

- regole IVA;
- regole detraibilità;
- regole deducibilità;
- regimi fiscali;
- scadenze;
- parametri lavoro;
- CCNL;
- INPS/INAIL;
- agevolazioni;
- limiti e soglie.

Ogni regola deve avere:

- anno;
- validità dal/al;
- fonte;
- ambito;
- testo semplice;
- test automatici;
- stato: bozza / validata / disattivata.

---

## 9. Piano implementazione

### Fase 0 — Bloccare la direzione

- [ ] Creare questo piano.
- [ ] Farlo leggere all'utente.
- [ ] Confermare: AI-first + risparmio fiscale lecito.
- [ ] Decidere se generare i 10 concept immagini.
- [ ] Scegliere concept vincente.

Controllo frequente:

- [ ] Nessuna implementazione grossa prima della scelta stile.
- [ ] Nessuna promessa fiscale non validata.

### Fase 1 — Design e concept

- [ ] Generare 10 concept immagini.
- [ ] Creare board confronto.
- [ ] Scegliere 1 stile base + 2 varianti.
- [ ] Definire design system.
- [ ] Definire componenti: topbar, mini bar, slidebar, command palette, inbox, cards, tabelle.

Controllo frequente:

- [ ] UI non deve sembrare gestionale vecchio.
- [ ] AI deve essere visibile nella prima schermata.
- [ ] Mobile deve avere bottom bar.

### Fase 2 — Fondamenta AI sicura

- [ ] Tabella documenti caricati.
- [ ] Storage documenti con hash.
- [ ] Stati documento: caricato, letto, da confermare, salvato, scartato.
- [ ] Audit log.
- [ ] Structured extraction schema.
- [ ] Coda elaborazioni.
- [ ] Ruoli e permessi.

Controllo frequente:

- [ ] Nessun documento salva dati definitivi senza conferma.
- [ ] Dati tenant sempre filtrati.
- [ ] Log senza dati sensibili inutili.

### Fase 3 — AI Document Inbox MVP

- [ ] Upload PDF/XML/foto.
- [ ] Riconoscimento tipo documento.
- [ ] Estrazione dati base.
- [ ] Matching azienda.
- [ ] Anteprima conferma.
- [ ] Creazione/aggiornamento azienda da documento.
- [ ] Test con visura, UNILAV, fattura, DURC.

Controllo frequente:

- [ ] Ogni campo estratto ha confidenza.
- [ ] Campo incerto evidenziato.
- [ ] Utente può correggere prima di salvare.

### Fase 4 — Azienda 360 AI

- [ ] Scheda azienda 360.
- [ ] Dati fiscali.
- [ ] Dati contributivi.
- [ ] Dati mancanti.
- [ ] Documenti collegati.
- [ ] Scadenze.
- [ ] Consigli AI.
- [ ] Audit timeline.

Controllo frequente:

- [ ] Ogni azienda deve avere stato salute: verde/giallo/rosso.
- [ ] Ogni dato mancante deve avere azione.

### Fase 5 — Dipendente 360 AI

- [ ] Crea/aggancia da CF.
- [ ] Import UNILAV.
- [ ] Scheda rapporto.
- [ ] Dati mancanti per cedolino.
- [ ] Storico cedolini.
- [ ] Alert UniEmens/F24.

Controllo frequente:

- [ ] No duplicati lavoratore.
- [ ] No rapporto senza azienda.
- [ ] No cedolino se dati minimi mancanti.

### Fase 6 — AI Contabilità Assistita

- [ ] Piano conti minimo.
- [ ] Prima nota proposta.
- [ ] Classificazione spese/ricavi.
- [ ] IVA proposta.
- [ ] Conferma manuale.
- [ ] Storico correzioni.

Controllo frequente:

- [ ] AI non decide detraibilità finale da sola.
- [ ] Ogni proposta ha motivazione.

### Fase 7 — AI Tax Saving Engine

- [ ] Regole opportunità.
- [ ] Suggerimenti IVA.
- [ ] Suggerimenti deducibilità.
- [ ] Documenti mancanti.
- [ ] Simulazione regime fiscale.
- [ ] Simulazione cashflow fiscale.
- [ ] Priorità per impatto.

Controllo frequente:

- [ ] Ogni consiglio ha fonte/regola.
- [ ] Ogni consiglio ha rischio.
- [ ] Ogni consiglio richiede conferma.
- [ ] Non usare linguaggio "trucco" o "scappatoia".

### Fase 8 — Copilot operativo

- [ ] Command palette.
- [ ] Chat laterale.
- [ ] Tool interni: cerca azienda, crea bozza, trova dati mancanti, prepara F24.
- [ ] Conferma azioni.
- [ ] Tracciamento tool call.

Controllo frequente:

- [ ] Il Copilot non invia nulla.
- [ ] Il Copilot non cancella dati.
- [ ] Il Copilot spiega perché propone un'azione.

### Fase 9 — Dashboard CFO e risparmio

- [ ] IVA prevista.
- [ ] Imposte/scadenze.
- [ ] Risparmio potenziale.
- [ ] Documenti mancanti.
- [ ] Fatture scadute.
- [ ] Costo personale.
- [ ] Report per azienda.

Controllo frequente:

- [ ] Numeri sempre riconciliabili.
- [ ] Importi con origine dati.

### Fase 10 — Validazione professionale

- [ ] Revisione commercialista.
- [ ] Revisione consulente del lavoro.
- [ ] Revisione privacy/GDPR.
- [ ] Test su aziende pilota.
- [ ] Correzioni da feedback.

Controllo frequente:

- [ ] Nessuna vendita come prodotto fiscale "certificato" prima della validazione.

---

## 10. Checklist generale anti-salti

Prima di chiudere ogni fase:

- [ ] Build backend verde.
- [ ] Test backend verdi.
- [ ] Build frontend verde.
- [ ] Test tenant isolation se tocca dati.
- [ ] Test conferma manuale se tocca AI.
- [ ] Audit log verificato se salva dati.
- [ ] UI mobile controllata.
- [ ] Errori scritti in italiano semplice.
- [ ] `PIANO.md` aggiornato se la fase entra nel piano principale.
- [ ] `CLAUDE.md` aggiornato nella sezione Fatto.

---

## 11. Metriche successo

Metriche operative:

- tempo per creare azienda da visura;
- tempo per creare lavoratore da UNILAV;
- percentuale documenti riconosciuti correttamente;
- numero dati mancanti risolti;
- numero duplicati evitati;
- numero scadenze evitate.

Metriche fiscali/valore:

- IVA potenzialmente recuperata;
- costi con documento mancante recuperati;
- sanzioni evitate da scadenze;
- F24 preparati senza errori bloccanti;
- consigli confermati dal commercialista;
- consigli scartati perché rischiosi.

Metriche UX:

- task success;
- soddisfazione utente;
- numero click per completare un flusso;
- tempo medio di conferma documento;
- errori utente per form.

---

## 12. Fonti iniziali da usare come base

Design/product:

- Google Material Design 3: https://m3.material.io/
- Apple Human Interface Guidelines: https://developer.apple.com/design/human-interface-guidelines
- Microsoft Fluent 2: https://fluent2.microsoft.design/
- Microsoft Inclusive Design: https://inclusive.microsoft.design/
- IBM Carbon: https://carbondesignsystem.com/
- Salesforce Lightning Design System: https://www.lightningdesignsystem.com/
- SAP Fiori design: https://www.sap.com/design-system/fiori-design-web/
- Shopify Polaris: https://polaris-react.shopify.com/
- Samsung One UI: https://developer.samsung.com/one-ui
- GOV.UK Design System: https://design-system.service.gov.uk/
- Stripe UI/components: https://docs.stripe.com/stripe-apps/components

AI:

- OpenAI Images and vision: https://developers.openai.com/api/docs/guides/images-vision
- OpenAI Structured Outputs: https://developers.openai.com/api/docs/guides/structured-outputs
- OpenAI Function calling: https://developers.openai.com/api/docs/guides/function-calling

Fiscalità italiana da collegare a regole versionate:

- Agenzia Entrate, fattura elettronica: https://www.agenziaentrate.gov.it/portale/aree-tematiche/fatturazione-elettronica
- Agenzia Entrate, regime forfetario: https://www.agenziaentrate.gov.it/portale/regime-forfetario-le-regole-2020-/infogen-regime-forfetario-le-regole-2020-
- Agenzia Entrate, semplificazioni forfetario IVA: https://www.agenziaentrate.gov.it/portale/schede/agevolazioni/regime-agevolato-forfettario/semplificazioni-e-adempimenti
- Agenzia Entrate, agevolazioni dichiarazione: https://www.agenziaentrate.gov.it/portale/tutte-le-agevolazioni-della-dichiarazione-2025
- Agenzia Entrate, conservazione fatture: https://www.agenziaentrate.gov.it/portale/schede/comunicazioni/fatture-e-corrispettivi/faq-fe/risposte-alle-domande-piu-frequenti-categoria/registrazione-e-conservazione-delle-fatture

---

## 13. Decisione prossima

Prossimo passo consigliato:

1. generare 10 concept UI AI-first;
2. scegliere stile;
3. poi implementare shell + AI Document Inbox MVP.

Non iniziare Tax Saving Engine completo prima di:

- design scelto;
- pipeline documenti pronta;
- regole normative versionate;
- conferma professionista sulle prime regole.
