-- Agregar columna nucleo_p si no existe
ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS nucleo_p VARCHAR(100);

-- Verificar que se agregó correctamente
SELECT column_name, data_type, is_nullable
FROM information_schema.columns 
WHERE table_name = 'produccion_lotes' 
AND column_name = 'nucleo_p';




