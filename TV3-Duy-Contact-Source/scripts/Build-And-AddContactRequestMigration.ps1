[CmdletBinding()]
param(
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]*$')]
    [string]$MigrationName = 'AddContactRequestManagement',

    [switch]$SkipTests,

    [switch]$AllowExistingContactMigration
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot 'CloudServiceStore.sln'
$infrastructureProject = Join-Path $repositoryRoot 'src/CloudServiceStore.Infrastructure/CloudServiceStore.Infrastructure.csproj'
$startupProject = Join-Path $repositoryRoot 'src/CloudServiceStore.WebApi/CloudServiceStore.WebApi.csproj'
$migrationDirectory = Join-Path $repositoryRoot 'src/CloudServiceStore.Infrastructure/Persistence/Migrations'

function Require-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found. Install .NET SDK 10 and the required EF tools."
    }
}

Require-Command 'dotnet'
Require-Command 'git'
if (-not (Test-Path $solutionPath)) { throw "Solution not found: $solutionPath" }
if (-not (Test-Path $infrastructureProject)) { throw "Infrastructure project not found: $infrastructureProject" }
if (-not (Test-Path $startupProject)) { throw "Web API startup project not found: $startupProject" }

$branch = (git branch --show-current).Trim()
if ($branch -in @('', 'main', 'master', 'dev')) {
    throw "Create the Contact migration on a feature branch from dev, not on '$branch'."
}

if (-not $AllowExistingContactMigration -and (Get-ChildItem -Path $migrationDirectory -Filter '*ContactRequest*.cs' -ErrorAction SilentlyContinue)) {
    throw "A Contact migration already exists. Review it or pass -AllowExistingContactMigration only when intentionally regenerating on a clean feature branch."
}

Push-Location $repositoryRoot
try {
    Write-Host "Restoring solution..." -ForegroundColor Cyan
    dotnet restore $solutionPath

    Write-Host "Building solution (Release)..." -ForegroundColor Cyan
    dotnet build $solutionPath --configuration Release --no-restore

    if (-not $SkipTests) {
        Write-Host "Running tests with code coverage..." -ForegroundColor Cyan
        dotnet test $solutionPath --configuration Release --no-build `
            --collect:"XPlat Code Coverage" `
            --results-directory TestResults
    }

    Write-Host "Checking EF Core CLI..." -ForegroundColor Cyan
    dotnet ef --version

    Write-Host "Creating migration '$MigrationName'..." -ForegroundColor Cyan
    dotnet ef migrations add $MigrationName `
        --project $infrastructureProject `
        --startup-project $startupProject `
        --output-dir Persistence/Migrations

    $migrationDiff = git diff -- $migrationDirectory
    if ($migrationDiff -notmatch 'ContactRequests' -or $migrationDiff -notmatch 'ContactRequestStatusHistories') {
        throw "Generated migration does not contain both Contact tables. Delete the generated migration and inspect model drift before retrying."
    }

    $forbiddenSchemaChange = '(?is)(CreateTable|DropTable)\s*\(\s*name:\s*"(AppUsers|NewsArticles|Orders|Promotions|Affiliate[^"]*)"|AlterColumn<[^>]+>\s*\(.*?table:\s*"(AppUsers|NewsArticles|Orders|Promotions|Affiliate[^"]*)"'
    if ($migrationDiff -match $forbiddenSchemaChange) {
        throw "Generated migration touches a non-Contact table. Delete the generated migration; do not edit it by hand to hide model drift."
    }

    Write-Host "Migration created and passed the Contact-only name guard. Review the full git diff before running 'dotnet ef database update'." -ForegroundColor Green
}
finally {
    Pop-Location
}
