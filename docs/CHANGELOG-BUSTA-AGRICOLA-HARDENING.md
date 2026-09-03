# Correzione e hardening della busta agricola OTD — 3 settembre 2026

Intervento sui 10 bug emersi dall'analisi delle 17 buste GOOD JOBS SRLS di luglio 2026
(CPL operai agricoli e florovivaisti Piacenza 2024-2027 + tabella E.B.A.P. OTD dal
01/06/2026). Riferimenti di codice tra parentesi.

## Correzioni

1. **Giorni di detrazione dal calendario del rapporto** (`ValidatoreBustaAgricola.GiorniDetrazioneNelMese`):
   `min(fine mese, cessazione) − max(inizio mese, assunzione) + 1`. Il server calcola il valore
   dalle date del rapporto e corregge l'input difforme segnalandolo (caso KHAN Md Rinku: 5 → 25).
   La stessa regola alimenta il ragguaglio a giorni della detrazione annua (÷ 365).
2. **Qualifica derivata, mai un default** («Operaio agricolo — Area/Livello: …»): per un rapporto
   OTD/OTI col CCNL operai agricoli la qualifica non arriva più dalla scheda (dove «Impiegato» era
   un default di modello); un tipo manodopera non riconoscibile blocca la generazione.
3. **Trasferte con mapping esplicito giorni/importo**: quantità = giorni, base = importo
   giornaliero; le inversioni palesi (giorni > 31 o > giornate lavorate, con importo plausibile
   come giorni) vengono scambiate con AVVISO; i gruppi con lo stesso importo si stampano in una
   riga sola. I casi ambigui non si toccano.
4. **Validatore pre-generazione** (`ValidatoreBustaAgricola.Valida`): trasferte oltre le giornate
   (ERRORE), giornate oltre il calendario del rapporto (ERRORE), ore/giornata fuori 3–10 (AVVISO)
   o diverse dalle ore giornaliere del contratto (AVVISO), trasferta < 5 € (AVVISO) o oltre il
   limite di esenzione 46,48 € (ERRORE, con quota eccedente resa imponibile), cessazione
   incoerente (ERRORE), ore/giornate dichiarate a metà (ERRORE), matricola INPS mancante (AVVISO
   in bozza, ERRORE alla validazione). Le segnalazioni viaggiano nella risposta API, in calce al
   PDF di bozza e nel riepilogo mensile.
5. **FIMI: una sola costante per calcolo ed etichetta**: l'aliquota configurata sul DB guida
   entrambe e le etichette stampano tutti i decimali significativi. Sul DB di sviluppo il FIMI
   Piacenza passa da 0,3333 % (≈1/300) a **0,33 %**; l'aliquota totale c/lavoratore stampata è la
   somma delle costanti (9,42 %).
6. **Reddito annuo presunto tracciato**: catena richiesta → scheda del rapporto
   (`Rapporti.RedditoPresuntoAnnuo`, PATCH dalla scheda dipendente) → default aziendale
   (`Aziende.RedditoPresuntoAgricoloAnnuo`) → stima dal contratto (imponibile del mese × mesi
   previsti), con `origineRedditoPresunto` su busta e API. Trattamento integrativo (art. 1
   DL 3/2020, 1.200 €/anno ≤ 15.000 € di reddito) applicato SOLO con capienza (IRPEF lorda annua
   presunta > detrazione annua art. 13 c. 1), rapportato ai giorni di detrazione.
7. **Stato dell'ELABORAZIONE mensile, non della busta** (`ElaborazioniAgricole`): le buste si
   archiviano sempre come bozza; la validazione avviene su
   `POST /api/aziende/{id}/agricoltura/elaborazioni/{anno}/{mese}/valida`, ricalcola ogni busta
   dagli input archiviati, fallisce con ERRORI aperti o numeri cambiati, e ristampa i PDF con
   «Prospetto controllato e validato il … » in calce. Riarchiviare una busta fa decadere la
   validazione del mese.
8. **Arrotondamento recuperato il mese successivo**: l'arrotondamento anticipato resta salvato
   sulla busta (`ArrotondamentoDelMese`) e la busta successiva lo recupera in automatico con la
   voce `AGR_ARROT_REC` prima del nuovo arrotondamento. L'ultimo cedolino paga l'esatto; un
   arrotondamento non più recuperabile (lavoratore cessato) è segnalato nel riepilogo mensile.
9. **Deducibilità per contributo** (`ContributoAgricolo.DeducibileIrpef`): FAP deducibile, FIMI e
   CAC no (default); la percentuale stampata è quella effettiva calcolata dai flag. Il valore
   osservato di Cuneo 2025 (7,80 %) resta un override esplicito finché il consulente non lo
   spiega.
10. **Cessazioni difformi a parità di assunzione**: AVVISO nel riepilogo/elaborazione quando, con
    la stessa data di assunzione, una cessazione differisce da quella prevalente (caso MIA Md
    Amran 30/08 vs 31/08).

Trasversali: un solo motore per tutti gli importi (invariante «netto = competenze − trattenute +
arrotondamento» verificata al centesimo su tutte le 17 buste fixture); aritmetica `decimal`
ovunque; **riepilogo mensile aziendale** JSON/CSV/PDF
(`GET /api/aziende/{id}/agricoltura/riepilogo/{anno}/{mese}?formato=`); PDF della busta e del
riepilogo in **bianco e nero**; salario provinciale, terzo elemento e layout a 6 sezioni
invariati. Migrazioni `HardeningBustaAgricola` per SQLite e PostgreSQL.

## Da far confermare al Consulente del Lavoro

| # | Punto | Valore attuale | Domanda |
|---|-------|----------------|---------|
| 1 | **Aliquota FIMI** | 0,33 % (configurata a DB, modificabile senza toccare codice) | L'art. 53 del CPL Piacenza 2024-27 riporta un valore che il vecchio calcolo interpretava come 0,3333 % (1/300). Qual è l'aliquota corretta da applicare e stampare? |
| 2 | **Deducibilità FIMI/CAC** | FIMI e CAC **non** dedotti dall'imponibile IRPEF; si deduce solo il FAP 8,84 % | I contributi di assistenza contrattuale (artt. 53-54 CPL) vanno considerati oneri deducibili ex art. 51 c. 2 lett. a) TUIR? I flag sono configurabili per contributo. |
| 3 | **Reddito presunto di default** | Nessun default aziendale impostato; per GOOD JOBS usati 5.000 € (15 lavoratori) e 6.000 € (2), ora salvati in scheda rapporto | Quale criterio usare per gli stagionali (stima dal contratto? soglia minima?) e quale valore di default aziendale impostare? |
| 4 | **Giorni di detrazione OTD** | Giorni di calendario del rapporto nel mese (regola generale art. 13 TUIR) | La busta reale INAZ di Cuneo (ott. 2025) esponeva 10 giorni con rapporto attivo tutto il mese: esiste una convenzione di settore diversa per gli OTD? |
| 5 | **Quota deducibile Cuneo 7,80 %** | Override osservato sulle buste reali, tuttora inspiegato | Perché la quota dedotta risulta inferiore al solo FAP 8,84 %? |
