-- ============================================================
-- Script para verificar y asegurar la estructura correcta de lote_reproductoras
-- ============================================================
-- Este script verifica que la tabla tenga todos los campos necesarios
-- y que el lote_id se guarde correctamente como string
-- ============================================================

-- 1. Verificar estructura actual de la tabla
SELECT 
    column_name,
    data_type,
    character_maximum_length,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_schema = 'public'
AND table_name = 'lote_reproductoras'
ORDER BY ordinal_position;

-- 2. Verificar constraints existentes
SELECT 
    tc.constraint_name,
    tc.constraint_type,
    kcu.column_name
FROM information_schema.table_constraints tc
JOIN information_schema.key_column_usage kcu
    ON tc.constraint_name = kcu.constraint_name
    AND tc.table_schema = kcu.table_schema
WHERE tc.table_schema = 'public'
AND tc.table_name = 'lote_reproductoras'
ORDER BY tc.constraint_type, tc.constraint_name;

-- 3. Verificar índices existentes
SELECT 
    indexname,
    indexdef
FROM pg_indexes
WHERE schemaname = 'public'
AND tablename = 'lote_reproductoras'
ORDER BY indexname;

-- 4. Verificar que lote_id sea character varying(64)
DO $$
DECLARE
    current_type TEXT;
BEGIN
    SELECT data_type INTO current_type
    FROM information_schema.columns
    WHERE table_schema = 'public'
    AND table_name = 'lote_reproductoras'
    AND column_name = 'lote_id';
    
    IF current_type = 'character varying' THEN
        RAISE NOTICE 'lote_id es character varying (correcto)';
    ELSE
        RAISE WARNING 'lote_id es % (esperado: character varying)', current_type;
    END IF;
END $$;

-- 5. Verificar que todos los lote_id sean numéricos válidos
SELECT 
    COUNT(*) as total_registros,
    COUNT(CASE WHEN lote_id ~ '^[0-9]+$' THEN 1 END) as lote_id_numericos,
    COUNT(CASE WHEN lote_id !~ '^[0-9]+$' OR lote_id IS NULL THEN 1 END) as lote_id_no_numericos
FROM public.lote_reproductoras;

-- 6. Mostrar registros con lote_id no numérico (si existen)
SELECT 
    lote_id,
    reproductora_id,
    nombre_lote,
    COUNT(*) as cantidad
FROM public.lote_reproductoras
WHERE lote_id !~ '^[0-9]+$' OR lote_id IS NULL
GROUP BY lote_id, reproductora_id, nombre_lote
ORDER BY cantidad DESC
LIMIT 10;

-- 7. Verificar integridad referencial (lote_id debe existir en lotes)
SELECT 
    COUNT(*) as registros_sin_lote
FROM public.lote_reproductoras lr
WHERE lr.lote_id ~ '^[0-9]+$'
AND NOT EXISTS (
    SELECT 1 
    FROM public.lotes l 
    WHERE l.lote_id::text = lr.lote_id
);

-- 8. Resumen de datos
SELECT 
    COUNT(*) as total_registros,
    COUNT(DISTINCT lote_id) as lotes_unicos,
    COUNT(DISTINCT reproductora_id) as reproductoras_unicas,
    MIN(fecha_encasetamiento) as fecha_minima,
    MAX(fecha_encasetamiento) as fecha_maxima
FROM public.lote_reproductoras;
