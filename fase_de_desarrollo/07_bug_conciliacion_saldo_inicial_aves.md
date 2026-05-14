# BUG — Conciliación de Saldo Inicial de Aves (Pollo Engorde)

**Módulo:** Pollo Engorde (`aves-engorde`)  
**Lote de Referencia:** 2602  
**Fecha de Encasetamiento:** 18/03/2026  
**Aves Encasetadas:** 13,550  
**Saldo Visible en Reporte (incorrecto):** ~774 aves en el primer registro  

---

## Descripción del Problema

El componente `tabs-principal-engorde` muestra en el **primer registro** un `saldoAves` de ~774 en lugar de 13,550.  

La lógica de saldo se calcula en `buildDiarioFilas()` como:

```typescript
const inicial = this.avesInicialesLote();   // debería ser 13,550
let acumTodasPerdidas = 0;
// ...para cada registro:
acumTodasPerdidas += perdidasTodasDia;
const saldo = Math.max(0, inicial - acumTodasPerdidas);
```

Las causas probables son:

1. **`avesInicialesLote()` retorna un valor incorrecto** — prioriza `hembrasL + machosL` sobre `avesEncasetadas`. Si esos campos tienen un valor parcial o incorrecto, el saldo inicial sale mal.
2. **Registros de seguimiento fuera del ciclo de vida del lote** — si existen `SeguimientoDiarioAvesEngorde` con fecha anterior al encasetamiento (por `created_at` en lugar de `Fecha`), se acumulan pérdidas antes del primer registro visible.

---

## Entidades y Tablas Involucradas

| Entidad EF Core | Tabla DB | Descripción |
|---|---|---|
| `LoteAveEngorde` | `lote_ave_engorde` | Lote con `aves_encasetadas`, `hembras_l`, `machos_l`, `mixtas` |
| `SeguimientoDiarioAvesEngorde` | `seguimiento_diario_aves_engorde` | Registros diarios: mortalidad, sel_h/m, error_sexaje, `Fecha` |
| `LoteRegistroHistoricoUnificados` | `lote_registro_historico_unificado` | Ventas (`VENTA_AVES`), ingresos, traslados de alimento |
| `HistorialLotePolloEngorde` | `historial_lote_pollo_engorde` | Registro de `Inicio` con `aves_hembras`, `aves_machos`, `aves_mixtas` |

---

## Plan de Auditoría

| ID | Actividad | Estado | Descripción |
|----|-----------|--------|-------------|
| BUG-01 | Verificar valor de `aves_encasetadas` en lote 2602 | ⏳ Pendiente | Confirmar que `lote_ave_engorde.aves_encasetadas = 13550` y que `hembras_l` / `machos_l` / `mixtas` son coherentes |
| BUG-02 | Corregir prioridad en `avesInicialesLote()` (frontend) | ⏳ Pendiente | Para lotes mixtos de engorde, `avesEncasetadas` debe tener prioridad sobre `hembrasL + machosL` |
| BUG-03 | Auditar registros de seguimiento fuera del ciclo | ⏳ Pendiente | Verificar si existen `SeguimientoDiarioAvesEngorde` con `Fecha < FechaEncaset` para el lote 2602 |
| BUG-04 | Confirmar que `HistorialLotePolloEngorde` (Inicio) tiene valores correctos | ⏳ Pendiente | El registro `TipoRegistro = 'Inicio'` debe sumar `aves_hembras + aves_machos + aves_mixtas = 13550` |

---

## Archivos Clave

### Frontend
- [tabs-principal-engorde.component.ts](../frontend/src/app/features/aves-engorde/pages/tabs-principal-engorde/tabs-principal-engorde.component.ts) — `buildDiarioFilas()` L190 · `avesInicialesLote()` L554
- [indicadores-diarios-engorde-compute.service.ts](../frontend/src/app/features/aves-engorde/services/indicadores-diarios-engorde-compute.service.ts) — `compute()` L33 · `avesInicialesLote()` L324 (prioridad invertida vs. el component)
- [tabla-indicadores-diarios-engorde.component.ts](../frontend/src/app/features/aves-engorde/pages/tabla-indicadores-diarios-engorde/tabla-indicadores-diarios-engorde.component.ts) — tabla diaria del reporte

### Backend
- [SeguimientoAvesEngordeService.cs](../backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoAvesEngordeService.cs) — `GetByLoteAsync()` L134 · `GetLiquidacionResumenAsync()` L170
- [LoteAveEngorde.cs](../backend/src/ZooSanMarino.Domain/Entities/LoteAveEngorde.cs) — entidad principal del lote
- [SeguimientoDiarioAvesEngorde.cs](../backend/src/ZooSanMarino.Domain/Entities/SeguimientoDiarioAvesEngorde.cs) — registros diarios

---

## Consulta de Diagnóstico

```sql
-- 1. Verificar valores del lote 2602
SELECT lote_ave_engorde_id, lote_nombre, aves_encasetadas, hembras_l, machos_l, mixtas, fecha_encaset
FROM lote_ave_engorde
WHERE lote_ave_engorde_id = 2602;

-- 2. Ver registro de Inicio en historial
SELECT tipo_registro, aves_hembras, aves_machos, aves_mixtas, created_at
FROM historial_lote_pollo_engorde
WHERE lote_ave_engorde_id = 2602 AND tipo_registro = 'Inicio';

-- 3. Buscar seguimientos FUERA del ciclo (antes del encasetamiento)
SELECT id, fecha, mortalidad_hembras, mortalidad_machos, sel_h, sel_m, created_at
FROM seguimiento_diario_aves_engorde
WHERE lote_ave_engorde_id = 2602
  AND fecha < '2026-03-18'
ORDER BY fecha;

-- 4. Ver todas las ventas del lote
SELECT tipo_evento, fecha_operacion, cantidad_mixtas, cantidad_hembras, cantidad_machos, anulado
FROM lote_registro_historico_unificado
WHERE lote_ave_engorde_id = 2602
  AND tipo_evento = 'VENTA_AVES'
ORDER BY fecha_operacion;
```

---

## Fix Propuesto (BUG-02 — prioridad en avesInicialesLote)

**Archivo:** `tabs-principal-engorde.component.ts` L554

```typescript
// ANTES — prioriza hembrasL+machosL, falla para lotes mixtos
private avesInicialesLote(): number {
    const h = Number(l['hembrasL'] ?? 0);
    const m = Number(l['machosL'] ?? 0);
    if (h + m > 0) return Math.round(h + m);   // BUG: bypasses avesEncasetadas
    const av = l['avesEncasetadas'];
    ...
}

// DESPUÉS — avesEncasetadas tiene prioridad (es el campo canónico para engorde)
private avesInicialesLote(): number {
    const av = Number((l as any)['avesEncasetadas'] ?? 0);
    if (av > 0) return av;
    const h = Number((l as any)['hembrasL'] ?? 0);
    const m = Number((l as any)['machosL'] ?? 0);
    const x = Number((l as any)['mixtas'] ?? 0);
    return h + m + x;
}
```

---

## Criterios de Aceptación

- El primer registro del reporte para lote 2602 muestra `saldoAves` = 13,550 (o el valor de `aves_encasetadas`).
- El saldo se calcula siempre como: `aves_encasetadas - Σ(mortalidad + selección + error_sexaje)` desde el día 1.
- Las ventas (`VENTA_AVES`) se muestran en la columna de despacho pero **no** reducen el saldo del seguimiento.
- No hay registros de seguimiento con `Fecha < FechaEncaset` que inflen las pérdidas acumuladas.
