-- ============================================================
-- Script para verificar y agregar foreign key a lote_reproductoras
-- ============================================================
-- Este script verifica y agrega la foreign key entre lote_reproductoras.lote_id
-- (character varying) y lotes.lote_id (integer) usando una conversión
-- ============================================================

-- 1. Verificar si la foreign key ya existe
DO $$
DECLARE
    fk_exists BOOLEAN;
BEGIN
    SELECT EXISTS (
        SELECT 1 
        FROM information_schema.table_constraints 
        WHERE constraint_name = 'fk_lote_reproductoras_lotes_lote_id'
        AND table_name = 'lote_reproductoras'
        AND table_schema = 'public'
    ) INTO fk_exists;

    IF NOT fk_exists THEN
        -- Agregar foreign key con conversión de tipos
        -- Nota: PostgreSQL no permite FK directa entre integer y varchar
        -- Por lo tanto, creamos un índice funcional y validamos manualmente
        RAISE NOTICE 'La foreign key no existe. Se recomienda validar manualmente la integridad referencial.';
        
        -- Crear índice funcional para mejorar el rendimiento de las consultas
        CREATE INDEX IF NOT EXISTS ix_lote_reproductoras_lote_id_int 
        ON public.lote_reproductoras((lote_id::integer))
        WHERE lote_id ~ '^[0-9]+$';
        
        RAISE NOTICE 'Índice funcional creado para lote_id convertido a integer.';
    ELSE
        RAISE NOTICE 'La foreign key ya existe.';
    END IF;
END $$;

-- 2. Verificar que todos los lote_id en lote_reproductoras sean numéricos válidos
-- y que existan en la tabla lotes
DO $$
DECLARE
    invalid_count INTEGER;
    missing_count INTEGER;
BEGIN
    -- Contar registros con lote_id no numérico
    SELECT COUNT(*) INTO invalid_count
    FROM public.lote_reproductoras
    WHERE lote_id !~ '^[0-9]+$' OR lote_id IS NULL;
    
    IF invalid_count > 0 THEN
        RAISE WARNING 'Se encontraron % registros con lote_id no numérico o NULL', invalid_count;
    END IF;
    
    -- Contar registros cuyo lote_id no existe en lotes
    SELECT COUNT(*) INTO missing_count
    FROM public.lote_reproductoras lr
    WHERE lr.lote_id ~ '^[0-9]+$'
    AND NOT EXISTS (
        SELECT 1 
        FROM public.lotes l 
        WHERE l.lote_id::text = lr.lote_id
    );
    
    IF missing_count > 0 THEN
        RAISE WARNING 'Se encontraron % registros con lote_id que no existe en la tabla lotes', missing_count;
    ELSE
        RAISE NOTICE 'Todos los lote_id numéricos existen en la tabla lotes.';
    END IF;
END $$;

-- 3. Verificar estructura de la tabla
SELECT 
    column_name,
    data_type,
    character_maximum_length,
    is_nullable
FROM information_schema.columns
WHERE table_schema = 'public'
AND table_name = 'lote_reproductoras'
ORDER BY ordinal_position;

-- 4. Mostrar resumen de registros
SELECT 
    COUNT(*) as total_registros,
    COUNT(DISTINCT lote_id) as lotes_unicos,
    COUNT(DISTINCT reproductora_id) as reproductoras_unicas
FROM public.lote_reproductoras;
