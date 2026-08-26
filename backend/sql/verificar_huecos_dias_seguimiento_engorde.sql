-- =============================================================================
-- HUECOS DE DIAS en el seguimiento diario de pollo engorde.
--
-- POR QUE EXISTE
-- La regla que se va a implementar dice: «todos los dias tienen que tener registro hasta que se
-- liquide el lote». Antes de encenderla hay que saber a cuantos lotes deja bloqueados el dia del
-- deploy, y sobre todo SEPARAR dos cosas que parecen la misma y no lo son:
--
--   * HUECO INTERIOR: un dia salteado entre el primer y el ultimo registro. Es un olvido real,
--     el operario puede llenarlo, y bloquear por el es correcto.
--   * COLA: el lote dejo de registrarse y nunca mas. En Panama esto NO es un olvido: es como
--     termina un lote alli, porque no registran la venta ni liquidan (3 ventas en todo el sistema
--     contra 1.452 de Ecuador). Exigir esos dias es pedir que INVENTEN registros de lotes cuyas
--     aves ya salieron de la granja.
--
-- Medido el 25-ago-2026 sobre la copia de produccion: Panama 565 dias faltantes en 44 lotes, de los
-- cuales 524 son cola; Ecuador 133 en 5 lotes, 129 de cola. El 93% del numero grande es cola.
--
-- USO
--   psql ... -f backend/sql/verificar_huecos_dias_seguimiento_engorde.sql
--
-- COMO SE LEE
--   El chequeo 2 (interiores) es el trabajo real que la regla le va a pedir al operario.
--   El chequeo 3 (cola) es una decision de operacion: se liquida, no se captura.
--
-- Plan: fase_de_desarrollo/plazo_validacion_desde_creacion_plan.md (seccion 9)
-- SIN-MIGRACION: diagnostico de solo lectura, no crea ningun objeto.
-- =============================================================================
\timing off

DROP VIEW IF EXISTS v_rango_lotes_engorde;
CREATE TEMP VIEW v_rango_lotes_engorde AS
SELECT l.lote_ave_engorde_id AS lote, l.lote_nombre, c.name AS empresa, f.name AS granja,
       c.requiere_validacion_seguimiento_diario AS flag_on,
       l.fecha_encaset::date AS encaset,
       MIN(s.fecha)::date AS primer, MAX(s.fecha)::date AS ultimo, COUNT(*) AS registros
FROM lote_ave_engorde l
JOIN companies c ON c.id = l.company_id
JOIN farms f ON f.id = l.granja_id
JOIN seguimiento_diario_aves_engorde s ON s.lote_ave_engorde_id = l.lote_ave_engorde_id
WHERE l.deleted_at IS NULL AND l.liquidado_at IS NULL
GROUP BY 1,2,3,4,5,6;

\echo ''
\echo '=== 1. Resumen por empresa: cuanto de lo que falta es hueco y cuanto es cola ==='
SELECT r.empresa, r.flag_on,
       COUNT(DISTINCT r.lote)                       AS lotes_afectados,
       COUNT(*)                                     AS dias_faltantes,
       COUNT(*) FILTER (WHERE d::date <= r.ultimo)  AS interiores,
       COUNT(*) FILTER (WHERE d::date >  r.ultimo)  AS cola,
       MAX(CURRENT_DATE - d::date)                  AS mas_viejo_dias
FROM v_rango_lotes_engorde r
CROSS JOIN LATERAL generate_series(r.primer, CURRENT_DATE - 1, '1 day') d
WHERE NOT EXISTS (SELECT 1 FROM seguimiento_diario_aves_engorde s
                  WHERE s.lote_ave_engorde_id = r.lote AND s.fecha::date = d::date)
GROUP BY 1,2 ORDER BY 4 DESC;

\echo ''
\echo '=== 2. HUECOS INTERIORES: los dias concretos que el operario tiene que llenar ==='
\echo '    (esto es lo que la regla le va a pedir. El mensaje de bloqueo debe nombrar estas fechas)'
SELECT r.empresa, r.granja, r.lote, r.lote_nombre,
       COUNT(*) AS dias,
       string_agg(to_char(d,'YYYY-MM-DD'), ', ' ORDER BY d) AS faltantes
FROM v_rango_lotes_engorde r
CROSS JOIN LATERAL generate_series(r.primer, r.ultimo, '1 day') d
WHERE NOT EXISTS (SELECT 1 FROM seguimiento_diario_aves_engorde s
                  WHERE s.lote_ave_engorde_id = r.lote AND s.fecha::date = d::date)
GROUP BY 1,2,3,4 ORDER BY 1, 5 DESC, 3;

\echo ''
\echo '=== 3. COLA: lotes que dejaron de registrarse. NO se llenan capturando: se liquidan ==='
\echo '    (un engorde se saca a ~42 dias. Edad muy por encima de eso + 0 salidas = lote terminado'
\echo '     que nadie cerro. Pedirle registros diarios a esto es pedirle datos falsos)'
SELECT r.empresa, r.granja, r.lote, r.lote_nombre,
       CURRENT_DATE - r.encaset          AS edad_dias,
       r.ultimo                          AS ultimo_registro,
       CURRENT_DATE - r.ultimo - 1       AS dias_de_cola,
       COALESCE(v.salidas, 0)            AS aves_con_salida_registrada
FROM v_rango_lotes_engorde r
LEFT JOIN (
    SELECT m.lote_ave_engorde_origen_id AS lote,
           SUM(COALESCE(m.cantidad_hembras,0)+COALESCE(m.cantidad_machos,0)+COALESCE(m.cantidad_mixtas,0)) AS salidas
    FROM movimiento_pollo_engorde m
    WHERE m.fecha_cancelacion IS NULL AND m.estado <> 'Anulado'
      AND m.lote_ave_engorde_origen_id IS NOT NULL
    GROUP BY 1
) v ON v.lote = r.lote
WHERE CURRENT_DATE - r.ultimo - 1 > 7
ORDER BY 7 DESC;

\echo ''
\echo '=== 4. Por que la cola de Panama es estructural: casi no registran la venta ==='
SELECT c.name AS empresa, m.estado, COUNT(*) AS ventas
FROM movimiento_pollo_engorde m
JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = m.lote_ave_engorde_origen_id
JOIN companies c ON c.id = l.company_id
GROUP BY 1,2 ORDER BY 1,3 DESC;
