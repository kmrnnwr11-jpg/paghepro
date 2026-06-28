# Caricare i CCNL reali (tutti)

Il software **non inventa** i minimi dei ~900 CCNL italiani: i numeri veri vanno caricati da
una fonte affidabile (Consulente del Lavoro, fornitore di dati, fogli Excel). Per questo c'è
un **import in blocco**: prepari un file JSON con le tabelle e le carichi — vale per qualsiasi CCNL,
senza toccare il codice.

## Formato del file (JSON)
Un array di CCNL; per ognuno: codice, descrizione, anno e i livelli con i minimi.

```json
[
  {
    "codice": "MULTISERVIZI_PULIZIE",
    "descrizione": "CCNL Multiservizi / Pulizie",
    "anno": 2026,
    "livelli": [
      { "livello": "1", "descrizione": "Primo livello",  "minimoTabellare": 1180.00, "edr": 0, "importoScatto": 25.00 },
      { "livello": "2", "descrizione": "Secondo livello", "minimoTabellare": 1250.00, "edr": 0, "importoScatto": 25.00 },
      { "livello": "3", "descrizione": "Terzo livello",   "minimoTabellare": 1330.00, "edr": 0, "importoScatto": 25.00 }
    ]
  }
]
```

## Come caricarlo
`POST /api/ccnl/importa` con il JSON nel corpo (autenticato). È un **upsert** per codice+anno:
i livelli di quell'anno vengono sostituiti con quelli forniti.

```bash
curl -X POST http://localhost:5171/api/ccnl/importa \
  -H "Authorization: Bearer <token>" -H "Content-Type: application/json" \
  -d @ccnl-italia-2026.json
```

Risposta: `{ "ccnlCaricati": N, "livelliCaricati": M }`.

## Note
- I codici interni (es. `MULTISERVIZI_PULIZIE`) sono quelli a cui la **mappatura UNILAV** collega i
  contratti letti dai PDF (vedi `MappaturaCcnl`). Aggiungere un CCNL = riga di mappatura + tabelle qui.
- I valori già presenti nel seed sono **esempi**: sostituirli con questo import quando hai i dati veri.
- Il calcolo "al centesimo" delle buste reali richiede anche gli elementi del settore (orario, indennità,
  detassazioni, ecc.): l'import dei minimi è il primo mattone, non l'unico. Vedi `docs/buste-reali/`.
