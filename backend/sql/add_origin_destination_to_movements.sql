-- Agregar campos origin y destination a la tabla farm_inventory_movements
-- Ejecutar este SQL manualmente en la base de datos

ALTER TABLE public.farm_inventory_movements
ADD COLUMN IF NOT EXISTS origin VARCHAR(100) NULL,
ADD COLUMN IF NOT EXISTS destination VARCHAR(100) NULL;

-- Comentarios para documentación
COMMENT ON COLUMN public.farm_inventory_movements.origin IS 'Origen para entradas (ej: "Planta Sanmarino", "Planta Itacol")';
COMMENT ON COLUMN public.farm_inventory_movements.destination IS 'Destino para salidas (ej: "Venta", "Movimiento", "Devolución")';

-- Index opcionales si se necesitan búsquedas por estos campos
-- CREATE INDEX IF NOT EXISTS ix_fim_origin ON public.farm_inventory_movements(origin);
-- CREATE INDEX IF NOT EXISTS ix_fim_destination ON public.farm_inventory_movements(destination);




