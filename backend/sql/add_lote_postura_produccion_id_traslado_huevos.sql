-- Añadir lote_postura_produccion_id a traslado_huevos para flujo LPP
-- Cuando está presente, el traslado usa disponibilidad y descuento en espejo_huevo_produccion

ALTER TABLE public.traslado_huevos
    ADD COLUMN IF NOT EXISTS lote_postura_produccion_id INTEGER NULL;

ALTER TABLE public.traslado_huevos
    ADD CONSTRAINT fk_th_lote_postura_produccion
    FOREIGN KEY (lote_postura_produccion_id)
    REFERENCES public.lote_postura_produccion(lote_postura_produccion_id) ON DELETE RESTRICT;

CREATE INDEX IF NOT EXISTS ix_traslado_huevos_lote_postura_produccion_id
    ON public.traslado_huevos(lote_postura_produccion_id);

COMMENT ON COLUMN public.traslado_huevos.lote_postura_produccion_id IS
    'Lote postura producción (LPP). Cuando está presente, se usa espejo_huevo_produccion para disponibilidad y descuentos.';
