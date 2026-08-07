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

# -----------------------------------------------------------------------------
#  Guard: bajar el backend previo antes de compilar.
#  Un ZooSanMarino.API vivo de una corrida anterior mantiene abiertos los .dll de
#  Application/Infrastructure/Domain -> MSBuild no los puede sobreescribir y el
#  build muere con MSB3026 x10 + MSB3027/MSB3021 ("The file is locked by:
#  ZooSanMarino.API (PID)"). Ademas deja ocupado el :5002.
# -----------------------------------------------------------------------------
function Stop-BackendPrevio {
    $objetivo = @()

    # 1) Cualquier API viva por nombre (aunque escuche en otro puerto).
    $objetivo += @(Get-Process -Name 'ZooSanMarino.API' -ErrorAction SilentlyContinue |
                   Select-Object -ExpandProperty Id)

    # 2) Quien tenga tomado el :5002, solo si es nuestro (API o el host dotnet).
    $conns = @()
    try { $conns = @(Get-NetTCPConnection -LocalPort 5002 -State Listen -ErrorAction SilentlyContinue) } catch { }
    foreach ($c in $conns) {
        $proc = Get-Process -Id $c.OwningProcess -ErrorAction SilentlyContinue
        if (-not $proc) { continue }
        if ($proc.ProcessName -eq 'ZooSanMarino.API' -or $proc.ProcessName -eq 'dotnet') {
            $objetivo += $proc.Id
        }
        else {
            Write-Host ("[dev-back] AVISO: :5002 lo ocupa " + $proc.ProcessName + " (PID " + $proc.Id + ") - no es el backend, no lo toco") -ForegroundColor Yellow
        }
    }

    # 3) El host `dotnet run` que lanzo la API (si no, queda huerfano).
    foreach ($id in @($objetivo)) {
        $hijo = Get-CimInstance Win32_Process -Filter "ProcessId=$id" -ErrorAction SilentlyContinue
        if (-not $hijo -or -not $hijo.ParentProcessId) { continue }
        $padre = Get-CimInstance Win32_Process -Filter "ProcessId=$($hijo.ParentProcessId)" -ErrorAction SilentlyContinue
        if ($padre -and $padre.Name -eq 'dotnet.exe' -and $padre.CommandLine -like '*run*') {
            $objetivo += [int]$padre.ProcessId
        }
    }

    $objetivo = @($objetivo | Sort-Object -Unique | Where-Object { $_ -ne $PID })
    if ($objetivo.Count -eq 0) { return }

    foreach ($id in $objetivo) {
        $p = Get-Process -Id $id -ErrorAction SilentlyContinue
        if (-not $p) { continue }
        Write-Host ("[dev-back] Cerrando backend previo: " + $p.ProcessName + " (PID " + $id + ")") -ForegroundColor Yellow
        try { Stop-Process -Id $id -Force -Confirm:$false -ErrorAction Stop }
        catch { Write-Host ("[dev-back] AVISO: no pude cerrar el PID " + $id + " - " + $_.Exception.Message) -ForegroundColor Yellow }
    }

    # Esperar a que Windows libere los handles y el puerto (hasta ~10s).
    for ($i = 0; $i -lt 20; $i++) {
        $viva = Get-Process -Name 'ZooSanMarino.API' -ErrorAction SilentlyContinue
        $puerto = @()
        try { $puerto = @(Get-NetTCPConnection -LocalPort 5002 -State Listen -ErrorAction SilentlyContinue) } catch { }
        if (-not $viva -and $puerto.Count -eq 0) { return }
        Start-Sleep -Milliseconds 500
    }
    Write-Host "[dev-back] AVISO: el backend previo no termino de cerrar; si el build falla por archivos bloqueados, reintenta." -ForegroundColor Yellow
}

Stop-BackendPrevio

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
