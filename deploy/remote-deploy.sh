#!/usr/bin/env bash
set -euo pipefail

APP_DIR="${APP_DIR:-$HOME/app}"
BACKUP_DIR="${BACKUP_DIR:-$HOME/backups}"
BACKUP_KEEP="${BACKUP_KEEP:-10}"
IMAGE_TAG="${IMAGE_TAG:-latest}"
HEALTH_TIMEOUT="${HEALTH_TIMEOUT:-240}"
ROLLBACK_ON_FAILURE="${ROLLBACK_ON_FAILURE:-1}"
ROLLBACK_KEEP="${ROLLBACK_KEEP:-3}"
REGISTRY="${REGISTRY:-ghcr.io}"
IMAGE_NAMESPACE="${IMAGE_NAMESPACE:-allas122/aiagentsberhakaton}"

SERVICES="chatnode web anonymizer"

log() { printf '[deploy %s] %s\n' "$(date +%H:%M:%S)" "$*"; }
die() { log "FATAL: $*"; exit 1; }

cd "$APP_DIR" || die "no such directory: $APP_DIR"
[ -f docker-compose.yml ] || die "docker-compose.yml missing in $APP_DIR"
[ -f docker-compose.prod.yml ] || die "docker-compose.prod.yml missing in $APP_DIR"
[ -f .env ] || die ".env missing in $APP_DIR"

compose_as() {
  local tag="$1"; shift
  IMAGE_TAG="$tag" docker compose -f docker-compose.yml -f docker-compose.prod.yml "$@"
}

compose() { compose_as "$IMAGE_TAG" "$@"; }

read_env() {
  local key="$1" fallback="$2" value
  value="$(sed -n "s/^${key}=//p" .env | tail -n 1 | tr -d '\r' || true)"
  printf '%s' "${value:-$fallback}"
}

WEB_PORT="$(read_env WEB_HTTP_PORT 8080)"
CHATNODE_PORT="$(read_env CHATNODE_HTTP_PORT 5064)"
STAMP="$(date +%Y%m%d-%H%M%S)"
ROLLBACK_TAG="rollback-$STAMP"
ROLLBACK_FILE="$BACKUP_DIR/rollback-$STAMP.txt"
HISTORY_FILE="$BACKUP_DIR/deploy-history.log"

WEB_CODE=000
API_CODE=000

health_ok() {
  local deadline=$((SECONDS + $1))
  while [ "$SECONDS" -lt "$deadline" ]; do
    WEB_CODE="$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "http://127.0.0.1:${WEB_PORT}/" || printf '000')"
    API_CODE="$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "http://127.0.0.1:${CHATNODE_PORT}/" || printf '000')"
    if [ "$WEB_CODE" = "200" ] && [ "$API_CODE" != "000" ]; then
      return 0
    fi
    sleep 5
  done
  return 1
}

mkdir -p "$BACKUP_DIR"

log "deploying tag: $IMAGE_TAG"
if [ -f "$HISTORY_FILE" ]; then
  log "last deploys:"
  tail -n 3 "$HISTORY_FILE" | sed 's/^/    /'
fi

DUMP="$BACKUP_DIR/predeploy-$STAMP.dump"
log "dumping postgres -> $DUMP"
if ! compose exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "$DUMP"; then
  rm -f "$DUMP"
  die "pg_dump failed, nothing was changed"
fi
if [ ! -s "$DUMP" ]; then
  rm -f "$DUMP"
  die "pg_dump produced an empty file, nothing was changed"
fi
log "dump ok ($(du -h "$DUMP" | cut -f1))"

: > "$ROLLBACK_FILE"
rollback_possible=1
for svc in $SERVICES; do
  cid="$(compose ps -q "$svc" 2>/dev/null || true)"
  if [ -z "$cid" ]; then
    log "warning: $svc is not running, auto-rollback disabled"
    rollback_possible=0
    continue
  fi
  img="$(docker inspect --format '{{.Image}}' "$cid" 2>/dev/null || true)"
  if [ -z "$img" ]; then
    log "warning: cannot resolve image of $svc, auto-rollback disabled"
    rollback_possible=0
    continue
  fi
  printf '%s %s\n' "$svc" "$img" >> "$ROLLBACK_FILE"
done

if [ "$rollback_possible" -eq 1 ]; then
  while read -r svc img; do
    docker tag "$img" "$REGISTRY/$IMAGE_NAMESPACE/$svc:$ROLLBACK_TAG"
  done < "$ROLLBACK_FILE"
  log "rollback point saved as tag $ROLLBACK_TAG"
fi

log "pulling images"
compose pull chatnode web anonymizer

log "recreating application containers"
compose up -d --remove-orphans

log "waiting for health (timeout ${HEALTH_TIMEOUT}s)"
if health_ok "$HEALTH_TIMEOUT"; then
  log "health ok (web=$WEB_CODE chatnode=$API_CODE)"
  printf '%s  tag=%-45s result=ok        rollback_point=%s\n' \
    "$(date '+%Y-%m-%d %H:%M:%S')" "$IMAGE_TAG" "$ROLLBACK_TAG" >> "$HISTORY_FILE"
else
  log "health check FAILED (web=$WEB_CODE chatnode=$API_CODE)"
  log "recent chatnode logs:"
  compose logs --tail 60 chatnode || true

  if [ "$ROLLBACK_ON_FAILURE" != "1" ]; then
    printf '%s  tag=%-45s result=failed    rollback=off\n' \
      "$(date '+%Y-%m-%d %H:%M:%S')" "$IMAGE_TAG" >> "$HISTORY_FILE"
    die "deploy failed, auto-rollback disabled; database untouched, dump at $DUMP"
  fi

  if [ "$rollback_possible" -ne 1 ]; then
    printf '%s  tag=%-45s result=failed    rollback=unavailable\n' \
      "$(date '+%Y-%m-%d %H:%M:%S')" "$IMAGE_TAG" >> "$HISTORY_FILE"
    die "deploy failed and no rollback point was captured; database untouched, dump at $DUMP"
  fi

  log "ROLLING BACK to $ROLLBACK_TAG"
  compose_as "$ROLLBACK_TAG" up -d --remove-orphans

  if health_ok 120; then
    log "rollback ok (web=$WEB_CODE chatnode=$API_CODE)"
    printf '%s  tag=%-45s result=rolledback rollback_point=%s\n' \
      "$(date '+%Y-%m-%d %H:%M:%S')" "$IMAGE_TAG" "$ROLLBACK_TAG" >> "$HISTORY_FILE"
    die "deploy failed, previous version restored; database untouched, dump at $DUMP"
  fi

  printf '%s  tag=%-45s result=rollback_failed\n' \
    "$(date '+%Y-%m-%d %H:%M:%S')" "$IMAGE_TAG" >> "$HISTORY_FILE"
  die "deploy failed AND rollback failed (web=$WEB_CODE chatnode=$API_CODE); manual action required, dump at $DUMP"
fi

ls -1t "$BACKUP_DIR"/predeploy-*.dump 2>/dev/null | tail -n +$((BACKUP_KEEP + 1)) | xargs -r rm -f
ls -1t "$BACKUP_DIR"/rollback-*.txt 2>/dev/null | tail -n +$((BACKUP_KEEP + 1)) | xargs -r rm -f

for svc in $SERVICES; do
  docker images --format '{{.Repository}}:{{.Tag}}' "$REGISTRY/$IMAGE_NAMESPACE/$svc" 2>/dev/null \
    | grep ':rollback-' \
    | sort -r \
    | tail -n +$((ROLLBACK_KEEP + 1)) \
    | while read -r stale; do
        docker rmi "$stale" >/dev/null 2>&1 || true
      done
done

docker image prune -f >/dev/null 2>&1 || true

log "disk: $(df -h / | awk 'NR==2 {print $4" free ("$5" used)"}')"
compose ps
log "done"
