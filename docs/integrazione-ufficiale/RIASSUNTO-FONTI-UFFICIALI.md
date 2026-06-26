# Riassunto fonti ufficiali e integrazione diretta

Data raccolta: 2026-06-26.

## Risposta breve

Si puo integrare in modo diretto e ufficiale, ma non simulando login o siti web.

Per Camera di Commercio / Registro Imprese / ComUnica la strada corretta e il servizio InfoCamere per verifica e trasmissione pratiche, previa adesione. Il client ufficiale scaricato contiene WSDL, XSD e sorgenti Java di esempio.

Per Agenzia Entrate il gestionale puo generare file, controllarli con JAR ufficiali e inviarli con Desktop Telematico / Entratel / Multifile quando il flusso lo consente.

Per INAIL e INPS, quando l'adempimento e collegato all'impresa, il canale e ComUnica. Per servizi fuori ComUnica si usano solo portali o interfacce ufficiali/accreditate, senza automazione del login.

## Fonti ufficiali principali

- Registro Imprese ComUnica: https://www.registroimprese.it/comunicazione-unica-d-impresa
- Specifiche Registro Imprese / ComUnica / web service: https://www.registroimprese.it/specifiche-tecniche
- Agenzia Entrate AA7/10: https://www.agenziaentrate.gov.it/portale/schede/istanze/aa7_10
- Agenzia Entrate AA9/12: https://www.agenziaentrate.gov.it/portale/schede/istanze/aa9_11-apertura-variazione-chiusura-pf
- Repository JAR controlli Agenzia Entrate: https://jws.agenziaentrate.it/telematicoControlliAssoSoftware/Documentazione/TabellaAssociazioniControlli.json
- INAIL modelli denuncia: https://www.inail.it/portale/it/atti-e-documenti/moduli-e-modelli/assicurazione/moduli-assicurazione/gestione-rapporto-assicurativo/modelli-denuncia-di-iscrizione--esercizio--variazione-e-cessazio.html
- INPS iscrizione/inizio attivita azienda: https://www.inps.it/it/it/dettaglio-approfondimento.schede-informative.49852.comunicazione-dell-iscrizione-o-inizio-attivit-di-un-azienda.html
- Impresainungiorno / SUAP: https://www.impresainungiorno.gov.it

## Cosa e stato scaricato

- Specifiche RI/Fedra e allegati.
- Specifiche Comunicazione Unica 2.0 e 3.1.
- Client WS InfoCamere `wscuClientWS.zip`.
- Manuali e moduli adesione per ComUnica/Bilancio e SCIA contestuale.
- Modelli, istruzioni e specifiche AA7/AA9.
- JAR controlli AA7/AA9, AA5, ANR.
- Modelli INAIL principali e 91 file ufficiali della pagina modelli denuncia.
- Documentazione INPS ComUnica e specifiche DM.
- Documenti SUAP/SCIA e XSD.

## Architettura consigliata

1. Il gestionale decide il canale:
   - impresa RI/REA: ComUnica;
   - soggetto non RI/REA: AA7/AA9 Agenzia Entrate;
   - servizi fuori ComUnica: handoff ufficiale al portale/servizio dell'ente.
2. Genera pratica e allegati.
3. Valida localmente con XSD e regole interne.
4. Firma dove richiesto.
5. Esegue controllo ufficiale:
   - JAR Agenzia Entrate per AA7/AA9/F24/CU;
   - `controllaPratica` InfoCamere per ComUnica.
6. Invia solo dopo esito OK:
   - Entratel/Multifile per flussi Agenzia supportati;
   - `inviaPratica` InfoCamere per ComUnica.
7. Importa esiti/ricevute:
   - `getEsito` InfoCamere;
   - ricevute Entratel/Agenzia per flussi fiscali.

## Primo passo pratico

Avviare adesione InfoCamere per il web service ComUnica e costruire una demo che esegue solo `controllaPratica`, senza invio reale. Quando il controllo preliminare funziona, si passa a firma, invio e gestione esiti.
