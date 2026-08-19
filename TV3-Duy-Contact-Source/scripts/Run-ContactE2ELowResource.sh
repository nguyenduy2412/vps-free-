#!/usr/bin/env bash
# Runs only Contact public E2E sequentially for memory-constrained local runners.
# It starts no external service itself; Playwright config owns the local web server.
set -Eeuo pipefail

readonly SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
readonly LOW_RESOURCE_WORKERS=1
readonly LOW_RESOURCE_RETRIES=0
readonly LOW_RESOURCE_MAX_FAILURES=1

usage() {
  cat <<'EOF'
Usage:
  bash scripts/Run-ContactE2ELowResource.sh

The script forces one worker, zero retry and stops after the first failure.
It runs only `e2e/contact-public.spec.ts` and never performs Git/GitHub actions.

Do not set PLAYWRIGHT_WORKERS for this script. Use the normal Playwright command
on a sufficiently resourced CI runner when intentionally testing parallel workers.
EOF
}

case "${1:-}" in
  "") ;;
  --help|-h)
    usage
    exit 0
    ;;
  *)
    printf 'CONTACT_E2E_LOW_RESOURCE=FAIL: Unknown argument: %s\n' "$1" >&2
    exit 1
    ;;
esac

if [[ -n "${PLAYWRIGHT_WORKERS:-}" ]]; then
  printf 'CONTACT_E2E_LOW_RESOURCE=FAIL: PLAYWRIGHT_WORKERS is not supported; this script always uses one worker.\n' >&2
  exit 1
fi

cd "$REPO_ROOT/frontend"
printf 'Running Contact E2E with one worker, zero retry and max one failure; NODE_ENV is removed for next dev.\n'
env -u NODE_ENV npx --no-install playwright test e2e/contact-public.spec.ts \
  --workers="$LOW_RESOURCE_WORKERS" \
  --retries="$LOW_RESOURCE_RETRIES" \
  --max-failures="$LOW_RESOURCE_MAX_FAILURES" \
  --reporter=line
printf 'CONTACT_E2E_LOW_RESOURCE=PASS\n'
