#!/usr/bin/env bash
# Pubblica PaghePro sul tuo VPS, dal Mac, con un solo comando:
#
#   VPS=root@1.2.3.4 DOMINIO=paghe.tuodominio.it ./scripts/pubblica-su-vps.sh
#
# Facoltativo: CHIAVE_SSH=~/.ssh/miachiave (default: quella di sistema).
# Cosa fa: copia il repo in /opt/paghepro sul server, installa Docker se manca,
# genera i segreti al primo avvio (e NON li rigenera mai), copia le chiavi AI da
# .chiavi/ se presenti, costruisce l'immagine e avvia app + HTTPS (Caddy).
# Il database resta nel volume Docker paghe-dati: le ripubblicazioni non lo toccano.
set -euo pipefail
cd "$(dirname "$0")/.."

: "${VPS:?ERRORE: indica il server, es. VPS=root@1.2.3.4}"
: "${DOMINIO:?ERRORE: indica il dominio, es. DOMINIO=paghe.tuodominio.it}"
SSH_OPTS=(-o ConnectTimeout=15)
[[ -n "${CHIAVE_SSH:-}" ]] && SSH_OPTS+=(-i "$CHIAVE_SSH")
DEST=/opt/paghepro

echo "── 1/4 Copio il progetto su $VPS:$DEST ..."
rsync -az --delete \
  --exclude .git --exclude node_modules --exclude '**/bin' --exclude '**/obj' \
  --exclude output --exclude .chiavi --exclude dist \
  -e "ssh ${SSH_OPTS[*]}" ./ "$VPS:$DEST/"

echo "── 2/4 Preparo Docker e i segreti sul server ..."
DEEPSEEK="$(cat .chiavi/deepseek.txt 2>/dev/null || true)"
GEMINI="$(cat .chiavi/gemini.txt 2>/dev/null || true)"
ssh "${SSH_OPTS[@]}" "$VPS" DOMINIO="$DOMINIO" DEEPSEEK="$DEEPSEEK" GEMINI="$GEMINI" 'bash -s' <<'REMOTO'
set -euo pipefail
if ! command -v docker >/dev/null 2>&1; then
  echo "Installo Docker..."
  curl -fsSL https://get.docker.com | sh
fi
cd /opt/paghepro/deploy/vps
if [[ ! -f .env ]]; then
  echo "Primo avvio: genero i segreti in .env"
  cp esempio.env .env
  chmod 600 .env
  sed -i "s|^DOMINIO=.*|DOMINIO=$DOMINIO|" .env
  sed -i "s|^Jwt__Key=.*|Jwt__Key=$(openssl rand -base64 48 | tr -d '\n')|" .env
  sed -i "s|^PAGHE_DATA_KEY=.*|PAGHE_DATA_KEY=$(openssl rand -base64 32 | tr -d '\n')|" .env
  [[ -n "$DEEPSEEK" ]] && sed -i "s|^Ai__ApiKey=.*|Ai__ApiKey=$DEEPSEEK|" .env
  [[ -n "$GEMINI" ]] && sed -i "s|^Ai__GeminiApiKey=.*|Ai__GeminiApiKey=$GEMINI|" .env
  echo "IMPORTANTE: copia /opt/paghepro/deploy/vps/.env in un posto sicuro fuori dal server."
else
  sed -i "s|^DOMINIO=.*|DOMINIO=$DOMINIO|" .env
fi
REMOTO

echo "── 3/4 Costruisco e avvio (la prima volta servono alcuni minuti) ..."
ssh "${SSH_OPTS[@]}" "$VPS" 'cd /opt/paghepro/deploy/vps && docker compose up -d --build'

echo "── 4/4 Controllo la salute ..."
ssh "${SSH_OPTS[@]}" "$VPS" '
for i in $(seq 1 60); do
  if docker compose -f /opt/paghepro/deploy/vps/docker-compose.yml exec -T app \
       sh -c "wget -q -O- http://127.0.0.1:8080/health" 2>/dev/null; then
    echo; echo "App in salute."; exit 0
  fi
  sleep 2
done
echo "App non in salute: guarda i log con: docker compose logs app"; exit 1'

echo
echo "FATTO. Appena il DNS di $DOMINIO punta all'IP del VPS,"
echo "il sito è su https://$DOMINIO (il certificato si attiva da solo al primo accesso)."
