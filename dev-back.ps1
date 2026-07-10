# =============================================================================
#  dev-back.ps1  -  Levanta el BACKEND (.NET 10) en modo Development
# -----------------------------------------------------------------------------
#  Por que existe: el proyecto es net10.0 pero el `dotnet` de
#  "C:\Program Files\dotnet" solo tiene SDK 8/9 -> `dotnet run` falla con
#  NETSDK1045. El SDK .NET 10 esta instalado user-local en %USERPROFILE%\.dotnet.
#  Este script antepone ese dotnet al PATH y arranca la API en :5002
#  (Program.cs fuerza UseUrls("http://+:5002"); el front dev apunta ahi).
#
#  Uso:   .\dev-back.ps1     (o: make dev-back)
#  NOTA: ASCII puro a proposito (Windows PowerShell 5.1 malparsea UTF-8 sin BOM).
# =============================================================================
$ErrorActionPreference = 'Stop'

$dotnetDir = Join-Path $env:USERPROFILE '.dotnet'
$dotnetExe = Join-Path $dotnetDir 'dotnet.exe'
if (-not (Test-Path $dotnetExe)) {
    throw "No se encontro .NET 10 user-local en $dotnetExe. Instala el SDK .NET 10 (dotnet-install) o revisa la ruta."
}

# Anteponer el .NET 10 user-local (gana sobre el de Program Files)
$env:DOTNET_ROOT            = $dotnetDir
$env:PATH                   = "$dotnetDir;$env:PATH"
$env:ASPNETCORE_ENVIRONMENT = 'Development'   # appsettings.Development.json (Postgres local :5433)

$apiDir = Join-Path $PSScriptRoot 'backend\src\ZooSanMarino.API'
Push-Location $apiDir
try {
    Write-Host ("[dev-back] dotnet: " + (& $dotnetExe --version) + "  (esperado 10.x)") -ForegroundColor Cyan
    Write-Host ("[dev-back] ASPNETCORE_ENVIRONMENT = " + $env:ASPNETCORE_ENVIRONMENT) -ForegroundColor Cyan
    Write-Host "[dev-back] Backend -> http://localhost:5002  (Swagger: http://localhost:5002/swagger)" -ForegroundColor Green
    & $dotnetExe run --launch-profile http
}
finally {
    Pop-Location
}
