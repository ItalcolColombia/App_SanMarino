-- ============================================================================
-- GATE MULTIPAÍS — paridad de fn_reporte_diario_costos_postura (levante + producción)
-- Plan: fase_de_desarrollo/reporte_diario_costos_postura_varios_registros_dia_plan.md (13-sep-2026)
-- Modelo: verificar_paridad_indicadores_semanales.sql. Correr con psql (usa \gset / \if).
--
-- Llama la fn para TODAS las empresas, sin filtros (granjas NULL = todas, ambas fases, todo el
-- rango) y guarda cada fila keyeada por (empresa, fase, lote, fecha).
--
-- USO:
--   1a corrida (ANTES del cambio): congela la línea base en _paridad_costos_postura_base.
--   2a corrida (DESPUÉS): recalcula en _paridad_costos_postura_actual y compara fila a fila.
--   Re-congelar: DROP TABLE _paridad_costos_postura_base; y volver a correr.
--
-- Criterio: toda empresa SIN permite_multiples_seguimientos_diarios sale con 0 diferencias. La que
-- lo tiene solo puede cambiar en días con 2+ registros; cada número se justifica.
-- `filas_distintas_sin_orden` compara el json de alimentos como conjunto: si una fila difiere solo en
-- el ORDEN de dos ítems empatados (mismo sexo y nombre), aparece en `filas_distintas` y no en esta.
-- ============================================================================

SET client_min_messages = warning;

SELECT to_regclass('public._paridad_costos_postura_base') IS NULL AS congelar \gset

DROP TABLE IF EXISTS _paridad_costos_postura_actual;
CREATE TABLE _paridad_costos_postura_actual AS
SELECT c.name                                                   AS empresa,
       c.id                                                     AS company_id,
       COALESCE(c.permite_multiples_seguimientos_diarios, false) AS multiples,
       f.fase,
       f.lote_id                                                AS lote,
       f.fecha,
       to_jsonb(f)                                              AS fila
  FROM companies c
 CROSS JOIN LATERAL fn_reporte_diario_costos_postura(c.id) f;

\if :congelar
    ALTER TABLE _paridad_costos_postura_actual RENAME TO _paridad_costos_postura_base;
    SELECT 'LINEA BASE CONGELADA' AS estado, empresa, fase, count(*) AS filas
      FROM _paridad_costos_postura_base GROUP BY empresa, fase ORDER BY empresa, fase;
\else
    -- 1) Resumen por empresa y fase: filas que difieren (valor distinto o fila de un solo lado).
    WITH norm AS (
        -- alimentos como conjunto ordenado (para separar "cambió el orden" de "cambió el dato")
        SELECT 'b' AS lado, * FROM _paridad_costos_postura_base
        UNION ALL
        SELECT 'a', * FROM _paridad_costos_postura_actual
    ),
    norm2 AS (
        SELECT n.*,
               n.fila - 'alimentos' || jsonb_build_object('alimentos',
                   (SELECT jsonb_agg(e ORDER BY e::text)
                      FROM jsonb_array_elements(COALESCE((n.fila ->> 'alimentos')::jsonb, '[]'::jsonb)) e))
                   AS fila_sin_orden
          FROM norm n
    ),
    pares AS (
        SELECT COALESCE(a.empresa, b.empresa)     AS empresa,
               COALESCE(a.multiples, b.multiples) AS multiples,
               COALESCE(a.fase, b.fase)           AS fase,
               (a.fila IS DISTINCT FROM b.fila)                     AS difiere,
               (a.fila_sin_orden IS DISTINCT FROM b.fila_sin_orden) AS difiere_sin_orden
          FROM (SELECT * FROM norm2 WHERE lado = 'a') a
          FULL JOIN (SELECT * FROM norm2 WHERE lado = 'b') b
            ON b.company_id = a.company_id AND b.fase = a.fase AND b.lote = a.lote AND b.fecha = a.fecha
    )
    SELECT empresa, multiples, fase, count(*) AS filas,
           count(*) FILTER (WHERE difiere)           AS filas_distintas,
           count(*) FILTER (WHERE difiere_sin_orden) AS filas_distintas_sin_orden
      FROM pares GROUP BY empresa, multiples, fase
     ORDER BY filas_distintas DESC, empresa, fase;

    -- 2) Detalle: cada columna que cambió (máx. 200).
    SELECT a.empresa, a.fase, a.lote, a.fecha, k.col, b.fila -> k.col AS antes, a.fila -> k.col AS despues
      FROM _paridad_costos_postura_actual a
      JOIN _paridad_costos_postura_base b
        ON b.company_id = a.company_id AND b.fase = a.fase AND b.lote = a.lote AND b.fecha = a.fecha
     CROSS JOIN LATERAL jsonb_object_keys(a.fila) AS k(col)
     WHERE a.fila -> k.col IS DISTINCT FROM b.fila -> k.col
     ORDER BY a.empresa, a.fase, a.lote, a.fecha, k.col
     LIMIT 200;

    -- 3) Filas que existen de un solo lado.
    SELECT 'solo_antes' AS lado, b.empresa, b.fase, b.lote, b.fecha, b.fila
      FROM _paridad_costos_postura_base b
     WHERE NOT EXISTS (SELECT 1 FROM _paridad_costos_postura_actual a
                        WHERE a.company_id = b.company_id AND a.fase = b.fase
                          AND a.lote = b.lote AND a.fecha = b.fecha)
    UNION ALL
    SELECT 'solo_despues', a.empresa, a.fase, a.lote, a.fecha, a.fila
      FROM _paridad_costos_postura_actual a
     WHERE NOT EXISTS (SELECT 1 FROM _paridad_costos_postura_base b
                        WHERE b.company_id = a.company_id AND b.fase = a.fase
                          AND b.lote = a.lote AND b.fecha = a.fecha)
     ORDER BY 1, 2, 3, 4, 5
     LIMIT 100;
\endif
