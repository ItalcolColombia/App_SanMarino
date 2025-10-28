-- Script CONSERVADOR para convertir LoteId de string a integer en SeguimientoLoteLevante
-- Solo convierte registros que tienen lote_id numérico válido y que existen en la tabla lotes

-- PASO 1: Crear tabla temporal con los datos válidos
CREATE TEMP TABLE seguimiento_lote_levante_temp AS
SELECT 
    s.id,
    s.lote_id::INTEGER as lote_id,
    s.fecha_registro,
    s.mortalidad_hembras,
    s.mortalidad_machos,
    s.sel_h,
    s.sel_m,
    s.error_sexaje_hembras,
    s.error_sexaje_machos,
    s.consumo_kg_hembras,
    s.tipo_alimento,
    s.observaciones,
    s.kcal_al_h,
    s.prot_al_h,
    s.kcal_ave_h,
    s.prot_ave_h,
    s.ciclo,
    s.consumo_kg_machos,
    s.peso_prom_h,
    s.peso_prom_m,
    s.uniformidad_h,
    s.uniformidad_m,
    s.cv_h,
    s.cv_m,
    s.company_id,
    s.created_by,
    s.created,
    s.last_modified_by,
    s.last_modified
FROM seguimiento_lote_levante s
WHERE s.lote_id ~ '^[0-9]+$'
AND EXISTS (SELECT 1 FROM lotes l WHERE l.lote_id = s.lote_id::INTEGER);

-- PASO 2: Verificar cuántos registros se van a convertir
SELECT 
    'Registros que se van a convertir:' as info,
    COUNT(*) as cantidad
FROM seguimiento_lote_levante_temp;

-- PASO 3: Verificar cuántos registros se van a perder
SELECT 
    'Registros que se van a perder (lote_id no numérico o lote no existe):' as info,
    COUNT(*) as cantidad
FROM seguimiento_lote_levante s
WHERE s.lote_id !~ '^[0-9]+$' 
OR NOT EXISTS (SELECT 1 FROM lotes l WHERE l.lote_id = s.lote_id::INTEGER);

-- PASO 4: Si estás seguro, ejecutar la conversión
-- IMPORTANTE: Descomenta las siguientes líneas solo si estás seguro de los resultados anteriores

/*
-- Eliminar la tabla original
DROP TABLE seguimiento_lote_levante;

-- Renombrar la tabla temporal
ALTER TABLE seguimiento_lote_levante_temp RENAME TO seguimiento_lote_levante;

-- Recrear los índices
CREATE INDEX idx_seguimiento_lote_levante_lote_id ON seguimiento_lote_levante(lote_id);
CREATE INDEX idx_seguimiento_lote_levante_fecha_registro ON seguimiento_lote_levante(fecha_registro);

-- Verificar el resultado
SELECT column_name, data_type 
FROM information_schema.columns 
WHERE table_name = 'seguimiento_lote_levante' 
AND column_name = 'lote_id';
*/







