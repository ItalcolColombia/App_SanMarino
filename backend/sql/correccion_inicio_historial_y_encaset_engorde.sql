-- =====================================================================================
-- Corrección de la REFERENCIA de conservación de pollo engorde (copia trazable de la
-- migración 20260805170000_CorreccionInicioHistorialYEncasetEngorde).
--
-- Ataca los lotes cuyo historial `Inicio` NO coincide con `aves_encasetadas`, que por eso
-- salen de fn_cuadre_aves_engorde con `referencia_confiable = false` y no se pueden auditar.
-- Hermana de correccion_maestro_aves_engorde_identidad.sql, que solo actúa sobre lotes cuya
-- referencia ya era confiable.
--
-- Son dos causas opuestas, cada una con su evidencia:
--
--   BLOQUE 1 — el Inicio es plantilla de la carga inicial (25.000 H / 25.000 M / 35-36 mix,
--   todos escritos el 2026-03-23). Donde `aves_encasetadas` sí se corrigió después al valor
--   real, se reescribe el Inicio con el reparto que exige la conservación de lo ya registrado.
--   La capacidad del galpón lo respalda: el G0050 manejó 24.384 / 22.535 / 24.000 aves en sus
--   otros ciclos y el G0051, 24.617 / 22.000 / 24.000 — 50.000 es el doble de lo físicamente
--   posible.
--
--   BLOQUE 2 — el Inicio es el correcto y `aves_encasetadas` está inflado. La prueba es que
--   bajo el Inicio AMBOS sexos cierran en 0 exacto, mientras que bajo `aves_encasetadas`
--   sobran 700 hembras y 700 machos: el mismo excedente partido en dos, firma de un encaset
--   digitado de más.
--
-- Ninguna regla nombra ids: todas se apoyan en evidencia registrada. Idempotente
-- (`IS DISTINCT FROM`). Simular siempre con BEGIN ... ROLLBACK antes de aplicar.
--
-- ─────────────────────────────────────────────────────────────────────────────────────
-- VALORES PREVIOS (dump tipo-prod 05-ago-2026) — para una reversión manual si hiciera falta:
--
--   lote 5  (Sacachun 3b · G0050 · 2602) historial Inicio: 25.000 H / 25.000 M / 35 mix
--   lote 7  (Sacachun 2  · G0051 · 2602) historial Inicio: 25.000 H / 25.000 M / 36 mix
--   lote 30 (SAN GUILLERMO · G0030 · 2601): aves_encasetadas 12.700 · hembras_l 1.744 ·
--           machos_l 2.140
--
-- QUEDAN FUERA A PROPÓSITO:
--   · id 132 (Sacachun 3b · G0049 · 2604): activo y sin ventas ⇒ la conservación no puede
--     discriminar entre 19.387 y 19.187. Necesita el documento físico de encasetamiento.
--   · ids 3, 4, 6, 8: encaset 50.000 Y el Inicio de plantilla — los DOS números son ficticios
--     y no hay actividad de la cual deducir el real (cero movimientos). El detector no los ve
--     porque su `referencia_confiable` compara ih + im sin las mixtas. Decisión de negocio.
-- =====================================================================================

\echo '=== ANTES ==='
SELECT l.lote_ave_engorde_id AS lote, f.name AS granja, l.galpon_id,
       l.aves_encasetadas AS encaset, l.hembras_l, l.machos_l,
       h.aves_hembras AS ini_h, h.aves_machos AS ini_m, h.aves_mixtas AS ini_x,
       (h.aves_hembras + h.aves_machos + h.aves_mixtas) = l.aves_encasetadas AS referencia_ok
FROM lote_ave_engorde l
JOIN farms f ON f.id = l.granja_id
JOIN LATERAL (SELECT * FROM historial_lote_pollo_engorde x
              WHERE x.lote_ave_engorde_id = l.lote_ave_engorde_id
                AND x.tipo_lote = 'LoteAveEngorde' AND x.tipo_registro = 'Inicio'
              ORDER BY x.fecha_registro, x.id LIMIT 1) h ON TRUE
WHERE l.deleted_at IS NULL AND COALESCE(l.aves_encasetadas, 0) > 0
  AND (h.aves_hembras + h.aves_machos + h.aves_mixtas) <> l.aves_encasetadas
ORDER BY 1;

-- ── BLOQUE 1 ─────────────────────────────────────────────────────────────────────────
WITH ini AS (
    SELECT DISTINCT ON (lote_ave_engorde_id) lote_ave_engorde_id AS id, id AS fila_id,
           COALESCE(aves_hembras, 0) AS ih, COALESCE(aves_machos, 0) AS im, COALESCE(aves_mixtas, 0) AS ix
    FROM historial_lote_pollo_engorde
    WHERE tipo_lote = 'LoteAveEngorde' AND tipo_registro = 'Inicio' AND lote_ave_engorde_id IS NOT NULL
    ORDER BY lote_ave_engorde_id, fecha_registro, id
), aj AS (
    SELECT lote_ave_engorde_id AS id,
           SUM(COALESCE(aves_hembras, 0)) AS ah, SUM(COALESCE(aves_machos, 0)) AS am
    FROM historial_lote_pollo_engorde
    WHERE tipo_lote = 'LoteAveEngorde' AND tipo_registro = 'Ajuste' AND lote_ave_engorde_id IS NOT NULL
    GROUP BY lote_ave_engorde_id
), v AS (
    SELECT lote_ave_engorde_origen_id AS id,
           SUM(cantidad_hembras) AS vh, SUM(cantidad_machos) AS vm
    FROM movimiento_pollo_engorde
    WHERE estado = 'Completado' AND deleted_at IS NULL AND lote_ave_engorde_origen_id IS NOT NULL
    GROUP BY lote_ave_engorde_origen_id
), ap AS (
    SELECT lote_ave_engorde_id AS id,
           SUM(COALESCE(cantidad_hembras, 0)) AS ph, SUM(COALESCE(cantidad_machos, 0)) AS pm
    FROM lote_registro_historico_unificado
    WHERE tipo_evento = 'BAJA_SEGUIMIENTO' AND NOT anulado AND lote_ave_engorde_id IS NOT NULL
    GROUP BY lote_ave_engorde_id
), objetivo AS (
    SELECT i.fila_id,
           COALESCE(l.hembras_l, 0) + COALESCE(v.vh, 0) + COALESCE(ap.ph, 0) + COALESCE(aj.ah, 0) AS nh,
           COALESCE(l.machos_l, 0)  + COALESCE(v.vm, 0) + COALESCE(ap.pm, 0) + COALESCE(aj.am, 0) AS nm
    FROM lote_ave_engorde l
    JOIN ini i ON i.id = l.lote_ave_engorde_id
    LEFT JOIN aj ON aj.id = l.lote_ave_engorde_id
    LEFT JOIN v  ON v.id  = l.lote_ave_engorde_id
    LEFT JOIN ap ON ap.id = l.lote_ave_engorde_id
    WHERE l.deleted_at IS NULL
      AND COALESCE(l.aves_encasetadas, 0) > 0
      AND i.ih = 25000 AND i.im = 25000
      AND i.ih + i.im + i.ix <> l.aves_encasetadas
      AND COALESCE(v.vh, 0) + COALESCE(v.vm, 0) > 0
      AND (COALESCE(l.hembras_l, 0) + COALESCE(v.vh, 0) + COALESCE(ap.ph, 0) + COALESCE(aj.ah, 0))
        + (COALESCE(l.machos_l, 0) + COALESCE(v.vm, 0) + COALESCE(ap.pm, 0) + COALESCE(aj.am, 0))
          = l.aves_encasetadas
)
UPDATE historial_lote_pollo_engorde h
SET aves_hembras = o.nh, aves_machos = o.nm, aves_mixtas = 0
FROM objetivo o
WHERE h.id = o.fila_id
  AND o.nh >= 0 AND o.nm >= 0
  AND (h.aves_hembras IS DISTINCT FROM o.nh
    OR h.aves_machos  IS DISTINCT FROM o.nm
    OR h.aves_mixtas  IS DISTINCT FROM 0);

-- ── BLOQUE 2 ─────────────────────────────────────────────────────────────────────────
WITH ini AS (
    SELECT DISTINCT ON (lote_ave_engorde_id) lote_ave_engorde_id AS id,
           COALESCE(aves_hembras, 0) AS ih, COALESCE(aves_machos, 0) AS im, COALESCE(aves_mixtas, 0) AS ix
    FROM historial_lote_pollo_engorde
    WHERE tipo_lote = 'LoteAveEngorde' AND tipo_registro = 'Inicio' AND lote_ave_engorde_id IS NOT NULL
    ORDER BY lote_ave_engorde_id, fecha_registro, id
), v AS (
    SELECT lote_ave_engorde_origen_id AS id,
           SUM(cantidad_hembras) AS vh, SUM(cantidad_machos) AS vm
    FROM movimiento_pollo_engorde
    WHERE estado = 'Completado' AND deleted_at IS NULL AND lote_ave_engorde_origen_id IS NOT NULL
    GROUP BY lote_ave_engorde_origen_id
), sg AS (
    SELECT lote_ave_engorde_id AS id,
           SUM(COALESCE(mortalidad_hembras, 0) + COALESCE(sel_h, 0) + COALESCE(error_sexaje_hembras, 0)) AS sh,
           SUM(COALESCE(mortalidad_machos, 0)  + COALESCE(sel_m, 0) + COALESCE(error_sexaje_machos, 0))  AS sm
    FROM seguimiento_diario_aves_engorde
    GROUP BY lote_ave_engorde_id
), ap AS (
    SELECT lote_ave_engorde_id AS id,
           SUM(COALESCE(cantidad_hembras, 0)) AS ph, SUM(COALESCE(cantidad_machos, 0)) AS pm
    FROM lote_registro_historico_unificado
    WHERE tipo_evento = 'BAJA_SEGUIMIENTO' AND NOT anulado AND lote_ave_engorde_id IS NOT NULL
    GROUP BY lote_ave_engorde_id
), objetivo AS (
    SELECT l.lote_ave_engorde_id AS id,
           i.ih + i.im + i.ix AS nuevo_encaset,
           i.ih - COALESCE(v.vh, 0) - COALESCE(ap.ph, 0) AS nh,
           i.im - COALESCE(v.vm, 0) - COALESCE(ap.pm, 0) AS nm
    FROM lote_ave_engorde l
    JOIN ini i ON i.id = l.lote_ave_engorde_id
    LEFT JOIN v  ON v.id  = l.lote_ave_engorde_id
    LEFT JOIN sg ON sg.id = l.lote_ave_engorde_id
    LEFT JOIN ap ON ap.id = l.lote_ave_engorde_id
    WHERE l.deleted_at IS NULL
      AND COALESCE(l.aves_encasetadas, 0) > 0
      AND i.ix = 0
      AND i.ih + i.im + i.ix <> l.aves_encasetadas
      AND i.ih - COALESCE(sg.sh, 0) - COALESCE(v.vh, 0) = 0
      AND i.im - COALESCE(sg.sm, 0) - COALESCE(v.vm, 0) = 0
)
UPDATE lote_ave_engorde l
SET aves_encasetadas = o.nuevo_encaset, hembras_l = o.nh, machos_l = o.nm
FROM objetivo o
WHERE l.lote_ave_engorde_id = o.id
  AND o.nuevo_encaset > 0 AND o.nh >= 0 AND o.nm >= 0
  AND (l.aves_encasetadas IS DISTINCT FROM o.nuevo_encaset
    OR l.hembras_l        IS DISTINCT FROM o.nh
    OR l.machos_l         IS DISTINCT FROM o.nm);

\echo ''
\echo '=== DESPUES: referencias NO confiables que quedan (esperado: solo el 132) ==='
SELECT lote_ave_engorde_id, granja, galpon, maestro_h, maestro_m, esperado_h, esperado_m
FROM fn_cuadre_aves_engorde(NULL) WHERE NOT referencia_confiable ORDER BY 1;

\echo ''
\echo '=== DESPUES: descuadrados con referencia confiable (esperado: 0 filas) ==='
SELECT lote_ave_engorde_id, granja, galpon, desfase_h, desfase_m
FROM fn_cuadre_aves_engorde(NULL) WHERE NOT cuadra AND referencia_confiable ORDER BY 1;
