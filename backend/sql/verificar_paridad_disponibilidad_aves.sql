-- verificar_paridad_disponibilidad_aves.sql
-- SIN-MIGRACION: diagnostico de SOLO LECTURA (prefijo verificar_*, exento del gate). No crea nada.
--
-- Compara las DOS fuentes que el front usa hoy para responder "cuantas aves puedo mover":
--
--   A) GET /api/Lote/{id}/resumen-mortalidad   -> LoteService.GetMortalidadResumenAsync (saldoHembras)
--      base = lote_etapa_levante.aves_inicio_hembras (si existe) si no lotes.hembras_l
--      - mort_caja_h - bajas de LEVANTE + ingresos/salidas de los ESPEJOS (acumulados)
--      Lo usa: modal-traslado-aves-seguimiento (Camino C).
--
--   B) GET /api/traslados/lote/{id}/disponibilidad -> DisponibilidadLoteService (aves.hembrasVivas)
--      base = lotes.hembras_l
--      - bajas de LEVANTE - bajas de PRODUCCION - salidas/+ ingresos de MOVIMIENTO_AVES (Completado)
--      Lo usan: inventario-dashboard y /traslados-aves/nuevo (Camino A).
--
-- Difieren en CUATRO dimensiones, no en redondeo:
--   1. la base (lote_etapa_levante vs hembras_l)
--   2. la mortalidad de caja (solo A la resta)
--   3. las bajas de PRODUCCION (solo B las resta)  <- causa los lotes 13 y 14
--   4. la fuente de los traslados (espejo acumulado vs movimiento_aves) <- causa los receptores
--
-- Medido el 3-sep-2026 en la copia local: 9 de 15 lotes con actividad divergen, hasta 19.385 aves.
-- Por eso NO se unifico la fuente en el front (gate B2 del plan
-- fase_de_desarrollo/consolidacion_traslados_aves_huevos_plan.md): ninguna de las dos es correcta
-- en todos los casos y elegir una cambiaria numeros que autorizan traslados.
--
-- Uso: psql ... -f backend/sql/verificar_paridad_disponibilidad_aves.sql

WITH base AS (
  SELECT l.lote_id, l.company_id,
         COALESCE(l.hembras_l,0) AS hembras_l,
         COALESCE(lel.aves_inicio_hembras, l.hembras_l, 0) AS base_resumen_h,
         COALESCE(l.mort_caja_h,0) AS mort_caja_h
  FROM lotes l
  LEFT JOIN lote_etapa_levante lel ON lel.lote_id = l.lote_id
  WHERE l.deleted_at IS NULL
),
lev AS (
  SELECT lote_id::int AS lote_id,
         SUM(COALESCE(mortalidad_hembras,0)+COALESCE(sel_h,0)+COALESCE(error_sexaje_hembras,0)) AS bajas_lev_h
  FROM seguimiento_diario_levante
  WHERE tipo_seguimiento='levante' AND lote_id ~ '^[0-9]+$'
  GROUP BY 1
),
prod AS (
  SELECT lpp.lote_id,
         SUM(COALESCE(sp.mortalidad_hembras,0)+COALESCE(sp.sel_h,0)+COALESCE(sp.error_sexaje_hembras,0)) AS bajas_prod_h
  FROM seguimiento_diario_produccion sp
  JOIN lote_postura_produccion lpp ON lpp.lote_postura_produccion_id = sp.lote_postura_produccion_id
  GROUP BY 1
),
espejo AS (
  SELECT b.lote_id,
    COALESCE((SELECT SUM(COALESCE(levante_traslado_ingreso_hembras,0)) FROM lote_postura_levante x
              WHERE x.lote_id=b.lote_id AND x.deleted_at IS NULL AND x.company_id=b.company_id),0)
  + COALESCE((SELECT SUM(COALESCE(produccion_traslado_ingreso_hembras,0)) FROM lote_postura_produccion y
              WHERE y.lote_id=b.lote_id AND y.deleted_at IS NULL AND y.company_id=b.company_id),0) AS esp_in_h,
    COALESCE((SELECT SUM(COALESCE(levante_traslado_salida_hembras,0)) FROM lote_postura_levante x
              WHERE x.lote_id=b.lote_id AND x.deleted_at IS NULL AND x.company_id=b.company_id),0)
  + COALESCE((SELECT SUM(COALESCE(produccion_traslado_salida_hembras,0)) FROM lote_postura_produccion y
              WHERE y.lote_id=b.lote_id AND y.deleted_at IS NULL AND y.company_id=b.company_id),0) AS esp_out_h
  FROM base b
),
mov AS (
  SELECT b.lote_id,
    COALESCE((SELECT SUM(cantidad_hembras) FROM movimiento_aves m
              WHERE m.lote_origen_id=b.lote_id AND m.estado='Completado'),0) AS mov_out_h,
    COALESCE((SELECT SUM(cantidad_hembras) FROM movimiento_aves m
              WHERE m.lote_destino_id=b.lote_id AND m.estado='Completado'),0) AS mov_in_h
  FROM base b
)
SELECT b.lote_id, b.company_id,
       b.base_resumen_h AS base_res, b.hembras_l AS base_disp, b.mort_caja_h AS mcaja,
       COALESCE(lev.bajas_lev_h,0) AS bajas_lev, COALESCE(prod.bajas_prod_h,0) AS bajas_prod,
       e.esp_in_h, e.esp_out_h, mv.mov_in_h, mv.mov_out_h,
       GREATEST(0, b.base_resumen_h - b.mort_caja_h - COALESCE(lev.bajas_lev_h,0)
                   + e.esp_in_h - e.esp_out_h)                                   AS a_saldo_resumen,
       GREATEST(0, b.hembras_l - COALESCE(lev.bajas_lev_h,0) - COALESCE(prod.bajas_prod_h,0)
                   - mv.mov_out_h + mv.mov_in_h)                                 AS b_vivas_disponibilidad,
       GREATEST(0, b.base_resumen_h - b.mort_caja_h - COALESCE(lev.bajas_lev_h,0) + e.esp_in_h - e.esp_out_h)
     - GREATEST(0, b.hembras_l - COALESCE(lev.bajas_lev_h,0) - COALESCE(prod.bajas_prod_h,0) - mv.mov_out_h + mv.mov_in_h)
                                                                                 AS delta
FROM base b
LEFT JOIN lev  ON lev.lote_id  = b.lote_id
LEFT JOIN prod ON prod.lote_id = b.lote_id
JOIN espejo e  ON e.lote_id    = b.lote_id
JOIN mov mv    ON mv.lote_id   = b.lote_id
WHERE COALESCE(lev.bajas_lev_h,0)+COALESCE(prod.bajas_prod_h,0)
    + e.esp_in_h+e.esp_out_h+mv.mov_in_h+mv.mov_out_h+b.mort_caja_h > 0
ORDER BY ABS(
       GREATEST(0, b.base_resumen_h - b.mort_caja_h - COALESCE(lev.bajas_lev_h,0) + e.esp_in_h - e.esp_out_h)
     - GREATEST(0, b.hembras_l - COALESCE(lev.bajas_lev_h,0) - COALESCE(prod.bajas_prod_h,0) - mv.mov_out_h + mv.mov_in_h)
) DESC, b.lote_id;
