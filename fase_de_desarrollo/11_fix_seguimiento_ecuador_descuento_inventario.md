# Plan de Desarrollo — Fix: `SeguimientoAvesEngordeEcuadorService` descontar inventario y afectar saldos

**ID:** 11
**Feature:** Habilitar descuento de alimento y afectación de inventario al crear/editar/eliminar seguimientos desde el endpoint Ecuador.
**Estado:** En implementación
**Fecha:** 2026-05-28
**Plan padre:** [10_fix_fn_seguimiento_diario_engorde_saldos.md](./10_fix_fn_seguimiento_diario_engorde_saldos.md)
**Endpoint disparador:** `POST /api/SeguimientoAvesEngordeEcuador` (Status 201 Created)

---

## Contexto y Problema

Al validar la función SQL `fn_seguimiento_diario_engorde` (fix #10) se descubrió que `SeguimientoAvesEngordeEcuadorService` está incompleto: solo persiste la entidad `SeguimientoDiarioAvesEngorde` pero **no aplica ninguna afectación lateral** que sí tiene el servicio original `SeguimientoAvesEngordeService`. Resultado: cuando un usuario Ecuador (token `x-active-pais: 2`, company `ItalcolEcuador`) crea un seguimiento diario:

| Efecto esperado | Estado actual |
|---|---|
| Descontar alimento del stock (`inventario_gestion_stock`) | ❌ NO se hace |
| Registrar `INV_CONSUMO` en `lote_registro_historico_unificado` | ❌ NO se hace |
| Registrar retiro de aves (mortalidad/selección/error sexaje) | ❌ NO se hace |
| Recalcular `saldo_alimento_kg` en la fila del seguimiento | ❌ NO se hace |
| Validar que el lote existe y no está cerrado | ❌ NO se hace |
| Construir `historico_consumo_alimento` (snapshot por ítem) | ❌ NO se hace |
| Patch de metadata con ingreso/traslado/documento/despacho del día | ❌ NO se hace |
| Calcular kcal/prot derivados del consumo | ❌ NO se hace |
| Inferir consumo desde gramaje cuando viene 0 | ❌ NO se hace |

### Tabla comparativa código actual vs servicio original

| Funcionalidad | `SeguimientoAvesEngordeService` (Colombia) | `SeguimientoAvesEngordeEcuadorService` (actual) |
|---|---|---|
| **Validar lote existe + no cerrado** | ✅ `lote.EstadoOperativoLote == "Cerrado"` lanza `InvalidOperationException` | ❌ Persiste sin validar |
| **Inferir kcal/prot desde `IAlimentoNutricionProvider`** | ✅ | ❌ |
| **Calcular consumo desde gramaje cuando llega 0** | ✅ usa `IGramajeProvider` + `CalcularHembrasVivasAsync` | ❌ |
| **Calcular kcal/prot ave-día** | ✅ `CalcularDerivados` | ❌ |
| **Snapshot `historico_consumo_alimento`** | ✅ `BuildHistoricoConsumoAlimentoAsync` | ❌ |
| **Patch metadata stock** | ✅ `BuildStockMetadataPatchAsync` + `MergeMetadataWithPatch` | ❌ |
| **🔴 Descontar inventario (RegistrarConsumoAsync)** | ✅ recorre `Metadata.itemsHembras`/`itemsMachos`, llama `IInventarioGestionService.RegistrarConsumoAsync` (genera `INV_CONSUMO`) | ❌ |
| **🔴 Retiro de aves (mortalidad+sel+err sexaje)** | ✅ `_movimientoAvesService.RegistrarRetiroDesdeSeguimientoAsync` | ❌ |
| **🔴 Recalcular saldo alimento del lote** | ✅ `RecalcularSaldoAlimentoPorLoteAsync` | ❌ |
| (Update) Comparar metadata vieja vs nueva, ajustar inventario | ✅ | ❌ |
| (Update) Comparar retiros viejos vs nuevos, ajustar movimientos aves | ✅ | ❌ |
| (Delete) Devolver alimento al inventario | ✅ `RegistrarIngresoAsync` con ref `"devolución por eliminación"` | ❌ (solo borra fila) |
| (Delete) Anular `INV_CONSUMO` huérfanos del histórico | ✅ marca `anulado = true` | ❌ |
| (Delete) Devolver aves al inventario | ✅ `DevolverAvesAlInventarioAsync` | ❌ |

## Decisión de diseño

**No tocar el servicio original** (`SeguimientoAvesEngordeService`) — está estable y lo usan otros flujos. **Portar la lógica lateral** al servicio Ecuador, reutilizando las mismas dependencias y patrones, con dos optimizaciones:

1. Extraer los helpers estáticos puros a una clase compartida `SeguimientoAvesEngordeInventarioHelpers` (en `Infrastructure/Services/Internal/`).
2. Los helpers que dependen de `_ctx` (`BuildStockMetadataPatchAsync`, `BuildHistoricoConsumoAlimentoAsync`, `RecalcularSaldoAlimentoPorLoteAsync`, etc.) se duplican en el servicio Ecuador **por ahora**, con un TODO para futura refactorización a una clase base abstracta — ese refactor es ortogonal y de alto riesgo para el servicio Colombia.

> **¿Por qué no clase base abstracta ahora?** El servicio original tiene 1869 líneas y muchos métodos no relacionados (Filter, Resultado, Backfill, BulkUpdate, etc.). Extraerlo invasivamente arriesga regresiones en Colombia. La duplicación quirúrgica de ~250 líneas en Ecuador es menor riesgo y entrega valor inmediato.

## Diseño de la implementación

### Dependencias a inyectar en `SeguimientoAvesEngordeEcuadorService`

```csharp
public SeguimientoAvesEngordeEcuadorService(
    ZooSanMarinoContext ctx,
    ICurrentUser current,
    IAlimentoNutricionProvider alimentos,
    IGramajeProvider gramaje,
    IMovimientoAvesService movimientoAvesService,
    IInventarioGestionService? inventarioGestionService = null)
```

Todas estas dependencias ya están registradas en `Program.cs:218, 243, 299, 300`. No requiere cambios de DI más allá de actualizar la firma del constructor.

### Nuevos métodos privados en el servicio Ecuador

Portados desde `SeguimientoAvesEngordeService.cs`:

| Método | Tipo | Origen (líneas) |
|---|---|---|
| `RecalcularSaldoAlimentoPorLoteAsync` | async, depende de `_ctx` | 455-557 |
| `ComputeSaldoAperturaGalponAntesPrimerSeguimiento` | static puro | 418-446 |
| `TryGetHistDeltaAndOrd` | static puro | 385-411 |
| `YmdHistoricoEfectivo`, `TsHistorico`, `TsSeguimiento`, `FormatYmd` | static puros | ~340-376 |
| `SaldoAlimentoEvent` (record struct) | tipo | 448 |
| `BuildHistoricoConsumoAlimentoAsync` | async, depende de `_ctx` | 614-665 |
| `BuildStockMetadataPatchAsync` | async, depende de `_ctx` | 1041-1131 |
| `MergeMetadataWithPatch` | static puro | 1133-1156 |
| `ParseMetadataItemsToKg`, `ToKg`, `FormatKg` | static puros | 1158-1195 |
| `CalcularHembrasVivasAsync` | async, depende de `_ctx` | 1197-1220 |
| `CalcularDerivados`, `CalcularSemana` | static puros | 1222-1232 |
| `DevolverAvesAlInventarioAsync` | async, depende de `_ctx` | 1007-1023 |
| `CloneJsonDocument` | static puro | 1026-1030 |

### Reescritura de `CreateAsync` (Ecuador)

Patrón espejo de `SeguimientoAvesEngordeService.cs:667-784`:

1. Validar lote existe (CompanyId, !DeletedAt, !Cerrado).
2. Inferir kcal/prot si vienen null.
3. Calcular consumo desde gramaje si `consumoKgH <= 0` y hay `FechaEncaset` + galpón.
4. Calcular derivados (kcalAveH, protAveH).
5. Build `stockPatch` y `metadataForEntity = MergeMetadataWithPatch(dto.Metadata, stockPatch)`.
6. Build `historicoConsumo = BuildHistoricoConsumoAlimentoAsync(...)`.
7. Crear entidad con todos los campos; persistir.
8. **Si `_inventarioGestionService != null && dto.Metadata != null`**: parsear items, llamar `RegistrarConsumoAsync` por cada ítem (esto inserta `INV_CONSUMO` con `origen_tabla = inventario_gestion_movimiento` y `referencia = "Seguimiento aves engorde #{id} {fecha}"`).
9. **Si hay retiros**: `_movimientoAvesService.RegistrarRetiroDesdeSeguimientoAsync(loteId, ..., "Engorde", ...)`.
10. **`RecalcularSaldoAlimentoPorLoteAsync(loteId, companyId)`** — actualiza `saldo_alimento_kg` en TODOS los seguimientos del lote.
11. Reload entidad y devolver DTO.

### Reescritura de `UpdateAsync` (Ecuador)

Patrón espejo de `SeguimientoAvesEngordeService.cs:786-938`:

1. Validar lote existe + no cerrado.
2. Cargar entidad existente (con join a lote por companyId).
3. Inferir kcal/prot + consumo desde gramaje (igual que Create).
4. Capturar `oldByItemId = ParseMetadataItemsToKg(ent.Metadata.RootElement)` y `oldHRet`, `oldMRet`.
5. Reconstruir `historicoConsumoUpdate` con `oldByItemId`.
6. Aplicar `stockPatch` + `metadataForSave`.
7. Actualizar entidad, forzar `EntityState.Modified` en jsonb (parche EF).
8. Persistir cambios.
9. **Diff inventario**: para cada ítem, calcular `diff = newQty - oldQty`. Si `diff > 0` → consumo adicional; si `diff < 0` → devolución (`RegistrarIngresoAsync` con ref `"(devolución)"`).
10. **Diff retiros**: si crecieron → registrar retiro adicional; si decrecieron → `DevolverAvesAlInventarioAsync`.
11. **`RecalcularSaldoAlimentoPorLoteAsync`**.

### Reescritura de `DeleteAsync` (Ecuador)

Patrón espejo de `SeguimientoAvesEngordeService.cs:940-1005`:

1. Cargar entidad con join a lote, validar `!Cerrado`.
2. **Devolver alimento al inventario** por cada ítem en metadata: `RegistrarIngresoAsync` con `ref = "Seguimiento aves engorde #{id} (devolución por eliminación)"`.
3. **Anular `INV_CONSUMO` huérfanos** en `lote_registro_historico_unificado` (los que comienzan con `"Seguimiento aves engorde #{id}"`).
4. **Devolver aves** al inventario (`DevolverAvesAlInventarioAsync`).
5. Eliminar entidad.
6. **`RecalcularSaldoAlimentoPorLoteAsync`** del lote.

### Comportamiento de coherencia con la función SQL (fix #10)

Tras el fix #10, `fn_seguimiento_diario_engorde` calcula el saldo dinámicamente al vuelo (no depende del valor persistido). Pero `RecalcularSaldoAlimentoPorLoteAsync` sigue siendo necesario porque:

1. Otros consumidores leen directamente `seguimiento_diario_aves_engorde.saldo_alimento_kg` (DTO `MapToDto`, `GetLiquidacionResumenAsync`, etc.).
2. La consistencia entre la fila persistida y el cálculo dinámico simplifica auditoría.

---

## Archivos a modificar/crear

| Archivo | Acción | Resumen |
|---------|--------|---------|
| `backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoAvesEngordeEcuadorService.cs` | **Modificar** | Reescritura completa de Create/Update/Delete con afectación lateral. Inyectar 4 dependencias nuevas. Agregar helpers privados portados del servicio original. |
| `backend/src/ZooSanMarino.API/Program.cs` | _(sin cambios)_ | Las dependencias inyectadas ya están registradas |

---

## Plan de Validación

### V-1 — Build sin errores
`dotnet build` en `backend/` debe terminar con 0 errores y los mismos warnings que antes.

### V-2 — Crear seguimiento via endpoint, verificar afectación
Usar el endpoint del request adjunto (`POST /api/SeguimientoAvesEngordeEcuador`) con un payload de prueba:

```json
{
  "loteId": 5,
  "fechaRegistro": "2026-05-29T12:00:00",
  "mortalidadHembras": 1,
  "mortalidadMachos": 0,
  "selH": 0, "selM": 0,
  "errorSexajeHembras": 0, "errorSexajeMachos": 0,
  "tipoAlimento": "INI",
  "itemsHembras": [
    { "tipoItem": "alimento", "catalogItemId": 1, "itemInventarioEcuadorId": 12, "cantidad": 100, "unidad": "kg" }
  ],
  "consumoKgHembras": 100
}
```

Verificaciones contra BD:
1. **Fila creada en `seguimiento_diario_aves_engorde`** con `consumo_kg_hembras = 100`, `saldo_alimento_kg` populado.
2. **Fila en `lote_registro_historico_unificado`** con `tipo_evento = INV_CONSUMO`, `cantidad_kg = 100`, `referencia = "Seguimiento aves engorde #{id} 2026-05-29"`, `origen_tabla = inventario_gestion_movimiento`.
3. **Stock reducido en `inventario_gestion_stock`** para `(farm_id, nucleo_id, galpon_id, item_inventario_ecuador_id=12)` en 100 kg.
4. **Movimiento de aves** en `movimientos_aves` con 1 hembra retirada por seguimiento.
5. **Saldo recalculado**: `SELECT saldo_alimento_kg FROM seguimiento_diario_aves_engorde WHERE id = {nuevoId}` y `SELECT saldo_alimento_kg FROM fn_seguimiento_diario_engorde(5) WHERE seg_id = {nuevoId}` deben coincidir.

### V-3 — Lote cerrado → 400 Bad Request
Probar `POST` con `loteId = 32` (cerrado). Esperado: `400 BadRequest` con mensaje "El lote está cerrado (liquidado)…".

### V-4 — Lote inexistente → 400 Bad Request
Probar con `loteId = 999999`. Esperado: `400 BadRequest` con mensaje "Lote aves de engorde '999999' no existe…".

### V-5 — Eliminar seguimiento → INV_CONSUMO anulado + devolución
Eliminar el seguimiento creado en V-2:
1. Verificar `anulado = true` en la fila `INV_CONSUMO` correspondiente.
2. Verificar nueva fila `INV_INGRESO` con `referencia = "...(devolución por eliminación)"`.
3. Verificar stock restituido en `inventario_gestion_stock`.
4. Verificar aves devueltas (hembras + 1).
5. Verificar saldo recalculado en seguimientos restantes.

### V-6 — UpdateAsync: ajuste del consumo
Editar el seguimiento creado en V-2 cambiando `cantidad = 100` → `cantidad = 150`:
1. Verificar nuevo `INV_CONSUMO` con `cantidad_kg = 50` (diff), `referencia = "...(ajuste)"`.
2. Verificar stock reducido 50 kg adicionales.

---

## Notas para futuro (no incluido en este fix)

- **Refactor a clase base** `SeguimientoAvesEngordeServiceBase` (abstract) con la lógica compartida — eliminar duplicación con servicio Colombia. Riesgo alto, mejor ticket separado.
- **Eventual sourcing**: considerar agente de eventos para que el seguimiento publique `SeguimientoCreado` y consumidores (inventario, aves) reaccionen — desacoplaría el servicio.
- **Idempotencia**: validar que un retry del POST no genere consumos duplicados (actualmente la unique constraint `uq_seg_diario_aves_engorde_lote_fecha` evita la fila duplicada, pero `INV_CONSUMO` sin esa restricción podría duplicarse si el cliente reintentara antes del SaveChanges).
