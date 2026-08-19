[CmdletBinding()]
param(
    [string]$BaseRef = 'origin/dev',
    [switch]$SkipBuild,
    [switch]$RunSqlServerApply,
    [string]$ResultsDirectory = 'artifacts/migration-safety-contact'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot 'CloudServiceStore.sln'
$infrastructureProject = Join-Path $repositoryRoot 'src/CloudServiceStore.Infrastructure/CloudServiceStore.Infrastructure.csproj'
$startupProject = Join-Path $repositoryRoot 'src/CloudServiceStore.WebApi/CloudServiceStore.WebApi.csproj'
$integrationProject = Join-Path $repositoryRoot 'tests/CloudServiceStore.Integration.Tests/CloudServiceStore.Integration.Tests.csproj'
$migrationsRelativePath = 'src/CloudServiceStore.Infrastructure/Persistence/Migrations'
$migrationsPath = Join-Path $repositoryRoot $migrationsRelativePath
$resultsPath = Join-Path $repositoryRoot $ResultsDirectory

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

function Write-Step([string]$Message) {
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

Require-Command 'dotnet'
Require-Command 'git'
if (-not (Test-Path $solutionPath)) { throw "Solution not found: $solutionPath" }
if (-not (Test-Path $infrastructureProject)) { throw "Infrastructure project not found: $infrastructureProject" }
if (-not (Test-Path $startupProject)) { throw "Startup project not found: $startupProject" }
if (-not (Test-Path $integrationProject)) { throw "Integration test project not found: $integrationProject" }
if (-not (Test-Path $migrationsPath)) { throw "Migrations directory not found: $migrationsPath" }

Push-Location $repositoryRoot
try {
    $isGitWorkTree = (& git rev-parse --is-inside-work-tree 2>$null)
    if ($LASTEXITCODE -ne 0 -or $isGitWorkTree -ne 'true') {
        throw 'Run this script from a real Git clone so migration diff can be compared with dev.'
    }

    Invoke-Checked "Fetch base ref '$BaseRef'" { git fetch --prune origin dev }
    Invoke-Checked 'Verify base ref exists' { git rev-parse --verify $BaseRef }
    Invoke-Checked 'Check diff whitespace' { git diff --check "$BaseRef...HEAD" }

    $branch = (& git branch --show-current).Trim()
    if ([string]::IsNullOrWhiteSpace($branch) -or $branch -eq 'main') {
        throw 'Run migration safety on a feature branch or dev, never directly on main.'
    }

    if (-not $SkipBuild) {
        Write-Step 'Restore and build before inspecting migration'
        Invoke-Checked '.NET restore' { dotnet restore $solutionPath }
        Invoke-Checked '.NET Release build' { dotnet build $solutionPath --configuration Release --no-restore }
    }

    Write-Step 'Inspect migration diff against dev'
    $changedMigrationFiles = @(
        & git diff --name-only "$BaseRef...HEAD" -- $migrationsRelativePath |
            Where-Object { $_ -match '\.cs$' -and $_ -notmatch '\.Designer\.cs$' -and $_ -notmatch 'ModelSnapshot\.cs$' }
    )
    if ($changedMigrationFiles.Count -eq 0) {
        throw "No generated migration .cs file differs from $BaseRef. Generate and review Contact migration before merge."
    }
    if ($changedMigrationFiles.Count -ne 1) {
        throw "Expected exactly one generated Contact migration .cs file relative to $BaseRef; found $($changedMigrationFiles.Count). Split/review unrelated migrations before merge."
    }

    $allowedContactTables = @('ContactRequests', 'ContactRequestStatusHistories')
    $allowedPrincipalTables = @('AppUsers')
    $migrationFindings = New-Object System.Collections.Generic.List[string]

    foreach ($relativeFile in $changedMigrationFiles) {
        $fullPath = Join-Path $repositoryRoot $relativeFile
        if (-not (Test-Path $fullPath)) { throw "Changed migration file is missing: $relativeFile" }
        $content = Get-Content -Raw -Path $fullPath

        if ($content -notmatch 'ContactRequests' -or $content -notmatch 'ContactRequestStatusHistories') {
            throw "Migration $relativeFile does not contain both required Contact tables."
        }

        if ($content -match 'migrationBuilder\.Sql\s*\(') {
            throw "Migration safety failed: raw SQL is not permitted in Contact migration $relativeFile."
        }

        $tableMatches = [regex]::Matches($content, '(?s)(?:CreateTable|DropTable)\s*\(\s*name:\s*"(?<table>[^"]+)"')
        foreach ($match in $tableMatches) {
            $table = $match.Groups['table'].Value
            if ($table -notin $allowedContactTables) {
                throw "Migration safety failed: table '$table' in $relativeFile is outside Contact scope."
            }
        }

        $operationTableMatches = [regex]::Matches($content, 'table:\s*"(?<table>[^"]+)"')
        foreach ($match in $operationTableMatches) {
            $table = $match.Groups['table'].Value
            if ($table -notin $allowedContactTables) {
                throw "Migration safety failed: operation targets table '$table' in $relativeFile, outside Contact scope."
            }
        }

        $principalTableMatches = [regex]::Matches($content, 'principalTable:\s*"(?<table>[^"]+)"')
        foreach ($match in $principalTableMatches) {
            $table = $match.Groups['table'].Value
            if ($table -notin $allowedPrincipalTables) {
                throw "Migration safety failed: foreign-key principal '$table' in $relativeFile is not an allowed Contact dependency."
            }
        }

        $migrationFindings.Add("PASS: $relativeFile creates only Contact tables, targets only Contact tables, and uses allowed Contact FK principals.")
    }

    Write-Step 'Verify EF Core tooling can list migrations'
    Invoke-Checked 'EF migrations list' {
        dotnet ef migrations list --project $infrastructureProject --startup-project $startupProject --no-build
    }

    if ($RunSqlServerApply) {
        $connectionString = $env:CONTACT_TEST_SQLSERVER_CONNECTION_STRING
        if ([string]::IsNullOrWhiteSpace($connectionString)) {
            throw 'Set CONTACT_TEST_SQLSERVER_CONNECTION_STRING to a disposable ContactRequestIntegration_* SQL Server database before -RunSqlServerApply.'
        }
        if ($connectionString -notmatch 'ContactRequestIntegration_') {
            throw 'CONTACT_TEST_SQLSERVER_CONNECTION_STRING must target a disposable ContactRequestIntegration_* database.'
        }

        Write-Step 'Run disposable SQL Server migration integration test'
        Invoke-Checked 'SQL Server Contact migration test' {
            dotnet test $integrationProject --configuration Release --no-build `
                --filter 'FullyQualifiedName~ContactRequestSqlServerMigrationTests'
        }
    }

    if (Test-Path $resultsPath) { Remove-Item $resultsPath -Recurse -Force }
    New-Item -ItemType Directory -Path $resultsPath -Force | Out-Null
    $reportPath = Join-Path $resultsPath 'MIGRATION_SAFETY_CONTACT_REPORT.md'
    $report = @(
        '# Contact migration safety report',
        '',
        "- Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss K')",
        "- Branch: $branch",
        "- Commit: $((& git rev-parse --short HEAD).Trim())",
        "- Base reference: $BaseRef",
        "- Disposable SQL Server apply: $RunSqlServerApply",
        '',
        '## Changed migration files',
        ''
    )
    $report += $changedMigrationFiles | ForEach-Object { "- ``$_``" }
    $report += '', '## Safety findings', ''
    $report += $migrationFindings
    $report += '', '## Required manual review', '', '- Verify `Up()`/`Down()` change only ContactRequests, ContactRequestStatusHistories, Contact indexes and Contact FKs.', '- Attach this report, migration diff, and SQL Server log (when -RunSqlServerApply is used) to the feature PR.'
    Set-Content -Path $reportPath -Value $report -Encoding utf8

    Write-Host "`nMIGRATION SAFETY PASS. Report: $reportPath" -ForegroundColor Green
}
finally {
    Pop-Location
}
