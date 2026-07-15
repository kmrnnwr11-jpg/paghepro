# Ricerca cedolino professionale e presenze - 2026

Aggiornata all'11 luglio 2026. Questo documento distingue requisiti normativi, pattern osservati
nei prodotti di mercato e backlog tecnico di PaghePro. Non autorizza a chiamare "ufficiale" un
output non validato.

## Risultato della ricerca

Non esiste un modello grafico statale unico da copiare. Zucchetti, TeamSystem e INAZ differiscono
nell'interfaccia, ma convergono su un flusso: calendario teorico, eventi giornalieri, giustificativi,
controlli bloccanti, elaborazione, confronto, approvazione/chiusura e produzione coerente di
cedolino, LUL, F24 e UniEmens. Copiare solo il loro aspetto darebbe una falsa sensazione di
affidabilita.

Fonti prodotto consultate:

- Zucchetti, elaborazione busta paga e presenze:
  https://www.zucchetti.it/it/cms/professionisti/software-paghe-hr/paghe/servizio-paghe-smart/elaborazione-busta-paga.html
  e https://www.zucchetti.it/it/cms/soluzioni/software-hr-zucchetti/software-gestione-presenze/presenze-assenze-ferie/presenze/software-rilevazione-presenze.html
- TeamSystem, paghe e rilevazione presenze:
  https://www.teamsystem.com/hr/teamsystem-hr/funzionalita/paghe/
  e https://www.teamsystem.com/hr/teamsystem-hr/funzionalita/rilevazione-presenze/
- INAZ, controlli prima della chiusura:
  https://www.inaz.it/software/cruscotto-controlli/

## Requisiti ufficiali principali

Il prospetto paga deve esporre lavoratore, qualifica, periodo, tutte le voci retributive e ogni
trattenuta distinta. Dal 28 giugno 2026 deve indicare anche il CCNL applicato tramite il codice
alfanumerico unico CNEL; servono inoltre firma, sigla o timbro del datore o di chi ne fa le veci.

- Legge 4/1953 vigente: https://www.normattiva.it/atto/caricaDettaglioAtto?atto.codiceRedazionale=053U0004&atto.dataPubblicazioneGazzetta=1953-01-27&tipoDettaglio=multivigenza
- Archivio contratti e codici CNEL: https://www.cnel.it/archivio-contratti

Il LUL richiede anche registrazione delle presenze: ore lavorate, straordinari, assenze, ferie e
riposi. La sola grafica non lo rende ufficiale: modalita di tenuta, sequenza, inalterabilita,
eventuale autorizzazione/comunicazione e conservazione fanno parte della conformita.

- Art. 39 DL 112/2008: https://www.normattiva.it/uri-res/N2Ls?urn%3Anir%3Astato%3Adecreto.legge%3A2008-06-25%3B112~art39%21vig=
- INAIL, Libro Unico del Lavoro: https://www.inail.it/portale/assicurazione/it/Datore-di-Lavoro/Impresa-con-dipendenti-industria-artigianato-terziario-altre-attivita/libri-regolamentari-e-comunicazione-rapporti-di-lavoro-impresa-con-dipendenti/libro-unico.html

Per la malattia il datore consulta l'attestato, non la diagnosi. Dal 2026 il flusso UniEmens usa
il calendario giornaliero e il collegamento all'evento/certificato; l'informazione di malattia e
un dato relativo alla salute e deve avere accesso ristretto.

- INPS, attestati telematici: https://www.inps.it/it/it/dettaglio-scheda.it.schede-servizio-strumento.schede-servizi.consultazione-degli-attestati-di-malattia-telematici.html
- INPS, nuove modalita UniEmens 2026: https://www.inps.it/it/it/inps-comunica/notizie/dettaglio-news-page.news.2026.03.indennit-di-malattia-le-nuove-modalit-del-flusso-uniemens.html
- Garante Privacy, dati dei lavoratori: https://www.garanteprivacy.it/web/guest/home/docweb/-/docweb-display/docweb/1364939

## Gap prioritari di PaghePro

1. **P0 - Cartellino giornaliero.** Data, ore ordinarie, causale per segmento, riferimento/PUC
   separato, stato di approvazione, controllo sovrapposizioni e quadratura con orario teorico.
2. **P0 - Motore versionato per CCNL.** Maturazione ferie/ROL, malattia, infortunio,
   imponibilita e voci speciali devono provenire da regole DB con anno, CCNL e fonte; nel motore
   restano ancora valori cablati da eliminare.
3. **P1 - Chiusura e snapshot.** Stati bozza/controllata/validata/chiusa, audit, confronto col mese
   precedente, snapshot immutabile di anagrafiche/voci/parametri, codice CNEL e firma/timbro.

## Scenario di accettazione P0

Un rapporto con calendario noto registra, su date non sovrapposte, 10 giorni lavorati, 5 di
malattia collegati a un evento, 5 di ferie e 2 di assenza non retribuita. La diagnostica deve
bloccare giorni scoperti, sovrapposizioni e causali incomplete. Dopo la chiusura, cedolino, PDF,
LUL e UniEmens devono usare lo stesso snapshot e mantenere isolamento per tenant.

## Stato consegnato in questa tranche

- Tutte le causali mensili gia supportate dal motore sono ora modificabili dal cruscotto Presenze.
- Il ricalcolo non azzera piu ferie, malattia o permessi gia archiviati.
- Conferma esplicita e validazioni server precedono l'elaborazione.
- Malattia e infortunio stornano le giornate ordinarie prima di applicare le componenti dedicate.
- Il PDF mostra il riepilogo del cartellino ed e marcato `BOZZA DA VALIDARE`.

Questa tranche non completa i tre gap sopra e non rende il cedolino certificato.
