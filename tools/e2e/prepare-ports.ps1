# Ensures Playwright webServer ports are free before starting tests.
$ErrorActionPreference = 'Stop'

$ApiPort = if ($env:E2E_API_PORT) { $env:E2E_API_PORT } else { 5076 }
$FePort = if ($env:E2E_FRONTEND_PORT) { $env:E2E_FRONTEND_PORT } else { 3000 }
$AdminPort = if ($env:E2E_ADMIN_PORT) { $env:E2E_ADMIN_PORT } else { 5174 }
$WaitTimeoutSec = if ($env:E2E_PORT_WAIT_TIMEOUT_SEC) { [int]$env:E2E_PORT_WAIT_TIMEOUT_SEC } else { 120 }
$CleanPorts = if ($env:E2E_CLEAN_PORTS) { $env:E2E_CLEAN_PORTS } else { '0' }

function Test-PortInUse {
    param([int]$Port)
    $conns = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    return [bool]$conns
}

function Show-PortOwner {
    param([int]$Port)
    $conns = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    if (-not $conns) {
        Write-Host "  (no listener found for port $Port)"
        return
    }
    foreach ($c in $conns) {
        $proc = Get-Process -Id $c.OwningProcess -ErrorAction SilentlyContinue
        $name = if ($proc) { $proc.ProcessName } else { 'unknown' }
        Write-Host "  - PID $($c.OwningProcess) $name"
    }
}

function Wait-ForPortFree {
    param([int]$Port)
    $deadline = (Get-Date).AddSeconds($WaitTimeoutSec)
    while ((Get-Date) -lt $deadline) {
        if (-not (Test-PortInUse -Port $Port)) {
            Write-Host "[e2e] Port $Port is free."
            return $true
        }
        $remaining = [int]($deadline - (Get-Date)).TotalSeconds
        Write-Host "[e2e] Waiting for port $Port to become free (${remaining}s left)..."
        Start-Sleep -Seconds 2
    }
    Write-Error "[e2e] ERROR: Port $Port still in use after ${WaitTimeoutSec}s."
    Show-PortOwner -Port $Port
    return $false
}

function Clear-Port {
    param([int]$Port)
    if (-not (Test-PortInUse -Port $Port)) {
        return $true
    }
    if ($CleanPorts -ne '1') {
        Write-Error "[e2e] Port $Port is in use. Set E2E_CLEAN_PORTS=1 to stop listeners on this port only."
        Show-PortOwner -Port $Port
        return $false
    }
    $conns = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    $pids = $conns | Select-Object -ExpandProperty OwningProcess -Unique
    if ($pids) {
        Write-Host "[e2e] Stopping processes on port $Port`: $($pids -join ', ')"
        foreach ($processId in $pids) {
            try { Stop-Process -Id $processId -Force -Confirm:$false -ErrorAction Stop } catch {}
        }
        Start-Sleep -Seconds 2
    }
    return (Wait-ForPortFree -Port $Port)
}

$ok = $true
foreach ($port in @($ApiPort, $FePort, $AdminPort)) {
    if (Test-PortInUse -Port $port) {
        if ($CleanPorts -eq '1') {
            if (-not (Clear-Port -Port $port)) { $ok = $false }
        } else {
            if (-not (Wait-ForPortFree -Port $port)) { $ok = $false }
        }
    } else {
        Write-Host "[e2e] Port $port is already free."
    }
}

if (-not $ok) {
    exit 1
}

Write-Host "[e2e] Port preparation complete."
