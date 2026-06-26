#!/bin/bash
# ─────────────────────────────────────────────────────────────
# Avvio locale di Paghe SaaS (backend + frontend) con un click.
# Fai doppio click su questo file dal Finder.
# Per fermare tutto: chiudi questa finestra o premi Ctrl + C.
# ─────────────────────────────────────────────────────────────
cd "$(dirname "$0")" || exit 1

echo "▶ Avvio backend (la cucina)…"
( cd backend && dotnet run --project src/Paghe.Api -c Release ) &
BACKEND_PID=$!

# Quando chiudi, ferma anche il backend
trap 'echo; echo "⏹  Arresto in corso…"; kill $BACKEND_PID 2>/dev/null; exit 0' INT TERM EXIT

echo "⏳ Attendo che il backend sia pronto…"
for i in $(seq 1 40); do
  if curl -s -o /dev/null --max-time 2 http://localhost:5171/swagger/v1/swagger.json; then
    echo "✅ Backend pronto su http://localhost:5171"
    break
  fi
  sleep 1
done

echo "▶ Preparo il frontend (la grafica)…"
cd frontend || exit 1
[ -d node_modules ] || npm install

# Apre il browser appena il sito è su
( sleep 4; open http://localhost:5173 ) &

echo "▶ Avvio frontend su http://localhost:5173 — si aprirà il browser."
npm run dev
