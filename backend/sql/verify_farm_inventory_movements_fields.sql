-- =====================================================
-- Script para verificar y agregar campos faltantes en farm_inventory_movements
-- Verifica que todos los campos necesarios para movimientos normales existan
-- =====================================================

BEGIN;

DO $$
BEGIN
    -- Verificar y agregar campo 'origin' si no existe
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'origin'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN origin VARCHAR(100);
        
        COMMENT ON COLUMN farm_inventory_movements.origin IS 'Origen para entradas (ej: "Planta Sanmarino", "Planta Itacol")';
        
        RAISE NOTICE 'Columna origin agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna origin ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- Verificar y agregar campo 'destination' si no existe
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'destination'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN destination VARCHAR(100);
        
        COMMENT ON COLUMN farm_inventory_movements.destination IS 'Destino para salidas (ej: "Venta", "Movimiento", "Devolución")';
        
        RAISE NOTICE 'Columna destination agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna destination ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- Verificar y agregar campo 'reference' si no existe
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'reference'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN reference VARCHAR(50);
        
        COMMENT ON COLUMN farm_inventory_movements.reference IS 'Referencia del movimiento (número de documento, factura, etc.)';
        
        RAISE NOTICE 'Columna reference agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna reference ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- Verificar y agregar campo 'reason' si no existe
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'reason'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN reason VARCHAR(200);
        
        COMMENT ON COLUMN farm_inventory_movements.reason IS 'Motivo del movimiento';
        
        RAISE NOTICE 'Columna reason agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna reason ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- Verificar y agregar campo 'unit' si no existe
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'unit'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN unit VARCHAR(20) NOT NULL DEFAULT 'kg';
        
        COMMENT ON COLUMN farm_inventory_movements.unit IS 'Unidad de medida (kg, und, l, etc.)';
        
        RAISE NOTICE 'Columna unit agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna unit ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- Verificar y agregar campo 'quantity' si no existe
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'quantity'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN quantity NUMERIC(18,3) NOT NULL DEFAULT 0;
        
        COMMENT ON COLUMN farm_inventory_movements.quantity IS 'Cantidad del movimiento';
        
        RAISE NOTICE 'Columna quantity agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna quantity ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- Verificar y agregar campo 'farm_id' si no existe
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'farm_id'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN farm_id INTEGER NOT NULL;
        
        ALTER TABLE farm_inventory_movements
        ADD CONSTRAINT fk_farm_inventory_movements_farm_id 
        FOREIGN KEY (farm_id) REFERENCES farms(id) ON DELETE RESTRICT;
        
        RAISE NOTICE 'Columna farm_id agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna farm_id ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- Verificar y agregar campo 'catalog_item_id' si no existe
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'catalog_item_id'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN catalog_item_id INTEGER NOT NULL;
        
        ALTER TABLE farm_inventory_movements
        ADD CONSTRAINT fk_farm_inventory_movements_catalog_item_id 
        FOREIGN KEY (catalog_item_id) REFERENCES catalogo_items(id) ON DELETE RESTRICT;
        
        RAISE NOTICE 'Columna catalog_item_id agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna catalog_item_id ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- Verificar y agregar campo 'movement_type' si no existe
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'movement_type'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN movement_type VARCHAR(20) NOT NULL;
        
        COMMENT ON COLUMN farm_inventory_movements.movement_type IS 'Tipo de movimiento: Entry, Exit, TransferIn, TransferOut, Adjust';
        
        RAISE NOTICE 'Columna movement_type agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna movement_type ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- Verificar campos de empresa y país (ya deberían existir por scripts anteriores)
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'company_id'
    ) THEN
        RAISE NOTICE 'ADVERTENCIA: Columna company_id no existe. Ejecute add_campos_empresa_pais_inventario.sql primero.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'pais_id'
    ) THEN
        RAISE NOTICE 'ADVERTENCIA: Columna pais_id no existe. Ejecute add_campos_empresa_pais_inventario.sql primero.';
    END IF;

END $$;

COMMIT;

-- Mostrar resumen de columnas
SELECT 
    column_name,
    data_type,
    character_maximum_length,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_schema = 'public' 
  AND table_name = 'farm_inventory_movements'
ORDER BY ordinal_position;
