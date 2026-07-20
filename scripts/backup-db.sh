#!/usr/bin/env bash
# Backup cifrato del database Paghe. Supporta SQLite e PostgreSQL.
#
# Uso:
#   SQLite:    ./backup-db.sh sqlite /percorso/paghe.db [cartella_backup]
#   Postgres:  PGSERVICE=paghe ./backup-db.sh postgres env [cartella_backup]
#              (oppure configurare PGHOST/PGDATABASE/PGUSER/PGPASSFILE; mai password in argv)
#
# Prerequisiti: `age` e la variabile PAGHE_BACKUP_AGE_RECIPIENT con la chiave PUBBLICA del
# destinatario. La chiave privata deve stare separata dal database e dai backup.
#
# Pianificazione consigliata: ogni notte via cron. Esempio (cron):
#   0 2 * * * /percorso/scripts/backup-db.sh sqlite /dati/paghe.db /backup >> /var/log/paghe-backup.log 2>&1
#
# Conserva gli ultimi 14 backup cifrati ed elimina i più vecchi.

set -euo pipefail
umask 077

TIPO="${1:-}"
SORGENTE="${2:-}"
DEST="${3:-./backup}"
RETENZIONE=14
AGE_RECIPIENT="${PAGHE_BACKUP_AGE_RECIPIENT:-}"

if [[ -z "$TIPO" || -z "$SORGENTE" ]]; then
  echo "Uso: $0 sqlite <file.db> [dest] | $0 postgres env [dest]" >&2
  exit 1
fi
if [[ -z "$AGE_RECIPIENT" ]]; then
  echo "PAGHE_BACKUP_AGE_RECIPIENT mancante: backup annullato, nessun file in chiaro creato." >&2
  exit 1
fi
if ! command -v age >/dev/null 2>&1; then
  echo "Comando 'age' non disponibile: installarlo prima di eseguire backup reali." >&2
  exit 1
fi

mkdir -p "$DEST"
chmod 700 "$DEST"
TS="$(date +%Y%m%d-%H%M%S)"
ID="$TS-$$"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/paghe-backup.XXXXXX")"
TMP_OUT="$DEST/.paghe-$ID.tmp"

cleanup() {
  rm -rf "$TMP_DIR"
  rm -f "$TMP_OUT"
}
trap cleanup EXIT INT TERM

case "$TIPO" in
  sqlite)
    if [[ ! -f "$SORGENTE" ]]; then echo "File DB non trovato: $SORGENTE" >&2; exit 1; fi
    if ! command -v sqlite3 >/dev/null 2>&1; then
      echo "Comando 'sqlite3' non disponibile: backup live annullato (nessun fallback con cp)." >&2
      exit 1
    fi
    RAW="$TMP_DIR/paghe.db"
    OUT="$DEST/paghe-$ID.db.gz.age"
    # .backup esegue una copia consistente anche con il DB in uso (meglio di cp).
    sqlite3 "$SORGENTE" ".backup '$RAW'"
    gzip -c "$RAW" | age -r "$AGE_RECIPIENT" -o "$TMP_OUT"
    mv "$TMP_OUT" "$OUT"
    chmod 600 "$OUT"
    echo "Backup cifrato creato: $OUT"
    ;;
  postgres)
    if [[ "$SORGENTE" != "env" ]]; then
      echo "Per PostgreSQL usa il valore 'env' e PGSERVICE/PGPASSFILE: DSN e password in argv sono vietati." >&2
      exit 1
    fi
    if ! command -v pg_dump >/dev/null 2>&1; then
      echo "Comando 'pg_dump' non disponibile: backup PostgreSQL annullato." >&2
      exit 1
    fi
    OUT="$DEST/paghe-$ID.dump.age"
    pg_dump --format=custom | age -r "$AGE_RECIPIENT" -o "$TMP_OUT"
    mv "$TMP_OUT" "$OUT"
    chmod 600 "$OUT"
    echo "Backup PostgreSQL cifrato creato: $OUT"
    ;;
  *)
    echo "Tipo non valido: $TIPO (usa 'sqlite' o 'postgres')" >&2
    exit 1
    ;;
esac

# Pulizia: mantiene solo gli ultimi N backup.
ls -1t "$DEST"/paghe-*.age 2>/dev/null \
  | tail -n +$((RETENZIONE + 1)) \
  | while IFS= read -r file; do rm -f -- "$file"; done
echo "Backup completato. Conservati gli ultimi $RETENZIONE file cifrati."
