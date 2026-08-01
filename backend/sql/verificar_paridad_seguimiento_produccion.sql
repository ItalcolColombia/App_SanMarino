-- ============================================================================
-- GATE MULTIPAÍS — paridad fila a fila de fn_seguimiento_diario_produccion
-- (modelo: verificar_paridad_saldo_engorde.sql; regla CLAUDE.md: todo cambio a la
--  fn se valida contra TODAS las empresas con producción, no solo la que motivó el fix)
--
-- USO:
--   1a corrida (ANTES del cambio): congela la línea base en _paridad_seg_prod_base.
--   2a corrida (DESPUÉS): compara fila a fila y reporta diferencias por empresa.
--   Para re-congelar: DROP TABLE _paridad_seg_prod_base; y volver a correr.
--
-- Criterio: toda empresa que NO sea el objetivo del cambio debe salir con 0 en todas
-- las columnas de diferencia; cualquier otra cosa se justifica por escrito por número.
-- ============================================================================

-- Paso 1: congelar la linea base si no existe
DO $paridad$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = '_paridad_seg_prod_base') THEN
        CREATE TABLE _paridad_seg_prod_base AS
        SELECT c.name AS empresa,
               l.lote_postura_produccion_id AS lpp_id,
               f.fecha, f.seg_id, f.fuente,
               f.mortalidad_hembras, f.mortalidad_machos, f.sel_h, f.sel_m,
               f.error_sexaje_hembras, f.error_sexaje_machos,
               f.cons_kg_h, f.cons_kg_m,
               f.huevo_tot, f.huevo_inc,
               f.huevo_tot_acum, f.huevo_inc_acum,
               f.pct_postura_dia,
               f.aves_h_inicio_dia, f.aves_m_inicio_dia,
               f.saldo_aves_h, f.saldo_aves_m,
               f.semana, f.edad_dias
          FROM lote_postura_produccion l
          JOIN farms fa ON fa.id = l.granja_id
          JOIN companies c ON c.id = fa.company_id
          CROSS JOIN LATERAL fn_seguimiento_diario_produccion(l.lote_postura_produccion_id, NULL) f
         WHERE l.deleted_at IS NULL;
        RAISE NOTICE 'Linea base congelada: % filas. Aplicar el cambio y volver a correr este script.',
            (SELECT count(*) FROM _paridad_seg_prod_base);
    END IF;
END
$paridad$;

-- Paso 2: comparar la corrida ACTUAL contra la base (solo informa si la base ya existia)
WITH actual AS (
    SELECT c.name AS empresa,
           l.lote_postura_produccion_id AS lpp_id,
           f.fecha, f.seg_id,
           f.mortalidad_hembras, f.mortalidad_machos, f.sel_h, f.sel_m,
           f.error_sexaje_hembras, f.error_sexaje_machos,
           f.cons_kg_h, f.cons_kg_m,
           f.huevo_tot, f.huevo_inc,
           f.huevo_tot_acum, f.huevo_inc_acum,
           f.pct_postura_dia,
           f.aves_h_inicio_dia, f.aves_m_inicio_dia,
           f.saldo_aves_h, f.saldo_aves_m
      FROM lote_postura_produccion l
      JOIN farms fa ON fa.id = l.granja_id
      JOIN companies c ON c.id = fa.company_id
      CROSS JOIN LATERAL fn_seguimiento_diario_produccion(l.lote_postura_produccion_id, NULL) f
     WHERE l.deleted_at IS NULL
),
pareo AS (
    SELECT COALESCE(b.empresa, a.empresa) AS empresa,
           COALESCE(b.lpp_id, a.lpp_id)   AS lpp_id,
           COALESCE(b.fecha, a.fecha)     AS fecha,
           (b.lpp_id IS NULL)::int        AS fila_nueva,
           (a.lpp_id IS NULL)::int        AS fila_desaparecida,
           CASE WHEN b.lpp_id IS NOT NULL AND a.lpp_id IS NOT NULL THEN
               (b.saldo_aves_h IS DISTINCT FROM a.saldo_aves_h)::int
             + (b.saldo_aves_m IS DISTINCT FROM a.saldo_aves_m)::int
             + (b.aves_h_inicio_dia IS DISTINCT FROM a.aves_h_inicio_dia)::int
             + (b.huevo_tot_acum IS DISTINCT FROM a.huevo_tot_acum)::int
             + (b.huevo_inc_acum IS DISTINCT FROM a.huevo_inc_acum)::int
             + (ABS(COALESCE(b.pct_postura_dia, 0) - COALESCE(a.pct_postura_dia, 0)) > 0.001)::int
             + (ABS(COALESCE(b.cons_kg_h, 0) - COALESCE(a.cons_kg_h, 0)) > 0.001)::int
             + (b.mortalidad_hembras IS DISTINCT FROM a.mortalidad_hembras)::int
             + (b.sel_h IS DISTINCT FROM a.sel_h)::int
             + (b.error_sexaje_hembras IS DISTINCT FROM a.error_sexaje_hembras)::int
             + (b.huevo_tot IS DISTINCT FROM a.huevo_tot)::int
           ELSE 0 END AS columnas_distintas
      FROM _paridad_seg_prod_base b
      FULL OUTER JOIN actual a
        ON a.lpp_id = b.lpp_id AND a.fecha = b.fecha
       AND ((a.seg_id IS NULL AND b.seg_id IS NULL) OR a.seg_id = b.seg_id)
)
SELECT empresa,
       count(*)                       AS filas,
       SUM(fila_nueva)                AS filas_nuevas,
       SUM(fila_desaparecida)         AS filas_desaparecidas,
       SUM(columnas_distintas)        AS celdas_distintas
  FROM pareo
 GROUP BY empresa
 ORDER BY empresa;
