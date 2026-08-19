[CmdletBinding()]
param([switch]$Reset)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$base = Join-Path $root 'docker-compose.yml'
$empty = Join-Path $root 'docker-compose.contact-empty.yml'

if (-not (Test-Path $base) -or -not (Test-Path $empty)) { throw 'Compose files were not found.' }
Push-Location $root
try {
    if ($Reset) { docker compose -f $base -f $empty down --volumes --remove-orphans }
    docker compose -f $base -f $empty up --build --detach
    docker compose -f $base -f $empty ps

    $migratorExitCode = $null
    $migratorState = $null
    for ($attempt = 1; $attempt -le 90; $attempt++) {
        $migratorState = docker compose -f $base -f $empty ps --all migrator --format '{{.State}}'
        if ($migratorState -eq 'exited') {
            $migratorExitCode = docker compose -f $base -f $empty ps --all migrator --format '{{.ExitCode}}'
            break
        }

        if ($migratorState -notin @('created', 'restarting', 'running')) {
            docker compose -f $base -f $empty logs migrator
            docker compose -f $base -f $empty logs sqlserver
            throw "Contact database migrator entered unexpected state '$migratorState'."
        }

        Start-Sleep -Seconds 2
    }

    if ($migratorState -ne 'exited') {
        docker compose -f $base -f $empty logs migrator
        docker compose -f $base -f $empty logs sqlserver
        throw 'Contact database migrator did not exit within 180 seconds.'
    }

    if ($migratorExitCode -ne '0') {
        docker compose -f $base -f $empty logs migrator
        throw "Contact database migrator did not exit successfully. Exit code: '$migratorExitCode'."
    }

    $health = $null
    for ($attempt = 1; $attempt -le 30; $attempt++) {
        try {
            $health = Invoke-WebRequest http://localhost:8080/health -UseBasicParsing -TimeoutSec 5
            if ($health.StatusCode -eq 200) { break }
        }
        catch {
            Start-Sleep -Seconds 2
        }
    }

    if ($null -eq $health -or $health.StatusCode -ne 200) {
        docker compose -f $base -f $empty logs api
        throw 'API health endpoint did not return HTTP 200 after the database migrator completed.'
    }

    $health | Select-Object StatusCode, Content
}
finally { Pop-Location }
