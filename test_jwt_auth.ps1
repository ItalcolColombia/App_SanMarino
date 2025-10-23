# Script PowerShell para probar la autenticación JWT
# Este script probará diferentes aspectos de la autenticación

$token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IjkyYWZlNGM4LWJmM2UtNGFiMC1hMzFhLTQ2Nzg5MDQ2MzU0MiIsInN1YiI6IjkyYWZlNGM4LWJmM2UtNGFiMC1hMzFhLTQ2Nzg5MDQ2MzU0MiIsInVuaXF1ZV9uYW1lIjoibW9pZXNiYnVnYUBnbWFpbC5jb20iLCJlbWFpbCI6Im1vaWVzYmJ1Z2FAZ21haWwuY29tIiwiZmlyc3ROYW1lIjoiTXVyaWxsbyByaXZhcyIsInN1ck5hbWUiOiJqb3NlIG1vaXNlcyIsImh0dHA6Ly9zY2hlbWFzLm1pY3Jvc29mdC5jb20vd3MvMjAwOC8wNi9pZGVudGl0eS9jbGFpbXMvcm9sZSI6IkFkbWluIiwiY29tcGFueV9pZCI6IjEiLCJjb21wYW55IjoiQWdyaWNvbGEgc2FubWFyaW5vIiwicGVybWlzc2lvbiI6InJvbGVzOm1hbmFnZSIsImV4cCI6MTc1OTkzODUwOCwiaXNzIjoiWm9vU2FuTWFyaW5vQVBJIiwiYXVkIjoiWm9vU2FuTWFyaW5vQ2xpZW50In0.qazLC5zNeV1K7Sf5uZOORJJa8MbtI2CfGHr5k73y7qg"
$apiUrl = "http://localhost:5002"

Write-Host "=== PRUEBA DE AUTENTICACIÓN JWT (TEMPORAL SIN AUTENTICACIÓN) ===" -ForegroundColor Green

# 1. Probar endpoint SIN autenticación (temporal)
Write-Host "`n1. Probando endpoint UserFarm SIN autenticación (temporal):" -ForegroundColor Yellow
$headers = @{
    "Content-Type" = "application/json"
    # "Authorization" = "Bearer $token"  # Comentado temporalmente
}

$body = @{
    UserId = "2a3056c0-f591-4f02-a6f4-88476910a738"
    FarmId = 30
    IsAdmin = $false
    IsDefault = $true
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$apiUrl/api/UserFarm" -Method POST -Body $body -Headers $headers
    Write-Host "✅ ÉXITO - Registro de usuario-granja completado" -ForegroundColor Green
    Write-Host ($response | ConvertTo-Json -Depth 3) -ForegroundColor White
} catch {
    Write-Host "❌ ERROR - Falló el registro" -ForegroundColor Red
    Write-Host "Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    Write-Host "Error Message: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody" -ForegroundColor Red
    }
}

# 2. Probar endpoint CON autenticación (para comparar)
Write-Host "`n2. Probando endpoint UserFarm CON autenticación (para comparar):" -ForegroundColor Yellow
$headersWithAuth = @{
    "Content-Type" = "application/json"
    "Authorization" = "Bearer $token"
}

try {
    $response = Invoke-RestMethod -Uri "$apiUrl/api/UserFarm" -Method POST -Body $body -Headers $headersWithAuth
    Write-Host "✅ ÉXITO - Registro con autenticación también funciona" -ForegroundColor Green
    Write-Host ($response | ConvertTo-Json -Depth 3) -ForegroundColor White
} catch {
    Write-Host "❌ ERROR - Falló con autenticación" -ForegroundColor Red
    Write-Host "Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    Write-Host "Error Message: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== PRUEBA COMPLETADA ===" -ForegroundColor Green
Write-Host "Revisa la consola del backend para ver los logs de debug" -ForegroundColor Yellow
Write-Host "NOTA: La autenticación está temporalmente deshabilitada para permitir el registro" -ForegroundColor Cyan
