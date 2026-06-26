# TODO ComUnica, Camera di Commercio, partita IVA, INAIL, INPS, SUAP

Data raccolta: 2026-06-26.

Questo piano serve per integrare nel gestionale i flussi ufficiali di apertura, variazione e cessazione impresa senza simulare login o siti web.

## Regola base

- Non automatizzare browser, Starweb, DIRE, Telemaco, INAIL, INPS o area riservata Agenzia con click/login simulati.
- Usare solo tracciati, specifiche, controlli, web service e procedure ufficiali.
- Dove serve invio telematico, attivare prima adesione/accreditamento ufficiale e utente abilitato.

## Canali da implementare

### 1. Impresa iscritta a Registro Imprese o REA

Canale primario: Comunicazione Unica d'impresa.

La pratica ComUnica e un pacchetto informatico che puo contenere:

- modello riepilogativo ComUnica;
- modulistica Registro Imprese / REA;
- modulistica Agenzia Entrate per codice fiscale e partita IVA;
- modulistica INPS;
- modulistica INAIL;
- eventuale pratica SUAP / SCIA contestuale.

Integrazione diretta ufficiale:

- generare ZIP pratica secondo specifiche ComUnica e RI/Fedra;
- generare XML `presentazione`;
- calcolare hash SHA1 del file ZIP nel formato richiesto;
- firmare digitalmente i file quando previsto;
- chiamare il servizio InfoCamere per `controllaPratica`;
- se OK, chiamare `inviaPratica`;
- interrogare `getEsito` finche la lavorazione e conclusa;
- importare protocollo, esiti, ricevute e scarti.

Asset scaricati:

- `registro-imprese/Specifiche_tecniche_RI_v_7_08.pdf`
- `registro-imprese/Decreto_direttoriale_20_maggio_2026_Fedra_v_709.pdf`
- `registro-imprese/allegati_709.zip`
- `registro-imprese/SpecificheComunicazioneUnica.zip`
- `registro-imprese/SpecificheComunicazioneUnica3.zip`
- `registro-imprese/wscuClientWS.zip`
- `registro-imprese/Manuale_Operativo_Servizio_verifica_trasmissione_ComUnica_Bilancio.pdf`
- `registro-imprese/Modulo_adesione_verifica_trasmissione_ComUnica_Bilancio.pdf`

Nota tecnica: `wscuClientWS.zip` contiene sorgenti Java, `ComunicazionePraticheRI.wsdl`, `presentazione.xsd` ed `esito.xsd`. Le operazioni WSDL verificate sono `controllaPratica`, `inviaPratica` e `getEsito`.

### 2. Partita IVA non soggetta a Registro Imprese

Canale: AA7/10 o AA9/12 Agenzia Entrate.

- AA7/10: soggetti diversi da persone fisiche.
- AA9/12: persone fisiche/professionisti non obbligati a iscrizione RI/REA.
- Se il soggetto e tenuto a Registro Imprese o REA, usare ComUnica.

Integrazione:

- generare file telematico secondo specifiche AA7/AA9;
- controllare con JAR ufficiale `Ctrl_AA7_AA9_AT6VA.jar`;
- per AA5 usare `controllo_AA5_AA508.jar`;
- per ANR usare `controllo_ANR_AN1VA.jar`;
- inviare con canale ufficiale Agenzia: Entratel/Desktop Telematico/Multifile dove supportato, oppure invio diretto manuale dell'utente se previsto.

Asset scaricati:

- `agenzia-entrate-partita-iva/AA7_10_modello_2025.pdf`
- `agenzia-entrate-partita-iva/AA7_10_istruzioni_2025.pdf`
- `agenzia-entrate-partita-iva/AA7_10_specifiche_inizio_variazione.pdf`
- `agenzia-entrate-partita-iva/AA7_10_specifiche_cessazione.pdf`
- `agenzia-entrate-partita-iva/AA9_12_modello.pdf`
- `agenzia-entrate-partita-iva/AA9_12_istruzioni_2024.pdf`
- `agenzia-entrate-partita-iva/AA9_12_specifiche_inizio_variazione.pdf`
- `agenzia-entrate-partita-iva/AA9_12_specifiche_cessazione.pdf`
- `controlli-jar/Ctrl_AA7_AA9_AT6VA.jar`
- `controlli-jar/controllo_AA5_AA508.jar`
- `controlli-jar/controllo_ANR_AN1VA.jar`

### 3. INAIL

Per apertura, variazione e cessazione posizione assicurativa collegata a impresa, il flusso ufficiale passa da ComUnica quando l'adempimento e relativo all'impresa.

Integrazione:

- raccogliere i dati ditta, PAT, sedi lavori, lavorazioni, artigiani, retribuzioni, soci/collaboratori quando necessari;
- valorizzare i quadri INAIL previsti dalla pratica ComUnica;
- allegare eventuali moduli ufficiali firmati se richiesti dalla pratica;
- lasciare i servizi INAIL fuori ComUnica sui canali ufficiali INAIL quando non esiste una API pubblica/accreditata per software house.

Asset scaricati:

- `inail/Denuncia_iscrizione_esercizio_variazione_cessazione.pdf`
- `inail/Manuale_utente_posizioni_assicurative_navigazione.pdf`
- `inail/Modulo_Quadro_P.pdf`
- `inail/Modulo_Q_vers_10_12_2024.pdf`
- `inail/Modulo_Q1_vers_10_12_2024.pdf`
- `inail/QuadroR.pdf`
- `inail/QuadroR_b.pdf`
- `inail/modelli-denuncia/` con 91 file ufficiali PDF/XLS/XLSX scaricati dalla pagina INAIL dei modelli denuncia.
- `suap/InvioDenunce.xsd`, pubblicato nella sezione tecnica Registro Imprese per tracciato denunce INAIL.

### 4. INPS

Per impresa con dipendenti, impresa agricola, artigiani e commercianti, ComUnica assolve molti adempimenti INPS. Dal materiale ufficiale INPS risulta che per l'iscrizione/inizio attivita di azienda con dipendenti ComUnica e il canale da usare nei casi previsti.

Integrazione:

- mappare pratica DM per imprese con dipendenti;
- mappare pratica DA per imprese agricole;
- mappare artigiani/commercianti tramite modulistica RI integrata;
- distinguere casi successivi all'avvio dove INPS indica ComUnica facoltativa o servizi online INPS;
- non promettere invio diretto INPS via API se non c'e convenzione o servizio ufficiale documentato.

Asset scaricati:

- `inps/Circolare_41_2010_ComUnica.html`
- `inps/Specifiche_tecniche_Comunicazione_Unica_DM_19_11_2009.pdf`
- `inps/Comunicazione_iscrizione_inizio_attivita_azienda.html`
- `inps/Comunicazione_cessazione_attivita_azienda.html`
- `inps/Comunicazione_variazione_inquadramento_azienda.html`
- `suap/Moduli-DM.zip`
- `suap/ImpreseAgricole.zip`
- `suap/ModuliCD-IAP-2.zip`

### 5. SUAP / SCIA contestuale

Canale: servizio di integrazione applicativa ComUnica con SCIA contestuale.

Integrazione:

- aderire al servizio InfoCamere;
- esporre un endpoint HTTPS del gestionale/software house per ricezione notifica istanza SUAP;
- censire certificato e parametri in fase di adesione;
- chiamare i servizi per ottenere URL Front Office SUAP, istanza e file;
- collegare pratica SCIA alla pratica ComUnica tramite codice pratica;
- verificare giornalmente elenco comuni SUAP abilitati.

Asset scaricati:

- `suap/Manuale_operativo_integrazione_ComUnica_SCIA_contestuale.pdf`
- `suap/Modulo_adesione_integrazione_ComUnica_SCIA.pdf`
- `suap/ImportaPraticaSUAP_manuale_uso_1_0.pdf`
- `suap/InvioDenunce.xsd`
- `suap/Moduli-DM.zip`
- `suap/ImpreseAgricole.zip`
- `suap/ModuliCD-IAP-2.zip`

## Accreditamenti e prerequisiti

- Compilare modulo adesione InfoCamere per servizio verifica/trasmissione pratiche ComUnica/Bilancio.
- Inviare adesione via PEC all'indirizzo indicato dal manuale ufficiale InfoCamere.
- Ottenere `client_id` e `client_secret` della societa richiedente.
- Configurare utente Telemaco abilitato alla trasmissione pratiche.
- Definire redirect URI e gestione access token per autenticazione IAM InfoCamere.
- Per SCIA contestuale, compilare modulo dedicato e censire endpoint/certificato del software.
- Definire firma digitale/CNS/token e conservazione sicura.
- Definire conto/credito Telemaco e gestione diritti/bollo.

## Modello dati consigliato

Tabelle o aggregate minime:

- `comunica_pratica`
- `comunica_soggetto`
- `comunica_unita_locale`
- `comunica_ri_rea`
- `comunica_iva`
- `comunica_inail`
- `comunica_inps`
- `comunica_suap`
- `comunica_allegato`
- `comunica_firma`
- `comunica_ws_request`
- `comunica_esito`
- `comunica_ricevuta`
- `comunica_accreditamento`

Stati pratica:

- `bozza`
- `validazione_locale_ko`
- `validazione_locale_ok`
- `pacchetto_generato`
- `firmata`
- `controllo_ws_inviato`
- `controllo_ws_ok`
- `controllo_ws_ko`
- `pronta_invio`
- `invio_ws_inviato`
- `protocollata`
- `sospesa`
- `evasa`
- `scartata`
- `annullata`

## Backlog tecnico

### Fase 0 - Governo e sicurezza

- [ ] Decidere ruolo: software house, intermediario, consulente o gestionale interno azienda.
- [ ] Acquisire moduli adesione firmati.
- [ ] Ottenere utenza Telemaco e abilitazioni.
- [ ] Definire policy credenziali: niente password in chiaro, usare keychain/vault.
- [ ] Definire audit log: chi ha generato, firmato, controllato e inviato.
- [ ] Definire conservazione documentale per pratiche, ricevute e allegati.

### Fase 1 - Generatore pacchetto ComUnica

- [ ] Estrarre XSD da `SpecificheComunicazioneUnica.zip`.
- [ ] Estrarre XSD/PDF da `SpecificheComunicazioneUnica3.zip`.
- [ ] Implementare writer XML modello ComUnica.
- [ ] Implementare writer RI/REA secondo specifiche RI/Fedra v7.08/v7.09.
- [ ] Implementare generatori allegati U3T/U3R e distinta.
- [ ] Implementare generatore ZIP pratica.
- [ ] Implementare calcolo SHA1 decimale del file ZIP.
- [ ] Validare XML con XSD ufficiali.
- [ ] Validare naming file e dimensioni massime.

### Fase 2 - Partita IVA

- [ ] Implementare scelta automatica canale: ComUnica se RI/REA obbligatorio, AA7/AA9 diretto se non obbligatorio.
- [ ] Implementare record AA7 inizio/variazione/cessazione.
- [ ] Implementare record AA9 inizio/variazione/cessazione.
- [ ] Integrare `Ctrl_AA7_AA9_AT6VA.jar`.
- [ ] Integrare diagnostico `finanze.anag.generale.StampaDiagnostici`.
- [ ] Aggiungere validazioni: codice fiscale, partita IVA, ATECO, domicilio fiscale, rappresentante, VIES.

### Fase 3 - INAIL

- [ ] Creare mappa quadri INAIL: ditta, PAT, sede lavori, dipendenti, artigiani, soci, rischi speciali.
- [ ] Derivare i campi dal set `inail/modelli-denuncia`.
- [ ] Implementare UI raccolta dati per apertura posizione.
- [ ] Implementare UI variazione/cessazione.
- [ ] Collegare dati INAIL al pacchetto ComUnica.
- [ ] Gestire casi fuori ComUnica come handoff a portale/servizi ufficiali INAIL, senza automazione login.

### Fase 4 - INPS

- [ ] Implementare pratica DM imprese con dipendenti.
- [ ] Implementare pratica DA imprese agricole.
- [ ] Implementare campi artigiani/commercianti integrati RI.
- [ ] Validare file INPS XML contro specifiche ufficiali.
- [ ] Gestire matricola aziendale, codice azienda, CIDA, decorrenze e cessazioni.
- [ ] Separare casi obbligatori ComUnica da casi facoltativi o da servizi online INPS.

### Fase 5 - Web service InfoCamere

- [ ] Estrarre `wscuClientWS.zip` in area sorgenti separata.
- [ ] Generare client SOAP da `ComunicazionePraticheRI.wsdl`.
- [ ] Implementare OAuth/IAM secondo manuale operativo nuovo.
- [ ] Gestire `client_id`, `client_secret`, redirect URI e access token.
- [ ] Implementare `controllaPratica`.
- [ ] Implementare polling `getEsito` per controllo preliminare.
- [ ] Implementare `inviaPratica`.
- [ ] Implementare polling `getEsito` per invio.
- [ ] Salvare request/response, praticaID, errori, protocollo e ricevute.
- [ ] Gestire ambiente test e ambiente produzione con configurazione separata.

### Fase 6 - Firma e allegati

- [ ] Definire motore firma CAdES/PAdES tramite provider ufficiale/token utente.
- [ ] Non salvare PIN firma nel database.
- [ ] Firmare distinte e allegati richiesti.
- [ ] Verificare firme prima dell'invio.
- [ ] Gestire procura speciale, documento identita e allegati obbligatori.

### Fase 7 - SUAP / SCIA

- [ ] Aderire al servizio SCIA contestuale.
- [ ] Implementare endpoint HTTPS per notifica SUAP.
- [ ] Censire certificato in adesione.
- [ ] Implementare chiamate per URL Front Office SUAP.
- [ ] Implementare recupero istanza e file SUAP.
- [ ] Collegare pratica SCIA al codice pratica ComUnica.
- [ ] Scaricare/aggiornare elenco comuni SUAP abilitati.

### Fase 8 - Test e go-live

- [ ] Costruire pratiche di test: nuova impresa, variazione, cessazione.
- [ ] Testare solo controllo preliminare prima dell'invio reale.
- [ ] Verificare costi/diritti/bollo/credito Telemaco.
- [ ] Fare test con pratica scartata per import errori.
- [ ] Fare test con pratica protocollata e ricevute.
- [ ] Scrivere manuale operatore interno.
- [ ] Far validare flussi a commercialista/consulente del lavoro prima del go-live.

## Cosa fare subito

1. Scegliere il primo caso pilota: apertura impresa individuale, societa, variazione o cessazione.
2. Completare adesione InfoCamere per web service ComUnica.
3. Estrarre `wscuClientWS.zip` e generare client SOAP in un progetto sandbox.
4. Implementare solo `controllaPratica` prima di implementare `inviaPratica`.
5. Aggiungere nel gestionale la mappa dei campi obbligatori per RI, IVA, INAIL e INPS.
6. Portare a invio reale solo dopo test di controllo preliminare e revisione di un professionista.
