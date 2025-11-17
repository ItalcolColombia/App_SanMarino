# Script PowerShell para probar la funcionalidad de filtrado de lotes por granja
# Este script simula el comportamiento del frontend

$apiUrl = "http://localhost:5002"

Write-Host "=== PRUEBA DE FILTRADO DE LOTES POR GRANJA ===" -ForegroundColor Green

# Simular el usuario que está logueado
$userId = "2a3056c0-f591-4f02-a6f4-88476910a738"  # Usuario de prueba

Write-Host "`nUsuario simulado: $userId" -ForegroundColor Cyan

# 1. Obtener las granjas disponibles para el usuario
Write-Host "`n1. Obteniendo granjas disponibles para el usuario:" -ForegroundColor Yellow
$headers = @{
    "Content-Type" = "application/json"
}

try {
    $farmResponse = Invoke-RestMethod -Uri "$apiUrl/api/Farm?id_user_session=$userId" -Method GET -Headers $headers
    Write-Host "✅ ÉXITO - Granjas obtenidas" -ForegroundColor Green
    Write-Host "Cantidad de granjas: $($farmResponse.Count)" -ForegroundColor White
    
    if ($farmResponse.Count -gt 0) {
        Write-Host "`nGranjas disponibles:" -ForegroundColor Gray
        foreach ($farm in $farmResponse) {
            Write-Host "  - ID: $($farm.id), Nombre: $($farm.name)" -ForegroundColor Gray
        }
        
        # 2. Obtener todos los lotes
        Write-Host "`n2. Obteniendo todos los lotes:" -ForegroundColor Yellow
        try {
            $loteResponse = Invoke-RestMethod -Uri "$apiUrl/api/Lote" -Method GET -Headers $headers
            Write-Host "✅ ÉXITO - Lotes obtenidos" -ForegroundColor Green
            Write-Host "Cantidad total de lotes: $($loteResponse.Count)" -ForegroundColor White
            
            # 3. Probar filtrado por cada granja
            Write-Host "`n3. Probando filtrado por granja:" -ForegroundColor Yellow
            foreach ($farm in $farmResponse) {
                $farmId = $farm.id
                $farmName = $farm.name
                
                # Filtrar lotes por granja (simulando el comportamiento del frontend)
                $lotesDeLaGranja = $loteResponse | Where-Object { $_.granjaId -eq $farmId }
                
                Write-Host "`n  Granja: $farmName (ID: $farmId)" -ForegroundColor Cyan
                Write-Host "  Lotes en esta granja: $($lotesDeLaGranja.Count)" -ForegroundColor White
                
                if ($lotesDeLaGranja.Count -gt 0) {
                    Write-Host "  Lotes encontrados:" -ForegroundColor Gray
                    foreach ($lote in $lotesDeLaGranja) {
                        Write-Host "    - Lote ID: $($lote.loteId), Nombre: $($lote.loteNombre)" -ForegroundColor Gray
                    }
                } else {
                    Write-Host "  ⚠️ No hay lotes en esta granja" -ForegroundColor Yellow
                }
            }
            
            # 4. Simular comportamiento sin granja seleccionada
            Write-Host "`n4. Simulando comportamiento sin granja seleccionada:" -ForegroundColor Yellow
            Write-Host "  - Tabla de lotes: OCULTA" -ForegroundColor Red
            Write-Host "  - Mensaje mostrado: 'Selecciona una granja'" -ForegroundColor Red
            Write-Host "  - Lotes mostrados: 0" -ForegroundColor Red
            
            # 5. Simular comportamiento con granja seleccionada
            $firstFarm = $farmResponse[0]
            Write-Host "`n5. Simulando comportamiento con granja seleccionada ($($firstFarm.name)):" -ForegroundColor Yellow
            $lotesFiltrados = $loteResponse | Where-Object { $_.granjaId -eq $firstFarm.id }
            Write-Host "  - Tabla de lotes: VISIBLE" -ForegroundColor Green
            Write-Host "  - Granja seleccionada: $($firstFarm.name)" -ForegroundColor Green
            Write-Host "  - Lotes mostrados: $($lotesFiltrados.Count)" -ForegroundColor Green
            
        } catch {
            Write-Host "❌ ERROR - Falló al obtener lotes" -ForegroundColor Red
            Write-Host "Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
            Write-Host "Error Message: $($_.Exception.Message)" -ForegroundColor Red
        }
        
    } else {
        Write-Host "⚠️ ADVERTENCIA - No hay granjas disponibles para este usuario" -ForegroundColor Yellow
        Write-Host "Esto podría indicar que el usuario no tiene granjas asignadas" -ForegroundColor Yellow
    }
    
} catch {
    Write-Host "❌ ERROR - Falló al obtener granjas" -ForegroundColor Red
    Write-Host "Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    Write-Host "Error Message: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== PRUEBA COMPLETADA ===" -ForegroundColor Green
Write-Host "NOTA: El módulo de lotes ahora requiere selección de granja obligatoria" -ForegroundColor Cyan
Write-Host "NOTA: La tabla solo se muestra cuando hay una granja seleccionada" -ForegroundColor Cyan
