# G0 — Purpose code e GDPR/AI Governance Pack

> Versione: 15 luglio 2026 · Stato: **BOZZA IN ATTESA DI APPROVAZIONE**
> Le basi giuridiche, le retention e lo screening DPIA/AI Act sono **proposte tecniche DA
> VALIDARE con DPO/legale e Consulente del Lavoro** prima di qualsiasi pilota con dati reali
> (piano §16). Un purpose senza record approvato **blocca build/release** delle funzioni AI
> che lo usano (Gate G0).

## 1. Come si legge

Ogni esecuzione AI dovrà dichiarare un `purposeCode` di questa lista. Il purpose determina:
risorse ammesse (dal [catalogo](G0-CATALOGO-RISORSE.md)), classi dati massime, operazioni massime,
modalità, e il registro GDPR collegato. Il modello non può creare o modificare purpose (piano §3.4).

Titolare del trattamento: lo studio/impresa utente (tenant). PaghePro nel profilo personale è
strumento locale del titolare; nel profilo SaaS il fornitore agisce da responsabile ex art. 28 —
inquadramento da confermare con DPO. Nessun dato va in training (piano §3.2).

## 2. Elenco dei purpose code (v1)

| # | purposeCode | Finalità in parole semplici | Classi max | Operazioni max | Modalità |
|---|---|---|---|---|---|
| 1 | `ONBOARDING_AZIENDA` | leggere visura/documenti e proporre l'anagrafica azienda | D1 | read, draft | strict, guided |
| 2 | `ONBOARDING_DIPENDENTE` | leggere UNILAV/contratto e proporre dipendente+rapporto | D2 | read, draft (write G5) | guided |
| 3 | `PREPARAZIONE_MESE_PAGHE` | raccogliere presenze/variabili e preparare il mese | D2 | read, draft (write G5) | guided |
| 4 | `SPIEGAZIONE_CALCOLO` | spiegare cedolino/F24/liquidazione con fonti e periodo | D0-D2 | read | strict, guided |
| 5 | `CONTROLLO_ANOMALIE_PAGHE` | trovare incoerenze e dati mancanti nelle paghe | D0-D2 | read, draft | guided, scheduled (G6) |
| 6 | `GS_MALATTIA_INFORTUNIO` | usare la terna malattia/infortunio per la paga del lotto | **D3A** | read, draft su campi minimi | strict + `GS-W` |
| 7 | `ADEMPIMENTO_TELEMATICO` | preparare/controllare F24, CU, 770, LIPE, UniEmens, INAIL e leggere i diagnostici ufficiali | D0-D2 | read, draft | guided |
| 8 | `SCADENZE_PROMEMORIA` | tenere scadenze e preparare testi di promemoria | D1 | read, draft (bozze messaggi: MAI invio) | guided, scheduled (G6) |
| 9 | `REGISTRAZIONE_CONTABILE` | classificare documenti e proporre registrazioni/riconciliazioni | D1 | read, draft (write G5) | guided, workspace |
| 10 | `CHIUSURA_BILANCIO` | preparare chiusure e bozza bilancio con quadratura | D1 | read, draft | guided, workspace |
| 11 | `LETTURA_DOCUMENTO` | leggere/riassumere un documento professionale importato | classe della fonte | read, draft | strict, guided |
| 12 | `PRATICA_ASSISTITA` | compilare pratiche ComUnica/AA7-10 da testo o documenti | D1-D2 | read, draft | guided |
| 13 | `REPORT_INTERNO` | cruscotti, scostamenti e report spiegati (solo dati interni) | D1 | read, draft | workspace read-only |

Esclusi per costruzione (nessun purpose esiste, quindi DENY): decisioni sui lavoratori, ranking,
invii esterni, pagamenti, firma, cancellazioni definitive (piano §10.1; gli invii avranno un
piano G8 separato con purpose `GX_*` dedicati).

## 3. Governance record per purpose (schema + bozze)

Schema del record (uno per purpose, versionato con il purpose):

```yaml
purposeCode:
titolare: tenant (studio/impresa)        # ruoli PaghePro da confermare con DPO
base_art6:                               # proposta tecnica DA VALIDARE
condizione_art9:                         # solo D3A
categorie_interessati:
categorie_dati:
necessita_proporzionalita:               # perché servono proprio quei dati
destinatari: nessuno esterno             # profilo personale: output resta interno
retention: rinvio al pacchetto GDPR-retention (non ancora definito) + TTL memorie del piano §8
diritti: accesso/rettifica/cancellazione via funzioni prodotto (anonimizza già presente)
informativa: da aggiornare prima del pilota
ropa: voce da inserire nel registro trattamenti del titolare
screening_dpia_aiact:                    # sì/no + motivo
approvatore: fondatore + professionista competente
```

### Bozze compilate (sintesi; stessa struttura per tutti)

**Comuni a tutti i purpose**: destinatari esterni = nessuno (l'AI non invia nulla); retention dei
derivati = minimo fra TTL memoria (§8.2), retention fonte e revoca; diritti esercitabili con le
funzioni già presenti (rettifica record, `POST /api/dipendenti/{id}/anonimizza`); nessun
trasferimento extra-UE nel profilo locale (modello on-device); log AI senza payload (§7.3).

| purposeCode | base art. 6 proposta | art. 9 | Screening DPIA/AI Act (proposta) |
|---|---|---|---|
| ONBOARDING_AZIENDA | b) contratto / f) leg. interesse organizzativo | — | DPIA no; AI Act: non alto rischio (assistenza compilazione, decisione umana) |
| ONBOARDING_DIPENDENTE | b) contratto + c) obblighi lavoro | — | DPIA screening sì (dati dipendenti su scala) → esito atteso: non necessaria nel profilo mono-operatore, DA CONFERMARE |
| PREPARAZIONE_MESE_PAGHE | c) obbligo legale (adempimenti retributivi) | — | come sopra |
| SPIEGAZIONE_CALCOLO | b)/c) | — | DPIA no |
| CONTROLLO_ANOMALIE_PAGHE | c) + f) qualità dell'adempimento | — | DPIA screening sì: nessuna decisione automatizzata (art. 22 non attivato: output = segnalazione) |
| GS_MALATTIA_INFORTUNIO | c) | **art. 9.2.b** (obblighi in materia di lavoro e sicurezza sociale) — DA VALIDARE | DPIA **sì** (categoria particolare): campi minimi, niente memoria durevole, niente confronti (piano §5.5) |
| ADEMPIMENTO_TELEMATICO | c) | — | DPIA no; evidenze = controlli ufficiali |
| SCADENZE_PROMEMORIA | b)/f) | — | DPIA no; invio SEMPRE umano |
| REGISTRAZIONE_CONTABILE | b)/c) contabilità | — | DPIA no |
| CHIUSURA_BILANCIO | c) | — | DPIA no |
| LETTURA_DOCUMENTO | dipende dalla fonte: b)/c)/f) | possibile → allora richiede grant `GS` | quarantena decide la classe PRIMA del purpose |
| PRATICA_ASSISTITA | b) | — | DPIA no |
| REPORT_INTERNO | f) controllo di gestione | — | DPIA no; vietati KPI per-lavoratore nominativi senza purpose paghe |

**Policy `D3A` separata (richiesta dal gate):** unico purpose ammesso `GS_MALATTIA_INFORTUNIO`;
`legalBasisRef` = record qui sopra; campi = SOLO terna malattia/infortunio del lotto S1/S2;
vietato qualunque uso D3A dentro gli altri purpose (il broker deve rifiutare).
**Policy `D3B`:** non esiste alcun purpose → tutto negato.

## 4. Vincoli di enforcement che discendono da questo pack

1. purpose assente/scaduto/non approvato ⇒ nessun grant emesso (build/release bloccate per le
   feature che lo richiedono);
2. un grant può solo RESTRINGERE ciò che il purpose ammette (piano §6.2);
3. cambio di finalità a metà lavoro ⇒ nuovo preflight e nuovo grant (anti purpose-laundering,
   vedi threat model);
4. l'audit registra `purposeCode` + `governanceRecordId` su ogni run (piano §7.3);
5. questo file è versionato in git: ogni modifica = nuova versione del record, la vecchia resta
   per audit ma non autorizza nuovi run (piano §6.2).

## 5. Caselle del Gate G0 coperte qui

- [x] purpose code ammessi definiti;
- [x] `GDPR/AI Governance Pack` bloccante per ogni purpose (bozza);
- [x] titolare, basi art. 6, condizione art. 9, categorie, necessità, destinatari, retention
      (rinvio esplicito), diritti, informativa, ROPA, screening DPIA/AI Act, approvatore;
- [x] policy `D3A` e `D3B` scritte separatamente;
- [ ] **validazione DPO/legale + CdL** (bloccante per il pilota, non per proseguire G1/G2 tecnici);
- [ ] approvazione del fondatore.
