[CmdletBinding()]
param(
    [string]$BaseRef = 'origin/dev',
    [string]$OutputDirectory = 'artifacts/pre-commit-contact'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$outputPath = Join-Path $repositoryRoot $OutputDirectory

function Require-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found."
    }
}

function Invoke-Checked([string]$Step, [scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed with exit code $LASTEXITCODE."
    }
}

function Get-FileKind([string]$Path) {
    if ($Path -in @(
        'src/CloudServiceStore.Infrastructure/Persistence/CloudServiceStoreDbContext.cs',
        'src/CloudServiceStore.Infrastructure/DependencyInjection.cs',
        'src/CloudServiceStore.WebApi/Program.cs',
        'src/CloudServiceStore.WebApi/appsettings.json',
        'frontend/src/lib/api.ts',
        'docker-compose.yml')) {
        return 'shared-hunk-review-required'
    }

    if ($Path -match '^(src/CloudServiceStore.Domain/(Entities/ContactRequestEntities\.cs|Enums/ContactRequestStatus\.cs)|src/CloudServiceStore.Application/ContactRequests/|src/CloudServiceStore.Infrastructure/Persistence/(Configurations/ContactRequestConfigurations\.cs|ContactRequestRepository\.cs)|src/CloudServiceStore.WebApi/(Controllers/ContactRequestsController\.cs|Security/ContactRequestRateLimitExtensions\.cs)|frontend/src/(app/contact/|app/admin/contact-requests/|components/(contact-public-client|admin-contact-requests-client)\.tsx|lib/contact-request-status\.ts)|frontend/(e2e/contact-public\.spec\.ts|playwright\.config\.ts)|tests/CloudServiceStore.Application.Tests/ContactRequestServiceTests\.cs|tests/CloudServiceStore.Integration.Tests/ContactRequest(.*)\.cs|scripts/(Build-And-AddContactRequestMigration|Run-ContactRequestEmptyDatabase|Start-ContactRequestDemo|Test-ContactRequestPrePr|Test-ContactRequestMigrationSafety|Export-TV3ContactPreCommitPatch)\.ps1|tests/postman/CloudServiceStore_TV3.*\.json|docs/(TV3_|CONTACT_REQUEST_))') {
        return 'tv3-contact-allowed'
    }

    return 'outside-tv3-scope'
}

Require-Command 'git'
if (-not (Test-Path (Join-Path $repositoryRoot '.git'))) {
    throw 'Run this script from the official Git clone. The extracted TV3 snapshot has no .git, so it cannot produce a truthful diff against dev.'
}

Push-Location $repositoryRoot
try {
    $branch = (& git branch --show-current).Trim()
    if ($LASTEXITCODE -ne 0 -or $branch -ne 'feature/contact-request-management') {
        throw "Current branch is '$branch'. Switch to feature/contact-request-management before exporting the TV3 pre-commit patch."
    }

    Invoke-Checked "Fetch '$BaseRef'" { git fetch --prune origin }
    Invoke-Checked "Verify '$BaseRef'" { git rev-parse --verify $BaseRef }

    $untracked = @(& git ls-files --others --exclude-standard)
    if ($untracked.Count -gt 0) {
        $untrackedList = $untracked -join [Environment]::NewLine
        throw "Untracked files are not represented by git diff. Review/add them intentionally, then rerun. Untracked:`n$untrackedList"
    }

    Invoke-Checked 'Check whitespace against dev' { git diff --check $BaseRef -- }
    if (Test-Path $outputPath) { Remove-Item $outputPath -Recurse -Force }
    New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

    $patchPath = Join-Path $outputPath 'TV3_CONTACT_PRECOMMIT_VS_DEV.patch'
    $filesPath = Join-Path $outputPath 'TV3_CONTACT_PRECOMMIT_FILES.md'
    $summaryPath = Join-Path $outputPath 'TV3_CONTACT_PRECOMMIT_SUMMARY.md'

    $patchLines = @(& git diff --binary --full-index --find-renames $BaseRef --)
    if ($LASTEXITCODE -ne 0) { throw "Patch export failed with exit code $LASTEXITCODE." }
    [System.IO.File]::WriteAllText(
        $patchPath,
        ($patchLines -join [Environment]::NewLine) + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))

    $changedFiles = @(& git diff --name-only $BaseRef --)
    $classified = $changedFiles | ForEach-Object {
        [PSCustomObject]@{ Path = $_; Kind = Get-FileKind $_ }
    }
    $outsideScope = @($classified | Where-Object Kind -eq 'outside-tv3-scope')
    $sharedFiles = @($classified | Where-Object Kind -eq 'shared-hunk-review-required')

    $fileReport = @(
        '# TV3 Contact pre-commit changed files',
        '',
        '| Path | Classification |',
        '|---|---|'
    )
    $fileReport += $classified | Sort-Object Path | ForEach-Object { "| ``$($_.Path)`` | $($_.Kind) |" }
    Set-Content -Path $filesPath -Value $fileReport -Encoding utf8

    $summary = @(
        '# TV3 Contact pre-commit patch summary',
        '',
        "- Branch: `$branch`",
        "- Base: `$BaseRef`",
        "- Head: $((& git rev-parse --short HEAD).Trim())",
        "- Changed files: $($changedFiles.Count)",
        "- Shared files requiring exact-hunk review: $($sharedFiles.Count)",
        "- Outside-TV3-scope files: $($outsideScope.Count)",
        "- Patch: ``$patchPath``",
        '',
        '## Required decision',
        ''
    )
    if ($outsideScope.Count -gt 0) {
        $summary += 'STOP: remove or obtain documented approval for the following files before commit:'
        $summary += $outsideScope | ForEach-Object { "- ``$($_.Path)``" }
    }
    else {
        $summary += 'PASS: no file outside the declared TV3 Contact scope was detected by this script.'
    }
    if ($sharedFiles.Count -gt 0) {
        $summary += '', '## Shared-file review', ''
        $summary += 'Review every shared diff against `docs/TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md`; preserve all dev code and retain only Contact hunk(s).'
        $summary += $sharedFiles | ForEach-Object { "- ``$($_.Path)``" }
    }
    $summary += '', '## Final commands', '', '```bash', 'git diff --check origin/dev --', 'git diff --name-only origin/dev --', 'git diff -- origin/dev --', '```'
    Set-Content -Path $summaryPath -Value $summary -Encoding utf8

    Write-Host "Patch exported: $patchPath" -ForegroundColor Green
    Write-Host "File manifest: $filesPath" -ForegroundColor Green
    Write-Host "Summary: $summaryPath" -ForegroundColor Green
    if ($outsideScope.Count -gt 0) { exit 2 }
}
finally {
    Pop-Location
}
