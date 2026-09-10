-- ============================================================================
-- verificar_venta_engorde_kilos_por_empresa.sql — DIAGNÓSTICO, sólo lectura.
-- ----------------------------------------------------------------------------
-- Contesta «¿hay ventas de pollo engorde que cuentan aves y aportan 0 kg?» en TODAS las empresas,
-- que es el gate multipaís de este cambio: la empresa que no es el objetivo tiene que salir igual
-- antes y después. Correr ANTES del backfill (congela la línea base) y DESPUÉS (compara).
--
-- Cómo leerlo:
--   * `bruto_igual_tara`  → el defecto de digitación (bruto y tara con el MISMO número ⇒ neto 0).
--   * `sin_peso`          → ventas sin báscula (peso NULL). Es otro problema, no se toca acá.
--   * `kg_por_ave`        → el control de sanidad: un pollo de engorde pesa ~2 a ~3,2 kg. Un valor
--                           de 0 dice que los kilos no llegaron; uno de ~7 dice que la columna trae
--                           el camión, no el pollo.
-- ============================================================================

\echo '=== 1) Ventas por empresa: cómo viene el peso ==='
SELECT c.name                                                                      AS empresa,
       count(*)                                                                    AS ventas,
       count(*) FILTER (WHERE m.peso_bruto = m.peso_tara)                          AS bruto_igual_tara,
       count(*) FILTER (WHERE m.peso_bruto IS NULL AND m.peso_tara IS NULL)         AS sin_peso,
       count(*) FILTER (WHERE COALESCE(m.peso_neto, 0) = 0)                        AS neto_en_cero,
       round(sum(COALESCE(m.peso_neto, 0))::numeric, 1)                            AS kg_netos,
       round((sum(COALESCE(m.peso_neto, 0))
              / NULLIF(sum(m.cantidad_hembras + m.cantidad_machos + m.cantidad_mixtas), 0))::numeric, 3)
                                                                                   AS kg_por_ave
FROM public.movimiento_pollo_engorde m
JOIN public.companies c ON c.id = m.company_id
WHERE m.deleted_at IS NULL
  AND m.tipo_movimiento = 'Venta'
GROUP BY c.name
ORDER BY c.name;

\echo '=== 2) Espejo histórico (lo que leen la tabla diaria y el informe semanal) ==='
SELECT c.name                                             AS empresa,
       count(*)                                           AS filas,
       sum(COALESCE(h.cantidad_hembras, 0)
         + COALESCE(h.cantidad_machos, 0)
         + COALESCE(h.cantidad_mixtas, 0))                AS aves,
       round(sum(COALESCE(h.peso_neto, 0)), 1)            AS kg,
       count(*) FILTER (WHERE COALESCE(h.peso_neto, 0) = 0) AS filas_sin_kg
FROM public.lote_registro_historico_unificado h
JOIN public.companies c ON c.id = h.company_id
WHERE h.tipo_evento = 'VENTA_AVES'
  AND NOT h.anulado
GROUP BY c.name
ORDER BY c.name;

\echo '=== 3) Lotes con liquidación CONGELADA cuya copia quedó en 0 kg =========='
\echo '    Tiene que salir VACÍO. La tabla diaria de un lote liquidado no se calcula: sale de la'
\echo '    foto (fn_seguimiento_diario_engorde arranca con un UNION sobre la copia congelada), así'
\echo '    que corregir los movimientos no le mueve un kilo. La migración corrige esas copias'
\echo '    tocando SÓLO las 3 columnas del despacho — no re-congela, que regeneraría la'
\echo '    liquidación aprobada entera con la fórmula de hoy.'
SELECT c.name                                                          AS empresa,
       l.lote_ave_engorde_id                                           AS lote,
       l.lote_nombre,
       count(f.*)                                                      AS filas_congeladas,
       sum(f.despacho_hembras + f.despacho_machos + f.despacho_mixtas) AS aves_despachadas,
       round(sum(COALESCE(f.despacho_peso_neto, 0))::numeric, 1)       AS kg_congelados
FROM public.liquidacion_lote_engorde_congelada lc
JOIN public.liquidacion_lote_engorde_congelada_fila f ON f.liquidacion_id = lc.id
JOIN public.lote_ave_engorde l ON l.lote_ave_engorde_id = lc.lote_ave_engorde_id
JOIN public.companies c ON c.id = l.company_id
WHERE lc.anulada_at IS NULL
GROUP BY c.name, l.lote_ave_engorde_id, l.lote_nombre
HAVING sum(f.despacho_hembras + f.despacho_machos + f.despacho_mixtas) > 0
   AND sum(COALESCE(f.despacho_peso_neto, 0)) = 0
ORDER BY c.name, l.lote_ave_engorde_id;
