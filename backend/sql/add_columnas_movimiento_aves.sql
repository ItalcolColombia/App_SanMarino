-- Script para agregar las columnas planta_destino y descripcion a la tabla movimiento_aves
-- Fecha: 2026-01-30
-- Descripción: Agrega campos para almacenar información de planta destino (para traslados) y descripción (para ventas)

-- Verificar si las columnas ya existen antes de agregarlas
DO $$
BEGIN
    -- Agregar columna planta_destino si no existe
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'movimiento_aves' 
        AND column_name = 'planta_destino'
    ) THEN
        ALTER TABLE movimiento_aves
        ADD COLUMN planta_destino VARCHAR(200);
        
        RAISE NOTICE 'Columna planta_destino agregada exitosamente';
    ELSE
        RAISE NOTICE 'La columna planta_destino ya existe';
    END IF;

    -- Agregar columna descripcion si no existe
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'movimiento_aves' 
        AND column_name = 'descripcion'
    ) THEN
        ALTER TABLE movimiento_aves
        ADD COLUMN descripcion VARCHAR(1000);
        
        RAISE NOTICE 'Columna descripcion agregada exitosamente';
    ELSE
        RAISE NOTICE 'La columna descripcion ya existe';
    END IF;
END $$;

-- Agregar comentarios a las columnas para documentación
COMMENT ON COLUMN movimiento_aves.planta_destino IS 'Nombre de la planta destino para traslados a plantas';
COMMENT ON COLUMN movimiento_aves.descripcion IS 'Descripción detallada de la venta o movimiento';
