-- =====================================================
-- Script para agregar campo item_type a farm_inventory_movements
-- Este campo guarda el tipo de item del catálogo (alimento, vacuna, medicamento, etc.)
-- =====================================================

BEGIN;

DO $$
BEGIN
    -- Agregar columna item_type si no existe
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'item_type'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN item_type VARCHAR(50);
        
        -- Actualizar registros existentes con item_type desde catalogo_items
        UPDATE farm_inventory_movements fim
        SET item_type = ci.item_type
        FROM catalogo_items ci
        WHERE fim.catalog_item_id = ci.id 
          AND fim.item_type IS NULL;
        
        -- Agregar índice para mejorar rendimiento
        CREATE INDEX IF NOT EXISTS ix_farm_inventory_movements_item_type 
        ON farm_inventory_movements(item_type);
        
        COMMENT ON COLUMN farm_inventory_movements.item_type IS 
            'Tipo de item del catálogo: alimento, vacuna, medicamento, accesorio, biologico, consumible, otro';
        
        RAISE NOTICE 'Columna item_type agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna item_type ya existe en la tabla farm_inventory_movements.';
    END IF;
END $$;

COMMIT;

-- Verificar que se agregó correctamente
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    character_maximum_length
FROM information_schema.columns
WHERE table_schema = 'public' 
  AND table_name = 'farm_inventory_movements'
  AND column_name = 'item_type';
