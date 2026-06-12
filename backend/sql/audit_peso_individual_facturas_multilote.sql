-- ============================================================================
-- AUDITORÍA (SOLO LECTURA) — Peso individual por lote en despachos (G4)
-- ----------------------------------------------------------------------------
-- Poblaciones en movimiento_pollo_engorde (Venta/Despacho/Retiro):
--   1-2) CLON GLOBAL SIN PRORRATEAR: las líneas del despacho comparten el MISMO
--        peso_bruto/peso_tara (camión clonado) y la suma de netos ≠ neto global
--        ⇒ la liquidación por lote queda mal (sobreconteo n× o reparto contra
--        otro global). OrganizarPeso re-prorratea los casos claros y manda a
--        revisión manual los contradictorios.
--   3)   PESO POR LÍNEA CORRUPTO: líneas con pesaje PROPIO (bruto/tara propios)
--        cuyo peso_neto NO es su propio bruto−tara — daño de la versión anterior
--        del reproceso (prorrateó pesajes propios desde el peso de una línea).
--        ESTA es la población que distorsiona la liquidación por lote hoy.
--        OrganizarPeso restaura neto = bruto−tara.
--   4)   PESO POR LÍNEA con *_global mal guardados (individual correcto): solo
--        se corrigen los campos *_global (= suma del despacho). Informativa.
--   5)   HUÉRFANOS sospechosos (sin factura ni número): revisión manual.
--   6)   Resumen por lote de las poblaciones 1-3 (lo que ve mal la liquidación).
--
-- El fix de datos NO se hace aquí: se ejecuta vía endpoint OrganizarPeso
-- (DryRun=true + ReprocesarTodo=true → revisión → DryRun=false).
-- ============================================================================

-- ── 1) Clones sin prorratear — facturas nuevas (factura_id) ──────────────────
WITH lineas AS (
    SELECT m.id, m.factura_id, m.lote_ave_engorde_origen_id,
           (m.cantidad_hembras + m.cantidad_machos + m.cantidad_mixtas) AS aves,
           m.peso_bruto, m.peso_tara,
           COALESCE(m.peso_neto,
                    CASE WHEN m.peso_bruto IS NOT NULL AND m.peso_tara IS NOT NULL
                         THEN m.peso_bruto - m.peso_tara END) AS neto_efectivo
    FROM public.movimiento_pollo_engorde m
    WHERE m.deleted_at IS NULL AND m.estado <> 'Cancelado'
      AND m.tipo_movimiento IN ('Venta','Despacho','Retiro')
      AND m.factura_id IS NOT NULL AND m.peso_bruto IS NOT NULL
)
SELECT 'factura_clon_sin_prorratear' AS poblacion,
       l.factura_id::text AS clave,
       COUNT(*) AS lineas, SUM(l.aves) AS aves,
       MAX(l.peso_bruto - l.peso_tara) AS neto_global_camion,
       SUM(l.neto_efectivo) AS kg_en_liquidacion,
       SUM(l.neto_efectivo) - MAX(l.peso_bruto - l.peso_tara) AS kg_diferencia,
       array_agg(DISTINCT l.lote_ave_engorde_origen_id) AS lotes
FROM lineas l
GROUP BY l.factura_id
HAVING COUNT(*) > 1
   AND COUNT(DISTINCT l.peso_bruto) = 1 AND COUNT(DISTINCT l.peso_tara) = 1
   AND ABS(SUM(l.neto_efectivo) - MAX(l.peso_bruto - l.peso_tara)) > 0.01
ORDER BY ABS(SUM(l.neto_efectivo) - MAX(l.peso_bruto - l.peso_tara)) DESC;

-- ── 2) Clones sin prorratear — legacy (numero_despacho + granja, sin fecha) ──
WITH lineas AS (
    SELECT m.id, TRIM(m.numero_despacho) AS nd, m.granja_origen_id,
           m.lote_ave_engorde_origen_id,
           (m.cantidad_hembras + m.cantidad_machos + m.cantidad_mixtas) AS aves,
           m.peso_bruto, m.peso_tara,
           COALESCE(m.peso_neto,
                    CASE WHEN m.peso_bruto IS NOT NULL AND m.peso_tara IS NOT NULL
                         THEN m.peso_bruto - m.peso_tara END) AS neto_efectivo
    FROM public.movimiento_pollo_engorde m
    WHERE m.deleted_at IS NULL AND m.estado <> 'Cancelado'
      AND m.tipo_movimiento IN ('Venta','Despacho','Retiro')
      AND m.factura_id IS NULL
      AND COALESCE(TRIM(m.numero_despacho),'') <> ''
      AND m.peso_bruto IS NOT NULL
)
SELECT 'legacy_clon_sin_prorratear' AS poblacion,
       l.nd || ' · granja ' || l.granja_origen_id AS clave,
       COUNT(*) AS lineas, SUM(l.aves) AS aves,
       MAX(l.peso_bruto - l.peso_tara) AS neto_global_camion,
       SUM(l.neto_efectivo) AS kg_en_liquidacion,
       SUM(l.neto_efectivo) - MAX(l.peso_bruto - l.peso_tara) AS kg_diferencia,
       array_agg(DISTINCT l.lote_ave_engorde_origen_id) AS lotes
FROM lineas l
GROUP BY l.nd, l.granja_origen_id
HAVING COUNT(*) > 1
   AND COUNT(DISTINCT l.peso_bruto) = 1 AND COUNT(DISTINCT l.peso_tara) = 1
   AND ABS(SUM(l.neto_efectivo) - MAX(l.peso_bruto - l.peso_tara)) > 0.01
ORDER BY ABS(SUM(l.neto_efectivo) - MAX(l.peso_bruto - l.peso_tara)) DESC;

-- ── 3) PESO POR LÍNEA CORRUPTO (la liquidación por lote está mal HOY) ────────
--     Línea con bruto/tara propios y peso_neto ≠ bruto−tara, que NO pertenece a
--     un grupo clon (allí el prorrateo es válido).
SELECT 'peso_propio_corrupto' AS poblacion,
       m.id AS movimiento_id,
       COALESCE('F:' || m.factura_id::text,
                COALESCE(TRIM(m.numero_despacho),'(sin)') || ' · granja ' || m.granja_origen_id) AS clave,
       m.fecha_movimiento::date AS fecha,
       m.lote_ave_engorde_origen_id AS lote_id,
       (m.cantidad_hembras + m.cantidad_machos + m.cantidad_mixtas) AS aves,
       m.peso_bruto, m.peso_tara,
       m.peso_neto AS neto_guardado,
       (m.peso_bruto - m.peso_tara) AS neto_correcto,
       (m.peso_bruto - m.peso_tara) - m.peso_neto AS kg_faltantes_en_liquidacion
FROM public.movimiento_pollo_engorde m
WHERE m.deleted_at IS NULL AND m.estado <> 'Cancelado'
  AND m.tipo_movimiento IN ('Venta','Despacho','Retiro')
  AND m.peso_bruto IS NOT NULL AND m.peso_tara IS NOT NULL AND m.peso_neto IS NOT NULL
  AND ABS(m.peso_neto - (m.peso_bruto - m.peso_tara)) > 0.01
  AND NOT EXISTS (   -- excluir líneas de grupos clon (mismo bruto y tara en una hermana)
      SELECT 1
      FROM public.movimiento_pollo_engorde h
      WHERE h.id <> m.id AND h.deleted_at IS NULL AND h.estado <> 'Cancelado'
        AND h.tipo_movimiento IN ('Venta','Despacho','Retiro')
        AND ((m.factura_id IS NOT NULL AND h.factura_id = m.factura_id)
          OR (m.factura_id IS NULL
              AND COALESCE(TRIM(m.numero_despacho),'') <> ''
              AND COALESCE(TRIM(h.numero_despacho),'') = TRIM(m.numero_despacho)
              AND h.granja_origen_id = m.granja_origen_id))
        AND h.peso_bruto IS NOT NULL AND h.peso_tara IS NOT NULL
        AND ABS(h.peso_bruto - m.peso_bruto) < 0.001
        AND ABS(h.peso_tara - m.peso_tara) < 0.001)
ORDER BY ABS((m.peso_bruto - m.peso_tara) - m.peso_neto) DESC;

-- ── 4) INFO — pesaje propio con *_global mal guardados (individual correcto) ─
SELECT 'peso_propio_globales_mal' AS poblacion,
       COALESCE('F:' || m.factura_id::text,
                TRIM(m.numero_despacho) || ' · granja ' || m.granja_origen_id) AS clave,
       COUNT(*) AS lineas,
       SUM(m.cantidad_hembras + m.cantidad_machos + m.cantidad_mixtas) AS aves,
       SUM(m.peso_bruto - m.peso_tara) AS neto_global_correcto,
       MAX(m.peso_neto_global) AS neto_global_guardado,
       array_agg(DISTINCT m.lote_ave_engorde_origen_id) AS lotes
FROM public.movimiento_pollo_engorde m
WHERE m.deleted_at IS NULL AND m.estado <> 'Cancelado'
  AND m.tipo_movimiento IN ('Venta','Despacho','Retiro')
  AND m.peso_bruto IS NOT NULL AND m.peso_tara IS NOT NULL
  AND (m.factura_id IS NOT NULL OR COALESCE(TRIM(m.numero_despacho),'') <> '')
GROUP BY 2
HAVING COUNT(*) > 1
   AND (COUNT(DISTINCT m.peso_bruto) > 1 OR COUNT(DISTINCT m.peso_tara) > 1)
   AND ABS(COALESCE(MAX(m.peso_neto_global),0) - SUM(m.peso_bruto - m.peso_tara)) > 0.01
ORDER BY SUM(m.peso_bruto - m.peso_tara) DESC;

-- ── 5) Huérfanos sospechosos (sin factura ni número) — revisión manual ──────
SELECT 'huerfanos_heuristica' AS poblacion,
       'granja ' || m.granja_origen_id || ' · ' || m.fecha_movimiento::date ||
       ' · bruto ' || m.peso_bruto || ' · placa ' || COALESCE(m.placa,'—') AS clave,
       COUNT(*) AS lineas,
       SUM(m.cantidad_hembras + m.cantidad_machos + m.cantidad_mixtas) AS aves,
       MAX(m.peso_bruto - m.peso_tara) AS neto_global_camion,
       array_agg(m.id) AS movimiento_ids,
       array_agg(DISTINCT m.lote_ave_engorde_origen_id) AS lotes
FROM public.movimiento_pollo_engorde m
WHERE m.deleted_at IS NULL AND m.estado <> 'Cancelado'
  AND m.tipo_movimiento IN ('Venta','Despacho','Retiro')
  AND m.factura_id IS NULL
  AND COALESCE(TRIM(m.numero_despacho),'') = ''
  AND m.peso_bruto IS NOT NULL
GROUP BY m.granja_origen_id, m.fecha_movimiento::date, m.peso_bruto, m.placa
HAVING COUNT(*) > 1
ORDER BY neto_global_camion DESC;

-- ── 6) Resumen por lote: kg mal contados HOY en la liquidación (poblaciones 1-3) ─
WITH corruptos AS (
    SELECT m.lote_ave_engorde_origen_id AS lote_id,
           (m.peso_bruto - m.peso_tara) - m.peso_neto AS kg_diferencia
    FROM public.movimiento_pollo_engorde m
    WHERE m.deleted_at IS NULL AND m.estado <> 'Cancelado'
      AND m.tipo_movimiento IN ('Venta','Despacho','Retiro')
      AND m.peso_bruto IS NOT NULL AND m.peso_tara IS NOT NULL AND m.peso_neto IS NOT NULL
      AND ABS(m.peso_neto - (m.peso_bruto - m.peso_tara)) > 0.01
      AND NOT EXISTS (
          SELECT 1
          FROM public.movimiento_pollo_engorde h
          WHERE h.id <> m.id AND h.deleted_at IS NULL AND h.estado <> 'Cancelado'
            AND h.tipo_movimiento IN ('Venta','Despacho','Retiro')
            AND ((m.factura_id IS NOT NULL AND h.factura_id = m.factura_id)
              OR (m.factura_id IS NULL
                  AND COALESCE(TRIM(m.numero_despacho),'') <> ''
                  AND COALESCE(TRIM(h.numero_despacho),'') = TRIM(m.numero_despacho)
                  AND h.granja_origen_id = m.granja_origen_id))
            AND h.peso_bruto IS NOT NULL AND h.peso_tara IS NOT NULL
            AND ABS(h.peso_bruto - m.peso_bruto) < 0.001
            AND ABS(h.peso_tara - m.peso_tara) < 0.001)
)
SELECT c.lote_id, lae.lote_nombre,
       COUNT(*) AS movimientos_afectados,
       ROUND(SUM(c.kg_diferencia)::numeric, 3) AS kg_corregir_en_liquidacion
FROM corruptos c
LEFT JOIN public.lote_ave_engorde lae ON lae.lote_ave_engorde_id = c.lote_id
GROUP BY c.lote_id, lae.lote_nombre
ORDER BY ABS(SUM(c.kg_diferencia)) DESC;
