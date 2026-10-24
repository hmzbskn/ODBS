#!/usr/bin/env bash
# start-all.sh ile başlatılan süreçleri (alt süreçleriyle) durdurur.
RUN="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/.run"
API_PORT="${API_PORT:-5154}"; OBIS_PORT="${OBIS_PORT:-5280}"; WEB_PORT="${WEB_PORT:-5173}"

kill_tree() {
  local pid="$1"
  if command -v taskkill >/dev/null 2>&1; then
    taskkill //PID "$pid" //T //F >/dev/null 2>&1 || true        # Windows (Git Bash)
  else
    pkill -TERM -P "$pid" 2>/dev/null || true; kill "$pid" 2>/dev/null || true
  fi
}

found=0
for name in web api obis; do
  f="$RUN/$name.pid"
  [ -f "$f" ] || continue
  found=1
  pid="$(cat "$f")"
  kill_tree "$pid"
  echo "Durduruldu: $name (PID $pid)"
  rm -f "$f"
done

# Git Bash'te $! dış süreç kimliğini vermeyebilir: portu dinleyen süreçleri de kapat.
if command -v netstat >/dev/null 2>&1 && command -v taskkill >/dev/null 2>&1; then
  for port in "$WEB_PORT" "$API_PORT" "$OBIS_PORT"; do
    for pid in $(netstat -ano 2>/dev/null | grep "[:.]$port .*LISTENING" | awk '{print $NF}' | sort -u); do
      taskkill //PID "$pid" //T //F >/dev/null 2>&1 && echo "Durduruldu: port $port (PID $pid)" && found=1
    done
  done
fi

[ "$found" = 1 ] && echo "Tamam." || echo "Çalışan kayıtlı süreç yok."
