$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$backendOut = Join-Path $repoRoot "backend-dev.log"
$backendErr = Join-Path $repoRoot "backend-dev.err.log"
$nextCmd = Join-Path $repoRoot "node_modules\.bin\next.cmd"
$backend = $null

Set-Location $repoRoot

function Test-BackendHealth {
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri "http://localhost:5140/api/v1/health" -TimeoutSec 2
        return $response.StatusCode -ge 200 -and $response.StatusCode -lt 300
    }
    catch {
        return $false
    }
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

        Write-Host "Backend logs: $backendOut"

        $isHealthy = $false
        for ($attempt = 1; $attempt -le 20; $attempt += 1) {
            if ($backend.HasExited) {
                break
            }

            if (Test-BackendHealth) {
                $isHealthy = $true
                break
            }

            Start-Sleep -Seconds 1
        }

        if (-not $isHealthy) {
            Write-Host "Backend failed to become healthy. Last backend error log lines:"
            if (Test-Path $backendErr) {
                Get-Content $backendErr -Tail 40
            }

            throw "Backend did not start. Check PostgreSQL on localhost:5433 and $backendErr."
        }
    }

    Write-Host "Starting frontend on http://localhost:3000 ..."

    if (Test-Path $nextCmd) {
        & $nextCmd dev
    }
    else {
        & next dev
    }
}
finally {
    if ($backend -and -not $backend.HasExited) {
        Write-Host "Stopping backend ..."
        Stop-Process -Id $backend.Id -Force
    }
}
