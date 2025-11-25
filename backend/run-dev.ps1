# Script para ejecutar el backend en modo Development
Write-Host "=== INICIANDO BACKEND EN MODO DEVELOPMENT ===" -ForegroundColor Cyan
Write-Host ""

$apiPath = "C:\Users\SAN MARINO\Documents\App_SanMarino_intalcol\App_SanMarino\backend\src\ZooSanMarino.API"

# Detener procesos que puedan estar usando el puerto
Write-Host "Verificando puerto 5002..." -ForegroundColor Yellow
$process = Get-NetTCPConnection -LocalPort 5002 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique
if ($process) {
    Write-Host "Deteniendo proceso $process que usa el puerto 5002..." -ForegroundColor Yellow
    Stop-Process -Id $process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

# Configurar entorno - IMPORTANTE: debe estar ANTES de ejecutar dotnet run
$env:ASPNETCORE_ENVIRONMENT = "Development"
Write-Host "ASPNETCORE_ENVIRONMENT = $env:ASPNETCORE_ENVIRONMENT" -ForegroundColor Green

# Limpiar cualquier variable ZOO_CONN que pueda estar sobrescribiendo
if ($env:ZOO_CONN) {
    Write-Host "ADVERTENCIA: ZOO_CONN esta configurada, puede sobrescribir appsettings" -ForegroundColor Yellow
    Write-Host "  ZOO_CONN = $env:ZOO_CONN" -ForegroundColor Gray
    $env:ZOO_CONN = $null
    Write-Host "  ZOO_CONN limpiada para usar appsettings.Development.json" -ForegroundColor Green
}

Write-Host ""

# Verificar configuración
Write-Host "Verificando configuracion..." -ForegroundColor Yellow
if (Test-Path "$apiPath\appsettings.Development.json") {
    $config = Get-Content "$apiPath\appsettings.Development.json" | ConvertFrom-Json
    $connStr = $config.ConnectionStrings.ZooSanMarinoContext
    Write-Host "ConnectionString que se usara:" -ForegroundColor Gray
    Write-Host "  $connStr" -ForegroundColor Gray
    Write-Host ""
} else {
    Write-Host "ADVERTENCIA: appsettings.Development.json no encontrado" -ForegroundColor Red
    Write-Host ""
}

# Navegar al directorio y ejecutar con perfil específico
Set-Location $apiPath
Write-Host "Iniciando backend con perfil 'http' (Development)..." -ForegroundColor Yellow
Write-Host "Presiona Ctrl+C para detener" -ForegroundColor Gray
Write-Host ""

# Usar --launch-profile para asegurar que use el perfil correcto
dotnet run --launch-profile http

