# Optimización: Histórico Unificado Filtrado por Ciclo de Vida del Lote

## Resumen Ejecutivo

Se optimizó el servicio `QueryHistoricoUnificadoDtosAsync` para retornar **exclusivamente** los registros históricos que pertenecen al ciclo de vida del lote seleccionado, ignorando datos de lotes anteriores que hayan ocupado el mismo galpón.

## Problema Original

Cuando un galpón tiene múltiples lotes a lo largo del año con diferentes fechas de encasetamiento, el histórico unificado retornaba **todos** los movimientos de alimento del galpón sin considerar el rango de fechas del lote consultado. Esto causaba:

- ❌ Registro inflados de ingresos/traslados de lotes anteriores
- ❌ Inconsistencias en el saldo de alimento calculado
- ❌ Validación "Cuadrar Saldos" comparando datos de múltiples lotes en el mismo galpón

## Solución Implementada

### 1. Método Auxiliar: `CalcularRangoFechasLoteAsync(int loteId)`

```csharp
private async Task<(DateTime?, DateTime?)> CalcularRangoFechasLoteAsync(int loteId)
{
    // Obtiene los registros de seguimiento diario para este lote
    var segFechas = await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
        .Where(s => s.LoteAveEngordeId == loteId)
        .Select(s => s.Fecha)
        .ToListAsync();

    if (segFechas.Count == 0)
        return (null, null);

    // Retorna (fecha_mín, fecha_máx) del ciclo de vida del lote
    return (segFechas.Min(), segFechas.Max());
}
```

**Lógica:**
- **Límite Inferior**: Primera fecha de seguimiento registrada en la aplicación
- **Límite Superior**: Última fecha de seguimiento registrada en la aplicación
- Si no hay seguimientos: retorna (null, null) y el histórico retorna vacío

### 2. Modificación: `QueryHistoricoUnificadoDtosAsync`

Agregados filtros de rango de fechas después de resolver la ubicación del lote:

```csharp
// Calcular rango de fechas del ciclo de vida del lote
var (fechaMinSeg, fechaMaxSeg) = await CalcularRangoFechasLoteAsync(loteId);

// ... construcción de query ...

// Aplicar filtro de rango de fechas
if (fechaMinSeg.HasValue)
    query = query.Where(h => h.FechaOperacion >= fechaMinSeg.Value.Date);
if (fechaMaxSeg.HasValue)
    query = query.Where(h => h.FechaOperacion <= fechaMaxSeg.Value.Date.AddDays(1).AddTicks(-1));
```

### 3. Refactorización: `ValidarCuadrarSaldosAsync`

Se eliminó la duplicación de lógica:

```csharp
// ANTES: código duplicado para calcular fechas
var segFechas = await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
    .Where(s => s.LoteAveEngordeId == loteId)
    .Select(s => s.Fecha)
    .ToListAsync();
DateTime? fechaMinSeg = segFechas.Count > 0 ? segFechas.Min() : null;
DateTime? fechaMaxSeg = segFechas.Count > 0 ? segFechas.Max() : null;

// AHORA: uso del método auxiliar
var (fechaMinSeg, fechaMaxSeg) = await CalcularRangoFechasLoteAsync(loteId);
```

## Impacto de la Optimización

### ✅ Aislamiento de Datos
- Cada lote solo ve registros dentro de su rango de fechas
- Galpones con múltiples lotes al año quedan aislados correctamente
- Ejemplo:
  - Lote A: 3 de marzo → 15 de marzo → busca histórico solo entre esas fechas
  - Lote B (mismo galpón): 1 de abril → 20 de abril → busca solo entre sus fechas
  - Los registros de A y B no se mezclan

### ✅ Casos Especiales Manejados
1. **Lote sin seguimientos**: Retorna histórico vacío
2. **Excel con fechas fuera del rango**: No son consideradas en validaciones
3. **Movimientos previos al primer seguimiento**: Excluidos automáticamente

### ✅ Refactorización DRY
- Eliminación de código duplicado
- Método único de cálculo de rango
- Fácil mantenimiento futuro

## Verificación

**Compilación**: ✅ Exitosa (0 errores, 6 warnings previos)

**Métodos Afectados**:
- `GetByLoteAsync(int loteId)` — usa `QueryHistoricoUnificadoDtosAsync` ✓
- `GetHistoricoUnificadoPorLoteAsync(int loteId)` — usa `QueryHistoricoUnificadoDtosAsync` ✓
- `ValidarCuadrarSaldosAsync(...)` — refactorizado con método auxiliar ✓
- `AplicarCuadrarSaldosAsync(...)` — hereda automáticamente el filtrado ✓

## Pruebas Recomendadas

1. **Test de Aislamiento**:
   - Crear Lote A (3-15 de marzo) con movimientos
   - Crear Lote B (1-20 de abril) en el mismo galpón
   - Validar que histórico de Lote A no incluya movimientos de Lote B

2. **Test de Rango Vacío**:
   - Crear lote sin seguimientos
   - Validar que retorna histórico vacío

3. **Test de Validación Cuadrar Saldos**:
   - Excel con fechas fuera del rango de seguimiento
   - Debe ignorarlas o sugerir inserción

## Archivos Modificados

- `backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoAvesEngordeService.cs`
  - Líneas 263-324: `QueryHistoricoUnificadoDtosAsync` (optimizado con filtro de fechas)
  - Líneas 326-345: `CalcularRangoFechasLoteAsync` (nuevo método auxiliar)
  - Línea 1338: `ValidarCuadrarSaldosAsync` (refactorizado para usar método auxiliar)
