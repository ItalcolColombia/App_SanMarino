-- ============================================================================
-- VERIFICACIÓN (SOLO LECTURA) de tablas candidatas a eliminación
-- Rama: refactor/optimizacion-multipais · Fase 4 del plan
-- Ejecutar en PROD (lectura) y en local para comparar. NO modifica nada.
-- ============================================================================
-- Candidatas (sin entidad EF, sin servicio, solo aparecen en migraciones/backups):
--   _backup_cuadre_expected_2026_06_01      (backup one-shot de cuadre)
--   _migracion_saldo_alimento_2026_05_28    (scratch de migración de datos)
--   _migracion_saldo_alimento_2026_05_31    (scratch)
--   _migracion_saldo_alimento_m1_2026_05_31 (scratch)
--   guia_semana                             (sin entidad ni servicio en el código)
--   user_paises                             (sin entidad ni servicio en el código)

SELECT c.relname                              AS tabla,
       c.reltuples::bigint                    AS filas_estimadas,
       pg_size_pretty(pg_total_relation_size(c.oid)) AS tamano,
       obj_description(c.oid)                 AS comentario
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public'
  AND c.relkind = 'r'
  AND c.relname IN (
    '_backup_cuadre_expected_2026_06_01',
    '_migracion_saldo_alimento_2026_05_28',
    '_migracion_saldo_alimento_2026_05_31',
    '_migracion_saldo_alimento_m1_2026_05_31',
    'guia_semana',
    'user_paises'
  )
ORDER BY c.relname;

-- Conteos exactos (más lentos; descomentar si las estimaciones no bastan):
-- SELECT '_backup_cuadre_expected_2026_06_01' t, count(*) FROM _backup_cuadre_expected_2026_06_01
-- UNION ALL SELECT '_migracion_saldo_alimento_2026_05_28', count(*) FROM _migracion_saldo_alimento_2026_05_28
-- UNION ALL SELECT '_migracion_saldo_alimento_2026_05_31', count(*) FROM _migracion_saldo_alimento_2026_05_31
-- UNION ALL SELECT '_migracion_saldo_alimento_m1_2026_05_31', count(*) FROM _migracion_saldo_alimento_m1_2026_05_31
-- UNION ALL SELECT 'guia_semana', count(*) FROM guia_semana
-- UNION ALL SELECT 'user_paises', count(*) FROM user_paises;

-- Dependencias (vistas/fn que referencien las candidatas — debe devolver 0 filas):
SELECT DISTINCT dependent_ns.nspname AS schema_dependiente,
       dependent_view.relname        AS objeto_dependiente,
       source_table.relname          AS tabla
FROM pg_depend
JOIN pg_rewrite            ON pg_depend.objid = pg_rewrite.oid
JOIN pg_class dependent_view ON pg_rewrite.ev_class = dependent_view.oid
JOIN pg_class source_table  ON pg_depend.refobjid = source_table.oid
JOIN pg_namespace dependent_ns ON dependent_view.relnamespace = dependent_ns.oid
WHERE source_table.relname IN (
    '_backup_cuadre_expected_2026_06_01','_migracion_saldo_alimento_2026_05_28',
    '_migracion_saldo_alimento_2026_05_31','_migracion_saldo_alimento_m1_2026_05_31',
    'guia_semana','user_paises');
