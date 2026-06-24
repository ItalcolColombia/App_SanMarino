-- =============================================================================
-- Backfill: guia_genetica_ecuador_header.pais_id (de 0 → país de sus lotes)
-- =============================================================================
-- La columna pais_id se agregó (migración 20260623135557) con default 0 en los
-- headers existentes, pero GuiaGeneticaEcuadorService.GetDatosAsync ahora resuelve
-- la guía por company_id + pais_id. Sin backfill, ningún lote (país 2/3) encuentra
-- su header (país 0). Se deriva el país del MODE (más frecuente) de los lotes que
-- cada header empareja por company + raza + año. Idempotente: solo toca pais_id = 0.
-- =============================================================================

UPDATE public.guia_genetica_ecuador_header gh
SET pais_id = sub.pais_id
FROM (
    SELECT h.id,
        MODE() WITHIN GROUP (ORDER BY l.pais_id) AS pais_id
    FROM public.guia_genetica_ecuador_header h
    JOIN public.lote_ave_engorde l
      ON l.company_id = h.company_id
     AND TRIM(LOWER(l.raza)) = TRIM(LOWER(h.raza))
     AND l.ano_tabla_genetica = h.anio_guia
     AND l.deleted_at IS NULL
     AND l.pais_id IS NOT NULL
    WHERE h.deleted_at IS NULL
      AND COALESCE(h.pais_id, 0) = 0
    GROUP BY h.id
) sub
WHERE gh.id = sub.id
  AND sub.pais_id IS NOT NULL
  AND sub.pais_id <> 0;
