-- Corrección del encaset de los lotes de engorde cuya referencia `Inicio` no empata con
-- `aves_encasetadas` y cuyo gap es EXACTAMENTE el desfase del maestro.
--
-- Contexto (18-ago-2026): el lote 132 (ItalcolEcuador · Sacachun 3b · Galpon-3 · «2604») era el
-- único de los 186 de la base con `referencia_confiable = false`. Su `Inicio` (id 180, 21-jul-2026)
-- dice 8.414 H + 10.773 M = 19.187, mientras `aves_encasetadas` decía 19.387: 200 hembras de más,
-- las mismas 200 del `desfase_h`. El lote se creó inflado y el `Inicio` guardó el número real.
--
-- Se corrige hacia el `Inicio`, no al revés: el `Inicio` es el registro del acto de encasetamiento;
-- `aves_encasetadas` es un campo editable del maestro, y su inflado ya fue la causa del lote 30.
--
-- Regla DINÁMICA: no nombra ids. Exige que el gap del encaset sea exactamente el desfase del
-- maestro, de modo que un lote descuadrado por OTRA causa no entra.
-- IDEMPOTENTE: la 2ª corrida da UPDATE 0.

WITH ini AS (
    SELECT DISTINCT ON (h.lote_ave_engorde_id) h.lote_ave_engorde_id AS id,
           COALESCE(h.aves_hembras,0) + COALESCE(h.aves_machos,0) + COALESCE(h.aves_mixtas,0) AS objetivo
    FROM historial_lote_pollo_engorde h
    WHERE h.tipo_lote = 'LoteAveEngorde'
      AND h.tipo_registro = 'Inicio'
      AND h.lote_ave_engorde_id IS NOT NULL
    ORDER BY h.lote_ave_engorde_id, h.fecha_registro, h.id
),
objetivo AS (
    SELECT c.lote_ave_engorde_id AS id, i.objetivo, c.desfase_h, c.desfase_m
    FROM fn_cuadre_aves_engorde(NULL) c
    JOIN ini i ON i.id = c.lote_ave_engorde_id
    JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = c.lote_ave_engorde_id
    WHERE NOT c.referencia_confiable
      AND c.desfase_h >= 0 AND c.desfase_m >= 0
      AND i.objetivo > 0
      AND l.aves_encasetadas - i.objetivo = c.desfase_h + c.desfase_m
)
UPDATE lote_ave_engorde l
SET aves_encasetadas = o.objetivo,
    hembras_l        = COALESCE(l.hembras_l,0) - o.desfase_h,
    machos_l         = COALESCE(l.machos_l,0)  - o.desfase_m
FROM objetivo o
WHERE l.lote_ave_engorde_id = o.id
  AND (l.aves_encasetadas IS DISTINCT FROM o.objetivo OR o.desfase_h <> 0 OR o.desfase_m <> 0);

-- Verificación esperada: 0 sin referencia confiable y 0 que no cuadran.
--   SELECT count(*) FILTER (WHERE NOT referencia_confiable), count(*) FILTER (WHERE NOT cuadra)
--   FROM fn_cuadre_aves_engorde(NULL);
