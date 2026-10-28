#!/usr/bin/env bash
# Owned by house-template-dotnet. `make smoke`: builds the API image, starts the backend in Compose
# (Postgres, Keycloak, the migrate step, the API) and asks it the questions that matter once it is
# an image and not a project: is it up, which commit is it, who may call it, does a write survive a
# retry, does it stop in time. Leaves nothing behind unless SMOKE_KEEP=1.
set -euo pipefail

api="http://127.0.0.1:${API_PORT:-8000}"
keycloak="http://localhost:${KEYCLOAK_PORT:-8080}"
export GIT_COMMIT="${GIT_COMMIT:-$(git rev-parse --short HEAD 2>/dev/null || echo smoke)}"
failures=0

cleanup() {
  if [ "${SMOKE_KEEP:-0}" != "1" ]; then docker compose down --volumes --remove-orphans >/dev/null 2>&1 || true; fi
}
trap cleanup EXIT

check() { # name, expected, actual
  if [ "$2" = "$3" ]; then echo "  ok   $1"; else echo "  FAIL $1: expected '$2', got '$3'"; failures=$((failures + 1)); fi
}
contains() { # name, needle, haystack
  case "$3" in *"$2"*) echo "  ok   $1" ;; *) echo "  FAIL $1: '$2' not in '$3'"; failures=$((failures + 1)) ;; esac
}
status() { curl -s -o /dev/null -w '%{http_code}' "$@"; }
token() { curl -s -d grant_type=password -d client_id=local-cli -d "username=$1" -d "password=$1" "$keycloak/realms/app/protocol/openid-connect/token" |
  sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p'; }

echo "Building and starting the backend (commit $GIT_COMMIT)..."
docker compose up -d --build --wait api

echo "Checking it:"
contains "live reports the commit" "\"commit\":\"$GIT_COMMIT\"" "$(curl -s "$api/health/live")"
check "ready" '{"status":"ok"}' "$(curl -s "$api/health/ready")"
check "an anonymous caller is refused" 401 "$(status "$api/v1/me")"
check "X-Dev-User means nothing outside development" 401 "$(status -H 'X-Dev-User: sneaky' "$api/v1/me")"

dev="$(token dev)"
visitor="$(token visitor)"
[ -n "$dev" ] && [ -n "$visitor" ] || { echo "  FAIL no token from Keycloak"; exit 1; }
contains "a signed-in user is known" '"permissions":["flags.read","me.read","notes.read","notes.write"]' "$(curl -s -H "Authorization: Bearer $dev" "$api/v1/me")"
check "a user without permissions is forbidden" 403 "$(status -H "Authorization: Bearer $visitor" "$api/v1/flags")"

key="smoke-$(date +%s)"
create() { curl -s -i -H "Authorization: Bearer $dev" -H 'Content-Type: application/json' -H "Idempotency-Key: $key" -d '{"text":"smoke"}' "$api/v1/platform/notes"; }
first="$(create)"
second="$(create)"
contains "a note is created" "201 Created" "$first"
contains "the retry is answered from the first request" "Idempotency-Replayed: true" "$second"
id="$(printf '%s' "$first" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p')"
check "the note can be read back" 200 "$(status -H "Authorization: Bearer $dev" "$api/v1/platform/notes/$id")"
contains "the list pages" '"nextCursor"' "$(curl -s -H "Authorization: Bearer $dev" "$api/v1/platform/notes?limit=1")"
contains "the flags are shown" '"key":"notes-list"' "$(curl -s -H "Authorization: Bearer $dev" "$api/v1/flags")"

echo "Stopping it:"
started=$(date +%s)
docker compose stop api >/dev/null
elapsed=$(( $(date +%s) - started ))
check "it stops well inside the grace period (${elapsed}s)" yes "$([ "$elapsed" -lt 20 ] && echo yes || echo no)"
check "it stopped cleanly" 0 "$(docker inspect --format '{{.State.ExitCode}}' "$(docker compose ps -a -q api)")"

if [ "$failures" -gt 0 ]; then echo "$failures check(s) failed."; docker compose logs api | tail -30; exit 1; fi
echo "All checks passed."
