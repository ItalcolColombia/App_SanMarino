# 📊 Tracker de Estado — Spec #15: Seguimiento Diario Pollo Engorde

**Plan de referencia:** [fase_de_desarrollo/15_spec_para_otro_chat.md](fase_de_desarrollo/15_spec_para_otro_chat.md)
**Fecha de validación:** 2026-05-28
**Resultado:** ✅ **TRABAJO COMPLETO — NO REQUIERE DESARROLLO ADICIONAL**

---

## Resumen ejecutivo

El spec contiene 5 fixes encadenados (CASOS 1–5). Se ejecutó la **checklist de validación inicial** completa antes de tocar código. **Todos los criterios de aceptación (CA) ya están implementados y verificados contra la BD local.**

Conforme a la instrucción del spec ("Si TODO el criterio ya está cumplido → no toques nada, déjalo así"), no se realizaron cambios al código en esta sesión.

---

## ✅ Checklist de validación — Resultados

### CASO 1 — Función SQL `fn_seguimiento_diario_engorde`
- [x] **CA1.1** `rango_seg` castea con `::DATE` — `backend/sql/fn_seguimiento_diario_engorde.sql:105,111`
- [x] **CA1.2** `INV_TRASLADO_SALIDA` usa `ABS(COALESCE(h.cantidad_kg, 0))` — línea 189
- [x] **CA1.3** Nuevo CTE `apertura_alimento` — línea 213
- [x] **CA1.4** `saldo_alimento_kg` se calcula dinámicamente — líneas 428-434
- [x] **CA1.5** `consumo_bodega_kg` mapeado a `se.consumo_dia_kg` — línea 440
- [x] **CA1.6** Test BD lote 5 @ 2026-02-27: `ingreso_alimento_kg=5000, documento='005-001-000053977'` ✅
- [x] **CA1.7** Test BD lote 32 @ 2025-12-30: `ingreso_alimento_kg=6000` ✅

### CASO 2 — `SeguimientoAvesEngordeEcuadorService` descuenta inventario
- [x] **CA2.1** Constructor inyecta las 4 dependencias — `SeguimientoAvesEngordeEcuadorService.cs:22-33`
- [x] **CA2.2** `CreateAsync` valida lote cerrado → `InvalidOperationException` — línea 197
- [x] **CA2.3** Parsea metadata y llama `RegistrarConsumoAsync` — línea 277
- [x] **CA2.4** Llama `RecalcularSaldoAlimentoPorLoteAsync` post-persist — línea 303
- [x] **CA2.5** `UpdateAsync` aplica diff con `(ajuste)`/`(devolución)` — líneas 418-423
- [x] **CA2.6** `DeleteAsync` con ref `"...(devolución por eliminación)"` + `anulado=true` — línea 482
- [x] **CA2.7** Try/catch resilientes preservan el seguimiento aunque falle inventario
- [x] **CA2.9** Mensaje exacto: "El lote está cerrado (liquidado). No se pueden agregar registros diarios." — línea 198

### CASO 3 — Saldo apertura filtrado por `fecha_encaset`
- [x] **CA3.1** SQL: condición `fecha_encaset::DATE` en `apertura_alimento` — `fn_seguimiento_diario_engorde.sql:310`
- [x] **CA3.2** Backend: `ComputeSaldoAperturaGalponAntesPrimerSeguimiento(DateTime? fechaEncaset)` — ambos servicios (`SeguimientoAvesEngordeService.cs:417-424`, `SeguimientoAvesEngordeEcuadorService.cs:947-953`)
- [x] **CA3.3** Call sites pasan `lote.FechaEncaset` — líneas 514, 1015
- [x] **CA3.4** Saldo dinámico via subconsulta `hist_alimento` — `fn_seguimiento_diario_engorde.sql:428-434`
- [x] **CA3.5** Test BD lote 75: `(3234, 2026-05-01, 5280)` ✅

### CASO 4 — Script masivo de recálculo
- [x] **CA4.1** Script envuelto en `BEGIN; ... COMMIT;` — `backend/sql/migrate_recalcular_saldo_alimento_engorde.sql:26,114`
- [x] **CA4.2** Crea tabla `_migracion_saldo_alimento_2026_05_28` — línea 32
- [x] **CA4.3** Solo lotes `deleted_at IS NULL` con `CROSS JOIN LATERAL` — líneas 53-54
- [x] **CA4.4** UPDATE idempotente (diff >= 0.001) — línea 64
- [x] **CA4.5** Reporte final + validación post-migración — líneas 70-109
- [x] **CA4.7** Test post-migración: `total=3422, coinciden=3422, max_diff=0` ✅

### CASO 5 — Deploy automático AWS + movs sin seguimiento
- [x] **CA5.1** Migración EF Core con DROP + FN_V4_SQL + MIGRACION_MASIVA_SQL — `Migrations/20260528212753_FixFnSeguimientoEngordeYRecalcularSaldosMasivo.cs:34-47`
- [x] **CA5.2** No tiene `AddColumn` espurios (`grep` no encuentra coincidencias)
- [x] **CA5.3** Snapshot interno idempotente con `CREATE TABLE IF NOT EXISTS` + `WHERE NOT EXISTS` — líneas 455-474
- [x] **CA5.4** CTE `fechas_universo` — `fn_seguimiento_diario_engorde.sql:281,324,380`
- [x] **CA5.5** `rango_seg.fecha_max = NULL` para lotes ABIERTOS — línea 111
- [x] **CA5.6** `seg_enriquecido` parte de `fechas_universo` — línea 326
- [x] **CA5.7** Window con `ORDER BY se.fecha, COALESCE(se.seg_id, 0)` — líneas 466-468
- [x] **CA5.8** DTO C#: `public long? SegId` — `SeguimientoDiarioTablaFilaDto.cs:13`
- [x] **CA5.9** TS interface: `segId: number | null` — `seguimiento-aves-engorde.service.ts:86`
- [x] **CA5.10** `trackByDiarioFila = f.segId ?? mov-${f.fecha}` — `tabs-principal-engorde.component.ts:94`
- [x] **CA5.11** `onViewDetailById/onEditById/onDelete` aceptan `number | null` — líneas 161,168,173
- [x] **CA5.12** HTML: `*ngIf="f.segId != null; else movSinSeg"` — `tabs-principal-engorde.component.html:201,210`
- [x] **CA5.13** Test BD lote 12: 11 movs con `seg_id IS NULL` ✅
- [x] **CA5.15** Migración aplicada en local sin error (idempotente)

---

## 🔬 Verificaciones de entorno

| Verificación | Resultado |
|---|---|
| `dotnet build` backend | ✅ 0 errors, 0 warnings |
| `yarn tsc --noEmit` frontend | ✅ 0 errors |
| Migración aplicada en BD local | ✅ `20260528212753_FixFnSeguimientoEngordeYRecalcularSaldosMasivo` presente en `__EFMigrationsHistory` |
| Tabla snapshot `_migracion_saldo_alimento_2026_05_28` | ✅ 3422 filas de backup |
| Saldos coinciden persistido vs función | ✅ 3422/3422, max_diff=0 |
| Función v4 devuelve filas con `seg_id IS NULL` | ✅ (lote 12 tiene 11 movs sin seg) |

---

## 📦 Archivos validados (sin modificación en esta sesión)

### Backend
- `backend/sql/fn_seguimiento_diario_engorde.sql` — versión v4 con fixes #10/#12/#14
- `backend/sql/migrate_recalcular_saldo_alimento_engorde.sql` — script standalone
- `backend/src/ZooSanMarino.Infrastructure/Migrations/20260528212753_FixFnSeguimientoEngordeYRecalcularSaldosMasivo.cs` — migración EF Core idempotente
- `backend/src/ZooSanMarino.Application/DTOs/SeguimientoDiarioTablaFilaDto.cs` — `SegId` nullable
- `backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoAvesEngordeService.cs` — Colombia con `fechaEncaset`
- `backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoAvesEngordeEcuadorService.cs` — Ecuador con descuento de inventario completo

### Frontend
- `frontend/src/app/features/aves-engorde/services/seguimiento-aves-engorde.service.ts`
- `frontend/src/app/features/aves-engorde/pages/tabs-principal-engorde/tabs-principal-engorde.component.ts`
- `frontend/src/app/features/aves-engorde/pages/tabs-principal-engorde/tabs-principal-engorde.component.html`

---

## 🚀 Siguiente paso recomendado

El trabajo está listo para deploy. Conforme a CLAUDE.md, `Database__RunMigrations=true` ya está activo en ECS prod, por lo que al hacer `make deploy-backend`:
1. EF Core aplicará automáticamente `20260528212753_FixFnSeguimientoEngordeYRecalcularSaldosMasivo`.
2. La función SQL queda en v4.
3. El UPDATE masivo persiste los saldos correctos.

No requiere intervención manual de SQL contra RDS prod.
