# Integrazione telematica ufficiale

Materiale di pianificazione per integrare il gestionale con i flussi telematici
ufficiali italiani (Agenzia Entrate, ComUnica/Registro Imprese, INAIL, INPS, SUAP).

Principio guida: **generare → validare → controllo ufficiale (JAR/web service) →
firmare → inviare solo via canali ufficiali**. Mai simulare login o interfacce web.

Data raccolta materiale: 2026-06-26.

## Indice

- [RIASSUNTO-FONTI-UFFICIALI.md](RIASSUNTO-FONTI-UFFICIALI.md) — sintesi fonti ufficiali, cosa si puo integrare, architettura consigliata, primo passo pratico.
- [KIT-INTEGRAZIONE-AGENZIA-ENTRATE.md](KIT-INTEGRAZIONE-AGENZIA-ENTRATE.md) — contenuto del kit (JAR controlli, specifiche, manuali), cosa NON integrare come API privata, uso dei JAR.
- [TODO-INTEGRAZIONE.md](TODO-INTEGRAZIONE.md) — piano tecnico modulo `ade-telematico`: generazione file (F24, CU), controlli JAR, Desktop Telematico, ricevute.
- [TODO-COMUNICA-CAMERA-INAIL-INPS.md](TODO-COMUNICA-CAMERA-INAIL-INPS.md) — piano completo ComUnica/RI/REA, partita IVA AA7-AA9, INAIL, INPS, SUAP: canali, modello dati, backlog a fasi.

## Stato implementazione (codice)

Modulo `backend/src/Paghe.Core/AdeTelematico/` (puro dominio, nessun invio telematico):

- [x] Modello F24 ordinario: contribuente/intermediario + sezioni Erario, INPS, Regioni, IMU/tributi locali, Altri enti (`ModelloF24.cs`).
- [x] Controlli interni pre-JAR (TODO §7): codice fiscale (carattere di controllo), partita IVA (mod 10), importi `decimal`, quadratura/saldo (`ValidatoreFiscale.cs`, `ValidazioneF24.cs`).
- [x] Macchina a stati della pratica telematica (`PraticaTelematica.cs`).
- [x] Wrapper controlli ufficiali: interfaccia + costruzione argomenti `java -cp` + runner a processo (`ControlliUfficiali.cs`).
- [x] **Import UNILAV** (Comunicazione Obbligatoria): legge l'XML, riconosce datore/lavoratore/rapporto e crea in automatico dipendente + rapporto senza inserimento manuale (`ImportazioneUnilav.cs`). Lettura namespace-agnostica e tollerante. Endpoint `POST /api/aziende/{id}/dipendenti/da-unilav`.
- [x] **F24 dalle paghe**: costruisce l'F24 da versare dai cedolini (ritenute IRPEF 1001, addizionali 3802/3848, contributi INPS), con scadenza al 16 del mese successivo (`F24DaCedolini.cs`). Endpoint `POST /api/f24/da-paghe` (importi nella request).
- [x] **F24 del mese da DB**: pesca in automatico i cedolini archiviati di un'azienda per anno/mese e li aggrega (`CedoliniService.GeneraF24DelMeseAsync`). Endpoint `POST /api/aziende/{id}/f24/mese`.
- [x] **Persistenza pratiche F24** con stato (macchina a stati) ed export su cartella per il Desktop Telematico (`PraticheF24Service`, `PraticaF24Entity`). Endpoint: `POST /api/aziende/{id}/f24/mese/salva`, `GET /api/aziende/{id}/f24`, `GET /api/f24/{id}`, `POST /api/f24/{id}/stato`, `POST /api/f24/{id}/esporta` (scrive in `export/agenzia-entrate/da-controllare/`).
- [x] **Tracciato telematico ufficiale F24 a lunghezza fissa (record A/M/V/Z, 1900 char, modello sez. IMU v.2013)**: implementato in `TracciatoF24Ufficiale.cs` dalle specifiche ufficiali (Allegato 1 AdE, nel kit). Generato/archiviato nella pratica ed esportato da `/esporta`.
  - **VALIDATO col modulo ufficiale `ControlliF24A0.jar` v.7.3.4 → ESITO POSITIVO** (prodotto il file controllato `.dcm`, zero errori) per un F24 con sezioni **Erario (1001) + Regioni (3802) + IMU/locali (3848)**. Confermati corretti: struttura record A/M/V/Z, posizioni, importi in centesimi, rateazione `00MM`, totali/segni di sezione, saldo finale, saldo M con virgola. Classe controllo `finanze.BQJCB102.main.CtrlFile` (costanti in `ControlloF24A0`).
  - Riscontri risolti durante la validazione (ora nel codice): controllo **codice ufficio P.IVA** (cifre 8-10: 001-100/120/121/888/999) in `ValidatoreFiscale`; rateazione `00MM` per 1001/3802/3848; periodo INPS "a" a zero per DM10; flag IMU non usati per 3848.
  - [ ] **Sezione INPS**: la **matricola** per causale DM10 deve rispettare il formato INPS "F" (Tabella formati matricole e codici INPS, con check interno): nessun valore a 10 cifre di test lo soddisfa e la regola non è ricavabile dal solo JAR. Va verificata con una **matricola reale** dell'azienda + la tabella INPS. L'F24 di sole ritenute/addizionali (senza INPS) è già ufficialmente valido.
- [x] **Tracciato telematico ufficiale CU 2026 (codice fornitura CUR26, redditi 2025)**: implementato in `TracciatoCuUfficiale.cs` dalle specifiche tecniche ufficiali agg. 24/02 (nel kit): record da 1.900 char, A/B/Z posizionali, C/D/G a struttura variabile (tabella di 75 elementi codice campo 8 + valore 16 dal carattere 90), quadri DA (anagrafiche), DB (fiscale: redditi, giorni, ritenute, addizionali, detrazioni, trattamento integrativo cod. 2, DB001718 cod. 2, tipologia reddituale 1), DC (previdenza INPS con mesi UniEmens CB/CB12), quadro CT (record C) e coda Z con i contatori. Servizio `CertificazioneUnicaService.CostruisciTracciatoTelematicoAsync` (tutte le CU dell'azienda + avvisi sui dati mancanti), endpoint `GET /api/aziende/{id}/cu/{anno}/telematico`.
  - **VALIDATO col modulo ufficiale `ModuliControlloCUR26.jar` v.1.1.0 (26/02/2026) → ESITO POSITIVO** (file controllato `.dcm` prodotto, zero errori nel diagnostico `.dgn`) su tre forniture: CU singola anno intero con quadro CT, CU con cessazione a metà anno e mesi UniEmens parziali senza quadro CT, fornitura con due certificazioni. Classe controllo `it.finanze.entrate.dic.main.MCUR26` (costanti in `ControlloCur26`); dump di prova dai test con le variabili `CU_DUMP`/`CU_DUMP2`/`CU_DUMP3`.
  - Riscontri risolti durante la validazione (ora nel codice): i campi posizionali numerici assenti vanno riempiti a **zeri, non a spazi** (record B campi 22/28/29, record D campi 6/7); il **CF con carattere di controllo errato scarta la singola CU** (il fixture di test `RSSMRA85T10F257K` era invalido → `...A`); **DB001718 è obbligatorio** in presenza di redditi (codice 2 = nessuna agevolazione), come DB001390 (trattamento integrativo) e la sezione tipologie reddituali DB001751/752.
  - [ ] Perimetro dichiarato: solo lavoro dipendente standard (niente record H lavoro autonomo, L locazioni, quadro DF TFR tassazione separata, assistenza fiscale 730, familiari a carico nel prospetto, annotazioni codificate DN). L'anno certificabile è il 2025 (modello CU 2026). Persistenza pratica e schermata SPA da fare.
- [ ] Allineare i nomi di campo del parser UNILAV su un tracciato reale (oggi su standard CO + candidati tolleranti).

Flusso completo verificato end-to-end (live + test): carica UNILAV (`POST /api/aziende/{id}/dipendenti/da-unilav`) → crea dipendente/rapporto → `POST /api/rapporti/{id}/cedolini` calcola e archivia il cedolino → `POST /api/aziende/{id}/f24/mese` genera l'F24 del periodo dai cedolini archiviati.

Test: `backend/tests/Paghe.Tests/AdeTelematico/`.

## Note

- `TODO-AUTOMAZIONE-UFFICIALE.md` (invio/ricevute via Entratel - Multifile) e citato nel kit ma non ancora trascritto qui.
- Gli asset binari ufficiali (JAR, XSD, ZIP, PDF, WSDL) NON sono in questo repo: vivono nel kit scaricato a parte. Qui c'e solo la documentazione/pianificazione.
