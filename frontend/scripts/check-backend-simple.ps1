# Script simple para verificar el backend
Write-Host "Verificando Backend San Marino..." -ForegroundColor Green
Write-Host "=================================" -ForegroundColor Green

$backendUrl = "http://localhost:5002"
$apiUrl = "$backendUrl/api"

# Verificar si el backend responde
try {
    Write-Host "Probando conexion al backend..." -ForegroundColor Yellow
    $response = Invoke-RestMethod -Uri $backendUrl -Method Get -TimeoutSec 5
    Write-Host "Backend Base: OK" -ForegroundColor Green
} catch {
    Write-Host "Backend Base: ERROR - $($_.Exception.Message)" -ForegroundColor Red
}

# Verificar Swagger
try {
    Write-Host "Probando Swagger..." -ForegroundColor Yellow
    $response = Invoke-RestMethod -Uri "$backendUrl/swagger" -Method Get -TimeoutSec 5
    Write-Host "Swagger: OK" -ForegroundColor Green
} catch {
    Write-Host "Swagger: ERROR - $($_.Exception.Message)" -ForegroundColor Red
}

# Verificar API de usuarios
try {
    Write-Host "Probando API Usuarios..." -ForegroundColor Yellow
    $response = Invoke-RestMethod -Uri "$apiUrl/Users" -Method Get -TimeoutSec 5
    Write-Host "API Usuarios: OK" -ForegroundColor Green
} catch {
    Write-Host "API Usuarios: ERROR - $($_.Exception.Message)" -ForegroundColor Red
}

# Verificar API de granjas
try {
    Write-Host "Probando API Granjas..." -ForegroundColor Yellow
    $response = Invoke-RestMethod -Uri "$apiUrl/Farm" -Method Get -TimeoutSec 5
    Write-Host "API Granjas: OK" -ForegroundColor Green
} catch {
    Write-Host "API Granjas: ERROR - $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "Si hay errores, el backend no esta corriendo." -ForegroundColor Yellow
Write-Host "Para iniciarlo:" -ForegroundColor White
Write-Host "cd ..\backend\src\ZooSanMarino.API" -ForegroundColor Gray
Write-Host "dotnet run" -ForegroundColor Gray
