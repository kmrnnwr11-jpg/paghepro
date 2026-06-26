# Partita IVA, Camera di Commercio e contabilita IVA

Modulo per preparare una pratica di apertura partita IVA e gestire una prima base di
contabilita IVA. Il software prepara dati, checklist e calcoli; l'invio telematico reale
richiede canali ufficiali, credenziali abilitate o un intermediario.

## Fonti ufficiali da seguire

- Agenzia Entrate: modello [`AA9/12`](https://www.agenziaentrate.gov.it/portale/schede/istanze/aa9_11-apertura-variazione-chiusura-pf/modello-e-istr-pi-pf) per imprese individuali e lavoratori autonomi.
- Agenzia Entrate: modello [`AA7/10`](https://www.agenziaentrate.gov.it/portale/schede/istanze/aa7_10/modello-aa7_10) per soggetti diversi dalle persone fisiche.
- Registro Imprese: [Comunicazione Unica d'Impresa](https://www.registroimprese.it/comunicazione-unica-d-impresa) per Registro Imprese, Agenzia Entrate,
  INPS, INAIL ed eventuale SCIA/SUAP.
- Agenzia Entrate: [comunicazione delle liquidazioni periodiche IVA](https://www.agenziaentrate.gov.it/portale/schede/comunicazioni/liquidazioni-periodiche-iva).

## Funzioni implementate

- Piano pratica apertura P.IVA in `Paghe.Core.PartiteIva`.
- Persistenza tenant-isolata delle pratiche in `PraticheAperturaPartitaIvaService`.
- Tracking degli adempimenti: Agenzia Entrate, ComUnica, Camera di Commercio/REA,
  INPS, INAIL, SUAP e setup contabilita.
- Calcolo liquidazione IVA vendite/acquisti in `CalcoloLiquidazioneIva`.
- Registro IVA acquisti persistito (`Spese`) e liquidazioni IVA salvate per azienda.

## Endpoint principali

Gli endpoint pratica richiedono Bearer token.

- `POST /api/pratiche/apertura-partita-iva/simula`
- `POST /api/pratiche/apertura-partita-iva`
- `GET  /api/pratiche/apertura-partita-iva`
- `GET  /api/pratiche/apertura-partita-iva/{id}`
- `PATCH /api/pratiche/apertura-partita-iva/{praticaId}/adempimenti/{adempimentoId}`
- `POST /api/contabilita/iva/liquidazione`
- `POST /api/aziende/{id}/spese`
- `GET  /api/aziende/{id}/spese`
- `POST /api/aziende/{id}/liquidazioni-iva`
- `GET  /api/aziende/{id}/liquidazioni-iva`
- `GET  /api/liquidazioni-iva/{id}`

## Esempio pratica commercio

```json
{
  "formaGiuridica": "ImpresaIndividuale",
  "regimeContabile": "Semplificato",
  "dataInizioAttivita": "2026-07-01",
  "denominazione": "Rossi Market",
  "codiceFiscale": "RSSMRA80A01F205X",
  "codiceAteco": "47.11.40",
  "descrizioneAttivita": "Minimarket",
  "indirizzoSede": "Via Roma 1",
  "cap": "20100",
  "comune": "Milano",
  "provincia": "MI",
  "attivitaCommercio": true,
  "haDipendentiPrevisti": true,
  "soggettaInail": true
}
```

Risultato atteso: modello `AA9/12`, Comunicazione Unica, Registro Imprese/REA,
Camera di Commercio, verifica INPS commercianti, SUAP/SCIA, INAIL se indicata
e setup contabilita.
