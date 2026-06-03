$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$backendOut = Join-Path $repoRoot "backend-dev.log"
$backendErr = Join-Path $repoRoot "backend-dev.err.log"
$frontendOut = Join-Path $repoRoot "frontend-dev.log"
$frontendErr = Join-Path $repoRoot "frontend-dev.err.log"
$nextCmd = Join-Path $repoRoot "node_modules\.bin\next.cmd"
$backend = $null
$frontend = $null
$startedBackend = $false
$startedFrontend = $false

Set-Location $repoRoot

function Import-DotEnvFile {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        return
    }

    Get-Content -LiteralPath $Path | ForEach-Object {
        $line = $_.Trim()

        if (-not $line -or $line.StartsWith("#") -or -not $line.Contains("=")) {
            return
        }

        $name, $value = $line.Split("=", 2)
        $name = $name.Trim()
        $value = $value.Trim().Trim('"').Trim("'")

        if ($name) {
            [Environment]::SetEnvironmentVariable($name, $value, "Process")
        }
    }
}

Import-DotEnvFile (Join-Path $repoRoot ".env.local")

function Test-BackendHealth {
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri "http://localhost:5140/api/v1/health" -TimeoutSec 2
        return $response.StatusCode -ge 200 -and $response.StatusCode -lt 300
    }
    catch {
        return $false
    }
}

function Test-FrontendHealth {
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri "http://localhost:3000" -TimeoutSec 2
        return $response.StatusCode -ge 200 -and $response.StatusCode -lt 500
    }
    catch {
        return $false
    }
}

function Wait-ForHealth {
    param(
        [scriptblock]$HealthCheck,
        [System.Diagnostics.Process]$Process,
        [string]$Name,
        [string]$ErrorLog,
        [int]$Attempts = 45
    )

    for ($attempt = 1; $attempt -le $Attempts; $attempt += 1) {
        if ($Process -and $Process.HasExited) {
            break
        }

        if (& $HealthCheck) {
            return $true
        }

        Start-Sleep -Seconds 1
    }

    Write-Host "$Name failed to become ready. Last $Name error log lines:"
    if (Test-Path $ErrorLog) {
        Get-Content $ErrorLog -Tail 40
    }

    return $false
}

try {
    if (Test-BackendHealth) {
        Write-Host "Backend is already running on http://localhost:5140."
    }
    else {
        Write-Host "Starting backend on http://localhost:5140 ..."
        $backend = Start-Process `
            -FilePath "dotnet" `
            -ArgumentList @("run", "--project", "Backend\Backend\Backend.csproj") `
            -WorkingDirectory $repoRoot `
            -RedirectStandardOutput $backendOut `
            -RedirectStandardError $backendErr `
            -PassThru

        $startedBackend = $true
        Write-Host "Backend logs: $backendOut"

        if (-not (Wait-ForHealth -HealthCheck ${function:Test-BackendHealth} -Process $backend -Name "Backend" -ErrorLog $backendErr -Attempts 30)) {
            throw "Backend did not start. Check PostgreSQL on localhost:5433 and $backendErr."
        }
    }

    if (Test-FrontendHealth) {
        Write-Host "Frontend is already running on http://localhost:3000."
    }
    else {
        Write-Host "Starting frontend on http://localhost:3000 ..."

        if (Test-Path $nextCmd) {
            $frontend = Start-Process `
                -FilePath $nextCmd `
                -ArgumentList @("dev") `
                -WorkingDirectory $repoRoot `
                -RedirectStandardOutput $frontendOut `
                -RedirectStandardError $frontendErr `
                -PassThru
        }
        else {
            $frontend = Start-Process `
                -FilePath "next" `
                -ArgumentList @("dev") `
                -WorkingDirectory $repoRoot `
                -RedirectStandardOutput $frontendOut `
                -RedirectStandardError $frontendErr `
                -PassThru
        }

        $startedFrontend = $true
        Write-Host "Frontend logs: $frontendOut"

        if (-not (Wait-ForHealth -HealthCheck ${function:Test-FrontendHealth} -Process $frontend -Name "Frontend" -ErrorLog $frontendErr -Attempts 45)) {
            throw "Frontend did not start. Check $frontendErr."
        }
    }

    Write-Host ""
    Write-Host "Local development stack is ready."
    Write-Host "Frontend: http://localhost:3000"
    Write-Host "Backend:  http://localhost:5140/api/v1/health"
    Write-Host "Press Ctrl+C to stop the dev stack."
    Write-Host ""

    Start-Process "http://localhost:3000"

    if ($frontend) {
        Wait-Process -Id $frontend.Id
    }
}
finally {
    if ($startedFrontend -and $frontend -and -not $frontend.HasExited) {
        Write-Host "Stopping frontend ..."
        Stop-Process -Id $frontend.Id -Force
    }

    if ($startedBackend -and $backend -and -not $backend.HasExited) {
        Write-Host "Stopping backend ..."
        Stop-Process -Id $backend.Id -Force
    }
}
