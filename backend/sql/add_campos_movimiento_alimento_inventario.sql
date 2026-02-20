-- =====================================================
-- Script para agregar campos de movimiento de alimento a farm_inventory_movements
-- ZooSanMarino - PostgreSQL
-- Campos: documento_origen, tipo_entrada, galpon_destino_id, fecha_movimiento
-- =====================================================

BEGIN;

DO $$
BEGIN
    -- 1. Agregar documento_origen
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'documento_origen'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN documento_origen VARCHAR(50) NULL;
        
        COMMENT ON COLUMN farm_inventory_movements.documento_origen IS 
            'Tipo de documento origen: Autoconsumo (autofacturado), RVN (Remisión facturada - Planta a Granja), EAN (Entrada de inventario)';
        
        -- Crear índice
        CREATE INDEX IF NOT EXISTS ix_farm_inventory_movements_documento_origen 
        ON farm_inventory_movements (documento_origen);
        
        RAISE NOTICE 'Columna documento_origen agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna documento_origen ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- 2. Agregar tipo_entrada
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'tipo_entrada'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN tipo_entrada VARCHAR(50) NULL;
        
        COMMENT ON COLUMN farm_inventory_movements.tipo_entrada IS 
            'Tipo de entrada: Entrada Nueva, Traslado entre galpon, Traslados entre granjas';
        
        -- Crear índice
        CREATE INDEX IF NOT EXISTS ix_farm_inventory_movements_tipo_entrada 
        ON farm_inventory_movements (tipo_entrada);
        
        RAISE NOTICE 'Columna tipo_entrada agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna tipo_entrada ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- 3. Agregar galpon_destino_id
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'galpon_destino_id'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN galpon_destino_id VARCHAR(50) NULL;
        
        COMMENT ON COLUMN farm_inventory_movements.galpon_destino_id IS 
            'ID del galpón destino donde se asigna el alimento';
        
        RAISE NOTICE 'Columna galpon_destino_id agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna galpon_destino_id ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- 4. Agregar fecha_movimiento
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'fecha_movimiento'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN fecha_movimiento TIMESTAMPTZ NULL;
        
        COMMENT ON COLUMN farm_inventory_movements.fecha_movimiento IS 
            'Fecha del movimiento (puede ser diferente a created_at)';
        
        -- Crear índice
        CREATE INDEX IF NOT EXISTS ix_farm_inventory_movements_fecha_movimiento 
        ON farm_inventory_movements (fecha_movimiento);
        
        RAISE NOTICE 'Columna fecha_movimiento agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna fecha_movimiento ya existe en la tabla farm_inventory_movements.';
    END IF;
END $$;

COMMIT;
