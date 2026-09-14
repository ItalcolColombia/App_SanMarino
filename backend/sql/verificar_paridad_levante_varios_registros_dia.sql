-- ============================================================================
-- GATE MULTIPAÍS — paridad de LEVANTE con varios registros por día (pesaje, uniformidad, CV)
-- Plan: fase_de_desarrollo/levante_varios_registros_dia_pesaje_uniformidad_plan.md (13-sep-2026)
-- Modelo: verificar_paridad_indicadores_semanales.sql. Correr con psql (usa \gset / \if).
--
-- Cubre los cuatro lectores de lo que cambia:
--   • fn_seguimiento_diario_levante          — todos los lotes con seguimiento de levante (clave: día)
--   • sp_recalcular_seguimiento_levante      — su salida en produccion_resultado_levante, calculada en
--                                              un subbloque que se REVIERTE: la tabla queda como estaba.
--                                              Clave = orden de la fila (la columna `fecha` es date y dos
--                                              días Bogotá pueden caer en la misma fecha UTC). Sin `id`.
--   • fn_reporte_semanal_levante_extras      — todos los lotes (clave: semana)
--   • fn_resumen_semanal_ra_pesadas_levante  — todas las empresas × años con levante, todas las semanas
--                                              (clave: año|lote|edad)
--
-- USO:
--   1a corrida (ANTES del cambio): congela la línea base en _paridad_lev_dia_base.
--   2a corrida (DESPUÉS): recalcula en _paridad_lev_dia_actual y compara fila a fila.
--   Re-congelar: DROP TABLE _paridad_lev_dia_base; y volver a correr.
--   ⚠️ Crea tablas de trabajo REALES (sobreviven entre corridas): correr sobre un CLON, no sobre prod.
--
-- Criterio: toda empresa SIN permite_multiples_seguimientos_diarios sale con 0 diferencias en la fn diaria
-- y el SP. Los dos reportes semanales no ramifican por flag: cualquier empresa podría cambiar SOLO en
-- una semana cuyo último día con pesaje tenga 2+ registros que pesaron; cada número se justifica.
-- ============================================================================

SET client_min_messages = warning;

SELECT to_regclass('public._paridad_lev_dia_base') IS NULL AS congelar \gset

DROP TABLE IF EXISTS _paridad_lev_dia_actual;
CREATE TABLE _paridad_lev_dia_actual (
    tipo        text,
    empresa     text,
    company_id  integer,
    multiples   boolean,
    lote        text,
    clave       text,
    fila        jsonb
);

DO $gate$
DECLARE
    r        record;
    v_filas  jsonb;
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
        INSERT INTO _paridad_lev_dia_actual
        SELECT 'diaria', r.name, r.company_id, r.multiples, r.lote_id::text, f.reg_date::text, to_jsonb(f)
          FROM fn_seguimiento_diario_levante(r.lote_id::text) f;

        -- Hace DROP TABLE IF EXISTS de su TEMP antes de crearla: se puede llamar N veces en el DO.
        INSERT INTO _paridad_lev_dia_actual
        SELECT 'extras', r.name, r.company_id, r.multiples, r.lote_id::text, f.semana::text, to_jsonb(f)
          FROM fn_reporte_semanal_levante_extras(r.lote_id) f;

        -- El SP hace DELETE + INSERT en produccion_resultado_levante. Se corre dentro de un subbloque
        -- que termina en una excepción propia (ZZ001) para REVERTIR esas escrituras; la salida sobrevive
        -- en la variable. Un error del SP (p. ej. «Lote no existe») se guarda como fila para compararlo.
        v_filas := NULL;
        BEGIN
            PERFORM sp_recalcular_seguimiento_levante(r.lote_id::text);
            SELECT jsonb_agg(to_jsonb(p) - 'id' ORDER BY p.id) INTO v_filas
              FROM produccion_resultado_levante p
             WHERE p.lote_id = r.lote_id::text;
            RAISE EXCEPTION USING ERRCODE = 'ZZ001', MESSAGE = 'gate: revertir escrituras del SP';
        EXCEPTION
            WHEN SQLSTATE 'ZZ001' THEN NULL;
            WHEN OTHERS THEN v_filas := jsonb_build_array(jsonb_build_object('error', SQLERRM));
        END;

        INSERT INTO _paridad_lev_dia_actual
        SELECT 'sp_recalcular', r.name, r.company_id, r.multiples, r.lote_id::text, e.ord::text, e.fila
          FROM jsonb_array_elements(COALESCE(v_filas, '[]'::jsonb)) WITH ORDINALITY AS e(fila, ord);
    END LOOP;

    -- RA Pesadas es multi-lote: una llamada por empresa × año (el fin de semana puede caer en el año
    -- siguiente al del registro), todas las semanas del año (p_sem_anio NULL).
    FOR r IN
        SELECT c.id AS company_id, c.name,
               COALESCE(c.permite_multiples_seguimientos_diarios, false) AS multiples, y.anio
          FROM companies c
          JOIN LATERAL (
                SELECT DISTINCT gs AS anio
                  FROM seguimiento_diario_levante s
                  JOIN lotes l ON l.lote_id::text = s.lote_id AND l.company_id = c.id
                 CROSS JOIN LATERAL generate_series(EXTRACT(YEAR FROM s.fecha)::int,
                                                    EXTRACT(YEAR FROM s.fecha)::int + 1) gs
                 WHERE s.tipo_seguimiento = 'levante'
          ) y ON true
    LOOP
        INSERT INTO _paridad_lev_dia_actual
        SELECT 'ra_pesadas', r.name, r.company_id, r.multiples, f.lote_id::text,
               concat_ws('|', r.anio, f.lote_id, f.edad_semana), to_jsonb(f)
          FROM fn_resumen_semanal_ra_pesadas_levante(r.company_id, r.anio, NULL, NULL, NULL, false) f;
    END LOOP;
END
$gate$;

\if :congelar
    ALTER TABLE _paridad_lev_dia_actual RENAME TO _paridad_lev_dia_base;
    SELECT 'LINEA BASE CONGELADA' AS estado, tipo, empresa, count(*) AS filas
      FROM _paridad_lev_dia_base GROUP BY tipo, empresa ORDER BY tipo, empresa;
\else
    -- 1) Resumen: filas que difieren (valor distinto, o fila que solo existe de un lado), por empresa.
    WITH b AS (SELECT * FROM _paridad_lev_dia_base),
         a AS (SELECT * FROM _paridad_lev_dia_actual),
         pares AS (
            SELECT COALESCE(a.tipo, b.tipo) AS tipo, COALESCE(a.empresa, b.empresa) AS empresa,
                   COALESCE(a.multiples, b.multiples) AS multiples,
                   (a.fila IS DISTINCT FROM b.fila) AS difiere
              FROM a FULL JOIN b ON b.tipo = a.tipo AND b.lote = a.lote AND b.clave = a.clave)
    SELECT tipo, empresa, multiples, count(*) AS filas, count(*) FILTER (WHERE difiere) AS filas_distintas
      FROM pares GROUP BY tipo, empresa, multiples ORDER BY filas_distintas DESC, tipo, empresa;

    -- 2) Detalle: cada columna que cambió (máx. 200).
    SELECT a.tipo, a.empresa, a.lote, a.clave, k.col, b.fila -> k.col AS antes, a.fila -> k.col AS despues
      FROM _paridad_lev_dia_actual a
      JOIN _paridad_lev_dia_base b ON b.tipo = a.tipo AND b.lote = a.lote AND b.clave = a.clave
     CROSS JOIN LATERAL jsonb_object_keys(a.fila) AS k(col)
     WHERE a.fila -> k.col IS DISTINCT FROM b.fila -> k.col
     ORDER BY a.tipo, a.empresa, a.lote, a.clave, k.col
     LIMIT 200;

    -- 3) Filas que existen de un solo lado.
    SELECT 'solo_antes' AS lado, b.tipo, b.empresa, b.lote, b.clave, b.fila
      FROM _paridad_lev_dia_base b
     WHERE NOT EXISTS (SELECT 1 FROM _paridad_lev_dia_actual a
                        WHERE a.tipo = b.tipo AND a.lote = b.lote AND a.clave = b.clave)
    UNION ALL
    SELECT 'solo_despues', a.tipo, a.empresa, a.lote, a.clave, a.fila
      FROM _paridad_lev_dia_actual a
     WHERE NOT EXISTS (SELECT 1 FROM _paridad_lev_dia_base b
                        WHERE b.tipo = a.tipo AND b.lote = a.lote AND b.clave = a.clave)
     ORDER BY 1, 2, 3, 4, 5
     LIMIT 100;
\endif
