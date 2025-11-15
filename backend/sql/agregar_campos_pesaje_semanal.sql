-- ============================================================
-- SQL para agregar campos de Pesaje Semanal a produccion_diaria
-- ============================================================

-- Verificar si la tabla existe
SELECT EXISTS (
   SELECT FROM information_schema.tables 
   WHERE table_name = 'produccion_diaria'
);

-- Campos de Pesaje Semanal (se registran una vez por semana)
-- pesoH: Peso promedio de hembras
ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS peso_h NUMERIC(8, 2) DEFAULT NULL;

-- PesoM: Peso promedio de machos
ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS peso_m NUMERIC(8, 2) DEFAULT NULL;

-- Uniformidad: Porcentaje de uniformidad del lote
ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS uniformidad NUMERIC(5, 2) DEFAULT NULL;

-- Coeficiente de variación (CV): Medida de variabilidad
ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS coeficiente_variacion NUMERIC(5, 2) DEFAULT NULL;

-- Observaciones específicas para el pesaje semanal
ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS observaciones_pesaje TEXT DEFAULT NULL;

-- Verificar que se agregaron las columnas
SELECT column_name, data_type, column_default, is_nullable
FROM information_schema.columns 
WHERE table_name = 'produccion_diaria' 
AND column_name IN ('peso_h', 'peso_m', 'uniformidad', 'coeficiente_variacion', 'observaciones_pesaje')
ORDER BY ordinal_position;

-- Comentarios para documentar los nuevos campos
COMMENT ON COLUMN produccion_diaria.peso_h IS 'Peso promedio de hembras (kg) - Registro semanal';
COMMENT ON COLUMN produccion_diaria.peso_m IS 'Peso promedio de machos (kg) - Registro semanal';
COMMENT ON COLUMN produccion_diaria.uniformidad IS 'Uniformidad del lote (porcentaje) - Registro semanal';
COMMENT ON COLUMN produccion_diaria.coeficiente_variacion IS 'Coeficiente de variación (CV) - Registro semanal';
COMMENT ON COLUMN produccion_diaria.observaciones_pesaje IS 'Observaciones específicas del pesaje semanal';



