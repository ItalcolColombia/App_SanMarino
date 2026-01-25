-- Agrega el campo SelM (retiro/selección machos) en el seguimiento diario de producción.
-- Tabla mapeada por EF: produccion_diaria

ALTER TABLE IF EXISTS public.produccion_diaria
ADD COLUMN IF NOT EXISTS sel_m integer NOT NULL DEFAULT 0;

