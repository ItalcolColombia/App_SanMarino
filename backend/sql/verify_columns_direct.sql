-- Script para verificar directamente si las columnas existen y en qué schema
-- Ejecutar este SQL directamente en la base de datos

-- 1. Verificar columnas con información completa
SELECT 
    table_schema,
    table_name,
    column_name,
    data_type,
    character_maximum_length,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_name = 'farm_inventory_movements'
  AND column_name IN ('origin', 'destination')
ORDER BY table_schema, table_name, column_name;

-- 2. Si no aparecen resultados, verificar todas las columnas de la tabla
SELECT 
    column_name,
    data_type,
    character_maximum_length,
    is_nullable
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'farm_inventory_movements'
ORDER BY ordinal_position;

-- 3. Verificar que la tabla existe
SELECT 
    table_schema,
    table_name,
    table_type
FROM information_schema.tables
WHERE table_name = 'farm_inventory_movements';

-- 4. Si las columnas no existen, crearlas de nuevo de forma explícita
DO $$
BEGIN
    -- Verificar y crear origin
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'public' 
          AND table_name = 'farm_inventory_movements' 
          AND column_name = 'origin'
    ) THEN
        ALTER TABLE public.farm_inventory_movements
        ADD COLUMN origin VARCHAR(100) NULL;
        RAISE NOTICE 'Columna origin creada';
    ELSE
        RAISE NOTICE 'Columna origin ya existe';
    END IF;

    -- Verificar y crear destination
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'public' 
          AND table_name = 'farm_inventory_movements' 
          AND column_name = 'destination'
    ) THEN
        ALTER TABLE public.farm_inventory_movements
        ADD COLUMN destination VARCHAR(100) NULL;
        RAISE NOTICE 'Columna destination creada';
    ELSE
        RAISE NOTICE 'Columna destination ya existe';
    END IF;
END $$;

-- 5. Verificar nuevamente después de la ejecución
SELECT 
    column_name,
    data_type,
    character_maximum_length,
    is_nullable
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'farm_inventory_movements'
  AND column_name IN ('origin', 'destination');

