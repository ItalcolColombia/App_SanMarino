-- ============================================================
-- VERIFICAR Y ARREGLAR produccion_lotes COMPLETAMENTE
-- ============================================================

-- 1. VER estructura actual
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_name = 'produccion_lotes' 
ORDER BY ordinal_position;

-- 2. AGREGAR columnas que faltan (ignora si ya existen)
ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS observaciones VARCHAR(1000);

ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS nucleo_p VARCHAR(100);

-- 3. VERIFICAR que se agregaron
SELECT 
    column_name, 
    data_type, 
    is_nullable
FROM information_schema.columns 
WHERE table_name = 'produccion_lotes' 
AND column_name IN ('observaciones', 'nucleo_p');

-- 4. VER estructura completa final
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_name = 'produccion_lotes' 
ORDER BY ordinal_position;

