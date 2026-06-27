#!/usr/bin/env bash
# Backup del database Paghe. Supporta SQLite (sviluppo) e PostgreSQL (produzione).
#
# Uso:
#   SQLite:    ./backup-db.sh sqlite /percorso/paghe.db [cartella_backup]
#   Postgres:  ./backup-db.sh postgres "postgresql://utente:pwd@host:5432/paghe" [cartella_backup]
#
# Pianificazione consigliata: ogni notte via cron. Esempio (cron):
#   0 2 * * * /percorso/scripts/backup-db.sh sqlite /dati/paghe.db /backup >> /var/log/paghe-backup.log 2>&1
#
# Conserva gli ultimi 14 backup ed elimina i più vecchi.

set -euo pipefail

TIPO="${1:-}"
SORGENTE="${2:-}"
DEST="${3:-./backup}"
RETENZIONE=14

if [[ -z "$TIPO" || -z "$SORGENTE" ]]; then
  echo "Uso: $0 <sqlite|postgres> <db-o-connection-string> [cartella_backup]" >&2
  exit 1
fi

mkdir -p "$DEST"
TS="$(date +%Y%m%d-%H%M%S)"

case "$TIPO" in
  sqlite)
    if [[ ! -f "$SORGENTE" ]]; then echo "File DB non trovato: $SORGENTE" >&2; exit 1; fi
    OUT="$DEST/paghe-$TS.db"
    # .backup esegue una copia consistente anche con il DB in uso (meglio di cp).
    if command -v sqlite3 >/dev/null 2>&1; then
      sqlite3 "$SORGENTE" ".backup '$OUT'"
    else
      cp "$SORGENTE" "$OUT"
    fi
    gzip -f "$OUT"
    echo "Backup creato: $OUT.gz"
    ;;
  postgres)
    OUT="$DEST/paghe-$TS.sql.gz"
    pg_dump "$SORGENTE" | gzip > "$OUT"
    echo "Backup creato: $OUT"
    ;;
  *)
    echo "Tipo non valido: $TIPO (usa 'sqlite' o 'postgres')" >&2
    exit 1
    ;;
esac

# Pulizia: mantiene solo gli ultimi N backup.
ls -1t "$DEST"/paghe-* 2>/dev/null | tail -n +$((RETENZIONE + 1)) | xargs -r rm -f
echo "Backup completato. Conservati gli ultimi $RETENZIONE."
