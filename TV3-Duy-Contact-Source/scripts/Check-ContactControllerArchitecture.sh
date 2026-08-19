#!/usr/bin/env bash
# Static architecture guard for ContactRequestsController. It intentionally checks
# concrete data-access symbols rather than broad substring grep that can false-positive.
set -Eeuo pipefail

readonly SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
readonly CONTROLLER="$REPO_ROOT/src/CloudServiceStore.WebApi/Controllers/ContactRequestsController.cs"

fail() {
  printf 'CONTACT_CONTROLLER_ARCHITECTURE=FAIL: %s\n' "$1" >&2
  exit 1
}

test -f "$CONTROLLER" || fail "Controller not found: $CONTROLLER"

# Contract: the controller delegates use cases to Application and must not directly
# reference EF/SQL/query persistence primitives.
grep -Eq 'IContactRequestService[[:space:]]+contactRequestService' "$CONTROLLER" \
  || fail 'IContactRequestService constructor dependency is missing.'

for forbidden in \
  'CloudServiceStoreDbContext' \
  '^[[:space:]]*using[[:space:]]+Microsoft\.EntityFrameworkCore' \
  '^[[:space:]]*using[[:space:]]+CloudServiceStore\.Infrastructure\.Persistence' \
  '\bDbContext\b' \
  '\bSet[[:space:]]*<' \
  '\bAnyAsync[[:space:]]*\(' \
  '\bWhere[[:space:]]*\(' \
  '\bInclude[[:space:]]*\(' \
  '\bAsNoTracking[[:space:]]*\(' \
  '\bSaveChanges(Async)?[[:space:]]*\(' \
  '\bDatabase\b' \
  '\bFromSql' \
  '\bSqlConnection\b'; do
  if grep -En "$forbidden" "$CONTROLLER"; then
    fail "Direct persistence symbol matched: $forbidden"
  fi
done

grep -Fq '[HttpPost("{id:guid}/status")]' "$CONTROLLER" \
  || fail 'Status endpoint is not the locked POST contract.'
grep -Fq 'contactRequestService.CreateAsync(' "$CONTROLLER" \
  || fail 'Create action does not delegate to service.'
grep -Fq 'contactRequestService.GetAsync(' "$CONTROLLER" \
  || fail 'List action does not delegate to service.'
grep -Fq 'contactRequestService.GetByIdAsync(' "$CONTROLLER" \
  || fail 'Detail action does not delegate to service.'
grep -Fq 'contactRequestService.UpdateStatusAsync(' "$CONTROLLER" \
  || fail 'Status action does not delegate to service.'

printf 'CONTACT_CONTROLLER_ARCHITECTURE=PASS\n'
