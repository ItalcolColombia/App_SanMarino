-- ============================================================
-- Script para verificar/agregar campo nucleo_p en produccion_lotes
-- ============================================================

-- Verificar si la tabla existe
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM information_schema.tables 
        WHERE table_name = 'produccion_lotes'
    ) THEN
        RAISE EXCEPTION 'La tabla produccion_lotes no existe. Ejecuta primero create_produccion_lote_table.sql';
    END IF;
END $$;

-- Agregar columna nucleo_p si no existe
ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS nucleo_p VARCHAR(100);

-- Agregar otras columnas que puedan faltar (por si acaso)
ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS huevos_iniciales INTEGER NOT NULL DEFAULT 0;

ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS tipo_nido VARCHAR(50) NOT NULL DEFAULT 'Manual';

ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS ciclo VARCHAR(50) NOT NULL DEFAULT 'normal';

ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS granja_id INTEGER;

ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS nucleo_id VARCHAR(100);

ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS galpon_id VARCHAR(100);

-- Verificar que se agregó correctamente
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_name = 'produccion_lotes' 
    AND column_name IN ('nucleo_p', 'huevos_iniciales', 'tipo_nido', 'ciclo', 'granja_id', 'nucleo_id', 'galpon_id')
ORDER BY column_name;


