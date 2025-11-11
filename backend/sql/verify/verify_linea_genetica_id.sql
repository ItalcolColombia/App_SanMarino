-- Script para verificar y agregar el campo linea_genetica_id a la tabla lote

-- 1. Verificar si el campo ya existe
SELECT column_name, data_type, is_nullable 
FROM information_schema.columns 
WHERE table_name = 'lote' 
AND column_name = 'linea_genetica_id';

-- 2. Si no existe, agregarlo
ALTER TABLE lote 
ADD COLUMN IF NOT EXISTS linea_genetica_id INTEGER;

-- 3. Agregar comentario
COMMENT ON COLUMN lote.linea_genetica_id IS 'ID de la línea genética asociada al lote';

-- 4. Crear índice para mejorar rendimiento
CREATE INDEX IF NOT EXISTS idx_lote_linea_genetica_id ON lote(linea_genetica_id);

-- 5. Verificar que se agregó correctamente
SELECT column_name, data_type, is_nullable 
FROM information_schema.columns 
WHERE table_name = 'lote' 
AND column_name = 'linea_genetica_id';

-- 6. Ver la estructura actual de la tabla lote
SELECT column_name, data_type, is_nullable 
FROM information_schema.columns 
WHERE table_name = 'lote' 
ORDER BY ordinal_position;

