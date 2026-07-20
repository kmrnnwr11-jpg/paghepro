# Governance AI — Gate G0

Deliverable del Gate G0 («Inventario e threat model») del piano
[`../PIANO-AI-LIBERA-LIMITI-USO.md`](../PIANO-AI-LIBERA-LIMITI-USO.md), prodotti il 15 luglio 2026
dal censimento del codice reale.

| Documento | Contenuto |
|---|---|
| [`G0-CATALOGO-RISORSE.md`](G0-CATALOGO-RISORSE.md) | Censimento e classificazione (D0-D4/DX/CP) di tabelle, file, cache, export, endpoint e strumenti; effetti read/draft/write/external; matrice capability × modalità × gate |
| [`G0-PURPOSE-GOVERNANCE.md`](G0-PURPOSE-GOVERNANCE.md) | 13 purpose code ammessi + GDPR/AI Governance Pack per ciascuno (basi art. 6, condizione art. 9 per D3A, DPIA screening); policy D3A/D3B separate |
| [`G0-THREAT-MODEL.md`](G0-THREAT-MODEL.md) | 18 minacce modellate con contromisure e gate che le prova; rischi accettati |
| [`G1-RAPPORTO-FONDAMENTA.md`](G1-RAPPORTO-FONDAMENTA.md) | Stato del Gate G1 (16/07/2026): backup cifrato+restore e interruttore AI **provati**; cifratura campi e migrazioni parziali; guscio desktop da fare — gate APERTO; decisione fondatore: **opzione B, G2 anticipato senza modello** |

## Avanzamento G2 (primo blocco, 16/07/2026)

Costruito e provato il nucleo del **Usage Governor** nell'app attuale (emendamento §12 del piano):
`ProfessionalResourceCatalog` (30 risorse censite, default DENY), `AiUsagePolicyStore` versionato
(5 finalità), `AiScopeGrantEntity` + lease HMAC opaca, enforcement in `AiGrantService`
(tenant, nonce, scadenza, inattività, operazione, risorsa, classe, quota atomica), revoca +
«Arresta AI», audit append-only con **catena hash tamper-evident**, endpoint
`/api/ai/governor/*` (catalogo, preflight, grants, accesso, revoca, cosa-vede-l'AI, verifica
catena). Prove: 15 test dedicati (fra cui 25 accessi concorrenti su quota 10 → esattamente 10
consentiti; manomissione audit rilevata), suite completa 574/574 verde, ciclo HTTP reale
provato end-to-end su istanza pulita. **UI del mandato nella SPA fatta e provata nel browser**
(16/07): il pannello Aurora concede il mandato dal preflight, «Cosa vede l'AI» mostra il registro
del mandato e «Arresta» revoca e disabilita i controlli.

**Query broker + registro capability + fuzz (16/07)**: aggiunto `ProfessionalQueryBroker`
(`/api/ai/governor/query`) — permesso + righe minimizzate in un passo, con lo scope (azienda,
periodo) preso dal MANIFEST del mandato e non dal client; dell'IBAN esce solo il flag di
presenza. `AiCapabilityRegistry` (§9.5) esposto nel catalogo (6 capability; quelle che
richiedono un modello restano `unavailable` fino a G1+G3). Aurora ora legge SOLO dal broker
(verificato in browser: la lettura passa da `/query`, non dalle API dirette). Fuzz del §13:
**10.000 tentativi ostili** (cross-tenant, lease manomessa, firma dell'attaccante, mandato
revocato, spazzatura) → **0 accessi consentiti**, e il titolare legittimo continua a funzionare.

**Il gate G2 NON è chiuso**: restano i test property-based sistematici, la UI drawer completa e
la revisione di sicurezza dedicata. Nessun modello AI è collegato (vincolo G1 intatto).

Nome prodotto dell'assistente: **Aurora** (scelto dal fondatore il 16/07/2026 al posto di
«Copilot», marchio Microsoft). Il pannello SPA è `frontend/src/Aurora.tsx`; nei documenti di
piano «Copilot interno» resta come termine generico di architettura.

## Stato

**APPROVATO DAL FONDATORE il 16 luglio 2026** (caselle G0 spuntate nel piano). Resta aperta, come
condizione per il **pilota con dati reali** (non per il lavoro tecnico G1/G2):

1. validazione DPO/legale e Consulente del Lavoro delle parti GDPR (basi giuridiche, art. 9,
   retention, DPIA) in [`G0-PURPOSE-GOVERNANCE.md`](G0-PURPOSE-GOVERNANCE.md).

Regola permanente introdotta da G0: **ogni nuova tabella, endpoint, formato o dipendenza nasce
NEGATA all'AI** finché non viene censita nel catalogo con classe, effetti e purpose.

Nessun codice AI è autorizzato da questi documenti: l'implementazione parte con G1/G2 su
richiesta separata (piano §1.1).
