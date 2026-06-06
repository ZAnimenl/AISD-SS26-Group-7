$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$devScript = Join-Path $repoRoot "scripts/dev.mjs"

Set-Location $repoRoot
& node $devScript @args
exit $LASTEXITCODE
