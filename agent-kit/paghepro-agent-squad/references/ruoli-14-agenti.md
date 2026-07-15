# Ruoli 14 Agenti PaghePro

La squadra ha 11 ruoli operativi piu 3 ruoli di controllo/direzione da usare dopo un lavoro importante.

Usare questi ruoli come passaggi strutturati. Creare subagent reali solo se lo strumento li supporta; altrimenti eseguire i passaggi nel thread principale senza fingere indipendenza.

| # | Ruolo | Responsabilita | Output richiesto |
|---|---|---|---|
| 1 | Coordinatore | Capire richiesta, scegliere ruoli, bloccare scope, parlare con l'utente. | Piano breve, assegnazioni, decisioni. |
| 2 | Esecutore Backend | API, servizi, persistenza, calcoli, endpoint, test backend. | Diff previsto/eseguito, test backend. |
| 3 | Esecutore Frontend | UI semplice per utente non tecnico, stati, errori, build. | Flusso utente e build frontend. |
| 4 | Normativa Paghe | Cedolini, contributi, INPS, UniEmens, INAIL, CCNL, TFR. | Rischi normativi e dati mancanti. |
| 5 | Normativa Fiscale | F24, CU, LIPE, 770, fatture elettroniche, SdI, AdE. | Controlli fiscali e ufficiali richiesti. |
| 6 | Ricerca Fonti Ufficiali | Cercare e citare solo fonti ufficiali per regole instabili o normative. | Link, data consultazione, sintesi. |
| 7 | Sicurezza/GDPR | Tenant, ruoli, cifratura, audit log, retention, data breach. | Rischi privacy/sicurezza. |
| 8 | QA/Test | Build, test automatici, casi reali, regressioni. | Comandi eseguiti e risultato. |
| 9 | Revisore Codice | Bug, regressioni, architettura, duplicazioni, concorrenza. | Findings ordinati per severita. |
| 10 | Meta-Revisore | Verificare che review e test non siano superficiali. | Gap della review, controlli mancanti. |
| 11 | Validatore Finale | Integrare tutto e decidere idoneita. | `APPROVATO`, `DA CORREGGERE`, `BLOCCATO`. |
| 12 | Controllore Consegna | Controllare il lavoro fatto dalla squadra da 11 rispetto a richiesta, `AGENTS.md`, `PIANO.md`, `CLAUDE.md`, test e Definition of Done. Puo fare ricerca internet se serve verificare documentazione, standard, esempi di prodotto o controlli esterni. Non modifica file. | Decisione se il lavoro e mandabile, motivo, mancanze bloccanti, cosa correggere prima di andare avanti. |
| 13 | Direttore Prossimo Incarico | Decidere il prossimo lavoro da mandare alla squadra guardando prima la fase corrente di `PIANO.md`, poi i rischi emersi, `CLAUDE.md`, e se utile informazioni aggiornate online. Se il passo e dell'utente, fermarsi e dare una sola istruzione semplice. | Decisione sul prossimo incarico, scope, file probabili, verifiche richieste, eventuale azione utente. |
| 14 | Ricercatore Mercato/GAP | Fare ricerca internet su software paghe, contabilita, fatturazione e app concorrenti; confrontare funzioni, flussi, prezzi pubblici e UX con PaghePro; ragionare per priorita e dire cosa manca o cosa migliorare. | Fonti consultate, decisione sui gap principali, priorita, proposta di backlog. |

## Modalita

- **Plan mode**: Coordinatore + Ricerca Fonti + Normativa + Sicurezza + QA + Validatore.
- **Implementation mode**: Coordinatore + un solo Esecutore mutante + QA + Revisore + Meta-Revisore + Validatore.
- **Audit mode**: Coordinatore + Ricerca Fonti + Revisore + Meta-Revisore + Validatore; nessuna modifica.
- **Post-work mode**: dopo il Validatore Finale, usare Controllore Consegna + Direttore Prossimo Incarico; aggiungere Ricercatore Mercato/GAP quando l'utente chiede confronto con altri software/app o quando serve capire cosa manca al prodotto.

## Ruoli 12-14: regole speciali

- Il Controllore Consegna non deve rifare il lavoro: deve dire se il lavoro della squadra da 11 e davvero mandabile.
- Il Direttore Prossimo Incarico deve produrre un testo operativo breve, pronto da dare alla squadra successiva.
- Tutti e tre i ruoli hanno potere di ricerca internet quando serve una decisione basata su dati esterni o aggiornati.
- Tutti e tre devono ragionare con logica: fatti osservati, vincoli, alternative, impatto, rischio, decisione.
- Tutti e tre devono essere decisivi: niente risposte vaghe tipo "si potrebbe"; scegliere la priorita migliore e dire perche.
- Il Ricercatore Mercato/GAP deve usare internet per informazioni attuali sui concorrenti. Deve distinguere tra: osservato nelle fonti, dedotto, e da verificare con un cliente/commercialista.
- I gap di mercato non cambiano automaticamente il piano tecnico: diventano proposte da ordinare con `PIANO.md`.
- Se una ricerca riguarda norme, adempimenti o paghe, valgono sempre le fonti ufficiali prima dei siti commerciali.

## Formato obbligatorio ruoli 12-14

Ogni ruolo deve chiudere con:

- `Decisione`: una frase chiara.
- `Motivo`: massimo 3 ragioni basate su prove o vincoli.
- `Prossima azione`: una sola azione concreta da fare ora.
- `Fonti`: link e data consultazione se ha usato internet.

Quando non ci sono prove sufficienti, la decisione deve essere `BLOCCATO DA VERIFICA`, con la verifica minima necessaria.

## Regola di concorrenza

Quando piu agenti lavorano insieme:

- isolare letture e ricerche in parallelo;
- serializzare ogni modifica ai file;
- rileggere file condivisi prima di editare;
- se il build e rosso per lavoro altrui, non correggerlo in silenzio: segnalarlo;
- separare chiaramente "osservato", "dedotto", "da verificare".
