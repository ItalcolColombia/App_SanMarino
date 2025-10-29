-- Script para verificar y corregir las columnas origin y destination
-- Ejecutar este SQL si el anterior no funcionó correctamente

-- Verificar si las columnas existen
SELECT column_name, data_type, character_maximum_length, is_nullable
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'farm_inventory_movements'
  AND column_name IN ('origin', 'destination');

-- Si las columnas no existen, crearlas:
DO $$
BEGIN
    -- Agregar origin si no existe
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'origin'
    ) THEN
        ALTER TABLE public.farm_inventory_movements
        ADD COLUMN origin VARCHAR(100) NULL;
        
        COMMENT ON COLUMN public.farm_inventory_movements.origin IS 
        'Origen para entradas (ej: "Planta Sanmarino", "Planta Itacol")';
        
        RAISE NOTICE 'Columna origin agregada exitosamente';
    ELSE
        RAISE NOTICE 'Columna origin ya existe';
    END IF;

    -- Agregar destination si no existe
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'destination'
    ) THEN
        ALTER TABLE public.farm_inventory_movements
        ADD COLUMN destination VARCHAR(100) NULL;
        
        COMMENT ON COLUMN public.farm_inventory_movements.destination IS 
        'Destino para salidas (ej: "Venta", "Movimiento", "Devolución")';
        
        RAISE NOTICE 'Columna destination agregada exitosamente';
    ELSE
        RAISE NOTICE 'Columna destination ya existe';
    END IF;
END $$;

-- Verificar nuevamente después de la ejecución
SELECT column_name, data_type, character_maximum_length, is_nullable
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'farm_inventory_movements'
  AND column_name IN ('origin', 'destination');

