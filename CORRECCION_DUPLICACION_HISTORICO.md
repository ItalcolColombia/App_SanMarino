# Corrección: Duplicación de Información en Tabla de Histórico Unificado

## Problema Identificado

La información de ingresos de alimento aparecía duplicada en la tabla HTML en diferentes filas/fechas, aunque el histórico unificado solo contenía un registro del movimiento.

**Raíz del problema:**
- `BuildStockMetadataPatchAsync()` en el backend NO aplicaba el filtro de rango de fechas del lote
- Cuando se creaba un seguimiento para una fecha específica, la metadata se llenaba con **todos** los movimientos históricos de esa fecha sin considerar si pertenecían al lote actual
- En galpones con múltiples lotes, esto causaba que movimientos de lotes anteriores se incluyeran en la metadata

## Solución Implementada

### Backend: Aplicar Filtro de Rango de Fechas en `BuildStockMetadataPatchAsync()`

```csharp
// ANTES: solo filtraba por fecha de operación
var agg = await _ctx.LoteRegistroHistoricoUnificados
    .AsNoTracking()
    .Where(x =>
        x.CompanyId == _current.CompanyId
        && x.LoteAveEngordeId == loteId
        && x.FechaOperacion == day  // ← Sin límite superior
        ...

// DESPUÉS: aplica rango de fechas del lote
var (fechaMinSeg, fechaMaxSeg) = await CalcularRangoFechasLoteAsync(loteId);

var query = _ctx.LoteRegistroHistoricoUnificados
    .AsNoTracking()
    .Where(x =>
        x.CompanyId == _current.CompanyId
        && x.LoteAveEngordeId == loteId
        && x.FechaOperacion == day
        ...);

// Aplicar filtro de rango de fechas (ciclo de vida del lote)
if (fechaMinSeg.HasValue)
    query = query.Where(x => x.FechaOperacion >= fechaMinSeg.Value.Date);
if (fechaMaxSeg.HasValue)
    query = query.Where(x => x.FechaOperacion <= fechaMaxSeg.Value.Date.AddDays(1).AddTicks(-1));
```

**Impacto:**
- ✅ Se usa el método auxiliar `CalcularRangoFechasLoteAsync()` (ya optimizado en commits anteriores)
- ✅ Metadata solo se llena con movimientos dentro del ciclo de vida del lote
- ✅ Evita duplicación de datos de lotes anteriores en el mismo galpón
- ✅ Consistencia: mismo filtro aplicado en 3 métodos
  - `QueryHistoricoUnificadoDtosAsync()` — consulta de histórico principal
  - `ValidarCuadrarSaldosAsync()` — validación de ingresos/traslados
  - `BuildStockMetadataPatchAsync()` — llenado de metadata en seguimientos

### Frontend: Comportamiento Esperado

El frontend agrupa movimientos históricos por "fecha efectiva" usando `ymdHistoricoEfectivo()`:

```typescript
private ymdHistoricoEfectivo(h: LoteRegistroHistoricoUnificadoDto): string | null {
  const ref = `${h.referencia ?? ''} ${h.numeroDocumento ?? ''}`.trim();
  
  // Para INV_CONSUMO: extrae fecha de la referencia "Seguimiento aves engorde #XXXX YYYY-MM-DD"
  if (h.tipoEvento === 'INV_CONSUMO') {
    const mAny = ref.match(/(\d{4}-\d{2}-\d{2})/);
    if (mAny) return mAny[1];
  }
  
  // Fallback: usa fechaOperacion
  return this.toYMD(h.fechaOperacion);
}
```

Esto garantiza que consumos registrados en un seguimiento anterior se agrupen con la fecha correcta.

## Archivos Modificados

- `backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoAvesEngordeService.cs`
  - Línea 1039-1081: `BuildStockMetadataPatchAsync()` (aplicado rango de fechas)
  - Reutiliza método auxiliar `CalcularRangoFechasLoteAsync()` (línea 334)

## Pruebas Recomendadas

1. **Crear 2 lotes en el mismo galpón, diferentes fechas:**
   - Lote A: 1-10 de marzo
   - Lote B: 15-25 de marzo
   - Asegurar que ingresos del 8 de marzo solo aparecen en Lote A

2. **Validar que Cuadrar Saldos no duplica información:**
   - Cargar Excel con movimientos
   - Verificar que ambos lotes ven solo sus propios datos en la tabla

3. **Test de edición de seguimientos:**
   - Editar seguimiento de Lote B
   - Verificar que metadata no incluye movimientos de Lote A

## Compilación

✅ Build exitoso (0 errores, 5 warnings previos sin cambios)

## Próximos Pasos (Opcional)

Si se sigue viendo duplicación después de aplicar este fix:
1. Limpiar datos duplicados en la tabla `lote_registro_historico_unificado`
2. Ejecutar `dotnet ef database update` para aplicar cambios
3. Recargar datos desde frontend

Los cambios serán aplicados automáticamente en el siguiente `dotnet run` o al hacer deploy a ECS.
