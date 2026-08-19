#!/usr/bin/env bash
# TV3/Duy Contact Request v53 local integration helper.
# It NEVER runs git add/commit/push, gh, curl POST, or any GitHub write operation.
set -Eeuo pipefail

REPO_DIR=""
SOURCE_DIR=""
BRANCH_NAME="feature/contact-request-management"
APPLY=false
AUDIT_DIFF=false
RUN_VALIDATION=false
SKIP_FETCH=false

usage() {
  cat <<'EOF'
Usage:
  bash scripts/Apply-ContactV53ToLocalDev.sh \
    --repo /absolute/path/to/official-local-clone \
    --source /absolute/path/to/TV3-Duy-Contact-Source \
    [--branch feature/contact-request-management] [--apply] [--audit-diff] \
    [--run-validation] [--skip-fetch]

Safety model:
  * Default mode validates only; it does not switch branches or copy files.
  * --apply fetches/switches the LOCAL clone to dev, creates a LOCAL feature branch,
    then copies only TV3 Contact non-shared files.
  * Shared files are never copied. Merge their documented hunks manually, then use
    --audit-diff to review the LOCAL working-tree diff against origin/dev.
  * --run-validation is local-only (restore/build/test/npm/lint/build). It is allowed
    only with --audit-diff after the shared hunks have been merged manually.
  * This script never writes to GitHub or deletes remote/local repository history.

Examples:
  # Prerequisite/source audit only, no local clone change:
  bash scripts/Apply-ContactV53ToLocalDev.sh \
    --repo /work/CloudServiceStore \
    --source /work/TV3-Duy-Contact-Source

  # Create a local branch and copy non-shared Contact source; then stop for hunks:
  bash scripts/Apply-ContactV53ToLocalDev.sh \
    --repo /work/CloudServiceStore \
    --source /work/TV3-Duy-Contact-Source \
    --apply

  # After manual shared-hunk merge, audit exact local diff and run local validation:
  bash scripts/Apply-ContactV53ToLocalDev.sh \
    --repo /work/CloudServiceStore \
    --source /work/TV3-Duy-Contact-Source \
    --audit-diff --run-validation
EOF
}

fail() { printf 'CONTACT_V53_LOCAL=FAIL: %s\n' "$*" >&2; exit 1; }
info() { printf 'INFO: %s\n' "$*"; }
pass() { printf 'PASS: %s\n' "$*"; }

while [[ $# -gt 0 ]]; do
  case "$1" in
    --repo) [[ $# -ge 2 ]] || fail '--repo requires a path.'; REPO_DIR="$2"; shift 2 ;;
    --source) [[ $# -ge 2 ]] || fail '--source requires a path.'; SOURCE_DIR="$2"; shift 2 ;;
    --branch) [[ $# -ge 2 ]] || fail '--branch requires a name.'; BRANCH_NAME="$2"; shift 2 ;;
    --apply) APPLY=true; shift ;;
    --audit-diff) AUDIT_DIFF=true; shift ;;
    --run-validation) RUN_VALIDATION=true; shift ;;
    --skip-fetch) SKIP_FETCH=true; shift ;;
    --help|-h) usage; exit 0 ;;
    *) fail "Unknown option: $1" ;;
  esac
done

[[ -n "$REPO_DIR" ]] || fail '--repo is required.'
[[ -n "$SOURCE_DIR" ]] || fail '--source is required.'
[[ "$APPLY" == true && "$AUDIT_DIFF" == true ]] && fail 'Run --apply first; audit after manual hunk merge in a separate command.'
[[ "$RUN_VALIDATION" == true && "$AUDIT_DIFF" != true ]] && fail '--run-validation requires --audit-diff after manual shared-hunk merge.'

REPO_DIR="$(cd -- "$REPO_DIR" && pwd)"
SOURCE_DIR="$(cd -- "$SOURCE_DIR" && pwd)"
git -C "$REPO_DIR" rev-parse --is-inside-work-tree >/dev/null 2>&1 || fail '--repo is not a Git worktree.'
[[ -f "$REPO_DIR/CloudServiceStore.sln" ]] || fail 'CloudServiceStore.sln is missing from --repo.'
[[ -f "$SOURCE_DIR/docs/README.md" ]] || fail '--source is not a TV3-Duy-Contact-Source package.'
[[ -f "$SOURCE_DIR/docs/TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md" ]] || fail 'Shared hunk guide is missing from --source.'

contact_files=(
  docker-compose.contact-empty.yml
  frontend/e2e/contact-public.spec.ts
  frontend/e2e/contact-responsive-evidence.spec.ts
  frontend/playwright.config.ts
  frontend/src/app/admin/contact-requests/page.tsx
  frontend/src/app/contact/page.tsx
  frontend/src/components/admin-contact-requests-client.tsx
  frontend/src/components/contact-public-client.tsx
  frontend/src/lib/contact-request-status.ts
  scripts/Apply-ContactV53ToLocalDev.sh
  scripts/Build-And-AddContactRequestMigration.ps1
  scripts/Check-ContactControllerArchitecture.sh
  scripts/Check-StagingCorsCompose.sh
  scripts/Export-TV3ContactPreCommitPatch.ps1
  scripts/Run-ContactE2ELowResource.sh
  scripts/Run-ContactRequestEmptyDatabase.ps1
  scripts/Start-ContactRequestDemo.ps1
  scripts/Test-ContactRequestMigrationSafety.ps1
  scripts/Test-ContactRequestPrePr.ps1
  scripts/Test-ContactRequestStagingSmoke.ps1
  src/CloudServiceStore.Application/ContactRequests/ContactRequestAbstractions.cs
  src/CloudServiceStore.Application/ContactRequests/ContactRequestContracts.cs
  src/CloudServiceStore.Application/ContactRequests/ContactRequestService.cs
  src/CloudServiceStore.Domain/Entities/ContactRequestEntities.cs
  src/CloudServiceStore.Domain/Enums/ContactRequestStatus.cs
  src/CloudServiceStore.Infrastructure/Persistence/Configurations/ContactRequestConfigurations.cs
  src/CloudServiceStore.Infrastructure/Persistence/ContactRequestRepository.cs
  src/CloudServiceStore.WebApi/Controllers/ContactRequestsController.cs
  src/CloudServiceStore.WebApi/Security/ContactRequestRateLimitExtensions.cs
  tests/CloudServiceStore.Application.Tests/ContactRequestServiceTests.cs
  tests/CloudServiceStore.Integration.Tests/ContactRequestQueryApiTests.cs
  tests/CloudServiceStore.Integration.Tests/ContactRequestRateLimitIntegrationTests.cs
  tests/CloudServiceStore.Integration.Tests/ContactRequestRateLimitOptionsTests.cs
  tests/CloudServiceStore.Integration.Tests/ContactRequestSqlServerMigrationTests.cs
  tests/CloudServiceStore.Integration.Tests/ContactRequestsControllerTests.cs
  tests/postman/CloudServiceStore_TV3.postman_collection.json
  tests/postman/CloudServiceStore_TV3_Local.postman_environment.json
)

shared_files=(
  src/CloudServiceStore.Infrastructure/Persistence/CloudServiceStoreDbContext.cs
  src/CloudServiceStore.Infrastructure/DependencyInjection.cs
  src/CloudServiceStore.WebApi/Program.cs
  src/CloudServiceStore.WebApi/appsettings.json
  frontend/src/lib/api.ts
  frontend/src/components/admin/admin-nav.ts
  docker-compose.yml
)

is_allowed_changed_path() {
  local candidate="$1"
  local allowed
  for allowed in "${contact_files[@]}" "${shared_files[@]}"; do
    [[ "$candidate" == "$allowed" ]] && return 0
  done

  case "$candidate" in
    src/CloudServiceStore.Infrastructure/Persistence/Migrations/*)
      # The generated migration and its designer/snapshot must still be reviewed manually.
      return 0
      ;;
    docs/TV3_*|docs/CONTACT_REQUEST_*)
      # TV3-owned documentation is permitted, but raw evidence remains prohibited by policy.
      return 0
      ;;
    *) return 1 ;;
  esac
}

shared_hunk_pattern() {
  case "$1" in
    src/CloudServiceStore.Infrastructure/Persistence/CloudServiceStoreDbContext.cs)
      printf '%s' 'ContactRequest'
      ;;
    src/CloudServiceStore.Infrastructure/DependencyInjection.cs)
      printf '%s' 'ContactRequest'
      ;;
    src/CloudServiceStore.WebApi/Program.cs)
      printf '%s' 'ContactRequest|ManageContactRequests|contact-requests'
      ;;
    src/CloudServiceStore.WebApi/appsettings.json)
      printf '%s' 'ContactPermitLimit|ContactRequest|Tv3LocalDemoData'
      ;;
    frontend/src/lib/api.ts)
      printf '%s' 'ContactRequest|contactRequests'
      ;;
    frontend/src/components/admin/admin-nav.ts)
      printf '%s' 'contact-requests|IconMessageCircle|Contact'
      ;;
    docker-compose.yml)
      printf '%s' 'migrator|Contact|healthcheck'
      ;;
    *) return 1 ;;
  esac
}

for relative_path in "${contact_files[@]}"; do
  [[ -f "$SOURCE_DIR/$relative_path" ]] || fail "Package is missing required Contact file: $relative_path"
done
pass 'v53 source manifest is complete'

if [[ "$APPLY" == false && "$AUDIT_DIFF" == false ]]; then
  printf 'CONTACT_V53_LOCAL=DRY_RUN_PASS (no local branch/file changed; no GitHub action performed)\n'
  exit 0
fi

if [[ "$APPLY" == true ]]; then
  [[ -z "$(git -C "$REPO_DIR" status --porcelain)" ]] || fail 'Local clone has uncommitted changes; commit/stash them outside this script first.'
  if [[ "$SKIP_FETCH" == false ]]; then git -C "$REPO_DIR" fetch --prune origin; fi
  git -C "$REPO_DIR" show-ref --verify --quiet refs/remotes/origin/dev || fail 'origin/dev is unavailable; verify the official local clone and fetch permission.'
  git -C "$REPO_DIR" switch dev
  if [[ "$SKIP_FETCH" == false ]]; then git -C "$REPO_DIR" pull --ff-only origin dev; fi
  git -C "$REPO_DIR" show-ref --verify --quiet "refs/heads/$BRANCH_NAME" && fail "Local branch already exists: $BRANCH_NAME"
  git -C "$REPO_DIR" switch -c "$BRANCH_NAME"
  pass "Created local branch $BRANCH_NAME from local dev"
  for relative_path in "${contact_files[@]}"; do
    target="$REPO_DIR/$relative_path"
    mkdir -p "$(dirname "$target")"
    cp -p "$SOURCE_DIR/$relative_path" "$target"
    printf 'COPIED: %s\n' "$relative_path"
  done
  chmod 755 "$REPO_DIR/scripts/Apply-ContactV53ToLocalDev.sh" "$REPO_DIR/scripts/Check-ContactControllerArchitecture.sh" "$REPO_DIR/scripts/Check-StagingCorsCompose.sh" "$REPO_DIR/scripts/Run-ContactE2ELowResource.sh" 2>/dev/null || true
  printf '%s\n' '=== REQUIRED MANUAL SHARED-HUNK STEP ==='
  printf '%s\n' "Do NOT copy these whole files:"
  printf '  %s\n' "${shared_files[@]}"
  printf '%s\n' 'Merge only documented Contact hunks from docs/TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md, then rerun with --audit-diff.'
  printf '%s\n' 'CONTACT_V53_LOCAL=APPLIED_LOCAL_ONLY (no commit/push/PR/GitHub write performed)'
  exit 0
fi

[[ "$(git -C "$REPO_DIR" branch --show-current)" == "$BRANCH_NAME" ]] || fail "--audit-diff requires current branch $BRANCH_NAME"
if [[ "$SKIP_FETCH" == false ]]; then git -C "$REPO_DIR" fetch --prune origin; fi
git -C "$REPO_DIR" show-ref --verify --quiet refs/remotes/origin/dev || fail 'origin/dev is unavailable.'

printf '%s\n' '=== LOCAL DIFF AGAINST origin/dev ==='
git -C "$REPO_DIR" diff --check origin/dev
mapfile -t tracked_changed < <(git -C "$REPO_DIR" diff --name-only origin/dev)
mapfile -t untracked_changed < <(git -C "$REPO_DIR" ls-files --others --exclude-standard)
printf '%s\n' '--- Tracked changes compared with origin/dev ---'
printf '%s\n' "${tracked_changed[@]:-<none>}"
printf '%s\n' '--- Untracked files (also audited before any git add) ---'
printf '%s\n' "${untracked_changed[@]:-<none>}"

printf '%s\n' '=== CONTACT SCOPE ALLOWLIST AUDIT ==='
out_of_scope=0
mapfile -t all_changed < <(printf '%s\n' "${tracked_changed[@]}" "${untracked_changed[@]}" | sed '/^$/d' | sort -u)
for changed_path in "${all_changed[@]}"; do
  if is_allowed_changed_path "$changed_path"; then
    printf 'ALLOWED: %s\n' "$changed_path"
  else
    printf 'OUT_OF_SCOPE: %s\n' "$changed_path" >&2
    out_of_scope=1
  fi
done
[[ "$out_of_scope" -eq 0 ]] || fail 'Remove or split all OUT_OF_SCOPE paths before migration/commit.'

printf '%s\n' '=== IMPORTANT SHARED FILE DIFFS ==='
for shared_path in "${shared_files[@]}"; do
  printf '%s\n' "--- $shared_path ---"
  shared_diff="$(git -C "$REPO_DIR" diff --unified=0 origin/dev -- "$shared_path" || true)"
  if [[ -z "$shared_diff" ]]; then
    printf '%s\n' '<no shared hunk changed>'
    continue
  fi
  printf '%s\n' "$shared_diff"
  pattern="$(shared_hunk_pattern "$shared_path")"
  if ! grep -Eq "$pattern" <<< "$shared_diff"; then
    fail "Shared file '$shared_path' changed but diff has no expected Contact indicator; review/remove it."
  fi
  printf 'SHARED_HUNK_HEURISTIC=PASS: %s\n' "$shared_path"
done

printf '%s\n' '=== OUT-OF-SCOPE NAME GUARD ==='
if printf '%s\n' "${all_changed[@]}" | grep -E 'AuthSecurityExtensions|site-header|/News/|/Landing/|/Customer|/Orders/|/Affiliates/'; then
  fail 'Out-of-scope changed file name detected. Remove it before migration/commit.'
fi
pass 'No obvious out-of-scope file name detected; human review of full diff remains mandatory'

if [[ "$RUN_VALIDATION" == true ]]; then
  command -v dotnet >/dev/null 2>&1 || fail 'dotnet SDK is required for --run-validation.'
  command -v npm >/dev/null 2>&1 || fail 'npm is required for --run-validation.'
  (
    cd "$REPO_DIR"
    dotnet restore CloudServiceStore.sln
    dotnet build CloudServiceStore.sln --configuration Release --no-restore
    dotnet test CloudServiceStore.sln --configuration Release --no-build --no-restore
    bash scripts/Check-ContactControllerArchitecture.sh
    cd frontend
    npm ci
    npm run lint
    NODE_ENV=production npm run build
  )
  pass 'Local backend/frontend validation completed; this is not GitHub CI evidence.'
fi

git -C "$REPO_DIR" status --short
printf '%s\n' 'CONTACT_V53_LOCAL=AUDIT_PASS_LOCAL_ONLY'
printf '%s\n' 'No git add/commit/push, GitHub CLI, remote PR action, or remote deletion was performed.'
