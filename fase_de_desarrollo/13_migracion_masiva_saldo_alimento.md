# Plan de Desarrollo — Migración masiva: recalcular `saldo_alimento_kg` para todos los lotes engorde

**ID:** 13
**Feature:** Script SQL idempotente que recalcula la columna `seguimiento_diario_aves_engorde.saldo_alimento_kg` para TODOS los lotes engorde activos, aplicando la lógica corregida del fix #12.
**Estado:** En ejecución
**Fecha:** 2026-05-28
**Plan padre:** [12_fix_saldo_apertura_lote_anterior.md](./12_fix_saldo_apertura_lote_anterior.md)

---

## Contexto

Tras aplicar el fix #12 (función SQL + helpers C#), la columna `saldo_alimento_kg` en `seguimiento_diario_aves_engorde` solo se recalcula cuando ocurre un CRUD en el lote. Los registros antiguos siguen con saldos heredados del lote anterior que ocupó el galpón.

### Diagnóstico pre-migración (`sanmarinoapplocal`)

| Métrica | Valor |
|---|---|
| Lotes engorde activos (`deleted_at IS NULL`) | 74 |
| Lotes con seguimientos | 73 |
| Total filas en `seguimiento_diario_aves_engorde` (lotes activos) | 3,422 |
| Saldos persistidos en NULL | 128 |
| Saldos persistidos que ya coinciden con `fn_seguimiento_diario_engorde` | 1,658 (48%) |
| Saldos persistidos divergentes | 1,636 (48%) |
| Diferencia máxima detectada | 253,254 kg |
| Lotes que requieren corrección | **42** |
| **Filas a actualizar** | **1,764** |

## Estrategia

**Approach: script SQL puro** (no requiere arrancar backend) que:

1. **Usa la función `fn_seguimiento_diario_engorde`** como fuente de verdad (ya tiene el fix #12 aplicado).
2. **Snapshot previo** en una tabla temporal para auditoría (fecha, lote_id, valor antes, valor nuevo).
3. **UPDATE atómico** dentro de transacción.
4. **Validación post**: 0 discrepancias entre persistido y `fn_seguimiento_diario_engorde`.
5. **Idempotente**: re-ejecutar el script no produce efectos diferentes (solo actualiza filas que difieren).

### Por qué SQL puro y no script C#

| Criterio | Script SQL | Script C# (`RecalcularSaldoAlimentoPorLoteAsync` en loop) |
|---|---|---|
| Velocidad | ✅ ~3s para 73 lotes (un solo query con LATERAL JOIN) | ❌ ~30s+ (overhead EF Core por lote) |
| Requiere backend | ❌ no | ✅ sí |
| Auditabilidad | ✅ snapshot en tabla, log de cambios | ⚠️ logs dispersos |
| Riesgo | ✅ se ejecuta en transacción, rollback fácil | ⚠️ cada lote es un SaveChanges separado, rollback parcial |
| Lógica | ✅ misma que la función SQL (ya validada 159/159) | ✅ misma lógica |
| Reusable | ✅ se puede re-ejecutar tras nuevos backfills de histórico | ✅ igual |

## Diseño del script

[`backend/sql/migrate_recalcular_saldo_alimento_engorde.sql`](../backend/sql/migrate_recalcular_saldo_alimento_engorde.sql):

```sql
BEGIN;

-- 1. Snapshot ANTES (tabla temporal de auditoría)
CREATE TEMP TABLE _migracion_saldo_alimento_snapshot AS
SELECT
    s.id AS seg_id,
    s.lote_ave_engorde_id AS lote_id,
    s.fecha,
    s.saldo_alimento_kg AS saldo_antes
FROM seguimiento_diario_aves_engorde s
JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
WHERE l.deleted_at IS NULL;

-- 2. UPDATE atómico usando fn_seguimiento_diario_engorde como fuente de verdad
WITH nuevos_saldos AS (
    SELECT
        l.lote_ave_engorde_id AS lote_id,
        fn.seg_id,
        fn.saldo_alimento_kg::numeric(18,3) AS saldo_nuevo
    FROM lote_ave_engorde l
    CROSS JOIN LATERAL fn_seguimiento_diario_engorde(l.lote_ave_engorde_id) fn
    WHERE l.deleted_at IS NULL
)
UPDATE seguimiento_diario_aves_engorde p
SET
    saldo_alimento_kg = n.saldo_nuevo,
    updated_at = (now() AT TIME ZONE 'utc')
FROM nuevos_saldos n
WHERE p.id = n.seg_id
  AND (
    p.saldo_alimento_kg IS NULL
    OR ABS(p.saldo_alimento_kg - n.saldo_nuevo) >= 0.001
  );

-- 3. Resumen de cambios
SELECT
    COUNT(*) AS filas_actualizadas,
    COUNT(*) FILTER (WHERE s.saldo_antes IS NULL) AS de_null_a_valor,
    COUNT(*) FILTER (WHERE s.saldo_antes IS NOT NULL) AS valor_corregido,
    COUNT(DISTINCT s.lote_id) AS lotes_afectados,
    MAX(ABS(COALESCE(s.saldo_antes, 0) - p.saldo_alimento_kg)) AS max_delta_kg
FROM _migracion_saldo_alimento_snapshot s
JOIN seguimiento_diario_aves_engorde p ON p.id = s.seg_id
WHERE (
    s.saldo_antes IS NULL AND p.saldo_alimento_kg IS NOT NULL
) OR (
    s.saldo_antes IS NOT NULL AND ABS(s.saldo_antes - p.saldo_alimento_kg) >= 0.001
);

-- 4. Validación: 0 discrepancias post-migración
WITH check_post AS (
    SELECT
        fn.seg_id,
        fn.saldo_alimento_kg AS fn_saldo,
        p.saldo_alimento_kg::float8 AS p_saldo
    FROM lote_ave_engorde l
    CROSS JOIN LATERAL fn_seguimiento_diario_engorde(l.lote_ave_engorde_id) fn
    JOIN seguimiento_diario_aves_engorde p ON p.id = fn.seg_id
    WHERE l.deleted_at IS NULL
)
SELECT
    COUNT(*) AS total,
    COUNT(*) FILTER (WHERE ABS(fn_saldo - p_saldo) < 0.001) AS coinciden,
    COUNT(*) FILTER (WHERE ABS(fn_saldo - p_saldo) >= 0.001) AS divergen,
    MAX(ABS(fn_saldo - p_saldo)) AS max_diff
FROM check_post;

-- Si la validación es OK (divergen = 0, max_diff < 0.001), COMMIT.
-- Sino, ROLLBACK y diagnosticar.
COMMIT;
```

## Pasos de ejecución

1. **Snapshot a archivo .csv** opcional para auditoría externa.
2. **`BEGIN` + script + verificación + `COMMIT`** en una sola transacción.
3. Si hay alguna discrepancia residual (> 0.001 kg), `ROLLBACK` y diagnosticar fila por fila.
4. **Resumen final**: filas actualizadas, lotes afectados, max delta.

## Validación esperada

| Métrica | Pre-migración | Post-migración esperado |
|---|---|---|
| Saldos NULL | 128 | 0 |
| Saldos coincidentes con fn | 1,658 | 3,422 (100%) |
| Saldos divergentes | 1,636 | 0 |
| Max diff vs fn | 253,254 kg | < 0.001 kg |

## Casos no cubiertos / supuestos

- **Lotes con `deleted_at IS NOT NULL`**: NO se tocan. Quedan con sus saldos históricos.
- **`updated_at`**: se setea a `now() AT TIME ZONE 'utc'` para auditoría.
- **No se publica evento de dominio** (sin invocar EF Core no hay eventos). Si hay subscriptores que deban reaccionar (ej. push notifications, recálculo de indicadores), ejecutar después manualmente o vía endpoint.

## Rollback de emergencia

Si tras la migración se detecta un problema, se puede restaurar usando el snapshot persistido en una tabla:

```sql
-- Crear tabla persistente antes (en lugar de TEMP):
CREATE TABLE _migracion_saldo_alimento_2026_05_28 AS
SELECT s.id, s.saldo_alimento_kg AS saldo_antes
FROM seguimiento_diario_aves_engorde s
JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
WHERE l.deleted_at IS NULL;

-- Para revertir (ROLLBACK manual):
UPDATE seguimiento_diario_aves_engorde p
SET saldo_alimento_kg = b.saldo_antes
FROM _migracion_saldo_alimento_2026_05_28 b
WHERE p.id = b.id;
```

El script de migración usará una tabla **persistente** (no TEMP) para permitir rollback post-COMMIT.
