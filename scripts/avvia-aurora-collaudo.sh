#!/usr/bin/env bash
# Avvia PaghePro in locale con Aurora accesa su DeepSeek, per il collaudo del fondatore.
#
#   ./scripts/avvia-aurora-collaudo.sh                  → solo chat e letture governate (A2)
#   ./scripts/avvia-aurora-collaudo.sh tua@email.it     → anche modifiche con conferma (A3)
#
# La chiave DeepSeek va in .chiavi/deepseek.txt (cartella già in .gitignore).
# Le modifiche A3 valgono SOLO per l'utente indicato: qualunque altro account resta in
# sola lettura. Nessuna configurazione viene scritta su file: tutto vive in questo avvio.
set -euo pipefail
cd "$(dirname "$0")/.."

CHIAVE_FILE=".chiavi/deepseek.txt"
DB="backend/src/Paghe.Api/paghe.db"

if [[ ! -s "$CHIAVE_FILE" ]]; then
  echo "ERRORE: manca la chiave DeepSeek in $CHIAVE_FILE" >&2
  exit 1
fi

EMAIL="${1:-}"
ENV_ARGS=(
  ASPNETCORE_ENVIRONMENT=Development
  ASPNETCORE_URLS=http://127.0.0.1:5171
  Ai__Provider=DeepSeek
  "Ai__ApiKey=$(cat "$CHIAVE_FILE")"
  Ai__SoloLocale=false
  Ai__ProviderCloudApprovato=true
)
if [[ -n "$EMAIL" ]]; then
  COPPIA=$(sqlite3 "$DB" \
    "SELECT lower(TenantId) || ':' || lower(Id) FROM Utenti WHERE Email = '$EMAIL' LIMIT 1;")
  if [[ -z "$COPPIA" ]]; then
    echo "ERRORE: nessun utente con email $EMAIL nel database locale." >&2
    echo "Apri prima l'app, entra (o registrati) con quella email, poi rilancia." >&2
    exit 1
  fi
  ENV_ARGS+=(
    Ai__GateAurora=A3
    Ai__ModalitaA3=Confirmed
    "Ai__CoppieShadowA3__0=$COPPIA"
  )
  echo "Modifiche A3 abilitate per $EMAIL (proposta -> tua conferma -> annullabile 24h)."
else
  echo "Solo chat e letture governate (A2). Per le modifiche: $0 tua@email.it"
fi

echo "Compilo l'ultima versione..."
dotnet build backend/Paghe.slnx -c Release >/dev/null

echo "Avvio su http://localhost:5171 — Ctrl+C per fermare."
env "${ENV_ARGS[@]}" \
  dotnet backend/src/Paghe.Api/bin/Release/net10.0/Paghe.Api.dll
