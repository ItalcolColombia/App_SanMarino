-- Script completo para convertir lote_id de text a integer con manejo automático de todas las dependencias
-- Ejecutar en la base de datos AWS RDS

-- 1. PASO 1: Identificar TODAS las dependencias automáticamente
DO $$
DECLARE
    fk_record RECORD;
    table_record RECORD;
    fk_constraints TEXT[] := ARRAY[]::TEXT[];
    dependent_tables TEXT[] := ARRAY[]::TEXT[];
BEGIN
    RAISE NOTICE '=== IDENTIFICANDO DEPENDENCIAS ===';
    
    -- Buscar todas las FK que referencian lotes.lote_id
    FOR fk_record IN 
        SELECT 
            tc.table_name,
            tc.constraint_name,
            kcu.column_name,
            ccu.table_name AS foreign_table_name,
            ccu.column_name AS foreign_column_name
        FROM information_schema.table_constraints AS tc 
        JOIN information_schema.key_column_usage AS kcu
            ON tc.constraint_name = kcu.constraint_name
            AND tc.table_schema = kcu.table_schema
        JOIN information_schema.constraint_column_usage AS ccu
            ON ccu.constraint_name = tc.constraint_name
            AND ccu.table_schema = tc.table_schema
        WHERE tc.constraint_type = 'FOREIGN KEY' 
        AND ccu.table_name = 'lotes'
        AND ccu.column_name = 'lote_id'
    LOOP
        fk_constraints := array_append(fk_constraints, fk_record.constraint_name);
        dependent_tables := array_append(dependent_tables, fk_record.table_name);
        RAISE NOTICE 'FK encontrada: % en tabla %', fk_record.constraint_name, fk_record.table_name;
    END LOOP;
    
    RAISE NOTICE 'Total de FK encontradas: %', array_length(fk_constraints, 1);
    RAISE NOTICE 'Tablas dependientes: %', array_to_string(dependent_tables, ', ');
END $$;

-- 2. PASO 2: Eliminar TODAS las claves foráneas automáticamente
DO $$
DECLARE
    fk_record RECORD;
BEGIN
    RAISE NOTICE '=== ELIMINANDO CLAVES FORÁNEAS ===';
    
    FOR fk_record IN 
        SELECT 
            tc.table_name,
            tc.constraint_name
        FROM information_schema.table_constraints AS tc 
        JOIN information_schema.key_column_usage AS kcu
            ON tc.constraint_name = kcu.constraint_name
            AND tc.table_schema = kcu.table_schema
        JOIN information_schema.constraint_column_usage AS ccu
            ON ccu.constraint_name = tc.constraint_name
            AND ccu.table_schema = tc.table_schema
        WHERE tc.constraint_type = 'FOREIGN KEY' 
        AND ccu.table_name = 'lotes'
        AND ccu.column_name = 'lote_id'
    LOOP
        EXECUTE format('ALTER TABLE public.%I DROP CONSTRAINT IF EXISTS %I', 
                      fk_record.table_name, fk_record.constraint_name);
        RAISE NOTICE 'FK eliminada: % de tabla %', fk_record.constraint_name, fk_record.table_name;
    END LOOP;
END $$;

-- 3. PASO 3: Convertir la columna principal lotes.lote_id
DO $$
DECLARE
    max_id INTEGER;
BEGIN
    RAISE NOTICE '=== CONVIRTIENDO COLUMNA PRINCIPAL ===';
    
    -- Crear columna temporal
    ALTER TABLE public.lotes ADD COLUMN lote_id_new INTEGER;
    
    -- Copiar datos válidos (solo números)
    UPDATE public.lotes 
    SET lote_id_new = lote_id::INTEGER 
    WHERE lote_id ~ '^[0-9]+$' AND lote_id IS NOT NULL;
    
    -- Eliminar la columna original
    ALTER TABLE public.lotes DROP COLUMN lote_id;
    
    -- Renombrar la nueva columna
    ALTER TABLE public.lotes RENAME COLUMN lote_id_new TO lote_id;
    
    RAISE NOTICE 'Columna principal convertida exitosamente';
END $$;

-- 4. PASO 4: Convertir TODAS las columnas dependientes automáticamente
DO $$
DECLARE
    table_record RECORD;
BEGIN
    RAISE NOTICE '=== CONVIRTIENDO COLUMNAS DEPENDIENTES ===';
    
    FOR table_record IN 
        SELECT DISTINCT tc.table_name
        FROM information_schema.table_constraints AS tc 
        JOIN information_schema.key_column_usage AS kcu
            ON tc.constraint_name = kcu.constraint_name
            AND tc.table_schema = kcu.table_schema
        JOIN information_schema.constraint_column_usage AS ccu
            ON ccu.constraint_name = tc.constraint_name
            AND ccu.table_schema = tc.table_schema
        WHERE tc.constraint_type = 'FOREIGN KEY' 
        AND ccu.table_name = 'lotes'
        AND ccu.column_name = 'lote_id'
    LOOP
        -- Actualizar datos válidos
        EXECUTE format('UPDATE public.%I SET lote_id = lote_id::INTEGER WHERE lote_id ~ ''^[0-9]+$'' AND lote_id IS NOT NULL', 
                      table_record.table_name);
        
        -- Cambiar tipo de columna
        EXECUTE format('ALTER TABLE public.%I ALTER COLUMN lote_id TYPE INTEGER USING lote_id::INTEGER', 
                      table_record.table_name);
        
        RAISE NOTICE 'Tabla % convertida exitosamente', table_record.table_name;
    END LOOP;
END $$;

-- 5. PASO 5: Recrear TODAS las claves foráneas automáticamente
DO $$
DECLARE
    fk_record RECORD;
BEGIN
    RAISE NOTICE '=== RECREANDO CLAVES FORÁNEAS ===';
    
    FOR fk_record IN 
        SELECT 
            tc.table_name,
            tc.constraint_name,
            kcu.column_name,
            ccu.table_name AS foreign_table_name,
            ccu.column_name AS foreign_column_name
        FROM information_schema.table_constraints AS tc 
        JOIN information_schema.key_column_usage AS kcu
            ON tc.constraint_name = kcu.constraint_name
            AND tc.table_schema = kcu.table_schema
        JOIN information_schema.constraint_column_usage AS ccu
            ON ccu.constraint_name = tc.constraint_name
            AND ccu.table_schema = tc.table_schema
        WHERE tc.constraint_type = 'FOREIGN KEY' 
        AND ccu.table_name = 'lotes'
        AND ccu.column_name = 'lote_id'
    LOOP
        -- Recrear la FK
        EXECUTE format('ALTER TABLE public.%I ADD CONSTRAINT %I FOREIGN KEY (%I) REFERENCES public.%I(%I)', 
                      fk_record.table_name, 
                      fk_record.constraint_name,
                      fk_record.column_name,
                      fk_record.foreign_table_name,
                      fk_record.foreign_column_name);
        
        RAISE NOTICE 'FK recreada: % en tabla %', fk_record.constraint_name, fk_record.table_name;
    END LOOP;
END $$;

-- 6. PASO 6: Verificar el resultado
SELECT 
    'Verificación final:' as paso,
    column_name,
    data_type,
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_name = 'lotes' 
AND column_name = 'lote_id';

-- 7. Verificar algunas FK recreadas
SELECT 
    'FK recreadas:' as paso,
    tc.table_name,
    tc.constraint_name,
    kcu.column_name,
    ccu.table_name AS foreign_table_name,
    ccu.column_name AS foreign_column_name
FROM information_schema.table_constraints AS tc 
JOIN information_schema.key_column_usage AS kcu
    ON tc.constraint_name = kcu.constraint_name
    AND tc.table_schema = kcu.table_schema
JOIN information_schema.constraint_column_usage AS ccu
    ON ccu.constraint_name = tc.constraint_name
    AND ccu.table_schema = tc.table_schema
WHERE tc.constraint_type = 'FOREIGN KEY' 
AND ccu.table_name = 'lotes'
AND ccu.column_name = 'lote_id'
LIMIT 5;

-- 8. Verificar algunos datos
SELECT 
    'Datos de prueba:' as paso,
    lote_id, 
    lote_nombre 
FROM public.lotes 
ORDER BY lote_id DESC 
LIMIT 5;







