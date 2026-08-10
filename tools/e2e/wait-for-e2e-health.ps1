$ErrorActionPreference = 'Stop'

$ApiUrl = if ($env:E2E_API_URL) { $env:E2E_API_URL } else { 'http://localhost:5076' }
$FeUrl = if ($env:E2E_FRONTEND_URL) { $env:E2E_FRONTEND_URL } else { 'http://localhost:3000' }
$AdminUrl = if ($env:E2E_ADMIN_URL) { $env:E2E_ADMIN_URL } else { 'http://localhost:5174' }
$TimeoutSec = if ($env:E2E_HEALTH_TIMEOUT_SEC) { [int]$env:E2E_HEALTH_TIMEOUT_SEC } else { 180 }

function Wait-Url {
    param([string]$Url)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        try {
            $resp = Invoke-WebRequest -Uri $Url -Method Get -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
            $code = [int]$resp.StatusCode
            if ($code -ge 200 -and $code -lt 400) {
                Write-Host "[e2e] Healthy ($code): $Url"
                return $true
            }
        } catch {
            $resp = $_.Exception.Response
            if ($resp) {
                $code = [int]$resp.StatusCode
                if ($code -ge 200 -and $code -lt 400) {
                    Write-Host "[e2e] Healthy ($code): $Url"
                    return $true
                }
            }
        }
        Start-Sleep -Seconds 2
    }
    Write-Error "[e2e] ERROR: Timed out waiting for $Url"
    return $false
}

$apiHealthUrl = $ApiUrl.TrimEnd('/') + '/health/live'
if (-not (Wait-Url -Url $apiHealthUrl)) { exit 1 }
if (-not (Wait-Url -Url "$FeUrl/")) { exit 1 }
if (-not (Wait-Url -Url "$AdminUrl/")) { exit 1 }
