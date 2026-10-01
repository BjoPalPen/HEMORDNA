#!/usr/bin/env bash
#
# Driftsättning av Hemordna. Körs av CI över SSH, och går att köra för hand på servern.
#
# CI-nyckeln är i serverns authorized_keys låst till PRECIS det här skriptet
# (command="..."), så en läckt CI-nyckel kan driftsätta - men inte läsa filer, inte öppna
# ett skal och inte röra något annat på maskinen. Det är skälet till att deployen är ETT
# skript och inte en rad kommandon från workflowet.
#
# Migreringar körs av API:t vid uppstart (RunMigrationsOnStartup), så det finns inget
# migreringssteg här.

set -euo pipefail

REPO_DIR="${HEMORDNA_REPO_DIR:-/home/deploy/hemordna}"
COMPOSE_FILE="docker-compose.prod.yml"
SERVICE="hemordna-api"
CONTAINER="hemordna-api"

cd "$REPO_DIR"

echo "== Hämtar main =="
git fetch --prune origin
# reset --hard, inte pull: servern ska vara exakt vad origin/main säger, även om något
# råkat ändras på plats. En lokal ändring här vore ändå osynlig för alla andra.
git reset --hard origin/main
git log --oneline -1

echo "== Bygger om och startar $SERVICE =="
docker compose -f "$COMPOSE_FILE" up -d --build --no-deps "$SERVICE"

echo "== Väntar in containern =="
for _ in $(seq 1 30); do
  status="$(docker inspect --format '{{.State.Status}}' "$CONTAINER" 2>/dev/null || echo saknas)"
  [ "$status" = "running" ] && break
  sleep 2
done

status="$(docker inspect --format '{{.State.Status}}' "$CONTAINER" 2>/dev/null || echo saknas)"
restarts="$(docker inspect --format '{{.RestartCount}}' "$CONTAINER" 2>/dev/null || echo '?')"
echo "Status=$status RestartCount=$restarts"

if [ "$status" != "running" ]; then
  echo "Containern kör inte efter driftsättning." >&2
  docker logs --tail 40 "$CONTAINER" >&2 || true
  exit 1
fi

# Migreringar är additiva (CLAUDE.md), men det är ändå värt att se i deployloggen vilka som
# faktiskt applicerades - det är den enda platsen den informationen finns efteråt.
echo "== Migreringar i den här uppstarten =="
docker logs "$CONTAINER" 2>&1 | grep -i 'Applying migration' | tail -5 || echo "(inga nya)"

echo "== Klart =="
