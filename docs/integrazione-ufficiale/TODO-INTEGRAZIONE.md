# TODO integrazione nel software

## 1. Decisione architetturale

- Creare un modulo `ade-telematico` separato dal modulo paghe/contabilita.
- Salvare ogni pratica in stati: `bozza`, `generata`, `controllata_ok`, `controllata_ko`, `pronta_per_desktop_telematico`, `inviata_manualmente`, `ricevuta_acquisita`, `scartata`.
- Non collegare direttamente il login Agenzia nel gestionale senza API/accreditamento ufficiale.
- Per apertura, variazione e cessazione impresa usare il piano dedicato `TODO-COMUNICA-CAMERA-INAIL-INPS.md`.

## 2. Generazione file

- Implementare writer tracciati a lunghezza fissa e record variabili.
- CU 2026: record `A`, `B`, eventuale `C`, `D`, `G`, `H`, `L`, `Z`.
- F24: record `A`, `M`, `V`, `Z`, distinguendo contribuente/intermediario.
- RCU26: richiesta massiva Certificazioni Uniche, se serve acquisizione da cassetto fiscale.
- 77026: aggiungere solo se il software gestisce sostituti d'imposta in modo completo.

## 3. Controlli ufficiali integrati

- Creare wrapper Java/CLI che esegue i JAR ufficiali.
- Leggere `TabellaAssociazioniControlli.json` a runtime o mantenerne copia versionata.
- Per ogni controllo salvare:
  - file originale generato;
  - file diagnostico `.dgn`;
  - diagnostico leggibile quando disponibile;
  - file controllato `.dcm`;
  - exit code e log.

## 4. Moduli gia scaricati

- `CUR26`: Certificazione Unica 2026.
- `RCU26`: Richiesta Certificazioni Uniche 2026.
- `F24A0` / `F7726`: Modelli F24.
- `F24EP`: F24 Enti Pubblici.
- `77026`: Modello 770 2026.
- `SuppTelem.jar`: supporto comune obbligatorio.

## 5. Desktop Telematico

- Installare Desktop Telematico manualmente sul computer dell'utente/intermediario.
- Usarlo per autenticazione e invio ufficiale.
- Il gestionale deve esportare i file in una cartella chiara, per esempio:
  - `export/agenzia-entrate/da-controllare`
  - `export/agenzia-entrate/controllati`
  - `export/agenzia-entrate/da-inviare-desktop-telematico`
  - `export/agenzia-entrate/ricevute`

## 5-bis. ComUnica / Camera di Commercio / INAIL / INPS

- Implementare ComUnica come modulo separato da `ade-telematico`.
- Usare il canale ufficiale InfoCamere, non Starweb/DIRE pilotati dal browser.
- Prima fare adesione al servizio web InfoCamere per verifica/trasmissione pratiche.
- Generare pacchetto pratica ZIP, XML `presentazione`, hash SHA1 e allegati firmati.
- Chiamare in ordine:
  - `controllaPratica`;
  - `getEsito`;
  - `inviaPratica`;
  - `getEsito`.
- Per partita IVA:
  - usare ComUnica se il soggetto deve iscriversi a Registro Imprese/REA;
  - usare AA7/AA9 diretto solo per soggetti non obbligati a RI/REA.
- Per INAIL e INPS:
  - valorizzare i modelli/quadri dentro ComUnica quando l'adempimento e collegato all'impresa;
  - non promettere API diretta INAIL/INPS se non c'e servizio ufficiale/accreditamento documentato.
- Per SUAP/SCIA:
  - usare il servizio ufficiale ComUnica con SCIA contestuale dopo adesione;
  - esporre endpoint HTTPS del software per la notifica SUAP se richiesto.

Dettaglio operativo: `TODO-COMUNICA-CAMERA-INAIL-INPS.md`.

## 6. Ricevute

- Implementare import manuale delle ricevute scaricate da Agenzia.
- Collegare ricevute a pratica tramite codice fiscale, tipo fornitura, periodo, protocollo.
- Gestire esiti: acquisita, scartata, annullata.

## 7. Controlli interni prima dei JAR

- Validazione formale dei campi obbligatori.
- Controlli codice fiscale e partita IVA.
- Controllo lunghezza record e caratteri ammessi.
- Calcolo totali/riepiloghi prima di generare il file.
- Versionamento per anno fiscale.

## 8. Prossimi passaggi pratici

1. Scegliere stack del gestionale: Java, .NET, Node, Python, Flutter desktop, web app locale.
2. Implementare prima F24 ordinario: e il modulo piu circoscritto.
3. Integrare `ControlliF24A0.jar` e leggere diagnostico.
4. Poi implementare CU 2026 con `ModuliControlloCUR26.jar`.
5. Solo dopo aggiungere flusso ricevute e handoff a Desktop Telematico.
6. In parallelo, avviare accreditamento InfoCamere se il gestionale deve inviare pratiche ComUnica direttamente.
