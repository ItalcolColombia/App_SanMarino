-- ============================================================
-- Script para agregar/verificar campos en tabla produccion_diaria
-- Esta tabla se usa para SeguimientoProduccion
-- ============================================================
-- Verificar y agregar campos que puedan faltar en produccion_diaria

-- 1. Verificar si la tabla existe
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM information_schema.tables 
        WHERE table_name = 'produccion_diaria'
    ) THEN
        -- Si no existe, crearla completa
        CREATE TABLE produccion_diaria (
            id SERIAL PRIMARY KEY,
            fecha_registro TIMESTAMP WITH TIME ZONE NOT NULL,
            lote_id TEXT NOT NULL,
            mortalidad_hembras INTEGER NOT NULL DEFAULT 0,
            mortalidad_machos INTEGER NOT NULL DEFAULT 0,
            sel_h INTEGER NOT NULL DEFAULT 0,
            cons_kg_h DOUBLE PRECISION NOT NULL DEFAULT 0,
            cons_kg_m DOUBLE PRECISION NOT NULL DEFAULT 0,
            huevo_tot INTEGER NOT NULL DEFAULT 0,
            huevo_inc INTEGER NOT NULL DEFAULT 0,
            tipo_alimento TEXT NOT NULL DEFAULT 'Standard',
            observaciones TEXT,
            peso_huevo DOUBLE PRECISION NOT NULL DEFAULT 0,
            etapa INTEGER NOT NULL DEFAULT 1
        );
        
        -- Índice único por lote y fecha
        CREATE UNIQUE INDEX IF NOT EXISTS ix_produccion_diaria_lote_fecha 
            ON produccion_diaria(lote_id, fecha_registro);
        
        -- Índice por lote_id
        CREATE INDEX IF NOT EXISTS ix_produccion_diaria_lote_id 
            ON produccion_diaria(lote_id);
        
        RAISE NOTICE 'Tabla produccion_diaria creada';
    ELSE
        RAISE NOTICE 'Tabla produccion_diaria ya existe';
    END IF;
END $$;

-- 2. Agregar campos que puedan faltar
ALTER TABLE produccion_diaria 
    ADD COLUMN IF NOT EXISTS sel_h INTEGER NOT NULL DEFAULT 0;

ALTER TABLE produccion_diaria 
    ADD COLUMN IF NOT EXISTS cons_kg_h DOUBLE PRECISION NOT NULL DEFAULT 0;

ALTER TABLE produccion_diaria 
    ADD COLUMN IF NOT EXISTS cons_kg_m DOUBLE PRECISION NOT NULL DEFAULT 0;

ALTER TABLE produccion_diaria 
    ADD COLUMN IF NOT EXISTS tipo_alimento TEXT NOT NULL DEFAULT 'Standard';

ALTER TABLE produccion_diaria 
    ADD COLUMN IF NOT EXISTS etapa INTEGER NOT NULL DEFAULT 1;

-- 3. Asegurar que tipo_alimento tenga un valor por defecto en registros existentes
UPDATE produccion_diaria 
SET tipo_alimento = 'Standard' 
WHERE tipo_alimento IS NULL OR tipo_alimento = '';

-- 4. Asegurar que etapa tenga un valor por defecto en registros existentes
UPDATE produccion_diaria 
SET etapa = 1 
WHERE etapa IS NULL OR etapa < 1 OR etapa > 3;

-- 5. Verificar estructura final
SELECT 
    column_name, 
    data_type, 
    column_default, 
    is_nullable
FROM information_schema.columns 
WHERE table_name = 'produccion_diaria' 
ORDER BY ordinal_position;

-- 6. Verificar índices
SELECT 
    indexname, 
    indexdef
FROM pg_indexes 
WHERE tablename = 'produccion_diaria';





