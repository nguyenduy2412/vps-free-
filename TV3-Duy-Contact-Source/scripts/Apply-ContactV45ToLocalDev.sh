#!/usr/bin/env bash
# TV3/Duy Contact Request v45 local integration helper.
# This script never executes git push, git commit, GitHub CLI, or PR operations.
set -Eeuo pipefail

readonly SCRIPT_NAME="$(basename "$0")"
REPO_DIR=""
SOURCE_DIR=""
BRANCH_NAME="feature/contact-request-management"
APPLY=false
RUN_VALIDATION=false
RUN_E2E=false
SKIP_FETCH=false
VALIDATE_EXISTING=false

usage() {
  cat <<'EOF'
Usage:
  bash scripts/Apply-ContactV45ToLocalDev.sh \
    --repo /absolute/path/to/official-local-clone \
    --source /absolute/path/to/TV3-Duy-Only-Contact-Source-v15 \
    [--branch feature/contact-request-management] [--apply | --validate-existing] [--run-validation] [--run-e2e] [--skip-fetch]

Safety model:
  * Without --apply, the script only validates paths, source scope, and Git prerequisites.
  * With --apply, it fetches/switches local branch dev, creates a local feature branch,
    and copies only Contact-only files. It never copies shared files automatically.
  * With --validate-existing, it runs validation on an existing local feature branch
    after documented shared hunks have been merged manually.
  * It never runs git add, git commit, git push, GitHub CLI, or any PR action.

Examples:
  # Review prerequisites first (no local clone changes):
  bash scripts/Apply-ContactV45ToLocalDev.sh \
    --repo /work/CloudServiceStore \
    --source /work/TV3-Duy-Only-Contact-Source-v15

  # Apply source locally, then stop for manual shared-hunk merge:
  bash scripts/Apply-ContactV45ToLocalDev.sh \
    --repo /work/CloudServiceStore \
    --source /work/TV3-Duy-Only-Contact-Source-v15 \
    --apply

  # After manual shared-hunk merge, validate the existing local feature branch:
  bash scripts/Apply-ContactV45ToLocalDev.sh \
    --repo /work/CloudServiceStore \
    --source /work/TV3-Duy-Only-Contact-Source-v15 \
    --validate-existing --run-validation --run-e2e
EOF
}

fail() {
  printf 'CONTACT_V45_APPLY=FAIL: %s\n' "$*" >&2
  exit 1
}

info() {
  printf 'INFO: %s\n' "$*"
}

pass() {
  printf 'PASS: %s\n' "$*"
}

require_value() {
  local option="$1" value="$2"
  [[ -n "$value" ]] || fail "$option requires a value."
}

copy_contact_file() {
  local relative_path="$1"
  local source_path="$SOURCE_DIR/$relative_path"
  local target_path="$REPO_DIR/$relative_path"
  [[ -f "$source_path" ]] || fail "Expected Contact v45 source file is missing: $source_path"
  mkdir -p "$(dirname "$target_path")"
  cp -p "$source_path" "$target_path"
  printf 'COPIED: %s\n' "$relative_path"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --repo)
      [[ $# -ge 2 ]] || fail "--repo requires a path."
      REPO_DIR="$2"
      shift 2
      ;;
    --source)
      [[ $# -ge 2 ]] || fail "--source requires a path."
      SOURCE_DIR="$2"
      shift 2
      ;;
    --branch)
      [[ $# -ge 2 ]] || fail "--branch requires a name."
      BRANCH_NAME="$2"
      shift 2
      ;;
    --apply)
      APPLY=true
      shift
      ;;
    --validate-existing)
      VALIDATE_EXISTING=true
      shift
      ;;
    --run-validation)
      RUN_VALIDATION=true
      shift
      ;;
    --run-e2e)
      RUN_E2E=true
      shift
      ;;
    --skip-fetch)
      SKIP_FETCH=true
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

require_value "--repo" "$REPO_DIR"
require_value "--source" "$SOURCE_DIR"
[[ "$APPLY" == true && "$VALIDATE_EXISTING" == true ]] && \
  fail "Use either --apply or --validate-existing, not both."
if [[ "$RUN_VALIDATION" == true || "$RUN_E2E" == true ]]; then
  [[ "$APPLY" == true || "$VALIDATE_EXISTING" == true ]] || \
    fail "Use --validate-existing to run validation without copying source."
fi
if [[ "$APPLY" == true && ( "$RUN_VALIDATION" == true || "$RUN_E2E" == true ) ]]; then
  fail "--apply only copies Contact files. Merge shared hunks manually, then rerun with --validate-existing and validation options."
fi
REPO_DIR="$(cd -- "$REPO_DIR" && pwd)"
SOURCE_DIR="$(cd -- "$SOURCE_DIR" && pwd)"

git -C "$REPO_DIR" rev-parse --is-inside-work-tree >/dev/null 2>&1 || \
  fail "--repo is not a Git worktree: $REPO_DIR"
[[ -f "$REPO_DIR/CloudServiceStore.sln" ]] || fail "CloudServiceStore.sln is missing in --repo."
[[ -f "$REPO_DIR/frontend/package.json" ]] || fail "frontend/package.json is missing in --repo."
[[ -f "$SOURCE_DIR/docs/TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md" ]] || \
  fail "--source does not look like the TV3 Contact v45 package."

printf '%s\n' '=== Contact v45 source files to copy automatically ==='
contact_files=(
  "docker-compose.contact-empty.yml"
  "frontend/e2e/contact-public.spec.ts"
  "frontend/src/app/admin/contact-requests/page.tsx"
  "frontend/src/app/contact/page.tsx"
  "frontend/src/components/admin-contact-requests-client.tsx"
  "frontend/src/components/contact-public-client.tsx"
  "frontend/src/lib/contact-request-status.ts"
  "scripts/Build-And-AddContactRequestMigration.ps1"
  "scripts/Check-StagingCorsCompose.sh"
  "scripts/Export-TV3ContactPreCommitPatch.ps1"
  "scripts/Run-ContactRequestEmptyDatabase.ps1"
  "scripts/Start-ContactRequestDemo.ps1"
  "scripts/Test-ContactRequestMigrationSafety.ps1"
  "scripts/Test-ContactRequestPrePr.ps1"
  "scripts/Test-ContactRequestStagingSmoke.ps1"
  "src/CloudServiceStore.Application/ContactRequests/ContactRequestAbstractions.cs"
  "src/CloudServiceStore.Application/ContactRequests/ContactRequestContracts.cs"
  "src/CloudServiceStore.Application/ContactRequests/ContactRequestService.cs"
  "src/CloudServiceStore.Domain/Entities/ContactRequestEntities.cs"
  "src/CloudServiceStore.Domain/Enums/ContactRequestStatus.cs"
  "src/CloudServiceStore.Infrastructure/Persistence/Configurations/ContactRequestConfigurations.cs"
  "src/CloudServiceStore.Infrastructure/Persistence/ContactRequestRepository.cs"
  "src/CloudServiceStore.WebApi/Controllers/ContactRequestsController.cs"
  "src/CloudServiceStore.WebApi/Security/ContactRequestRateLimitExtensions.cs"
  "tests/CloudServiceStore.Application.Tests/ContactRequestServiceTests.cs"
  "tests/CloudServiceStore.Integration.Tests/ContactRequestQueryApiTests.cs"
  "tests/CloudServiceStore.Integration.Tests/ContactRequestRateLimitIntegrationTests.cs"
  "tests/CloudServiceStore.Integration.Tests/ContactRequestSqlServerMigrationTests.cs"
  "tests/CloudServiceStore.Integration.Tests/ContactRequestsControllerTests.cs"
  "tests/postman/CloudServiceStore_TV3.postman_collection.json"
  "tests/postman/CloudServiceStore_TV3_Local.postman_environment.json"
)
printf '%s\n' "${contact_files[@]}"

printf '%s\n' '=== Shared files deliberately NOT copied by this script ==='
cat <<'EOF'
src/CloudServiceStore.Infrastructure/Persistence/CloudServiceStoreDbContext.cs
src/CloudServiceStore.Infrastructure/DependencyInjection.cs
src/CloudServiceStore.WebApi/Program.cs
src/CloudServiceStore.WebApi/appsettings.json
frontend/src/lib/api.ts
docker-compose.yml
EOF
printf '%s\n' 'Merge only the documented TV3 hunks in docs/TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md.'

for relative_path in "${contact_files[@]}"; do
  [[ -f "$SOURCE_DIR/$relative_path" ]] || fail "Missing Contact source: $relative_path"
done
pass "Source manifest is complete"

if [[ "$APPLY" != true && "$VALIDATE_EXISTING" != true ]]; then
  printf '%s\n' 'CONTACT_V45_APPLY=DRY_RUN_PASS (no branch was created and no file was copied)'
  exit 0
fi

if [[ "$VALIDATE_EXISTING" == true ]]; then
  [[ "$(git -C "$REPO_DIR" branch --show-current)" == "$BRANCH_NAME" ]] || \
    fail "--validate-existing requires current branch $BRANCH_NAME."
  printf '%s\n' 'INFO: Existing feature branch validation selected; no source file will be copied.'
fi

if [[ "$APPLY" == true ]]; then
  [[ -z "$(git -C "$REPO_DIR" status --porcelain)" ]] || \
    fail "Local clone has uncommitted changes. Commit/stash them outside this script before applying Contact v45."

  if [[ "$SKIP_FETCH" != true ]]; then
    git -C "$REPO_DIR" fetch --prune origin
  fi
  git -C "$REPO_DIR" switch dev
  if [[ "$SKIP_FETCH" != true ]]; then
    git -C "$REPO_DIR" pull --ff-only origin dev
  fi

  if git -C "$REPO_DIR" show-ref --verify --quiet "refs/heads/$BRANCH_NAME"; then
    fail "Local branch already exists: $BRANCH_NAME. Use a new branch name or inspect it manually."
  fi
  git -C "$REPO_DIR" switch -c "$BRANCH_NAME"
  pass "Created local branch $BRANCH_NAME from current dev"

  for relative_path in "${contact_files[@]}"; do
    copy_contact_file "$relative_path"
  done
  chmod 755 "$REPO_DIR/scripts/Check-StagingCorsCompose.sh" 2>/dev/null || true

  printf '%s\n' '=== Required manual step before validation ==='
  printf '%s\n' 'Merge shared hunks now; the script intentionally stopped before copying any shared file.'
  printf '%s\n' 'Guide: docs/TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md'
  printf '%s\n' 'Then rerun with --validate-existing --run-validation (and optionally --run-e2e) after hunks are complete.'
  printf '%s\n' 'CONTACT_V45_APPLY=APPLIED_LOCAL_ONLY'
  exit 0
fi

if [[ "$RUN_VALIDATION" == true ]]; then
  command -v dotnet >/dev/null 2>&1 || fail "dotnet SDK is required for --run-validation."
  command -v npm >/dev/null 2>&1 || fail "npm is required for --run-validation."
  (
    cd "$REPO_DIR"
    dotnet restore CloudServiceStore.sln
    dotnet build CloudServiceStore.sln --configuration Release --no-restore
    dotnet test CloudServiceStore.sln --configuration Release --no-build --no-restore
    cd frontend
    npm ci
    npm run lint
    NODE_ENV=production npm run build
  )
  pass "Backend/frontend validation commands completed"
fi

if [[ "$RUN_E2E" == true ]]; then
  command -v npm >/dev/null 2>&1 || fail "npm is required for --run-e2e."
  (
    cd "$REPO_DIR/frontend"
    env -u NODE_ENV npm run test:e2e:contact
  )
  pass "Contact E2E completed across configured viewports, including 360px"
fi

printf '%s\n' '=== Scope audit ==='
git -C "$REPO_DIR" diff --check
git -C "$REPO_DIR" status --short
printf '%s\n' 'CONTACT_V45_APPLY=PASS_LOCAL_ONLY'
printf '%s\n' 'No commit/push/PR was performed. Review migration checklist before Docker DB rỗng.'
