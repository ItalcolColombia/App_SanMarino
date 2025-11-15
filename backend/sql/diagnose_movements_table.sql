-- Script de diagnóstico para la tabla farm_inventory_movements
-- Ejecutar esto para verificar todo relacionado con la tabla

-- 1. Verificar que la tabla existe y sus columnas
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
ORDER BY ordinal_position;

-- 2. Verificar específicamente origin y destination
SELECT 
    'origin' as campo,
    CASE WHEN EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
          AND table_name = 'farm_inventory_movements' 
          AND column_name = 'origin'
    ) THEN 'EXISTE' ELSE 'NO EXISTE' END as estado
UNION ALL
SELECT 
    'destination' as campo,
    CASE WHEN EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
          AND table_name = 'farm_inventory_movements' 
          AND column_name = 'destination'
    ) THEN 'EXISTE' ELSE 'NO EXISTE' END as estado;

-- 3. Intentar crear las columnas si no existen (con verificación)
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
        RAISE NOTICE '✅ Columna origin CREADA';
    ELSE
        RAISE NOTICE '✅ Columna origin ya existe';
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
        RAISE NOTICE '✅ Columna destination CREADA';
    ELSE
        RAISE NOTICE '✅ Columna destination ya existe';
    END IF;
END $$;

-- 4. Verificar con nombres exactos (case-sensitive)
SELECT 
    column_name,
    CASE 
        WHEN column_name = 'origin' THEN '✅ Coincide con minúsculas'
        WHEN column_name = 'Origin' THEN '⚠️ Mayúscula inicial'
        WHEN LOWER(column_name) = 'origin' THEN '⚠️ Diferente case'
        ELSE '❌ No coincide'
    END as verificacion
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'farm_inventory_movements'
  AND LOWER(column_name) IN ('origin', 'destination');

-- 5. Listar TODAS las columnas de la tabla (para debug)
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'farm_inventory_movements'
ORDER BY ordinal_position;




