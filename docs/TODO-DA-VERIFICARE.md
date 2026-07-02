# Da verificare con un Consulente del Lavoro (non inventato nel codice)

Elenco dei comportamenti che il software oggi NON calcola da solo perché richiederebbero
valori o regole normative che non sono verificabili qui. Meglio dichiararlo che inventare.
Aggiornato: 2 luglio 2026 (verifica coerenza calcoli, `CoerenzaAnnualeTests`).

## Motore cedolino (metodo presuntivo, base 12)

1. **Assunzione/cessazione a metà mese**: il motore calcola il mese pieno; la quota parte
   va oggi gestita manualmente con `OreAssenzaNonRetribuita` nel cartellino. Il criterio di
   proratizzazione (giorni di calendario vs giorni lavorati vs 26esimi) varia per CCNL e va
   deciso con il consulente prima di automatizzarlo.
2. **Tredicesima/quattordicesima nel cedolino**: i ratei sono calcolati e testati a parte
   (`MensilitaAggiuntive`, 1/12 per mese maturato), ma NON confluiscono automaticamente nel
   cedolino di dicembre/giugno: l'erogazione (mese, tassazione separata dal presuntivo base 12)
   va confermata per ciascun CCNL.
3. **Conguaglio di fine anno**: esiste il calcolo (`CalcolaConguaglioAnnualeAsync`, testato) ma
   non viene applicato automaticamente al cedolino di dicembre.
4. **Malattia/maternità/infortunio**: i giorni si registrano nel cartellino ma l'integrazione
   INPS/datore (carenza, percentuali per CCNL) non è calcolata.

## Documenti fiscali

5. **CU e 770 sono bozze di controllo** (totali dai cedolini archiviati, export CSV): il file
   telematico ufficiale richiede le specifiche annuali dell'Agenzia delle Entrate e l'invio via
   Entratel (vedi `PIANO-DICHIARAZIONI.md`).
6. **Codici tributo F24 paghe** (1001, 3802, 3848, DM10): valori di prassi diffusa, da
   confermare caso per caso (es. acconto/saldo addizionale comunale 3847/3848, addizionali in
   rate). La sezione INPS richiede la matricola reale nel formato ufficiale.

## Anagrafica comuni

7. **CAP dei comuni nati da fusione (2 luglio 2026)**: l'anagrafica `frontend/public/comuni.json`
   è stata allineata al dataset MEF 2026 (rimossi 15 comuni soppressi, aggiunti gli 8 nuovi
   M432–M439: Misiliscemi, Bardello con Malgesso e Bregano, Moransengo-Tonengo, Uggiate con
   Ronago, Sovizzo, Setteville, Santa Caterina d'Este, Castegnero Nanto). Il **CAP** dei nuovi
   comuni è lasciato vuoto perché non presente in una fonte verificabile nel repo: il campo CAP
   nel form resta da compilare a mano per questi 8 comuni. Le addizionali comunali NON ne
   risentono (usano il codice catastale, coperto dal dataset MEF).

## Cosa è invece verificato automaticamente (test)

- Coerenza incrociata su un anno intero: cedolini archiviati → CU → 770 → F24 mensili
  raccontano gli stessi numeri (IRPEF, addizionali, contributi), anche con part-time 50%
  e assunzione a luglio (`CoerenzaAnnualeTests`).
- Tutti gli importi persistiti sono al centesimo; netto = competenze − trattenute.
- Golden test su buste reali (INPS, IRPEF, minimi CCNL 2026, voci variabili per 5 CCNL).
