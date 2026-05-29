# Plan de Desarrollo — Fix: `fn_seguimiento_diario_engorde` (Saldos y Primer Ingreso)

**ID:** 10
**Feature:** Corrección de la función PostgreSQL `fn_seguimiento_diario_engorde(p_lote_id INT)`
**Estado:** En implementación
**Fecha:** 2026-05-28
**Plan padre:** [09_fn_seguimiento_diario_engorde.md](./09_fn_seguimiento_diario_engorde.md)

---

## Contexto y Problema

La tabla diaria del seguimiento engorde (servida por `fn_seguimiento_diario_engorde`) presenta tres síntomas reportados por el usuario:

1. **El primer registro de ingreso de alimento no aparece** en la columna `ingreso_alimento_kg` el día en que se creó el primer seguimiento del lote.
2. **Faltan saldos al final** o aparecen descuadrados respecto al inventario físico esperado.
3. **El consumo del seguimiento diario no reduce el inventario** visible en la columna `consumo_bodega_kg` (se ve en 0 mientras el `consumo_dia_kg` sí tiene valores).

## Diagnóstico (validado contra `sanmarinoapplocal`)

### Hallazgo #1 — Bug crítico de tipo de fecha en `rango_seg`

La columna `seguimiento_diario_aves_engorde.fecha` es `TIMESTAMP WITH TIME ZONE` con hora 12:00:00-05:00. El CTE `rango_seg` la devuelve sin castear a DATE:

```sql
SELECT MIN(s.fecha) AS fecha_min, MAX(s.fecha) AS fecha_max  -- TIMESTAMPTZ
FROM seguimiento_diario_aves_engorde s
WHERE s.lote_ave_engorde_id = p_lote_id
```

La comparación `DATE(h.fecha_operacion) >= rs.fecha_min` convierte:
- `DATE('2026-02-27')` → `'2026-02-27 00:00:00'`
- `rs.fecha_min` = `'2026-02-27 12:00:00-05'`

Resultado: `00:00 >= 12:00` ⇒ **FALSE** ❌

Por eso los movimientos del histórico del MISMO día del primer seguimiento (cuando ese día es el `MIN`) **NO** aparecen en la salida de la función.

#### Validación en BD (lote 5)

| Dato | Valor |
|---|---|
| Primer seguimiento | `2026-02-27` |
| INV_INGRESO el 2026-02-27 | 5000 kg (`numero_documento = 005-001-000053977`) |
| `fn_seguimiento_diario_engorde(5)` reporta `ingreso_alimento_kg` para 2026-02-27 | **0 kg** ❌ |
| `saldo_alimento_kg` persistido para 2026-02-27 | 116,195 kg ✅ (incluye apertura + ingreso) |

#### Validación en BD (lote 32)

| Dato | Valor |
|---|---|
| Primer seguimiento | `2025-12-30` |
| INV_INGRESO el 2025-12-30 | 6000 kg |
| `fn_seguimiento_diario_engorde(32)` reporta `ingreso_alimento_kg` para 2025-12-30 | **0 kg** ❌ |
| `saldo_alimento_kg` persistido para 2025-12-30 | 5800 kg ✅ (= 6000 − 200 consumo) |

### Hallazgo #2 — `consumo_bodega_kg` no refleja el consumo del seguimiento

El SQL actual mapea `consumo_bodega_kg` desde la suma de `INV_CONSUMO` del histórico unificado. Pero:

- El `RecalcularSaldoAlimentoPorLoteAsync` (backend) **excluye explícitamente** `INV_CONSUMO` del cálculo de saldo (ver `TryGetHistDeltaAndOrd` en `SeguimientoAvesEngordeService.cs:385-411` — solo procesa `INV_INGRESO`, `INV_TRASLADO_ENTRADA`, `INV_TRASLADO_SALIDA`).
- El consumo real diario que reduce el saldo viene de `seguimiento_diario_aves_engorde.consumo_kg_hembras + consumo_kg_machos` (= `consumo_dia_kg`).
- Los registros `INV_CONSUMO` que existen en `lote_registro_historico_unificado` vienen del módulo `inventario_gestion_movimiento` y pueden estar asociados a OTROS lotes que ocuparon el mismo galpón (validado: lote 33 ocupó el mismo galpón G0050 que el lote 5).

Por eso la columna actual da una vista **inconsistente** que contamina con consumos de otros lotes y no muestra el verdadero consumo del seguimiento.

### Hallazgo #3 — Saldos al final descuadrados por TRASLADO_SALIDA negativo + servicio Ecuador no recalcula

#### Sub-hallazgo 3a — Datos sucios con `cantidad_kg < 0`

```sql
fecha       | cantidad_kg | referencia
2026-02-11  | -920.000    | Cuadre saldos Excel — Insertar traslado salida ...
2026-02-22  |  7810.000   | (sin referencia)
2026-04-30  |  1080.000   | (sin referencia)
```

El SQL actual hace `SUM(CASE WHEN h.tipo_evento = 'INV_TRASLADO_SALIDA' THEN COALESCE(h.cantidad_kg, 0) ELSE 0 END)` que suma directamente `-920 + 7810 + 1080 = 7970`.

El backend hace `delta = -Math.Abs(kg)` (`SeguimientoAvesEngordeService.cs:406`), siempre tomando valor negativo absoluto. Para los mismos datos calcula `-(920 + 7810 + 1080) = -9810` ⇒ saldo correcto en `saldo_alimento_kg`.

Diferencia: 1,840 kg. La función SQL queda divergente respecto al saldo persistido.

#### Sub-hallazgo 3b — `SeguimientoAvesEngordeEcuadorService` no llama a `RecalcularSaldoAlimentoPorLoteAsync`

`SeguimientoAvesEngordeEcuadorService.GetTablaDiariaAsync(loteId)` (`SeguimientoAvesEngordeEcuadorService.cs:154-160`) ejecuta directamente la función SQL **sin recalcular** previamente el saldo persistido. Si hay movimientos nuevos en el histórico entre llamadas, los saldos quedan obsoletos.

## Solución propuesta

Hacer la función SQL **autónoma**: que calcule el saldo de alimento internamente con la misma lógica que el backend, eliminando la dependencia del valor persistido y arreglando los tres síntomas.

### Cambios en `backend/sql/fn_seguimiento_diario_engorde.sql`

#### Cambio 1 — `rango_seg` devuelve DATE (no TIMESTAMPTZ)

```sql
rango_seg AS (
    SELECT
        MIN(s.fecha)::DATE AS fecha_min,
        MAX(s.fecha)::DATE AS fecha_max
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
)
```

#### Cambio 2 — Usar `ABS()` para TRASLADO_SALIDA en `hist_alimento`

```sql
COALESCE(SUM(CASE
    WHEN h.tipo_evento = 'INV_TRASLADO_SALIDA'
    THEN ABS(COALESCE(h.cantidad_kg, 0)) ELSE 0 END), 0)::FLOAT8  AS traslado_salida_kg
```

#### Cambio 3 — Saldo de apertura del galpón antes del primer seguimiento

Nuevo CTE que replica `ComputeSaldoAperturaGalponAntesPrimerSeguimiento` del backend:

```sql
apertura_alimento AS (
    SELECT COALESCE(SUM(delta), 0)::FLOAT8 AS apertura_kg
    FROM (
        SELECT
            -- Aplicar piso-0 vía window function (no en SUM porque no es asociativo)
            CASE h.tipo_evento
                WHEN 'INV_INGRESO'          THEN  COALESCE(h.cantidad_kg, 0)
                WHEN 'INV_TRASLADO_ENTRADA' THEN  COALESCE(h.cantidad_kg, 0)
                WHEN 'INV_TRASLADO_SALIDA'  THEN -ABS(COALESCE(h.cantidad_kg, 0))
            END AS delta
        FROM lote_registro_historico_unificado h
        JOIN lote_info li ON TRUE
        JOIN rango_seg  rs ON rs.fecha_min IS NOT NULL
        WHERE NOT h.anulado
          AND h.tipo_evento IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA', 'INV_TRASLADO_SALIDA')
          AND NOT (h.tipo_evento = 'INV_INGRESO'
                   AND h.referencia IS NOT NULL
                   AND h.referencia LIKE 'Seguimiento aves engorde #%')
          AND NOT (h.referencia IS NOT NULL AND (
                   h.referencia LIKE '%devolución por eliminación%'
                OR h.referencia LIKE '%devolucion por eliminacion%'))
          AND h.farm_id = li.granja_id
          AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
          AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id
          AND DATE(h.fecha_operacion) < rs.fecha_min
    ) prev
)
```

**Nota técnica:** El cálculo es una suma simple (no aplica piso-0 evento-por-evento). En la práctica, el `ComputeSaldoAperturaGalponAntesPrimerSeguimiento` del backend procesa cada evento secuencialmente con piso 0; pero en datos reales el saldo nunca se vuelve negativo durante la apertura. Si en el futuro se requiere paridad exacta, se puede convertir a procedimiento `plpgsql` con loop, pero para la primera versión SQL `STABLE` mantenemos la suma directa.

#### Cambio 4 — Calcular `saldo_alimento_kg` dinámicamente

En la query final, reemplazar `se.saldo_alimento_kg` por:

```sql
GREATEST(0,
    (SELECT apertura_kg FROM apertura_alimento)
    + SUM(se.ingreso_alimento_kg + se.traslado_entrada_kg - se.traslado_salida_kg - se.consumo_dia_kg) OVER w_ord
)::FLOAT8  AS saldo_alimento_kg
```

#### Cambio 5 — `consumo_bodega_kg` = `consumo_dia_kg`

En el SELECT final:

```sql
se.consumo_dia_kg  AS consumo_bodega_kg
```

Eliminamos completamente el uso de `INV_CONSUMO` del histórico (que es ambiguo). La columna ahora siempre refleja el consumo del seguimiento del día.

---

## Validación pre/post aplicación

### Lote 5 — Validar que el primer ingreso aparece

```sql
-- Esperado tras el fix:
-- fecha 2026-02-27 → ingreso_alimento_kg = 5000, documento = '005-001-000053977'
SELECT fecha, ingreso_alimento_kg, documento
FROM fn_seguimiento_diario_engorde(5)
WHERE fecha = '2026-02-27';
```

### Lote 32 — Validar que el primer ingreso aparece

```sql
-- Esperado tras el fix:
-- fecha 2025-12-30 → ingreso_alimento_kg = 6000
SELECT fecha, ingreso_alimento_kg
FROM fn_seguimiento_diario_engorde(32)
WHERE fecha = '2025-12-30';
```

### Lote 5 — Validar saldo final coincide con backend

Recalcular manualmente:
- Apertura (movimientos pre-2026-02-27): 119,745 (ingresos) − 8,730 (traslados salida con ABS) + 480 (traslado entrada) = **111,495 kg**
- Suma durante seguimiento: 234,770 − 119,745 (ingresos previos) + 4,160 (tras_ent) − (9,810 − 8,730) (tras_sal con ABS) − 117,625 (consumo) = ...

Verificación directa: tras el fix, el último `saldo_alimento_kg` debe coincidir con el valor de `SaldoAlimentoKg` persistido por el backend para el último seguimiento.

```sql
SELECT
    (SELECT saldo_alimento_kg FROM fn_seguimiento_diario_engorde(5) ORDER BY fecha DESC LIMIT 1) AS saldo_funcion,
    (SELECT saldo_alimento_kg FROM seguimiento_diario_aves_engorde WHERE lote_ave_engorde_id = 5 ORDER BY fecha DESC LIMIT 1) AS saldo_persistido;
-- Deben coincidir
```

### Lote 5 — Validar `consumo_bodega_kg` ahora refleja el seguimiento

```sql
SELECT fecha, consumo_dia_kg, consumo_bodega_kg
FROM fn_seguimiento_diario_engorde(5)
ORDER BY fecha DESC
LIMIT 10;
-- Esperado: consumo_dia_kg = consumo_bodega_kg en todas las filas
```

---

## Archivos a modificar

| Archivo | Acción |
|---------|--------|
| `backend/sql/fn_seguimiento_diario_engorde.sql` | **Modificar** — aplicar cambios 1-5 |

### Aplicación

El script SQL es la fuente de verdad. Se aplica directamente con `psql` o DBeaver. No requiere nueva migración EF Core: la función ya existe (creada por migración `20260520140828_AddFnSeguimientoDiarioEngorde`), simplemente se reemplaza con `CREATE OR REPLACE FUNCTION`.

```powershell
$env:PGPASSWORD = "123456789"
& "C:\Program Files\PostgreSQL\17\bin\psql.exe" -h localhost -p 5433 -U postgres -d sanmarinoapplocal -f backend\sql\fn_seguimiento_diario_engorde.sql
```

---

## Observaciones de negocio (para visibilidad futura)

### Punto pendiente — `RecalcularSaldoAlimentoPorLoteAsync` en servicio Ecuador

Tras este fix la función SQL es autónoma y NO depende del saldo persistido. Sin embargo, queda como deuda técnica que `SeguimientoAvesEngordeEcuadorService` no llame a `RecalcularSaldoAlimentoPorLoteAsync` tras CREATE/UPDATE/DELETE, lo que deja la columna `seguimiento_diario_aves_engorde.saldo_alimento_kg` desactualizada para los registros creados por el flujo Ecuador. Como ahora la tabla diaria del frontend lee de la función SQL (que calcula al vuelo), no afecta la vista, pero sí puede afectar otros consumidores que lean directamente la columna.

**Recomendación:** ticket aparte para agregar el recálculo al servicio Ecuador (alineado con el servicio original).

### Punto pendiente — Inserción de INV_CONSUMO desde el seguimiento

El usuario sugiere que al guardar un seguimiento debería insertarse una fila `INV_CONSUMO` en `lote_registro_historico_unificado`. Sin embargo, la arquitectura actual del backend **deliberadamente NO duplica** este consumo en el histórico (ver comentario en `TryGetHistDeltaAndOrd`: "sin INV_CONSUMO: el consumo va en el seguimiento diario"). El consumo se considera evento del seguimiento, no movimiento de inventario.

Con el fix #5 (`consumo_bodega_kg` = `consumo_dia_kg`), la columna ya refleja el consumo correcto sin necesidad de duplicar datos. No se requiere cambio adicional.
