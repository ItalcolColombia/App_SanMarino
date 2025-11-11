-- Script de migración para configurar auto-incremento en tabla lotes
-- Ejecutar en la base de datos AWS RDS

-- Paso 1: Verificar si ya existe una secuencia
DO $$
DECLARE
    seq_exists BOOLEAN;
    max_id INTEGER;
BEGIN
    -- Verificar si la secuencia ya existe
    SELECT EXISTS (
        SELECT 1 FROM information_schema.sequences 
        WHERE sequence_name = 'lotes_lote_id_seq'
    ) INTO seq_exists;
    
    IF NOT seq_exists THEN
        -- Crear la secuencia
        CREATE SEQUENCE lotes_lote_id_seq;
        RAISE NOTICE 'Secuencia lotes_lote_id_seq creada';
    ELSE
        RAISE NOTICE 'Secuencia lotes_lote_id_seq ya existe';
    END IF;
    
    -- Configurar la columna para usar la secuencia
    ALTER TABLE public.lotes 
    ALTER COLUMN lote_id SET DEFAULT nextval('lotes_lote_id_seq');
    
    -- Configurar la secuencia para que pertenezca a la columna
    ALTER SEQUENCE lotes_lote_id_seq OWNED BY public.lotes.lote_id;
    
    -- Configurar el valor inicial basado en el máximo valor actual
    SELECT COALESCE(MAX(lote_id::INTEGER), 0) INTO max_id 
    FROM public.lotes 
    WHERE lote_id IS NOT NULL;
    
    IF max_id > 0 THEN
        PERFORM setval('lotes_lote_id_seq', max_id + 1, false);
        RAISE NOTICE 'Secuencia configurada con valor inicial: %', max_id + 1;
    ELSE
        PERFORM setval('lotes_lote_id_seq', 1, false);
        RAISE NOTICE 'Secuencia configurada con valor inicial: 1';
    END IF;
    
END $$;

-- Paso 2: Verificar la configuración
SELECT 
    column_name,
    data_type,
    column_default,
    is_nullable
FROM information_schema.columns 
WHERE table_name = 'lotes' 
AND column_name = 'lote_id';

-- Paso 3: Probar la secuencia
SELECT nextval('lotes_lote_id_seq') as next_value;

-- Paso 4: Verificar que la tabla puede insertar sin especificar lote_id
-- (Solo para prueba - comentar en producción)
/*
INSERT INTO public.lotes (
    lote_nombre, 
    granja_id, 
    company_id, 
    created_by_user_id,
    created_at
) VALUES (
    'TEST_AUTO_INCREMENT_' || extract(epoch from now())::text, 
    1, 
    1, 
    'test-user',
    now()
);

SELECT lote_id, lote_nombre 
FROM public.lotes 
WHERE lote_nombre LIKE 'TEST_AUTO_INCREMENT_%'
ORDER BY created_at DESC 
LIMIT 1;

-- Limpiar el registro de prueba
DELETE FROM public.lotes 
WHERE lote_nombre LIKE 'TEST_AUTO_INCREMENT_%';
*/
