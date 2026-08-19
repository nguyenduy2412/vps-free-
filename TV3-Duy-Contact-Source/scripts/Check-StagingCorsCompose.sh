#!/usr/bin/env bash
# TV3 Contact Request — static staging CORS/environment and Compose preflight.
# Design: never source an env file and never print secret values.
set -Eeuo pipefail

readonly SCRIPT_NAME="$(basename "$0")"
readonly SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"

BASE_COMPOSE="$REPO_ROOT/docker-compose.yml"
STAGING_OVERRIDE="$REPO_ROOT/deploy/docker-compose.staging.yml.example"
ENV_FILE=""
FRONTEND_ORIGIN="${FRONTEND_STAGING_ORIGIN:-}"
FRONTEND_SITE_URL="${NEXT_PUBLIC_SITE_URL:-}"
API_BASE_URL_VALUE="${API_BASE_URL:-}"
REQUIRE_FRONTEND_RUNTIME=false
REQUIRE_BOOTSTRAP_ADMIN=false
SKIP_DOCKER_CONFIG=false

usage() {
  cat <<'EOF'
Usage:
  bash scripts/Check-StagingCorsCompose.sh [options]

Options:
  --env-file PATH                 Protected Compose environment file. It is parsed as data only;
                                  the script never sources or prints its secret values.
  --frontend-origin URL           Public browser origin for CORS. Defaults to FRONTEND_STAGING_ORIGIN.
  --next-public-site-url URL      Frontend runtime URL. Defaults to NEXT_PUBLIC_SITE_URL.
  --api-base-url URL              Internal URL used by Next.js server. Defaults to API_BASE_URL.
  --require-frontend-runtime      Require and compare --next-public-site-url/--api-base-url.
  --require-bootstrap-admin       Require SEED_ADMIN_PASSWORD for a new staging database bootstrap.
  --skip-docker-config            Validate inputs and source files without invoking Docker.
  --help                          Show this help.

Examples:
  bash scripts/Check-StagingCorsCompose.sh \
    --env-file /run/secrets/cloud-store-staging.env \
    --frontend-origin https://staging.example.com \
    --next-public-site-url https://staging.example.com \
    --api-base-url http://api:8080 \
    --require-frontend-runtime

The command uses `docker compose config --no-interpolate`; it deliberately does
not print a rendered configuration with resolved secret values.
EOF
}

fail() {
  printf 'STAGING_PREFLIGHT=FAIL: %s\n' "$*" >&2
  exit 1
}

pass() {
  printf 'PASS: %s\n' "$*"
}

command_exists() {
  command -v "$1" >/dev/null 2>&1
}

trim() {
  local value="$1"
  value="${value#"${value%%[![:space:]]*}"}"
  value="${value%"${value##*[![:space:]]}"}"
  printf '%s' "$value"
}

# Reads a simple KEY=value entry as data. This intentionally avoids `source`
# because a protected env file must never be executed as shell code.
read_env_file_value() {
  local key="$1"
  local line value
  line="$(grep -E "^[[:space:]]*${key}=" "$ENV_FILE" | tail -n 1 || true)"
  [[ -n "$line" ]] || return 1
  value="${line#*=}"
  value="$(trim "$value")"
  if [[ "$value" =~ ^\".*\"$ ]] || [[ "$value" =~ ^\'.*\'$ ]]; then
    value="${value:1:${#value}-2}"
  fi
  printf '%s' "$value"
}

value_for() {
  local key="$1"
  if [[ -n "$ENV_FILE" ]]; then
    read_env_file_value "$key" || true
  else
    printenv "$key" 2>/dev/null || true
  fi
}

require_secret() {
  local key="$1"
  local value
  value="$(value_for "$key")"
  [[ -n "$value" ]] || fail "$key is missing or blank. Supply it through a protected environment source."
  pass "$key is present (value withheld)"
}

require_https_origin() {
  local label="$1"
  local value="$2"
  [[ -n "$value" ]] || fail "$label is required."
  [[ "$value" != *" "* ]] || fail "$label must not contain whitespace."
  [[ "$value" != *,* ]] || fail "$label must contain exactly one origin, not a comma-separated list."
  [[ "$value" != */ ]] || fail "$label must not end with a slash."
  [[ "$value" != *"/"*"/"*"/"* ]] || true
  [[ "$value" =~ ^https://[A-Za-z0-9.-]+(:[0-9]{1,5})?$ ]] || \
    fail "$label must be one exact HTTPS origin such as https://staging.example.com (no path or wildcard)."
}

require_api_base_url() {
  local value="$1"
  [[ -n "$value" ]] || fail "API_BASE_URL is required when --require-frontend-runtime is used."
  [[ "$value" != *" "* && "$value" != *,* && "$value" != */ ]] || \
    fail "API_BASE_URL must be one base URL without whitespace, comma, or trailing slash."
  [[ "$value" =~ ^https?://[A-Za-z0-9.-]+(:[0-9]{1,5})?$ ]] || \
    fail "API_BASE_URL must be an HTTP(S) server URL such as http://api:8080."
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --env-file)
      [[ $# -ge 2 ]] || fail "--env-file requires a path."
      ENV_FILE="$2"
      shift 2
      ;;
    --frontend-origin)
      [[ $# -ge 2 ]] || fail "--frontend-origin requires a URL."
      FRONTEND_ORIGIN="$2"
      shift 2
      ;;
    --next-public-site-url)
      [[ $# -ge 2 ]] || fail "--next-public-site-url requires a URL."
      FRONTEND_SITE_URL="$2"
      shift 2
      ;;
    --api-base-url)
      [[ $# -ge 2 ]] || fail "--api-base-url requires a URL."
      API_BASE_URL_VALUE="$2"
      shift 2
      ;;
    --require-frontend-runtime)
      REQUIRE_FRONTEND_RUNTIME=true
      shift
      ;;
    --require-bootstrap-admin)
      REQUIRE_BOOTSTRAP_ADMIN=true
      shift
      ;;
    --skip-docker-config)
      SKIP_DOCKER_CONFIG=true
      shift
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      fail "Unknown option: $1"
      ;;
  esac
done

[[ -f "$BASE_COMPOSE" ]] || fail "Base compose file not found: $BASE_COMPOSE"
[[ -f "$STAGING_OVERRIDE" ]] || fail "Staging override not found: $STAGING_OVERRIDE"
if [[ -n "$ENV_FILE" ]]; then
  [[ -f "$ENV_FILE" ]] || fail "Protected env file not found: $ENV_FILE"
  [[ ! -L "$ENV_FILE" ]] || fail "Protected env file must not be a symbolic link."
fi

require_https_origin "FRONTEND_STAGING_ORIGIN" "$FRONTEND_ORIGIN"
pass "FRONTEND_STAGING_ORIGIN has one exact HTTPS origin"

if [[ -n "$ENV_FILE" ]]; then
  env_origin="$(value_for FRONTEND_STAGING_ORIGIN)"
  [[ -n "$env_origin" ]] || fail "FRONTEND_STAGING_ORIGIN is missing from the protected env file."
  [[ "$env_origin" == "$FRONTEND_ORIGIN" ]] || \
    fail "--frontend-origin must exactly match FRONTEND_STAGING_ORIGIN in the protected env file."
fi

require_secret "MSSQL_SA_PASSWORD"
require_secret "JWT_SIGNING_KEY"
jwt_value="$(value_for JWT_SIGNING_KEY)"
(( ${#jwt_value} >= 32 )) || fail "JWT_SIGNING_KEY must contain at least 32 characters."
pass "JWT_SIGNING_KEY satisfies the minimum length (value withheld)"

if [[ "$REQUIRE_BOOTSTRAP_ADMIN" == true ]]; then
  require_secret "SEED_ADMIN_PASSWORD"
fi

if [[ "$REQUIRE_FRONTEND_RUNTIME" == true ]]; then
  require_https_origin "NEXT_PUBLIC_SITE_URL" "$FRONTEND_SITE_URL"
  [[ "$FRONTEND_SITE_URL" == "$FRONTEND_ORIGIN" ]] || \
    fail "NEXT_PUBLIC_SITE_URL must exactly match FRONTEND_STAGING_ORIGIN."
  require_api_base_url "$API_BASE_URL_VALUE"
  pass "NEXT_PUBLIC_SITE_URL matches the allowed browser origin"
  pass "API_BASE_URL has a valid internal server URL format"
else
  printf '%s\n' 'INFO: Frontend runtime comparison skipped. Use --require-frontend-runtime for a separately deployed Next.js frontend.'
fi

grep -Fq 'ASPNETCORE_ENVIRONMENT: Staging' "$STAGING_OVERRIDE" || fail "Override does not set ASPNETCORE_ENVIRONMENT=Staging."
grep -Fq 'Seed__Tv3LocalDemoData: "false"' "$STAGING_OVERRIDE" || fail "Override does not disable TV3 local seed."
grep -Fq 'Seed__VisualQaData: "false"' "$STAGING_OVERRIDE" || fail "Override does not disable Visual QA seed."
grep -Fq 'Cors__AllowedOrigins__0: ${FRONTEND_STAGING_ORIGIN:?' "$STAGING_OVERRIDE" || fail "Override does not map the required frontend origin to CORS."
pass "Staging override contains expected environment and CORS controls"

if [[ "$SKIP_DOCKER_CONFIG" == true ]]; then
  printf '%s\n' 'STAGING_PREFLIGHT=PASS (Docker config skipped by option)'
  exit 0
fi

command_exists docker || fail "Docker CLI is required. Use --skip-docker-config for syntax/input validation only."
docker compose version >/dev/null 2>&1 || fail "Docker Compose v2 is not available through 'docker compose'."

printf '%s\n' 'Running Docker Compose config with --no-interpolate; no resolved secret values will be printed.'
compose_command=(docker compose)
if [[ -n "$ENV_FILE" ]]; then
  compose_command+=(--env-file "$ENV_FILE")
fi
compose_command+=(-f "$BASE_COMPOSE" -f "$STAGING_OVERRIDE" config --no-interpolate)
rendered_config="$("${compose_command[@]}")" || \
  fail "docker compose config failed. Review YAML and variable declarations without exposing secrets."

grep -Fq 'ASPNETCORE_ENVIRONMENT: Staging' <<<"$rendered_config" || fail "Rendered config is missing ASPNETCORE_ENVIRONMENT=Staging."
grep -Fq 'Seed__Tv3LocalDemoData: "false"' <<<"$rendered_config" || fail "Rendered config is missing TV3 seed disablement."
grep -Fq 'Seed__VisualQaData: "false"' <<<"$rendered_config" || fail "Rendered config is missing Visual QA seed disablement."
grep -Fq 'Cors__AllowedOrigins__0:' <<<"$rendered_config" || fail "Rendered config is missing CORS origin mapping."

printf '%s\n' 'PASS: docker compose config --no-interpolate succeeded.'
printf '%s\n' 'STAGING_PREFLIGHT=PASS'
