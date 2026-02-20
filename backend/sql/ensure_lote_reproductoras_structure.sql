-- ============================================================
-- Script para asegurar que la tabla lote_reproductoras tenga
-- la estructura correcta según el esquema proporcionado
-- ============================================================
-- Este script verifica y agrega campos si faltan, pero NO modifica
-- la estructura existente si ya está correcta
-- ============================================================

-- 1. Verificar que lote_id sea character varying(64)
DO $$
DECLARE
    current_type TEXT;
    current_length INTEGER;
BEGIN
    SELECT data_type, character_maximum_length 
    INTO current_type, current_length
    FROM information_schema.columns
    WHERE table_schema = 'public'
    AND table_name = 'lote_reproductoras'
    AND column_name = 'lote_id';
    
    IF current_type IS NULL THEN
        RAISE EXCEPTION 'La columna lote_id no existe en la tabla lote_reproductoras';
    ELSIF current_type != 'character varying' OR current_length != 64 THEN
        RAISE NOTICE 'lote_id es % (%), se espera character varying(64)', current_type, current_length;
        RAISE NOTICE 'NOTA: Si necesitas cambiar el tipo, ejecuta manualmente:';
        RAISE NOTICE 'ALTER TABLE public.lote_reproductoras ALTER COLUMN lote_id TYPE character varying(64);';
    ELSE
        RAISE NOTICE 'lote_id es character varying(64) (correcto)';
    END IF;
END $$;

-- 2. Verificar que todos los campos requeridos existan
DO $$
DECLARE
    missing_columns TEXT[] := ARRAY[]::TEXT[];
    col_record RECORD;
    required_columns TEXT[] := ARRAY[
        'lote_id', 'reproductora_id', 'nombre_lote', 'fecha_encasetamiento',
        'm', 'h', 'mixtas', 'mort_caja_h', 'mort_caja_m',
        'unif_h', 'unif_m', 'peso_inicial_m', 'peso_inicial_h', 'peso_mixto'
    ];
BEGIN
    FOR col_record IN 
        SELECT column_name 
        FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'lote_reproductoras'
    LOOP
        -- Remover de required_columns si existe
        required_columns := array_remove(required_columns, col_record.column_name);
    END LOOP;
    
    IF array_length(required_columns, 1) > 0 THEN
        RAISE WARNING 'Faltan las siguientes columnas: %', array_to_string(required_columns, ', ');
    ELSE
        RAISE NOTICE 'Todas las columnas requeridas existen';
    END IF;
END $$;

-- 3. Verificar constraints
SELECT 
    tc.constraint_name,
    tc.constraint_type,
    string_agg(kcu.column_name, ', ' ORDER BY kcu.ordinal_position) as columns
FROM information_schema.table_constraints tc
JOIN information_schema.key_column_usage kcu
    ON tc.constraint_name = kcu.constraint_name
    AND tc.table_schema = kcu.table_schema
WHERE tc.table_schema = 'public'
AND tc.table_name = 'lote_reproductoras'
GROUP BY tc.constraint_name, tc.constraint_type
ORDER BY tc.constraint_type, tc.constraint_name;

-- 4. Verificar índices
SELECT 
    indexname,
    indexdef
FROM pg_indexes
WHERE schemaname = 'public'
AND tablename = 'lote_reproductoras'
ORDER BY indexname;

-- 5. Resumen de la estructura actual
SELECT 
    column_name,
    data_type,
    character_maximum_length,
    numeric_precision,
    numeric_scale,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_schema = 'public'
AND table_name = 'lote_reproductoras'
ORDER BY ordinal_position;

-- 6. Verificar integridad de datos (lote_id debe ser numérico y existir en lotes)
SELECT 
    COUNT(*) as total_registros,
    COUNT(CASE WHEN lote_id ~ '^[0-9]+$' THEN 1 END) as lote_id_numericos,
    COUNT(CASE WHEN lote_id !~ '^[0-9]+$' OR lote_id IS NULL THEN 1 END) as lote_id_no_numericos,
    COUNT(CASE WHEN lote_id ~ '^[0-9]+$' AND EXISTS (
        SELECT 1 FROM public.lotes l WHERE l.lote_id::text = lote_reproductoras.lote_id
    ) THEN 1 END) as registros_con_lote_valido,
    COUNT(CASE WHEN lote_id ~ '^[0-9]+$' AND NOT EXISTS (
        SELECT 1 FROM public.lotes l WHERE l.lote_id::text = lote_reproductoras.lote_id
    ) THEN 1 END) as registros_sin_lote_valido
FROM public.lote_reproductoras;

-- NOTA: La foreign key no se puede crear directamente porque:
-- - lotes.lote_id es INTEGER
-- - lote_reproductoras.lote_id es CHARACTER VARYING(64)
-- La integridad referencial se valida manualmente en el código de la aplicación
