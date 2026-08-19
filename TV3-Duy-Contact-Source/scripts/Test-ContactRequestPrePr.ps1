[CmdletBinding()]
param(
    [switch]$SkipRestore,
    [switch]$SkipFrontend,
    [string]$ResultsDirectory = 'artifacts/pre-pr-contact'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot 'CloudServiceStore.sln'
$integrationProject = Join-Path $repositoryRoot 'tests/CloudServiceStore.Integration.Tests/CloudServiceStore.Integration.Tests.csproj'
$frontendPath = Join-Path $repositoryRoot 'frontend'
$resultsPath = Join-Path $repositoryRoot $ResultsDirectory

function Require-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found."
    }
}

function Write-Step([string]$Message) {
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Invoke-Checked([string]$Step, [scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed with exit code $LASTEXITCODE."
    }
}

Require-Command 'dotnet'
Require-Command 'git'
if (-not $SkipFrontend) { Require-Command 'npm' }
if (-not (Test-Path $solutionPath)) { throw "Solution not found: $solutionPath" }
if (-not (Test-Path $integrationProject)) { throw "Integration test project not found: $integrationProject" }
if (-not (Test-Path $frontendPath)) { throw "Frontend directory not found: $frontendPath" }

Push-Location $repositoryRoot
try {
    $isGitWorkTree = (& git rev-parse --is-inside-work-tree 2>$null)
    if ($LASTEXITCODE -ne 0 -or $isGitWorkTree -ne 'true') {
        throw 'Run this script from a real Git clone before opening a PR.'
    }

    Write-Step 'Checking Git diff whitespace'
    Invoke-Checked 'Git whitespace check' { git diff --check }

    if (-not $SkipRestore) {
        Write-Step 'Restoring .NET solution'
        Invoke-Checked '.NET restore' { dotnet restore $solutionPath }
    }

    Write-Step 'Building .NET solution in Release'
    Invoke-Checked '.NET Release build' { dotnet build $solutionPath --configuration Release --no-restore }

    if (Test-Path $resultsPath) { Remove-Item $resultsPath -Recurse -Force }
    New-Item -ItemType Directory -Path $resultsPath -Force | Out-Null

    Write-Step 'Running all backend tests with Cobertura coverage'
    Invoke-Checked 'Backend tests with coverage' {
        dotnet test $solutionPath --configuration Release --no-build `
            --collect:'XPlat Code Coverage' `
            --results-directory $resultsPath
    }

    Write-Step 'Running Contact pagination/status-filter API tests explicitly'
    Invoke-Checked 'Contact query API tests' {
        dotnet test $integrationProject --configuration Release --no-build `
            --filter 'FullyQualifiedName~ContactRequestQueryApiTests'
    }

    if (-not $SkipFrontend) {
        Push-Location $frontendPath
        try {
            Write-Step 'Installing frontend dependencies reproducibly'
            Invoke-Checked 'npm ci' { npm ci }

            Write-Step 'Running frontend lint'
            Invoke-Checked 'Frontend lint' { npm run lint }

            Write-Step 'Running frontend production build'
            $previousNodeEnv = $env:NODE_ENV
            $env:NODE_ENV = 'production'
            try { Invoke-Checked 'Frontend production build' { npm run build } }
            finally { $env:NODE_ENV = $previousNodeEnv }
        }
        finally { Pop-Location }
    }

    $coverageFiles = Get-ChildItem -Path $resultsPath -Filter 'coverage.cobertura.xml' -Recurse
    if ($coverageFiles.Count -eq 0) { throw 'Coverage collector produced no cobertura XML artifacts.' }

    $reportPath = Join-Path $resultsPath 'PRE_PR_CONTACT_REPORT.md'
    $report = @(
        '# Pre-PR Contact Request report',
        '',
        "- Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss K')",
        "- Commit: $(git rev-parse --short HEAD)",
        "- Branch: $(git branch --show-current)",
        "- Backend coverage artifacts: $($coverageFiles.Count)",
        "- Frontend checks: $(if ($SkipFrontend) { 'skipped by parameter' } else { 'npm ci, lint and production build passed' })",
        '',
        '| Coverage artifact | Line coverage | Branch coverage |',
        '|---|---:|---:|'
    )

    foreach ($coverageFile in $coverageFiles | Sort-Object FullName) {
        [xml]$coverage = Get-Content $coverageFile.FullName
        $lineRate = [double]$coverage.coverage.'line-rate'
        $branchRate = [double]$coverage.coverage.'branch-rate'
        $relative = Resolve-Path -Relative $coverageFile.FullName
        $report += "| ``$relative`` | $($lineRate.ToString('P2')) | $($branchRate.ToString('P2')) |"
    }

    $report += '', '## Git status', '', '```text', (git status --short), '```'
    Set-Content -Path $reportPath -Value $report -Encoding utf8

    Write-Host "`nPRE-PR PASS. Coverage report: $reportPath" -ForegroundColor Green
}
finally {
    Pop-Location
}
