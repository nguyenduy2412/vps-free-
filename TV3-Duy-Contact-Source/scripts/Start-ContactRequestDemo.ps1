[CmdletBinding()]
param(
    [switch]$ResetDatabase,
    [switch]$SkipNpmInstall,
    [switch]$ForegroundFrontend
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $root '.env'
$dockerScript = Join-Path $PSScriptRoot 'Run-ContactRequestEmptyDatabase.ps1'
$frontend = Join-Path $root 'frontend'

function Wait-ForHttpOk {
    param([string]$Url, [int]$Attempts = 30)

    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        try {
            $response = Invoke-WebRequest $Url -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -eq 200) { return }
        }
        catch { }
        Start-Sleep -Seconds 2
    }

    throw "Timed out waiting for HTTP 200 from $Url."
}

if (-not (Test-Path $envFile)) {
    throw "Missing $envFile. Copy .env.example to .env and replace MSSQL_SA_PASSWORD, SEED_ADMIN_PASSWORD and JWT_SIGNING_KEY first."
}

if (-not (Test-Path $dockerScript) -or -not (Test-Path $frontend)) {
    throw 'Required demo script or frontend directory was not found.'
}

Push-Location $root
try {
    docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml config
    if ($LASTEXITCODE -ne 0) { throw 'Docker Compose validation failed.' }

    if ($ResetDatabase) {
        & $dockerScript -Reset
    }
    else {
        & $dockerScript
    }

    Wait-ForHttpOk 'http://localhost:8080/health'

    Push-Location $frontend
    try {
        if (-not $SkipNpmInstall) {
            npm ci
            if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }
        }

        $env:API_BASE_URL = 'http://localhost:8080'
        $env:NEXT_PUBLIC_SITE_URL = 'http://localhost:3000'

        try {
            $existing = Invoke-WebRequest 'http://localhost:3000/contact' -UseBasicParsing -TimeoutSec 3
            if ($existing.StatusCode -eq 200) {
                Write-Host 'Frontend is already available at http://localhost:3000/contact.' -ForegroundColor Yellow
                return
            }
        }
        catch { }

        if ($ForegroundFrontend) {
            npm run dev
            return
        }

        $process = Start-Process -FilePath 'npm.cmd' -ArgumentList 'run', 'dev' -WorkingDirectory $frontend -PassThru
        Wait-ForHttpOk 'http://localhost:3000/contact'

        Write-Host 'Demo is ready.' -ForegroundColor Green
        Write-Host 'Public form:  http://localhost:3000/contact'
        Write-Host 'Login:        http://localhost:3000/login'
        Write-Host 'Admin list:   http://localhost:3000/admin/contact-requests'
        Write-Host 'API health:   http://localhost:8080/health'
        Write-Host "Frontend process id: $($process.Id). Stop it with: Stop-Process -Id $($process.Id)"
    }
    finally { Pop-Location }
}
finally { Pop-Location }
