# Plan de Desarrollo — Función DB: Tabla Diaria Seguimiento Pollo Engorde

**ID:** 09  
**Feature:** `fn_seguimiento_diario_engorde(p_lote_id INT)` — función PostgreSQL que devuelve todas las filas pre-calculadas del seguimiento diario de un lote, eliminando cálculos del frontend y del servicio .NET  
**Estado:** Pendiente de implementación  
**Fecha:** 2026-05-20

---

## Contexto y Problema

### Situación actual

El módulo `aves-engorde` (`tabs-principal-engorde.component.ts`) recibe dos conjuntos de datos del backend:

1. `seguimientos[]` — registros de `seguimiento_diario_aves_engorde`
2. `historicoUnificado[]` — filas de `lote_registro_historico_unificado`

Y realiza **todos los cálculos en el frontend** (`buildDiarioFilas()`):

| Cálculo | Complejidad | Ubicación actual |
|---------|-------------|-----------------|
| `edadDia`, `semana` | simple | Frontend |
| `totalMortSelDia`, `consumoDiaKg` | simple | Frontend |
| `acumConsumoKg` | acumulado corriente | Frontend |
| `saldoAves` | acumulado corriente con piso-0 | Frontend |
| `pctPerdidasDia` | derivado de saldo | Frontend |
| `ingresoAlimento`, `traslado`, `documento` | JOIN con historico por fecha | Frontend |
| `despachoH/M/X` | JOIN con historico (VENTA_AVES) | Frontend |
| `consumoBodegaKg` | JOIN con historico (INV_CONSUMO) | Frontend |
| `saldoAlimentoKg` | balance corriente con piso-0 por evento | Ya en BD* |
| `avesInicialesLote()` | lógica lote abierto/cerrado | Frontend |

*`saldo_alimento_kg` **ya se calcula y persiste en BD** por `RecalcularSaldoAlimentoPorLoteAsync()` del servicio original `SeguimientoAvesEngordeService`. Se llama en cada `GetByLoteAsync`. La columna `seguimiento_diario_aves_engorde.saldo_alimento_kg` tiene el valor correcto al momento del GET.

### Objetivo

Crear una función PostgreSQL `fn_seguimiento_diario_engorde(p_lote_id INT)` que retorne una tabla con todas las filas ya calculadas. El backend expone un endpoint nuevo que la llama directamente. El frontend solo renderiza — sin `buildDiarioFilas()`, sin `aggregateHistoricoPorFecha()`, sin `computeSaldoAlimento*()`.

### Restricción importante — Ecuador vs. servicio original

El servicio Ecuador (`SeguimientoAvesEngordeEcuadorService`) actualmente **NO llama** `RecalcularSaldoAlimentoPorLoteAsync`. Debe agregarse antes de retornar datos, igual que el servicio original. Esto asegura que `saldo_alimento_kg` esté actualizado en la BD antes de que la función lo lea.

## ⚠️ CORRECCIÓN CRÍTICA — Tabla correcta (validado 2026-05-20)

La migración `20260517104629_SplitSeguimientoDiarioAvesEngordeByCountry` **ya fue creada y debe ser aplicada**. Esta migración:
- **Crea** `seguimiento_diario_aves_engorde_ecuador` con todos los datos del Ecuador
- **Crea** `seguimiento_diario_aves_engorde_panama`
- **Elimina** la tabla compartida original `seguimiento_diario_aves_engorde`

**Consecuencias para esta implementación:**

| Elemento | ❌ Plan original (incorrecto) | ✅ Plan corregido |
|---------|-------------------------------|-----------------|
| Tabla en la función SQL | `seguimiento_diario_aves_engorde` | `seguimiento_diario_aves_engorde_ecuador` |
| DbSet del servicio | `_ctx.SeguimientoDiarioAvesEngorde` | `_ctx.SeguimientoDiarioAvesEngordeEcuador` |
| Tipo de entidad en MapToDto | `SeguimientoDiarioAvesEngorde` | `SeguimientoDiarioAvesEngordeEcuador` |
| Migración EF Core | No contemplada | Requerida (crea la función PostgreSQL) |

El `SeguimientoDiarioAvesEngordeEcuador` ya existe en Domain y en el DbContext como `_ctx.SeguimientoDiarioAvesEngordeEcuador`.

---

## Arquitectura del Cambio

```
[PostgreSQL]
  fn_seguimiento_diario_engorde(p_lote_id INT)
    ├── CTE lote_info         → granja_id, nucleo_id, galpon_id, fecha_encaset, aves_encasetadas, estado
    ├── CTE aves_iniciales    → lógica lote abierto/cerrado
    ├── CTE hist_por_fecha    → agregados del historico por fecha (ingresos, traslados, ventas, consumo bodega)
    ├── CTE seg_base          → seguimiento_diario + edad_dia + semana + cálculos simples
    └── Query final           → JOIN seg_base + hist_por_fecha + window functions (acumConsumoKg, saldoAves)

[.NET Backend — SeguimientoAvesEngordeEcuadorService]
  GetTablaDiariaAsync(int loteId)
    ├── RecalcularSaldoAlimentoPorLoteAsync()  ← igual que servicio original
    └── ctx.Database.SqlQueryRaw("SELECT * FROM fn_seguimiento_diario_engorde(@p)", param)

[.NET Backend — SeguimientoAvesEngordeEcuadorController]
  GET /api/SeguimientoAvesEngordeEcuador/por-lote/{loteId}/tabla-diaria
    → IReadOnlyList<SeguimientoDiarioTablaFilaDto>

[Angular — SeguimientoAvesEngordeService]
  getTablaDiaria(loteId): Observable<SeguimientoDiarioTablaFilaDto[]>

[Angular — SeguimientoAvesEngordeListComponent]
  onLoteChange() → llama getTablaDiaria() y asigna tablaFilas[]

[Angular — TabsPrincipalEngordeComponent]
  @Input() tablaFilas: SeguimientoDiarioTablaFilaDto[]
  → usa tablaFilas directamente en template (elimina buildDiarioFilas())
```

---

## Diseño de la Función PostgreSQL

### Columnas de salida

```sql
CREATE OR REPLACE FUNCTION fn_seguimiento_diario_engorde(p_lote_id INT)
RETURNS TABLE (
    -- Identificación
    seg_id                    BIGINT,
    fecha                     DATE,
    -- Tiempo
    edad_dia                  INT,
    semana                    SMALLINT,
    -- Seguimiento crudo
    mortalidad_hembras        INT,
    mortalidad_machos         INT,
    sel_h                     INT,
    sel_m                     INT,
    error_sexaje_hembras      INT,
    error_sexaje_machos       INT,
    -- Calculados simples
    total_mort_sel_dia        INT,
    perdidas_totales_dia      INT,
    consumo_kg_hembras        NUMERIC,
    consumo_kg_machos         NUMERIC,
    consumo_dia_kg            NUMERIC,
    -- Acumulados corrientes (window functions)
    acum_consumo_kg           NUMERIC,
    saldo_aves                INT,
    pct_perdidas_dia          NUMERIC,
    -- Saldo alimento (pre-calculado en columna BD)
    saldo_alimento_kg         NUMERIC,
    -- Histórico agregado por fecha
    ingreso_alimento_kg       NUMERIC,
    traslado_entrada_kg       NUMERIC,
    traslado_salida_kg        NUMERIC,
    consumo_bodega_kg         NUMERIC,
    documento                 TEXT,
    despacho_hembras          INT,
    despacho_machos           INT,
    despacho_mixtas           INT,
    -- Mediciones / otros campos del seguimiento
    tipo_alimento             TEXT,
    peso_prom_hembras         NUMERIC,
    peso_prom_machos          NUMERIC,
    uniformidad_hembras       NUMERIC,
    uniformidad_machos        NUMERIC,
    cv_hembras                NUMERIC,
    cv_machos                 NUMERIC,
    consumo_agua_diario       NUMERIC,
    consumo_agua_ph           NUMERIC,
    consumo_agua_orp          NUMERIC,
    consumo_agua_temperatura  NUMERIC,
    observaciones             TEXT,
    ciclo                     TEXT,
    metadata                  JSONB,
    items_adicionales         JSONB,
    historico_consumo_alimento JSONB,
    created_by_user_id        TEXT
) LANGUAGE sql STABLE AS $$
```

### Estructura interna (CTEs)

```sql
WITH
-- 1. Info del lote
lote_info AS (
    SELECT
        l.granja_id,
        COALESCE(l.nucleo_id, '') AS nucleo_id,
        COALESCE(l.galpon_id, '') AS galpon_id,
        l.fecha_encaset,
        COALESCE(l.aves_encasetadas, 0) AS aves_encasetadas,
        COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0) + COALESCE(l.mixtas, 0) AS suma_hm,
        l.estado_operativo_lote
    FROM lote_ave_engorde l
    WHERE l.lote_ave_engorde_id = p_lote_id
      AND l.deleted_at IS NULL
),

-- 2. Rango de fechas del lote (para aislar historico del ciclo actual)
rango_seg AS (
    SELECT
        MIN(s.fecha) AS fecha_min,
        MAX(s.fecha) AS fecha_max
    FROM seguimiento_diario_aves_engorde_ecuador s  -- ⚠️ tabla Ecuador (migración 20260517)
    WHERE s.lote_ave_engorde_id = p_lote_id
),

-- 3. Total salidas (para lotes cerrados: aves_iniciales = suma de salidas)
salidas_totales AS (
    SELECT
        COALESCE(SUM(
            COALESCE(s.mortalidad_hembras,0) + COALESCE(s.mortalidad_machos,0) +
            COALESCE(s.sel_h,0) + COALESCE(s.sel_m,0) +
            COALESCE(s.error_sexaje_hembras,0) + COALESCE(s.error_sexaje_machos,0)
        ), 0) AS bajas_seguimiento
    FROM seguimiento_diario_aves_engorde_ecuador s  -- ⚠️ tabla Ecuador
    WHERE s.lote_ave_engorde_id = p_lote_id
),
ventas_totales AS (
    SELECT COALESCE(SUM(
        COALESCE(h.cantidad_hembras,0) + COALESCE(h.cantidad_machos,0) + COALESCE(h.cantidad_mixtas,0)
    ), 0) AS total_ventas
    FROM lote_registro_historico_unificado h
    WHERE h.lote_ave_engorde_id = p_lote_id
      AND h.tipo_evento = 'VENTA_AVES'
      AND NOT h.anulado
),

-- 4. Aves iniciales (lógica lote abierto/cerrado — espeja avesInicialesLote() del frontend)
aves_iniciales AS (
    SELECT
        CASE
            -- Lote cerrado: saldo final=0 → inicial = suma de todas las salidas
            WHEN LOWER(li.estado_operativo_lote) = 'cerrado' THEN
                GREATEST(1, st.bajas_seguimiento + vt.total_ventas)
            -- Solo aves_encasetadas poblado
            WHEN li.aves_encasetadas > 0 AND li.suma_hm = 0 THEN li.aves_encasetadas
            -- Solo suma h+m poblada
            WHEN li.suma_hm > 0 AND li.aves_encasetadas = 0 THEN li.suma_hm
            -- Ambos iguales
            WHEN li.aves_encasetadas = li.suma_hm THEN li.aves_encasetadas
            -- Difieren y lote abierto → usar aves_encasetadas (campo canónico)
            ELSE li.aves_encasetadas
        END AS inicial
    FROM lote_info li
    CROSS JOIN salidas_totales st
    CROSS JOIN ventas_totales vt
),

-- 5. Ventas VENTA_AVES por fecha (para descontar del saldo aves)
ventas_por_fecha AS (
    SELECT
        DATE(h.fecha_operacion) AS fecha,
        COALESCE(SUM(COALESCE(h.cantidad_hembras,0) + COALESCE(h.cantidad_machos,0) + COALESCE(h.cantidad_mixtas,0)), 0) AS ventas_dia,
        COALESCE(SUM(COALESCE(h.cantidad_hembras,0)), 0) AS despacho_h,
        COALESCE(SUM(COALESCE(h.cantidad_machos,0)), 0)  AS despacho_m,
        COALESCE(SUM(COALESCE(h.cantidad_mixtas,0)), 0)  AS despacho_x
    FROM lote_registro_historico_unificado h
    WHERE h.lote_ave_engorde_id = p_lote_id
      AND h.tipo_evento = 'VENTA_AVES'
      AND NOT h.anulado
    GROUP BY DATE(h.fecha_operacion)
),

-- 6. Movimientos de alimento por fecha (desde el galpón — lote_ave_engorde_id puede ser NULL)
hist_alimento AS (
    SELECT
        DATE(h.fecha_operacion) AS fecha,
        COALESCE(SUM(CASE
            WHEN h.tipo_evento = 'INV_INGRESO'
                AND NOT (h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%')
            THEN COALESCE(h.cantidad_kg, 0) ELSE 0 END), 0) AS ingreso_kg,
        COALESCE(SUM(CASE WHEN h.tipo_evento = 'INV_TRASLADO_ENTRADA' THEN COALESCE(h.cantidad_kg,0) ELSE 0 END), 0) AS traslado_entrada_kg,
        COALESCE(SUM(CASE WHEN h.tipo_evento = 'INV_TRASLADO_SALIDA'  THEN COALESCE(h.cantidad_kg,0) ELSE 0 END), 0) AS traslado_salida_kg,
        COALESCE(SUM(CASE WHEN h.tipo_evento = 'INV_CONSUMO'          THEN COALESCE(h.cantidad_kg,0) ELSE 0 END), 0) AS consumo_bodega_kg,
        STRING_AGG(DISTINCT NULLIF(TRIM(COALESCE(h.numero_documento, h.referencia, '')), ''), ', ') AS documento
    FROM lote_registro_historico_unificado h
    JOIN lote_info li ON TRUE
    JOIN rango_seg rs ON TRUE
    WHERE NOT h.anulado
      AND NOT (h.referencia IS NOT NULL AND (
              h.referencia LIKE '%devolución por eliminación%'
           OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND h.tipo_evento != 'VENTA_AVES'
      AND h.farm_id = li.granja_id
      AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
      AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id
      AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
      AND (rs.fecha_max IS NULL OR DATE(h.fecha_operacion) <= rs.fecha_max)
    GROUP BY DATE(h.fecha_operacion)
),

-- 7. Base seguimiento con cálculos simples
seg_base AS (
    SELECT
        s.id                                              AS seg_id,
        DATE(s.fecha)                                    AS fecha,
        -- Edad y semana
        CASE
            WHEN li.fecha_encaset IS NOT NULL
            THEN GREATEST(0, DATE(s.fecha) - DATE(li.fecha_encaset))
            ELSE 0
        END                                              AS edad_dia,
        COALESCE(s.mortalidad_hembras, 0)               AS mortalidad_hembras,
        COALESCE(s.mortalidad_machos, 0)                AS mortalidad_machos,
        COALESCE(s.sel_h, 0)                             AS sel_h,
        COALESCE(s.sel_m, 0)                             AS sel_m,
        COALESCE(s.error_sexaje_hembras, 0)             AS error_sexaje_hembras,
        COALESCE(s.error_sexaje_machos, 0)              AS error_sexaje_machos,
        COALESCE(s.consumo_kg_hembras, 0)               AS consumo_kg_hembras,
        COALESCE(s.consumo_kg_machos, 0)                AS consumo_kg_machos,
        s.saldo_alimento_kg,
        s.tipo_alimento,
        s.peso_prom_hembras,
        s.peso_prom_machos,
        s.uniformidad_hembras,
        s.uniformidad_machos,
        s.cv_hembras,
        s.cv_machos,
        s.consumo_agua_diario,
        s.consumo_agua_ph,
        s.consumo_agua_orp,
        s.consumo_agua_temperatura,
        s.observaciones,
        s.ciclo,
        s.metadata,
        s.items_adicionales,
        s.historico_consumo_alimento,
        s.created_by_user_id
    FROM seguimiento_diario_aves_engorde_ecuador s  -- ⚠️ tabla Ecuador
    CROSS JOIN lote_info li
    WHERE s.lote_ave_engorde_id = p_lote_id
),

-- 8. Enriquecer con historico y calcular columnas derivadas
seg_enriquecido AS (
    SELECT
        sb.*,
        -- Campos calculados simples
        (sb.mortalidad_hembras + sb.mortalidad_machos + sb.sel_h + sb.sel_m)          AS total_mort_sel_dia,
        (sb.mortalidad_hembras + sb.mortalidad_machos + sb.sel_h + sb.sel_m
            + sb.error_sexaje_hembras + sb.error_sexaje_machos)                        AS perdidas_totales_dia,
        (sb.consumo_kg_hembras + sb.consumo_kg_machos)                                AS consumo_dia_kg,
        -- Semana
        LEAST(8, GREATEST(1,
            CEIL((GREATEST(0, DATE(sb.fecha) - DATE(li.fecha_encaset)) + 1) / 7.0)::SMALLINT
        ))                                                                              AS semana,
        -- Ventas del día (para saldo aves)
        COALESCE(vpf.ventas_dia, 0)   AS ventas_dia,
        COALESCE(vpf.despacho_h, 0)   AS despacho_h,
        COALESCE(vpf.despacho_m, 0)   AS despacho_m,
        COALESCE(vpf.despacho_x, 0)   AS despacho_x,
        -- Historico alimento
        COALESCE(ha.ingreso_kg, 0)           AS ingreso_alimento_kg,
        COALESCE(ha.traslado_entrada_kg, 0)  AS traslado_entrada_kg,
        COALESCE(ha.traslado_salida_kg, 0)   AS traslado_salida_kg,
        COALESCE(ha.consumo_bodega_kg, 0)    AS consumo_bodega_kg,
        ha.documento
    FROM seg_base sb
    CROSS JOIN lote_info li
    LEFT JOIN ventas_por_fecha vpf ON vpf.fecha = sb.fecha
    LEFT JOIN hist_alimento ha     ON ha.fecha  = sb.fecha
)

-- 9. Query final: window functions para acumulados corrientes
SELECT
    se.seg_id,
    se.fecha,
    se.edad_dia,
    se.semana,
    se.mortalidad_hembras,
    se.mortalidad_machos,
    se.sel_h,
    se.sel_m,
    se.error_sexaje_hembras,
    se.error_sexaje_machos,
    se.total_mort_sel_dia,
    se.perdidas_totales_dia,
    se.consumo_kg_hembras,
    se.consumo_kg_machos,
    se.consumo_dia_kg,
    -- Acumulado consumo
    SUM(se.consumo_dia_kg) OVER w_ord                            AS acum_consumo_kg,
    -- Saldo aves con piso-0
    GREATEST(0,
        ai.inicial - SUM(se.perdidas_totales_dia + se.ventas_dia) OVER w_ord
    )::INT                                                        AS saldo_aves,
    -- % pérdidas respecto al saldo al inicio del día
    CASE
        WHEN GREATEST(0, ai.inicial - COALESCE(
            SUM(se.perdidas_totales_dia + se.ventas_dia) OVER w_prev, 0)) > 0
        THEN ROUND(100.0 * se.total_mort_sel_dia /
            GREATEST(0, ai.inicial - COALESCE(
                SUM(se.perdidas_totales_dia + se.ventas_dia) OVER w_prev, 0)), 4)
        WHEN se.total_mort_sel_dia > 0 THEN 100.0
        ELSE NULL
    END                                                           AS pct_perdidas_dia,
    -- Saldo alimento (ya calculado y persistido por RecalcularSaldoAlimentoPorLoteAsync)
    se.saldo_alimento_kg,
    se.ingreso_alimento_kg,
    se.traslado_entrada_kg,
    se.traslado_salida_kg,
    se.consumo_bodega_kg,
    se.documento,
    se.despacho_h        AS despacho_hembras,
    se.despacho_m        AS despacho_machos,
    se.despacho_x        AS despacho_mixtas,
    se.tipo_alimento,
    se.peso_prom_hembras,
    se.peso_prom_machos,
    se.uniformidad_hembras,
    se.uniformidad_machos,
    se.cv_hembras,
    se.cv_machos,
    se.consumo_agua_diario,
    se.consumo_agua_ph,
    se.consumo_agua_orp,
    se.consumo_agua_temperatura,
    se.observaciones,
    se.ciclo,
    se.metadata,
    se.items_adicionales,
    se.historico_consumo_alimento,
    se.created_by_user_id
FROM seg_enriquecido se
CROSS JOIN aves_iniciales ai
WINDOW
    w_ord  AS (ORDER BY se.fecha, se.seg_id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW),
    w_prev AS (ORDER BY se.fecha, se.seg_id ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING)
ORDER BY se.fecha, se.seg_id;
$$;
```

---

## Archivos a Crear / Modificar

### Base de Datos (SQL + Migración EF Core)

**Buenas prácticas:** la función PostgreSQL se entrega en dos artefactos complementarios:

| Artefacto | Propósito |
|-----------|-----------|
| `/backend/sql/fn_seguimiento_diario_engorde.sql` | Referencia versionada; se puede aplicar manualmente en psql o DBeaver |
| Migración EF Core `AddFnSeguimientoDiarioEngorde` | Aplica la función automáticamente con `dotnet ef database update`; forma parte del historial de migraciones |

**Cómo crear la migración para una función SQL:**
```bash
# Desde /backend/src/ZooSanMarino.API/
dotnet ef migrations add AddFnSeguimientoDiarioEngorde \
  --project ../ZooSanMarino.Infrastructure \
  --startup-project . \
  --context ZooSanMarinoContext
```
Luego **editar** el archivo `Up()` generado para agregar:
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    var sql = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "../../../../sql/fn_seguimiento_diario_engorde.sql"));
    migrationBuilder.Sql(sql);
}
protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_seguimiento_diario_engorde(INT);");
}
```
O bien embeber el SQL directamente como string literal en el `Up()` (más portable, sin dependencia de ruta de archivo).

**Aplicar:**
```bash
dotnet ef database update
```

| Archivo | Acción |
|---------|--------|
| `/backend/sql/fn_seguimiento_diario_engorde.sql` | **Crear** — script con `CREATE OR REPLACE FUNCTION` |
| Migración auto-generada + editada | **Crear** — `Up()` llama `migrationBuilder.Sql(fnSql)` |

### Backend — Application

| Archivo | Acción | Detalle |
|---------|--------|---------|
| `/backend/src/ZooSanMarino.Application/DTOs/SeguimientoDiarioTablaFilaDto.cs` | **Crear** | Record con todas las columnas de la función |
| `/backend/src/ZooSanMarino.Application/Interfaces/ISeguimientoAvesEngordeEcuadorService.cs` | **Modificar** | Agregar `Task<IReadOnlyList<SeguimientoDiarioTablaFilaDto>> GetTablaDiariaAsync(int loteId)` |

### Backend — Infrastructure

| Archivo | Acción | Detalle |
|---------|--------|---------|
| `/backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoAvesEngordeEcuadorService.cs` | **Modificar** | (1) Cambiar todas las referencias de `_ctx.SeguimientoDiarioAvesEngorde` → `_ctx.SeguimientoDiarioAvesEngordeEcuador`. (2) Actualizar `MapToDto` para aceptar `SeguimientoDiarioAvesEngordeEcuador`. (3) Implementar `GetTablaDiariaAsync`. (4) Agregar `RecalcularSaldoAlimentoPorLoteAsync` en `GetByLoteAsync`. |

### Backend — API

| Archivo | Acción | Detalle |
|---------|--------|---------|
| `/backend/src/ZooSanMarino.API/Controllers/SeguimientoAvesEngordeEcuadorController.cs` | **Modificar** | Agregar `GET por-lote/{loteId}/tabla-diaria` |

### Frontend — Service

| Archivo | Acción | Detalle |
|---------|--------|---------|
| `/frontend/src/app/features/aves-engorde/services/seguimiento-aves-engorde.service.ts` | **Modificar** | Agregar `getTablaDiaria(loteId)`, exportar `SeguimientoDiarioTablaFilaDto` |

### Frontend — List Component

| Archivo | Acción | Detalle |
|---------|--------|---------|
| `/frontend/src/app/features/aves-engorde/pages/seguimiento-aves-engorde-list/seguimiento-aves-engorde-list.component.ts` | **Modificar** | `onLoteChange()` llama `getTablaDiaria()` → asigna `tablaFilas[]`; mantiene `seguimientos[]` solo para crear/editar |
| `.../seguimiento-aves-engorde-list.component.html` | **Modificar** | Pasar `[tablaFilas]` al `TabsPrincipalEngordeComponent` |

### Frontend — TabsPrincipal Component

| Archivo | Acción | Detalle |
|---------|--------|---------|
| `/frontend/src/app/features/aves-engorde/pages/tabs-principal-engorde/tabs-principal-engorde.component.ts` | **Modificar** | Nuevo `@Input() tablaFilas`; eliminar `buildDiarioFilas()`, `aggregateHistoricoPorFecha()`, `computeSaldoAlimento*()`, `computeVentaAves*()`, `avesInicialesLote()`; las columnas de la tabla vienen directas del DTO |
| `.../tabs-principal-engorde.component.html` | **Modificar** | Bindings apuntan a `f.edadDia`, `f.saldoAves`, `f.saldoAlimentoKg`, etc. del nuevo DTO |

---

## DTO Backend (`SeguimientoDiarioTablaFilaDto.cs`)

```csharp
namespace ZooSanMarino.Application.DTOs;

public sealed record SeguimientoDiarioTablaFilaDto(
    long   SegId,
    DateOnly Fecha,
    int    EdadDia,
    short  Semana,
    int    MortalidadHembras,
    int    MortalidadMachos,
    int    SelH,
    int    SelM,
    int    ErrorSexajeHembras,
    int    ErrorSexajeMachos,
    int    TotalMortSelDia,
    int    PerdidasTotalesDia,
    decimal ConsumoKgHembras,
    decimal ConsumoKgMachos,
    decimal ConsumoDiaKg,
    decimal AcumConsumoKg,
    int    SaldoAves,
    decimal? PctPerdidasDia,
    decimal? SaldoAlimentoKg,
    decimal IngresoAlimentoKg,
    decimal TrasladoEntradaKg,
    decimal TrasladoSalidaKg,
    decimal ConsumoBodegaKg,
    string? Documento,
    int    DespachoHembras,
    int    DespachoMachos,
    int    DespachoMixtas,
    string? TipoAlimento,
    double? PesoPromHembras,
    double? PesoPromMachos,
    double? UniformidadHembras,
    double? UniformidadMachos,
    double? CvHembras,
    double? CvMachos,
    double? ConsumoAguaDiario,
    double? ConsumoAguaPh,
    double? ConsumoAguaOrp,
    double? ConsumoAguaTemperatura,
    string? Observaciones,
    string? Ciclo,
    System.Text.Json.JsonDocument? Metadata,
    System.Text.Json.JsonDocument? ItemsAdicionales,
    System.Text.Json.JsonDocument? HistoricoConsumoAlimento,
    string? CreatedByUserId
);
```

---

## DTO Frontend (`SeguimientoDiarioTablaFilaDto` TypeScript)

```typescript
export interface SeguimientoDiarioTablaFilaDto {
  segId: number;
  fecha: string;                    // YYYY-MM-DD
  edadDia: number;
  semana: number;
  mortalidadHembras: number;
  mortalidadMachos: number;
  selH: number;
  selM: number;
  errorSexajeHembras: number;
  errorSexajeMachos: number;
  totalMortSelDia: number;
  perdidasTotalesDia: number;
  consumoKgHembras: number;
  consumoKgMachos: number;
  consumoDiaKg: number;
  acumConsumoKg: number;
  saldoAves: number;
  pctPerdidasDia: number | null;
  saldoAlimentoKg: number | null;
  ingresoAlimentoKg: number;
  trasladoEntradaKg: number;
  trasladoSalidaKg: number;
  consumoBodegaKg: number;
  documento: string | null;
  despachoHembras: number;
  despachoMachos: number;
  despachoMixtas: number;
  tipoAlimento: string | null;
  pesoPromHembras: number | null;
  pesoPromMachos: number | null;
  uniformidadHembras: number | null;
  uniformidadMachos: number | null;
  cvHembras: number | null;
  cvMachos: number | null;
  consumoAguaDiario: number | null;
  consumoAguaPh: number | null;
  consumoAguaOrp: number | null;
  consumoAguaTemperatura: number | null;
  observaciones: string | null;
  ciclo: string | null;
  metadata: any;
  itemsAdicionales: any;
  historicoConsumoAlimento: any;
  createdByUserId: string | null;
}
```

---

## Lógica de Negocio Clave (restricciones)

### `avesIniciales` — Regla de prioridad (BUG-02 ya corregido)
```
lote CERRADO  → Σ(bajas_seguimiento) + Σ(VENTA_AVES)   [saldo final debe ser 0]
solo avesEncasetadas > 0 y suma_hm = 0 → avesEncasetadas
solo suma_hm > 0 y avesEncasetadas = 0 → suma_hm
ambos iguales → avesEncasetadas
ambos distintos y ABIERTO → avesEncasetadas   [campo canónico]
```

### Filtro historico `INV_INGRESO` (no inflar saldo alimento)
```sql
-- Excluir devoluciones generadas por el sistema al editar seguimiento:
AND NOT (tipo_evento = 'INV_INGRESO'
         AND referencia LIKE 'Seguimiento aves engorde #%')
-- Excluir devoluciones por eliminación:
AND NOT (referencia LIKE '%devolución por eliminación%'
      OR referencia LIKE '%devolucion por eliminacion%')
```

### Scope del galpón (no scope del lote) para movimientos de alimento
Los eventos `INV_INGRESO`, `INV_TRASLADO_*`, `INV_CONSUMO` se registran a nivel `farm_id + nucleo_id + galpon_id` (el `lote_ave_engorde_id` puede ser NULL cuando el trigger corre antes de crear el lote). Solo `VENTA_AVES` usa `lote_ave_engorde_id`.

### `saldo_alimento_kg` — no recalcular en la función
Ya persiste en `seguimiento_diario_aves_engorde_ecuador.saldo_alimento_kg`. La función lo lee directamente. El servicio Ecuador debe llamar `RecalcularSaldoAlimentoPorLoteAsync` antes de llamar la función (igual que `SeguimientoAvesEngordeService.GetByLoteAsync`).

### Tabla correcta post-migración 20260517
Todos los scripts SQL y el servicio C# deben referenciar **`seguimiento_diario_aves_engorde_ecuador`**, no la tabla original `seguimiento_diario_aves_engorde` (eliminada por la migración de split por país).

### `pct_perdidas_dia` — denominador
```
saldo_inicio_dia = avesIniciales - SUM(perdidas + ventas)[filas anteriores]
= saldo_aves de la fila ANTERIOR (LAG o window w_prev)
```

---

## Lo que se mantiene en el frontend (NO migrar)

| Elemento | Razón |
|----------|-------|
| `diaCorto` | `Intl.DateTimeFormat` con locale `es-CO` — depende del browser |
| `tipoAlimentoCorto` | Abreviación visual (PRE/INI/ENG/FIN-D) — lógica de presentación |
| Filtros de tabla (fecha/semana/tipo) | Son filtros de vista en memoria sobre datos ya recibidos |
| Export Excel | Opera sobre `tablaFilas` ya recibidas |
| `hayFiltrosDiarioActivos`, `diarioFilasFiltradas` | Filtros client-side sobre el array |

---

## Plan de Validación

| Paso | Qué verificar |
|------|--------------|
| SQL directo | `SELECT * FROM fn_seguimiento_diario_engorde(75)` — validar 19 filas con mismos valores que la tabla actual del front |
| Lote abierto | Verificar `saldo_aves` baja correctamente con cada día |
| Lote cerrado | Verificar `aves_iniciales = Σ salidas` → `saldo_aves` llega a 0 |
| Saldo alimento | Validar que la columna `saldo_alimento_kg` de la función = `saldo_alimento_kg` del campo en BD |
| pct_perdidas | Primera fila: denominador = avesIniciales; resto: fila anterior |
| Historico vacío | Lote sin movimientos de inventario → `ingreso_alimento_kg = 0`, `documento = NULL` |
