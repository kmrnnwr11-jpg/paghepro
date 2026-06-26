# Kit integrazione ufficiale Agenzia Entrate

Data raccolta: 2026-06-26.

Questo kit raccoglie documentazione e moduli ufficiali utili per integrare un software gestionale con i flussi telematici dell'Agenzia delle Entrate e con i flussi ComUnica / Registro Imprese / INAIL / INPS / SUAP.

## Cosa si puo integrare

- Generazione dei file telematici nel gestionale, seguendo le specifiche ufficiali.
- Controllo dei file tramite JAR ufficiali dell'Agenzia delle Entrate.
- Lettura del diagnostico prodotto dai controlli e blocco invio se ci sono errori.
- Preparazione di una cartella "validati" da consegnare a Desktop Telematico / Entratel / File Internet.
- Automazione batch ufficiale tramite Entratel - Multifile, usando gli script documentati `runAutentica`, `runInvio`, `runDownload`, `runRicevute` e `runVisualizzaRicevute`.
- Generazione e controllo di pratiche ComUnica per Camera di Commercio, Agenzia Entrate, INAIL, INPS e SUAP.
- Integrazione diretta ufficiale con InfoCamere tramite web service ComUnica, dopo adesione/accreditamento.

## Cosa non va integrato come API privata

- Login automatico all'area riservata.
- Invio autenticato simulando l'interfaccia web.
- Automazione non documentata di Desktop Telematico.

Per inviare le pratiche, il flusso ufficiale resta: file generato -> controllo -> autenticazione -> invio tramite canale ufficiale. L'automazione diretta va fatta usando Entratel - Multifile, non simulando login o click sul sito.

Per ComUnica / Camera di Commercio il flusso ufficiale diretto e: pacchetto pratica generato -> firma/validazione -> `controllaPratica` -> `inviaPratica` -> `getEsito` tramite web service InfoCamere, dopo adesione e con utente Telemaco abilitato.

## Contenuto

- `controlli-jar/`: JAR ufficiali integrabili nei gestionali.
- `specifiche/`: specifiche tecniche e modelli CU/F24.
- `documentazione/`: manuali Desktop Telematico e integrazione controlli.
- `registro-imprese/`: specifiche RI/Fedra, ComUnica e client WS InfoCamere.
- `agenzia-entrate-partita-iva/`: modelli, istruzioni e specifiche AA7/AA9.
- `inail/`: manuali e modelli denuncia iscrizione/variazione/cessazione.
- `inps/`: circolari, specifiche e pagine operative INPS collegate a ComUnica.
- `suap/`: manuali, XSD e moduli per SCIA contestuale e tracciati collegati.
- `tools/`: script wrapper per chiamare i controlli JAR.
- `manifest-official-sources.json`: elenco fonti ufficiali, link e classi Java.
- `RIASSUNTO-FONTI-UFFICIALI.md`: sintesi delle fonti, cosa si puo integrare e architettura consigliata.
- `TODO-INTEGRAZIONE.md`: piano tecnico per applicare il kit al software.
- `TODO-AUTOMAZIONE-UFFICIALE.md`: piano per invio/ricevute automatiche con Entratel - Multifile.
- `TODO-COMUNICA-CAMERA-INAIL-INPS.md`: piano completo per ComUnica, partita IVA, Camera di Commercio, INAIL, INPS e SUAP.

## Stato ComUnica / Camera / INAIL / INPS

Sono stati scaricati gli asset ufficiali principali:

- specifiche RI/Fedra v7.08/v7.09;
- specifiche ComUnica versione 2.0 e 3.1;
- client WS InfoCamere con WSDL `ComunicazionePraticheRI.wsdl` e XSD `presentazione.xsd` / `esito.xsd`;
- manuale e modulo adesione per verifica/trasmissione pratiche ComUnica e Bilancio;
- modelli e specifiche AA7/AA9 Agenzia Entrate;
- JAR ufficiali AA7/AA9, AA5 e ANR;
- modelli ufficiali INAIL denuncia iscrizione/variazione/cessazione;
- materiale INPS per adempimenti ComUnica;
- manuale e modulo adesione ComUnica con SCIA contestuale.

La strada tecnica consigliata e descritta in `TODO-COMUNICA-CAMERA-INAIL-INPS.md`.

## Stato download Desktop Telematico

Il pacchetto macOS ufficiale e stato avviato ma i mirror dell'Agenzia erano molto lenti. Il file parziale e:

`desktop-telematico/DesktopTelematico-macosx_140.zip.partial`

Per completarlo, riscaricare da uno dei link nel manifest e verificare SHA256 ufficiale:

`40B99A0212B71CE6B57B7CCF373AA4F0E01575315BF6DE27E90035943B1100D6`

## Uso dei controlli JAR

I JAR vanno richiamati da Java con `SuppTelem.jar` nel classpath piu il JAR specifico del modello.

Esempio generico:

```bash
java -cp "controlli-jar/SuppTelem.jar:controlli-jar/ModuliControlloCUR26.jar" \
  it.finanze.entrate.dic.main.MCUR26 \
  input.txt diagnostico.dgn controllato.dcm
```

Il tuo software deve incapsulare questo comando in un servizio interno, salvare diagnostici e file controllati, e mostrare all'utente gli errori prima dell'invio.
