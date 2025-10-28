-- Script para convertir LoteId de string a integer en SeguimientoLoteLevante
-- IMPORTANTE: Ejecutar este script en la base de datos AWS RDS

-- 1. Verificar el tipo actual de la columna
SELECT column_name, data_type, character_maximum_length 
FROM information_schema.columns 
WHERE table_name = 'seguimiento_lote_levante' 
AND column_name = 'lote_id';

-- 2. Crear una columna temporal con el tipo correcto
ALTER TABLE seguimiento_lote_levante 
ADD COLUMN lote_id_temp INTEGER;

-- 3. Convertir los datos de string a integer (solo registros válidos)
UPDATE seguimiento_lote_levante 
SET lote_id_temp = lote_id::INTEGER 
WHERE lote_id ~ '^[0-9]+$';

-- 4. Verificar cuántos registros se convirtieron correctamente
SELECT 
    COUNT(*) as total_registros,
    COUNT(lote_id_temp) as registros_convertidos,
    COUNT(*) - COUNT(lote_id_temp) as registros_no_convertidos
FROM seguimiento_lote_levante;

-- 5. Si hay registros no convertidos, mostrarlos
SELECT lote_id, COUNT(*) 
FROM seguimiento_lote_levante 
WHERE lote_id_temp IS NULL 
GROUP BY lote_id 
ORDER BY COUNT(*) DESC;

-- 6. Eliminar la columna original
ALTER TABLE seguimiento_lote_levante 
DROP COLUMN lote_id;

-- 7. Renombrar la columna temporal
ALTER TABLE seguimiento_lote_levante 
RENAME COLUMN lote_id_temp TO lote_id;

-- 8. Crear el índice y la foreign key si no existen
CREATE INDEX IF NOT EXISTS idx_seguimiento_lote_levante_lote_id 
ON seguimiento_lote_levante(lote_id);

-- 9. Verificar el resultado final
SELECT column_name, data_type 
FROM information_schema.columns 
WHERE table_name = 'seguimiento_lote_levante' 
AND column_name = 'lote_id';







