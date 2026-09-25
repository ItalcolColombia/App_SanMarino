# =============================================================================
#  dev-back.ps1  -  Levanta el BACKEND (.NET 10) en modo Development
# -----------------------------------------------------------------------------
#  Por que existe: el proyecto es net10.0 pero el `dotnet` de
#  "C:\Program Files\dotnet" solo tiene SDK 8/9 -> `dotnet run` falla con
#  NETSDK1045. El SDK .NET 10 esta instalado user-local en %USERPROFILE%\.dotnet.
#  Este script antepone ese dotnet al PATH y arranca la API en :5002
#  (Program.cs fuerza UseUrls("http://+:5002"); el front dev apunta ahi).
#
#  Uso:
#    .\dev-back.ps1             build incremental seguro + arranque
#    .\dev-back.ps1 -NoBuild    arranque inmediato del ultimo build
#    .\dev-back.ps1 -Migrate    build + migraciones locales + arranque
#  NOTA: ASCII puro a proposito (Windows PowerShell 5.1 malparsea UTF-8 sin BOM).
# =============================================================================
[CmdletBinding()]
param(
    [switch]$NoBuild,
    [switch]$Migrate
)

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
$env:PORT                   = '5002'
$env:Database__RunMigrations = if ($Migrate) { 'true' } else { 'false' }
$env:Database__RunDevBootstrap = if ($Migrate) { 'true' } else { 'false' }
$env:Database__RunSeed       = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$env:MSBUILDDISABLENODEREUSE = '1'

$apiDir = Join-Path $PSScriptRoot 'backend\src\ZooSanMarino.API'
$apiProject = Join-Path $apiDir 'ZooSanMarino.API.csproj'
$apiDll = Join-Path $apiDir 'bin\Debug\net10.0\ZooSanMarino.API.dll'
$projects = @(
    (Join-Path $PSScriptRoot 'backend\src\ZooSanMarino.Domain\ZooSanMarino.Domain.csproj'),
    (Join-Path $PSScriptRoot 'backend\src\ZooSanMarino.Application\ZooSanMarino.Application.csproj'),
    (Join-Path $PSScriptRoot 'backend\src\ZooSanMarino.Infrastructure\ZooSanMarino.Infrastructure.csproj'),
    $apiProject
)

function Test-RestoreRequired {
    foreach ($project in $projects) {
        $projectInfo = Get-Item -LiteralPath $project
        $assets = Join-Path $projectInfo.DirectoryName 'obj\project.assets.json'
        if (-not (Test-Path -LiteralPath $assets)) {
            return $true
        }

        if ((Get-Item -LiteralPath $assets).LastWriteTimeUtc -lt $projectInfo.LastWriteTimeUtc) {
            return $true
        }
    }

    return $false
}

Push-Location $apiDir
try {
    Write-Host ("[dev-back] dotnet: " + (& $dotnetExe --version) + "  (esperado 10.x)") -ForegroundColor Cyan
    Write-Host ("[dev-back] ASPNETCORE_ENVIRONMENT = " + $env:ASPNETCORE_ENVIRONMENT) -ForegroundColor Cyan
    Write-Host ("[dev-back] Migraciones al arrancar = " + $env:Database__RunMigrations) -ForegroundColor Cyan
    Write-Host ("[dev-back] Bootstrap DDL local = " + $env:Database__RunDevBootstrap) -ForegroundColor Cyan

    if (-not $NoBuild) {
        if (Test-RestoreRequired) {
            Write-Host "[dev-back] Restaurando dependencias (assets ausentes o desactualizados)..." -ForegroundColor Yellow
            & $dotnetExe restore $apiProject --verbosity minimal
            if ($LASTEXITCODE -ne 0) {
                throw "Fallo dotnet restore (exit $LASTEXITCODE)."
            }
        }

        Write-Host "[dev-back] Build incremental con memoria acotada..." -ForegroundColor Yellow
        & $dotnetExe build $apiProject --no-restore -m:1 --verbosity minimal `
            /nodeReuse:false `
            /p:UseSharedCompilation=false `
            /p:BuildInParallel=false `
            /p:RunAnalyzers=false `
            /p:RunAnalyzersDuringBuild=false `
            /p:DebugType=None `
            /p:DebugSymbols=false
        if ($LASTEXITCODE -ne 0) {
            throw "Fallo dotnet build (exit $LASTEXITCODE)."
        }
    }
    elseif (-not (Test-Path -LiteralPath $apiDll)) {
        throw "No existe $apiDll. Ejecuta primero make dev-back."
    }

    Write-Host "[dev-back] Backend -> http://localhost:5002  (Swagger: http://localhost:5002/swagger)" -ForegroundColor Green
    Write-Host "[dev-back] Ejecutando DLL compilada (sin segundo build ni restore)..." -ForegroundColor Green
    & $dotnetExe $apiDll
}
finally {
    Pop-Location
}
