-- =====================================================
-- Script para agregar campos específicos para movimiento de alimento
-- ZooSanMarino - PostgreSQL
-- =====================================================

BEGIN;

DO $$
BEGIN
    -- 1. Documento origen (Autoconsumo, RVN, EAN)
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'documento_origen'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN documento_origen VARCHAR(50) NULL;
        
        RAISE NOTICE 'Columna documento_origen agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna documento_origen ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- 2. Tipo de entrada (Entrada Nueva, Traslado entre galpon, Traslados entre granjas)
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'tipo_entrada'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN tipo_entrada VARCHAR(50) NULL;
        
        RAISE NOTICE 'Columna tipo_entrada agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna tipo_entrada ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- 3. Galpón destino (para movimientos a galpones específicos)
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'galpon_destino_id'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN galpon_destino_id VARCHAR(50) NULL;
        
        RAISE NOTICE 'Columna galpon_destino_id agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna galpon_destino_id ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- 4. Fecha de movimiento (permite registrar fecha diferente a created_at)
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'fecha_movimiento'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN fecha_movimiento TIMESTAMP WITH TIME ZONE NULL;
        
        -- Establecer fecha_movimiento = created_at para registros existentes
        UPDATE farm_inventory_movements
        SET fecha_movimiento = created_at
        WHERE fecha_movimiento IS NULL;
        
        RAISE NOTICE 'Columna fecha_movimiento agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna fecha_movimiento ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- 5. Crear índices para mejorar consultas
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = 'public'
        AND tablename = 'farm_inventory_movements' 
        AND indexname = 'ix_farm_inventory_movements_documento_origen'
    ) THEN
        CREATE INDEX ix_farm_inventory_movements_documento_origen 
        ON farm_inventory_movements (documento_origen);
        RAISE NOTICE 'Índice ix_farm_inventory_movements_documento_origen creado.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = 'public'
        AND tablename = 'farm_inventory_movements' 
        AND indexname = 'ix_farm_inventory_movements_tipo_entrada'
    ) THEN
        CREATE INDEX ix_farm_inventory_movements_tipo_entrada 
        ON farm_inventory_movements (tipo_entrada);
        RAISE NOTICE 'Índice ix_farm_inventory_movements_tipo_entrada creado.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = 'public'
        AND tablename = 'farm_inventory_movements' 
        AND indexname = 'ix_farm_inventory_movements_fecha_movimiento'
    ) THEN
        CREATE INDEX ix_farm_inventory_movements_fecha_movimiento 
        ON farm_inventory_movements (fecha_movimiento);
        RAISE NOTICE 'Índice ix_farm_inventory_movements_fecha_movimiento creado.';
    END IF;
END $$;

COMMENT ON COLUMN farm_inventory_movements.documento_origen IS 'Tipo de documento origen: Autoconsumo (autofacturado), Remisión facturada (RVN), Entrada de inventario (EAN)';
COMMENT ON COLUMN farm_inventory_movements.tipo_entrada IS 'Tipo de entrada: Entrada Nueva, Traslado entre galpon, Traslados entre granjas';
COMMENT ON COLUMN farm_inventory_movements.galpon_destino_id IS 'ID del galpón destino para movimientos a galpones específicos';
COMMENT ON COLUMN farm_inventory_movements.fecha_movimiento IS 'Fecha del movimiento (puede ser diferente a created_at para movimientos retroactivos)';

COMMIT;
