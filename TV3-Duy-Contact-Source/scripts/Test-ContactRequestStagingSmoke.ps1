<##
.SYNOPSIS
Runs post-deploy staging smoke tests for the TV3 Contact Request API.

.DESCRIPTION
Uses access tokens supplied only through STAGING_ADMIN_ACCESS_TOKEN and optional
STAGING_CUSTOMER_ACCESS_TOKEN environment variables. The script never writes
tokens or Authorization headers to its report. It creates one uniquely labelled
technical Contact Request and moves it through the normal backend workflow.

.EXAMPLE
$env:STAGING_ADMIN_ACCESS_TOKEN = '<token-from-secure-session>'
$env:STAGING_CUSTOMER_ACCESS_TOKEN = '<optional-customer-token>'
.\scripts\Test-ContactRequestStagingSmoke.ps1 -ApiBaseUrl 'https://staging-api.example.com'

.EXAMPLE
.\scripts\Test-ContactRequestStagingSmoke.ps1 -ApiBaseUrl 'https://staging-api.example.com' `
  -IncludeRateLimitProbe -RateLimitProbeCount 6
##>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$ApiBaseUrl,

    [string]$AdminAccessToken = $env:STAGING_ADMIN_ACCESS_TOKEN,

    [string]$CustomerAccessToken = $env:STAGING_CUSTOMER_ACCESS_TOKEN,

    [switch]$IncludeRateLimitProbe,

    [ValidateRange(1, 100)]
    [int]$RateLimitProbeCount = 6,

    [string]$ResultsDirectory = "artifacts/staging-smoke-contact"
)

$ErrorActionPreference = "Stop"

function Assert-Required {
    param([string]$Value, [string]$Name)
    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "$Name is required. Set it through a protected environment variable, not source code."
    }
}

function Get-ResponseBody {
    param($Response)
    if ($null -eq $Response) { return "" }

    try {
        $stream = $Response.GetResponseStream()
        if ($null -eq $stream) { return "" }
        $reader = [System.IO.StreamReader]::new($stream)
        return $reader.ReadToEnd()
    }
    catch {
        return ""
    }
}

function Invoke-Api {
    param(
        [ValidateSet("GET", "POST")][string]$Method,
        [string]$Uri,
        [hashtable]$Headers = @{},
        [object]$Body = $null
    )

    $request = @{
        Uri = $Uri
        Method = $Method
        Headers = $Headers
        UseBasicParsing = $true
        ErrorAction = "Stop"
    }

    if ($null -ne $Body) {
        $request.ContentType = "application/json"
        $request.Body = $Body | ConvertTo-Json -Depth 8 -Compress
    }

    try {
        $response = Invoke-WebRequest @request
        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            Body = $response.Content
        }
    }
    catch {
        if ($null -ne $_.Exception.Response) {
            return [pscustomobject]@{
                StatusCode = [int]$_.Exception.Response.StatusCode
                Body = Get-ResponseBody $_.Exception.Response
            }
        }

        throw
    }
}

function Add-Check {
    param(
        [string]$Name,
        [int[]]$ExpectedStatus,
        [int]$ActualStatus,
        [string]$Note = ""
    )

    $passed = $ExpectedStatus -contains $ActualStatus
    $script:Checks.Add([pscustomobject]@{
            Name = $Name
            ExpectedStatus = $ExpectedStatus -join "/"
            ActualStatus = $ActualStatus
            Passed = $passed
            Note = $Note
        })

    if (-not $passed) {
        throw "$Name failed: expected HTTP $($ExpectedStatus -join ' or '), received HTTP $ActualStatus."
    }
}

Assert-Required $AdminAccessToken "STAGING_ADMIN_ACCESS_TOKEN"

$baseUrl = $ApiBaseUrl.TrimEnd("/")
$apiUrl = "$baseUrl/api/v1"
$adminHeaders = @{ Authorization = "Bearer $AdminAccessToken" }
$customerHeaders = if ([string]::IsNullOrWhiteSpace($CustomerAccessToken)) { @{} } else { @{ Authorization = "Bearer $CustomerAccessToken" } }
$Checks = [System.Collections.Generic.List[object]]::new()
$runId = [Guid]::NewGuid().ToString("N")
$email = "staging-contact-$runId@example.test"
$timestamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMddHHmmss")

New-Item -ItemType Directory -Force -Path $ResultsDirectory | Out-Null
$reportPath = Join-Path $ResultsDirectory "STAGING_SMOKE_CONTACT_REPORT_$timestamp.json"

try {
    $health = Invoke-Api -Method "GET" -Uri "$baseUrl/health"
    Add-Check -Name "API health" -ExpectedStatus @(200) -ActualStatus $health.StatusCode

    $createPayload = @{
        fullName = "TV3 Staging Smoke $runId"
        email = $email
        phoneNumber = "+84900000000"
        companyName = "TV3 Staging Technical Test"
        subject = "Staging smoke $timestamp"
        message = "Technical Contact Request created by post-deploy staging smoke test."
    }
    $create = Invoke-Api -Method "POST" -Uri "$apiUrl/contact-requests" -Body $createPayload
    Add-Check -Name "Public create Contact" -ExpectedStatus @(201) -ActualStatus $create.StatusCode
    $contactId = ($create.Body | ConvertFrom-Json).id
    if ([string]::IsNullOrWhiteSpace($contactId)) { throw "Public create response did not contain Contact Request id." }

    $duplicate = Invoke-Api -Method "POST" -Uri "$apiUrl/contact-requests" -Body $createPayload
    Add-Check -Name "Public duplicate Contact conflict" -ExpectedStatus @(409) -ActualStatus $duplicate.StatusCode

    $invalid = Invoke-Api -Method "POST" -Uri "$apiUrl/contact-requests" -Body @{
        fullName = "X"
        email = "invalid"
        phoneNumber = "1"
        subject = "x"
        message = "short"
    }
    Add-Check -Name "Public validation ProblemDetails" -ExpectedStatus @(400) -ActualStatus $invalid.StatusCode

    $anonymousList = Invoke-Api -Method "GET" -Uri "$apiUrl/contact-requests?page=1&pageSize=5"
    Add-Check -Name "Anonymous admin list denied" -ExpectedStatus @(401) -ActualStatus $anonymousList.StatusCode

    if ($customerHeaders.Count -gt 0) {
        $customerList = Invoke-Api -Method "GET" -Uri "$apiUrl/contact-requests?page=1&pageSize=5" -Headers $customerHeaders
        Add-Check -Name "Customer admin list denied" -ExpectedStatus @(403) -ActualStatus $customerList.StatusCode
    }

    $encodedSearch = [Uri]::EscapeDataString($email)
    $adminList = Invoke-Api -Method "GET" -Uri "$apiUrl/contact-requests?page=1&pageSize=1&search=$encodedSearch&status=1" -Headers $adminHeaders
    Add-Check -Name "Admin list/paging/status filter Contact" -ExpectedStatus @(200) -ActualStatus $adminList.StatusCode

    $detail = Invoke-Api -Method "GET" -Uri "$apiUrl/contact-requests/$contactId" -Headers $adminHeaders
    Add-Check -Name "Admin detail/history Contact" -ExpectedStatus @(200) -ActualStatus $detail.StatusCode

    $missingId = [Guid]::NewGuid()
    $missingDetail = Invoke-Api -Method "GET" -Uri "$apiUrl/contact-requests/$missingId" -Headers $adminHeaders
    Add-Check -Name "Admin missing detail ProblemDetails" -ExpectedStatus @(404) -ActualStatus $missingDetail.StatusCode

    $missingStatus = Invoke-Api -Method "POST" -Uri "$apiUrl/contact-requests/$missingId/status" -Headers $adminHeaders -Body @{ status = 2; note = "Missing resource staging check" }
    Add-Check -Name "Admin missing status ProblemDetails" -ExpectedStatus @(404) -ActualStatus $missingStatus.StatusCode

    $contacted = Invoke-Api -Method "POST" -Uri "$apiUrl/contact-requests/$contactId/status" -Headers $adminHeaders -Body @{ status = 2; note = "Staging smoke contacted" }
    Add-Check -Name "Workflow Pending to Contacted" -ExpectedStatus @(200) -ActualStatus $contacted.StatusCode

    $invalidEnum = Invoke-Api -Method "POST" -Uri "$apiUrl/contact-requests/$contactId/status" -Headers $adminHeaders -Body @{ status = 99; note = "Invalid enum staging check" }
    Add-Check -Name "Workflow invalid enum ProblemDetails" -ExpectedStatus @(400) -ActualStatus $invalidEnum.StatusCode

    $missingRejectionNote = Invoke-Api -Method "POST" -Uri "$apiUrl/contact-requests/$contactId/status" -Headers $adminHeaders -Body @{ status = 4; note = $null }
    Add-Check -Name "Rejected without note ProblemDetails" -ExpectedStatus @(400) -ActualStatus $missingRejectionNote.StatusCode

    $approved = Invoke-Api -Method "POST" -Uri "$apiUrl/contact-requests/$contactId/status" -Headers $adminHeaders -Body @{ status = 3; note = "Staging smoke approved" }
    Add-Check -Name "Workflow Contacted to Approved" -ExpectedStatus @(200) -ActualStatus $approved.StatusCode

    $reopen = Invoke-Api -Method "POST" -Uri "$apiUrl/contact-requests/$contactId/status" -Headers $adminHeaders -Body @{ status = 2; note = "Invalid staging reopen check" }
    Add-Check -Name "Terminal status cannot reopen" -ExpectedStatus @(409) -ActualStatus $reopen.StatusCode

    if ($IncludeRateLimitProbe) {
        $rateStatuses = [System.Collections.Generic.List[int]]::new()
        for ($index = 1; $index -le $RateLimitProbeCount; $index++) {
            $probe = Invoke-Api -Method "POST" -Uri "$apiUrl/contact-requests" -Body @{
                fullName = "TV3 Rate Probe $runId $index"
                email = "staging-rate-$runId-$index@example.test"
                phoneNumber = "+84900000000"
                companyName = "TV3 Staging Technical Test"
                subject = "Rate limit staging probe $index"
                message = "Technical rate limit probe; unique request to avoid duplicate validation."
            }
            $rateStatuses.Add($probe.StatusCode)
        }

        if (-not ($rateStatuses -contains 429)) {
            throw "Rate-limit probe did not observe HTTP 429. Set RateLimitProbeCount greater than the deployed ContactPermitLimit before rerunning."
        }

        $Checks.Add([pscustomobject]@{
                Name = "Public rate-limit probe"
                ExpectedStatus = "At least one 429"
                ActualStatus = $rateStatuses -join ","
                Passed = $true
                Note = "Unique technical emails; no secrets recorded."
            })
    }

    $result = [pscustomobject]@{
        Succeeded = $true
        ExecutedAtUtc = [DateTimeOffset]::UtcNow
        ApiBaseUrl = $baseUrl
        TechnicalContactId = $contactId
        TechnicalContactEmail = $email
        Checks = $Checks
    }
    $result | ConvertTo-Json -Depth 8 | Set-Content -Path $reportPath -Encoding utf8
    Write-Host "STAGING_SMOKE_CONTACT=PASS"
    Write-Host "Report: $reportPath"
}
catch {
    $result = [pscustomobject]@{
        Succeeded = $false
        ExecutedAtUtc = [DateTimeOffset]::UtcNow
        ApiBaseUrl = $baseUrl
        TechnicalContactEmail = $email
        Error = $_.Exception.Message
        Checks = $Checks
    }
    $result | ConvertTo-Json -Depth 8 | Set-Content -Path $reportPath -Encoding utf8
    Write-Error "STAGING_SMOKE_CONTACT=FAIL. Report: $reportPath. $($_.Exception.Message)"
    exit 1
}
