-- Script SEGURO para convertir LoteId de string a integer en SeguimientoLoteLevante
-- IMPORTANTE: Ejecutar este script paso a paso en la base de datos AWS RDS

-- PASO 1: Verificar el estado actual
SELECT 'Estado actual de la tabla seguimiento_lote_levante:' as info;

SELECT column_name, data_type, character_maximum_length 
FROM information_schema.columns 
WHERE table_name = 'seguimiento_lote_levante' 
AND column_name = 'lote_id';

-- PASO 2: Verificar qué valores de lote_id existen
SELECT 'Valores únicos de lote_id en seguimiento_lote_levante:' as info;

SELECT lote_id, COUNT(*) as cantidad_registros
FROM seguimiento_lote_levante 
GROUP BY lote_id 
ORDER BY lote_id;

-- PASO 3: Verificar cuáles valores son numéricos válidos
SELECT 'Valores que se pueden convertir a integer:' as info;

SELECT lote_id, COUNT(*) as cantidad_registros
FROM seguimiento_lote_levante 
WHERE lote_id ~ '^[0-9]+$'
GROUP BY lote_id 
ORDER BY lote_id::INTEGER;

-- PASO 4: Verificar valores problemáticos
SELECT 'Valores que NO se pueden convertir a integer:' as info;

SELECT lote_id, COUNT(*) as cantidad_registros
FROM seguimiento_lote_levante 
WHERE lote_id !~ '^[0-9]+$' OR lote_id IS NULL
GROUP BY lote_id 
ORDER BY lote_id;

-- PASO 5: Verificar que los lotes referenciados existen
SELECT 'Lotes referenciados que existen en la tabla lotes:' as info;

SELECT DISTINCT s.lote_id, COUNT(*) as registros_seguimiento
FROM seguimiento_lote_levante s
WHERE s.lote_id ~ '^[0-9]+$'
AND EXISTS (SELECT 1 FROM lotes l WHERE l.lote_id = s.lote_id::INTEGER)
GROUP BY s.lote_id
ORDER BY s.lote_id::INTEGER;

-- PASO 6: Verificar lotes referenciados que NO existen
SELECT 'Lotes referenciados que NO existen en la tabla lotes:' as info;

SELECT DISTINCT s.lote_id, COUNT(*) as registros_seguimiento
FROM seguimiento_lote_levante s
WHERE s.lote_id ~ '^[0-9]+$'
AND NOT EXISTS (SELECT 1 FROM lotes l WHERE l.lote_id = s.lote_id::INTEGER)
GROUP BY s.lote_id
ORDER BY s.lote_id::INTEGER;







