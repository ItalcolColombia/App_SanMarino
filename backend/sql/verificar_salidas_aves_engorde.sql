-- =============================================================================
-- SALIDAS DE AVES EN ENGORDE: lotes liquidados que perdieron su encasetamiento,
-- y las definiciones de "salida" que no coinciden entre si.
--
-- POR QUE EXISTE
-- `fn_seguimiento_diario_engorde` reescribe el encasetamiento del lote CERRADO como
-- `GREATEST(1, bajas_seguimiento + total_ventas)` y el cierre CONGELA esa foto. Si la venta no
-- estaba registrada, las aves desaparecen del registro para siempre (salvo reabrir el lote).
--
-- El detector natural, `fn_cuadre_aves_engorde`, es CIEGO a esto: no mira `estado_operativo_lote`,
-- asi que un lote liquidado con miles de aves fantasma le devuelve `cuadra = true`.
--
-- ⚠️ POR QUE ESTO ES UN .sql Y NO UNA COLUMNA NUEVA EN LA fn
-- Medido el 25-ago-2026: `fn_cuadre_aves_engorde` NO tiene un solo consumidor en runtime (cero
-- `SqlQueryRaw`/`FromSql` en todo el backend). Agregarle columnas no crea una alarma —crea una
-- consulta que alguien tendria que escribir igual—, y cuesta DROP FUNCTION + cambio de firma +
-- migracion + Designer clonado. Esto da lo mismo, sin DDL y sin riesgo.
--
-- USO
--   psql ... -f backend/sql/verificar_salidas_aves_engorde.sql
--
-- COMO SE LEE
--   Chequeo 1: lo ya perdido. Chequeo 2: lo que se perderia al liquidar (usar ANTES de cerrar).
--   Chequeo 3: la trampa latente — cinco definiciones distintas de "salida de aves".
--
-- Plan: fase_de_desarrollo/correccion_bugs_anotados_plan.md
-- SIN-MIGRACION: diagnostico de solo lectura, no crea ningun objeto.
-- =============================================================================
\timing off

DROP VIEW IF EXISTS v_aves_engorde_por_lote;
CREATE TEMP VIEW v_aves_engorde_por_lote AS
SELECT l.lote_ave_engorde_id AS lote, l.lote_nombre, c.name AS empresa, f.name AS granja,
       l.liquidado_at::date AS liquidado,
       LOWER(COALESCE(l.estado_operativo_lote,'')) AS estado,
       COALESCE(l.aves_encasetadas,0) AS encasetadas,
       COALESCE((SELECT SUM(COALESCE(s.mortalidad_hembras,0)+COALESCE(s.mortalidad_machos,0)
                           +COALESCE(s.sel_h,0)+COALESCE(s.sel_m,0)
                           +COALESCE(s.error_sexaje_hembras,0)+COALESCE(s.error_sexaje_machos,0))
                 FROM seguimiento_diario_aves_engorde s
                 WHERE s.lote_ave_engorde_id = l.lote_ave_engorde_id), 0) AS bajas,
       COALESCE((SELECT SUM(COALESCE(h.cantidad_hembras,0)+COALESCE(h.cantidad_machos,0)
                           +COALESCE(h.cantidad_mixtas,0))
                 FROM lote_registro_historico_unificado h
                 WHERE h.lote_ave_engorde_id = l.lote_ave_engorde_id
                   AND h.tipo_evento = 'VENTA_AVES' AND NOT h.anulado), 0) AS ventas
FROM lote_ave_engorde l
JOIN companies c ON c.id = l.company_id
JOIN farms f ON f.id = l.granja_id
WHERE l.deleted_at IS NULL;

\echo ''
\echo '=== 1. YA PERDIDO: lotes liquidados cuyo encasetamiento quedo reescrito hacia abajo ==='
\echo '    (la foto esta congelada: solo se recupera reabriendo el lote)'
SELECT empresa,
       COUNT(*) FILTER (WHERE liquidado IS NOT NULL) AS liquidados,
       COUNT(*) FILTER (WHERE liquidado IS NOT NULL AND encasetadas - GREATEST(1, bajas+ventas) > 100) AS con_perdida,
       COALESCE(SUM(GREATEST(encasetadas - GREATEST(1, bajas+ventas), 0))
                FILTER (WHERE liquidado IS NOT NULL), 0) AS aves_perdidas
FROM v_aves_engorde_por_lote GROUP BY 1 ORDER BY 4 DESC;

\echo ''
\echo '--- detalle de los liquidados con perdida > 100 aves ---'
SELECT empresa, granja, lote, lote_nombre, liquidado, encasetadas, bajas, ventas,
       encasetadas - GREATEST(1, bajas+ventas) AS aves_perdidas
FROM v_aves_engorde_por_lote
WHERE liquidado IS NOT NULL AND encasetadas - GREATEST(1, bajas+ventas) > 100
ORDER BY 9 DESC;

\echo ''
\echo '=== 2. A PUNTO DE PERDERSE: lotes ABIERTOS y que se perderia si se liquidaran hoy ==='
\echo '    LEER ASI: 0 -> listo para liquidar. > 0 -> falta registrar la VENTA (ver chequeo 3).'
SELECT empresa, granja, lote, lote_nombre, encasetadas, bajas, ventas,
       encasetadas - GREATEST(1, bajas+ventas) AS se_perderia
FROM v_aves_engorde_por_lote
WHERE liquidado IS NULL AND encasetadas - GREATEST(1, bajas+ventas) > 100
ORDER BY 8 DESC;

\echo ''
\echo '=== 3. LATENTE: no todas las salidas cuentan como salida ==='
\echo '    El trigger trg_lote_hist_desde_movimiento_pollo_engorde emite VENTA_AVES SOLO para'
\echo '    tipo_movimiento = ''Venta'' (create_lote_registro_historico_unificado.sql), pero'
\echo '    MovimientoPolloEngordeService.EsSalidaVenta cuenta Venta|Despacho|Retiro. O sea que un'
\echo '    Despacho descuenta aves del maestro y NO alimenta total_ventas => al liquidar, esas aves'
\echo '    tambien desaparecen. Medido el 25-ago-2026 esto NO tenia victimas (solo existen'
\echo '    movimientos ''Venta''), pero se arma solo el dia que alguien registre un Despacho.'
SELECT m.tipo_movimiento, m.estado, COUNT(*) AS movimientos,
       SUM(COALESCE(m.cantidad_hembras,0)+COALESCE(m.cantidad_machos,0)+COALESCE(m.cantidad_mixtas,0)) AS aves,
       CASE WHEN m.tipo_movimiento = 'Venta' THEN 'si, alimenta total_ventas'
            ELSE '*** NO alimenta total_ventas: estas aves se pierden al liquidar ***' END AS cuenta_para_la_fn
FROM movimiento_pollo_engorde m
GROUP BY 1,2 ORDER BY 3 DESC;

\echo ''
\echo '--- control: cada movimiento Venta no anulado tiene que tener su fila VENTA_AVES ---'
SELECT (SELECT COUNT(*) FROM movimiento_pollo_engorde
        WHERE tipo_movimiento='Venta' AND estado <> 'Anulado') AS movimientos_venta,
       (SELECT COUNT(*) FROM lote_registro_historico_unificado
        WHERE tipo_evento='VENTA_AVES' AND NOT anulado)        AS filas_historico,
       CASE WHEN (SELECT COUNT(*) FROM movimiento_pollo_engorde
                  WHERE tipo_movimiento='Venta' AND estado <> 'Anulado')
               = (SELECT COUNT(*) FROM lote_registro_historico_unificado
                  WHERE tipo_evento='VENTA_AVES' AND NOT anulado)
            THEN 'OK — calzan' ELSE '*** FALLA: el trigger no escribio todas ***' END AS chequeo;
