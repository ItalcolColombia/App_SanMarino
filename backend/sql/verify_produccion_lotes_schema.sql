-- Verificar las columnas que existen en produccion_lotes
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_name = 'produccion_lotes' 
ORDER BY ordinal_position;
