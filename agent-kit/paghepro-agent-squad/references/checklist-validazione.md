# Checklist Validazione

Usare prima di dichiarare completato un lavoro PaghePro.

## Tecnica

- Build backend: `cd backend && dotnet build Paghe.slnx -c Release`.
- Test backend: `cd backend && dotnet test Paghe.slnx -c Release`.
- Build frontend se toccato: `cd frontend && npm run build`.
- Nuovo codice coperto da test proporzionati al rischio.
- Nessuna modifica non richiesta a file o refactor estranei.
- File condivisi riletti prima dell'edit finale.

## Dominio Paghe/Fisco

- Importi, aliquote e percentuali usano `decimal`, mai `float` o `double`.
- Ogni query persistita filtra per `tenantId`.
- Parametri normativi caricati da DB versionato per anno; niente costanti normative hardcoded.
- Output fiscali/paghe/telematici mostrano stato `bozza da validare` finche manca controllo ufficiale.
- Fonti ufficiali consultate per regole nuove, annuali o incerte.

## GDPR/Sicurezza

- Dati sensibili cifrati o motivazione documentata.
- Audit log previsto per accessi/modifiche rilevanti.
- Ruoli e permessi rispettati.
- Retention/cancellazione considerate quando si trattano dati personali.
- Nessun segreto in codice o repository.

## Ufficiale/Professionale

- F24/CU/LIPE/770: controllo Agenzia Entrate quando disponibile.
- UniEmens: validatore/software INPS quando disponibile.
- INAIL: dati da basi di calcolo/tassi ufficiali o inseriti dal professionista.
- Fatture: controlli SdI e canale/provider ufficiale per invio reale.
- Cedolini/calcoli: validazione da consulente del lavoro prima di vendita o uso reale.

## Documentazione Di Stato

- Aggiornare `PIANO.md` solo se una casella e davvero completata.
- Aggiornare `CLAUDE.md` "Fatto" solo con risultati verificati.
- Segnalare azioni utente `🧑` e attese `⏳` una alla volta, in linguaggio semplice.
