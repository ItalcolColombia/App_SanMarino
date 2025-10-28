-- Script de verificación para auto-incremento en tabla lotes
-- Ejecutar después de aplicar la migración

-- 1. Verificar la configuración de la columna lote_id
SELECT 
    'Configuración de columna lote_id:' as verificación,
    column_name,
    data_type,
    column_default,
    is_nullable
FROM information_schema.columns 
WHERE table_name = 'lotes' 
AND column_name = 'lote_id';

-- 2. Verificar la secuencia
SELECT 
    'Configuración de secuencia:' as verificación,
    sequence_name,
    data_type,
    start_value,
    minimum_value,
    maximum_value,
    increment,
    last_value
FROM information_schema.sequences 
WHERE sequence_name = 'lotes_lote_id_seq';

-- 3. Verificar el valor actual de la secuencia
SELECT 
    'Valor actual de la secuencia:' as verificación,
    last_value,
    is_called
FROM lotes_lote_id_seq;

-- 4. Verificar los últimos IDs en la tabla
SELECT 
    'Últimos IDs en la tabla:' as verificación,
    lote_id,
    lote_nombre,
    created_at
FROM public.lotes 
WHERE lote_id IS NOT NULL
ORDER BY lote_id DESC 
LIMIT 5;

-- 5. Probar la secuencia (esto incrementará el valor)
SELECT 
    'Próximo valor de la secuencia:' as verificación,
    nextval('lotes_lote_id_seq') as next_value;

-- 6. Verificar que la tabla tiene la configuración correcta
SELECT 
    'Verificación final:' as verificación,
    CASE 
        WHEN column_default LIKE '%nextval%' THEN '✅ Auto-incremento configurado correctamente'
        ELSE '❌ Auto-incremento NO configurado'
    END as estado
FROM information_schema.columns 
WHERE table_name = 'lotes' 
AND column_name = 'lote_id';
