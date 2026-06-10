-- ============================================================================
-- Cruce automático: Seguimiento Diario Reproductora  →  Seguimiento Diario Pollo Engorde
-- ----------------------------------------------------------------------------
-- Durante los primeros 7 días de vida, el seguimiento del lote pollo engorde
-- (tabla CANÓNICA seguimiento_diario_aves_engorde) NO se digita: se genera
-- consolidando los seguimientos de los lotes reproductora asociados.
--
-- Reglas (Excel "requerimiento panama 2"):
--   * Cruce por EDAD de vida: edad = fecha_registro - fecha_encasetamiento (días).
--     El día siguiente al encaset = edad 1. Solo edades 1..7.
--   * Sumas: consumo M/H, mortalidad M/H, selección M/H, error sexaje M/H.
--   * Aves vivas al inicio del día d = aves_inicio - Σ(mort+sel+error) de edades < d.
--   * Peso promedio = PROMEDIO PONDERADO por aves vivas:
--         peso_m = Σ(aves_m_i · peso_m_i) / Σ(aves_m_i)   (idem hembras).
--   * Multi-lote: solo se genera el día d cuando TODOS los lotes reproductora
--     tienen registro de esa edad. Si falta alguno → no se genera (y se borra
--     el cruce previo de esa edad si existía). 1 solo lote → copia directa.
--   * Inventario/aves NO se vuelven a descontar (espejo informativo).
--   * Registros marcados origen_cruce=true → solo lectura, regenerables.
--
-- Idempotente: CREATE OR REPLACE + DROP/CREATE TRIGGER + INDEX IF NOT EXISTS.
-- ============================================================================

-- Índice único parcial: un registro de cruce por (lote, fecha)
CREATE UNIQUE INDEX IF NOT EXISTS ux_seg_engorde_cruce_lote_fecha
    ON seguimiento_diario_aves_engorde (lote_ave_engorde_id, fecha)
    WHERE origen_cruce;

-- ----------------------------------------------------------------------------
-- Función principal: recalcula las edades 1..7 de un lote pollo engorde.
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_cruce_reproductora_a_engorde(p_lote_ave_engorde_id int)
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
    c_max_dias   constant int := 7;
    v_fecha_enc  date;
    v_n_lotes    int;
    d            int;
    r            record;
    v_fecha_dest date;
BEGIN
    -- Fecha de encasetamiento del lote pollo engorde (padre), para fechar el destino.
    SELECT lae.fecha_encaset::date
      INTO v_fecha_enc
      FROM lote_ave_engorde lae
     WHERE lae.lote_ave_engorde_id = p_lote_ave_engorde_id;

    -- Número de lotes reproductora hijos.
    SELECT COUNT(*)
      INTO v_n_lotes
      FROM lote_reproductora_ave_engorde lr
     WHERE lr.lote_ave_engorde_id = p_lote_ave_engorde_id;

    -- Sin lotes reproductora: limpiar cualquier cruce previo y salir.
    IF v_n_lotes = 0 THEN
        DELETE FROM seguimiento_diario_aves_engorde
         WHERE lote_ave_engorde_id = p_lote_ave_engorde_id
           AND origen_cruce;
        RETURN;
    END IF;

    FOR d IN 1..c_max_dias LOOP
        -- Agregados del día d sobre todos los lotes reproductora del padre.
        SELECT
            COUNT(DISTINCT dia.repro_id)                                   AS n_con,
            COALESCE(SUM(dia.aves_m), 0)                                   AS machos,
            COALESCE(SUM(dia.aves_h), 0)                                   AS hembras,
            SUM(dia.consumo_kg_machos)                                     AS consumo_m,
            SUM(dia.consumo_kg_hembras)                                    AS consumo_h,
            SUM(dia.mortalidad_machos)                                     AS mort_m,
            SUM(dia.mortalidad_hembras)                                    AS mort_h,
            SUM(dia.sel_m)                                                 AS sel_m,
            SUM(dia.sel_h)                                                 AS sel_h,
            SUM(dia.error_sexaje_machos)                                   AS err_m,
            SUM(dia.error_sexaje_hembras)                                  AS err_h,
            CASE WHEN SUM(dia.aves_m) > 0
                 THEN SUM(dia.aves_m * dia.peso_prom_machos) / SUM(dia.aves_m)
            END                                                            AS peso_m,
            CASE WHEN SUM(dia.aves_h) > 0
                 THEN SUM(dia.aves_h * dia.peso_prom_hembras) / SUM(dia.aves_h)
            END                                                            AS peso_h,
            MAX(dia.fecha_reg)                                             AS fecha_reg,
            -- Consumo de agua: se toma el valor DEL PRIMER lote reproductora (menor repro_id),
            -- SIN sumar ni promediar (decisión 2026-06-05). Si hay un solo lote, es su propio valor.
            (array_agg(dia.consumo_agua_diario       ORDER BY dia.repro_id))[1]  AS agua_diario,
            (array_agg(dia.consumo_agua_ph           ORDER BY dia.repro_id))[1]  AS agua_ph,
            (array_agg(dia.consumo_agua_orp          ORDER BY dia.repro_id))[1]  AS agua_orp,
            (array_agg(dia.consumo_agua_temperatura  ORDER BY dia.repro_id))[1]  AS agua_temp,
            -- Nombre del alimento: en los reproductora viene "H: <nombre> / M: <nombre>"
            -- (mismo para H y M en los primeros 7 días). Se extrae un único nombre limpio.
            MAX(dia.tipo_alimento)                                         AS tipo_alimento,
            jsonb_agg(dia.repro_id ORDER BY dia.repro_id)                  AS lotes_json
          INTO r
          FROM (
            SELECT
                lr.id AS repro_id,
                -- aves vivas al inicio del día d = inicio - retiros de edades [1, d)
                COALESCE(lr.aves_inicio_machos, lr.m, 0)
                  - COALESCE((
                        SELECT SUM(COALESCE(p.mortalidad_machos,0)
                                 + COALESCE(p.sel_m,0)
                                 + COALESCE(p.error_sexaje_machos,0))
                          FROM seguimiento_diario_lote_reproductora_aves_engorde p
                         WHERE p.lote_reproductora_ave_engorde_id = lr.id
                           AND (p.fecha::date - lr.fecha_encasetamiento::date) >= 1
                           AND (p.fecha::date - lr.fecha_encasetamiento::date) <  d
                    ), 0)                                                  AS aves_m,
                COALESCE(lr.aves_inicio_hembras, lr.h, 0)
                  - COALESCE((
                        SELECT SUM(COALESCE(p.mortalidad_hembras,0)
                                 + COALESCE(p.sel_h,0)
                                 + COALESCE(p.error_sexaje_hembras,0))
                          FROM seguimiento_diario_lote_reproductora_aves_engorde p
                         WHERE p.lote_reproductora_ave_engorde_id = lr.id
                           AND (p.fecha::date - lr.fecha_encasetamiento::date) >= 1
                           AND (p.fecha::date - lr.fecha_encasetamiento::date) <  d
                    ), 0)                                                  AS aves_h,
                s.consumo_kg_machos, s.consumo_kg_hembras,
                s.mortalidad_machos, s.mortalidad_hembras,
                s.sel_m, s.sel_h, s.error_sexaje_machos, s.error_sexaje_hembras,
                s.peso_prom_machos, s.peso_prom_hembras,
                s.consumo_agua_diario, s.consumo_agua_ph,
                s.consumo_agua_orp, s.consumo_agua_temperatura,
                s.tipo_alimento,
                s.fecha::date AS fecha_reg
              FROM lote_reproductora_ave_engorde lr
              JOIN seguimiento_diario_lote_reproductora_aves_engorde s
                ON s.lote_reproductora_ave_engorde_id = lr.id
               AND (s.fecha::date - lr.fecha_encasetamiento::date) = d
             WHERE lr.lote_ave_engorde_id = p_lote_ave_engorde_id
          ) dia;

        -- Borrar cualquier cruce previo de esta edad (se regenera abajo si aplica).
        DELETE FROM seguimiento_diario_aves_engorde
         WHERE lote_ave_engorde_id = p_lote_ave_engorde_id
           AND origen_cruce
           AND (metadata->>'edad')::int = d;

        -- Generar solo si TODOS los lotes reproductora tienen registro de la edad d.
        IF r.n_con = v_n_lotes AND v_n_lotes > 0 THEN
            v_fecha_dest := COALESCE(v_fecha_enc + d, r.fecha_reg);

            INSERT INTO seguimiento_diario_aves_engorde (
                lote_ave_engorde_id, fecha,
                mortalidad_machos, mortalidad_hembras,
                sel_m, sel_h, error_sexaje_machos, error_sexaje_hembras,
                consumo_kg_machos, consumo_kg_hembras,
                peso_prom_machos, peso_prom_hembras,
                consumo_agua_diario, consumo_agua_ph,
                consumo_agua_orp, consumo_agua_temperatura,
                tipo_alimento, ciclo, observaciones,
                metadata, origen_cruce, created_by_user_id, created_at
            ) VALUES (
                p_lote_ave_engorde_id, v_fecha_dest,
                r.mort_m, r.mort_h, r.sel_m, r.sel_h, r.err_m, r.err_h,
                r.consumo_m, r.consumo_h, r.peso_m, r.peso_h,
                r.agua_diario, r.agua_ph, r.agua_orp, r.agua_temp,
                -- Nombre del alimento limpio: "H: <nombre> / M: <nombre>" → "<nombre>".
                -- Si no trae ese formato, se deja el texto tal cual.
                CASE
                    WHEN r.tipo_alimento ~ '^\s*H:.*/\s*M:'
                    THEN btrim(split_part(regexp_replace(r.tipo_alimento, '^\s*H:\s*', ''), ' / M:', 1))
                    ELSE r.tipo_alimento
                END,
                'Normal',
                'Generado automáticamente desde ' || v_n_lotes
                  || ' lote(s) reproductora (día ' || d || ').',
                jsonb_build_object(
                    'origenCruce', true,
                    'edad', d,
                    'lotesReproductora', r.lotes_json
                ),
                true, 'SYSTEM_CRUCE', now()
            );
        END IF;
    END LOOP;
END;
$$;

-- ----------------------------------------------------------------------------
-- Función trigger: resuelve el lote pollo engorde padre y dispara el recálculo.
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION trg_fn_cruce_reproductora_engorde()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_lote_ave int;
BEGIN
    SELECT lr.lote_ave_engorde_id
      INTO v_lote_ave
      FROM lote_reproductora_ave_engorde lr
     WHERE lr.id = COALESCE(NEW.lote_reproductora_ave_engorde_id,
                            OLD.lote_reproductora_ave_engorde_id);

    IF v_lote_ave IS NOT NULL THEN
        PERFORM fn_cruce_reproductora_a_engorde(v_lote_ave);
    END IF;

    RETURN NULL; -- AFTER trigger
END;
$$;

DROP TRIGGER IF EXISTS trg_cruce_reproductora_engorde
    ON seguimiento_diario_lote_reproductora_aves_engorde;

CREATE TRIGGER trg_cruce_reproductora_engorde
    AFTER INSERT OR UPDATE OR DELETE
    ON seguimiento_diario_lote_reproductora_aves_engorde
    FOR EACH ROW
    EXECUTE FUNCTION trg_fn_cruce_reproductora_engorde();
