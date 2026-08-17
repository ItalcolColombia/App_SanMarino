-- =============================================================================
-- Paridad del SALDO DE AVES DE LEVANTE entre sus dos funciones semanales.
--
-- POR QUÉ EXISTE
-- El saldo de levante llegó a tener CUATRO consumidores y DOS fórmulas: unos descuentan la venta de
-- aves y otros no, así que el mismo lote y la misma semana mostraban dos conteos distintos según la
-- pantalla (lote 143 sem 24: 10.619 en Indicadores contra 10.329 en el reporte semanal). Es la misma
-- clase de divergencia que CLAUDE.md prohíbe en § «Una sola fórmula por número», y no se notaba
-- porque casi nadie había registrado ventas de levante todavía.
--
-- QUÉ COMPARA
-- `fn_indicadores_levante_postura(lote)`.aves_fin_semana  vs
-- `fn_reporte_semanal_levante_extras(lote)`.(aves_hembras_fin + aves_machos_fin)
-- para TODOS los lotes de levante y TODAS sus semanas.
--
-- USO — el mismo comando las dos veces, sin flags:
--
--     psql ... -f backend/sql/verificar_paridad_saldo_levante.sql     <- ANTES del cambio (congela)
--     ... aplicar el cambio ...
--     psql ... -f backend/sql/verificar_paridad_saldo_levante.sql     <- DESPUÉS (compara)
--
-- CÓMO SE LEE
-- `dif_vs_extras` tiene que ir a CERO (las dos fns coinciden) y `filas_que_cambian` sólo puede
-- moverse en los lotes con venta registrada: un lote sin ventas no puede cambiar ni un número.
-- Para empezar de cero: DROP TABLE _paridad_levante_base;
-- =============================================================================

\timing off
\set ON_ERROR_STOP on

DROP TABLE IF EXISTS _paridad_levante_nuevo;
CREATE TABLE _paridad_levante_nuevo AS
SELECT l.company_id,
       l.lote_id,
       i.semana,
       i.aves_fin_semana                                  AS ind_saldo,
       (e.aves_hembras_fin + e.aves_machos_fin)           AS ext_saldo,
       i.aves_fin_hembras,
       i.aves_fin_machos,
       i.peso_cierre,
       i.consumo_total_semana,
       i.mortalidad_sem
FROM lotes l
CROSS JOIN LATERAL fn_indicadores_levante_postura(l.lote_id) i
LEFT JOIN LATERAL (
    SELECT x.aves_hembras_fin, x.aves_machos_fin
      FROM fn_reporte_semanal_levante_extras(l.lote_id) x
     WHERE x.semana = i.semana
) e ON TRUE
WHERE l.deleted_at IS NULL;

DO $$
BEGIN
    IF to_regclass('public._paridad_levante_base') IS NULL THEN
        CREATE TABLE _paridad_levante_base AS SELECT * FROM _paridad_levante_nuevo;
        RAISE NOTICE '';
        RAISE NOTICE '>>> LINEA BASE CREADA (% filas). Aplica tu cambio y volve a correr este mismo script.',
                     (SELECT count(*) FROM _paridad_levante_base);
        RAISE NOTICE '';
    ELSE
        RAISE NOTICE '';
        RAISE NOTICE '>>> COMPARANDO contra la linea base (% filas).', (SELECT count(*) FROM _paridad_levante_base);
        RAISE NOTICE '';
    END IF;
END $$;

\echo '=== LAS DOS FUNCIONES, POR EMPRESA (dif_vs_extras debe terminar en 0) ==='

SELECT c.name                                                                  AS empresa,
       count(*)                                                                AS filas,
       count(*) FILTER (WHERE n.ext_saldo IS NOT NULL
                          AND n.ind_saldo IS DISTINCT FROM n.ext_saldo)        AS dif_vs_extras,
       coalesce(max(abs(n.ind_saldo - n.ext_saldo))
                FILTER (WHERE n.ext_saldo IS NOT NULL), 0)                     AS peor_dif_aves
FROM _paridad_levante_nuevo n
JOIN companies c ON c.id = n.company_id
GROUP BY c.name
ORDER BY c.name;

\echo ''
\echo '=== QUE CAMBIO RESPECTO DE LA LINEA BASE (solo pueden moverse los lotes CON venta) ==='

SELECT c.name                                                                  AS empresa,
       count(*) FILTER (WHERE b.ind_saldo IS DISTINCT FROM n.ind_saldo)        AS filas_saldo_cambiado,
       count(*) FILTER (WHERE b.aves_fin_hembras IS DISTINCT FROM n.aves_fin_hembras
                           OR b.aves_fin_machos  IS DISTINCT FROM n.aves_fin_machos) AS filas_sexo_cambiado,
       count(*) FILTER (WHERE b.peso_cierre          IS DISTINCT FROM n.peso_cierre
                           OR b.consumo_total_semana IS DISTINCT FROM n.consumo_total_semana
                           OR b.mortalidad_sem       IS DISTINCT FROM n.mortalidad_sem) AS filas_otras_columnas
FROM _paridad_levante_base b
JOIN _paridad_levante_nuevo n ON n.lote_id = b.lote_id AND n.semana = b.semana
JOIN companies c ON c.id = b.company_id
GROUP BY c.name
ORDER BY c.name;

\echo ''
\echo '=== DETALLE DE LAS FILAS QUE SIGUEN DESALINEADAS (deberia quedar vacio) ==='

SELECT n.company_id, n.lote_id, n.semana, n.ind_saldo, n.ext_saldo,
       (n.ind_saldo - n.ext_saldo) AS diferencia
FROM _paridad_levante_nuevo n
WHERE n.ext_saldo IS NOT NULL AND n.ind_saldo IS DISTINCT FROM n.ext_saldo
ORDER BY abs(n.ind_saldo - n.ext_saldo) DESC
LIMIT 20;
