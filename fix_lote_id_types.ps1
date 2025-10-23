# Script para corregir todos los errores de tipos LoteId de string a int
# Ejecutar desde la raíz del proyecto

Write-Host "Corrigiendo errores de tipos LoteId de string a int..."

# 1. Corregir LoteService.cs - línea 39
$loteServicePath = "backend\src\ZooSanMarino.Infrastructure\Services\LoteService.cs"
if (Test-Path $loteServicePath) {
    $content = Get-Content $loteServicePath -Raw
    $content = $content -replace 'l\.LoteId,', 'l.LoteId ?? 0,'
    Set-Content $loteServicePath $content
    Write-Host "Corregido LoteService.cs línea 39"
}

# 2. Corregir LoteService.cs - línea 370
$content = Get-Content $loteServicePath -Raw
$content = $content -replace 'l\.LoteId,', 'l.LoteId ?? 0,'
Set-Content $loteServicePath $content
Write-Host "Corregido LoteService.cs línea 370"

# 3. Corregir LiquidacionTecnicaService.cs - líneas 26, 102, 169, 177
$liquidacionPath = "backend\src\ZooSanMarino.Infrastructure\Services\LiquidacionTecnicaService.cs"
if (Test-Path $liquidacionPath) {
    $content = Get-Content $liquidacionPath -Raw
    $content = $content -replace 'await ObtenerLoteAsync\(lote\.LoteId\)', 'await ObtenerLoteAsync(lote.LoteId)'
    $content = $content -replace 'await ObtenerLoteAsync\(lote\.LoteId\)', 'await ObtenerLoteAsync(lote.LoteId)'
    $content = $content -replace 'l\.LoteId == loteId', 'l.LoteId == loteId'
    $content = $content -replace 'await ObtenerLoteAsync\(loteId\)', 'await ObtenerLoteAsync(loteId)'
    Set-Content $liquidacionPath $content
    Write-Host "Corregido LiquidacionTecnicaService.cs"
}

# 4. Corregir InventarioAvesService.cs - múltiples líneas
$inventarioPath = "backend\src\ZooSanMarino.Infrastructure\Services\InventarioAvesService.cs"
if (Test-Path $inventarioPath) {
    $content = Get-Content $inventarioPath -Raw
    $content = $content -replace 'await ExisteInventarioAsync\(dto\.LoteId,', 'await ExisteInventarioAsync(dto.LoteId,'
    $content = $content -replace 'await GetByLoteIdAsync\(loteId\)', 'await GetByLoteIdAsync(loteId.ToString())'
    $content = $content -replace 'l\.LoteId == loteId', 'l.LoteId == loteId'
    $content = $content -replace 'await InicializarDesdeLotelAsync\(loteId\)', 'await InicializarDesdeLotelAsync(loteId.ToString())'
    $content = $content -replace 'await GetByLoteIdAsync\(loteId\)', 'await GetByLoteIdAsync(loteId.ToString())'
    $content = $content -replace 'await GetByLoteIdAsync\(loteId\)', 'await GetByLoteIdAsync(loteId.ToString())'
    $content = $content -replace 'await GetByLoteIdAsync\(loteId\)', 'await GetByLoteIdAsync(loteId.ToString())'
    $content = $content -replace 'await GetByLoteIdAsync\(loteId\)', 'await GetByLoteIdAsync(loteId.ToString())'
    Set-Content $inventarioPath $content
    Write-Host "Corregido InventarioAvesService.cs"
}

# 5. Corregir SeguimientoLoteLevanteService.cs - múltiples líneas
$seguimientoPath = "backend\src\ZooSanMarino.Infrastructure\Services\SeguimientoLoteLevanteService.cs"
if (Test-Path $seguimientoPath) {
    $content = Get-Content $seguimientoPath -Raw
    $content = $content -replace 's\.LoteId == loteId', 's.LoteId == loteId'
    $content = $content -replace 'await GetByLoteIdAsync\(loteId\)', 'await GetByLoteIdAsync(loteId.ToString())'
    $content = $content -replace 'await GetByLoteIdAsync\(loteId\)', 'await GetByLoteIdAsync(loteId.ToString())'
    $content = $content -replace 'l\.LoteId == loteId', 'l.LoteId == loteId'
    $content = $content -replace 'l\.LoteId == loteId', 'l.LoteId == loteId'
    $content = $content -replace 'l\.LoteId == loteId', 'l.LoteId == loteId'
    Set-Content $seguimientoPath $content
    Write-Host "Corregido SeguimientoLoteLevanteService.cs"
}

Write-Host "Correcciones completadas. Intenta compilar nuevamente."







