// Partial de 20260801110000_FnSeguimientoProduccionV2FilasTsdVisibles: SQL verbatim.
// NUEVAS = v2 (sincronizadas con backend/sql/); PREV = versiones v1/090000 para el Down.

namespace ZooSanMarino.Infrastructure.Migrations
{
    public partial class FnSeguimientoProduccionV2FilasTsdVisibles
    {
        private const string FnDiariaV2 = """
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- fn_seguimiento_diario_produccion — grilla diaria CANÓNICA de producción (postura)
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- v2 (2026-08-01) — filas TSD visibles en la rama LPP (migración 20260801110000)
--   Problema: las filas de traslado creadas por TrasladoAvesDesdeSegService nacen con
--   lote_postura_produccion_id NULL (matching por lote_id + fecha, documentado en
--   fn_migracion_seguimiento.sql) ⇒ la rama LPP (filtro por lpp_id) no las devolvía y el
--   traslado hecho desde la pantalla era INVISIBLE en la grilla del LPP.
--   Solución: la rama LPP suma las filas de la tabla canónica con lpp NULL y el MISMO
--   lote base (marcadas con la columna nueva fila_sin_lpp = true). Las 3 fns semanales
--   las EXCLUYEN explícitamente (AND NOT fila_sin_lpp) — su salida no cambia ni un byte:
--   un día solo-traslado no es un «día con registro» para los indicadores (paridad con el
--   comportamiento previo). El saldo tampoco cambia: esas filas traen mort/sel/err = 0 y
--   el movimiento ya entra por movimiento_aves.
-- v1 (2026-08-01) — creación (plan fase_de_desarrollo/seguimiento_produccion_fn_canonica_plan.md)
--
--   Patrón fn_seguimiento_diario_engorde (v13): LANGUAGE sql STABLE a PROPÓSITO — el
--   inlining en CROSS JOIN LATERAL es real (plpgsql = Function Scan, ×2.8 más lento medido
--   en engorde) y plpgsql RETURN QUERY no aplica assignment casts (SUM(int)→bigint vs INT
--   = error 42804). Por eso TODOS los agregados del SELECT final van casteados explícitos.
--
--   ÚNICA FÓRMULA de los números diarios de producción (regla del repo «una sola fórmula
--   por número»): fila diaria cruda + derivados (saldo de aves del día, acumulados de
--   huevos, % postura hen-day diario). El espejo C# de especificación ejecutable es
--   Application/Calculos/SeguimientoDiarioProduccionCalculos.cs (tests xUnit = contrato).
--
--   Decisiones de diseño (D1-D4 del plan, confirmadas):
--   • Universo = días con seguimiento (dedup) ∪ días con movimientos de aves (filas
--     movimiento-only con seg_id NULL, patrón engorde v7: una venta tardía genera su fila
--     y el saldo del lote la refleja).
--   • Fuente dual + dedup por día Bogotá con «gana el timestamp más temprano»: MISMO bloque
--     que fn_indicadores_produccion_postura / fn_clasificacion_huevo_items_produccion /
--     fn_resumen_semanal_ra_pesadas_produccion (el registro puede vivir en la tabla
--     canónica seguimiento_diario_produccion o en la legacy seguimiento_diario_levante
--     con tipo_seguimiento='produccion'; hoy la legacy tiene 0 filas de producción).
--   • SALDO DE AVES (D4): GREATEST(0, base − Σ(mort+sel+ERR) − Σ mov_out + Σ mov_in),
--     CON error de sexaje — semántica de los escritores incrementales
--     (SeguimientoProduccionService.AplicarDescuentoLppAsync y fn_migracion paso 3).
--     base = COALESCE(lpp.aves_h_inicial, lpp.hembras_iniciales_prod, 0) (misma prioridad
--     que ObtenerInformacionLoteAsync). mov_out = movimiento_aves Completado no borrado con
--     el lote como ORIGEN (Venta+Traslado+Retiro, cualquier tipo — igual que el GET);
--     mov_in = tipo Traslado con el lote como DESTINO. lote_postura_produccion.aves_*_actual
--     es DERIVADO verificable, jamás fuente.
--   • FILTRO DE FASE de los movimientos (divergencia DELIBERADA vs el GET viejo): solo se
--     cuentan movimientos con fecha >= lpp.fecha_inicio_produccion (día Bogotá). Los
--     anteriores pertenecen al LEVANTE y ya están reflejados en aves_h_inicial del LPP
--     (las aves iniciales de producción = aves vivas al cierre del levante): contarlos de
--     nuevo los duplicaba. Caso real: lote 130 — el GET viejo daba 8.646 H (restaba otra
--     vez la venta 100 + salida 500 − ingreso 200 del levante); el valor correcto validado
--     por el E2E de carga masiva es 9.039. Con fecha_inicio_produccion NULL no se filtra
--     (comportamiento del GET, conservador).
--   • Semana de vida CRUDA ((fecha − ref)/7)+1 SIN piso 26 ni corte 25: el corte es del
--     consumidor (los indicadores cortan en 25; la clasificación por ítems de Santa Reyes
--     deliberadamente NO corta). ref = COALESCE(lev.fecha_encaset, lpp.fecha_encaset,
--     lpp.fecha_inicio_produccion) en día Bogotá (idéntico a las 3 fns semanales).
--   • Rama LPP filtra por lote_postura_produccion_id (paridad con la grilla y las fns de
--     hoy) ⇒ las filas de traslado TSD con lpp NULL siguen fuera de esta rama (deuda
--     documentada; su efecto en el saldo entra por movimiento_aves, que sí las audita).
--   • Rama legacy (p_lote_id): el C# ya resuelve el lote hijo en fase Produccion; acá solo
--     se listan sus filas. Sin LPP no hay base de aves ⇒ saldos NULL (no 0: GREATEST
--     ignora NULLs, por eso el CASE explícito).
--   • Corte de día SIEMPRE AT TIME ZONE 'America/Bogota'; jamás date_trunc dependiente de
--     la TZ de sesión ni ::date directo sobre timestamptz.
--
--   Consumidores: grilla GET /api/Produccion/seguimiento (SqlQueryRaw, snake_case),
--   informacion-lote (saldo del último día), y las fns semanales re-sourced sobre esta.
--
--   Firma: exactamente UNO de los dos parámetros debe venir no-NULL.
-- ═══════════════════════════════════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION fn_seguimiento_diario_produccion(
    p_lote_postura_produccion_id INT,
    p_lote_id                    INT
)
RETURNS TABLE (
    -- Identificación
    seg_id                      BIGINT,       -- NULL = fila movimiento-only (sin registro diario)
    fecha                       DATE,
    fecha_ts                    TIMESTAMPTZ,  -- timestamp original del registro (NULL en movimiento-only)
    fuente                      TEXT,         -- 'sdp' | 'sdl' (legacy) | 'mov'
    fila_sin_lpp                BOOLEAN,      -- v2: fila TSD del lote base con lpp NULL en rama LPP (las fns semanales la excluyen)
    lote_id                     INT,
    lote_postura_produccion_id  INT,
    company_id                  INT,
    -- Tiempo
    edad_dias                   INT,
    semana                      INT,
    -- Aves crudas del registro
    mortalidad_hembras          INT,
    mortalidad_machos           INT,
    sel_h                       INT,
    sel_m                       INT,
    error_sexaje_hembras        INT,
    error_sexaje_machos         INT,
    -- Consumo
    cons_kg_h                   DOUBLE PRECISION,
    cons_kg_m                   DOUBLE PRECISION,
    consumo_total_kg            DOUBLE PRECISION,
    tipo_alimento               TEXT,
    -- Huevos crudos
    huevo_tot                   INT,
    huevo_inc                   INT,
    huevo_limpio                INT,
    huevo_tratado               INT,
    huevo_sucio                 INT,
    huevo_deforme               INT,
    huevo_blanco                INT,
    huevo_doble_yema            INT,
    huevo_piso                  INT,
    huevo_pequeno               INT,
    huevo_roto                  INT,
    huevo_desecho               INT,
    huevo_otro                  INT,
    peso_huevo                  DOUBLE PRECISION,
    -- Derivados de huevos
    huevo_tot_acum              BIGINT,
    huevo_inc_acum              BIGINT,
    pct_postura_dia             DOUBLE PRECISION,  -- hen-day diario: huevo_tot / aves_h_inicio_dia * 100
    -- Movimientos de aves del día (desde movimiento_aves Completado)
    mov_venta_h                 INT,
    mov_venta_m                 INT,
    mov_retiro_h                INT,
    mov_retiro_m                INT,
    mov_traslado_in_h           INT,
    mov_traslado_in_m           INT,
    mov_traslado_out_h          INT,
    mov_traslado_out_m          INT,
    -- Saldo de aves (D4: con error de sexaje; NULL en rama legacy sin LPP)
    aves_h_inicio_dia           INT,
    aves_m_inicio_dia           INT,
    saldo_aves_h                INT,
    saldo_aves_m                INT,
    -- Traslado crudo de la fila diaria
    es_traslado                 BOOLEAN,
    traslado_direccion          TEXT,
    traslado_ingreso_hembras    INT,
    traslado_ingreso_machos     INT,
    traslado_salida_hembras     INT,
    traslado_salida_machos      INT,
    lote_destino_id             INT,
    granja_destino_id           INT,
    -- Pesaje (peso_h/m, uniformidad y CV del lote son NUMERIC — mismos tipos que la tabla,
    -- para que el C# los lea como decimal EXACTO, sin pasar por float8)
    peso_h                      NUMERIC,
    peso_m                      NUMERIC,
    uniformidad                 NUMERIC,
    coeficiente_variacion       NUMERIC,
    uniformidad_hembras         DOUBLE PRECISION,
    uniformidad_machos          DOUBLE PRECISION,
    cv_hembras                  DOUBLE PRECISION,
    cv_machos                   DOUBLE PRECISION,
    observaciones_pesaje        TEXT,
    -- Agua
    consumo_agua_diario         DOUBLE PRECISION,
    consumo_agua_ph             DOUBLE PRECISION,
    consumo_agua_orp            DOUBLE PRECISION,
    consumo_agua_temperatura    DOUBLE PRECISION,
    -- Otros
    etapa                       INT,
    ciclo                       TEXT,
    observaciones               TEXT,
    metadata                    JSONB,
    created_by_user_id          INT,
    created_at                  TIMESTAMPTZ,
    updated_at                  TIMESTAMPTZ
)
LANGUAGE sql STABLE
AS $$
WITH ctx AS (
    -- ── Rama LPP: base de aves + fecha de referencia (idéntica a fn_indicadores) ──
    SELECT lpp.lote_postura_produccion_id                            AS ctx_lpp_id,
           lpp.lote_id                                               AS ctx_lote_id,
           lpp.company_id                                            AS ctx_company,
           COALESCE(lpp.aves_h_inicial, lpp.hembras_iniciales_prod, 0) AS base_h,
           COALESCE(lpp.aves_m_inicial, lpp.machos_iniciales_prod, 0)  AS base_m,
           (COALESCE(lev.fecha_encaset, lpp.fecha_encaset, lpp.fecha_inicio_produccion)
               AT TIME ZONE 'America/Bogota')::date                  AS ref_date,
           (lpp.fecha_inicio_produccion
               AT TIME ZONE 'America/Bogota')::date                  AS mov_desde,
           true                                                      AS es_lpp
      FROM lote_postura_produccion lpp
      LEFT JOIN lote_postura_levante lev
             ON lev.lote_postura_levante_id = lpp.lote_postura_levante_id
            AND lev.deleted_at IS NULL
     WHERE p_lote_postura_produccion_id IS NOT NULL
       AND lpp.lote_postura_produccion_id = p_lote_postura_produccion_id
       AND lpp.deleted_at IS NULL
    UNION ALL
    -- ── Rama legacy: p_lote_id ya resuelto por el C# (lote hijo en fase Produccion o lote
    --    crudo). LEFT JOIN para no perder filas huérfanas cuyo lote no existe en lotes
    --    (paridad con la grilla actual, que igual las devuelve). Sin base de aves.
    SELECT NULL::int, p_lote_id, lo.company_id,
           NULL::int, NULL::int,
           (COALESCE(lo.fecha_inicio_produccion, pa.fecha_encaset, lo.fecha_encaset)
               AT TIME ZONE 'America/Bogota')::date,
           NULL::date,
           false
      FROM (SELECT 1) uno
      LEFT JOIN lotes lo ON lo.lote_id = p_lote_id AND lo.deleted_at IS NULL
      LEFT JOIN lotes pa ON pa.lote_id = lo.lote_padre_id AND pa.deleted_at IS NULL
     WHERE p_lote_postura_produccion_id IS NULL
       AND p_lote_id IS NOT NULL
),
-- ── Fuente dual + dedup por día Bogotá (bloque canónico de las fns semanales) ──
crudos AS (
    SELECT sd.id::bigint                                  AS c_seg_id,
           'sdl'::text                                    AS c_fuente,
           sd.fecha                                       AS c_ts,
           COALESCE(sd.mortalidad_hembras, 0)             AS c_mort_h,
           COALESCE(sd.mortalidad_machos, 0)              AS c_mort_m,
           COALESCE(sd.sel_h, 0)                          AS c_sel_h,
           COALESCE(sd.sel_m, 0)                          AS c_sel_m,
           COALESCE(sd.error_sexaje_hembras, 0)           AS c_err_h,
           COALESCE(sd.error_sexaje_machos, 0)            AS c_err_m,
           COALESCE(sd.consumo_kg_hembras, 0)::float8     AS c_cons_h,
           COALESCE(sd.consumo_kg_machos, 0)::float8      AS c_cons_m,
           sd.tipo_alimento::text                         AS c_tipo_alimento,
           COALESCE(sd.huevo_tot, 0)                      AS c_huevo_tot,
           COALESCE(sd.huevo_inc, 0)                      AS c_huevo_inc,
           COALESCE(sd.huevo_limpio, 0)                   AS c_h_limpio,
           COALESCE(sd.huevo_tratado, 0)                  AS c_h_tratado,
           COALESCE(sd.huevo_sucio, 0)                    AS c_h_sucio,
           COALESCE(sd.huevo_deforme, 0)                  AS c_h_deforme,
           COALESCE(sd.huevo_blanco, 0)                   AS c_h_blanco,
           COALESCE(sd.huevo_doble_yema, 0)               AS c_h_doble,
           COALESCE(sd.huevo_piso, 0)                     AS c_h_piso,
           COALESCE(sd.huevo_pequeno, 0)                  AS c_h_pequeno,
           COALESCE(sd.huevo_roto, 0)                     AS c_h_roto,
           COALESCE(sd.huevo_desecho, 0)                  AS c_h_desecho,
           COALESCE(sd.huevo_otro, 0)                     AS c_h_otro,
           sd.peso_huevo::float8                          AS c_peso_huevo,
           sd.es_traslado                                 AS c_es_traslado,
           sd.traslado_direccion::text                    AS c_tras_dir,
           COALESCE(sd.traslado_ingreso_hembras, 0)       AS c_tras_in_h,
           COALESCE(sd.traslado_ingreso_machos, 0)        AS c_tras_in_m,
           COALESCE(sd.traslado_salida_hembras, 0)        AS c_tras_out_h,
           COALESCE(sd.traslado_salida_machos, 0)         AS c_tras_out_m,
           NULL::int                                      AS c_lote_destino_id,
           NULL::int                                      AS c_granja_destino_id,
           sd.peso_h                                      AS c_peso_h,
           sd.peso_m                                      AS c_peso_m,
           sd.uniformidad                                 AS c_unif,
           sd.coeficiente_variacion                       AS c_cv,
           sd.uniformidad_hembras::float8                 AS c_unif_h,
           sd.uniformidad_machos::float8                  AS c_unif_m,
           sd.cv_hembras::float8                          AS c_cv_h,
           sd.cv_machos::float8                           AS c_cv_m,
           sd.observaciones_pesaje                        AS c_obs_pesaje,
           sd.consumo_agua_diario                         AS c_agua,
           sd.consumo_agua_ph                             AS c_agua_ph,
           sd.consumo_agua_orp                            AS c_agua_orp,
           sd.consumo_agua_temperatura                    AS c_agua_temp,
           sd.etapa                                       AS c_etapa,
           sd.ciclo::text                                 AS c_ciclo,
           sd.observaciones                               AS c_observaciones,
           sd.metadata                                    AS c_metadata,
           NULL::int                                      AS c_created_by,  -- legacy: varchar, no casteable
           sd.created_at                                  AS c_created_at,
           sd.updated_at                                  AS c_updated_at,
           NULL::int                                      AS c_company_id,
           sd.lote_postura_produccion_id                  AS c_lpp
      FROM seguimiento_diario_levante sd
     WHERE sd.tipo_seguimiento = 'produccion'
       AND ( (p_lote_postura_produccion_id IS NOT NULL
                AND sd.lote_postura_produccion_id = p_lote_postura_produccion_id)
          OR (p_lote_postura_produccion_id IS NULL
                AND sd.lote_id = p_lote_id::text) )
    UNION ALL
    SELECT sp.id::bigint,
           'sdp'::text,
           sp.fecha_registro,
           COALESCE(sp.mortalidad_hembras, 0),
           COALESCE(sp.mortalidad_machos, 0),
           COALESCE(sp.sel_h, 0),
           COALESCE(sp.sel_m, 0),
           COALESCE(sp.error_sexaje_hembras, 0),
           COALESCE(sp.error_sexaje_machos, 0),
           COALESCE(sp.cons_kg_h, 0)::float8,
           COALESCE(sp.cons_kg_m, 0)::float8,
           sp.tipo_alimento,
           COALESCE(sp.huevo_tot, 0),
           COALESCE(sp.huevo_inc, 0),
           COALESCE(sp.huevo_limpio, 0),
           COALESCE(sp.huevo_tratado, 0),
           COALESCE(sp.huevo_sucio, 0),
           COALESCE(sp.huevo_deforme, 0),
           COALESCE(sp.huevo_blanco, 0),
           COALESCE(sp.huevo_doble_yema, 0),
           COALESCE(sp.huevo_piso, 0),
           COALESCE(sp.huevo_pequeno, 0),
           COALESCE(sp.huevo_roto, 0),
           COALESCE(sp.huevo_desecho, 0),
           COALESCE(sp.huevo_otro, 0),
           sp.peso_huevo,
           sp.es_traslado,
           sp.traslado_direccion::text,
           COALESCE(sp.traslado_ingreso_hembras, 0),
           COALESCE(sp.traslado_ingreso_machos, 0),
           COALESCE(sp.traslado_salida_hembras, 0),
           COALESCE(sp.traslado_salida_machos, 0),
           sp.lote_destino_id,
           sp.granja_destino_id,
           sp.peso_h,
           sp.peso_m,
           sp.uniformidad,
           sp.coeficiente_variacion,
           sp.uniformidad_hembras,
           sp.uniformidad_machos,
           sp.cv_hembras,
           sp.cv_machos,
           sp.observaciones_pesaje,
           sp.consumo_agua_diario,
           sp.consumo_agua_ph,
           sp.consumo_agua_orp,
           sp.consumo_agua_temperatura,
           sp.etapa,
           sp.ciclo::text,
           sp.observaciones,
           sp.metadata,
           sp.created_by_user_id,
           sp.created_at,
           sp.updated_at,
           sp.company_id,
           sp.lote_postura_produccion_id
      FROM seguimiento_diario_produccion sp
     WHERE ( (p_lote_postura_produccion_id IS NOT NULL
                AND sp.lote_postura_produccion_id = p_lote_postura_produccion_id)
          -- v2: filas TSD del MISMO lote base con lpp NULL (traslados desde la pantalla de
          -- seguimiento no setean la FK; matching por lote crudo, igual que fn_migracion)
          OR (p_lote_postura_produccion_id IS NOT NULL
                AND sp.lote_postura_produccion_id IS NULL
                AND sp.lote_id IN (SELECT c2.ctx_lote_id FROM ctx c2
                                    WHERE c2.es_lpp AND c2.ctx_lote_id IS NOT NULL))
          OR (p_lote_postura_produccion_id IS NULL
                AND sp.lote_id = p_lote_id) )
),
seg_dias AS (
    SELECT DISTINCT ON ((c.c_ts AT TIME ZONE 'America/Bogota')::date)
           c.*,
           (c.c_ts AT TIME ZONE 'America/Bogota')::date AS reg_date
      FROM crudos c
     ORDER BY (c.c_ts AT TIME ZONE 'America/Bogota')::date, c.c_ts
),
-- ── Movimientos de aves (solo rama LPP con lote base) — misma población que el GET
--    informacion-lote: Completado, no borrado, misma empresa; salidas = CUALQUIER tipo con
--    el lote como origen; entradas = tipo Traslado con el lote como destino. ──
movs AS (
    SELECT (m.fecha_movimiento AT TIME ZONE 'America/Bogota')::date AS mov_date,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id
                THEN COALESCE(m.cantidad_hembras, 0) ELSE 0 END AS out_h,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id
                THEN COALESCE(m.cantidad_machos, 0) ELSE 0 END AS out_m,
           CASE WHEN m.tipo_movimiento = 'Traslado' AND m.lote_destino_id = c.ctx_lote_id
                THEN COALESCE(m.cantidad_hembras, 0) ELSE 0 END AS in_h,
           CASE WHEN m.tipo_movimiento = 'Traslado' AND m.lote_destino_id = c.ctx_lote_id
                THEN COALESCE(m.cantidad_machos, 0) ELSE 0 END AS in_m,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Venta'
                THEN COALESCE(m.cantidad_hembras, 0) ELSE 0 END AS venta_h,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Venta'
                THEN COALESCE(m.cantidad_machos, 0) ELSE 0 END AS venta_m,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Retiro'
                THEN COALESCE(m.cantidad_hembras, 0) ELSE 0 END AS retiro_h,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Retiro'
                THEN COALESCE(m.cantidad_machos, 0) ELSE 0 END AS retiro_m,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Traslado'
                THEN COALESCE(m.cantidad_hembras, 0) ELSE 0 END AS tout_h,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Traslado'
                THEN COALESCE(m.cantidad_machos, 0) ELSE 0 END AS tout_m
      FROM movimiento_aves m
      JOIN ctx c ON c.es_lpp AND c.ctx_lote_id IS NOT NULL
     WHERE m.estado = 'Completado'
       AND m.deleted_at IS NULL
       AND m.company_id = c.ctx_company
       AND (m.lote_origen_id = c.ctx_lote_id OR m.lote_destino_id = c.ctx_lote_id)
       -- Filtro de FASE: los movimientos previos al inicio de producción son del levante y
       -- ya viven en aves_h_inicial (ver changelog v1)
       AND (c.mov_desde IS NULL
            OR (m.fecha_movimiento AT TIME ZONE 'America/Bogota')::date >= c.mov_desde)
),
movs_dia AS (
    SELECT mv.mov_date,
           SUM(mv.out_h)::int    AS out_h,
           SUM(mv.out_m)::int    AS out_m,
           SUM(mv.in_h)::int     AS in_h,
           SUM(mv.in_m)::int     AS in_m,
           SUM(mv.venta_h)::int  AS venta_h,
           SUM(mv.venta_m)::int  AS venta_m,
           SUM(mv.retiro_h)::int AS retiro_h,
           SUM(mv.retiro_m)::int AS retiro_m,
           SUM(mv.tout_h)::int   AS tout_h,
           SUM(mv.tout_m)::int   AS tout_m
      FROM movs mv
     GROUP BY mv.mov_date
),
-- ── Universo: días con seguimiento ∪ días solo-movimiento (FULL JOIN por día) ──
universo AS (
    SELECT COALESCE(s.reg_date, md.mov_date) AS u_fecha,
           s.*,
           md.out_h    AS m_out_h,
           md.out_m    AS m_out_m,
           md.in_h     AS m_in_h,
           md.in_m     AS m_in_m,
           md.venta_h  AS m_venta_h,
           md.venta_m  AS m_venta_m,
           md.retiro_h AS m_retiro_h,
           md.retiro_m AS m_retiro_m,
           md.tout_h   AS m_tout_h,
           md.tout_m   AS m_tout_m
      FROM seg_dias s
      FULL OUTER JOIN movs_dia md ON md.mov_date = s.reg_date
)
SELECT
    u.c_seg_id                                                        AS seg_id,
    u.u_fecha                                                         AS fecha,
    u.c_ts                                                            AS fecha_ts,
    COALESCE(u.c_fuente, 'mov')                                       AS fuente,
    (u.c_seg_id IS NOT NULL AND u.c_lpp IS NULL AND c.es_lpp)         AS fila_sin_lpp,
    c.ctx_lote_id                                                     AS lote_id,
    COALESCE(u.c_lpp, c.ctx_lpp_id)                                   AS lote_postura_produccion_id,
    COALESCE(u.c_company_id, c.ctx_company)                           AS company_id,
    CASE WHEN c.ref_date IS NULL THEN NULL
         ELSE GREATEST(0, u.u_fecha - c.ref_date) END::int            AS edad_dias,
    CASE WHEN c.ref_date IS NULL THEN NULL
         ELSE ((u.u_fecha - c.ref_date) / 7) + 1 END::int             AS semana,
    u.c_mort_h                                                        AS mortalidad_hembras,
    u.c_mort_m                                                        AS mortalidad_machos,
    u.c_sel_h                                                         AS sel_h,
    u.c_sel_m                                                         AS sel_m,
    u.c_err_h                                                         AS error_sexaje_hembras,
    u.c_err_m                                                         AS error_sexaje_machos,
    u.c_cons_h                                                        AS cons_kg_h,
    u.c_cons_m                                                        AS cons_kg_m,
    (COALESCE(u.c_cons_h, 0) + COALESCE(u.c_cons_m, 0))::float8       AS consumo_total_kg,
    u.c_tipo_alimento                                                 AS tipo_alimento,
    u.c_huevo_tot                                                     AS huevo_tot,
    u.c_huevo_inc                                                     AS huevo_inc,
    u.c_h_limpio                                                      AS huevo_limpio,
    u.c_h_tratado                                                     AS huevo_tratado,
    u.c_h_sucio                                                       AS huevo_sucio,
    u.c_h_deforme                                                     AS huevo_deforme,
    u.c_h_blanco                                                      AS huevo_blanco,
    u.c_h_doble                                                       AS huevo_doble_yema,
    u.c_h_piso                                                        AS huevo_piso,
    u.c_h_pequeno                                                     AS huevo_pequeno,
    u.c_h_roto                                                        AS huevo_roto,
    u.c_h_desecho                                                     AS huevo_desecho,
    u.c_h_otro                                                        AS huevo_otro,
    u.c_peso_huevo                                                    AS peso_huevo,
    SUM(COALESCE(u.c_huevo_tot, 0)) OVER w_ord::bigint                AS huevo_tot_acum,
    SUM(COALESCE(u.c_huevo_inc, 0)) OVER w_ord::bigint                AS huevo_inc_acum,
    CASE
        WHEN c.base_h IS NULL THEN NULL
        WHEN GREATEST(0, c.base_h
                - COALESCE(SUM(u.c_mort_h + u.c_sel_h + u.c_err_h) OVER w_prev, 0)
                - COALESCE(SUM(COALESCE(u.m_out_h, 0)) OVER w_prev, 0)
                + COALESCE(SUM(COALESCE(u.m_in_h, 0)) OVER w_prev, 0)) > 0
            THEN (100.0 * COALESCE(u.c_huevo_tot, 0)
                / GREATEST(0, c.base_h
                    - COALESCE(SUM(u.c_mort_h + u.c_sel_h + u.c_err_h) OVER w_prev, 0)
                    - COALESCE(SUM(COALESCE(u.m_out_h, 0)) OVER w_prev, 0)
                    + COALESCE(SUM(COALESCE(u.m_in_h, 0)) OVER w_prev, 0)))
        ELSE 0
    END::float8                                                       AS pct_postura_dia,
    COALESCE(u.m_venta_h, 0)                                          AS mov_venta_h,
    COALESCE(u.m_venta_m, 0)                                          AS mov_venta_m,
    COALESCE(u.m_retiro_h, 0)                                         AS mov_retiro_h,
    COALESCE(u.m_retiro_m, 0)                                         AS mov_retiro_m,
    COALESCE(u.m_in_h, 0)                                             AS mov_traslado_in_h,
    COALESCE(u.m_in_m, 0)                                             AS mov_traslado_in_m,
    COALESCE(u.m_tout_h, 0)                                           AS mov_traslado_out_h,
    COALESCE(u.m_tout_m, 0)                                           AS mov_traslado_out_m,
    CASE WHEN c.base_h IS NULL THEN NULL
         ELSE GREATEST(0, c.base_h
                - COALESCE(SUM(u.c_mort_h + u.c_sel_h + u.c_err_h) OVER w_prev, 0)
                - COALESCE(SUM(COALESCE(u.m_out_h, 0)) OVER w_prev, 0)
                + COALESCE(SUM(COALESCE(u.m_in_h, 0)) OVER w_prev, 0)) END::int AS aves_h_inicio_dia,
    CASE WHEN c.base_m IS NULL THEN NULL
         ELSE GREATEST(0, c.base_m
                - COALESCE(SUM(u.c_mort_m + u.c_sel_m + u.c_err_m) OVER w_prev, 0)
                - COALESCE(SUM(COALESCE(u.m_out_m, 0)) OVER w_prev, 0)
                + COALESCE(SUM(COALESCE(u.m_in_m, 0)) OVER w_prev, 0)) END::int AS aves_m_inicio_dia,
    CASE WHEN c.base_h IS NULL THEN NULL
         ELSE GREATEST(0, c.base_h
                - COALESCE(SUM(u.c_mort_h + u.c_sel_h + u.c_err_h) OVER w_ord, 0)
                - COALESCE(SUM(COALESCE(u.m_out_h, 0)) OVER w_ord, 0)
                + COALESCE(SUM(COALESCE(u.m_in_h, 0)) OVER w_ord, 0)) END::int  AS saldo_aves_h,
    CASE WHEN c.base_m IS NULL THEN NULL
         ELSE GREATEST(0, c.base_m
                - COALESCE(SUM(u.c_mort_m + u.c_sel_m + u.c_err_m) OVER w_ord, 0)
                - COALESCE(SUM(COALESCE(u.m_out_m, 0)) OVER w_ord, 0)
                + COALESCE(SUM(COALESCE(u.m_in_m, 0)) OVER w_ord, 0)) END::int  AS saldo_aves_m,
    COALESCE(u.c_es_traslado, false)                                  AS es_traslado,
    u.c_tras_dir                                                      AS traslado_direccion,
    u.c_tras_in_h                                                     AS traslado_ingreso_hembras,
    u.c_tras_in_m                                                     AS traslado_ingreso_machos,
    u.c_tras_out_h                                                    AS traslado_salida_hembras,
    u.c_tras_out_m                                                    AS traslado_salida_machos,
    u.c_lote_destino_id                                               AS lote_destino_id,
    u.c_granja_destino_id                                             AS granja_destino_id,
    u.c_peso_h                                                        AS peso_h,
    u.c_peso_m                                                        AS peso_m,
    u.c_unif                                                          AS uniformidad,
    u.c_cv                                                            AS coeficiente_variacion,
    u.c_unif_h                                                        AS uniformidad_hembras,
    u.c_unif_m                                                        AS uniformidad_machos,
    u.c_cv_h                                                          AS cv_hembras,
    u.c_cv_m                                                          AS cv_machos,
    u.c_obs_pesaje                                                    AS observaciones_pesaje,
    u.c_agua                                                          AS consumo_agua_diario,
    u.c_agua_ph                                                       AS consumo_agua_ph,
    u.c_agua_orp                                                      AS consumo_agua_orp,
    u.c_agua_temp                                                     AS consumo_agua_temperatura,
    u.c_etapa                                                         AS etapa,
    u.c_ciclo                                                         AS ciclo,
    u.c_observaciones                                                 AS observaciones,
    u.c_metadata                                                      AS metadata,
    u.c_created_by                                                    AS created_by_user_id,
    u.c_created_at                                                    AS created_at,
    u.c_updated_at                                                    AS updated_at
FROM universo u
CROSS JOIN ctx c
WINDOW
    w_ord  AS (ORDER BY u.u_fecha, COALESCE(u.c_seg_id, 0)
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW),
    w_prev AS (ORDER BY u.u_fecha, COALESCE(u.c_seg_id, 0)
               ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING)
ORDER BY u.u_fecha, COALESCE(u.c_seg_id, 0);
$$;
""";

        private const string FnIndicadoresV2 = """
-- ============================================================================
-- fn_indicadores_produccion_postura(...)
-- Indicadores semanales de PRODUCCIÓN (postura) calculados en la BD.
-- Reemplaza el cómputo en memoria de IndicadoresProduccionService.CalcularIndicadoresAsync
-- (C#, 727 líneas): el servicio ahora resuelve company/lote y DELEGA el cálculo aquí.
--
-- Replica EXACTO el algoritmo C# actual (que ya incorpora las correcciones REQ-004 de
-- comparación vs guía) usando double precision y el mismo orden de operaciones.
--
-- Correcciones de guía YA presentes en el C# y replicadas aquí (documentadas):
--   REQ-004a  %Producción (hen-day) = promedioHuevos/día / HEMBRAS vivas * 100
--             (solo hembras en el denominador; los machos no ponen).
--   REQ-004b  Peso de aves normalizado a kg: >100 ? /1000 (los pesajes vienen en gramos)
--             para casar con la guía (peso_h/1000).
--   REQ-004c  H.T.A.A / H.I.A.A reales (acumulados por ave alojada) se comparan contra
--             h_total_aa / h_inc_aa de la guía (que son acumulados), no contra huevos/día.
--   REQ-004d  Mortalidad de guía es % (decimal), no entero (no se trunca a 0).
--   REQ-004e  (Verenice rev 6-jul-26) La tabla "% Retiro (Real vs Guía)" del front mostraba el
--             REAL pero la GUÍA quedaba vacía: la fn calculaba retiro_ac_h/m REAL pero nunca
--             exponía la guía. Se agregan retiro_ac_h_guia/retiro_ac_m_guia leyendo
--             guia_genetica_sanmarino_colombia.retiro_ac_h/retiro_ac_m (mismo parseo NULLIF/btrim
--             que las demás columnas guía; NULL si no hay guía para la semana).
--   Guía = tabla real guia_genetica_sanmarino_colombia filtrada por company + raza + año
--          (misma tabla que ProduccionAvicolaRaw); indexada por Edad = SEMANA DE VIDA.
--
-- Desviaciones preservadas (NO son bugs de guía → se replican tal cual, ver spec §3):
--   * aves_hembras_inicio_semana = avesHActuales + mortH + selH (sobrecuenta el censo de
--     inicio respecto al saldo real de arranque). Campo informativo; NO afecta comparación.
--   * consumo_real_h/m divide por ese aves_*_inicio_semana sobrecontado.
--   * %mortalidad / %selección usan avesHActuales (saldo real de inicio), no el sobrecontado.
--
-- Timezone: America/Bogota para el corte de semanas. Con Npgsql.EnableLegacyTimestampBehavior
--   =true el back lee timestamptz como hora local del proceso; en dev/local el TZ es UTC-5
--   (= America/Bogota sin DST) → .Date del C# = fecha Bogotá. Se normaliza a Bogotá aquí.
--
-- Fuente de verdad: IndicadoresProduccionService.cs (ObtenerIndicadoresSemanalesAsync/CalcularIndicadoresAsync).
-- ============================================================================

-- ── Helper: diferencia porcentual (== CalcularDiferenciaPorcentual del C#).
--    NULL si falta real/guía o guía = 0.
CREATE OR REPLACE FUNCTION fn_dif_pct(p_real double precision, p_guia double precision)
RETURNS double precision LANGUAGE sql IMMUTABLE AS $$
    SELECT CASE
        WHEN p_real IS NULL OR p_guia IS NULL OR p_guia = 0 THEN NULL
        ELSE ((p_real - p_guia) / p_guia) * 100
    END;
$$;

-- ── Helper: parseo de edad numérica de la guía (== TryParseEdadNumerica del C#).
--    Intenta parsear a entero (coma->punto); si no, extrae el primer grupo de dígitos.
--    Devuelve NULL si no hay dígitos. (Edades de producción son enteros: 26, 27, …)
CREATE OR REPLACE FUNCTION fn_parse_edad_numerica(p_edad text)
RETURNS integer LANGUAGE plpgsql IMMUTABLE AS $$
DECLARE
    v_clean text;
    v_match text;
BEGIN
    IF p_edad IS NULL OR btrim(p_edad) = '' THEN RETURN NULL; END IF;
    v_clean := replace(btrim(p_edad), ',', '.');
    IF v_clean ~ '^[+-]?\d+$' THEN
        RETURN v_clean::integer;
    END IF;
    v_match := (regexp_match(v_clean, '(\d+)'))[1];
    IF v_match IS NULL THEN RETURN NULL; END IF;
    RETURN v_match::integer;
END;
$$;

-- REQ-004 cambia la firma de RETURNS TABLE (agrega retiro_sem_h/m + retiro_ac_h/m). Postgres NO
-- permite CREATE OR REPLACE cuando cambia el row type de los parámetros OUT → DROP idempotente antes.
DROP FUNCTION IF EXISTS fn_indicadores_produccion_postura(integer, integer, integer, integer, integer, date, date);

CREATE OR REPLACE FUNCTION fn_indicadores_produccion_postura(
    p_company_id                  integer,
    p_lote_postura_produccion_id  integer  DEFAULT NULL,
    p_lote_id                     integer  DEFAULT NULL,
    p_semana_desde                integer  DEFAULT NULL,
    p_semana_hasta                integer  DEFAULT NULL,
    p_fecha_desde                 date     DEFAULT NULL,
    p_fecha_hasta                 date     DEFAULT NULL
)
RETURNS TABLE(
    semana                              integer,
    fecha_inicio_semana                 date,
    fecha_fin_semana                    date,
    total_registros                     integer,
    mortalidad_hembras                  integer,
    mortalidad_machos                   integer,
    porcentaje_mortalidad_hembras       double precision,
    porcentaje_mortalidad_machos        double precision,
    mortalidad_guia_hembras             double precision,
    mortalidad_guia_machos              double precision,
    diferencia_mortalidad_hembras       double precision,
    diferencia_mortalidad_machos        double precision,
    seleccion_hembras                   integer,
    porcentaje_seleccion_hembras        double precision,
    consumo_kg_hembras                  double precision,
    consumo_kg_machos                   double precision,
    consumo_total_kg                    double precision,
    consumo_promedio_diario_kg          double precision,
    consumo_guia_hembras                double precision,
    consumo_guia_machos                 double precision,
    diferencia_consumo_hembras          double precision,
    diferencia_consumo_machos           double precision,
    huevos_totales                      integer,
    huevos_incubables                   integer,
    promedio_huevos_por_dia             double precision,
    eficiencia_produccion               double precision,
    huevos_totales_guia                 double precision,
    huevos_incubables_guia              double precision,
    porcentaje_produccion_guia          double precision,
    diferencia_huevos_totales           double precision,
    diferencia_huevos_incubables        double precision,
    diferencia_porcentaje_produccion    double precision,
    peso_huevo_promedio                 double precision,
    peso_huevo_guia                     double precision,
    diferencia_peso_huevo               double precision,
    peso_promedio_hembras               double precision,
    peso_promedio_machos                double precision,
    peso_guia_hembras                   double precision,
    peso_guia_machos                    double precision,
    diferencia_peso_hembras             double precision,
    diferencia_peso_machos              double precision,
    uniformidad_promedio                double precision,
    uniformidad_guia                    double precision,
    diferencia_uniformidad              double precision,
    coeficiente_variacion_promedio      double precision,
    huevos_limpios                      integer,
    huevos_tratados                     integer,
    huevos_sucios                       integer,
    huevos_deformes                     integer,
    huevos_blancos                      integer,
    huevos_doble_yema                   integer,
    huevos_piso                         integer,
    huevos_pequenos                     integer,
    huevos_rotos                        integer,
    huevos_desecho                      integer,
    huevos_otro                         integer,
    aves_hembras_inicio_semana          integer,
    aves_machos_inicio_semana           integer,
    aves_hembras_fin_semana             integer,
    aves_machos_fin_semana              integer,
    htaa_real                           double precision,
    hiaa_real                           double precision,
    -- REQ-004: %Retiro REAL por sexo (mortalidad + selección). Semanal sobre saldo de inicio del
    --   sexo; acumulado sobre aves iniciales del sexo. Aritmética == ProduccionCalculos.PorcentajeRetiro*.
    retiro_sem_h                        double precision,
    retiro_sem_m                        double precision,
    retiro_ac_h                         double precision,
    retiro_ac_m                         double precision,
    -- REQ-004 (Verenice rev 6-jul-26): %Retiro acumulado de GUÍA por sexo, desde
    --   guia_genetica_sanmarino_colombia.retiro_ac_h/retiro_ac_m (texto, mismo parseo que las
    --   demás columnas guía: NULLIF(btrim(...),'')::double precision). NULL si no hay guía para
    --   esa semana (g_found=false); si hay guía pero el campo viene vacío, 0 (mismo criterio que
    --   g_mort_h/g_mort_m, no el de huevos/%prod que preservan NULL).
    retiro_ac_h_guia                    double precision,
    retiro_ac_m_guia                    double precision
)
LANGUAGE plpgsql VOLATILE AS $fn$
DECLARE
    -- ── contexto del lote resuelto ──
    v_enc_date       date;            -- fechaEncaset.Date (Bogotá)
    v_aves_h_ini     integer;
    v_aves_m_ini     integer;
    v_raza           text;
    v_ano            text;            -- ano_tabla_genetica::text
    v_lote_id_str    text;            -- para el flujo legacy (lote_id como texto)
    v_lote_id_int    integer;         -- flujo legacy: lote resuelto, para fn_seguimiento_diario_produccion
    v_has_lote       boolean := false;

    -- ── acumuladores iterativos (mismos que el C#) ──
    v_aves_h_act     integer;
    v_aves_m_act     integer;
    v_cum_h_tot      bigint := 0;
    v_cum_h_inc      bigint := 0;
    -- REQ-004: acumulados de retiro por sexo (mortalidad + selección)
    v_cum_mort_h     bigint := 0;
    v_cum_sel_h      bigint := 0;
    v_cum_mort_m     bigint := 0;

    v_max_sem        integer;
    s                integer;

    -- ── por semana ──
    r_dias           integer;
    r_mort_h         integer;
    r_mort_m         integer;
    r_sel_h          integer;
    r_cons_kg_h      double precision;
    r_cons_kg_m      double precision;
    r_huevos_tot     integer;
    r_huevos_inc     integer;
    r_prom_huevos    double precision;
    r_efic           double precision;
    r_htaa           double precision;
    r_hiaa           double precision;
    r_peso_h         double precision;
    r_peso_m         double precision;
    r_unif           double precision;
    r_cv             double precision;
    r_peso_huevo     double precision;
    r_porc_mort_h    double precision;
    r_porc_mort_m    double precision;
    r_porc_sel_h     double precision;
    -- REQ-004: %Retiro real por semana
    r_retiro_sem_h   double precision;
    r_retiro_sem_m   double precision;
    r_retiro_ac_h    double precision;
    r_retiro_ac_m    double precision;
    r_aves_h_inicio  integer;
    r_aves_m_inicio  integer;
    -- guía
    g_cons_h         double precision;
    g_cons_m         double precision;
    g_mort_h         double precision;
    g_mort_m         double precision;
    g_peso_h         double precision;
    g_peso_m         double precision;
    g_unif           double precision;
    g_huevos_tot     double precision;
    g_huevos_inc     double precision;
    g_prod_pct       double precision;
    g_peso_huevo     double precision;
    -- REQ-004 (Verenice): %Retiro acumulado de guía por sexo.
    g_retiro_ac_h    double precision;
    g_retiro_ac_m    double precision;
    g_found          boolean;
    -- consumo real
    r_cons_real_h    double precision;
    r_cons_real_m    double precision;
    -- clasificadora
    r_limpios        integer;
    r_tratados       integer;
    r_sucios         integer;
    r_deformes       integer;
    r_blancos        integer;
    r_doble_yema     integer;
    r_piso           integer;
    r_pequenos       integer;
    r_rotos          integer;
    r_desecho        integer;
    r_otro           integer;
BEGIN
    -- ════════════════════════════════════════════════════════════════════
    -- 1) RESOLVER LOTE (misma prioridad y semántica que el C#).
    -- ════════════════════════════════════════════════════════════════════
    IF p_lote_postura_produccion_id IS NOT NULL AND p_lote_postura_produccion_id > 0 THEN
        -- ── Flujo LPP ──
        SELECT
            -- fecha ref: encaset del levante ligado -> lpp.fecha_encaset -> lpp.fecha_inicio_produccion
            (COALESCE(lev.fecha_encaset, lpp.fecha_encaset, lpp.fecha_inicio_produccion)
                AT TIME ZONE 'America/Bogota')::date,
            COALESCE(lpp.aves_h_inicial, lpp.hembras_iniciales_prod, 0),
            COALESCE(lpp.aves_m_inicial, lpp.machos_iniciales_prod, 0),
            COALESCE(lpp.raza, ''),
            lpp.ano_tabla_genetica::text
          INTO v_enc_date, v_aves_h_ini, v_aves_m_ini, v_raza, v_ano
          FROM lote_postura_produccion lpp
          LEFT JOIN lote_postura_levante lev
                 ON lev.lote_postura_levante_id = lpp.lote_postura_levante_id
                AND lev.deleted_at IS NULL
         WHERE lpp.lote_postura_produccion_id = p_lote_postura_produccion_id
           AND lpp.company_id = p_company_id
           AND lpp.deleted_at IS NULL;

        IF NOT FOUND OR v_enc_date IS NULL THEN
            RETURN;  -- lote inexistente o sin fecha de referencia -> sin filas (el C# lanza; el servicio valida antes)
        END IF;
        v_has_lote := true;

        -- Seguimientos: desde fn_seguimiento_diario_produccion (la fn diaria canónica ya hace el
        -- UNION dual-fuente + dedup por día Bogotá «gana el más temprano»); solo días con registro
        -- (seg_id IS NOT NULL — sin días movimiento-only).
        CREATE TEMP TABLE _seg ON COMMIT DROP AS
        SELECT f.fecha_ts AS ts,
               COALESCE(f.mortalidad_hembras,0) AS mort_h, COALESCE(f.mortalidad_machos,0) AS mort_m,
               COALESCE(f.sel_h,0) AS sel_h,
               COALESCE(f.cons_kg_h,0)::double precision AS cons_h,
               COALESCE(f.cons_kg_m,0)::double precision AS cons_m,
               COALESCE(f.huevo_tot,0) AS huevo_tot, COALESCE(f.huevo_inc,0) AS huevo_inc,
               COALESCE(f.huevo_limpio,0) AS h_limpio, COALESCE(f.huevo_tratado,0) AS h_tratado,
               COALESCE(f.huevo_sucio,0) AS h_sucio, COALESCE(f.huevo_deforme,0) AS h_deforme,
               COALESCE(f.huevo_blanco,0) AS h_blanco, COALESCE(f.huevo_doble_yema,0) AS h_doble,
               COALESCE(f.huevo_piso,0) AS h_piso, COALESCE(f.huevo_pequeno,0) AS h_pequeno,
               COALESCE(f.huevo_roto,0) AS h_roto, COALESCE(f.huevo_desecho,0) AS h_desecho,
               COALESCE(f.huevo_otro,0) AS h_otro,
               f.peso_huevo::double precision AS peso_huevo,
               f.peso_h::double precision AS peso_h, f.peso_m::double precision AS peso_m,
               f.uniformidad::double precision AS unif, f.coeficiente_variacion::double precision AS cv
          FROM fn_seguimiento_diario_produccion(p_lote_postura_produccion_id, NULL) f
         WHERE f.seg_id IS NOT NULL
           AND NOT f.fila_sin_lpp;   -- v2 fn diaria: los dias solo-traslado TSD no son "dia con registro"

    ELSIF p_lote_id IS NOT NULL AND p_lote_id > 0 THEN
        -- ── Flujo legacy: Lote en fase Producción ──
        -- lote_prod: hijo (lote_padre_id = p_lote_id) en fase Produccion; si no, el propio lote_id.
        DECLARE
            v_lp_lote_id      integer;
            v_lp_padre_id     integer;
            v_lp_fip          timestamptz;
            v_lp_raza         text;
            v_lp_ano          integer;
        BEGIN
            SELECT l.lote_id, l.lote_padre_id, l.fecha_inicio_produccion, l.raza, l.ano_tabla_genetica
              INTO v_lp_lote_id, v_lp_padre_id, v_lp_fip, v_lp_raza, v_lp_ano
              FROM lotes l
             WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
               AND l.fase = 'Produccion' AND l.lote_padre_id = p_lote_id
             ORDER BY l.lote_id
             LIMIT 1;

            IF NOT FOUND THEN
                SELECT l.lote_id, l.lote_padre_id, l.fecha_inicio_produccion, l.raza, l.ano_tabla_genetica
                  INTO v_lp_lote_id, v_lp_padre_id, v_lp_fip, v_lp_raza, v_lp_ano
                  FROM lotes l
                 WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
                   AND l.fase = 'Produccion' AND l.lote_id = p_lote_id
                 LIMIT 1;
            END IF;

            IF NOT FOUND THEN
                RETURN;
            END IF;
            v_has_lote := true;
            v_lote_id_str := v_lp_lote_id::text;
            v_lote_id_int := v_lp_lote_id;

            -- fecha ref = fecha_inicio_produccion; si null y hay padre -> fecha_encaset del padre
            IF v_lp_fip IS NULL AND v_lp_padre_id IS NOT NULL THEN
                SELECT p.fecha_encaset INTO v_lp_fip
                  FROM lotes p WHERE p.lote_id = v_lp_padre_id AND p.deleted_at IS NULL;
            END IF;
            IF v_lp_fip IS NULL THEN
                RETURN;
            END IF;
            v_enc_date := (v_lp_fip AT TIME ZONE 'America/Bogota')::date;

            SELECT COALESCE(hembras_iniciales_prod,0), COALESCE(machos_iniciales_prod,0)
              INTO v_aves_h_ini, v_aves_m_ini
              FROM lotes WHERE lote_id = v_lp_lote_id;

            -- raza/año del lote; si faltan y hay padre, del padre
            v_raza := COALESCE(v_lp_raza, '');
            v_ano  := v_lp_ano::text;
            IF (v_raza = '' OR v_lp_ano IS NULL) AND v_lp_padre_id IS NOT NULL THEN
                SELECT COALESCE(p.raza,''), p.ano_tabla_genetica::text
                  INTO v_raza, v_ano
                  FROM lotes p WHERE p.lote_id = v_lp_padre_id AND p.deleted_at IS NULL;
            END IF;
        END;

        -- Seguimientos legacy: desde fn_seguimiento_diario_produccion (dedup dual-fuente ya
        -- resuelto por la fn diaria); solo días con registro.
        CREATE TEMP TABLE _seg ON COMMIT DROP AS
        SELECT f.fecha_ts AS ts,
               COALESCE(f.mortalidad_hembras,0) AS mort_h, COALESCE(f.mortalidad_machos,0) AS mort_m,
               COALESCE(f.sel_h,0) AS sel_h,
               COALESCE(f.cons_kg_h,0)::double precision AS cons_h,
               COALESCE(f.cons_kg_m,0)::double precision AS cons_m,
               COALESCE(f.huevo_tot,0) AS huevo_tot, COALESCE(f.huevo_inc,0) AS huevo_inc,
               COALESCE(f.huevo_limpio,0) AS h_limpio, COALESCE(f.huevo_tratado,0) AS h_tratado,
               COALESCE(f.huevo_sucio,0) AS h_sucio, COALESCE(f.huevo_deforme,0) AS h_deforme,
               COALESCE(f.huevo_blanco,0) AS h_blanco, COALESCE(f.huevo_doble_yema,0) AS h_doble,
               COALESCE(f.huevo_piso,0) AS h_piso, COALESCE(f.huevo_pequeno,0) AS h_pequeno,
               COALESCE(f.huevo_roto,0) AS h_roto, COALESCE(f.huevo_desecho,0) AS h_desecho,
               COALESCE(f.huevo_otro,0) AS h_otro,
               f.peso_huevo::double precision AS peso_huevo,
               f.peso_h::double precision AS peso_h, f.peso_m::double precision AS peso_m,
               f.uniformidad::double precision AS unif, f.coeficiente_variacion::double precision AS cv
          FROM fn_seguimiento_diario_produccion(NULL, v_lote_id_int) f
         WHERE f.seg_id IS NOT NULL
           AND NOT f.fila_sin_lpp;   -- v2 fn diaria: los dias solo-traslado TSD no son "dia con registro"

    ELSE
        RETURN;  -- ni LPP ni loteId válido
    END IF;

    IF NOT v_has_lote THEN RETURN; END IF;

    -- ════════════════════════════════════════════════════════════════════
    -- 2) Semana de VIDA de cada registro + filtro de fechas (== C#).
    --    semanaVida = floor(dias/7)+1 con dias = regDate - encDate (división entera).
    -- ════════════════════════════════════════════════════════════════════
    ALTER TABLE _seg ADD COLUMN reg_date date;
    ALTER TABLE _seg ADD COLUMN sem_vida integer;
    UPDATE _seg SET reg_date = (ts AT TIME ZONE 'America/Bogota')::date;
    -- filtro de fechas (request.FechaDesde/Hasta) sobre la fecha local
    IF p_fecha_desde IS NOT NULL THEN
        DELETE FROM _seg WHERE reg_date < p_fecha_desde;
    END IF;
    IF p_fecha_hasta IS NOT NULL THEN
        DELETE FROM _seg WHERE reg_date > p_fecha_hasta;
    END IF;
    UPDATE _seg SET sem_vida = ((reg_date - v_enc_date) / 7) + 1;  -- división entera == C# (dias/7)+1
    -- REQ-012b: producción arranca en la semana 25 de vida (antes 26). La guía genética empieza en
    --   la semana 26, así que la 25 queda con columnas de guía en NULL (g_found=false ya lo soporta).
    DELETE FROM _seg WHERE sem_vida < 25;

    SELECT MAX(sem_vida) INTO v_max_sem FROM _seg;
    IF v_max_sem IS NULL THEN RETURN; END IF;

    -- ════════════════════════════════════════════════════════════════════
    -- 3) Iterar semanas presentes en orden (== foreach sobre grupos ordenados).
    --    OJO: itera SOLO las semanas con registros (>=25 tras REQ-012b) y en orden asc.
    --    Los acumuladores (aves actuales, htaa/hiaa, retiro) avanzan solo en esas semanas.
    -- ════════════════════════════════════════════════════════════════════
    v_aves_h_act := v_aves_h_ini;
    v_aves_m_act := v_aves_m_ini;

    FOR s IN 25..v_max_sem LOOP  -- REQ-012b: incluir semana 25 (antes 26)
        CONTINUE WHEN NOT EXISTS (SELECT 1 FROM _seg WHERE sem_vida = s);

        SELECT COUNT(*)::int,
               COALESCE(SUM(mort_h),0), COALESCE(SUM(mort_m),0), COALESCE(SUM(sel_h),0),
               COALESCE(SUM(cons_h),0), COALESCE(SUM(cons_m),0),
               COALESCE(SUM(huevo_tot),0), COALESCE(SUM(huevo_inc),0),
               COALESCE(SUM(h_limpio),0), COALESCE(SUM(h_tratado),0), COALESCE(SUM(h_sucio),0),
               COALESCE(SUM(h_deforme),0), COALESCE(SUM(h_blanco),0), COALESCE(SUM(h_doble),0),
               COALESCE(SUM(h_piso),0), COALESCE(SUM(h_pequeno),0), COALESCE(SUM(h_roto),0),
               COALESCE(SUM(h_desecho),0), COALESCE(SUM(h_otro),0)
          INTO r_dias, r_mort_h, r_mort_m, r_sel_h, r_cons_kg_h, r_cons_kg_m,
               r_huevos_tot, r_huevos_inc,
               r_limpios, r_tratados, r_sucios, r_deformes, r_blancos, r_doble_yema,
               r_piso, r_pequenos, r_rotos, r_desecho, r_otro
          FROM _seg WHERE sem_vida = s;

        r_prom_huevos := CASE WHEN r_dias > 0 THEN r_huevos_tot::double precision / r_dias ELSE 0 END;

        -- REQ-004a: %Producción hen-day = huevos/día / HEMBRAS vivas (solo hembras) * 100
        r_efic := CASE WHEN v_aves_h_act > 0 THEN r_prom_huevos / v_aves_h_act * 100 ELSE 0 END;

        -- Acumulados por ave alojada (REQ-004c)
        v_cum_h_tot := v_cum_h_tot + r_huevos_tot;
        v_cum_h_inc := v_cum_h_inc + r_huevos_inc;

        -- REQ-004: acumulados de retiro (mortalidad + selección) por sexo. Machos sin selección en
        --   esta fn (igual que el decremento de aves, que solo resta mort_m).
        v_cum_mort_h := v_cum_mort_h + r_mort_h;
        v_cum_sel_h  := v_cum_sel_h + r_sel_h;
        v_cum_mort_m := v_cum_mort_m + r_mort_m;
        r_htaa := CASE WHEN v_aves_h_ini > 0 THEN v_cum_h_tot::double precision / v_aves_h_ini ELSE 0 END;
        r_hiaa := CASE WHEN v_aves_h_ini > 0 THEN v_cum_h_inc::double precision / v_aves_h_ini ELSE 0 END;

        -- Peso aves (kg, REQ-004b): promedio de registros con valor NO NULO, luego normalizar.
        SELECT AVG(peso_h) FILTER (WHERE peso_h IS NOT NULL),
               AVG(peso_m) FILTER (WHERE peso_m IS NOT NULL),
               AVG(unif)   FILTER (WHERE unif   IS NOT NULL),
               AVG(cv)     FILTER (WHERE cv     IS NOT NULL),
               AVG(peso_huevo) FILTER (WHERE peso_huevo > 0)
          INTO r_peso_h, r_peso_m, r_unif, r_cv, r_peso_huevo
          FROM _seg WHERE sem_vida = s;
        IF r_peso_h IS NOT NULL THEN r_peso_h := CASE WHEN r_peso_h > 100 THEN r_peso_h/1000 ELSE r_peso_h END; END IF;
        IF r_peso_m IS NOT NULL THEN r_peso_m := CASE WHEN r_peso_m > 100 THEN r_peso_m/1000 ELSE r_peso_m END; END IF;

        -- %mortalidad / %selección: sobre el saldo REAL de inicio (avesActuales)
        r_porc_mort_h := CASE WHEN v_aves_h_act > 0 THEN r_mort_h::double precision / v_aves_h_act * 100 ELSE 0 END;
        r_porc_mort_m := CASE WHEN v_aves_m_act > 0 THEN r_mort_m::double precision / v_aves_m_act * 100 ELSE 0 END;
        r_porc_sel_h  := CASE WHEN v_aves_h_act > 0 THEN r_sel_h::double precision  / v_aves_h_act * 100 ELSE 0 END;

        -- REQ-004: %Retiro REAL (== ProduccionCalculos.PorcentajeRetiroSemanal/Acumulado).
        --   Semanal: (mort + sel de la semana) / saldo REAL de inicio del sexo (v_aves_*_act, pre-decremento) * 100.
        --   Acumulado: (mort + sel acumulados) / aves iniciales del sexo * 100.
        r_retiro_sem_h := CASE WHEN v_aves_h_act > 0 THEN (r_mort_h + r_sel_h)::double precision / v_aves_h_act * 100 ELSE 0 END;
        r_retiro_sem_m := CASE WHEN v_aves_m_act > 0 THEN r_mort_m::double precision / v_aves_m_act * 100 ELSE 0 END;
        r_retiro_ac_h  := CASE WHEN v_aves_h_ini > 0 THEN (v_cum_mort_h + v_cum_sel_h)::double precision / v_aves_h_ini * 100 ELSE 0 END;
        r_retiro_ac_m  := CASE WHEN v_aves_m_ini > 0 THEN v_cum_mort_m::double precision / v_aves_m_ini * 100 ELSE 0 END;

        -- Censo de inicio de semana (desviación preservada: sobrecuenta con las bajas de la propia semana)
        r_aves_h_inicio := v_aves_h_act + r_mort_h + r_sel_h;
        r_aves_m_inicio := v_aves_m_act + r_mort_m;

        -- ── Guía (una sola tabla) por Edad = semana de VIDA (s) ──
        g_found := false;
        SELECT true,
               NULLIF(btrim(g.gr_ave_dia_h),'')::double precision,
               NULLIF(btrim(g.gr_ave_dia_m),'')::double precision,
               NULLIF(btrim(g.mort_sem_h),'')::double precision,
               NULLIF(btrim(g.mort_sem_m),'')::double precision,
               NULLIF(btrim(g.peso_h),'')::double precision,
               NULLIF(btrim(g.peso_m),'')::double precision,
               NULLIF(btrim(g.uniformidad),'')::double precision,
               NULLIF(btrim(g.h_total_aa),'')::double precision,
               NULLIF(btrim(g.h_inc_aa),'')::double precision,
               NULLIF(btrim(g.prod_porcentaje),'')::double precision,
               NULLIF(btrim(g.peso_huevo),'')::double precision,
               NULLIF(btrim(g.retiro_ac_h),'')::double precision,
               NULLIF(btrim(g.retiro_ac_m),'')::double precision
          INTO g_found, g_cons_h, g_cons_m, g_mort_h, g_mort_m, g_peso_h, g_peso_m, g_unif,
               g_huevos_tot, g_huevos_inc, g_prod_pct, g_peso_huevo, g_retiro_ac_h, g_retiro_ac_m
          FROM guia_genetica_sanmarino_colombia g
         WHERE g.company_id = p_company_id
           AND g.deleted_at IS NULL
           AND btrim(lower(g.raza)) = btrim(lower(v_raza))
           AND btrim(g.anio_guia) = v_ano
           AND fn_parse_edad_numerica(g.edad) = s
         -- La semana 25 tiene DOS filas que parsean a 25: '25' (cierre de
         -- levante) y '25P' (arranque de producción), con valores muy distintos
         -- (retiro_ac_h 4,03 vs 0,10). Sin ORDER BY la que gana depende del
         -- plan y del orden físico de la tabla: hoy sale '25P' por el ctid, no
         -- por contrato. Se fija el desempate en la variante con sufijo —la de
         -- producción, que es la correcta acá y la que ya venía devolviendo—
         -- para que un VACUUM o un re-seed no cambien el reporte en silencio.
         ORDER BY (CASE WHEN btrim(g.edad) = s::text THEN 1 ELSE 0 END), g.id
         LIMIT 1;
        g_found := COALESCE(g_found, false);

        IF g_found THEN
            -- ParseDouble => 0 cuando el string es vacío/no numérico (no NULL). Las columnas de la
            -- guía "obtenerGuiaGeneticaProduccion" pasan por ParseDouble (0 si vacío); las del raw
            -- (huevos/%prod/pesoHuevo) por ParseDecimal (NULL si vacío). Se respeta esa diferencia:
            g_cons_h := COALESCE(g_cons_h, 0);
            g_cons_m := COALESCE(g_cons_m, 0);
            g_mort_h := COALESCE(g_mort_h, 0);
            g_mort_m := COALESCE(g_mort_m, 0);
            g_peso_h := COALESCE(g_peso_h, 0) / 1000;   -- peso_h/1000
            g_peso_m := COALESCE(g_peso_m, 0) / 1000;   -- peso_m/1000
            g_unif   := COALESCE(g_unif, 0);
            -- huevos/%prod/pesoHuevo: quedan NULL si vacíos (ParseDecimal), no 0.
            -- retiro_ac_h/m guía: mismo criterio que mort_h/mort_m (ParseDouble => 0 si vacío).
            g_retiro_ac_h := COALESCE(g_retiro_ac_h, 0);
            g_retiro_ac_m := COALESCE(g_retiro_ac_m, 0);
        ELSE
            g_cons_h := NULL; g_cons_m := NULL; g_mort_h := NULL; g_mort_m := NULL;
            g_peso_h := NULL; g_peso_m := NULL; g_unif := NULL;
            g_huevos_tot := NULL; g_huevos_inc := NULL; g_prod_pct := NULL; g_peso_huevo := NULL;
            g_retiro_ac_h := NULL; g_retiro_ac_m := NULL;
        END IF;

        -- Consumo real (g/ave/día) — denominador = censo de inicio sobrecontado (desviación preservada)
        r_cons_real_h := CASE WHEN r_dias > 0 AND r_aves_h_inicio > 0
                              THEN r_cons_kg_h * 1000 / (r_dias * r_aves_h_inicio) ELSE NULL END;
        r_cons_real_m := CASE WHEN r_dias > 0 AND r_aves_m_inicio > 0
                              THEN r_cons_kg_m * 1000 / (r_dias * r_aves_m_inicio) ELSE NULL END;

        -- Decremento de aves (al final, == C#)
        v_aves_h_act := GREATEST(0, v_aves_h_act - r_mort_h - r_sel_h);
        v_aves_m_act := GREATEST(0, v_aves_m_act - r_mort_m);

        -- ── Emitir fila (respetando filtro semanaDesde/Hasta como en C#) ──
        IF (p_semana_desde IS NULL OR s >= p_semana_desde)
           AND (p_semana_hasta IS NULL OR s <= p_semana_hasta) THEN
            semana                           := s;
            fecha_inicio_semana              := v_enc_date + ((s - 1) * 7);
            fecha_fin_semana                 := v_enc_date + ((s - 1) * 7) + 6;
            total_registros                  := r_dias;
            mortalidad_hembras               := r_mort_h;
            mortalidad_machos                := r_mort_m;
            porcentaje_mortalidad_hembras    := r_porc_mort_h;
            porcentaje_mortalidad_machos     := r_porc_mort_m;
            mortalidad_guia_hembras          := g_mort_h;
            mortalidad_guia_machos           := g_mort_m;
            diferencia_mortalidad_hembras    := fn_dif_pct(r_porc_mort_h, g_mort_h);
            diferencia_mortalidad_machos     := fn_dif_pct(r_porc_mort_m, g_mort_m);
            seleccion_hembras                := r_sel_h;
            porcentaje_seleccion_hembras     := r_porc_sel_h;
            consumo_kg_hembras               := r_cons_kg_h;
            consumo_kg_machos                := r_cons_kg_m;
            consumo_total_kg                 := r_cons_kg_h + r_cons_kg_m;
            consumo_promedio_diario_kg       := CASE WHEN r_dias > 0 THEN (r_cons_kg_h + r_cons_kg_m)/r_dias ELSE 0 END;
            consumo_guia_hembras             := g_cons_h;
            consumo_guia_machos              := g_cons_m;
            diferencia_consumo_hembras       := fn_dif_pct(r_cons_real_h, g_cons_h);
            diferencia_consumo_machos        := fn_dif_pct(r_cons_real_m, g_cons_m);
            huevos_totales                   := r_huevos_tot;
            huevos_incubables                := r_huevos_inc;
            promedio_huevos_por_dia          := r_prom_huevos;
            eficiencia_produccion            := r_efic;
            huevos_totales_guia              := g_huevos_tot;
            huevos_incubables_guia           := g_huevos_inc;
            porcentaje_produccion_guia       := g_prod_pct;
            diferencia_huevos_totales        := fn_dif_pct(r_htaa, g_huevos_tot);
            diferencia_huevos_incubables     := fn_dif_pct(r_hiaa, g_huevos_inc);
            diferencia_porcentaje_produccion := fn_dif_pct(r_efic, g_prod_pct);
            peso_huevo_promedio              := r_peso_huevo;
            peso_huevo_guia                  := g_peso_huevo;
            diferencia_peso_huevo            := fn_dif_pct(r_peso_huevo, g_peso_huevo);
            peso_promedio_hembras            := r_peso_h;
            peso_promedio_machos             := r_peso_m;
            peso_guia_hembras                := g_peso_h;
            peso_guia_machos                 := g_peso_m;
            diferencia_peso_hembras          := fn_dif_pct(r_peso_h, g_peso_h);
            diferencia_peso_machos           := fn_dif_pct(r_peso_m, g_peso_m);
            uniformidad_promedio             := r_unif;
            uniformidad_guia                 := g_unif;
            diferencia_uniformidad           := fn_dif_pct(r_unif, g_unif);
            coeficiente_variacion_promedio   := r_cv;
            huevos_limpios                   := r_limpios;
            huevos_tratados                  := r_tratados;
            huevos_sucios                    := r_sucios;
            huevos_deformes                  := r_deformes;
            huevos_blancos                   := r_blancos;
            huevos_doble_yema                := r_doble_yema;
            huevos_piso                      := r_piso;
            huevos_pequenos                  := r_pequenos;
            huevos_rotos                     := r_rotos;
            huevos_desecho                   := r_desecho;
            huevos_otro                      := r_otro;
            aves_hembras_inicio_semana       := r_aves_h_inicio;
            aves_machos_inicio_semana        := r_aves_m_inicio;
            aves_hembras_fin_semana          := v_aves_h_act;
            aves_machos_fin_semana           := v_aves_m_act;
            htaa_real                        := r_htaa;
            hiaa_real                        := r_hiaa;
            retiro_sem_h                     := r_retiro_sem_h;
            retiro_sem_m                     := r_retiro_sem_m;
            retiro_ac_h                      := r_retiro_ac_h;
            retiro_ac_m                      := r_retiro_ac_m;
            retiro_ac_h_guia                 := g_retiro_ac_h;
            retiro_ac_m_guia                 := g_retiro_ac_m;
            RETURN NEXT;
        END IF;
    END LOOP;

    RETURN;
END;
$fn$;
""";

        private const string FnClasificacionV2 = """
-- ============================================================================
-- fn_clasificacion_huevo_items_produccion(...)
-- FASE 5 (Santa Reyes) — Desglose de la clasificación de huevos POR ÍTEM del catálogo
-- (categorías comerciales Primera / Pnc) agrupado por SEMANA DE VIDA, para las empresas con
-- companies.clasificacion_huevo_por_items = true: en ellas las 11 columnas fijas
-- (huevo_limpio…huevo_otro) quedan en 0 y el desglose real vive en el jsonb
-- seguimiento_diario_produccion.metadata -> 'huevoItems'
-- ([{catalogItemId, codigo, nombre, tipoHuevo, cantidad, um}, …], escrito por
-- HuevoItemsCalculos.EscribirEnMetadata; huevo_tot conserva la SUMA para no romper
-- espejo/trigger/saldos/indicadores).
--
-- Es el endpoint HERMANO de fn_indicadores_produccion_postura: MISMOS parámetros de entrada,
-- MISMA resolución de lote (LPP prioritario / legacy por lote), MISMA fuente de datos
-- (UNION seguimiento_diario_levante[tipo_seguimiento='produccion'] + seguimiento_diario_produccion
-- con DISTINCT ON por día en America/Bogota) y MISMA fórmula de semana
-- (semana de vida = ((fecha_local - fecha_encaset) / 7) + 1, división entera) → la columna
-- `semana` casa 1:1 con la grilla de indicadores semanales.
--
-- Diferencias deliberadas respecto de fn_indicadores_produccion_postura (documentadas):
--   * NO aplica el corte "semanas de producción >= 25": el desglose respeta EXCLUSIVAMENTE los
--     filtros que envía el llamador (p_semana_desde/p_semana_hasta). Motivo: las empresas que usan
--     clasificación por ítems pasan a producción antes de la semana 25 (Santa Reyes liquida el
--     levante ~semana 16) y ese desglose se perdería. El front actual manda semanaDesde = 26,
--     así que el resultado visible sigue alineado con la grilla.
--   * No usa tablas TEMP: todo se resuelve con CTEs (la fn puede invocarse varias veces dentro de
--     la misma transacción sin colisionar).
--
-- Robustez (nunca lanza; devuelve 0 filas cuando no hay nada):
--   * lote inexistente / sin fecha de referencia / sin ninguno de los dos parámetros → sin filas.
--   * registros sin metadata o sin la clave 'huevoItems' → no aportan (COALESCE a '[]').
--   * 'huevoItems' que no sea un array JSON, o elementos que no sean objetos → se ignoran.
--   * cantidades ausentes, no numéricas o <= 0 → se descartan.
--
-- Consumido por IndicadoresProduccionService.ObtenerClasificacionHuevoItemsAsync (SqlQueryRaw,
-- columnas snake_case → propiedades PascalCase) vía POST /api/Produccion/clasificacion-huevo-items.
-- ============================================================================

-- Idempotente: el row type de los parámetros OUT puede cambiar en el futuro y Postgres no admite
-- CREATE OR REPLACE en ese caso → DROP defensivo con la firma exacta.
DROP FUNCTION IF EXISTS fn_clasificacion_huevo_items_produccion(integer, integer, integer, integer, integer, date, date);

CREATE OR REPLACE FUNCTION fn_clasificacion_huevo_items_produccion(
    p_company_id                  integer,
    p_lote_postura_produccion_id  integer  DEFAULT NULL,
    p_lote_id                     integer  DEFAULT NULL,
    p_semana_desde                integer  DEFAULT NULL,
    p_semana_hasta                integer  DEFAULT NULL,
    p_fecha_desde                 date     DEFAULT NULL,
    p_fecha_hasta                 date     DEFAULT NULL
)
RETURNS TABLE(
    semana      integer,
    tipo_huevo  text,
    codigo      text,
    nombre      text,
    cantidad    bigint
)
LANGUAGE plpgsql STABLE AS $fn$
DECLARE
    v_enc_date     date;               -- fecha de referencia (encaset) en zona Bogotá
    v_lote_id_str  text;               -- flujo legacy: lote_id como texto (columna varchar del unificado)
    v_lote_id_int  integer;            -- flujo legacy: lote resuelto, para fn_seguimiento_diario_produccion
    v_flujo_lpp    boolean := false;   -- true = flujo LPP, false = flujo legacy por lote
    v_has_lote     boolean := false;
BEGIN
    -- ════════════════════════════════════════════════════════════════════
    -- 1) RESOLVER LOTE (misma prioridad y semántica que fn_indicadores_produccion_postura).
    -- ════════════════════════════════════════════════════════════════════
    IF p_lote_postura_produccion_id IS NOT NULL AND p_lote_postura_produccion_id > 0 THEN
        -- ── Flujo LPP ──
        SELECT
            -- fecha ref: encaset del levante ligado -> lpp.fecha_encaset -> lpp.fecha_inicio_produccion
            (COALESCE(lev.fecha_encaset, lpp.fecha_encaset, lpp.fecha_inicio_produccion)
                AT TIME ZONE 'America/Bogota')::date
          INTO v_enc_date
          FROM lote_postura_produccion lpp
          LEFT JOIN lote_postura_levante lev
                 ON lev.lote_postura_levante_id = lpp.lote_postura_levante_id
                AND lev.deleted_at IS NULL
         WHERE lpp.lote_postura_produccion_id = p_lote_postura_produccion_id
           AND lpp.company_id = p_company_id
           AND lpp.deleted_at IS NULL;

        IF NOT FOUND OR v_enc_date IS NULL THEN
            RETURN;  -- lote inexistente o sin fecha de referencia -> sin filas
        END IF;
        v_flujo_lpp := true;
        v_has_lote  := true;

    ELSIF p_lote_id IS NOT NULL AND p_lote_id > 0 THEN
        -- ── Flujo legacy: Lote en fase Producción ──
        -- lote_prod: hijo (lote_padre_id = p_lote_id) en fase Produccion; si no, el propio lote_id.
        DECLARE
            v_lp_lote_id      integer;
            v_lp_padre_id     integer;
            v_lp_fip          timestamptz;
        BEGIN
            SELECT l.lote_id, l.lote_padre_id, l.fecha_inicio_produccion
              INTO v_lp_lote_id, v_lp_padre_id, v_lp_fip
              FROM lotes l
             WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
               AND l.fase = 'Produccion' AND l.lote_padre_id = p_lote_id
             ORDER BY l.lote_id
             LIMIT 1;

            IF NOT FOUND THEN
                SELECT l.lote_id, l.lote_padre_id, l.fecha_inicio_produccion
                  INTO v_lp_lote_id, v_lp_padre_id, v_lp_fip
                  FROM lotes l
                 WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
                   AND l.fase = 'Produccion' AND l.lote_id = p_lote_id
                 LIMIT 1;
            END IF;

            IF NOT FOUND THEN
                RETURN;
            END IF;
            v_lote_id_str := v_lp_lote_id::text;
            v_lote_id_int := v_lp_lote_id;

            -- fecha ref = fecha_inicio_produccion; si null y hay padre -> fecha_encaset del padre
            IF v_lp_fip IS NULL AND v_lp_padre_id IS NOT NULL THEN
                SELECT p.fecha_encaset INTO v_lp_fip
                  FROM lotes p WHERE p.lote_id = v_lp_padre_id AND p.deleted_at IS NULL;
            END IF;
            IF v_lp_fip IS NULL THEN
                RETURN;
            END IF;
            v_enc_date := (v_lp_fip AT TIME ZONE 'America/Bogota')::date;
            v_has_lote := true;
        END;

    ELSE
        RETURN;  -- ni LPP ni loteId válido
    END IF;

    IF NOT v_has_lote THEN RETURN; END IF;

    -- ════════════════════════════════════════════════════════════════════
    -- 2) Seguimientos del lote — desde fn_seguimiento_diario_produccion (la fn diaria canónica
    --    ya resuelve el UNION dual-fuente + dedup por día Bogotá «gana el más temprano»; acá
    --    solo se toman los días con registro, seg_id IS NOT NULL — sin días movimiento-only).
    -- 3) Semana de vida + filtros de fecha/semana.
    -- 4) Expansión del jsonb 'huevoItems' y agregación por semana × tipo × ítem.
    -- ════════════════════════════════════════════════════════════════════
    RETURN QUERY
    WITH dias AS (
        SELECT f.fecha AS reg_date, f.metadata AS meta
          FROM fn_seguimiento_diario_produccion(
                   CASE WHEN v_flujo_lpp THEN p_lote_postura_produccion_id END,
                   CASE WHEN NOT v_flujo_lpp THEN v_lote_id_int END) f
         WHERE f.seg_id IS NOT NULL
       AND NOT f.fila_sin_lpp   -- v2 fn diaria: los dias solo-traslado TSD no son "dia con registro"
    ),
    filtrados AS (
        SELECT ((dd.reg_date - v_enc_date) / 7) + 1 AS sem,   -- división entera == C# (dias/7)+1
               dd.meta
          FROM dias dd
         WHERE (p_fecha_desde IS NULL OR dd.reg_date >= p_fecha_desde)
           AND (p_fecha_hasta IS NULL OR dd.reg_date <= p_fecha_hasta)
    ),
    items AS (
        SELECT f.sem,
               COALESCE(NULLIF(btrim(it->>'tipoHuevo'), ''), '') AS tipo,
               COALESCE(NULLIF(btrim(it->>'codigo'),    ''), '') AS cod,
               COALESCE(NULLIF(btrim(it->>'nombre'),    ''), '') AS nom,
               CASE WHEN jsonb_typeof(it->'cantidad') = 'number'
                    THEN (it->>'cantidad')::numeric
                    ELSE NULL END AS cant
          FROM filtrados f
          CROSS JOIN LATERAL jsonb_array_elements(
                 CASE WHEN jsonb_typeof(COALESCE(f.meta->'huevoItems', '[]'::jsonb)) = 'array'
                      THEN COALESCE(f.meta->'huevoItems', '[]'::jsonb)
                      ELSE '[]'::jsonb
                 END) AS it
         WHERE jsonb_typeof(it) = 'object'
    )
    SELECT i.sem::integer,
           i.tipo::text,
           i.cod::text,
           i.nom::text,
           SUM(i.cant)::bigint
      FROM items i
     WHERE i.cant IS NOT NULL
       AND i.cant > 0
       AND (p_semana_desde IS NULL OR i.sem >= p_semana_desde)
       AND (p_semana_hasta IS NULL OR i.sem <= p_semana_hasta)
     GROUP BY i.sem, i.tipo, i.cod, i.nom
     ORDER BY i.sem, i.tipo, i.cod, i.nom;

    RETURN;
END;
$fn$;
""";

        private const string FnResumenV2 = """
-- ============================================================================
-- fn_resumen_semanal_ra_pesadas_produccion(...)
-- Hoja «RESUMEN SEMANAL» del Informe RA Pesadas — BLOQUE PRODUCCIÓN AP.
-- ----------------------------------------------------------------------------
-- UNA FILA POR LOTE para UNA semana CALENDARIO. Hermana de
-- fn_resumen_semanal_ra_pesadas_levante; misma convención de semana del año
-- (WEEKNUM de Excel, ver cabecera de la fn de levante).
--
-- ⚠️ Set-based A PROPÓSITO. Prohibido iterar lotes desde C# llamando la fn
--    por-lote: la BD filtra, el backend orquesta (regla del repo).
--
-- EQUIVALENCIA CON EL DETALLE (fn_indicadores_produccion_postura, **flujo LPP**):
--   ⚠️ El Detalle (ReporteTecnicoSemanalService.Produccion) llama la fn base con
--      `lpp.LotePosturaProduccionId`, NO con lote_id ⇒ hay que replicar el flujo
--      LPP, que resuelve distinto que el flujo legacy por lote. Usar
--      lotes.fecha_inicio_produccion aquí daría OTRA semana de vida y el Resumen
--      no cuadraría con el Detalle.
--   * unidad      = lote_postura_produccion (lpp), scope por company + deleted_at
--   * fecha ref   = COALESCE(lev.fecha_encaset, lpp.fecha_encaset,
--                            lpp.fecha_inicio_produccion)  ← encaset del levante
--                   ligado PRIMERO. Sin fecha ⇒ lote fuera.
--   * base aves   = COALESCE(lpp.aves_h_inicial, lpp.hembras_iniciales_prod, 0)
--                   COALESCE(lpp.aves_m_inicial, lpp.machos_iniciales_prod, 0)
--   * fuentes     = seguimiento_diario_levante (tipo_seguimiento='produccion')
--                   UNION seguimiento_diario_produccion, ambas filtradas por
--                   lote_postura_produccion_id, DEDUPLICADAS POR DÍA
--                   (calendario Bogotá): gana el registro de timestamp MÁS
--                   TEMPRANO del día — idéntico al DISTINCT ON de la fn base.
--   * semana vida = ((reg_date − fecha_ref) / 7) + 1  (división ENTERA, como la
--                   fn base; equivale a floor() con fechas no negativas)
--   * arranque    = semana 25 (REQ-012b)
--   * saldos      = hembras: −(mort + sel);  machos: −mort  (los machos NO
--                   tienen selección en esta fn — desviación preservada)
--   * %           = todos los porcentajes semanales van sobre el saldo al
--                   INICIO de la semana (pre-decremento), igual que la fn base
--
-- FÓRMULAS (verificadas contra la fn base y ReporteTecnicoSemanalCalculos):
--   %Prod        = (huevo_tot / días) / saldo_hembras_inicio * 100
--   HTAA         = Σ huevo_tot hasta la semana / hembras_iniciales
--   HIAA         = Σ huevo_inc hasta la semana / hembras_iniciales
--   %AprovSem    = huevo_inc * 100 / huevo_tot
--   GrHuevoInc   = (cons_kg_h + cons_kg_m) * 1000 / huevo_inc
--   %MortH       = mort_h / saldo_hembras_inicio * 100
--   %RetiroH     = Σ(mort_h + sel_h) / hembras_iniciales * 100
--   %RetiroM     = Σ mort_m / machos_iniciales * 100
--   PesoM/H      = peso_m / peso_h * 100   (pesos normalizados a kg: >100 ⇒ /1000)
--   Dif*         = fn_dif_pct(real, guía) = (real − guía) / guía * 100
--
-- Guía: guia_genetica_sanmarino_colombia por (company, raza, año, edad = semana
-- de vida). Todo TEXT ⇒ f_safe_numeric.
-- ============================================================================
DROP FUNCTION IF EXISTS fn_resumen_semanal_ra_pesadas_produccion(integer, integer, integer, integer[], text, text);

CREATE OR REPLACE FUNCTION fn_resumen_semanal_ra_pesadas_produccion(
    p_company_id  integer,
    p_anio        integer,
    p_sem_anio    integer,   -- NULL = todas las semanas del año
    p_granja_ids  integer[] DEFAULT NULL,
    p_regional    text      DEFAULT NULL,
    p_ciclo       text      DEFAULT NULL
)
RETURNS TABLE(
    lote_postura_produccion_id integer,
    lote_id                   integer,
    lote_nombre               text,
    granja_id                 integer,
    granja_nombre             text,
    nucleo_nombre             text,
    regional                  text,
    raza                      text,
    anio_guia                 integer,
    ciclo_produccion          text,
    tipo_nido                 text,
    edad_semana               integer,
    fecha_fin_semana          date,
    dias_con_registro         integer,
    part                      double precision,
    saldo_hembras             double precision,
    saldo_machos              double precision,
    produccion_pct            double precision,
    produccion_pct_guia       double precision,
    dif_produccion_pct        double precision,
    htaa                      double precision,
    htaa_guia                 double precision,
    dif_htaa                  double precision,
    hiaa                      double precision,
    hiaa_guia                 double precision,
    dif_hiaa                  double precision,
    aprov_sem_pct             double precision,
    aprov_sem_pct_guia        double precision,
    dif_aprov_sem_pct         double precision,
    gr_huevo_inc              double precision,
    mort_hembras_pct          double precision,
    retiro_acum_hembras_pct   double precision,
    retiro_acum_hembras_guia  double precision,
    mort_machos_pct           double precision,
    retiro_acum_machos_pct    double precision,
    retiro_acum_machos_guia   double precision,
    peso_macho_sobre_hembra   double precision
)
LANGUAGE sql STABLE AS $$
WITH
-- ── 1) Lotes de producción (LPP) de la empresa (+ ubicación, guía y fecha ref) ─
lote_base AS (
    SELECT lpp.lote_postura_produccion_id,
           lpp.lote_id,
           lpp.lote_nombre::text                                        AS lote_nombre,
           lpp.granja_id,
           f.name::text                                                 AS granja_nombre,
           n.nucleo_nombre::text                                        AS nucleo_nombre,
           COALESCE(NULLIF(mo.value, ''), NULLIF(lpp.regional, ''))::text AS regional,
           COALESCE(lpp.raza, '')::text                                 AS raza,
           lpp.ano_tabla_genetica                                       AS anio_guia,
           NULLIF(lpp.ciclo_produccion, '')::text                       AS ciclo_produccion,
           NULLIF(lpp.tipo_nido, '')::text                              AS tipo_nido,
           (COALESCE(lev.fecha_encaset, lpp.fecha_encaset, lpp.fecha_inicio_produccion)
                AT TIME ZONE 'America/Bogota')::date                    AS ref_date,
           COALESCE(lpp.aves_h_inicial, lpp.hembras_iniciales_prod, 0)::double precision AS base_h,
           COALESCE(lpp.aves_m_inicial, lpp.machos_iniciales_prod, 0)::double precision  AS base_m
      FROM lote_postura_produccion lpp
      JOIN farms f
        ON f.id = lpp.granja_id
      LEFT JOIN lote_postura_levante lev
        ON lev.lote_postura_levante_id = lpp.lote_postura_levante_id
       AND lev.deleted_at IS NULL
      LEFT JOIN nucleos n
        ON n.granja_id = lpp.granja_id
       AND n.nucleo_id = lpp.nucleo_id
       AND n.deleted_at IS NULL
      LEFT JOIN master_list_options mo
        ON mo.id = f.regional_id
     WHERE lpp.company_id = p_company_id
       AND lpp.deleted_at IS NULL
       AND (p_granja_ids IS NULL OR lpp.granja_id = ANY (p_granja_ids))
       AND (p_ciclo IS NULL OR COALESCE(lpp.ciclo_produccion, '') = p_ciclo)
),
lote_ok AS (
    SELECT * FROM lote_base WHERE ref_date IS NOT NULL
),
-- ── 2+3) Un registro por lote y DÍA desde fn_seguimiento_diario_produccion (la fn diaria
--         canónica ya hace el UNION dual-fuente + dedup por día Bogotá «gana el más
--         temprano»); solo días con registro (seg_id IS NOT NULL). Patrón CROSS JOIN LATERAL
--         del Reporte de Costos de engorde: la fn LANGUAGE sql se inlinea. ─────────────────
dedup AS (
    SELECT lo.lote_postura_produccion_id                   AS lpp_id,
           f.fecha                                         AS reg_date,
           COALESCE(f.mortalidad_hembras, 0)               AS mort_h,
           COALESCE(f.mortalidad_machos, 0)                AS mort_m,
           COALESCE(f.sel_h, 0)                            AS sel_h,
           COALESCE(f.cons_kg_h, 0)::double precision      AS cons_h,
           COALESCE(f.cons_kg_m, 0)::double precision      AS cons_m,
           COALESCE(f.huevo_tot, 0)                        AS huevo_tot,
           COALESCE(f.huevo_inc, 0)                        AS huevo_inc,
           f.peso_h::double precision                      AS peso_h,
           f.peso_m::double precision                      AS peso_m
      FROM lote_ok lo
      CROSS JOIN LATERAL fn_seguimiento_diario_produccion(lo.lote_postura_produccion_id, NULL) f
     WHERE f.seg_id IS NOT NULL
       AND NOT f.fila_sin_lpp   -- v2 fn diaria: los dias solo-traslado TSD no son "dia con registro"
),
-- ── 4) Semana de vida (división entera, arranque en 25) ─────────────────────
reg AS (
    SELECT d.*,
           ((d.reg_date - lo.ref_date) / 7) + 1 AS sem
      FROM dedup d
      JOIN lote_ok lo ON lo.lote_postura_produccion_id = d.lpp_id
),
reg_ok AS (
    SELECT * FROM reg WHERE sem >= 25
),
-- ── 5) Agregado semanal por lote ────────────────────────────────────────────
sem AS (
    SELECT lpp_id,
           sem,
           COUNT(*)::int                     AS dias,
           SUM(mort_h)::double precision     AS mort_h,
           SUM(mort_m)::double precision     AS mort_m,
           SUM(sel_h)::double precision      AS sel_h,
           SUM(cons_h)                       AS cons_kg_h,
           SUM(cons_m)                       AS cons_kg_m,
           SUM(huevo_tot)::double precision  AS huevo_tot,
           SUM(huevo_inc)::double precision  AS huevo_inc,
           AVG(peso_h) FILTER (WHERE peso_h IS NOT NULL) AS peso_h,
           AVG(peso_m) FILTER (WHERE peso_m IS NOT NULL) AS peso_m
      FROM reg_ok
     GROUP BY lpp_id, sem
),
-- ── 6) Acumulados por ventana ──────────────────────────────────────────────
--    `*_prev` = acumulado EXCLUYENDO la semana actual ⇒ saldo al INICIO de la
--    semana, que es el denominador de todos los % (igual que la fn base).
acum AS (
    SELECT s.*,
           COALESCE(SUM(s.mort_h + s.sel_h) OVER (
               PARTITION BY s.lpp_id ORDER BY s.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING), 0) AS bajas_h_prev,
           COALESCE(SUM(s.mort_m) OVER (
               PARTITION BY s.lpp_id ORDER BY s.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING), 0) AS bajas_m_prev,
           SUM(s.mort_h + s.sel_h) OVER (
               PARTITION BY s.lpp_id ORDER BY s.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)     AS bajas_h_ac,
           SUM(s.mort_m) OVER (
               PARTITION BY s.lpp_id ORDER BY s.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)     AS bajas_m_ac,
           SUM(s.huevo_tot) OVER (
               PARTITION BY s.lpp_id ORDER BY s.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)     AS huevo_tot_ac,
           SUM(s.huevo_inc) OVER (
               PARTITION BY s.lpp_id ORDER BY s.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)     AS huevo_inc_ac
      FROM sem s
),
-- ── 7) Solo la semana calendario pedida ────────────────────────────────────
sem_objetivo AS (
    SELECT lo.lote_postura_produccion_id, lo.lote_id, lo.lote_nombre, lo.granja_id, lo.granja_nombre,
           lo.nucleo_nombre, lo.regional, lo.raza, lo.anio_guia,
           lo.ciclo_produccion, lo.tipo_nido, lo.base_h, lo.base_m,
           a.sem, a.dias, a.mort_h, a.mort_m, a.sel_h,
           a.cons_kg_h, a.cons_kg_m, a.huevo_tot, a.huevo_inc,
           a.peso_h, a.peso_m,
           a.bajas_h_prev, a.bajas_m_prev, a.bajas_h_ac, a.bajas_m_ac,
           a.huevo_tot_ac, a.huevo_inc_ac,
           (lo.ref_date + ((a.sem - 1) * 7) + 6) AS fin_sem
      FROM acum a
      JOIN lote_ok lo ON lo.lote_postura_produccion_id = a.lpp_id
     WHERE EXTRACT(YEAR FROM (lo.ref_date + ((a.sem - 1) * 7) + 6))::int = p_anio
       -- p_sem_anio NULL = TODAS las semanas del año (curva del año completo);
       -- con valor, una sola semana calendario.
       AND (p_sem_anio IS NULL OR (
             floor(
               ( (lo.ref_date + ((a.sem - 1) * 7) + 6)
                 - date_trunc('year', (lo.ref_date + ((a.sem - 1) * 7) + 6)::timestamp)::date
                 + EXTRACT(DOW FROM date_trunc('year', (lo.ref_date + ((a.sem - 1) * 7) + 6)::timestamp))::int
               ) / 7.0
             )::int + 1
           ) = p_sem_anio)
       AND (p_regional IS NULL OR lo.regional = p_regional)
),
-- ── 8) Guía ────────────────────────────────────────────────────────────────
con_guia AS (
    SELECT so.*,
           f_safe_numeric(g.prod_porcentaje) AS g_prod_pct,
           f_safe_numeric(g.h_total_aa)      AS g_htaa,
           f_safe_numeric(g.h_inc_aa)        AS g_hiaa,
           f_safe_numeric(g.aprov_sem)       AS g_aprov_sem,
           f_safe_numeric(g.retiro_ac_h)     AS g_retiro_ac_h,
           f_safe_numeric(g.retiro_ac_m)     AS g_retiro_ac_m
      FROM sem_objetivo so
      LEFT JOIN LATERAL (
            SELECT gg.*
              FROM guia_genetica_sanmarino_colombia gg
             WHERE gg.company_id = p_company_id
               AND gg.deleted_at IS NULL
               AND lower(trim(gg.raza)) = lower(trim(COALESCE(so.raza, '')))
               AND trim(gg.anio_guia) = so.anio_guia::text
               -- ⚠️ Edad PARSEADA a número, igual que fn_indicadores_produccion_postura
               --    (`fn_parse_edad_numerica(g.edad) = s`) — distinto de la fn de
               --    levante, que compara texto exacto.
               --    La semana 25 tiene DOS filas en la guía y ambas parsean a 25:
               --      '25'  → cierre de LEVANTE   (retiro_ac_h 4,03)
               --      '25P' → arranque de PRODUCCIÓN (retiro_ac_h 0,10)
               --    La fn base resuelve con LIMIT 1 sin ORDER BY y el plan (index
               --    scan por company_id) le devuelve la fila '25P'. Acá el desempate
               --    se hace EXPLÍCITO —prefiriendo la variante con sufijo, que es la
               --    de producción— para no depender del plan de ejecución.
               AND fn_parse_edad_numerica(gg.edad) = so.sem
             ORDER BY (CASE WHEN btrim(gg.edad) = so.sem::text THEN 1 ELSE 0 END), gg.id
             LIMIT 1
      ) g ON true
),
-- ── 9) Saldos, pesos normalizados y derivadas ──────────────────────────────
calc AS (
    SELECT cg.*,
           GREATEST(0, cg.base_h - cg.bajas_h_prev) AS ini_h,
           GREATEST(0, cg.base_m - cg.bajas_m_prev) AS ini_m,
           GREATEST(0, cg.base_h - cg.bajas_h_ac)   AS fin_h,
           GREATEST(0, cg.base_m - cg.bajas_m_ac)   AS fin_m,
           CASE WHEN cg.peso_h IS NULL THEN NULL
                WHEN cg.peso_h > 100 THEN cg.peso_h / 1000.0 ELSE cg.peso_h END AS peso_h_kg,
           CASE WHEN cg.peso_m IS NULL THEN NULL
                WHEN cg.peso_m > 100 THEN cg.peso_m / 1000.0 ELSE cg.peso_m END AS peso_m_kg
      FROM con_guia cg
),
final AS (
    SELECT c.*,
           CASE WHEN c.dias > 0 AND c.ini_h > 0
                THEN (c.huevo_tot / c.dias) / c.ini_h * 100.0 END        AS prod_pct,
           CASE WHEN c.base_h > 0 THEN c.huevo_tot_ac / c.base_h END     AS htaa_real,
           CASE WHEN c.base_h > 0 THEN c.huevo_inc_ac / c.base_h END     AS hiaa_real,
           CASE WHEN c.huevo_tot > 0 THEN c.huevo_inc * 100.0 / c.huevo_tot END AS aprov_pct,
           CASE WHEN c.huevo_inc > 0
                THEN (c.cons_kg_h + c.cons_kg_m) * 1000.0 / c.huevo_inc END AS gr_huevo_inc_real
      FROM calc c
)
SELECT
    f.lote_postura_produccion_id,
    f.lote_id,
    f.lote_nombre,
    f.granja_id,
    f.granja_nombre,
    f.nucleo_nombre,
    f.regional,
    f.raza,
    f.anio_guia,
    f.ciclo_produccion,
    f.tipo_nido,
    f.sem                                                          AS edad_semana,
    f.fin_sem                                                      AS fecha_fin_semana,
    f.dias                                                         AS dias_con_registro,
    -- Participación sobre el TOTAL del resultado (misma ventana que la migración desplegada
    -- 20260728120000; el .sql llegó a tener un PARTITION BY fin_sem nunca migrado que dejaba
    -- part=1 con lotes de encaset distinto — realineado al comportamiento vivo). El C# además
    -- recalcula PART tras el recorte por alcance (ResumenSemanalRaPesadasCalculos).
    CASE WHEN SUM(f.fin_h) OVER () > 0
         THEN f.fin_h / SUM(f.fin_h) OVER () END                     AS part,
    f.fin_h                                                        AS saldo_hembras,
    f.fin_m                                                        AS saldo_machos,
    f.prod_pct                                                     AS produccion_pct,
    f.g_prod_pct::double precision                                 AS produccion_pct_guia,
    fn_dif_pct(f.prod_pct, f.g_prod_pct::double precision)         AS dif_produccion_pct,
    f.htaa_real                                                    AS htaa,
    f.g_htaa::double precision                                     AS htaa_guia,
    fn_dif_pct(f.htaa_real, f.g_htaa::double precision)            AS dif_htaa,
    f.hiaa_real                                                    AS hiaa,
    f.g_hiaa::double precision                                     AS hiaa_guia,
    fn_dif_pct(f.hiaa_real, f.g_hiaa::double precision)            AS dif_hiaa,
    f.aprov_pct                                                    AS aprov_sem_pct,
    f.g_aprov_sem::double precision                                AS aprov_sem_pct_guia,
    fn_dif_pct(f.aprov_pct, f.g_aprov_sem::double precision)       AS dif_aprov_sem_pct,
    f.gr_huevo_inc_real                                            AS gr_huevo_inc,
    CASE WHEN f.ini_h > 0 THEN f.mort_h / f.ini_h * 100.0 END      AS mort_hembras_pct,
    CASE WHEN f.base_h > 0 THEN f.bajas_h_ac / f.base_h * 100.0 END AS retiro_acum_hembras_pct,
    f.g_retiro_ac_h::double precision                              AS retiro_acum_hembras_guia,
    CASE WHEN f.ini_m > 0 THEN f.mort_m / f.ini_m * 100.0 END      AS mort_machos_pct,
    CASE WHEN f.base_m > 0 THEN f.bajas_m_ac / f.base_m * 100.0 END AS retiro_acum_machos_pct,
    f.g_retiro_ac_m::double precision                              AS retiro_acum_machos_guia,
    CASE WHEN COALESCE(f.peso_h_kg, 0) > 0 AND f.peso_m_kg IS NOT NULL
         THEN f.peso_m_kg / f.peso_h_kg * 100.0 END                AS peso_macho_sobre_hembra
  FROM final f
 ORDER BY f.sem DESC, f.lote_nombre;
$$;

COMMENT ON FUNCTION fn_resumen_semanal_ra_pesadas_produccion(integer, integer, integer, integer[], text, text)
IS 'Informe RA Pesadas — hoja RESUMEN SEMANAL, bloque Producción: una fila por lote para una semana calendario (WEEKNUM Excel). Set-based; equivalente 1:1 a fn_indicadores_produccion_postura (flujo lote).';
""";

        private const string FnDiariaPrev = """
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- fn_seguimiento_diario_produccion — grilla diaria CANÓNICA de producción (postura)
-- ═══════════════════════════════════════════════════════════════════════════════════════
-- v1 (2026-08-01) — creación (plan fase_de_desarrollo/seguimiento_produccion_fn_canonica_plan.md)
--
--   Patrón fn_seguimiento_diario_engorde (v13): LANGUAGE sql STABLE a PROPÓSITO — el
--   inlining en CROSS JOIN LATERAL es real (plpgsql = Function Scan, ×2.8 más lento medido
--   en engorde) y plpgsql RETURN QUERY no aplica assignment casts (SUM(int)→bigint vs INT
--   = error 42804). Por eso TODOS los agregados del SELECT final van casteados explícitos.
--
--   ÚNICA FÓRMULA de los números diarios de producción (regla del repo «una sola fórmula
--   por número»): fila diaria cruda + derivados (saldo de aves del día, acumulados de
--   huevos, % postura hen-day diario). El espejo C# de especificación ejecutable es
--   Application/Calculos/SeguimientoDiarioProduccionCalculos.cs (tests xUnit = contrato).
--
--   Decisiones de diseño (D1-D4 del plan, confirmadas):
--   • Universo = días con seguimiento (dedup) ∪ días con movimientos de aves (filas
--     movimiento-only con seg_id NULL, patrón engorde v7: una venta tardía genera su fila
--     y el saldo del lote la refleja).
--   • Fuente dual + dedup por día Bogotá con «gana el timestamp más temprano»: MISMO bloque
--     que fn_indicadores_produccion_postura / fn_clasificacion_huevo_items_produccion /
--     fn_resumen_semanal_ra_pesadas_produccion (el registro puede vivir en la tabla
--     canónica seguimiento_diario_produccion o en la legacy seguimiento_diario_levante
--     con tipo_seguimiento='produccion'; hoy la legacy tiene 0 filas de producción).
--   • SALDO DE AVES (D4): GREATEST(0, base − Σ(mort+sel+ERR) − Σ mov_out + Σ mov_in),
--     CON error de sexaje — semántica de los escritores incrementales
--     (SeguimientoProduccionService.AplicarDescuentoLppAsync y fn_migracion paso 3).
--     base = COALESCE(lpp.aves_h_inicial, lpp.hembras_iniciales_prod, 0) (misma prioridad
--     que ObtenerInformacionLoteAsync). mov_out = movimiento_aves Completado no borrado con
--     el lote como ORIGEN (Venta+Traslado+Retiro, cualquier tipo — igual que el GET);
--     mov_in = tipo Traslado con el lote como DESTINO. lote_postura_produccion.aves_*_actual
--     es DERIVADO verificable, jamás fuente.
--   • FILTRO DE FASE de los movimientos (divergencia DELIBERADA vs el GET viejo): solo se
--     cuentan movimientos con fecha >= lpp.fecha_inicio_produccion (día Bogotá). Los
--     anteriores pertenecen al LEVANTE y ya están reflejados en aves_h_inicial del LPP
--     (las aves iniciales de producción = aves vivas al cierre del levante): contarlos de
--     nuevo los duplicaba. Caso real: lote 130 — el GET viejo daba 8.646 H (restaba otra
--     vez la venta 100 + salida 500 − ingreso 200 del levante); el valor correcto validado
--     por el E2E de carga masiva es 9.039. Con fecha_inicio_produccion NULL no se filtra
--     (comportamiento del GET, conservador).
--   • Semana de vida CRUDA ((fecha − ref)/7)+1 SIN piso 26 ni corte 25: el corte es del
--     consumidor (los indicadores cortan en 25; la clasificación por ítems de Santa Reyes
--     deliberadamente NO corta). ref = COALESCE(lev.fecha_encaset, lpp.fecha_encaset,
--     lpp.fecha_inicio_produccion) en día Bogotá (idéntico a las 3 fns semanales).
--   • Rama LPP filtra por lote_postura_produccion_id (paridad con la grilla y las fns de
--     hoy) ⇒ las filas de traslado TSD con lpp NULL siguen fuera de esta rama (deuda
--     documentada; su efecto en el saldo entra por movimiento_aves, que sí las audita).
--   • Rama legacy (p_lote_id): el C# ya resuelve el lote hijo en fase Produccion; acá solo
--     se listan sus filas. Sin LPP no hay base de aves ⇒ saldos NULL (no 0: GREATEST
--     ignora NULLs, por eso el CASE explícito).
--   • Corte de día SIEMPRE AT TIME ZONE 'America/Bogota'; jamás date_trunc dependiente de
--     la TZ de sesión ni ::date directo sobre timestamptz.
--
--   Consumidores: grilla GET /api/Produccion/seguimiento (SqlQueryRaw, snake_case),
--   informacion-lote (saldo del último día), y las fns semanales re-sourced sobre esta.
--
--   Firma: exactamente UNO de los dos parámetros debe venir no-NULL.
-- ═══════════════════════════════════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION fn_seguimiento_diario_produccion(
    p_lote_postura_produccion_id INT,
    p_lote_id                    INT
)
RETURNS TABLE (
    -- Identificación
    seg_id                      BIGINT,       -- NULL = fila movimiento-only (sin registro diario)
    fecha                       DATE,
    fecha_ts                    TIMESTAMPTZ,  -- timestamp original del registro (NULL en movimiento-only)
    fuente                      TEXT,         -- 'sdp' | 'sdl' (legacy) | 'mov'
    lote_id                     INT,
    lote_postura_produccion_id  INT,
    company_id                  INT,
    -- Tiempo
    edad_dias                   INT,
    semana                      INT,
    -- Aves crudas del registro
    mortalidad_hembras          INT,
    mortalidad_machos           INT,
    sel_h                       INT,
    sel_m                       INT,
    error_sexaje_hembras        INT,
    error_sexaje_machos         INT,
    -- Consumo
    cons_kg_h                   DOUBLE PRECISION,
    cons_kg_m                   DOUBLE PRECISION,
    consumo_total_kg            DOUBLE PRECISION,
    tipo_alimento               TEXT,
    -- Huevos crudos
    huevo_tot                   INT,
    huevo_inc                   INT,
    huevo_limpio                INT,
    huevo_tratado               INT,
    huevo_sucio                 INT,
    huevo_deforme               INT,
    huevo_blanco                INT,
    huevo_doble_yema            INT,
    huevo_piso                  INT,
    huevo_pequeno               INT,
    huevo_roto                  INT,
    huevo_desecho               INT,
    huevo_otro                  INT,
    peso_huevo                  DOUBLE PRECISION,
    -- Derivados de huevos
    huevo_tot_acum              BIGINT,
    huevo_inc_acum              BIGINT,
    pct_postura_dia             DOUBLE PRECISION,  -- hen-day diario: huevo_tot / aves_h_inicio_dia * 100
    -- Movimientos de aves del día (desde movimiento_aves Completado)
    mov_venta_h                 INT,
    mov_venta_m                 INT,
    mov_retiro_h                INT,
    mov_retiro_m                INT,
    mov_traslado_in_h           INT,
    mov_traslado_in_m           INT,
    mov_traslado_out_h          INT,
    mov_traslado_out_m          INT,
    -- Saldo de aves (D4: con error de sexaje; NULL en rama legacy sin LPP)
    aves_h_inicio_dia           INT,
    aves_m_inicio_dia           INT,
    saldo_aves_h                INT,
    saldo_aves_m                INT,
    -- Traslado crudo de la fila diaria
    es_traslado                 BOOLEAN,
    traslado_direccion          TEXT,
    traslado_ingreso_hembras    INT,
    traslado_ingreso_machos     INT,
    traslado_salida_hembras     INT,
    traslado_salida_machos      INT,
    lote_destino_id             INT,
    granja_destino_id           INT,
    -- Pesaje (peso_h/m, uniformidad y CV del lote son NUMERIC — mismos tipos que la tabla,
    -- para que el C# los lea como decimal EXACTO, sin pasar por float8)
    peso_h                      NUMERIC,
    peso_m                      NUMERIC,
    uniformidad                 NUMERIC,
    coeficiente_variacion       NUMERIC,
    uniformidad_hembras         DOUBLE PRECISION,
    uniformidad_machos          DOUBLE PRECISION,
    cv_hembras                  DOUBLE PRECISION,
    cv_machos                   DOUBLE PRECISION,
    observaciones_pesaje        TEXT,
    -- Agua
    consumo_agua_diario         DOUBLE PRECISION,
    consumo_agua_ph             DOUBLE PRECISION,
    consumo_agua_orp            DOUBLE PRECISION,
    consumo_agua_temperatura    DOUBLE PRECISION,
    -- Otros
    etapa                       INT,
    ciclo                       TEXT,
    observaciones               TEXT,
    metadata                    JSONB,
    created_by_user_id          INT,
    created_at                  TIMESTAMPTZ,
    updated_at                  TIMESTAMPTZ
)
LANGUAGE sql STABLE
AS $$
WITH ctx AS (
    -- ── Rama LPP: base de aves + fecha de referencia (idéntica a fn_indicadores) ──
    SELECT lpp.lote_postura_produccion_id                            AS ctx_lpp_id,
           lpp.lote_id                                               AS ctx_lote_id,
           lpp.company_id                                            AS ctx_company,
           COALESCE(lpp.aves_h_inicial, lpp.hembras_iniciales_prod, 0) AS base_h,
           COALESCE(lpp.aves_m_inicial, lpp.machos_iniciales_prod, 0)  AS base_m,
           (COALESCE(lev.fecha_encaset, lpp.fecha_encaset, lpp.fecha_inicio_produccion)
               AT TIME ZONE 'America/Bogota')::date                  AS ref_date,
           (lpp.fecha_inicio_produccion
               AT TIME ZONE 'America/Bogota')::date                  AS mov_desde,
           true                                                      AS es_lpp
      FROM lote_postura_produccion lpp
      LEFT JOIN lote_postura_levante lev
             ON lev.lote_postura_levante_id = lpp.lote_postura_levante_id
            AND lev.deleted_at IS NULL
     WHERE p_lote_postura_produccion_id IS NOT NULL
       AND lpp.lote_postura_produccion_id = p_lote_postura_produccion_id
       AND lpp.deleted_at IS NULL
    UNION ALL
    -- ── Rama legacy: p_lote_id ya resuelto por el C# (lote hijo en fase Produccion o lote
    --    crudo). LEFT JOIN para no perder filas huérfanas cuyo lote no existe en lotes
    --    (paridad con la grilla actual, que igual las devuelve). Sin base de aves.
    SELECT NULL::int, p_lote_id, lo.company_id,
           NULL::int, NULL::int,
           (COALESCE(lo.fecha_inicio_produccion, pa.fecha_encaset, lo.fecha_encaset)
               AT TIME ZONE 'America/Bogota')::date,
           NULL::date,
           false
      FROM (SELECT 1) uno
      LEFT JOIN lotes lo ON lo.lote_id = p_lote_id AND lo.deleted_at IS NULL
      LEFT JOIN lotes pa ON pa.lote_id = lo.lote_padre_id AND pa.deleted_at IS NULL
     WHERE p_lote_postura_produccion_id IS NULL
       AND p_lote_id IS NOT NULL
),
-- ── Fuente dual + dedup por día Bogotá (bloque canónico de las fns semanales) ──
crudos AS (
    SELECT sd.id::bigint                                  AS c_seg_id,
           'sdl'::text                                    AS c_fuente,
           sd.fecha                                       AS c_ts,
           COALESCE(sd.mortalidad_hembras, 0)             AS c_mort_h,
           COALESCE(sd.mortalidad_machos, 0)              AS c_mort_m,
           COALESCE(sd.sel_h, 0)                          AS c_sel_h,
           COALESCE(sd.sel_m, 0)                          AS c_sel_m,
           COALESCE(sd.error_sexaje_hembras, 0)           AS c_err_h,
           COALESCE(sd.error_sexaje_machos, 0)            AS c_err_m,
           COALESCE(sd.consumo_kg_hembras, 0)::float8     AS c_cons_h,
           COALESCE(sd.consumo_kg_machos, 0)::float8      AS c_cons_m,
           sd.tipo_alimento::text                         AS c_tipo_alimento,
           COALESCE(sd.huevo_tot, 0)                      AS c_huevo_tot,
           COALESCE(sd.huevo_inc, 0)                      AS c_huevo_inc,
           COALESCE(sd.huevo_limpio, 0)                   AS c_h_limpio,
           COALESCE(sd.huevo_tratado, 0)                  AS c_h_tratado,
           COALESCE(sd.huevo_sucio, 0)                    AS c_h_sucio,
           COALESCE(sd.huevo_deforme, 0)                  AS c_h_deforme,
           COALESCE(sd.huevo_blanco, 0)                   AS c_h_blanco,
           COALESCE(sd.huevo_doble_yema, 0)               AS c_h_doble,
           COALESCE(sd.huevo_piso, 0)                     AS c_h_piso,
           COALESCE(sd.huevo_pequeno, 0)                  AS c_h_pequeno,
           COALESCE(sd.huevo_roto, 0)                     AS c_h_roto,
           COALESCE(sd.huevo_desecho, 0)                  AS c_h_desecho,
           COALESCE(sd.huevo_otro, 0)                     AS c_h_otro,
           sd.peso_huevo::float8                          AS c_peso_huevo,
           sd.es_traslado                                 AS c_es_traslado,
           sd.traslado_direccion::text                    AS c_tras_dir,
           COALESCE(sd.traslado_ingreso_hembras, 0)       AS c_tras_in_h,
           COALESCE(sd.traslado_ingreso_machos, 0)        AS c_tras_in_m,
           COALESCE(sd.traslado_salida_hembras, 0)        AS c_tras_out_h,
           COALESCE(sd.traslado_salida_machos, 0)         AS c_tras_out_m,
           NULL::int                                      AS c_lote_destino_id,
           NULL::int                                      AS c_granja_destino_id,
           sd.peso_h                                      AS c_peso_h,
           sd.peso_m                                      AS c_peso_m,
           sd.uniformidad                                 AS c_unif,
           sd.coeficiente_variacion                       AS c_cv,
           sd.uniformidad_hembras::float8                 AS c_unif_h,
           sd.uniformidad_machos::float8                  AS c_unif_m,
           sd.cv_hembras::float8                          AS c_cv_h,
           sd.cv_machos::float8                           AS c_cv_m,
           sd.observaciones_pesaje                        AS c_obs_pesaje,
           sd.consumo_agua_diario                         AS c_agua,
           sd.consumo_agua_ph                             AS c_agua_ph,
           sd.consumo_agua_orp                            AS c_agua_orp,
           sd.consumo_agua_temperatura                    AS c_agua_temp,
           sd.etapa                                       AS c_etapa,
           sd.ciclo::text                                 AS c_ciclo,
           sd.observaciones                               AS c_observaciones,
           sd.metadata                                    AS c_metadata,
           NULL::int                                      AS c_created_by,  -- legacy: varchar, no casteable
           sd.created_at                                  AS c_created_at,
           sd.updated_at                                  AS c_updated_at,
           NULL::int                                      AS c_company_id,
           sd.lote_postura_produccion_id                  AS c_lpp
      FROM seguimiento_diario_levante sd
     WHERE sd.tipo_seguimiento = 'produccion'
       AND ( (p_lote_postura_produccion_id IS NOT NULL
                AND sd.lote_postura_produccion_id = p_lote_postura_produccion_id)
          OR (p_lote_postura_produccion_id IS NULL
                AND sd.lote_id = p_lote_id::text) )
    UNION ALL
    SELECT sp.id::bigint,
           'sdp'::text,
           sp.fecha_registro,
           COALESCE(sp.mortalidad_hembras, 0),
           COALESCE(sp.mortalidad_machos, 0),
           COALESCE(sp.sel_h, 0),
           COALESCE(sp.sel_m, 0),
           COALESCE(sp.error_sexaje_hembras, 0),
           COALESCE(sp.error_sexaje_machos, 0),
           COALESCE(sp.cons_kg_h, 0)::float8,
           COALESCE(sp.cons_kg_m, 0)::float8,
           sp.tipo_alimento,
           COALESCE(sp.huevo_tot, 0),
           COALESCE(sp.huevo_inc, 0),
           COALESCE(sp.huevo_limpio, 0),
           COALESCE(sp.huevo_tratado, 0),
           COALESCE(sp.huevo_sucio, 0),
           COALESCE(sp.huevo_deforme, 0),
           COALESCE(sp.huevo_blanco, 0),
           COALESCE(sp.huevo_doble_yema, 0),
           COALESCE(sp.huevo_piso, 0),
           COALESCE(sp.huevo_pequeno, 0),
           COALESCE(sp.huevo_roto, 0),
           COALESCE(sp.huevo_desecho, 0),
           COALESCE(sp.huevo_otro, 0),
           sp.peso_huevo,
           sp.es_traslado,
           sp.traslado_direccion::text,
           COALESCE(sp.traslado_ingreso_hembras, 0),
           COALESCE(sp.traslado_ingreso_machos, 0),
           COALESCE(sp.traslado_salida_hembras, 0),
           COALESCE(sp.traslado_salida_machos, 0),
           sp.lote_destino_id,
           sp.granja_destino_id,
           sp.peso_h,
           sp.peso_m,
           sp.uniformidad,
           sp.coeficiente_variacion,
           sp.uniformidad_hembras,
           sp.uniformidad_machos,
           sp.cv_hembras,
           sp.cv_machos,
           sp.observaciones_pesaje,
           sp.consumo_agua_diario,
           sp.consumo_agua_ph,
           sp.consumo_agua_orp,
           sp.consumo_agua_temperatura,
           sp.etapa,
           sp.ciclo::text,
           sp.observaciones,
           sp.metadata,
           sp.created_by_user_id,
           sp.created_at,
           sp.updated_at,
           sp.company_id,
           sp.lote_postura_produccion_id
      FROM seguimiento_diario_produccion sp
     WHERE ( (p_lote_postura_produccion_id IS NOT NULL
                AND sp.lote_postura_produccion_id = p_lote_postura_produccion_id)
          OR (p_lote_postura_produccion_id IS NULL
                AND sp.lote_id = p_lote_id) )
),
seg_dias AS (
    SELECT DISTINCT ON ((c.c_ts AT TIME ZONE 'America/Bogota')::date)
           c.*,
           (c.c_ts AT TIME ZONE 'America/Bogota')::date AS reg_date
      FROM crudos c
     ORDER BY (c.c_ts AT TIME ZONE 'America/Bogota')::date, c.c_ts
),
-- ── Movimientos de aves (solo rama LPP con lote base) — misma población que el GET
--    informacion-lote: Completado, no borrado, misma empresa; salidas = CUALQUIER tipo con
--    el lote como origen; entradas = tipo Traslado con el lote como destino. ──
movs AS (
    SELECT (m.fecha_movimiento AT TIME ZONE 'America/Bogota')::date AS mov_date,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id
                THEN COALESCE(m.cantidad_hembras, 0) ELSE 0 END AS out_h,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id
                THEN COALESCE(m.cantidad_machos, 0) ELSE 0 END AS out_m,
           CASE WHEN m.tipo_movimiento = 'Traslado' AND m.lote_destino_id = c.ctx_lote_id
                THEN COALESCE(m.cantidad_hembras, 0) ELSE 0 END AS in_h,
           CASE WHEN m.tipo_movimiento = 'Traslado' AND m.lote_destino_id = c.ctx_lote_id
                THEN COALESCE(m.cantidad_machos, 0) ELSE 0 END AS in_m,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Venta'
                THEN COALESCE(m.cantidad_hembras, 0) ELSE 0 END AS venta_h,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Venta'
                THEN COALESCE(m.cantidad_machos, 0) ELSE 0 END AS venta_m,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Retiro'
                THEN COALESCE(m.cantidad_hembras, 0) ELSE 0 END AS retiro_h,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Retiro'
                THEN COALESCE(m.cantidad_machos, 0) ELSE 0 END AS retiro_m,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Traslado'
                THEN COALESCE(m.cantidad_hembras, 0) ELSE 0 END AS tout_h,
           CASE WHEN m.lote_origen_id = c.ctx_lote_id AND m.tipo_movimiento = 'Traslado'
                THEN COALESCE(m.cantidad_machos, 0) ELSE 0 END AS tout_m
      FROM movimiento_aves m
      JOIN ctx c ON c.es_lpp AND c.ctx_lote_id IS NOT NULL
     WHERE m.estado = 'Completado'
       AND m.deleted_at IS NULL
       AND m.company_id = c.ctx_company
       AND (m.lote_origen_id = c.ctx_lote_id OR m.lote_destino_id = c.ctx_lote_id)
       -- Filtro de FASE: los movimientos previos al inicio de producción son del levante y
       -- ya viven en aves_h_inicial (ver changelog v1)
       AND (c.mov_desde IS NULL
            OR (m.fecha_movimiento AT TIME ZONE 'America/Bogota')::date >= c.mov_desde)
),
movs_dia AS (
    SELECT mv.mov_date,
           SUM(mv.out_h)::int    AS out_h,
           SUM(mv.out_m)::int    AS out_m,
           SUM(mv.in_h)::int     AS in_h,
           SUM(mv.in_m)::int     AS in_m,
           SUM(mv.venta_h)::int  AS venta_h,
           SUM(mv.venta_m)::int  AS venta_m,
           SUM(mv.retiro_h)::int AS retiro_h,
           SUM(mv.retiro_m)::int AS retiro_m,
           SUM(mv.tout_h)::int   AS tout_h,
           SUM(mv.tout_m)::int   AS tout_m
      FROM movs mv
     GROUP BY mv.mov_date
),
-- ── Universo: días con seguimiento ∪ días solo-movimiento (FULL JOIN por día) ──
universo AS (
    SELECT COALESCE(s.reg_date, md.mov_date) AS u_fecha,
           s.*,
           md.out_h    AS m_out_h,
           md.out_m    AS m_out_m,
           md.in_h     AS m_in_h,
           md.in_m     AS m_in_m,
           md.venta_h  AS m_venta_h,
           md.venta_m  AS m_venta_m,
           md.retiro_h AS m_retiro_h,
           md.retiro_m AS m_retiro_m,
           md.tout_h   AS m_tout_h,
           md.tout_m   AS m_tout_m
      FROM seg_dias s
      FULL OUTER JOIN movs_dia md ON md.mov_date = s.reg_date
)
SELECT
    u.c_seg_id                                                        AS seg_id,
    u.u_fecha                                                         AS fecha,
    u.c_ts                                                            AS fecha_ts,
    COALESCE(u.c_fuente, 'mov')                                       AS fuente,
    c.ctx_lote_id                                                     AS lote_id,
    COALESCE(u.c_lpp, c.ctx_lpp_id)                                   AS lote_postura_produccion_id,
    COALESCE(u.c_company_id, c.ctx_company)                           AS company_id,
    CASE WHEN c.ref_date IS NULL THEN NULL
         ELSE GREATEST(0, u.u_fecha - c.ref_date) END::int            AS edad_dias,
    CASE WHEN c.ref_date IS NULL THEN NULL
         ELSE ((u.u_fecha - c.ref_date) / 7) + 1 END::int             AS semana,
    u.c_mort_h                                                        AS mortalidad_hembras,
    u.c_mort_m                                                        AS mortalidad_machos,
    u.c_sel_h                                                         AS sel_h,
    u.c_sel_m                                                         AS sel_m,
    u.c_err_h                                                         AS error_sexaje_hembras,
    u.c_err_m                                                         AS error_sexaje_machos,
    u.c_cons_h                                                        AS cons_kg_h,
    u.c_cons_m                                                        AS cons_kg_m,
    (COALESCE(u.c_cons_h, 0) + COALESCE(u.c_cons_m, 0))::float8       AS consumo_total_kg,
    u.c_tipo_alimento                                                 AS tipo_alimento,
    u.c_huevo_tot                                                     AS huevo_tot,
    u.c_huevo_inc                                                     AS huevo_inc,
    u.c_h_limpio                                                      AS huevo_limpio,
    u.c_h_tratado                                                     AS huevo_tratado,
    u.c_h_sucio                                                       AS huevo_sucio,
    u.c_h_deforme                                                     AS huevo_deforme,
    u.c_h_blanco                                                      AS huevo_blanco,
    u.c_h_doble                                                       AS huevo_doble_yema,
    u.c_h_piso                                                        AS huevo_piso,
    u.c_h_pequeno                                                     AS huevo_pequeno,
    u.c_h_roto                                                        AS huevo_roto,
    u.c_h_desecho                                                     AS huevo_desecho,
    u.c_h_otro                                                        AS huevo_otro,
    u.c_peso_huevo                                                    AS peso_huevo,
    SUM(COALESCE(u.c_huevo_tot, 0)) OVER w_ord::bigint                AS huevo_tot_acum,
    SUM(COALESCE(u.c_huevo_inc, 0)) OVER w_ord::bigint                AS huevo_inc_acum,
    CASE
        WHEN c.base_h IS NULL THEN NULL
        WHEN GREATEST(0, c.base_h
                - COALESCE(SUM(u.c_mort_h + u.c_sel_h + u.c_err_h) OVER w_prev, 0)
                - COALESCE(SUM(COALESCE(u.m_out_h, 0)) OVER w_prev, 0)
                + COALESCE(SUM(COALESCE(u.m_in_h, 0)) OVER w_prev, 0)) > 0
            THEN (100.0 * COALESCE(u.c_huevo_tot, 0)
                / GREATEST(0, c.base_h
                    - COALESCE(SUM(u.c_mort_h + u.c_sel_h + u.c_err_h) OVER w_prev, 0)
                    - COALESCE(SUM(COALESCE(u.m_out_h, 0)) OVER w_prev, 0)
                    + COALESCE(SUM(COALESCE(u.m_in_h, 0)) OVER w_prev, 0)))
        ELSE 0
    END::float8                                                       AS pct_postura_dia,
    COALESCE(u.m_venta_h, 0)                                          AS mov_venta_h,
    COALESCE(u.m_venta_m, 0)                                          AS mov_venta_m,
    COALESCE(u.m_retiro_h, 0)                                         AS mov_retiro_h,
    COALESCE(u.m_retiro_m, 0)                                         AS mov_retiro_m,
    COALESCE(u.m_in_h, 0)                                             AS mov_traslado_in_h,
    COALESCE(u.m_in_m, 0)                                             AS mov_traslado_in_m,
    COALESCE(u.m_tout_h, 0)                                           AS mov_traslado_out_h,
    COALESCE(u.m_tout_m, 0)                                           AS mov_traslado_out_m,
    CASE WHEN c.base_h IS NULL THEN NULL
         ELSE GREATEST(0, c.base_h
                - COALESCE(SUM(u.c_mort_h + u.c_sel_h + u.c_err_h) OVER w_prev, 0)
                - COALESCE(SUM(COALESCE(u.m_out_h, 0)) OVER w_prev, 0)
                + COALESCE(SUM(COALESCE(u.m_in_h, 0)) OVER w_prev, 0)) END::int AS aves_h_inicio_dia,
    CASE WHEN c.base_m IS NULL THEN NULL
         ELSE GREATEST(0, c.base_m
                - COALESCE(SUM(u.c_mort_m + u.c_sel_m + u.c_err_m) OVER w_prev, 0)
                - COALESCE(SUM(COALESCE(u.m_out_m, 0)) OVER w_prev, 0)
                + COALESCE(SUM(COALESCE(u.m_in_m, 0)) OVER w_prev, 0)) END::int AS aves_m_inicio_dia,
    CASE WHEN c.base_h IS NULL THEN NULL
         ELSE GREATEST(0, c.base_h
                - COALESCE(SUM(u.c_mort_h + u.c_sel_h + u.c_err_h) OVER w_ord, 0)
                - COALESCE(SUM(COALESCE(u.m_out_h, 0)) OVER w_ord, 0)
                + COALESCE(SUM(COALESCE(u.m_in_h, 0)) OVER w_ord, 0)) END::int  AS saldo_aves_h,
    CASE WHEN c.base_m IS NULL THEN NULL
         ELSE GREATEST(0, c.base_m
                - COALESCE(SUM(u.c_mort_m + u.c_sel_m + u.c_err_m) OVER w_ord, 0)
                - COALESCE(SUM(COALESCE(u.m_out_m, 0)) OVER w_ord, 0)
                + COALESCE(SUM(COALESCE(u.m_in_m, 0)) OVER w_ord, 0)) END::int  AS saldo_aves_m,
    COALESCE(u.c_es_traslado, false)                                  AS es_traslado,
    u.c_tras_dir                                                      AS traslado_direccion,
    u.c_tras_in_h                                                     AS traslado_ingreso_hembras,
    u.c_tras_in_m                                                     AS traslado_ingreso_machos,
    u.c_tras_out_h                                                    AS traslado_salida_hembras,
    u.c_tras_out_m                                                    AS traslado_salida_machos,
    u.c_lote_destino_id                                               AS lote_destino_id,
    u.c_granja_destino_id                                             AS granja_destino_id,
    u.c_peso_h                                                        AS peso_h,
    u.c_peso_m                                                        AS peso_m,
    u.c_unif                                                          AS uniformidad,
    u.c_cv                                                            AS coeficiente_variacion,
    u.c_unif_h                                                        AS uniformidad_hembras,
    u.c_unif_m                                                        AS uniformidad_machos,
    u.c_cv_h                                                          AS cv_hembras,
    u.c_cv_m                                                          AS cv_machos,
    u.c_obs_pesaje                                                    AS observaciones_pesaje,
    u.c_agua                                                          AS consumo_agua_diario,
    u.c_agua_ph                                                       AS consumo_agua_ph,
    u.c_agua_orp                                                      AS consumo_agua_orp,
    u.c_agua_temp                                                     AS consumo_agua_temperatura,
    u.c_etapa                                                         AS etapa,
    u.c_ciclo                                                         AS ciclo,
    u.c_observaciones                                                 AS observaciones,
    u.c_metadata                                                      AS metadata,
    u.c_created_by                                                    AS created_by_user_id,
    u.c_created_at                                                    AS created_at,
    u.c_updated_at                                                    AS updated_at
FROM universo u
CROSS JOIN ctx c
WINDOW
    w_ord  AS (ORDER BY u.u_fecha, COALESCE(u.c_seg_id, 0)
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW),
    w_prev AS (ORDER BY u.u_fecha, COALESCE(u.c_seg_id, 0)
               ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING)
ORDER BY u.u_fecha, COALESCE(u.c_seg_id, 0);
$$;
""";

        private const string FnIndicadoresPrev = """
-- ============================================================================
-- fn_indicadores_produccion_postura(...)
-- Indicadores semanales de PRODUCCIÓN (postura) calculados en la BD.
-- Reemplaza el cómputo en memoria de IndicadoresProduccionService.CalcularIndicadoresAsync
-- (C#, 727 líneas): el servicio ahora resuelve company/lote y DELEGA el cálculo aquí.
--
-- Replica EXACTO el algoritmo C# actual (que ya incorpora las correcciones REQ-004 de
-- comparación vs guía) usando double precision y el mismo orden de operaciones.
--
-- Correcciones de guía YA presentes en el C# y replicadas aquí (documentadas):
--   REQ-004a  %Producción (hen-day) = promedioHuevos/día / HEMBRAS vivas * 100
--             (solo hembras en el denominador; los machos no ponen).
--   REQ-004b  Peso de aves normalizado a kg: >100 ? /1000 (los pesajes vienen en gramos)
--             para casar con la guía (peso_h/1000).
--   REQ-004c  H.T.A.A / H.I.A.A reales (acumulados por ave alojada) se comparan contra
--             h_total_aa / h_inc_aa de la guía (que son acumulados), no contra huevos/día.
--   REQ-004d  Mortalidad de guía es % (decimal), no entero (no se trunca a 0).
--   REQ-004e  (Verenice rev 6-jul-26) La tabla "% Retiro (Real vs Guía)" del front mostraba el
--             REAL pero la GUÍA quedaba vacía: la fn calculaba retiro_ac_h/m REAL pero nunca
--             exponía la guía. Se agregan retiro_ac_h_guia/retiro_ac_m_guia leyendo
--             guia_genetica_sanmarino_colombia.retiro_ac_h/retiro_ac_m (mismo parseo NULLIF/btrim
--             que las demás columnas guía; NULL si no hay guía para la semana).
--   Guía = tabla real guia_genetica_sanmarino_colombia filtrada por company + raza + año
--          (misma tabla que ProduccionAvicolaRaw); indexada por Edad = SEMANA DE VIDA.
--
-- Desviaciones preservadas (NO son bugs de guía → se replican tal cual, ver spec §3):
--   * aves_hembras_inicio_semana = avesHActuales + mortH + selH (sobrecuenta el censo de
--     inicio respecto al saldo real de arranque). Campo informativo; NO afecta comparación.
--   * consumo_real_h/m divide por ese aves_*_inicio_semana sobrecontado.
--   * %mortalidad / %selección usan avesHActuales (saldo real de inicio), no el sobrecontado.
--
-- Timezone: America/Bogota para el corte de semanas. Con Npgsql.EnableLegacyTimestampBehavior
--   =true el back lee timestamptz como hora local del proceso; en dev/local el TZ es UTC-5
--   (= America/Bogota sin DST) → .Date del C# = fecha Bogotá. Se normaliza a Bogotá aquí.
--
-- Fuente de verdad: IndicadoresProduccionService.cs (ObtenerIndicadoresSemanalesAsync/CalcularIndicadoresAsync).
-- ============================================================================

-- ── Helper: diferencia porcentual (== CalcularDiferenciaPorcentual del C#).
--    NULL si falta real/guía o guía = 0.
CREATE OR REPLACE FUNCTION fn_dif_pct(p_real double precision, p_guia double precision)
RETURNS double precision LANGUAGE sql IMMUTABLE AS $$
    SELECT CASE
        WHEN p_real IS NULL OR p_guia IS NULL OR p_guia = 0 THEN NULL
        ELSE ((p_real - p_guia) / p_guia) * 100
    END;
$$;

-- ── Helper: parseo de edad numérica de la guía (== TryParseEdadNumerica del C#).
--    Intenta parsear a entero (coma->punto); si no, extrae el primer grupo de dígitos.
--    Devuelve NULL si no hay dígitos. (Edades de producción son enteros: 26, 27, …)
CREATE OR REPLACE FUNCTION fn_parse_edad_numerica(p_edad text)
RETURNS integer LANGUAGE plpgsql IMMUTABLE AS $$
DECLARE
    v_clean text;
    v_match text;
BEGIN
    IF p_edad IS NULL OR btrim(p_edad) = '' THEN RETURN NULL; END IF;
    v_clean := replace(btrim(p_edad), ',', '.');
    IF v_clean ~ '^[+-]?\d+$' THEN
        RETURN v_clean::integer;
    END IF;
    v_match := (regexp_match(v_clean, '(\d+)'))[1];
    IF v_match IS NULL THEN RETURN NULL; END IF;
    RETURN v_match::integer;
END;
$$;

-- REQ-004 cambia la firma de RETURNS TABLE (agrega retiro_sem_h/m + retiro_ac_h/m). Postgres NO
-- permite CREATE OR REPLACE cuando cambia el row type de los parámetros OUT → DROP idempotente antes.
DROP FUNCTION IF EXISTS fn_indicadores_produccion_postura(integer, integer, integer, integer, integer, date, date);

CREATE OR REPLACE FUNCTION fn_indicadores_produccion_postura(
    p_company_id                  integer,
    p_lote_postura_produccion_id  integer  DEFAULT NULL,
    p_lote_id                     integer  DEFAULT NULL,
    p_semana_desde                integer  DEFAULT NULL,
    p_semana_hasta                integer  DEFAULT NULL,
    p_fecha_desde                 date     DEFAULT NULL,
    p_fecha_hasta                 date     DEFAULT NULL
)
RETURNS TABLE(
    semana                              integer,
    fecha_inicio_semana                 date,
    fecha_fin_semana                    date,
    total_registros                     integer,
    mortalidad_hembras                  integer,
    mortalidad_machos                   integer,
    porcentaje_mortalidad_hembras       double precision,
    porcentaje_mortalidad_machos        double precision,
    mortalidad_guia_hembras             double precision,
    mortalidad_guia_machos              double precision,
    diferencia_mortalidad_hembras       double precision,
    diferencia_mortalidad_machos        double precision,
    seleccion_hembras                   integer,
    porcentaje_seleccion_hembras        double precision,
    consumo_kg_hembras                  double precision,
    consumo_kg_machos                   double precision,
    consumo_total_kg                    double precision,
    consumo_promedio_diario_kg          double precision,
    consumo_guia_hembras                double precision,
    consumo_guia_machos                 double precision,
    diferencia_consumo_hembras          double precision,
    diferencia_consumo_machos           double precision,
    huevos_totales                      integer,
    huevos_incubables                   integer,
    promedio_huevos_por_dia             double precision,
    eficiencia_produccion               double precision,
    huevos_totales_guia                 double precision,
    huevos_incubables_guia              double precision,
    porcentaje_produccion_guia          double precision,
    diferencia_huevos_totales           double precision,
    diferencia_huevos_incubables        double precision,
    diferencia_porcentaje_produccion    double precision,
    peso_huevo_promedio                 double precision,
    peso_huevo_guia                     double precision,
    diferencia_peso_huevo               double precision,
    peso_promedio_hembras               double precision,
    peso_promedio_machos                double precision,
    peso_guia_hembras                   double precision,
    peso_guia_machos                    double precision,
    diferencia_peso_hembras             double precision,
    diferencia_peso_machos              double precision,
    uniformidad_promedio                double precision,
    uniformidad_guia                    double precision,
    diferencia_uniformidad              double precision,
    coeficiente_variacion_promedio      double precision,
    huevos_limpios                      integer,
    huevos_tratados                     integer,
    huevos_sucios                       integer,
    huevos_deformes                     integer,
    huevos_blancos                      integer,
    huevos_doble_yema                   integer,
    huevos_piso                         integer,
    huevos_pequenos                     integer,
    huevos_rotos                        integer,
    huevos_desecho                      integer,
    huevos_otro                         integer,
    aves_hembras_inicio_semana          integer,
    aves_machos_inicio_semana           integer,
    aves_hembras_fin_semana             integer,
    aves_machos_fin_semana              integer,
    htaa_real                           double precision,
    hiaa_real                           double precision,
    -- REQ-004: %Retiro REAL por sexo (mortalidad + selección). Semanal sobre saldo de inicio del
    --   sexo; acumulado sobre aves iniciales del sexo. Aritmética == ProduccionCalculos.PorcentajeRetiro*.
    retiro_sem_h                        double precision,
    retiro_sem_m                        double precision,
    retiro_ac_h                         double precision,
    retiro_ac_m                         double precision,
    -- REQ-004 (Verenice rev 6-jul-26): %Retiro acumulado de GUÍA por sexo, desde
    --   guia_genetica_sanmarino_colombia.retiro_ac_h/retiro_ac_m (texto, mismo parseo que las
    --   demás columnas guía: NULLIF(btrim(...),'')::double precision). NULL si no hay guía para
    --   esa semana (g_found=false); si hay guía pero el campo viene vacío, 0 (mismo criterio que
    --   g_mort_h/g_mort_m, no el de huevos/%prod que preservan NULL).
    retiro_ac_h_guia                    double precision,
    retiro_ac_m_guia                    double precision
)
LANGUAGE plpgsql VOLATILE AS $fn$
DECLARE
    -- ── contexto del lote resuelto ──
    v_enc_date       date;            -- fechaEncaset.Date (Bogotá)
    v_aves_h_ini     integer;
    v_aves_m_ini     integer;
    v_raza           text;
    v_ano            text;            -- ano_tabla_genetica::text
    v_lote_id_str    text;            -- para el flujo legacy (lote_id como texto)
    v_lote_id_int    integer;         -- flujo legacy: lote resuelto, para fn_seguimiento_diario_produccion
    v_has_lote       boolean := false;

    -- ── acumuladores iterativos (mismos que el C#) ──
    v_aves_h_act     integer;
    v_aves_m_act     integer;
    v_cum_h_tot      bigint := 0;
    v_cum_h_inc      bigint := 0;
    -- REQ-004: acumulados de retiro por sexo (mortalidad + selección)
    v_cum_mort_h     bigint := 0;
    v_cum_sel_h      bigint := 0;
    v_cum_mort_m     bigint := 0;

    v_max_sem        integer;
    s                integer;

    -- ── por semana ──
    r_dias           integer;
    r_mort_h         integer;
    r_mort_m         integer;
    r_sel_h          integer;
    r_cons_kg_h      double precision;
    r_cons_kg_m      double precision;
    r_huevos_tot     integer;
    r_huevos_inc     integer;
    r_prom_huevos    double precision;
    r_efic           double precision;
    r_htaa           double precision;
    r_hiaa           double precision;
    r_peso_h         double precision;
    r_peso_m         double precision;
    r_unif           double precision;
    r_cv             double precision;
    r_peso_huevo     double precision;
    r_porc_mort_h    double precision;
    r_porc_mort_m    double precision;
    r_porc_sel_h     double precision;
    -- REQ-004: %Retiro real por semana
    r_retiro_sem_h   double precision;
    r_retiro_sem_m   double precision;
    r_retiro_ac_h    double precision;
    r_retiro_ac_m    double precision;
    r_aves_h_inicio  integer;
    r_aves_m_inicio  integer;
    -- guía
    g_cons_h         double precision;
    g_cons_m         double precision;
    g_mort_h         double precision;
    g_mort_m         double precision;
    g_peso_h         double precision;
    g_peso_m         double precision;
    g_unif           double precision;
    g_huevos_tot     double precision;
    g_huevos_inc     double precision;
    g_prod_pct       double precision;
    g_peso_huevo     double precision;
    -- REQ-004 (Verenice): %Retiro acumulado de guía por sexo.
    g_retiro_ac_h    double precision;
    g_retiro_ac_m    double precision;
    g_found          boolean;
    -- consumo real
    r_cons_real_h    double precision;
    r_cons_real_m    double precision;
    -- clasificadora
    r_limpios        integer;
    r_tratados       integer;
    r_sucios         integer;
    r_deformes       integer;
    r_blancos        integer;
    r_doble_yema     integer;
    r_piso           integer;
    r_pequenos       integer;
    r_rotos          integer;
    r_desecho        integer;
    r_otro           integer;
BEGIN
    -- ════════════════════════════════════════════════════════════════════
    -- 1) RESOLVER LOTE (misma prioridad y semántica que el C#).
    -- ════════════════════════════════════════════════════════════════════
    IF p_lote_postura_produccion_id IS NOT NULL AND p_lote_postura_produccion_id > 0 THEN
        -- ── Flujo LPP ──
        SELECT
            -- fecha ref: encaset del levante ligado -> lpp.fecha_encaset -> lpp.fecha_inicio_produccion
            (COALESCE(lev.fecha_encaset, lpp.fecha_encaset, lpp.fecha_inicio_produccion)
                AT TIME ZONE 'America/Bogota')::date,
            COALESCE(lpp.aves_h_inicial, lpp.hembras_iniciales_prod, 0),
            COALESCE(lpp.aves_m_inicial, lpp.machos_iniciales_prod, 0),
            COALESCE(lpp.raza, ''),
            lpp.ano_tabla_genetica::text
          INTO v_enc_date, v_aves_h_ini, v_aves_m_ini, v_raza, v_ano
          FROM lote_postura_produccion lpp
          LEFT JOIN lote_postura_levante lev
                 ON lev.lote_postura_levante_id = lpp.lote_postura_levante_id
                AND lev.deleted_at IS NULL
         WHERE lpp.lote_postura_produccion_id = p_lote_postura_produccion_id
           AND lpp.company_id = p_company_id
           AND lpp.deleted_at IS NULL;

        IF NOT FOUND OR v_enc_date IS NULL THEN
            RETURN;  -- lote inexistente o sin fecha de referencia -> sin filas (el C# lanza; el servicio valida antes)
        END IF;
        v_has_lote := true;

        -- Seguimientos: desde fn_seguimiento_diario_produccion (la fn diaria canónica ya hace el
        -- UNION dual-fuente + dedup por día Bogotá «gana el más temprano»); solo días con registro
        -- (seg_id IS NOT NULL — sin días movimiento-only).
        CREATE TEMP TABLE _seg ON COMMIT DROP AS
        SELECT f.fecha_ts AS ts,
               COALESCE(f.mortalidad_hembras,0) AS mort_h, COALESCE(f.mortalidad_machos,0) AS mort_m,
               COALESCE(f.sel_h,0) AS sel_h,
               COALESCE(f.cons_kg_h,0)::double precision AS cons_h,
               COALESCE(f.cons_kg_m,0)::double precision AS cons_m,
               COALESCE(f.huevo_tot,0) AS huevo_tot, COALESCE(f.huevo_inc,0) AS huevo_inc,
               COALESCE(f.huevo_limpio,0) AS h_limpio, COALESCE(f.huevo_tratado,0) AS h_tratado,
               COALESCE(f.huevo_sucio,0) AS h_sucio, COALESCE(f.huevo_deforme,0) AS h_deforme,
               COALESCE(f.huevo_blanco,0) AS h_blanco, COALESCE(f.huevo_doble_yema,0) AS h_doble,
               COALESCE(f.huevo_piso,0) AS h_piso, COALESCE(f.huevo_pequeno,0) AS h_pequeno,
               COALESCE(f.huevo_roto,0) AS h_roto, COALESCE(f.huevo_desecho,0) AS h_desecho,
               COALESCE(f.huevo_otro,0) AS h_otro,
               f.peso_huevo::double precision AS peso_huevo,
               f.peso_h::double precision AS peso_h, f.peso_m::double precision AS peso_m,
               f.uniformidad::double precision AS unif, f.coeficiente_variacion::double precision AS cv
          FROM fn_seguimiento_diario_produccion(p_lote_postura_produccion_id, NULL) f
         WHERE f.seg_id IS NOT NULL;

    ELSIF p_lote_id IS NOT NULL AND p_lote_id > 0 THEN
        -- ── Flujo legacy: Lote en fase Producción ──
        -- lote_prod: hijo (lote_padre_id = p_lote_id) en fase Produccion; si no, el propio lote_id.
        DECLARE
            v_lp_lote_id      integer;
            v_lp_padre_id     integer;
            v_lp_fip          timestamptz;
            v_lp_raza         text;
            v_lp_ano          integer;
        BEGIN
            SELECT l.lote_id, l.lote_padre_id, l.fecha_inicio_produccion, l.raza, l.ano_tabla_genetica
              INTO v_lp_lote_id, v_lp_padre_id, v_lp_fip, v_lp_raza, v_lp_ano
              FROM lotes l
             WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
               AND l.fase = 'Produccion' AND l.lote_padre_id = p_lote_id
             ORDER BY l.lote_id
             LIMIT 1;

            IF NOT FOUND THEN
                SELECT l.lote_id, l.lote_padre_id, l.fecha_inicio_produccion, l.raza, l.ano_tabla_genetica
                  INTO v_lp_lote_id, v_lp_padre_id, v_lp_fip, v_lp_raza, v_lp_ano
                  FROM lotes l
                 WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
                   AND l.fase = 'Produccion' AND l.lote_id = p_lote_id
                 LIMIT 1;
            END IF;

            IF NOT FOUND THEN
                RETURN;
            END IF;
            v_has_lote := true;
            v_lote_id_str := v_lp_lote_id::text;
            v_lote_id_int := v_lp_lote_id;

            -- fecha ref = fecha_inicio_produccion; si null y hay padre -> fecha_encaset del padre
            IF v_lp_fip IS NULL AND v_lp_padre_id IS NOT NULL THEN
                SELECT p.fecha_encaset INTO v_lp_fip
                  FROM lotes p WHERE p.lote_id = v_lp_padre_id AND p.deleted_at IS NULL;
            END IF;
            IF v_lp_fip IS NULL THEN
                RETURN;
            END IF;
            v_enc_date := (v_lp_fip AT TIME ZONE 'America/Bogota')::date;

            SELECT COALESCE(hembras_iniciales_prod,0), COALESCE(machos_iniciales_prod,0)
              INTO v_aves_h_ini, v_aves_m_ini
              FROM lotes WHERE lote_id = v_lp_lote_id;

            -- raza/año del lote; si faltan y hay padre, del padre
            v_raza := COALESCE(v_lp_raza, '');
            v_ano  := v_lp_ano::text;
            IF (v_raza = '' OR v_lp_ano IS NULL) AND v_lp_padre_id IS NOT NULL THEN
                SELECT COALESCE(p.raza,''), p.ano_tabla_genetica::text
                  INTO v_raza, v_ano
                  FROM lotes p WHERE p.lote_id = v_lp_padre_id AND p.deleted_at IS NULL;
            END IF;
        END;

        -- Seguimientos legacy: desde fn_seguimiento_diario_produccion (dedup dual-fuente ya
        -- resuelto por la fn diaria); solo días con registro.
        CREATE TEMP TABLE _seg ON COMMIT DROP AS
        SELECT f.fecha_ts AS ts,
               COALESCE(f.mortalidad_hembras,0) AS mort_h, COALESCE(f.mortalidad_machos,0) AS mort_m,
               COALESCE(f.sel_h,0) AS sel_h,
               COALESCE(f.cons_kg_h,0)::double precision AS cons_h,
               COALESCE(f.cons_kg_m,0)::double precision AS cons_m,
               COALESCE(f.huevo_tot,0) AS huevo_tot, COALESCE(f.huevo_inc,0) AS huevo_inc,
               COALESCE(f.huevo_limpio,0) AS h_limpio, COALESCE(f.huevo_tratado,0) AS h_tratado,
               COALESCE(f.huevo_sucio,0) AS h_sucio, COALESCE(f.huevo_deforme,0) AS h_deforme,
               COALESCE(f.huevo_blanco,0) AS h_blanco, COALESCE(f.huevo_doble_yema,0) AS h_doble,
               COALESCE(f.huevo_piso,0) AS h_piso, COALESCE(f.huevo_pequeno,0) AS h_pequeno,
               COALESCE(f.huevo_roto,0) AS h_roto, COALESCE(f.huevo_desecho,0) AS h_desecho,
               COALESCE(f.huevo_otro,0) AS h_otro,
               f.peso_huevo::double precision AS peso_huevo,
               f.peso_h::double precision AS peso_h, f.peso_m::double precision AS peso_m,
               f.uniformidad::double precision AS unif, f.coeficiente_variacion::double precision AS cv
          FROM fn_seguimiento_diario_produccion(NULL, v_lote_id_int) f
         WHERE f.seg_id IS NOT NULL;

    ELSE
        RETURN;  -- ni LPP ni loteId válido
    END IF;

    IF NOT v_has_lote THEN RETURN; END IF;

    -- ════════════════════════════════════════════════════════════════════
    -- 2) Semana de VIDA de cada registro + filtro de fechas (== C#).
    --    semanaVida = floor(dias/7)+1 con dias = regDate - encDate (división entera).
    -- ════════════════════════════════════════════════════════════════════
    ALTER TABLE _seg ADD COLUMN reg_date date;
    ALTER TABLE _seg ADD COLUMN sem_vida integer;
    UPDATE _seg SET reg_date = (ts AT TIME ZONE 'America/Bogota')::date;
    -- filtro de fechas (request.FechaDesde/Hasta) sobre la fecha local
    IF p_fecha_desde IS NOT NULL THEN
        DELETE FROM _seg WHERE reg_date < p_fecha_desde;
    END IF;
    IF p_fecha_hasta IS NOT NULL THEN
        DELETE FROM _seg WHERE reg_date > p_fecha_hasta;
    END IF;
    UPDATE _seg SET sem_vida = ((reg_date - v_enc_date) / 7) + 1;  -- división entera == C# (dias/7)+1
    -- REQ-012b: producción arranca en la semana 25 de vida (antes 26). La guía genética empieza en
    --   la semana 26, así que la 25 queda con columnas de guía en NULL (g_found=false ya lo soporta).
    DELETE FROM _seg WHERE sem_vida < 25;

    SELECT MAX(sem_vida) INTO v_max_sem FROM _seg;
    IF v_max_sem IS NULL THEN RETURN; END IF;

    -- ════════════════════════════════════════════════════════════════════
    -- 3) Iterar semanas presentes en orden (== foreach sobre grupos ordenados).
    --    OJO: itera SOLO las semanas con registros (>=25 tras REQ-012b) y en orden asc.
    --    Los acumuladores (aves actuales, htaa/hiaa, retiro) avanzan solo en esas semanas.
    -- ════════════════════════════════════════════════════════════════════
    v_aves_h_act := v_aves_h_ini;
    v_aves_m_act := v_aves_m_ini;

    FOR s IN 25..v_max_sem LOOP  -- REQ-012b: incluir semana 25 (antes 26)
        CONTINUE WHEN NOT EXISTS (SELECT 1 FROM _seg WHERE sem_vida = s);

        SELECT COUNT(*)::int,
               COALESCE(SUM(mort_h),0), COALESCE(SUM(mort_m),0), COALESCE(SUM(sel_h),0),
               COALESCE(SUM(cons_h),0), COALESCE(SUM(cons_m),0),
               COALESCE(SUM(huevo_tot),0), COALESCE(SUM(huevo_inc),0),
               COALESCE(SUM(h_limpio),0), COALESCE(SUM(h_tratado),0), COALESCE(SUM(h_sucio),0),
               COALESCE(SUM(h_deforme),0), COALESCE(SUM(h_blanco),0), COALESCE(SUM(h_doble),0),
               COALESCE(SUM(h_piso),0), COALESCE(SUM(h_pequeno),0), COALESCE(SUM(h_roto),0),
               COALESCE(SUM(h_desecho),0), COALESCE(SUM(h_otro),0)
          INTO r_dias, r_mort_h, r_mort_m, r_sel_h, r_cons_kg_h, r_cons_kg_m,
               r_huevos_tot, r_huevos_inc,
               r_limpios, r_tratados, r_sucios, r_deformes, r_blancos, r_doble_yema,
               r_piso, r_pequenos, r_rotos, r_desecho, r_otro
          FROM _seg WHERE sem_vida = s;

        r_prom_huevos := CASE WHEN r_dias > 0 THEN r_huevos_tot::double precision / r_dias ELSE 0 END;

        -- REQ-004a: %Producción hen-day = huevos/día / HEMBRAS vivas (solo hembras) * 100
        r_efic := CASE WHEN v_aves_h_act > 0 THEN r_prom_huevos / v_aves_h_act * 100 ELSE 0 END;

        -- Acumulados por ave alojada (REQ-004c)
        v_cum_h_tot := v_cum_h_tot + r_huevos_tot;
        v_cum_h_inc := v_cum_h_inc + r_huevos_inc;

        -- REQ-004: acumulados de retiro (mortalidad + selección) por sexo. Machos sin selección en
        --   esta fn (igual que el decremento de aves, que solo resta mort_m).
        v_cum_mort_h := v_cum_mort_h + r_mort_h;
        v_cum_sel_h  := v_cum_sel_h + r_sel_h;
        v_cum_mort_m := v_cum_mort_m + r_mort_m;
        r_htaa := CASE WHEN v_aves_h_ini > 0 THEN v_cum_h_tot::double precision / v_aves_h_ini ELSE 0 END;
        r_hiaa := CASE WHEN v_aves_h_ini > 0 THEN v_cum_h_inc::double precision / v_aves_h_ini ELSE 0 END;

        -- Peso aves (kg, REQ-004b): promedio de registros con valor NO NULO, luego normalizar.
        SELECT AVG(peso_h) FILTER (WHERE peso_h IS NOT NULL),
               AVG(peso_m) FILTER (WHERE peso_m IS NOT NULL),
               AVG(unif)   FILTER (WHERE unif   IS NOT NULL),
               AVG(cv)     FILTER (WHERE cv     IS NOT NULL),
               AVG(peso_huevo) FILTER (WHERE peso_huevo > 0)
          INTO r_peso_h, r_peso_m, r_unif, r_cv, r_peso_huevo
          FROM _seg WHERE sem_vida = s;
        IF r_peso_h IS NOT NULL THEN r_peso_h := CASE WHEN r_peso_h > 100 THEN r_peso_h/1000 ELSE r_peso_h END; END IF;
        IF r_peso_m IS NOT NULL THEN r_peso_m := CASE WHEN r_peso_m > 100 THEN r_peso_m/1000 ELSE r_peso_m END; END IF;

        -- %mortalidad / %selección: sobre el saldo REAL de inicio (avesActuales)
        r_porc_mort_h := CASE WHEN v_aves_h_act > 0 THEN r_mort_h::double precision / v_aves_h_act * 100 ELSE 0 END;
        r_porc_mort_m := CASE WHEN v_aves_m_act > 0 THEN r_mort_m::double precision / v_aves_m_act * 100 ELSE 0 END;
        r_porc_sel_h  := CASE WHEN v_aves_h_act > 0 THEN r_sel_h::double precision  / v_aves_h_act * 100 ELSE 0 END;

        -- REQ-004: %Retiro REAL (== ProduccionCalculos.PorcentajeRetiroSemanal/Acumulado).
        --   Semanal: (mort + sel de la semana) / saldo REAL de inicio del sexo (v_aves_*_act, pre-decremento) * 100.
        --   Acumulado: (mort + sel acumulados) / aves iniciales del sexo * 100.
        r_retiro_sem_h := CASE WHEN v_aves_h_act > 0 THEN (r_mort_h + r_sel_h)::double precision / v_aves_h_act * 100 ELSE 0 END;
        r_retiro_sem_m := CASE WHEN v_aves_m_act > 0 THEN r_mort_m::double precision / v_aves_m_act * 100 ELSE 0 END;
        r_retiro_ac_h  := CASE WHEN v_aves_h_ini > 0 THEN (v_cum_mort_h + v_cum_sel_h)::double precision / v_aves_h_ini * 100 ELSE 0 END;
        r_retiro_ac_m  := CASE WHEN v_aves_m_ini > 0 THEN v_cum_mort_m::double precision / v_aves_m_ini * 100 ELSE 0 END;

        -- Censo de inicio de semana (desviación preservada: sobrecuenta con las bajas de la propia semana)
        r_aves_h_inicio := v_aves_h_act + r_mort_h + r_sel_h;
        r_aves_m_inicio := v_aves_m_act + r_mort_m;

        -- ── Guía (una sola tabla) por Edad = semana de VIDA (s) ──
        g_found := false;
        SELECT true,
               NULLIF(btrim(g.gr_ave_dia_h),'')::double precision,
               NULLIF(btrim(g.gr_ave_dia_m),'')::double precision,
               NULLIF(btrim(g.mort_sem_h),'')::double precision,
               NULLIF(btrim(g.mort_sem_m),'')::double precision,
               NULLIF(btrim(g.peso_h),'')::double precision,
               NULLIF(btrim(g.peso_m),'')::double precision,
               NULLIF(btrim(g.uniformidad),'')::double precision,
               NULLIF(btrim(g.h_total_aa),'')::double precision,
               NULLIF(btrim(g.h_inc_aa),'')::double precision,
               NULLIF(btrim(g.prod_porcentaje),'')::double precision,
               NULLIF(btrim(g.peso_huevo),'')::double precision,
               NULLIF(btrim(g.retiro_ac_h),'')::double precision,
               NULLIF(btrim(g.retiro_ac_m),'')::double precision
          INTO g_found, g_cons_h, g_cons_m, g_mort_h, g_mort_m, g_peso_h, g_peso_m, g_unif,
               g_huevos_tot, g_huevos_inc, g_prod_pct, g_peso_huevo, g_retiro_ac_h, g_retiro_ac_m
          FROM guia_genetica_sanmarino_colombia g
         WHERE g.company_id = p_company_id
           AND g.deleted_at IS NULL
           AND btrim(lower(g.raza)) = btrim(lower(v_raza))
           AND btrim(g.anio_guia) = v_ano
           AND fn_parse_edad_numerica(g.edad) = s
         -- La semana 25 tiene DOS filas que parsean a 25: '25' (cierre de
         -- levante) y '25P' (arranque de producción), con valores muy distintos
         -- (retiro_ac_h 4,03 vs 0,10). Sin ORDER BY la que gana depende del
         -- plan y del orden físico de la tabla: hoy sale '25P' por el ctid, no
         -- por contrato. Se fija el desempate en la variante con sufijo —la de
         -- producción, que es la correcta acá y la que ya venía devolviendo—
         -- para que un VACUUM o un re-seed no cambien el reporte en silencio.
         ORDER BY (CASE WHEN btrim(g.edad) = s::text THEN 1 ELSE 0 END), g.id
         LIMIT 1;
        g_found := COALESCE(g_found, false);

        IF g_found THEN
            -- ParseDouble => 0 cuando el string es vacío/no numérico (no NULL). Las columnas de la
            -- guía "obtenerGuiaGeneticaProduccion" pasan por ParseDouble (0 si vacío); las del raw
            -- (huevos/%prod/pesoHuevo) por ParseDecimal (NULL si vacío). Se respeta esa diferencia:
            g_cons_h := COALESCE(g_cons_h, 0);
            g_cons_m := COALESCE(g_cons_m, 0);
            g_mort_h := COALESCE(g_mort_h, 0);
            g_mort_m := COALESCE(g_mort_m, 0);
            g_peso_h := COALESCE(g_peso_h, 0) / 1000;   -- peso_h/1000
            g_peso_m := COALESCE(g_peso_m, 0) / 1000;   -- peso_m/1000
            g_unif   := COALESCE(g_unif, 0);
            -- huevos/%prod/pesoHuevo: quedan NULL si vacíos (ParseDecimal), no 0.
            -- retiro_ac_h/m guía: mismo criterio que mort_h/mort_m (ParseDouble => 0 si vacío).
            g_retiro_ac_h := COALESCE(g_retiro_ac_h, 0);
            g_retiro_ac_m := COALESCE(g_retiro_ac_m, 0);
        ELSE
            g_cons_h := NULL; g_cons_m := NULL; g_mort_h := NULL; g_mort_m := NULL;
            g_peso_h := NULL; g_peso_m := NULL; g_unif := NULL;
            g_huevos_tot := NULL; g_huevos_inc := NULL; g_prod_pct := NULL; g_peso_huevo := NULL;
            g_retiro_ac_h := NULL; g_retiro_ac_m := NULL;
        END IF;

        -- Consumo real (g/ave/día) — denominador = censo de inicio sobrecontado (desviación preservada)
        r_cons_real_h := CASE WHEN r_dias > 0 AND r_aves_h_inicio > 0
                              THEN r_cons_kg_h * 1000 / (r_dias * r_aves_h_inicio) ELSE NULL END;
        r_cons_real_m := CASE WHEN r_dias > 0 AND r_aves_m_inicio > 0
                              THEN r_cons_kg_m * 1000 / (r_dias * r_aves_m_inicio) ELSE NULL END;

        -- Decremento de aves (al final, == C#)
        v_aves_h_act := GREATEST(0, v_aves_h_act - r_mort_h - r_sel_h);
        v_aves_m_act := GREATEST(0, v_aves_m_act - r_mort_m);

        -- ── Emitir fila (respetando filtro semanaDesde/Hasta como en C#) ──
        IF (p_semana_desde IS NULL OR s >= p_semana_desde)
           AND (p_semana_hasta IS NULL OR s <= p_semana_hasta) THEN
            semana                           := s;
            fecha_inicio_semana              := v_enc_date + ((s - 1) * 7);
            fecha_fin_semana                 := v_enc_date + ((s - 1) * 7) + 6;
            total_registros                  := r_dias;
            mortalidad_hembras               := r_mort_h;
            mortalidad_machos                := r_mort_m;
            porcentaje_mortalidad_hembras    := r_porc_mort_h;
            porcentaje_mortalidad_machos     := r_porc_mort_m;
            mortalidad_guia_hembras          := g_mort_h;
            mortalidad_guia_machos           := g_mort_m;
            diferencia_mortalidad_hembras    := fn_dif_pct(r_porc_mort_h, g_mort_h);
            diferencia_mortalidad_machos     := fn_dif_pct(r_porc_mort_m, g_mort_m);
            seleccion_hembras                := r_sel_h;
            porcentaje_seleccion_hembras     := r_porc_sel_h;
            consumo_kg_hembras               := r_cons_kg_h;
            consumo_kg_machos                := r_cons_kg_m;
            consumo_total_kg                 := r_cons_kg_h + r_cons_kg_m;
            consumo_promedio_diario_kg       := CASE WHEN r_dias > 0 THEN (r_cons_kg_h + r_cons_kg_m)/r_dias ELSE 0 END;
            consumo_guia_hembras             := g_cons_h;
            consumo_guia_machos              := g_cons_m;
            diferencia_consumo_hembras       := fn_dif_pct(r_cons_real_h, g_cons_h);
            diferencia_consumo_machos        := fn_dif_pct(r_cons_real_m, g_cons_m);
            huevos_totales                   := r_huevos_tot;
            huevos_incubables                := r_huevos_inc;
            promedio_huevos_por_dia          := r_prom_huevos;
            eficiencia_produccion            := r_efic;
            huevos_totales_guia              := g_huevos_tot;
            huevos_incubables_guia           := g_huevos_inc;
            porcentaje_produccion_guia       := g_prod_pct;
            diferencia_huevos_totales        := fn_dif_pct(r_htaa, g_huevos_tot);
            diferencia_huevos_incubables     := fn_dif_pct(r_hiaa, g_huevos_inc);
            diferencia_porcentaje_produccion := fn_dif_pct(r_efic, g_prod_pct);
            peso_huevo_promedio              := r_peso_huevo;
            peso_huevo_guia                  := g_peso_huevo;
            diferencia_peso_huevo            := fn_dif_pct(r_peso_huevo, g_peso_huevo);
            peso_promedio_hembras            := r_peso_h;
            peso_promedio_machos             := r_peso_m;
            peso_guia_hembras                := g_peso_h;
            peso_guia_machos                 := g_peso_m;
            diferencia_peso_hembras          := fn_dif_pct(r_peso_h, g_peso_h);
            diferencia_peso_machos           := fn_dif_pct(r_peso_m, g_peso_m);
            uniformidad_promedio             := r_unif;
            uniformidad_guia                 := g_unif;
            diferencia_uniformidad           := fn_dif_pct(r_unif, g_unif);
            coeficiente_variacion_promedio   := r_cv;
            huevos_limpios                   := r_limpios;
            huevos_tratados                  := r_tratados;
            huevos_sucios                    := r_sucios;
            huevos_deformes                  := r_deformes;
            huevos_blancos                   := r_blancos;
            huevos_doble_yema                := r_doble_yema;
            huevos_piso                      := r_piso;
            huevos_pequenos                  := r_pequenos;
            huevos_rotos                     := r_rotos;
            huevos_desecho                   := r_desecho;
            huevos_otro                      := r_otro;
            aves_hembras_inicio_semana       := r_aves_h_inicio;
            aves_machos_inicio_semana        := r_aves_m_inicio;
            aves_hembras_fin_semana          := v_aves_h_act;
            aves_machos_fin_semana           := v_aves_m_act;
            htaa_real                        := r_htaa;
            hiaa_real                        := r_hiaa;
            retiro_sem_h                     := r_retiro_sem_h;
            retiro_sem_m                     := r_retiro_sem_m;
            retiro_ac_h                      := r_retiro_ac_h;
            retiro_ac_m                      := r_retiro_ac_m;
            retiro_ac_h_guia                 := g_retiro_ac_h;
            retiro_ac_m_guia                 := g_retiro_ac_m;
            RETURN NEXT;
        END IF;
    END LOOP;

    RETURN;
END;
$fn$;
""";

        private const string FnClasificacionPrev = """
-- ============================================================================
-- fn_clasificacion_huevo_items_produccion(...)
-- FASE 5 (Santa Reyes) — Desglose de la clasificación de huevos POR ÍTEM del catálogo
-- (categorías comerciales Primera / Pnc) agrupado por SEMANA DE VIDA, para las empresas con
-- companies.clasificacion_huevo_por_items = true: en ellas las 11 columnas fijas
-- (huevo_limpio…huevo_otro) quedan en 0 y el desglose real vive en el jsonb
-- seguimiento_diario_produccion.metadata -> 'huevoItems'
-- ([{catalogItemId, codigo, nombre, tipoHuevo, cantidad, um}, …], escrito por
-- HuevoItemsCalculos.EscribirEnMetadata; huevo_tot conserva la SUMA para no romper
-- espejo/trigger/saldos/indicadores).
--
-- Es el endpoint HERMANO de fn_indicadores_produccion_postura: MISMOS parámetros de entrada,
-- MISMA resolución de lote (LPP prioritario / legacy por lote), MISMA fuente de datos
-- (UNION seguimiento_diario_levante[tipo_seguimiento='produccion'] + seguimiento_diario_produccion
-- con DISTINCT ON por día en America/Bogota) y MISMA fórmula de semana
-- (semana de vida = ((fecha_local - fecha_encaset) / 7) + 1, división entera) → la columna
-- `semana` casa 1:1 con la grilla de indicadores semanales.
--
-- Diferencias deliberadas respecto de fn_indicadores_produccion_postura (documentadas):
--   * NO aplica el corte "semanas de producción >= 25": el desglose respeta EXCLUSIVAMENTE los
--     filtros que envía el llamador (p_semana_desde/p_semana_hasta). Motivo: las empresas que usan
--     clasificación por ítems pasan a producción antes de la semana 25 (Santa Reyes liquida el
--     levante ~semana 16) y ese desglose se perdería. El front actual manda semanaDesde = 26,
--     así que el resultado visible sigue alineado con la grilla.
--   * No usa tablas TEMP: todo se resuelve con CTEs (la fn puede invocarse varias veces dentro de
--     la misma transacción sin colisionar).
--
-- Robustez (nunca lanza; devuelve 0 filas cuando no hay nada):
--   * lote inexistente / sin fecha de referencia / sin ninguno de los dos parámetros → sin filas.
--   * registros sin metadata o sin la clave 'huevoItems' → no aportan (COALESCE a '[]').
--   * 'huevoItems' que no sea un array JSON, o elementos que no sean objetos → se ignoran.
--   * cantidades ausentes, no numéricas o <= 0 → se descartan.
--
-- Consumido por IndicadoresProduccionService.ObtenerClasificacionHuevoItemsAsync (SqlQueryRaw,
-- columnas snake_case → propiedades PascalCase) vía POST /api/Produccion/clasificacion-huevo-items.
-- ============================================================================

-- Idempotente: el row type de los parámetros OUT puede cambiar en el futuro y Postgres no admite
-- CREATE OR REPLACE en ese caso → DROP defensivo con la firma exacta.
DROP FUNCTION IF EXISTS fn_clasificacion_huevo_items_produccion(integer, integer, integer, integer, integer, date, date);

CREATE OR REPLACE FUNCTION fn_clasificacion_huevo_items_produccion(
    p_company_id                  integer,
    p_lote_postura_produccion_id  integer  DEFAULT NULL,
    p_lote_id                     integer  DEFAULT NULL,
    p_semana_desde                integer  DEFAULT NULL,
    p_semana_hasta                integer  DEFAULT NULL,
    p_fecha_desde                 date     DEFAULT NULL,
    p_fecha_hasta                 date     DEFAULT NULL
)
RETURNS TABLE(
    semana      integer,
    tipo_huevo  text,
    codigo      text,
    nombre      text,
    cantidad    bigint
)
LANGUAGE plpgsql STABLE AS $fn$
DECLARE
    v_enc_date     date;               -- fecha de referencia (encaset) en zona Bogotá
    v_lote_id_str  text;               -- flujo legacy: lote_id como texto (columna varchar del unificado)
    v_lote_id_int  integer;            -- flujo legacy: lote resuelto, para fn_seguimiento_diario_produccion
    v_flujo_lpp    boolean := false;   -- true = flujo LPP, false = flujo legacy por lote
    v_has_lote     boolean := false;
BEGIN
    -- ════════════════════════════════════════════════════════════════════
    -- 1) RESOLVER LOTE (misma prioridad y semántica que fn_indicadores_produccion_postura).
    -- ════════════════════════════════════════════════════════════════════
    IF p_lote_postura_produccion_id IS NOT NULL AND p_lote_postura_produccion_id > 0 THEN
        -- ── Flujo LPP ──
        SELECT
            -- fecha ref: encaset del levante ligado -> lpp.fecha_encaset -> lpp.fecha_inicio_produccion
            (COALESCE(lev.fecha_encaset, lpp.fecha_encaset, lpp.fecha_inicio_produccion)
                AT TIME ZONE 'America/Bogota')::date
          INTO v_enc_date
          FROM lote_postura_produccion lpp
          LEFT JOIN lote_postura_levante lev
                 ON lev.lote_postura_levante_id = lpp.lote_postura_levante_id
                AND lev.deleted_at IS NULL
         WHERE lpp.lote_postura_produccion_id = p_lote_postura_produccion_id
           AND lpp.company_id = p_company_id
           AND lpp.deleted_at IS NULL;

        IF NOT FOUND OR v_enc_date IS NULL THEN
            RETURN;  -- lote inexistente o sin fecha de referencia -> sin filas
        END IF;
        v_flujo_lpp := true;
        v_has_lote  := true;

    ELSIF p_lote_id IS NOT NULL AND p_lote_id > 0 THEN
        -- ── Flujo legacy: Lote en fase Producción ──
        -- lote_prod: hijo (lote_padre_id = p_lote_id) en fase Produccion; si no, el propio lote_id.
        DECLARE
            v_lp_lote_id      integer;
            v_lp_padre_id     integer;
            v_lp_fip          timestamptz;
        BEGIN
            SELECT l.lote_id, l.lote_padre_id, l.fecha_inicio_produccion
              INTO v_lp_lote_id, v_lp_padre_id, v_lp_fip
              FROM lotes l
             WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
               AND l.fase = 'Produccion' AND l.lote_padre_id = p_lote_id
             ORDER BY l.lote_id
             LIMIT 1;

            IF NOT FOUND THEN
                SELECT l.lote_id, l.lote_padre_id, l.fecha_inicio_produccion
                  INTO v_lp_lote_id, v_lp_padre_id, v_lp_fip
                  FROM lotes l
                 WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
                   AND l.fase = 'Produccion' AND l.lote_id = p_lote_id
                 LIMIT 1;
            END IF;

            IF NOT FOUND THEN
                RETURN;
            END IF;
            v_lote_id_str := v_lp_lote_id::text;
            v_lote_id_int := v_lp_lote_id;

            -- fecha ref = fecha_inicio_produccion; si null y hay padre -> fecha_encaset del padre
            IF v_lp_fip IS NULL AND v_lp_padre_id IS NOT NULL THEN
                SELECT p.fecha_encaset INTO v_lp_fip
                  FROM lotes p WHERE p.lote_id = v_lp_padre_id AND p.deleted_at IS NULL;
            END IF;
            IF v_lp_fip IS NULL THEN
                RETURN;
            END IF;
            v_enc_date := (v_lp_fip AT TIME ZONE 'America/Bogota')::date;
            v_has_lote := true;
        END;

    ELSE
        RETURN;  -- ni LPP ni loteId válido
    END IF;

    IF NOT v_has_lote THEN RETURN; END IF;

    -- ════════════════════════════════════════════════════════════════════
    -- 2) Seguimientos del lote — desde fn_seguimiento_diario_produccion (la fn diaria canónica
    --    ya resuelve el UNION dual-fuente + dedup por día Bogotá «gana el más temprano»; acá
    --    solo se toman los días con registro, seg_id IS NOT NULL — sin días movimiento-only).
    -- 3) Semana de vida + filtros de fecha/semana.
    -- 4) Expansión del jsonb 'huevoItems' y agregación por semana × tipo × ítem.
    -- ════════════════════════════════════════════════════════════════════
    RETURN QUERY
    WITH dias AS (
        SELECT f.fecha AS reg_date, f.metadata AS meta
          FROM fn_seguimiento_diario_produccion(
                   CASE WHEN v_flujo_lpp THEN p_lote_postura_produccion_id END,
                   CASE WHEN NOT v_flujo_lpp THEN v_lote_id_int END) f
         WHERE f.seg_id IS NOT NULL
    ),
    filtrados AS (
        SELECT ((dd.reg_date - v_enc_date) / 7) + 1 AS sem,   -- división entera == C# (dias/7)+1
               dd.meta
          FROM dias dd
         WHERE (p_fecha_desde IS NULL OR dd.reg_date >= p_fecha_desde)
           AND (p_fecha_hasta IS NULL OR dd.reg_date <= p_fecha_hasta)
    ),
    items AS (
        SELECT f.sem,
               COALESCE(NULLIF(btrim(it->>'tipoHuevo'), ''), '') AS tipo,
               COALESCE(NULLIF(btrim(it->>'codigo'),    ''), '') AS cod,
               COALESCE(NULLIF(btrim(it->>'nombre'),    ''), '') AS nom,
               CASE WHEN jsonb_typeof(it->'cantidad') = 'number'
                    THEN (it->>'cantidad')::numeric
                    ELSE NULL END AS cant
          FROM filtrados f
          CROSS JOIN LATERAL jsonb_array_elements(
                 CASE WHEN jsonb_typeof(COALESCE(f.meta->'huevoItems', '[]'::jsonb)) = 'array'
                      THEN COALESCE(f.meta->'huevoItems', '[]'::jsonb)
                      ELSE '[]'::jsonb
                 END) AS it
         WHERE jsonb_typeof(it) = 'object'
    )
    SELECT i.sem::integer,
           i.tipo::text,
           i.cod::text,
           i.nom::text,
           SUM(i.cant)::bigint
      FROM items i
     WHERE i.cant IS NOT NULL
       AND i.cant > 0
       AND (p_semana_desde IS NULL OR i.sem >= p_semana_desde)
       AND (p_semana_hasta IS NULL OR i.sem <= p_semana_hasta)
     GROUP BY i.sem, i.tipo, i.cod, i.nom
     ORDER BY i.sem, i.tipo, i.cod, i.nom;

    RETURN;
END;
$fn$;
""";

        private const string FnResumenPrev = """
-- ============================================================================
-- fn_resumen_semanal_ra_pesadas_produccion(...)
-- Hoja «RESUMEN SEMANAL» del Informe RA Pesadas — BLOQUE PRODUCCIÓN AP.
-- ----------------------------------------------------------------------------
-- UNA FILA POR LOTE para UNA semana CALENDARIO. Hermana de
-- fn_resumen_semanal_ra_pesadas_levante; misma convención de semana del año
-- (WEEKNUM de Excel, ver cabecera de la fn de levante).
--
-- ⚠️ Set-based A PROPÓSITO. Prohibido iterar lotes desde C# llamando la fn
--    por-lote: la BD filtra, el backend orquesta (regla del repo).
--
-- EQUIVALENCIA CON EL DETALLE (fn_indicadores_produccion_postura, **flujo LPP**):
--   ⚠️ El Detalle (ReporteTecnicoSemanalService.Produccion) llama la fn base con
--      `lpp.LotePosturaProduccionId`, NO con lote_id ⇒ hay que replicar el flujo
--      LPP, que resuelve distinto que el flujo legacy por lote. Usar
--      lotes.fecha_inicio_produccion aquí daría OTRA semana de vida y el Resumen
--      no cuadraría con el Detalle.
--   * unidad      = lote_postura_produccion (lpp), scope por company + deleted_at
--   * fecha ref   = COALESCE(lev.fecha_encaset, lpp.fecha_encaset,
--                            lpp.fecha_inicio_produccion)  ← encaset del levante
--                   ligado PRIMERO. Sin fecha ⇒ lote fuera.
--   * base aves   = COALESCE(lpp.aves_h_inicial, lpp.hembras_iniciales_prod, 0)
--                   COALESCE(lpp.aves_m_inicial, lpp.machos_iniciales_prod, 0)
--   * fuentes     = seguimiento_diario_levante (tipo_seguimiento='produccion')
--                   UNION seguimiento_diario_produccion, ambas filtradas por
--                   lote_postura_produccion_id, DEDUPLICADAS POR DÍA
--                   (calendario Bogotá): gana el registro de timestamp MÁS
--                   TEMPRANO del día — idéntico al DISTINCT ON de la fn base.
--   * semana vida = ((reg_date − fecha_ref) / 7) + 1  (división ENTERA, como la
--                   fn base; equivale a floor() con fechas no negativas)
--   * arranque    = semana 25 (REQ-012b)
--   * saldos      = hembras: −(mort + sel);  machos: −mort  (los machos NO
--                   tienen selección en esta fn — desviación preservada)
--   * %           = todos los porcentajes semanales van sobre el saldo al
--                   INICIO de la semana (pre-decremento), igual que la fn base
--
-- FÓRMULAS (verificadas contra la fn base y ReporteTecnicoSemanalCalculos):
--   %Prod        = (huevo_tot / días) / saldo_hembras_inicio * 100
--   HTAA         = Σ huevo_tot hasta la semana / hembras_iniciales
--   HIAA         = Σ huevo_inc hasta la semana / hembras_iniciales
--   %AprovSem    = huevo_inc * 100 / huevo_tot
--   GrHuevoInc   = (cons_kg_h + cons_kg_m) * 1000 / huevo_inc
--   %MortH       = mort_h / saldo_hembras_inicio * 100
--   %RetiroH     = Σ(mort_h + sel_h) / hembras_iniciales * 100
--   %RetiroM     = Σ mort_m / machos_iniciales * 100
--   PesoM/H      = peso_m / peso_h * 100   (pesos normalizados a kg: >100 ⇒ /1000)
--   Dif*         = fn_dif_pct(real, guía) = (real − guía) / guía * 100
--
-- Guía: guia_genetica_sanmarino_colombia por (company, raza, año, edad = semana
-- de vida). Todo TEXT ⇒ f_safe_numeric.
-- ============================================================================
DROP FUNCTION IF EXISTS fn_resumen_semanal_ra_pesadas_produccion(integer, integer, integer, integer[], text, text);

CREATE OR REPLACE FUNCTION fn_resumen_semanal_ra_pesadas_produccion(
    p_company_id  integer,
    p_anio        integer,
    p_sem_anio    integer,   -- NULL = todas las semanas del año
    p_granja_ids  integer[] DEFAULT NULL,
    p_regional    text      DEFAULT NULL,
    p_ciclo       text      DEFAULT NULL
)
RETURNS TABLE(
    lote_postura_produccion_id integer,
    lote_id                   integer,
    lote_nombre               text,
    granja_id                 integer,
    granja_nombre             text,
    nucleo_nombre             text,
    regional                  text,
    raza                      text,
    anio_guia                 integer,
    ciclo_produccion          text,
    tipo_nido                 text,
    edad_semana               integer,
    fecha_fin_semana          date,
    dias_con_registro         integer,
    part                      double precision,
    saldo_hembras             double precision,
    saldo_machos              double precision,
    produccion_pct            double precision,
    produccion_pct_guia       double precision,
    dif_produccion_pct        double precision,
    htaa                      double precision,
    htaa_guia                 double precision,
    dif_htaa                  double precision,
    hiaa                      double precision,
    hiaa_guia                 double precision,
    dif_hiaa                  double precision,
    aprov_sem_pct             double precision,
    aprov_sem_pct_guia        double precision,
    dif_aprov_sem_pct         double precision,
    gr_huevo_inc              double precision,
    mort_hembras_pct          double precision,
    retiro_acum_hembras_pct   double precision,
    retiro_acum_hembras_guia  double precision,
    mort_machos_pct           double precision,
    retiro_acum_machos_pct    double precision,
    retiro_acum_machos_guia   double precision,
    peso_macho_sobre_hembra   double precision
)
LANGUAGE sql STABLE AS $$
WITH
-- ── 1) Lotes de producción (LPP) de la empresa (+ ubicación, guía y fecha ref) ─
lote_base AS (
    SELECT lpp.lote_postura_produccion_id,
           lpp.lote_id,
           lpp.lote_nombre::text                                        AS lote_nombre,
           lpp.granja_id,
           f.name::text                                                 AS granja_nombre,
           n.nucleo_nombre::text                                        AS nucleo_nombre,
           COALESCE(NULLIF(mo.value, ''), NULLIF(lpp.regional, ''))::text AS regional,
           COALESCE(lpp.raza, '')::text                                 AS raza,
           lpp.ano_tabla_genetica                                       AS anio_guia,
           NULLIF(lpp.ciclo_produccion, '')::text                       AS ciclo_produccion,
           NULLIF(lpp.tipo_nido, '')::text                              AS tipo_nido,
           (COALESCE(lev.fecha_encaset, lpp.fecha_encaset, lpp.fecha_inicio_produccion)
                AT TIME ZONE 'America/Bogota')::date                    AS ref_date,
           COALESCE(lpp.aves_h_inicial, lpp.hembras_iniciales_prod, 0)::double precision AS base_h,
           COALESCE(lpp.aves_m_inicial, lpp.machos_iniciales_prod, 0)::double precision  AS base_m
      FROM lote_postura_produccion lpp
      JOIN farms f
        ON f.id = lpp.granja_id
      LEFT JOIN lote_postura_levante lev
        ON lev.lote_postura_levante_id = lpp.lote_postura_levante_id
       AND lev.deleted_at IS NULL
      LEFT JOIN nucleos n
        ON n.granja_id = lpp.granja_id
       AND n.nucleo_id = lpp.nucleo_id
       AND n.deleted_at IS NULL
      LEFT JOIN master_list_options mo
        ON mo.id = f.regional_id
     WHERE lpp.company_id = p_company_id
       AND lpp.deleted_at IS NULL
       AND (p_granja_ids IS NULL OR lpp.granja_id = ANY (p_granja_ids))
       AND (p_ciclo IS NULL OR COALESCE(lpp.ciclo_produccion, '') = p_ciclo)
),
lote_ok AS (
    SELECT * FROM lote_base WHERE ref_date IS NOT NULL
),
-- ── 2+3) Un registro por lote y DÍA desde fn_seguimiento_diario_produccion (la fn diaria
--         canónica ya hace el UNION dual-fuente + dedup por día Bogotá «gana el más
--         temprano»); solo días con registro (seg_id IS NOT NULL). Patrón CROSS JOIN LATERAL
--         del Reporte de Costos de engorde: la fn LANGUAGE sql se inlinea. ─────────────────
dedup AS (
    SELECT lo.lote_postura_produccion_id                   AS lpp_id,
           f.fecha                                         AS reg_date,
           COALESCE(f.mortalidad_hembras, 0)               AS mort_h,
           COALESCE(f.mortalidad_machos, 0)                AS mort_m,
           COALESCE(f.sel_h, 0)                            AS sel_h,
           COALESCE(f.cons_kg_h, 0)::double precision      AS cons_h,
           COALESCE(f.cons_kg_m, 0)::double precision      AS cons_m,
           COALESCE(f.huevo_tot, 0)                        AS huevo_tot,
           COALESCE(f.huevo_inc, 0)                        AS huevo_inc,
           f.peso_h::double precision                      AS peso_h,
           f.peso_m::double precision                      AS peso_m
      FROM lote_ok lo
      CROSS JOIN LATERAL fn_seguimiento_diario_produccion(lo.lote_postura_produccion_id, NULL) f
     WHERE f.seg_id IS NOT NULL
),
-- ── 4) Semana de vida (división entera, arranque en 25) ─────────────────────
reg AS (
    SELECT d.*,
           ((d.reg_date - lo.ref_date) / 7) + 1 AS sem
      FROM dedup d
      JOIN lote_ok lo ON lo.lote_postura_produccion_id = d.lpp_id
),
reg_ok AS (
    SELECT * FROM reg WHERE sem >= 25
),
-- ── 5) Agregado semanal por lote ────────────────────────────────────────────
sem AS (
    SELECT lpp_id,
           sem,
           COUNT(*)::int                     AS dias,
           SUM(mort_h)::double precision     AS mort_h,
           SUM(mort_m)::double precision     AS mort_m,
           SUM(sel_h)::double precision      AS sel_h,
           SUM(cons_h)                       AS cons_kg_h,
           SUM(cons_m)                       AS cons_kg_m,
           SUM(huevo_tot)::double precision  AS huevo_tot,
           SUM(huevo_inc)::double precision  AS huevo_inc,
           AVG(peso_h) FILTER (WHERE peso_h IS NOT NULL) AS peso_h,
           AVG(peso_m) FILTER (WHERE peso_m IS NOT NULL) AS peso_m
      FROM reg_ok
     GROUP BY lpp_id, sem
),
-- ── 6) Acumulados por ventana ──────────────────────────────────────────────
--    `*_prev` = acumulado EXCLUYENDO la semana actual ⇒ saldo al INICIO de la
--    semana, que es el denominador de todos los % (igual que la fn base).
acum AS (
    SELECT s.*,
           COALESCE(SUM(s.mort_h + s.sel_h) OVER (
               PARTITION BY s.lpp_id ORDER BY s.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING), 0) AS bajas_h_prev,
           COALESCE(SUM(s.mort_m) OVER (
               PARTITION BY s.lpp_id ORDER BY s.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING), 0) AS bajas_m_prev,
           SUM(s.mort_h + s.sel_h) OVER (
               PARTITION BY s.lpp_id ORDER BY s.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)     AS bajas_h_ac,
           SUM(s.mort_m) OVER (
               PARTITION BY s.lpp_id ORDER BY s.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)     AS bajas_m_ac,
           SUM(s.huevo_tot) OVER (
               PARTITION BY s.lpp_id ORDER BY s.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)     AS huevo_tot_ac,
           SUM(s.huevo_inc) OVER (
               PARTITION BY s.lpp_id ORDER BY s.sem
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)     AS huevo_inc_ac
      FROM sem s
),
-- ── 7) Solo la semana calendario pedida ────────────────────────────────────
sem_objetivo AS (
    SELECT lo.lote_postura_produccion_id, lo.lote_id, lo.lote_nombre, lo.granja_id, lo.granja_nombre,
           lo.nucleo_nombre, lo.regional, lo.raza, lo.anio_guia,
           lo.ciclo_produccion, lo.tipo_nido, lo.base_h, lo.base_m,
           a.sem, a.dias, a.mort_h, a.mort_m, a.sel_h,
           a.cons_kg_h, a.cons_kg_m, a.huevo_tot, a.huevo_inc,
           a.peso_h, a.peso_m,
           a.bajas_h_prev, a.bajas_m_prev, a.bajas_h_ac, a.bajas_m_ac,
           a.huevo_tot_ac, a.huevo_inc_ac,
           (lo.ref_date + ((a.sem - 1) * 7) + 6) AS fin_sem
      FROM acum a
      JOIN lote_ok lo ON lo.lote_postura_produccion_id = a.lpp_id
     WHERE EXTRACT(YEAR FROM (lo.ref_date + ((a.sem - 1) * 7) + 6))::int = p_anio
       -- p_sem_anio NULL = TODAS las semanas del año (curva del año completo);
       -- con valor, una sola semana calendario.
       AND (p_sem_anio IS NULL OR (
             floor(
               ( (lo.ref_date + ((a.sem - 1) * 7) + 6)
                 - date_trunc('year', (lo.ref_date + ((a.sem - 1) * 7) + 6)::timestamp)::date
                 + EXTRACT(DOW FROM date_trunc('year', (lo.ref_date + ((a.sem - 1) * 7) + 6)::timestamp))::int
               ) / 7.0
             )::int + 1
           ) = p_sem_anio)
       AND (p_regional IS NULL OR lo.regional = p_regional)
),
-- ── 8) Guía ────────────────────────────────────────────────────────────────
con_guia AS (
    SELECT so.*,
           f_safe_numeric(g.prod_porcentaje) AS g_prod_pct,
           f_safe_numeric(g.h_total_aa)      AS g_htaa,
           f_safe_numeric(g.h_inc_aa)        AS g_hiaa,
           f_safe_numeric(g.aprov_sem)       AS g_aprov_sem,
           f_safe_numeric(g.retiro_ac_h)     AS g_retiro_ac_h,
           f_safe_numeric(g.retiro_ac_m)     AS g_retiro_ac_m
      FROM sem_objetivo so
      LEFT JOIN LATERAL (
            SELECT gg.*
              FROM guia_genetica_sanmarino_colombia gg
             WHERE gg.company_id = p_company_id
               AND gg.deleted_at IS NULL
               AND lower(trim(gg.raza)) = lower(trim(COALESCE(so.raza, '')))
               AND trim(gg.anio_guia) = so.anio_guia::text
               -- ⚠️ Edad PARSEADA a número, igual que fn_indicadores_produccion_postura
               --    (`fn_parse_edad_numerica(g.edad) = s`) — distinto de la fn de
               --    levante, que compara texto exacto.
               --    La semana 25 tiene DOS filas en la guía y ambas parsean a 25:
               --      '25'  → cierre de LEVANTE   (retiro_ac_h 4,03)
               --      '25P' → arranque de PRODUCCIÓN (retiro_ac_h 0,10)
               --    La fn base resuelve con LIMIT 1 sin ORDER BY y el plan (index
               --    scan por company_id) le devuelve la fila '25P'. Acá el desempate
               --    se hace EXPLÍCITO —prefiriendo la variante con sufijo, que es la
               --    de producción— para no depender del plan de ejecución.
               AND fn_parse_edad_numerica(gg.edad) = so.sem
             ORDER BY (CASE WHEN btrim(gg.edad) = so.sem::text THEN 1 ELSE 0 END), gg.id
             LIMIT 1
      ) g ON true
),
-- ── 9) Saldos, pesos normalizados y derivadas ──────────────────────────────
calc AS (
    SELECT cg.*,
           GREATEST(0, cg.base_h - cg.bajas_h_prev) AS ini_h,
           GREATEST(0, cg.base_m - cg.bajas_m_prev) AS ini_m,
           GREATEST(0, cg.base_h - cg.bajas_h_ac)   AS fin_h,
           GREATEST(0, cg.base_m - cg.bajas_m_ac)   AS fin_m,
           CASE WHEN cg.peso_h IS NULL THEN NULL
                WHEN cg.peso_h > 100 THEN cg.peso_h / 1000.0 ELSE cg.peso_h END AS peso_h_kg,
           CASE WHEN cg.peso_m IS NULL THEN NULL
                WHEN cg.peso_m > 100 THEN cg.peso_m / 1000.0 ELSE cg.peso_m END AS peso_m_kg
      FROM con_guia cg
),
final AS (
    SELECT c.*,
           CASE WHEN c.dias > 0 AND c.ini_h > 0
                THEN (c.huevo_tot / c.dias) / c.ini_h * 100.0 END        AS prod_pct,
           CASE WHEN c.base_h > 0 THEN c.huevo_tot_ac / c.base_h END     AS htaa_real,
           CASE WHEN c.base_h > 0 THEN c.huevo_inc_ac / c.base_h END     AS hiaa_real,
           CASE WHEN c.huevo_tot > 0 THEN c.huevo_inc * 100.0 / c.huevo_tot END AS aprov_pct,
           CASE WHEN c.huevo_inc > 0
                THEN (c.cons_kg_h + c.cons_kg_m) * 1000.0 / c.huevo_inc END AS gr_huevo_inc_real
      FROM calc c
)
SELECT
    f.lote_postura_produccion_id,
    f.lote_id,
    f.lote_nombre,
    f.granja_id,
    f.granja_nombre,
    f.nucleo_nombre,
    f.regional,
    f.raza,
    f.anio_guia,
    f.ciclo_produccion,
    f.tipo_nido,
    f.sem                                                          AS edad_semana,
    f.fin_sem                                                      AS fecha_fin_semana,
    f.dias                                                         AS dias_con_registro,
    -- Participación sobre el TOTAL del resultado (misma ventana que la migración desplegada
    -- 20260728120000; el .sql llegó a tener un PARTITION BY fin_sem nunca migrado que dejaba
    -- part=1 con lotes de encaset distinto — realineado al comportamiento vivo). El C# además
    -- recalcula PART tras el recorte por alcance (ResumenSemanalRaPesadasCalculos).
    CASE WHEN SUM(f.fin_h) OVER () > 0
         THEN f.fin_h / SUM(f.fin_h) OVER () END                     AS part,
    f.fin_h                                                        AS saldo_hembras,
    f.fin_m                                                        AS saldo_machos,
    f.prod_pct                                                     AS produccion_pct,
    f.g_prod_pct::double precision                                 AS produccion_pct_guia,
    fn_dif_pct(f.prod_pct, f.g_prod_pct::double precision)         AS dif_produccion_pct,
    f.htaa_real                                                    AS htaa,
    f.g_htaa::double precision                                     AS htaa_guia,
    fn_dif_pct(f.htaa_real, f.g_htaa::double precision)            AS dif_htaa,
    f.hiaa_real                                                    AS hiaa,
    f.g_hiaa::double precision                                     AS hiaa_guia,
    fn_dif_pct(f.hiaa_real, f.g_hiaa::double precision)            AS dif_hiaa,
    f.aprov_pct                                                    AS aprov_sem_pct,
    f.g_aprov_sem::double precision                                AS aprov_sem_pct_guia,
    fn_dif_pct(f.aprov_pct, f.g_aprov_sem::double precision)       AS dif_aprov_sem_pct,
    f.gr_huevo_inc_real                                            AS gr_huevo_inc,
    CASE WHEN f.ini_h > 0 THEN f.mort_h / f.ini_h * 100.0 END      AS mort_hembras_pct,
    CASE WHEN f.base_h > 0 THEN f.bajas_h_ac / f.base_h * 100.0 END AS retiro_acum_hembras_pct,
    f.g_retiro_ac_h::double precision                              AS retiro_acum_hembras_guia,
    CASE WHEN f.ini_m > 0 THEN f.mort_m / f.ini_m * 100.0 END      AS mort_machos_pct,
    CASE WHEN f.base_m > 0 THEN f.bajas_m_ac / f.base_m * 100.0 END AS retiro_acum_machos_pct,
    f.g_retiro_ac_m::double precision                              AS retiro_acum_machos_guia,
    CASE WHEN COALESCE(f.peso_h_kg, 0) > 0 AND f.peso_m_kg IS NOT NULL
         THEN f.peso_m_kg / f.peso_h_kg * 100.0 END                AS peso_macho_sobre_hembra
  FROM final f
 ORDER BY f.sem DESC, f.lote_nombre;
$$;

COMMENT ON FUNCTION fn_resumen_semanal_ra_pesadas_produccion(integer, integer, integer, integer[], text, text)
IS 'Informe RA Pesadas — hoja RESUMEN SEMANAL, bloque Producción: una fila por lote para una semana calendario (WEEKNUM Excel). Set-based; equivalente 1:1 a fn_indicadores_produccion_postura (flujo lote).';
""";
    }
}
