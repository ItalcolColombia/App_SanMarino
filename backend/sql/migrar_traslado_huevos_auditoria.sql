-- Script SQL para agregar campos de auditoría a la tabla traslado_huevos existente
-- Ejecutar este script en tu base de datos PostgreSQL

-- 1. Agregar company_id si no existe
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'traslado_huevos' AND column_name = 'company_id'
    ) THEN
        ALTER TABLE traslado_huevos 
        ADD COLUMN company_id INTEGER NOT NULL DEFAULT 1;
        
        -- Remover el valor por defecto después de agregar
        ALTER TABLE traslado_huevos 
        ALTER COLUMN company_id DROP DEFAULT;
        
        RAISE NOTICE 'Columna company_id agregada exitosamente';
    ELSE
        RAISE NOTICE 'Columna company_id ya existe';
    END IF;
END $$;

-- 2. Agregar created_by_user_id si no existe
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'traslado_huevos' AND column_name = 'created_by_user_id'
    ) THEN
        ALTER TABLE traslado_huevos 
        ADD COLUMN created_by_user_id INTEGER NOT NULL DEFAULT 1;
        
        -- Remover el valor por defecto después de agregar
        ALTER TABLE traslado_huevos 
        ALTER COLUMN created_by_user_id DROP DEFAULT;
        
        RAISE NOTICE 'Columna created_by_user_id agregada exitosamente';
    ELSE
        RAISE NOTICE 'Columna created_by_user_id ya existe';
    END IF;
END $$;

-- 3. Actualizar created_at si existe pero tiene tipo diferente
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
        
        RAISE NOTICE 'Tipo de created_at actualizado a TIMESTAMP WITH TIME ZONE';
    ELSIF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'traslado_huevos' AND column_name = 'created_at'
    ) THEN
        ALTER TABLE traslado_huevos 
        ADD COLUMN created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP;
        
        ALTER TABLE traslado_huevos 
        ALTER COLUMN created_at DROP DEFAULT;
        
        RAISE NOTICE 'Columna created_at agregada exitosamente';
    ELSE
        RAISE NOTICE 'Columna created_at ya existe con el tipo correcto';
    END IF;
END $$;

-- 4. Agregar updated_by_user_id si no existe
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'traslado_huevos' AND column_name = 'updated_by_user_id'
    ) THEN
        ALTER TABLE traslado_huevos 
        ADD COLUMN updated_by_user_id INTEGER;
        
        RAISE NOTICE 'Columna updated_by_user_id agregada exitosamente';
    ELSE
        RAISE NOTICE 'Columna updated_by_user_id ya existe';
    END IF;
END $$;

-- 5. Actualizar updated_at si existe pero tiene tipo diferente
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
        
        RAISE NOTICE 'Tipo de updated_at actualizado a TIMESTAMP WITH TIME ZONE';
    ELSIF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'traslado_huevos' AND column_name = 'updated_at'
    ) THEN
        ALTER TABLE traslado_huevos 
        ADD COLUMN updated_at TIMESTAMP WITH TIME ZONE;
        
        RAISE NOTICE 'Columna updated_at agregada exitosamente';
    ELSE
        RAISE NOTICE 'Columna updated_at ya existe con el tipo correcto';
    END IF;
END $$;

-- 6. Actualizar deleted_at si existe pero tiene tipo diferente
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
        
        RAISE NOTICE 'Tipo de deleted_at actualizado a TIMESTAMP WITH TIME ZONE';
    ELSIF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'traslado_huevos' AND column_name = 'deleted_at'
    ) THEN
        ALTER TABLE traslado_huevos 
        ADD COLUMN deleted_at TIMESTAMP WITH TIME ZONE;
        
        RAISE NOTICE 'Columna deleted_at agregada exitosamente';
    ELSE
        RAISE NOTICE 'Columna deleted_at ya existe con el tipo correcto';
    END IF;
END $$;

-- 7. Eliminar columnas antiguas si existen (created_by, updated_by)
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'traslado_huevos' AND column_name = 'created_by'
    ) THEN
        ALTER TABLE traslado_huevos DROP COLUMN created_by;
        RAISE NOTICE 'Columna created_by eliminada';
    END IF;
    
    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'traslado_huevos' AND column_name = 'updated_by'
    ) THEN
        ALTER TABLE traslado_huevos DROP COLUMN updated_by;
        RAISE NOTICE 'Columna updated_by eliminada';
    END IF;
END $$;

-- 8. Crear índice para company_id si no existe
CREATE INDEX IF NOT EXISTS idx_traslado_huevos_company_id ON traslado_huevos(company_id);

-- 9. Verificar que todas las columnas estén presentes
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_name = 'traslado_huevos' 
    AND column_name IN (
        'company_id', 
        'created_by_user_id', 
        'created_at', 
        'updated_by_user_id', 
        'updated_at', 
        'deleted_at'
    )
ORDER BY column_name;

-- Mensaje final
DO $$
BEGIN
    RAISE NOTICE '========================================';
    RAISE NOTICE 'Migración completada exitosamente';
    RAISE NOTICE 'Verifica las columnas listadas arriba';
    RAISE NOTICE '========================================';
END $$;





