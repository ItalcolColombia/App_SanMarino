-- Script de migración para agregar columnas de auditoría a la tabla traslado_huevos
-- Ejecutar este script si la tabla ya existe sin estas columnas

-- Agregar company_id si no existe
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'traslado_huevos' AND column_name = 'company_id'
    ) THEN
        ALTER TABLE traslado_huevos 
        ADD COLUMN company_id INTEGER NOT NULL DEFAULT 1;
        
        -- Actualizar el valor por defecto después de agregar la columna
        ALTER TABLE traslado_huevos 
        ALTER COLUMN company_id DROP DEFAULT;
        
        COMMENT ON COLUMN traslado_huevos.company_id IS 'ID de la compañía (auditoría)';
    END IF;
END $$;

-- Agregar created_by_user_id si no existe
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'traslado_huevos' AND column_name = 'created_by_user_id'
    ) THEN
        ALTER TABLE traslado_huevos 
        ADD COLUMN created_by_user_id INTEGER NOT NULL DEFAULT 1;
        
        -- Actualizar el valor por defecto después de agregar la columna
        ALTER TABLE traslado_huevos 
        ALTER COLUMN created_by_user_id DROP DEFAULT;
        
        COMMENT ON COLUMN traslado_huevos.created_by_user_id IS 'ID del usuario que creó el registro (auditoría)';
    END IF;
END $$;

-- Actualizar created_at si existe pero no tiene el tipo correcto
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'traslado_huevos' 
        AND column_name = 'created_at' 
        AND data_type != 'timestamp with time zone'
    ) THEN
        ALTER TABLE traslado_huevos 
        ALTER COLUMN created_at TYPE TIMESTAMP WITH TIME ZONE;
    END IF;
END $$;

-- Agregar updated_by_user_id si no existe
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'traslado_huevos' AND column_name = 'updated_by_user_id'
    ) THEN
        ALTER TABLE traslado_huevos 
        ADD COLUMN updated_by_user_id INTEGER;
        
        COMMENT ON COLUMN traslado_huevos.updated_by_user_id IS 'ID del usuario que actualizó el registro (auditoría)';
    END IF;
END $$;

-- Actualizar updated_at si existe pero no tiene el tipo correcto
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'traslado_huevos' 
        AND column_name = 'updated_at' 
        AND data_type != 'timestamp with time zone'
    ) THEN
        ALTER TABLE traslado_huevos 
        ALTER COLUMN updated_at TYPE TIMESTAMP WITH TIME ZONE;
    END IF;
END $$;

-- Actualizar deleted_at si existe pero no tiene el tipo correcto
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'traslado_huevos' 
        AND column_name = 'deleted_at' 
        AND data_type != 'timestamp with time zone'
    ) THEN
        ALTER TABLE traslado_huevos 
        ALTER COLUMN deleted_at TYPE TIMESTAMP WITH TIME ZONE;
    END IF;
END $$;

-- Eliminar columnas antiguas si existen (created_by, updated_by)
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'traslado_huevos' AND column_name = 'created_by'
    ) THEN
        ALTER TABLE traslado_huevos DROP COLUMN created_by;
    END IF;
    
    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'traslado_huevos' AND column_name = 'updated_by'
    ) THEN
        ALTER TABLE traslado_huevos DROP COLUMN updated_by;
    END IF;
END $$;

-- Crear índice para company_id si no existe
CREATE INDEX IF NOT EXISTS idx_traslado_huevos_company_id ON traslado_huevos(company_id);

-- Verificar que todas las columnas estén presentes
SELECT 
    column_name, 
    data_type, 
    is_nullable
FROM information_schema.columns 
WHERE table_name = 'traslado_huevos' 
    AND column_name IN ('company_id', 'created_by_user_id', 'created_at', 'updated_by_user_id', 'updated_at', 'deleted_at')
ORDER BY column_name;





