-- ============================================================================
-- GATE MULTIPAÍS — paridad de los INDICADORES SEMANALES de postura (levante + producción)
-- Plan: fase_de_desarrollo/indicadores_semanales_varios_registros_dia_plan.md (13-sep-2026)
-- Modelo: verificar_paridad_seguimiento_produccion.sql. Correr con psql (usa \gset / \if).
--
-- Cubre las tres fns que alimentan las pestañas Indicadores y Gráfica:
--   • fn_indicadores_levante_postura            — todos los lotes con seguimiento de levante
--   • fn_indicadores_produccion_postura         — todos los LPP, todas las semanas
--   • fn_clasificacion_huevo_items_produccion   — todos los LPP (Primera / Pnc)
--
-- USO:
--   1a corrida (ANTES del cambio): congela la línea base en _paridad_ind_sem_base.
--   2a corrida (DESPUÉS): recalcula en _paridad_ind_sem_actual y compara fila a fila.
--   Re-congelar: DROP TABLE _paridad_ind_sem_base; y volver a correr.
--
-- Criterio: toda empresa SIN permite_multiples_seguimientos_diarios sale con 0 diferencias. La que
-- lo tiene solo puede cambiar en semanas con 2+ registros el mismo día; cada número se justifica.
-- ============================================================================

SET client_min_messages = warning;

SELECT to_regclass('public._paridad_ind_sem_base') IS NULL AS congelar \gset

DROP TABLE IF EXISTS _paridad_ind_sem_actual;
CREATE TABLE _paridad_ind_sem_actual (
    tipo        text,
    empresa     text,
    company_id  integer,
    multiples   boolean,
    lote        integer,
    semana      integer,
    clave       text,
    fila        jsonb
);

DO $gate$
DECLARE
    r record;
BEGIN
    FOR r IN
        SELECT l.lote_id, l.company_id, c.name,
               COALESCE(c.permite_multiples_seguimientos_diarios, false) AS multiples
          FROM lotes l
          JOIN companies c ON c.id = l.company_id
         WHERE l.deleted_at IS NULL
           AND EXISTS (SELECT 1 FROM seguimiento_diario_levante s
                        WHERE s.tipo_seguimiento = 'levante' AND s.lote_id = l.lote_id::text)
    LOOP
        INSERT INTO _paridad_ind_sem_actual
        SELECT 'levante', r.name, r.company_id, r.multiples, r.lote_id, f.semana, f.semana::text, to_jsonb(f)
          FROM fn_indicadores_levante_postura(r.lote_id) f;
    END LOOP;

    FOR r IN
        SELECT p.lote_postura_produccion_id AS lpp, p.company_id, c.name,
               COALESCE(c.permite_multiples_seguimientos_diarios, false) AS multiples
          FROM lote_postura_produccion p
          JOIN companies c ON c.id = p.company_id
         WHERE p.deleted_at IS NULL
    LOOP
        -- fn_indicadores_produccion_postura crea la TEMP `_seg` ON COMMIT DROP sin DROP previo:
        -- dentro de un solo DO hay que soltarla antes de cada llamada.
        DROP TABLE IF EXISTS _seg;
        INSERT INTO _paridad_ind_sem_actual
        SELECT 'produccion', r.name, r.company_id, r.multiples, r.lpp, f.semana, f.semana::text, to_jsonb(f)
          FROM fn_indicadores_produccion_postura(r.company_id, r.lpp, NULL, NULL, NULL, NULL, NULL) f;

        INSERT INTO _paridad_ind_sem_actual
        SELECT 'clasificacion', r.name, r.company_id, r.multiples, r.lpp, f.semana,
               concat_ws('|', f.semana, f.tipo_huevo, f.codigo, f.nombre), to_jsonb(f)
          FROM fn_clasificacion_huevo_items_produccion(r.company_id, r.lpp, NULL, NULL, NULL, NULL, NULL) f;
    END LOOP;
END
$gate$;

\if :congelar
    ALTER TABLE _paridad_ind_sem_actual RENAME TO _paridad_ind_sem_base;
    SELECT 'LINEA BASE CONGELADA' AS estado, tipo, empresa, count(*) AS filas
      FROM _paridad_ind_sem_base GROUP BY tipo, empresa ORDER BY tipo, empresa;
\else
    -- 1) Resumen: filas que difieren (valor distinto, o fila que solo existe de un lado), por empresa.
    WITH b AS (SELECT * FROM _paridad_ind_sem_base),
         a AS (SELECT * FROM _paridad_ind_sem_actual),
         pares AS (
            SELECT COALESCE(a.tipo, b.tipo) AS tipo, COALESCE(a.empresa, b.empresa) AS empresa,
                   COALESCE(a.multiples, b.multiples) AS multiples,
                   (a.fila IS DISTINCT FROM b.fila) AS difiere
              FROM a FULL JOIN b ON b.tipo = a.tipo AND b.lote = a.lote AND b.clave = a.clave)
    SELECT tipo, empresa, multiples, count(*) AS filas, count(*) FILTER (WHERE difiere) AS filas_distintas
      FROM pares GROUP BY tipo, empresa, multiples ORDER BY filas_distintas DESC, tipo, empresa;

    -- 2) Detalle: cada columna que cambió (máx. 200).
    SELECT a.tipo, a.empresa, a.lote, a.clave, k.col, b.fila -> k.col AS antes, a.fila -> k.col AS despues
      FROM _paridad_ind_sem_actual a
      JOIN _paridad_ind_sem_base b ON b.tipo = a.tipo AND b.lote = a.lote AND b.clave = a.clave
     CROSS JOIN LATERAL jsonb_object_keys(a.fila) AS k(col)
     WHERE a.fila -> k.col IS DISTINCT FROM b.fila -> k.col
     ORDER BY a.tipo, a.empresa, a.lote, a.semana, a.clave, k.col
     LIMIT 200;

    -- 3) Filas que existen de un solo lado.
    SELECT 'solo_antes' AS lado, b.tipo, b.empresa, b.lote, b.clave, b.fila
      FROM _paridad_ind_sem_base b
     WHERE NOT EXISTS (SELECT 1 FROM _paridad_ind_sem_actual a
                        WHERE a.tipo = b.tipo AND a.lote = b.lote AND a.clave = b.clave)
    UNION ALL
    SELECT 'solo_despues', a.tipo, a.empresa, a.lote, a.clave, a.fila
      FROM _paridad_ind_sem_actual a
     WHERE NOT EXISTS (SELECT 1 FROM _paridad_ind_sem_base b
                        WHERE b.tipo = a.tipo AND b.lote = a.lote AND b.clave = a.clave)
     ORDER BY 1, 2, 3, 4, 5
     LIMIT 100;
\endif
