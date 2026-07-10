# =============================================================================
#  dev-front.ps1  -  Levanta el FRONTEND (Angular 22) con el Node correcto
# -----------------------------------------------------------------------------
#  Por que existe: Angular CLI 22 exige Node >= 22.22.3, pero el Node del
#  sistema es 22.15.0 -> `yarn start` aborta ("requires a minimum Node.js
#  version of v22.22.3"). El Node portable 22.23.1 vive en
#  %USERPROFILE%\node-portable\node-v*. Este script lo antepone al PATH y
#  arranca el dev server en :4200.
#
#  Uso:   .\dev-front.ps1    (o: make dev-front)
#  NOTA: ASCII puro a proposito (Windows PowerShell 5.1 malparsea UTF-8 sin BOM).
# =============================================================================
$ErrorActionPreference = 'Stop'

# Elegir el node-portable mas nuevo disponible (node-v22.23.1-win-x64, etc.)
$portableRoot = Join-Path $env:USERPROFILE 'node-portable'
$nodeDir = Get-ChildItem $portableRoot -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like 'node-v*' } |
    Sort-Object Name -Descending |
    Select-Object -First 1
if (-not $nodeDir) {
    throw "No se encontro Node portable en $portableRoot (carpeta node-v*). Descarga el zip de Node >= 22.22.3."
}

# Anteponer el Node portable (gana sobre el Node del sistema)
$env:PATH = "$($nodeDir.FullName);$env:PATH"

$frontDir = Join-Path $PSScriptRoot 'frontend'
Push-Location $frontDir
try {
    Write-Host ("[dev-front] node: " + (node --version) + "  (esperado >= 22.22.3)") -ForegroundColor Cyan
    Write-Host "[dev-front] Frontend -> http://localhost:4200" -ForegroundColor Green
    yarn start
}
finally {
    Pop-Location
}
