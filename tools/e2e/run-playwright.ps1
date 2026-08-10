# PowerShell equivalent of run-playwright.sh
$ErrorActionPreference = 'Stop'

$Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

# Playwright requires Node.js 20+ (Next.js 15 requirement).
$nodeVersion = & node -p "process.versions.node.split('.')[0]" 2>$null
if (-not $nodeVersion) { $nodeVersion = 0 }
if ([int]$nodeVersion -lt 20) {
    $currentVersion = try { (& node -v) } catch { 'not found' }
    Write-Error "Playwright requires Node.js 20+. Current: $currentVersion."
    Write-Host "Install/use Node 20+ (see .nvmrc), e.g. via nvm-windows: nvm use 20" -ForegroundColor Yellow
    exit 1
}

Push-Location (Join-Path $Root 'e2e')
try {
    npm install
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    npm run install:browsers
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    # Keep UI e2e isolated from developer data by default.
    if (-not $env:E2E_DATABASE_URL) {
        $env:E2E_DATABASE_URL = 'Host=localhost;Port=5432;Database=baseproject_e2e;Username=postgres;Password=postgres'
    }
    if (-not $env:E2E_DATABASE_NAME) {
        $env:E2E_DATABASE_NAME = 'baseproject_e2e'
    }

    if ($env:CI) {
        if (-not $env:E2E_DROP_DB_AFTER_RUN) { $env:E2E_DROP_DB_AFTER_RUN = '1' }
        if (-not $env:E2E_POSTGRES_CLI) { $env:E2E_POSTGRES_CLI = '1' }
        # In CI, free our standard ports before Playwright starts webServers (avoids stale dev servers).
        if (-not $env:E2E_PREPARE_PORTS) { $env:E2E_PREPARE_PORTS = '1' }
        # Optional: run frontend tests with 2 workers after setup (set E2E_STABLE_PARALLEL=1).
        if (-not $env:E2E_WORKERS) { $env:E2E_WORKERS = '1' }
    } else {
        if (-not $env:E2E_DROP_DB_AFTER_RUN) { $env:E2E_DROP_DB_AFTER_RUN = '0' }
        if (-not $env:E2E_PREPARE_PORTS) { $env:E2E_PREPARE_PORTS = '0' }
    }

    if (-not $env:E2E_REUSE_SERVERS) { $env:E2E_REUSE_SERVERS = 'false' }

    if ($env:E2E_PREPARE_PORTS -eq '1') {
        & (Join-Path $Root 'tools\e2e\prepare-ports.ps1')
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    if ($env:E2E_REUSE_SERVERS -eq 'true') {
        & (Join-Path $Root 'tools\e2e\wait-for-e2e-health.ps1')
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $env:E2E_SKIP_WEBSERVER = '1'
        npm test @args
        exit $LASTEXITCODE
    }

    npm test @args
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
