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
- [x] **Tracciato telematico ufficiale F24 a lunghezza fissa (record A/M/V/Z, 1900 char, modello sez. IMU v.2013)**: implementato in `TracciatoF24Ufficiale.cs` dalle specifiche ufficiali (Allegato 1 AdE, nel kit). Generato/archiviato nella pratica ed esportato da `/esporta`. Resta da eseguire la validazione col modulo ufficiale `ControlliF24A0.jar` (classe `finanze.BQJCB102.main.CtrlFile`, costanti in `ControlloF24A0`) — richiede i JAR del kit + Java + autorizzazione a eseguirli.
- [ ] Allineare i nomi di campo del parser UNILAV su un tracciato reale (oggi su standard CO + candidati tolleranti).

Flusso completo verificato end-to-end (live + test): carica UNILAV (`POST /api/aziende/{id}/dipendenti/da-unilav`) → crea dipendente/rapporto → `POST /api/rapporti/{id}/cedolini` calcola e archivia il cedolino → `POST /api/aziende/{id}/f24/mese` genera l'F24 del periodo dai cedolini archiviati.

Test: `backend/tests/Paghe.Tests/AdeTelematico/`.

## Note

- `TODO-AUTOMAZIONE-UFFICIALE.md` (invio/ricevute via Entratel - Multifile) e citato nel kit ma non ancora trascritto qui.
- Gli asset binari ufficiali (JAR, XSD, ZIP, PDF, WSDL) NON sono in questo repo: vivono nel kit scaricato a parte. Qui c'e solo la documentazione/pianificazione.
