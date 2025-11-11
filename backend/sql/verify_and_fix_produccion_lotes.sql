-- ============================================================
-- Verificar y arreglar tabla produccion_lotes
-- ============================================================

-- 1. Ver estructura actual de la tabla
SELECT 
    column_name, 
    data_type, 
    character_maximum_length,
    column_default, 
    is_nullable
FROM information_schema.columns 
WHERE table_name = 'produccion_lotes' 
ORDER BY ordinal_position;

-- 2. Si lote_id es VARCHAR, necesitamos cambiarlo a INTEGER
-- Verificar si existe columna con ese nombre
SELECT EXISTS (
    SELECT 1 FROM information_schema.columns 
    WHERE table_name = 'produccion_lotes' 
    AND column_name = 'lote_id'
);

-- 3. Si la columna lote_id es VARCHAR, agregar una nueva columna integer
-- y eliminar la vieja (ESTE ES UN PROCESO COMPLEJO, MEJOR RENOMBRAR)

-- OPCIÓN SEGURA: Ver si ya tiene las columnas correctas con otros nombres
-- y crear las que faltan

-- Agregar columnas que faltan (si no existen con estos nombres exactos)
ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS aves_iniciales_h INTEGER NOT NULL DEFAULT 0;

ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS aves_iniciales_m INTEGER NOT NULL DEFAULT 0;

ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS huevos_iniciales INTEGER NOT NULL DEFAULT 0;

ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS tipo_nido VARCHAR(50) NOT NULL DEFAULT 'Manual';

ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS nucleo_p VARCHAR(100);

ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS ciclo VARCHAR(50) NOT NULL DEFAULT 'normal';

-- Verificar después de agregar columnas
SELECT column_name, data_type, is_nullable
FROM information_schema.columns 
WHERE table_name = 'produccion_lotes' 
ORDER BY ordinal_position;

