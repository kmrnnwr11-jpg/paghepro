# PaghePro sul tuo VPS

Un solo servizio Docker (SPA + API, database SQLite su volume) più Caddy per
l'HTTPS automatico. Tutto il necessario è in `deploy/vps/`; la pubblicazione si
lancia dal Mac con `scripts/pubblica-su-vps.sh`.

## Prerequisiti

1. Un VPS Linux (Ubuntu/Debian) raggiungibile via SSH come `root` (o utente sudo).
2. Un dominio o sottodominio (es. `paghe.tuodominio.it`) con un **record DNS A**
   che punta all'IP del VPS.
3. Sul Mac: le chiavi AI in `.chiavi/deepseek.txt` e `.chiavi/gemini.txt`
   (facoltative: senza, Aurora resta configurata ma muta).

## Pubblicare (e ripubblicare gli aggiornamenti)

```bash
VPS=root@IP_DEL_VPS DOMINIO=paghe.tuodominio.it ./scripts/pubblica-su-vps.sh
```

Con una chiave SSH specifica: aggiungi `CHIAVE_SSH=~/.ssh/nomechiave`.

Lo script: copia il repo in `/opt/paghepro`, installa Docker se manca, al primo
avvio genera `Jwt__Key` e `PAGHE_DATA_KEY` in `deploy/vps/.env` (mai rigenerati
nelle pubblicazioni successive), costruisce l'immagine e avvia `app` + `caddy`.
Il database vive nel volume Docker `paghe-dati` e sopravvive alle ripubblicazioni.

## Da sapere

- **`deploy/vps/.env` sul server contiene i segreti**: fanne subito una copia
  fuori dal VPS. Se `PAGHE_DATA_KEY` si perde, i dati cifrati non si recuperano.
- **Backup**: il database è nel volume `paghe-dati`. Copia di sicurezza:
  `docker run --rm -v vps_paghe-dati:/data -v /root:/backup alpine cp /data/paghe.db /backup/paghe-$(date +%F).db`
- **Log**: `cd /opt/paghepro/deploy/vps && docker compose logs -f app`
- **Spegnere/riavviare**: `docker compose down` / `docker compose up -d`
- Questo archivio è **separato** da Render/Supabase e dal pacchetto locale
  `~/PaghePro`: tre ambienti, tre database.
- Il motore paghe resta BOZZA DA VALIDARE come sul resto del progetto.
