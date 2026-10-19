param(
    [string]$BaseUrl = "https://kenya-location-api-dev.grayforest-93e1dd53.southafricanorth.azurecontainerapps.io"
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd("/")
$Failures = 0

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Path,
        [scriptblock]$Validate
    )

    try {
        $Response = Invoke-RestMethod `
            -Uri "$BaseUrl$Path" `
            -Method Get `
            -TimeoutSec 20

        & $Validate $Response

        Write-Host "[PASS] $Name" -ForegroundColor Green
    }
    catch {
        $script:Failures++
        Write-Host "[FAIL] $Name - $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "Azure API smoke tests: $BaseUrl"
Write-Host ""

Test-Endpoint "Liveness" "/health/live" {
    param($Response)
    if ($Response.status -ne "alive") {
        throw "Expected status 'alive'; received '$($Response.status)'."
    }
}

Test-Endpoint "Readiness" "/health/ready" {
    param($Response)
    if ($Response.status -ne "ready") {
        throw "Expected status 'ready'; received '$($Response.status)'."
    }
}

Test-Endpoint "Legacy health" "/health" {
    param($Response)
    if ($Response.status -ne "ok") {
        throw "Expected status 'ok'; received '$($Response.status)'."
    }
}

Test-Endpoint "API metadata" "/api/v1/meta" {
    param($Response)
    if ($Response.service -ne "Kenya Location Intelligence API") {
        throw "Unexpected service name '$($Response.service)'."
    }
}

Test-Endpoint "OpenAPI document" "/swagger/v1/swagger.json" {
    param($Response)
    if ($Response.info.title -ne "Kenya Location Intelligence API") {
        throw "Unexpected OpenAPI title '$($Response.info.title)'."
    }
    if (-not $Response.paths) {
        throw "OpenAPI document contains no paths."
    }
}

Write-Host ""
if ($Failures -gt 0) {
    Write-Host "$Failures smoke test(s) failed." -ForegroundColor Red
    exit 1
}

Write-Host "All smoke tests passed." -ForegroundColor Green
exit 0
