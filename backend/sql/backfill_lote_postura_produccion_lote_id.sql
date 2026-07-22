-- backfill_lote_postura_produccion_lote_id.sql
-- Contexto: los lotes de producción (lote_postura_produccion) se crean SOLO al cerrar un lote de
-- levante (LotePosturaLevanteService.CerrarLoteYCrearProduccionAsync -> CrearLoteProduccion).
-- Ese método omitía copiar LoteId/LotePadreId del levante, dejando lote_id = NULL. Al guardar un
-- seguimiento de producción, ProduccionService exige lote_id > 0 y devolvía 400
-- ("El lote postura producción no tiene LoteId asociado ...").
--
-- El código ya fue corregido (hereda LoteId/LotePadreId hacia adelante). Este script repara las
-- filas ya existentes copiando el lote base desde el levante asociado.
--
-- IDEMPOTENTE: solo toca filas con lote_id NULL/<=0. Re-ejecutar no vuelve a afectar filas ya sanas.
-- Seguro: no modifica levantes ni seguimientos; solo completa la referencia al Lote base.

BEGIN;

-- Diagnóstico previo (informativo): cuántas filas de producción están sin lote_id y son reparables.
DO $$
DECLARE
    v_sin_lote   integer;
    v_reparables integer;
BEGIN
    SELECT count(*) INTO v_sin_lote
    FROM public.lote_postura_produccion p
    WHERE (p.lote_id IS NULL OR p.lote_id <= 0);

    SELECT count(*) INTO v_reparables
    FROM public.lote_postura_produccion p
    JOIN public.lote_postura_levante lev
      ON lev.lote_postura_levante_id = p.lote_postura_levante_id
    WHERE (p.lote_id IS NULL OR p.lote_id <= 0)
      AND lev.lote_id IS NOT NULL;

    RAISE NOTICE 'lote_postura_produccion sin lote_id: %, reparables desde levante: %',
        v_sin_lote, v_reparables;
END $$;

-- Backfill: hereda el Lote base (y el lote padre) desde el levante origen.
UPDATE public.lote_postura_produccion p
SET lote_id       = lev.lote_id,
    lote_padre_id = COALESCE(p.lote_padre_id, lev.lote_padre_id)
FROM public.lote_postura_levante lev
WHERE p.lote_postura_levante_id = lev.lote_postura_levante_id
  AND (p.lote_id IS NULL OR p.lote_id <= 0)
  AND lev.lote_id IS NOT NULL;

COMMIT;

-- Verificación (debería devolver 0 filas irreparables tras el backfill):
--   SELECT p.lote_postura_produccion_id, p.lote_postura_levante_id
--   FROM public.lote_postura_produccion p
--   LEFT JOIN public.lote_postura_levante lev
--     ON lev.lote_postura_levante_id = p.lote_postura_levante_id
--   WHERE (p.lote_id IS NULL OR p.lote_id <= 0);
