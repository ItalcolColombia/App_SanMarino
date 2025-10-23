# Script PowerShell para probar el filtrado de granjas en el módulo de lotes
# Este script simula las llamadas que hace el frontend del módulo de lotes

$apiUrl = "http://localhost:5002"

Write-Host "=== PRUEBA DE FILTRADO DE GRANJAS EN MÓDULO DE LOTES ===" -ForegroundColor Green

# Simular el usuario que está logueado
$userId = "2a3056c0-f591-4f02-a6f4-88476910a738"  # Usuario de prueba

Write-Host "`nUsuario simulado: $userId" -ForegroundColor Cyan

# 1. Probar la carga de granjas (como lo hace el módulo de lotes)
Write-Host "`n1. Probando carga de granjas para el módulo de lotes:" -ForegroundColor Yellow
$headers = @{
    "Content-Type" = "application/json"
}

try {
    # Simular la llamada que hace el frontend con el ID del usuario
    $farmResponse = Invoke-RestMethod -Uri "$apiUrl/api/Farm?id_user_session=$userId" -Method GET -Headers $headers
    Write-Host "✅ ÉXITO - Granjas cargadas para el módulo de lotes" -ForegroundColor Green
    Write-Host "Cantidad de granjas disponibles: $($farmResponse.Count)" -ForegroundColor White
    
    if ($farmResponse.Count -gt 0) {
        Write-Host "`nGranjas disponibles:" -ForegroundColor Gray
        foreach ($farm in $farmResponse) {
            Write-Host "  - ID: $($farm.id), Nombre: $($farm.name), Compañía: $($farm.companyId)" -ForegroundColor Gray
        }
        
        # 2. Probar la carga de lotes (usando una granja específica)
        $firstFarmId = $farmResponse[0].id
        Write-Host "`n2. Probando carga de lotes para la granja ID: $firstFarmId" -ForegroundColor Yellow
        
        try {
            $loteResponse = Invoke-RestMethod -Uri "$apiUrl/api/Lote" -Method GET -Headers $headers
            Write-Host "✅ ÉXITO - Lotes cargados" -ForegroundColor Green
            Write-Host "Cantidad de lotes: $($loteResponse.Count)" -ForegroundColor White
            
            # Filtrar lotes por la granja específica
            $lotesDeLaGranja = $loteResponse | Where-Object { $_.granjaId -eq $firstFarmId }
            Write-Host "Lotes en la granja $firstFarmId: $($lotesDeLaGranja.Count)" -ForegroundColor White
            
            if ($lotesDeLaGranja.Count -gt 0) {
                Write-Host "`nLotes en la granja:" -ForegroundColor Gray
                foreach ($lote in $lotesDeLaGranja) {
                    Write-Host "  - Lote ID: $($lote.loteId), Nombre: $($lote.loteNombre)" -ForegroundColor Gray
                }
            }
            
        } catch {
            Write-Host "❌ ERROR - Falló al cargar lotes" -ForegroundColor Red
            Write-Host "Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
            Write-Host "Error Message: $($_.Exception.Message)" -ForegroundColor Red
        }
    } else {
        Write-Host "⚠️ ADVERTENCIA - No hay granjas disponibles para este usuario" -ForegroundColor Yellow
        Write-Host "Esto podría indicar que el usuario no tiene granjas asignadas" -ForegroundColor Yellow
    }
    
} catch {
    Write-Host "❌ ERROR - Falló al cargar granjas" -ForegroundColor Red
    Write-Host "Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    Write-Host "Error Message: $($_.Exception.Message)" -ForegroundColor Red
}

# 3. Probar con un usuario sin granjas asignadas
Write-Host "`n3. Probando con usuario sin granjas asignadas:" -ForegroundColor Yellow
$userSinGranjas = "00000000-0000-0000-0000-000000000000"

try {
    $response = Invoke-RestMethod -Uri "$apiUrl/api/Farm?id_user_session=$userSinGranjas" -Method GET -Headers $headers
    Write-Host "✅ ÉXITO - Usuario sin granjas manejado correctamente" -ForegroundColor Green
    Write-Host "Cantidad de granjas: $($response.Count)" -ForegroundColor White
    
    if ($response.Count -eq 0) {
        Write-Host "  - Correcto: No se devolvieron granjas para usuario sin asignaciones" -ForegroundColor Green
    }
} catch {
    Write-Host "❌ ERROR - Falló con usuario sin granjas" -ForegroundColor Red
    Write-Host "Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    Write-Host "Error Message: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== PRUEBA COMPLETADA ===" -ForegroundColor Green
Write-Host "Revisa la consola del backend para ver los logs de debug" -ForegroundColor Yellow
Write-Host "NOTA: El módulo de lotes ahora filtra las granjas según el usuario logueado" -ForegroundColor Cyan
