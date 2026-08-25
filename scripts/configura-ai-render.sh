#!/usr/bin/env bash
# Imposta su Render le variabili dell'AI (DeepSeek principale + Google Gemini di riserva).
#
#   ./scripts/configura-ai-render.sh
#
# Serve una chiave API Render (Account Settings → API Keys → Create API Key). La chiave viene
# chiesta a schermo e non viene mai scritta su file, né mostrata, né salvata nella cronologia
# della shell. Le chiavi dei provider vengono lette da .chiavi/ (cartella già in .gitignore).
#
# Attenzione: cambiare una variabile d'ambiente su Render fa ripartire il servizio, quindi
# l'app resta irraggiungibile per qualche minuto. Lo script chiede conferma prima di scrivere.
set -euo pipefail
cd "$(dirname "$0")/.."

NOME_SERVIZIO="${RENDER_SERVICE_NAME:-paghepro}"
CHIAVE_DEEPSEEK_FILE=".chiavi/deepseek.txt"
CHIAVE_GEMINI_FILE=".chiavi/gemini.txt"

for f in "$CHIAVE_DEEPSEEK_FILE" "$CHIAVE_GEMINI_FILE"; do
  if [[ ! -s "$f" ]]; then
    echo "ERRORE: manca $f" >&2
    exit 1
  fi
done

command -v curl >/dev/null || { echo "ERRORE: serve curl." >&2; exit 1; }
command -v python3 >/dev/null || { echo "ERRORE: serve python3." >&2; exit 1; }

read -rsp "Chiave API Render (non viene mostrata): " RENDER_API_KEY
echo
[[ -n "$RENDER_API_KEY" ]] || { echo "ERRORE: chiave vuota." >&2; exit 1; }

api() {
  curl -sS --max-time 60 -H "Authorization: Bearer $RENDER_API_KEY" \
    -H "Accept: application/json" -H "Content-Type: application/json" "$@"
}

echo "Cerco il servizio «$NOME_SERVIZIO»..."
SERVIZIO_JSON=$(api "https://api.render.com/v1/services?name=$NOME_SERVIZIO&limit=20")
SERVICE_ID=$(python3 -c "
import json,sys
dati=json.loads(sys.argv[1])
for riga in dati:
    servizio=riga.get('service', riga)
    if servizio.get('name')==sys.argv[2]:
        print(servizio.get('id')); break
" "$SERVIZIO_JSON" "$NOME_SERVIZIO")

if [[ -z "$SERVICE_ID" ]]; then
  echo "ERRORE: nessun servizio di nome «$NOME_SERVIZIO» in questo account." >&2
  echo "Se il servizio ha un altro nome: RENDER_SERVICE_NAME=nome $0" >&2
  exit 1
fi
echo "Servizio trovato: $SERVICE_ID"

echo
echo "Sto per impostare su Render:"
echo "  Ai__ApiKey        = (chiave DeepSeek da $CHIAVE_DEEPSEEK_FILE)"
echo "  Ai__GeminiApiKey  = (chiave Google da $CHIAVE_GEMINI_FILE)"
echo "  Ai__ModelloGemini = gemini-3.5-flash"
echo "Il servizio verrà riavviato e resterà irraggiungibile per qualche minuto."
read -rp "Procedo? [scrivi SI] " CONFERMA
[[ "$CONFERMA" == "SI" ]] || { echo "Annullato: nulla è stato modificato."; exit 0; }

for COPPIA in "Ai__ApiKey:$(cat "$CHIAVE_DEEPSEEK_FILE")" \
              "Ai__GeminiApiKey:$(cat "$CHIAVE_GEMINI_FILE")" \
              "Ai__ModelloGemini:gemini-3.5-flash"; do
  CHIAVE="${COPPIA%%:*}"
  VALORE="${COPPIA#*:}"
  CORPO=$(python3 -c "import json,sys; print(json.dumps({'value': sys.argv[1]}))" "$VALORE")
  # PUT su una singola variabile: non tocca le altre già configurate nel dashboard.
  ESITO=$(api -X PUT -d "$CORPO" \
    "https://api.render.com/v1/services/$SERVICE_ID/env-vars/$CHIAVE" -w '\n%{http_code}')
  CODICE=$(printf '%s' "$ESITO" | tail -1)
  if [[ "$CODICE" == "200" || "$CODICE" == "201" ]]; then
    echo "  $CHIAVE impostata."
  else
    echo "  ERRORE su $CHIAVE (HTTP $CODICE): $(printf '%s' "$ESITO" | head -n -1)" >&2
    exit 1
  fi
done

echo
echo "Fatto. Il servizio si sta riavviando con le nuove chiavi."
echo "Per verificare, quando è tornato su:"
echo "  1. entra su https://paghepro.onrender.com e apri Aurora;"
echo "  2. il pannello deve mostrare DeepSeek come provider e Gemini come riserva."
echo
echo "Promemoria: la chiave Google è stata incollata in chat, quindi va considerata esposta."
echo "Rigenerala su Google AI Studio, aggiorna $CHIAVE_GEMINI_FILE e rilancia questo script."
