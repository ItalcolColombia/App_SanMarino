-- ============================================================================
-- backfill_liquidacion_congelada_kilos_venta.sql
-- ESPEJO de la 2ª parte de la migración FixVentaEngordeNetoCeroBrutoIgualTara
-- (el vehículo es la migración; este archivo es la versión legible).
-- ----------------------------------------------------------------------------
-- POR QUÉ HACE FALTA
--   La tabla diaria de un lote LIQUIDADO no se calcula: `fn_seguimiento_diario_engorde` arranca con
--   un `SELECT ... FROM liquidacion_lote_engorde_congelada_fila ... UNION ALL <cálculo vivo>`, así
--   que si el lote tiene copia vigente devuelve la FOTO. Corregir `movimiento_pollo_engorde` no
--   mueve un solo kilo ahí. Medido el 9-sep-2026: 4 lotes de Panamá (161/163/164/165, 176.930 aves)
--   quedaron congelados con 0 kg.
--
-- POR QUÉ **NO** SE RE-CONGELA
--   `fn_recongelar_liquidacion_engorde` regenera la copia entera con la fórmula de HOY. Medido:
--   reescribe **57 de esas 171 filas** en columnas que no son la de kilos (saldo de aves, saldo de
--   alimento, consumo, mortalidad), porque la fn avanzó de v13/v15 a v18 desde que se congelaron.
--   Eso no es corregir los kilos: es re-liquidar. Acá se tocan **sólo las 3 columnas del despacho**,
--   y el resto de la liquidación aprobada queda byte a byte igual — incluido el resumen de la
--   cabecera, que nunca dependió de los kilos de venta (son conteos de aves y saldo de alimento).
--
-- DE DÓNDE SALEN LOS KILOS
--   Del MISMO CTE `ventas_por_fecha` que usa la fn: suma por fecha de `lote_registro_historico_unificado`
--   (`VENTA_AVES`, no anulado). Se corre DESPUÉS del backfill de `movimiento_pollo_engorde`, así que
--   el espejo ya trae los kilos corregidos.
--
-- GUARDA: sólo se toca una fila si (a) su copia está vigente, (b) hoy tiene 0 kg, (c) el día trae
--   kilos, y (d) las aves H/M/mixtas de la fila coinciden EXACTO con las del día. Si no coinciden,
--   la fila no es el mismo día del mismo lote y se deja quieta (aparecerá en el verificador).
--
-- FECHAS REPETIDAS: un día puede tener dos seguimientos ⇒ dos filas. La fn hace
--   `LEFT JOIN ventas_por_fecha ON fecha`, o sea le pone a las DOS filas el total del día; el UPDATE
--   junta por fecha y reproduce exactamente eso.
--
-- IDEMPOTENTE: tras correr, esas filas tienen kg > 0 y dejan de cumplir la guarda (b).
-- ============================================================================

-- 1) Respaldo previo de las 3 columnas (para revertir sin adivinar).
CREATE TABLE IF NOT EXISTS public._backup_liq_congelada_fila_kilos (
    fila_id                     BIGINT PRIMARY KEY,
    liquidacion_id              BIGINT,
    despacho_peso_neto          NUMERIC,
    despacho_peso_tara          NUMERIC,
    despacho_promedio_peso_ave  NUMERIC,
    checksum_cabecera           VARCHAR(64),
    respaldado_en               TIMESTAMPTZ NOT NULL DEFAULT now()
);

INSERT INTO public._backup_liq_congelada_fila_kilos (
    fila_id, liquidacion_id, despacho_peso_neto, despacho_peso_tara,
    despacho_promedio_peso_ave, checksum_cabecera)
SELECT f.id, f.liquidacion_id, f.despacho_peso_neto, f.despacho_peso_tara,
       f.despacho_promedio_peso_ave, c.checksum
FROM public.liquidacion_lote_engorde_congelada_fila f
JOIN public.liquidacion_lote_engorde_congelada c ON c.id = f.liquidacion_id
WHERE c.anulada_at IS NULL
  AND (f.despacho_hembras + f.despacho_machos + f.despacho_mixtas) > 0
  AND COALESCE(f.despacho_peso_neto, 0) = 0
  AND NOT EXISTS (SELECT 1 FROM public._backup_liq_congelada_fila_kilos b WHERE b.fila_id = f.id);

-- 2) Corrección de las 3 columnas del despacho.
WITH ventas AS (
    SELECT h.lote_ave_engorde_id                                  AS lote,
           DATE(h.fecha_operacion)                                AS fecha,
           COALESCE(SUM(COALESCE(h.cantidad_hembras, 0)), 0)      AS dh,
           COALESCE(SUM(COALESCE(h.cantidad_machos,  0)), 0)      AS dm,
           COALESCE(SUM(COALESCE(h.cantidad_mixtas,  0)), 0)      AS dx,
           COALESCE(SUM(COALESCE(h.peso_neto,      0)), 0)        AS kg,
           COALESCE(SUM(COALESCE(h.peso_tara_real, 0)), 0)        AS tara
    FROM public.lote_registro_historico_unificado h
    WHERE h.tipo_evento = 'VENTA_AVES'
      AND NOT h.anulado
    GROUP BY 1, 2
)
UPDATE public.liquidacion_lote_engorde_congelada_fila f
   SET despacho_peso_neto         = v.kg,
       despacho_peso_tara         = v.tara,
       despacho_promedio_peso_ave = v.kg / (f.despacho_hembras + f.despacho_machos + f.despacho_mixtas)
  FROM public.liquidacion_lote_engorde_congelada c,
       ventas v
 WHERE c.id = f.liquidacion_id
   AND c.anulada_at IS NULL
   AND v.lote  = c.lote_ave_engorde_id
   AND v.fecha = f.fecha
   AND (f.despacho_hembras + f.despacho_machos + f.despacho_mixtas) > 0
   AND COALESCE(f.despacho_peso_neto, 0) = 0
   AND v.kg > 0
   AND f.despacho_hembras = v.dh
   AND f.despacho_machos  = v.dm
   AND f.despacho_mixtas  = v.dx;

-- 3) Checksum y rastro. El checksum se recalcula con la MISMA expresión de
--    `fn_congelar_liquidacion_engorde` — y como el lote tiene copia vigente,
--    `fn_seguimiento_diario_engorde` devuelve justamente estas filas ya corregidas, así que el
--    valor vuelve a describir el contenido. Dejar el viejo sería un checksum que miente.
UPDATE public.liquidacion_lote_engorde_congelada c
   SET checksum = COALESCE((
           SELECT md5(string_agg(x::text, '|' ORDER BY x.orden))
           FROM (SELECT row_number() OVER (ORDER BY g.fecha, COALESCE(g.seg_id, 0)) AS orden, g.*
                   FROM fn_seguimiento_diario_engorde(c.lote_ave_engorde_id) g) x
       ), md5('')),
       metadata = COALESCE(c.metadata, '{}'::jsonb) || jsonb_build_object(
           'correccionKilosVenta', jsonb_build_object(
               'aplicadaEn', now(),
               'motivo', 'Ventas registradas con peso bruto = peso tara (neto 0). Se corrigieron '
                      || 'SOLO las columnas de despacho de la copia congelada; el resto de la '
                      || 'liquidacion aprobada quedo intacto.',
               'filas', (SELECT count(*) FROM public._backup_liq_congelada_fila_kilos b
                          WHERE b.liquidacion_id = c.id)))
 WHERE c.anulada_at IS NULL
   AND EXISTS (SELECT 1 FROM public._backup_liq_congelada_fila_kilos b WHERE b.liquidacion_id = c.id)
   AND NOT (COALESCE(c.metadata, '{}'::jsonb) ? 'correccionKilosVenta');
