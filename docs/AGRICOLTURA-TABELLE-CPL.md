# Caricare le tabelle salariali agricole (contratti provinciali)

In agricoltura la paga **non** è nazionale: il CCNL operai agricoli e florovivaisti rinvia al
**contratto provinciale di lavoro (CPL)**, e la stessa qualifica vale cifre diverse a Cuneo e a
Piacenza. Per questo le tabelle sono versionate **per anno e per provincia**, e il software si
rifiuta di elaborare una busta per una provincia che non ha tabelle: applicare quelle di un'altra
provincia produrrebbe una busta sbagliata dall'aria giusta.

## Cosa c'è già

Una sola tabella, **Cuneo 2025**, ricavata da buste paga reali e verificata al centesimo
(vedi `docs/buste-reali/busta-05-agricoltura-otd-cuneo-2025.md`). Non è il CPL completo: contiene
la sola tariffa osservata in busta. Va sostituita con la tabella ufficiale appena disponibile.

## Formato del file (JSON)

```json
{
  "anno": 2026,
  "provincia": "CN",
  "aliquotaContributiDeducibili": 0.0780,
  "fonte": "CPL operai agricoli Cuneo 2026 — tabelle allegate all'accordo del ...",
  "tariffe": [
    { "livello": "AREA1_S", "descrizione": "Specializzato super",
      "salarioProvincialeOrario": 7.10000, "terzoElementoOrario": 2.77000 },
    { "livello": "COMUNE", "descrizione": "Operaio comune",
      "salarioProvincialeOrario": 6.16000, "terzoElementoOrario": 2.40671 }
  ],
  "contributi": [
    { "codice": "32300", "descrizione": "Contributo FAVLA Cuneo", "aliquota": 0.0030, "aCaricoDipendente": true },
    { "codice": "32301", "descrizione": "CAC provinciale Cuneo",  "aliquota": 0.0020, "aCaricoDipendente": true },
    { "codice": "40000", "descrizione": "Contributo FAP (IVS)",   "aliquota": 0.0884, "aCaricoDipendente": true },
    { "codice": "45000", "descrizione": "CAC nazionale",          "aliquota": 0.0020, "aCaricoDipendente": true },
    { "codice": "32301A", "descrizione": "CAC provinciale c/azienda", "aliquota": 0.0044, "aCaricoDipendente": false }
  ]
}
```

Note sui campi:
- **Cinque decimali** sulle paghe orarie: la tariffa reale è 8,56671 €/h, non 8,57.
- `terzoElementoOrario` è la quota che in agricoltura paga ora per ora ferie, festività e
  mensilità aggiuntive: senza, la busta risulterebbe più bassa del dovuto.
- `aliquotaContributiDeducibili` è la quota che riduce l'imponibile fiscale. **Non** coincide con
  la somma delle trattenute (i contributi di assistenza contrattuale non sono deducibili). Il
  valore osservato sulle buste di Cuneo è 7,80% e resta da chiarire col Consulente del Lavoro.
- `aCaricoDipendente: false` = contributo dell'azienda: viene mostrato come dato figurativo e non
  trattenuto al lavoratore.

## Come caricarlo

`POST /api/agricoltura/tabelle/importa` — è un **upsert per anno + provincia**: le tariffe e i
contributi di quell'anno vengono sostituiti con quelli forniti, gli altri anni non si toccano.
Come per i CCNL, le tabelle sono condivise da tutti gli studi, quindi l'operazione è riservata
all'**amministratore della piattaforma**.

```bash
curl -X POST http://localhost:5171/api/agricoltura/tabelle/importa \
  -H "Authorization: Bearer <token super-admin>" -H "Content-Type: application/json" \
  -d @cpl-cuneo-2026.json
```

Risposta: `{ "tariffeCaricate": N, "contributiCaricati": M }`.

## Come verificarle

- `GET /api/agricoltura/province` — quali province e anni sono caricati, con la fonte e il campo
  `calcolabile`: è `false` se mancano i parametri nazionali dello stesso anno.
- `GET /api/agricoltura/tabelle/{anno}/{provincia}` — tariffe, contributi e paga oraria calcolata.
- `POST /api/agricoltura/cedolino/anteprima` — calcola una busta OTD **senza archiviarla**: serve a
  confrontare il risultato con una busta del vecchio software prima di fidarsi.

Esempio di anteprima (la busta reale di ottobre 2025: netto 600,00):

```json
{ "anno": 2025, "mese": 10, "provincia": "CN", "livello": "COMUNE", "tipo": "Determinato",
  "oreOrdinarie": 64, "giornateLavorate": 8, "giorniDetrazione": 10,
  "unaTantum": 114.00, "redditoPresuntoAnnuo": 6000 }
```

**Attenzione ai parametri nazionali dell'anno**: anche le aliquote IRPEF sono versionate. Per
elaborare una busta del 2025 devono esistere i parametri del 2025; altrimenti l'API risponde che
l'anno non è presente invece di calcolare con le aliquote di un altro anno.

## Anteprima collegata al rapporto e PDF

La pagina **Paghe → Agricoltura OTD** usa questi endpoint:

- `POST /api/agricoltura/rapporti/{rapportoId}/cedolino/anteprima`;
- `POST /api/agricoltura/rapporti/{rapportoId}/cedolino/pdf`.

Il rapporto deve appartenere al tenant autenticato e usare `AGRICOLTURA_OPERAI`. Provincia CPL,
livello, tipo, residenza e diritto alle detrazioni vengono letti dall'anagrafica: non possono
essere sostituiti dal browser. Il corpo contiene soltanto i dati mensili:

```json
{
  "anno": 2025,
  "mese": 10,
  "oreOrdinarie": 64,
  "giornateLavorate": 8,
  "giorniDetrazione": 10,
  "unaTantum": 114.00,
  "detrazioniGiaGoduteAnno": 0,
  "arrotondamentoRiportato": 0,
  "ultimoCedolino": false,
  "redditoPresuntoAnnuo": 6000
}
```

Il reddito annuo presunto è obbligatorio: per uno stagionale annualizzare il singolo mese
produrrebbe un reddito falso. Il PDF è una **bozza A4 da validare**, non viene archiviato e non
costituisce LUL, denuncia UniEmens/PosAgri o F24. Mostra solo le voci datoriali note e dichiara che
i contributi complessivi del datore saranno determinati dall'INPS dopo la denuncia.

La sola tabella validata e i casi golden disponibili sono OTD Cuneo 2025. Un rapporto OTI viene
riconosciuto, ma motore, API e SPA lo bloccano esplicitamente: non è consentito riusare salario e
terzo elemento OTD finché non esistono parametri OTI distinti e validati professionalmente.

Finché non esistono cartellino agricolo persistito, progressivi, snapshot e workflow PosAgri, il
normale endpoint di archiviazione rifiuta i rapporti agricoli: è un blocco intenzionale contro
cedolini elaborati con TFR, ferie/ROL o adempimenti ordinari non applicabili.
