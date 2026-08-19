[CmdletBinding()]
param(
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]*$')]
    [string]$MigrationName = 'AddContactRequestManagement',

    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot 'CloudServiceStore.sln'
$infrastructureProject = Join-Path $repositoryRoot 'src/CloudServiceStore.Infrastructure/CloudServiceStore.Infrastructure.csproj'
$startupProject = Join-Path $repositoryRoot 'src/CloudServiceStore.WebApi/CloudServiceStore.WebApi.csproj'

function Require-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found. Install .NET SDK 10 and the required EF tools."
    }
}

Require-Command 'dotnet'
if (-not (Test-Path $solutionPath)) { throw "Solution not found: $solutionPath" }
if (-not (Test-Path $infrastructureProject)) { throw "Infrastructure project not found: $infrastructureProject" }
if (-not (Test-Path $startupProject)) { throw "Web API startup project not found: $startupProject" }

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

    Write-Host "Migration created. Review generated files before running 'dotnet ef database update'." -ForegroundColor Green
}
finally {
    Pop-Location
}
