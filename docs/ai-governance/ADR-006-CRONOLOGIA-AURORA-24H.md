# ADR-006 — Cronologia Aurora cifrata per 24 ore

- **Stato:** approvato per implementazione
- **Data:** 4 settembre 2026
- **Decision owner:** utente/proprietario del prodotto, con richiesta esplicita `/goal`
- **Ambito:** continuità della chat Aurora fra pagina, reload e schede di lavoro

## Contesto

Aurora era stateless lato server e salvava fino a venti conversazioni in chiaro nel
`localStorage`, usando una chiave per solo tenant. Il browser rimandava la storia e il backend
eliminava le risposte `assistant`, perciò il contesto si perdeva e due utenti dello stesso studio
potevano condividere involontariamente la cronologia locale.

## Decisione

Introduciamo un workspace recente, separato da `AiRun`, con queste proprietà:

1. opt-in esplicito per tenant e utente, disattivato per default;
2. transcript cifrato AES-256-GCM con AAD legata a tenant, utente, conversazione, record e campo;
3. scope immutabile `Generale`, `Azienda`, `Rapporto` o `Progetto`, con periodo opzionale;
4. turni idempotenti identificati dal client e sequenza autorevole del server;
5. TTL assoluto di 24 ore dalla creazione, hard-delete su scadenza, eliminazione o opt-out;
6. il server costruisce la storia; il browser non è la fonte autorevole;
7. `D3A`, `D3B`, `D4`, allegati, selezioni, prompt di sistema, output tool, lease e token non sono
   persistiti;
8. nessun import automatico della cronologia locale precedente, perché non prova l'identità
   dell'autore.

La chiusura di una scheda nasconde soltanto la conversazione; il comando «Elimina» la distrugge.
Una risposta tardiva non può ricreare una conversazione cancellata. Cambiare azienda o lavoratore
apre un nuovo filo e non rietichetta mai i messaggi esistenti.

## Conseguenze

- Reload, cambio pagina e riapertura dalla cronologia mantengono il lavoro recente.
- Conversazioni diverse possono procedere in parallelo; lo stesso turno non viene duplicato.
- I contenuti recenti esistono nel database per non oltre 24 ore e devono essere inclusi nelle
  verifiche di cifratura, isolamento e cancellazione.
- Memoria oltre 24 ore, pin permanente o sintesi automatiche non sono autorizzati da questa ADR.
