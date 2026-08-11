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
     -- A5: la fila borrada no cuenta. La fn ya filtraba el `deleted_at` de `lotes`, `lpp`, `lpl` y
     -- `movimiento_aves`, pero NO el de la tabla de la que saca los seguimientos. La columna existe
     -- y está mapeada en EF; hoy nadie la escribe (los borrados son físicos), así que esto es un
     -- no-op sobre los datos actuales — y deja de haber una forma silenciosa de inflar el saldo el
     -- día que algo empiece a marcarla.
     WHERE sp.deleted_at IS NULL
       AND ( (p_lote_postura_produccion_id IS NOT NULL
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
