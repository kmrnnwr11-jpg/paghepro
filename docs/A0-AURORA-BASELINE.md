# A0 — Baseline riconciliata per Aurora agente

> Data ricognizione iniziale: **18 agosto 2026**  
> Ultima verifica tecnica: **22 agosto 2026**  
> Stato: **chiusa — checkpoint Git creati il 22 agosto 2026** dopo la richiesta del fondatore di
> proseguire il piano Aurora: baseline corrente confermata, otto commit funzionali secondo
> l'ordine di §4 (pacchetti; B0; B1; B2; B3 in due parti; B4; documentazione), file misti separati
> per hunk, artefatti di §5 non versionati.

## 1. Punto di partenza verificato

- `HEAD`, `main` e `origin/main` coincidono sul commit `b818a2c`.
- Non esistono commit o stash successivi attribuibili a Fable.
- All'inizio della ricognizione il working tree conteneva **85 file tracciati modificati** e
  **53 file non tracciati reali** (138 percorsi; `git status` compatta alcune cartelle).
- Nessun file era in stage. La baseline non poteva quindi essere ricostruita in modo affidabile
  dalla sola cronologia Git.
- La classificazione qui sotto è **funzionale**, non attribuisce la paternità delle modifiche.
- Dopo riconciliazione, correzioni e nuovi test il working tree contiene **90 file tracciati
  modificati** e **76 file non tracciati** (166 percorsi), sempre con zero file in stage. La
  crescita rispetto ai 138 percorsi iniziali è attribuita qui sotto; non è stata confusa con la
  baseline lasciata da Fable.

## 2. Lotti funzionali riconosciuti

### B0 — Prerequisito cedolino e schema

Il broker Aurora legge lo stato del cedolino introdotto dal nuovo workflow; questo lotto deve
precedere il Governor:

- workflow `Bozza -> Controllata -> Validata -> Chiusa`;
- snapshot e PDF cifrati, firma/timbro e blocco delle modifiche;
- stato esplicito `PreWorkflow` per lo storico precedente, contabilizzabile ma mai presentato
  come controllato, validato o definitivo;
- PDF professionale, calendario, CNEL, ANF e riporti ferie/permessi;
- migrazioni SQLite/PostgreSQL `OrdineVociCedolino`, `WorkflowCedolinoImmutabile`,
  `ResiduiRapportoEAssegniFamiliari`, `ClassificazioneCedoliniPreWorkflow` e
  `VincoloTenantVociCedolino` e `VincoliTenantAnagrafiche`, con snapshot EF allineate.

Correzioni di riconciliazione già applicate:

- gli storici vengono backfillati a `PreWorkflow`, non a `Bozza` o `Chiusa`;
- tutti i consumer downstream usano il predicato centralizzato
  `Chiusa || PreWorkflow`, lasciando esclusi gli stati in lavorazione;
- lo storico è immutabile, non transitabile e scaricabile solo come ricostruzione non definitiva;
- snapshot e PDF possono essere inseriti soltanto nella stessa transazione della transizione
  prevista; evidenze orfane, versioni saltate e transizioni senza evidenze sono negate;
- il prospetto non si qualifica più falsamente come Libro unico del lavoro;
- le fixture workflow richiedono un calendario completo come il servizio di produzione;
- le quattro colonne ANF/residui hanno migrazioni additive e default semantici;
- il database ibrido realmente osservato (storico fermo al workflow ma colonne già presenti)
  converge senza doppie colonne: preserva i valori, normalizza gli stati ANF vuoti, classifica
  lo storico e installa il vincolo composito `(TenantId, CedolinoId)` sulle voci;
- record orfani o payload cifrati associati a un tenant discordante fermano la migrazione senza
  registrarla falsamente come applicata;
- l'adozione legacy usa una sentinella durevole e registra lo storico in modo atomico; riprende
  soltanto da un prefisso esatto, rifiuta indici parziali e colonne SQLite sconosciute invece di
  rischiare perdita dati;
- FK composite legano `Rapporto -> Dipendente`, `DURC -> Azienda` e
  `Promemoria -> Azienda` al `TenantId`; orfani, mismatch e semantiche `ON DELETE` errate sono
  fail-closed anche negli schemi manuali o parziali.

### B1 — Provider e chat fail-closed

- `AiChatClient`, endpoint chat ed estrattori compatibili;
- `LocalSecretBoundaryFilter` e test API;
- configurazione predefinita `Provider=Spento`, `SoloLocale=true`, nessun redirect/proxy;
- chat senza strumenti, `tool_choice=none`, tool-call spontanee ignorate;
- endpoint legacy `/api/ai/chat/azione` sempre `409` e servizio legacy non registrato.

### B2 — Governor e primo broker deterministico

- catalogo capability, policy finalità/risorse/operazioni, grant e lease firmata;
- owner, tenant, scope, quantità server-side, quota senza overflow e revoca atomica;
- proiezioni minimizzate senza dati salute;
- filtro e proiezione Dipendenti eseguiti nel SQL: un riferimento legacy cross-tenant non viene
  materializzato né decifrato prima di essere escluso;
- controllo mensile cedolini in sola lettura, lotto visuale massimo 10;
- test di revoca fra query e risposta, isolamento tenant e audit.

### B3 — Pannello Aurora e selezione monouso

- pannello globale desktop/mobile e contesto della sezione;
- selezione massima 4.000 caratteri, monouso, single-range e blocco aree private;
- preflight legato ad azienda+mese, emissione/revoca mandato e trasparenza dei dati letti;
- risultati nascosti immediatamente al cambio scope;
- copy veritiera: il verticale corrente è un **controllo deterministico**, non un agente attivo.

### B4 — Configurazione deploy e documentazione

- `render.yaml` mantiene provider e gate spenti;
- `CHAT-AI-DEEPSEEK.md`, `PIANO-AURORA-AGENTE.md`, `PIANO.md` e `CLAUDE.md` descrivono
  il checkpoint governato e distinguono ciò che è ancora da implementare.

## 3. File misti: non mettere in stage per intero alla cieca

Questi file contengono modifiche appartenenti a più lotti e vanno revisionati per hunk:

- `backend/src/Paghe.Api/Program.cs` — cablaggio Aurora e workflow cedolino;
- `backend/src/Paghe.Persistence/Sicurezza/RuoloPolicy.cs` e relativo test — Governor e workflow;
- `frontend/src/api.ts` — Aurora, workflow, ANF e residui;
- `frontend/src/pages/Presenze.tsx` — ANF e marker privacy Aurora;
- `PIANO.md` e `CLAUDE.md` — Aurora, GDPR e cedolino/PDF;
- entità, `PagheDbContext` e snapshot EF — workflow, ANF/residui e cifratura.

## 4. Dipendenze e ordine dei checkpoint proposto

1. **Schema + workflow cedolino + regressioni payroll** (`B0`).
2. **Provider/chat fail-closed** (`B1`).
3. **Governor + broker + RBAC** (`B2`; dipende dallo stato cedolino di B0).
4. **Pannello e contesto frontend** (`B3`; dipende dalle API di B1/B2).
5. **Configurazione deploy fail-closed**.
6. **Documentazione**, aggiornata soltanto dopo i test completi.

Ogni checkpoint deve compilare e avere test verdi da solo. Se la separazione per hunk rompe una
migrazione o lo snapshot EF, il lotto schema resta atomico: non si forza un commit artificialmente
piccolo a scapito della riproducibilità.

## 5. File deliberatamente esclusi dai checkpoint

- `output/aurora/` contiene immagini storiche non affidabili come evidenza attuale;
- i PDF/PNG di concept sotto `output/` (circa 18 MB) non sono referenziati dal prodotto;
- `Aurora-assistente-AI-PaghePro.docx` è un brief storico con conteggi e gate superati;
- nessuno di questi file va cancellato senza conferma, ma non va versionato automaticamente.

## 6. Verifiche concluse il 22 agosto 2026

- build backend Release: **0 errori**; restano due `NU1900` perché il feed online degli advisory
  NuGet non era raggiungibile;
- suite backend completa: **843/843** test verdi, incluso il fuzz da 10.000 richieste ostili;
- frontend: **27/27** test verdi e build Vite/TypeScript riuscita (solo avviso bundle da 616 kB);
- EF: nessuna modifica di modello senza migrazione, sia per SQLite sia per PostgreSQL;
- smoke Production su una copia isolata del database reale: 12 migrazioni applicate, tre
  cedolini classificati `PreWorkflow`, tre stati ANF `DaVerificare`, quattro relazioni tenant
  a due colonne (`CedolinoVoce`, `Rapporto`, `DURC`, `Promemoria`) e
  `foreign_key_check = 0`;
- nello stesso smoke `/health` è verde, gli endpoint AI anonimi rispondono `401`, provider e chat
  risultano spenti, la chat restituisce zero azioni e l'endpoint legacy resta `409`;
- l'hash SHA-256 del `paghe.db` originale è rimasto invariato prima e dopo la prova.

## 7. Gate ancora aperti e avanzamento successivo

- **A0 Git:** risolto il 22 agosto 2026 — checkpoint locali creati con la conferma del fondatore;
  nessun file è stato scartato.
- **A1 modello:** provider/DPA, trasparenza e accensione in sviluppo richiedono la scelta del
  fondatore; Render resta spento.
- **Retention:** snapshot e PDF sono cifrati ma esclusi fail-closed dal motore di cancellazione.
  Durata, decorrenza e base giuridica del fascicolo cedolino devono essere approvate prima di
  aggiungere una categoria distruttiva.
- **Aurora agente:** dopo A0 è stato completato e verificato il verticale A2 read-only:
  registry dinamico, SessionLog, lease collegata alla chat e cinque letture broker. Lo scaffold
  cifrato delle proposte è presente, ma A3 resta chiuso: nessuna scrittura è offerta o
  confermabile finché non sono provati riverifica grant/policy, applicazione transazionale,
  idempotenza crash-safe e diff end-to-end. Navigazione e consigli restano A4-A5.

## 8. Criterio di uscita da A0

A0 si chiude solo quando:

1. il fondatore conferma di mantenere la baseline corrente (**fatto il 22 agosto 2026**);
2. i lotti sopra vengono committati senza includere gli artefatti esclusi (**fatto**);
3. build backend/frontend, suite completa, migrazioni SQLite/PostgreSQL e smoke Production
   fail-closed risultano verdi (**soddisfatto**: 843/843 backend e 27/27 frontend riverificati
   prima dei checkpoint);
4. `PIANO-AURORA-AGENTE.md` e `CLAUDE.md` riportano conteggi ed esiti finali reali
   (**soddisfatto**).
