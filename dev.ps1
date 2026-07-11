# =============================================================================
#  dev.ps1  -  Levanta BACKEND + FRONTEND en dos ventanas de PowerShell
# -----------------------------------------------------------------------------
#  Requisitos ya cubiertos en la maquina:
#    - .NET 10 SDK user-local en %USERPROFILE%\.dotnet
#    - Node portable >= 22.22.3 en %USERPROFILE%\node-portable\node-v*
#    - Postgres local escuchando en :5433 (base sanmarinoapplocal)
#
#  Uso:   .\dev.ps1     (o: make dev)
#  (cada servicio queda en su propia ventana; cerrala para detenerlo)
#  NOTA: ASCII puro a proposito (Windows PowerShell 5.1 malparsea UTF-8 sin BOM).
# =============================================================================
$ErrorActionPreference = 'Stop'

Write-Host "[dev] Verificando Postgres local en :5433..." -ForegroundColor Cyan
$pg = Test-NetConnection -ComputerName 127.0.0.1 -Port 5433 -WarningAction SilentlyContinue
if (-not $pg.TcpTestSucceeded) {
    Write-Host "[dev] AVISO: Postgres NO responde en :5433. El backend crasheara al arrancar." -ForegroundColor Yellow
    Write-Host "[dev]        Levanta la base (PG17) antes de continuar." -ForegroundColor Yellow
}

Start-Process powershell -ArgumentList '-NoExit','-ExecutionPolicy','Bypass','-File',(Join-Path $PSScriptRoot 'dev-back.ps1')
Start-Process powershell -ArgumentList '-NoExit','-ExecutionPolicy','Bypass','-File',(Join-Path $PSScriptRoot 'dev-front.ps1')

Write-Host "[dev] Backend y Frontend lanzados en ventanas separadas." -ForegroundColor Green
Write-Host "[dev]   Backend : http://localhost:5002/swagger" -ForegroundColor Green
Write-Host "[dev]   Frontend: http://localhost:4200" -ForegroundColor Green
