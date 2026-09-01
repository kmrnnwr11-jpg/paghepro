# Netto concordato con conguaglio («busta dal netto»)

Richiesta del fondatore (1 settembre 2026): poter creare le buste partendo dal **netto
pattuito** col lavoratore, con un conguaglio che tenga il netto stabile mese per mese.
È la funzione «assegno netto garantito» dei software professionali. Questa specifica
definisce la versione corretta e vendibile; l'implementazione va fatta in una sessione
dedicata, con test.

## Cosa esiste già

`CalcoloInversoNetto` (Paghe.Core/Calcolo, 8 agosto 2026): dato il netto desiderato trova
il **superminimo individuale** che lo produce, perché minimo tabellare, scatti ed EDR sono
fissati dal CCNL e il superminimo è l'unica componente liberamente concordabile. Se il
netto chiesto è sotto quello del solo minimo contrattuale, il risultato è «non
raggiungibile»: non si forza. Oggi è un calcolo una tantum, non collegato al rapporto.

## Cosa si aggiunge

### Fase 1 — Netto concordato sul rapporto (motore ordinario)

- Campo `NettoConcordatoMensile` (nullable) su `RapportoLavoroEntity` + migrazioni per
  entrambi i provider; casella nella scheda dipendente con l'esito del calcolo inverso
  («raggiungibile con superminimo X / non raggiungibile»).
- A ogni elaborazione il motore ricalcola il superminimo necessario per il mese (le
  imposte cambiano con addizionali, conguagli, detrazioni): la differenza rispetto al
  superminimo base appare come voce **«Conguaglio netto concordato»** — una voce LORDA,
  tassata e contribuita normalmente, che agisce sul superminimo. Mai un importo netto
  aggiunto a mano.
- Nota fissa in busta: «Netto concordato: il lordo è determinato dal netto pattuito».

### Fase 2 — Variante agricola (compenso netto giornaliero/orario pattuito)

- In agricoltura la paga è ore × tariffa provinciale: qui il netto concordato è una
  **tariffa** («X € netti al giorno/ora») convertita nella tariffa lorda equivalente.
- Accettata solo se la tariffa lorda risultante è **≥ tariffa CPL** di provincia/livello;
  sotto il minimo → rifiuto con messaggio esplicito (stessa regola del motore ordinario).
- Le ore e le giornate restano SEMPRE un dato reale inserito dall'utente.

## Paletti non negoziabili

Stessa famiglia delle regole anti-errore già volute dal fondatore (mesi futuri bloccati,
niente busta prima dell'assunzione):

1. **Solo mese corrente e futuri.** La funzione NON ricostruisce buste di mesi passati
   partendo da somme già pagate: le buste vecchie di un cliente che cambia commercialista
   le consegna il commercialista precedente (obbligo di legge), non si reinventano.
2. **Il calcolo inverso muove solo superminimo/tariffa.** Mai le ore, mai le giornate,
   mai trasferte o assenze (regola dell'8 agosto 2026, confermata il 1° settembre).
3. **Mai sotto il minimo CCNL/CPL.** Netto non raggiungibile = dichiarato, non forzato.
4. **Trasparenza in busta.** La voce di conguaglio e la nota «netto concordato» sono
   sempre visibili: il consulente deve vedere che il lordo deriva dal netto pattuito.

## Casi d'uso

- Cliente nuovo da altro commercialista: si conosce il netto pattuito → si imposta il
  netto concordato e le buste **da questo mese in poi** escono da sole; i mesi passati
  arrivano dal vecchio commercialista.
- Stagionale agricolo con paga netta giornaliera pattuita (≥ minimo CPL).
- Preventivo al datore: «per dare N netti servono L lordi, costo azienda C».

## Test previsti

- Floor: netto sotto minimo → non raggiungibile, nessuna busta.
- Convergenza: netto raggiunto al centesimo su casi golden esistenti.
- Conguaglio: mese con addizionali a rate → il netto resta il pattuito e la voce di
  conguaglio quadra al lordo.
- Blocco retroattivo: periodo passato → rifiuto con messaggio.
- Agricolo: tariffa netta sotto CPL → rifiuto; sopra → busta con ore reali.

## Stato

Specifica scritta il 1° settembre 2026. **Fase 1 implementata lo stesso giorno**:
`NettoConcordatoMensile` + `NettoConcordatoDecorrenza` su `RapportoLavoroEntity`
(migrazioni `NettoConcordato` per entrambi i provider), impostazione dal
PATCH `/api/rapporti/{id}` (decorrenza retroattiva rifiutata, rimozione con
`rimuoviNettoConcordato`), applicazione in `CedoliniService` (calcolo inverso a ogni
elaborazione dal mese di decorrenza, voce di trasparenza `NETTO_CONC`, errore chiaro se
il netto è sotto il minimo). Nota di implementazione: in Fase 1 il superminimo del mese È
il valore trovato dal calcolo inverso (esposto nella voce `SUPERMIN`); la voce distinta di
conguaglio rispetto a un superminimo base arriverà se servirà davvero. Test: 6 casi in
`NettoConcordatoTests` (netto raggiunto, sotto-minimo rifiutato, decorrenza rispettata,
retroattivo rifiutato, voce in busta, PDF), suite completa 1.098/1.098 verde.
Restano: casella nella SPA (scheda dipendente) e Fase 2 agricola dopo la validazione
delle tabelle CPL col consulente.
