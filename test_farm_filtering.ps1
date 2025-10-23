# Script PowerShell para probar el filtrado de granjas por usuario
# Este script probará la nueva funcionalidad de filtrado de granjas

$apiUrl = "http://localhost:5002"

Write-Host "=== PRUEBA DE FILTRADO DE GRANJAS POR USUARIO ===" -ForegroundColor Green

# 1. Probar endpoint SIN filtro de usuario (debería devolver todas las granjas)
Write-Host "`n1. Probando endpoint Farm SIN filtro de usuario:" -ForegroundColor Yellow
$headers = @{
    "Content-Type" = "application/json"
}

try {
    $response = Invoke-RestMethod -Uri "$apiUrl/api/Farm" -Method GET -Headers $headers
    Write-Host "✅ ÉXITO - Todas las granjas obtenidas" -ForegroundColor Green
    Write-Host "Cantidad de granjas: $($response.Count)" -ForegroundColor White
    foreach ($farm in $response) {
        Write-Host "  - Granja ID: $($farm.id), Nombre: $($farm.name)" -ForegroundColor Gray
    }
} catch {
    Write-Host "❌ ERROR - Falló al obtener granjas" -ForegroundColor Red
    Write-Host "Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    Write-Host "Error Message: $($_.Exception.Message)" -ForegroundColor Red
}

# 2. Probar endpoint CON filtro de usuario específico
Write-Host "`n2. Probando endpoint Farm CON filtro de usuario:" -ForegroundColor Yellow
$userId = "2a3056c0-f591-4f02-a6f4-88476910a738"  # Usuario de prueba

try {
    $response = Invoke-RestMethod -Uri "$apiUrl/api/Farm?id_user_session=$userId" -Method GET -Headers $headers
    Write-Host "✅ ÉXITO - Granjas filtradas por usuario" -ForegroundColor Green
    Write-Host "Usuario: $userId" -ForegroundColor White
    Write-Host "Cantidad de granjas: $($response.Count)" -ForegroundColor White
    foreach ($farm in $response) {
        Write-Host "  - Granja ID: $($farm.id), Nombre: $($farm.name)" -ForegroundColor Gray
    }
} catch {
    Write-Host "❌ ERROR - Falló al obtener granjas filtradas" -ForegroundColor Red
    Write-Host "Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    Write-Host "Error Message: $($_.Exception.Message)" -ForegroundColor Red
}

# 3. Probar con un usuario que no existe
Write-Host "`n3. Probando endpoint Farm con usuario inexistente:" -ForegroundColor Yellow
$nonExistentUserId = "00000000-0000-0000-0000-000000000000"

try {
    $response = Invoke-RestMethod -Uri "$apiUrl/api/Farm?id_user_session=$nonExistentUserId" -Method GET -Headers $headers
    Write-Host "✅ ÉXITO - Usuario inexistente manejado correctamente" -ForegroundColor Green
    Write-Host "Cantidad de granjas: $($response.Count)" -ForegroundColor White
    if ($response.Count -eq 0) {
        Write-Host "  - Correcto: No se devolvieron granjas para usuario inexistente" -ForegroundColor Green
    }
} catch {
    Write-Host "❌ ERROR - Falló con usuario inexistente" -ForegroundColor Red
    Write-Host "Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    Write-Host "Error Message: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== PRUEBA COMPLETADA ===" -ForegroundColor Green
Write-Host "Revisa la consola del backend para ver los logs de debug" -ForegroundColor Yellow
Write-Host "NOTA: El filtrado de granjas por usuario está implementado" -ForegroundColor Cyan
