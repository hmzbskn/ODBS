#!/usr/bin/env bash
# Mock OBİS, backend API ve frontend'i tek komutla başlatır (Linux / macOS / Windows Git Bash).
# Durdurmak için: ./stop-all.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RUN="$ROOT/.run"
OBIS_PORT="${OBIS_PORT:-5280}"
API_PORT="${API_PORT:-5154}"
WEB_PORT="${WEB_PORT:-5173}"
TIMEOUT="${TIMEOUT_SECONDS:-90}"
mkdir -p "$RUN"

for cmd in dotnet node npm curl; do
  command -v "$cmd" >/dev/null 2>&1 || { echo "'$cmd' bulunamadı. .NET 8+ SDK ve Node.js 20+ kurulu olmalı (bkz. README)." >&2; exit 1; }
done

if ls "$RUN"/*.pid >/dev/null 2>&1; then
  for f in "$RUN"/*.pid; do
    if kill -0 "$(cat "$f")" 2>/dev/null; then echo "Uygulama zaten çalışıyor görünüyor. Önce ./stop-all.sh çalıştırın." >&2; exit 1; fi
  done
fi

if [ ! -d "$ROOT/frontend/node_modules" ]; then
  echo "> Frontend bağımlılıkları kuruluyor (ilk çalıştırma)..."
  (cd "$ROOT/frontend" && npm install --no-audit --no-fund)
fi
echo "> Backend derleniyor..."
dotnet build "$ROOT/backend/OgrenciDanismanOtomasyonu.slnx" -nologo -v quiet

export ASPNETCORE_ENVIRONMENT=Development
export VITE_API_TARGET="http://localhost:$API_PORT"

( cd "$ROOT/backend/src/Obis.MockApi" && ASPNETCORE_URLS="http://localhost:$OBIS_PORT" \
    nohup dotnet run --no-build --no-launch-profile >"$RUN/obis.log" 2>&1 & echo $! >"$RUN/obis.pid" )
( cd "$ROOT/backend/src/Advisor.Api" && ASPNETCORE_URLS="http://localhost:$API_PORT" Obis__BaseUrl="http://localhost:$OBIS_PORT" \
    nohup dotnet run --no-build --no-launch-profile >"$RUN/api.log" 2>&1 & echo $! >"$RUN/api.pid" )
( cd "$ROOT/frontend" && nohup npm run dev -- --port "$WEB_PORT" --strictPort >"$RUN/web.log" 2>&1 & echo $! >"$RUN/web.pid" )

wait_for() {
  local name="$1" url="$2" i=0
  until curl -s -o /dev/null "$url"; do   # 401/404 de "ayakta" demektir; yalnızca bağlantı hatası bekletir
    i=$((i + 1)); [ "$i" -gt $((TIMEOUT * 2)) ] && { echo "$name $TIMEOUT sn içinde hazır olmadı. Günlükler: $RUN" >&2; exit 1; }
    sleep 0.5
  done
}

echo "> Servislerin hazır olması bekleniyor..."
wait_for "Mock OBİS" "http://localhost:$OBIS_PORT/api/advisors"
wait_for "Backend API" "http://localhost:$API_PORT/api/auth/me"
wait_for "Frontend" "http://localhost:$WEB_PORT/"

echo
echo "Hazır."
echo "  Uygulama   : http://localhost:$WEB_PORT"
echo "  Swagger    : http://localhost:$API_PORT/swagger"
echo "  Mock OBİS  : http://localhost:$OBIS_PORT/swagger"
echo "  Demo hesap : admin / danisman1 / ogrenci2  (şifre hepsi: Demo123!)"
echo "  Günlükler  : $RUN"
echo "  Durdurmak  : ./stop-all.sh"
