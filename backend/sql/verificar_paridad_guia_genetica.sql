-- =============================================================================
-- GATE DE PARIDAD de GUÍA GENÉTICA — la medición que convierte «delta cero» en un hecho.
--
-- POR QUÉ EXISTE
-- Hay TRES tablas de guía genética y se quedan separadas a propósito:
--
--   1. guia_genetica_sanmarino_colombia   -> Sanmarino / Demo / Ecuador (postura + reproductora)
--   2. guia_genetica_ecuador_header/_detalle -> Ecuador + Panamá (pollo engorde)
--   3. guia_genetica_santa_reyes          -> Santa Reyes (postura) — nació seed-only
--
-- El trabajo de X19 construye superficie NUEVA sobre la tercera y promete «cero cambio de
-- comportamiento fuera de Santa Reyes». Sin este archivo esa promesa es una afirmación: nadie
-- estaba midiendo qué devuelven hoy los objetos SQL que leen las otras dos. Este script congela
-- esa salida POR EMPRESA y, en la segunda corrida, muestra el delta.
--
-- Es el mismo patrón de `verificar_paridad_saldo_engorde.sql` y `verificar_cuadre_alimento_engorde.sql`.
--
-- USO — el mismo comando las dos veces, sin flags:
--
--     psql ... -f backend/sql/verificar_paridad_guia_genetica.sql     <- ANTES del cambio (congela)
--     ... aplicar el cambio (migración, fn nueva, refactor del C#) ...
--     psql ... -f backend/sql/verificar_paridad_guia_genetica.sql     <- DESPUÉS (compara)
--
-- Para empezar de cero:  DROP TABLE diagnostico.paridad_guia_base;
--
-- CÓMO SE LEE
-- Toda empresa que NO sea Santa Reyes tiene que salir con 0 en TODAS las columnas del bloque
-- «DIFERENCIAS». Cualquier otra cosa se justifica por escrito antes de mergear.
--
-- SEGURIDAD
-- SOLO LECTURA sobre las tablas de negocio. Lo único que escribe son sus dos tablas de snapshot,
-- que viven en un esquema propio `diagnostico` (no en `public`) y llevan COMMENT que las marca
-- como diagnóstico desechable. NO ejecuta ninguna función que escriba (ver §fn_congelar abajo).
--
-- GATE DE MIGRACIÓN (`backend/scripts/verificar-sql-llega-por-migracion.js`)
-- No lleva `-- SIN-MIGRACION:`. Ese script sólo exige migración a los `fn_*.sql` / `vw_*.sql`, y
-- exime por PREFIJO a `verificar_*` justamente porque son diagnósticos de solo lectura que se
-- corren a mano contra un dump. Este archivo entra por esa exención; agregarle la marca sería
-- ruido (el gate hace `continue` antes de leerla).
-- =============================================================================

\timing off
\set ON_ERROR_STOP on

-- ── Determinismo: sin esto el verificador miente ─────────────────────────────
-- MEDIDO el 26-ago-2026: `fn_informe_semanal_pollo_engorde(5)` devuelve **212 filas** con la zona
-- por defecto del servidor (America/Bogota) y **213 filas** con la sesión en UTC. La fn deriva
-- fechas de columnas `timestamptz`, así que la zona de la SESIÓN cambia el resultado. Un gate que
-- da distinto según desde qué shell se corre no sirve: se pinan las tres GUC que afectan la
-- representación textual que va al hash.
--   · timezone           -> se fija al default del servidor, para que los números congelados sean
--                           los mismos que ve un psql normal y los que usan las fns internamente
--                           (todas hacen `AT TIME ZONE 'America/Bogota'`).
--   · extra_float_digits -> el `::text` de un double precision depende de esta GUC.
--   · DateStyle          -> el `::text` de date/timestamp depende de esta.
SET timezone           = 'America/Bogota';
SET extra_float_digits = 3;
SET DateStyle          = 'ISO, MDY';

-- ── Esquema de diagnóstico ───────────────────────────────────────────────────
CREATE SCHEMA IF NOT EXISTS diagnostico;
COMMENT ON SCHEMA diagnostico IS
  'Snapshots desechables de los verificar_*.sql. NADA de la app lee este esquema: se puede borrar entero.';

DROP TABLE IF EXISTS diagnostico.paridad_guia_nuevo;
CREATE TABLE diagnostico.paridad_guia_nuevo (
    objeto     text    NOT NULL,   -- qué objeto SQL produjo la fila
    company_id integer,            -- empresa dueña del dato (la unidad de comparación)
    clave      text    NOT NULL,   -- clave natural de la fila, como texto
    n_filas    integer NOT NULL,   -- cuántas filas colapsan en esa clave (hay vistas sin clave única)
    hash_guia  text,               -- md5 SOLO de las columnas que salen de la guía genética
    hash_fila  text                -- md5 de la fila COMPLETA (señal más amplia, informativa)
);
COMMENT ON TABLE diagnostico.paridad_guia_nuevo IS
  'Diagnóstico desechable — corrida actual de verificar_paridad_guia_genetica.sql.';

-- `hash_guia` es la señal DURA: son las columnas que un cambio en la guía movería.
-- `hash_fila` es la señal ANCHA: cambia también si se movió un dato de operación (un seguimiento
-- nuevo, una mortalidad corregida). Sirve para distinguir «rompí la guía» de «cambiaron los datos».

-- =============================================================================
-- 1) fn_indicadores_levante_postura(p_lote_id)
--    CONJUNTO REPRESENTATIVO: **todos** los lotes de levante no borrados, de todas las empresas.
--    Criterio: la población entera corre en ~0,2 s (22 lotes / 166 filas). Cuando el universo
--    completo es barato, muestrear sólo introduce el riesgo de no mirar justo el lote que se rompió.
--    Re-entrante: la fn hace `DROP TABLE IF EXISTS _seg_sem` al entrar, así que el LATERAL es seguro.
-- =============================================================================
INSERT INTO diagnostico.paridad_guia_nuevo (objeto, company_id, clave, n_filas, hash_guia, hash_fila)
SELECT 'fn_indicadores_levante_postura', company_id, clave, count(*),
       md5(string_agg(hg, '|' ORDER BY hg)), md5(string_agg(hf, '|' ORDER BY hf))
FROM (
    SELECT l.company_id,
           l.lote_id::text || '|' || f.semana AS clave,
           md5(ROW(f.consumo_tabla, f.peso_tabla, f.unif_tabla, f.mort_tabla, f.ganancia_tabla,
                   f.dif_peso_pct,
                   f.consumo_tabla_hembras, f.consumo_tabla_machos,
                   f.peso_tabla_hembras,    f.peso_tabla_machos,
                   f.mort_tabla_hembras,    f.mort_tabla_machos,
                   f.dif_peso_pct_hembras,  f.dif_peso_pct_machos)::text) AS hg,
           md5(to_jsonb(f)::text)                                          AS hf
    FROM lotes l
    CROSS JOIN LATERAL fn_indicadores_levante_postura(l.lote_id) f
    WHERE l.deleted_at IS NULL
) s
GROUP BY 1, 2, 3;

-- =============================================================================
-- 2) fn_indicadores_produccion_postura(p_company_id, p_lote_postura_produccion_id)
--    CONJUNTO REPRESENTATIVO: **todos** los lotes de producción no borrados (mismo criterio que 1).
--
--    ⚠️ NO es re-entrante: la fn hace `CREATE TEMP TABLE _seg ON COMMIT DROP` sin dropearla al
--    entrar, así que un `CROSS JOIN LATERAL` (una sola transacción, N invocaciones) revienta con
--    `relation "_seg" already exists` en la segunda vuelta. Por eso va en un LOOP que borra la
--    temporal entre iteración e iteración. No es un rodeo estético: sin esto el bloque no corre.
-- =============================================================================
CREATE TEMP TABLE _paridad_prod_raw (company_id integer, clave text, hg text, hf text);

DO $$
DECLARE r record;
BEGIN
    FOR r IN SELECT lpp.company_id, lpp.lote_postura_produccion_id AS id
               FROM lote_postura_produccion lpp
              WHERE lpp.deleted_at IS NULL
              ORDER BY 1, 2
    LOOP
        INSERT INTO _paridad_prod_raw (company_id, clave, hg, hf)
        SELECT r.company_id,
               r.id::text || '|' || f.semana,
               md5(ROW(f.mortalidad_guia_hembras,   f.mortalidad_guia_machos,
                       f.diferencia_mortalidad_hembras, f.diferencia_mortalidad_machos,
                       f.consumo_guia_hembras,      f.consumo_guia_machos,
                       f.diferencia_consumo_hembras, f.diferencia_consumo_machos,
                       f.huevos_totales_guia,       f.huevos_incubables_guia,
                       f.porcentaje_produccion_guia,
                       f.diferencia_huevos_totales, f.diferencia_huevos_incubables,
                       f.diferencia_porcentaje_produccion,
                       f.peso_huevo_guia,           f.diferencia_peso_huevo,
                       f.peso_guia_hembras,         f.peso_guia_machos,
                       f.diferencia_peso_hembras,   f.diferencia_peso_machos,
                       f.uniformidad_guia,          f.diferencia_uniformidad,
                       f.retiro_ac_h_guia,          f.retiro_ac_m_guia)::text),
               md5(to_jsonb(f)::text)
        FROM fn_indicadores_produccion_postura(r.company_id, r.id) f;

        DROP TABLE IF EXISTS pg_temp._seg;   -- ← lo que hace re-entrante al bucle
    END LOOP;
END $$;

INSERT INTO diagnostico.paridad_guia_nuevo (objeto, company_id, clave, n_filas, hash_guia, hash_fila)
SELECT 'fn_indicadores_produccion_postura', company_id, clave, count(*),
       md5(string_agg(hg, '|' ORDER BY hg)), md5(string_agg(hf, '|' ORDER BY hf))
FROM _paridad_prod_raw
GROUP BY 1, 2, 3;

DROP TABLE _paridad_prod_raw;

-- =============================================================================
-- 3) fn_resumen_semanal_ra_pesadas_levante(p_company_id, p_anio, p_sem_anio, ...)
--    CONJUNTO REPRESENTATIVO: (empresa × año) derivado de los DATOS, no de una lista a mano —
--    todos los años en los que la empresa tiene un registro de levante, más el año siguiente
--    (la semana de un registro de fin de diciembre cierra en enero del año que viene).
--    `p_sem_anio = NULL` significa, dentro de la fn, TODAS las semanas del año ⇒ un solo llamado
--    por año cubre la curva completa. `p_granja_ids`/`p_regional` NULL ⇒ sin filtrar.
--    El join `sl.lote_id = l.lote_id::text` replica el de la fn (la columna es varchar legado).
-- =============================================================================
INSERT INTO diagnostico.paridad_guia_nuevo (objeto, company_id, clave, n_filas, hash_guia, hash_fila)
SELECT 'fn_resumen_semanal_ra_pesadas_levante', company_id, clave, count(*),
       md5(string_agg(hg, '|' ORDER BY hg)), md5(string_agg(hf, '|' ORDER BY hf))
FROM (
    WITH anios AS (
        SELECT DISTINCT l.company_id, y.anio
        FROM lotes l
        JOIN seguimiento_diario_levante s
          ON s.lote_id = l.lote_id::text AND s.tipo_seguimiento = 'levante'
        CROSS JOIN LATERAL (VALUES (EXTRACT(YEAR FROM s.fecha)::int),
                                   (EXTRACT(YEAR FROM s.fecha)::int + 1)) AS y(anio)
        WHERE l.deleted_at IS NULL
    )
    SELECT a.company_id,
           a.anio::text || '|' || f.lote_id::text || '|' || f.edad_semana AS clave,
           md5(ROW(f.raza, f.anio_guia,
                   f.retiro_acum_hembras_guia, f.retiro_acum_machos_guia,
                   f.dif_consumo_hembras_pct,  f.dif_peso_hembras_pct,
                   f.dif_consumo_machos_pct,   f.dif_peso_machos_pct)::text) AS hg,
           md5(to_jsonb(f)::text)                                            AS hf
    FROM anios a
    CROSS JOIN LATERAL fn_resumen_semanal_ra_pesadas_levante(a.company_id, a.anio, NULL) f
) s
GROUP BY 1, 2, 3;

-- =============================================================================
-- 4) fn_resumen_semanal_ra_pesadas_produccion(p_company_id, p_anio, p_sem_anio, ...)
--    Mismo criterio de (empresa × año) que el bloque 3, sobre seguimiento_diario_produccion.
-- =============================================================================
INSERT INTO diagnostico.paridad_guia_nuevo (objeto, company_id, clave, n_filas, hash_guia, hash_fila)
SELECT 'fn_resumen_semanal_ra_pesadas_produccion', company_id, clave, count(*),
       md5(string_agg(hg, '|' ORDER BY hg)), md5(string_agg(hf, '|' ORDER BY hf))
FROM (
    WITH anios AS (
        SELECT DISTINCT lpp.company_id, y.anio
        FROM lote_postura_produccion lpp
        JOIN seguimiento_diario_produccion s
          ON s.lote_postura_produccion_id = lpp.lote_postura_produccion_id
        CROSS JOIN LATERAL (VALUES (EXTRACT(YEAR FROM s.fecha_registro)::int),
                                   (EXTRACT(YEAR FROM s.fecha_registro)::int + 1)) AS y(anio)
        WHERE lpp.deleted_at IS NULL AND s.deleted_at IS NULL
    )
    SELECT a.company_id,
           a.anio::text || '|' || f.lote_postura_produccion_id::text || '|' || f.edad_semana AS clave,
           md5(ROW(f.raza, f.anio_guia,
                   f.produccion_pct_guia,   f.dif_produccion_pct,
                   f.htaa_guia,             f.dif_htaa,
                   f.hiaa_guia,             f.dif_hiaa,
                   f.aprov_sem_pct_guia,    f.dif_aprov_sem_pct,
                   f.retiro_acum_hembras_guia, f.retiro_acum_machos_guia)::text) AS hg,
           md5(to_jsonb(f)::text)                                                AS hf
    FROM anios a
    CROSS JOIN LATERAL fn_resumen_semanal_ra_pesadas_produccion(a.company_id, a.anio, NULL) f
) s
GROUP BY 1, 2, 3;

-- =============================================================================
-- 5) fn_informe_semanal_pollo_engorde(p_company_id, ...)
--    CONJUNTO REPRESENTATIVO: una llamada por empresa que tenga al menos un lote de engorde vivo,
--    con TODOS los demás parámetros en NULL ⇒ sin filtro de granja, núcleo, galpón, lote ni fecha.
--    O sea: el universo entero de engorde de esa empresa en una sola invocación.
-- =============================================================================
INSERT INTO diagnostico.paridad_guia_nuevo (objeto, company_id, clave, n_filas, hash_guia, hash_fila)
SELECT 'fn_informe_semanal_pollo_engorde', company_id, clave, count(*),
       md5(string_agg(hg, '|' ORDER BY hg)), md5(string_agg(hf, '|' ORDER BY hf))
FROM (
    SELECT f.company_id,
           f.lote_ave_engorde_id::text || '|' || f.semana AS clave,
           md5(ROW(f.consumo_tabla_g, f.peso_tabla_g, f.ganancia_tabla_g,
                   f.conversion_tabla, f.mortalidad_tabla_pct,
                   f.pct_consumo, f.pct_peso, f.pct_conversion)::text) AS hg,
           md5(to_jsonb(f)::text)                                      AS hf
    FROM (SELECT DISTINCT company_id FROM lote_ave_engorde WHERE deleted_at IS NULL) d
    CROSS JOIN LATERAL fn_informe_semanal_pollo_engorde(d.company_id) f
) s
GROUP BY 1, 2, 3;

-- =============================================================================
-- 6) vw_guia_genetica_por_lote_postura  — vista, sin parámetros: se congela ENTERA.
--    OJO: su clave natural NO es única (medido: 546 filas de Levante sobre 525 claves distintas,
--    porque un lote puede aparecer por levante y por producción del mismo período). Por eso el
--    snapshot guarda `n_filas` y colapsa los duplicados con `string_agg(... ORDER BY hash)`:
--    el resultado es independiente del orden en que la vista los devuelva.
--    Todas sus columnas no-clave SON la guía ⇒ hash_guia == hash de la fila.
-- =============================================================================
INSERT INTO diagnostico.paridad_guia_nuevo (objeto, company_id, clave, n_filas, hash_guia, hash_fila)
SELECT 'vw_guia_genetica_por_lote_postura', company_id, clave, count(*),
       md5(string_agg(hf, '|' ORDER BY hf)), md5(string_agg(hf, '|' ORDER BY hf))
FROM (
    SELECT v.company_id,
           coalesce(v.etapa, '-') || '|' || coalesce(v.lote_postura_id::text, '-') || '|'
                                  || coalesce(v.lote_id::text, '-')         || '|'
                                  || coalesce(v.semana::text, '-')          AS clave,
           md5(to_jsonb(v)::text)                                           AS hf
    FROM vw_guia_genetica_por_lote_postura v
) s
GROUP BY 1, 2, 3;

-- =============================================================================
-- 7) vw_indicadores_diarios_engorde — vista, sin parámetros: se congela ENTERA (6.762 filas).
-- =============================================================================
INSERT INTO diagnostico.paridad_guia_nuevo (objeto, company_id, clave, n_filas, hash_guia, hash_fila)
SELECT 'vw_indicadores_diarios_engorde', company_id, clave, count(*),
       md5(string_agg(hg, '|' ORDER BY hg)), md5(string_agg(hf, '|' ORDER BY hf))
FROM (
    SELECT v.company_id,
           v.lote_ave_engorde_id::text || '|' || coalesce(v.fecha_registro::text, '-') AS clave,
           md5(ROW(v.guia_genetica_ecuador_header_id,
                   v.peso_tabla_g, v.ganancia_diaria_tabla_g, v.consumo_diario_tabla_g,
                   v.alimento_acum_tabla_g, v.ca_tabla, v.mort_sel_tabla_pct,
                   v.dif_peso_vs_tabla_pct)::text) AS hg,
           md5(to_jsonb(v)::text)                  AS hf
    FROM vw_indicadores_diarios_engorde v
) s
GROUP BY 1, 2, 3;

-- =============================================================================
-- 8) fn_congelar_liquidacion_engorde(p_lote_id, p_user, p_origen)  — **NO SE EJECUTA**
--
--    ESCRIBE: hace `INSERT INTO liquidacion_lote_engorde_congelada` y
--    `INSERT INTO liquidacion_lote_engorde_congelada_fila`. Correrla para «medir» dejaría
--    liquidaciones congeladas falsas en la BD — exactamente lo que CLAUDE.md prohíbe («verificar
--    antes de limpiar datos»: simulá, no escribas).
--
--    EN SU LUGAR se congela, en modo SOLO LECTURA, la ÚNICA lectura de guía que la fn hace: el
--    id del header de Ecuador que resolvería para cada lote. Es literalmente la misma subconsulta
--    que vive dentro de la fn (company_id + deleted_at IS NULL + estado='active' + anio_guia +
--    raza ILIKE, ORDER BY id LIMIT 1). Si un cambio moviera lo que la fn congelaría, este bloque
--    lo ve; y si no se mueve, la fn no puede haber cambiado por el lado de la guía.
-- =============================================================================
INSERT INTO diagnostico.paridad_guia_nuevo (objeto, company_id, clave, n_filas, hash_guia, hash_fila)
SELECT 'fn_congelar_liquidacion_engorde:lookup_guia', company_id, clave, count(*),
       md5(string_agg(hg, '|' ORDER BY hg)), md5(string_agg(hg, '|' ORDER BY hg))
FROM (
    SELECT l.company_id,
           l.lote_ave_engorde_id::text AS clave,
           md5(ROW(l.raza, l.ano_tabla_genetica, l.codigo_guia_genetica,
                   (SELECT h.id
                      FROM guia_genetica_ecuador_header h
                     WHERE h.company_id = l.company_id
                       AND h.deleted_at IS NULL
                       AND h.estado = 'active'
                       AND h.anio_guia = l.ano_tabla_genetica
                       AND h.raza ILIKE l.raza
                     ORDER BY h.id
                     LIMIT 1))::text) AS hg
    FROM lote_ave_engorde l
    WHERE l.deleted_at IS NULL
) s
GROUP BY 1, 2, 3;

-- =============================================================================
-- 9) LAS TRES TABLAS DE GUÍA, crudas — la CAUSA, no el efecto.
--    Si mañana el diff marca que se movió `fn_indicadores_levante_postura`, estas filas dicen si
--    fue porque alguien tocó la guía o porque cambió un dato de operación.
--    `guia_genetica_ecuador_detalle` se atribuye a la empresa por su propia columna company_id.
-- =============================================================================
INSERT INTO diagnostico.paridad_guia_nuevo (objeto, company_id, clave, n_filas, hash_guia, hash_fila)
SELECT 'tabla:guia_genetica_sanmarino_colombia', t.company_id, t.id::text, 1,
       md5(to_jsonb(t)::text), md5(to_jsonb(t)::text)
FROM guia_genetica_sanmarino_colombia t;

INSERT INTO diagnostico.paridad_guia_nuevo (objeto, company_id, clave, n_filas, hash_guia, hash_fila)
SELECT 'tabla:guia_genetica_ecuador_header', t.company_id, t.id::text, 1,
       md5(to_jsonb(t)::text), md5(to_jsonb(t)::text)
FROM guia_genetica_ecuador_header t;

INSERT INTO diagnostico.paridad_guia_nuevo (objeto, company_id, clave, n_filas, hash_guia, hash_fila)
SELECT 'tabla:guia_genetica_ecuador_detalle', t.company_id, t.id::text, 1,
       md5(to_jsonb(t)::text), md5(to_jsonb(t)::text)
FROM guia_genetica_ecuador_detalle t;

INSERT INTO diagnostico.paridad_guia_nuevo (objeto, company_id, clave, n_filas, hash_guia, hash_fila)
SELECT 'tabla:guia_genetica_santa_reyes', t.company_id, t.id::text, 1,
       md5(to_jsonb(t)::text), md5(to_jsonb(t)::text)
FROM guia_genetica_santa_reyes t;

-- =============================================================================
-- LÍNEA BASE / COMPARACIÓN
-- =============================================================================
DO $$
BEGIN
    IF to_regclass('diagnostico.paridad_guia_base') IS NULL THEN
        CREATE TABLE diagnostico.paridad_guia_base AS
            SELECT now() AS tomada_el, * FROM diagnostico.paridad_guia_nuevo;
        COMMENT ON TABLE diagnostico.paridad_guia_base IS
          'Diagnóstico desechable — línea base de verificar_paridad_guia_genetica.sql. DROP para reiniciar.';
        RAISE NOTICE '';
        RAISE NOTICE '>>> LINEA BASE CREADA (% filas). Aplica tu cambio y volve a correr este mismo script.',
                     (SELECT count(*) FROM diagnostico.paridad_guia_base);
        RAISE NOTICE '';
    ELSE
        RAISE NOTICE '';
        RAISE NOTICE '>>> COMPARANDO contra la linea base (% filas, tomada el %).',
                     (SELECT count(*) FROM diagnostico.paridad_guia_base),
                     (SELECT max(tomada_el) FROM diagnostico.paridad_guia_base);
        RAISE NOTICE '';
    END IF;
END $$;

\echo ''
\echo '=== 1) DIFERENCIAS POR EMPRESA Y OBJETO — todo en 0 = el cambio no la toca ==='
\echo '    dif_guia  = movio una columna que SALE DE LA GUIA. Es la senal dura.'
\echo '    dif_fila  = movio cualquier columna (puede ser un dato de operacion). Es informativa.'

SELECT coalesce(c.name, '(sin empresa: ' || coalesce(k.company_id::text, 'NULL') || ')') AS empresa,
       k.objeto,
       count(*) FILTER (WHERE b.clave IS NOT NULL)                                AS filas_base,
       count(*) FILTER (WHERE b.clave IS NOT NULL AND n.clave IS NULL)            AS filas_que_desaparecen,
       count(*) FILTER (WHERE b.clave IS NULL     AND n.clave IS NOT NULL)        AS filas_nuevas,
       count(*) FILTER (WHERE b.clave IS NOT NULL AND n.clave IS NOT NULL
                          AND b.n_filas <> n.n_filas)                             AS dif_multiplicidad,
       count(*) FILTER (WHERE b.clave IS NOT NULL AND n.clave IS NOT NULL
                          AND b.hash_guia IS DISTINCT FROM n.hash_guia)           AS dif_guia,
       count(*) FILTER (WHERE b.clave IS NOT NULL AND n.clave IS NOT NULL
                          AND b.hash_fila IS DISTINCT FROM n.hash_fila)           AS dif_fila
FROM (
    SELECT coalesce(b.objeto, n.objeto)         AS objeto,
           coalesce(b.company_id, n.company_id) AS company_id,
           b.clave AS bc, n.clave AS nc
    FROM diagnostico.paridad_guia_base b
    FULL JOIN diagnostico.paridad_guia_nuevo n
           ON n.objeto = b.objeto
          AND n.company_id IS NOT DISTINCT FROM b.company_id
          AND n.clave = b.clave
) k
LEFT JOIN diagnostico.paridad_guia_base  b ON b.objeto = k.objeto
                                          AND b.company_id IS NOT DISTINCT FROM k.company_id
                                          AND b.clave = k.bc
LEFT JOIN diagnostico.paridad_guia_nuevo n ON n.objeto = k.objeto
                                          AND n.company_id IS NOT DISTINCT FROM k.company_id
                                          AND n.clave = k.nc
LEFT JOIN companies c ON c.id = k.company_id
GROUP BY 1, 2
ORDER BY 1, 2;

\echo ''
\echo '=== 2) LAS PRIMERAS 30 CLAVES QUE CAMBIARON (para justificar el diff) ==='

SELECT coalesce(c.name, '(sin empresa)') AS empresa,
       coalesce(b.objeto, n.objeto)      AS objeto,
       coalesce(b.clave,  n.clave)       AS clave,
       CASE WHEN b.clave IS NULL                                    THEN 'NUEVA'
            WHEN n.clave IS NULL                                    THEN 'desaparecio'
            WHEN b.hash_guia IS DISTINCT FROM n.hash_guia           THEN 'CAMBIO LA GUIA'
            WHEN b.n_filas   <> n.n_filas                           THEN 'cambio la multiplicidad'
            ELSE 'cambio un dato de operacion (guia intacta)'
       END AS que_paso
FROM diagnostico.paridad_guia_base b
FULL JOIN diagnostico.paridad_guia_nuevo n
       ON n.objeto = b.objeto
      AND n.company_id IS NOT DISTINCT FROM b.company_id
      AND n.clave = b.clave
LEFT JOIN companies c ON c.id = coalesce(b.company_id, n.company_id)
WHERE b.clave IS NULL
   OR n.clave IS NULL
   OR b.hash_guia IS DISTINCT FROM n.hash_guia
   OR b.hash_fila IS DISTINCT FROM n.hash_fila
   OR b.n_filas   <> n.n_filas
ORDER BY 1, 2, 3
LIMIT 30;

\echo ''
\echo '=== 3) LINEA BASE POR EMPRESA — cuantas filas de guia tiene cada una, por tabla ==='

SELECT c.name AS empresa,
       count(*) FILTER (WHERE g.tabla = 'sanmarino_colombia') AS guia_sanmarino_colombia,
       count(*) FILTER (WHERE g.tabla = 'ecuador_header')     AS guia_ecuador_header,
       count(*) FILTER (WHERE g.tabla = 'ecuador_detalle')    AS guia_ecuador_detalle,
       count(*) FILTER (WHERE g.tabla = 'santa_reyes')        AS guia_santa_reyes
FROM companies c
LEFT JOIN (
    SELECT company_id, 'sanmarino_colombia' AS tabla FROM guia_genetica_sanmarino_colombia
    UNION ALL SELECT company_id, 'ecuador_header'  FROM guia_genetica_ecuador_header
    UNION ALL SELECT company_id, 'ecuador_detalle' FROM guia_genetica_ecuador_detalle
    UNION ALL SELECT company_id, 'santa_reyes'     FROM guia_genetica_santa_reyes
) g ON g.company_id = c.id
GROUP BY c.id, c.name
ORDER BY c.name;

\echo ''
\echo '=== 4) COBERTURA DE LA CORRIDA — cuantas claves congelo cada objeto, por empresa ==='

SELECT coalesce(c.name, '(sin empresa: ' || coalesce(n.company_id::text, 'NULL') || ')') AS empresa,
       n.objeto,
       count(*)      AS claves,
       sum(n.n_filas) AS filas
FROM diagnostico.paridad_guia_nuevo n
LEFT JOIN companies c ON c.id = n.company_id
GROUP BY 1, 2
ORDER BY 1, 2;
