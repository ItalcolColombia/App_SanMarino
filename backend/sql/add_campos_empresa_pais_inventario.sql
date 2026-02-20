-- =====================================================
-- Script para agregar campos de empresa y país a las tablas de inventario
-- ZooSanMarino - PostgreSQL
-- =====================================================

BEGIN;

DO $$
BEGIN
    -- 1. Agregar company_id y pais_id a farm_product_inventory
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_product_inventory' 
        AND column_name = 'company_id'
    ) THEN
        ALTER TABLE farm_product_inventory
        ADD COLUMN company_id INTEGER NULL;
        
        -- Actualizar registros existentes con company_id desde farms
        UPDATE farm_product_inventory fpi
        SET company_id = f.company_id
        FROM farms f
        WHERE fpi.farm_id = f.id AND fpi.company_id IS NULL;
        
        -- Hacer NOT NULL después de actualizar
        ALTER TABLE farm_product_inventory
        ALTER COLUMN company_id SET NOT NULL;
        
        -- Agregar foreign key
        ALTER TABLE farm_product_inventory
        ADD CONSTRAINT fk_farm_product_inventory_company_id 
        FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE;
        
        RAISE NOTICE 'Columna company_id agregada a la tabla farm_product_inventory.';
    ELSE
        RAISE NOTICE 'Columna company_id ya existe en la tabla farm_product_inventory.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_product_inventory' 
        AND column_name = 'pais_id'
    ) THEN
        ALTER TABLE farm_product_inventory
        ADD COLUMN pais_id INTEGER NULL;
        
        -- Actualizar registros existentes con pais_id desde farms a través de departamentos
        UPDATE farm_product_inventory fpi
        SET pais_id = d.pais_id
        FROM farms f
        JOIN departamentos d ON f.departamento_id = d.departamento_id
        WHERE fpi.farm_id = f.id AND fpi.pais_id IS NULL;
        
        -- Hacer NOT NULL después de actualizar
        ALTER TABLE farm_product_inventory
        ALTER COLUMN pais_id SET NOT NULL;
        
        -- Agregar foreign key
        ALTER TABLE farm_product_inventory
        ADD CONSTRAINT fk_farm_product_inventory_pais_id 
        FOREIGN KEY (pais_id) REFERENCES paises(pais_id) ON DELETE CASCADE;
        
        RAISE NOTICE 'Columna pais_id agregada a la tabla farm_product_inventory.';
    ELSE
        RAISE NOTICE 'Columna pais_id ya existe en la tabla farm_product_inventory.';
    END IF;

    -- 2. Agregar company_id y pais_id a farm_inventory_movements
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'company_id'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN company_id INTEGER NULL;
        
        -- Actualizar registros existentes con company_id desde farms
        UPDATE farm_inventory_movements fim
        SET company_id = f.company_id
        FROM farms f
        WHERE fim.farm_id = f.id AND fim.company_id IS NULL;
        
        -- Hacer NOT NULL después de actualizar
        ALTER TABLE farm_inventory_movements
        ALTER COLUMN company_id SET NOT NULL;
        
        -- Agregar foreign key
        ALTER TABLE farm_inventory_movements
        ADD CONSTRAINT fk_farm_inventory_movements_company_id 
        FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE;
        
        RAISE NOTICE 'Columna company_id agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna company_id ya existe en la tabla farm_inventory_movements.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'pais_id'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN pais_id INTEGER NULL;
        
        -- Actualizar registros existentes con pais_id desde farms a través de departamentos
        UPDATE farm_inventory_movements fim
        SET pais_id = d.pais_id
        FROM farms f
        JOIN departamentos d ON f.departamento_id = d.departamento_id
        WHERE fim.farm_id = f.id AND fim.pais_id IS NULL;
        
        -- Hacer NOT NULL después de actualizar
        ALTER TABLE farm_inventory_movements
        ALTER COLUMN pais_id SET NOT NULL;
        
        -- Agregar foreign key
        ALTER TABLE farm_inventory_movements
        ADD CONSTRAINT fk_farm_inventory_movements_pais_id 
        FOREIGN KEY (pais_id) REFERENCES paises(pais_id) ON DELETE CASCADE;
        
        RAISE NOTICE 'Columna pais_id agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna pais_id ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- 3. Crear índices para mejorar rendimiento
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = 'public'
        AND tablename = 'farm_product_inventory' 
        AND indexname = 'ix_farm_product_inventory_company_id'
    ) THEN
        CREATE INDEX ix_farm_product_inventory_company_id 
        ON farm_product_inventory (company_id);
        RAISE NOTICE 'Índice ix_farm_product_inventory_company_id creado.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = 'public'
        AND tablename = 'farm_product_inventory' 
        AND indexname = 'ix_farm_product_inventory_pais_id'
    ) THEN
        CREATE INDEX ix_farm_product_inventory_pais_id 
        ON farm_product_inventory (pais_id);
        RAISE NOTICE 'Índice ix_farm_product_inventory_pais_id creado.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = 'public'
        AND tablename = 'farm_inventory_movements' 
        AND indexname = 'ix_farm_inventory_movements_company_id'
    ) THEN
        CREATE INDEX ix_farm_inventory_movements_company_id 
        ON farm_inventory_movements (company_id);
        RAISE NOTICE 'Índice ix_farm_inventory_movements_company_id creado.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = 'public'
        AND tablename = 'farm_inventory_movements' 
        AND indexname = 'ix_farm_inventory_movements_pais_id'
    ) THEN
        CREATE INDEX ix_farm_inventory_movements_pais_id 
        ON farm_inventory_movements (pais_id);
        RAISE NOTICE 'Índice ix_farm_inventory_movements_pais_id creado.';
    END IF;
END $$;

COMMENT ON COLUMN farm_product_inventory.company_id IS 'ID de la empresa asociada al inventario';
COMMENT ON COLUMN farm_product_inventory.pais_id IS 'ID del país asociado al inventario';
COMMENT ON COLUMN farm_inventory_movements.company_id IS 'ID de la empresa asociada al movimiento';
COMMENT ON COLUMN farm_inventory_movements.pais_id IS 'ID del país asociado al movimiento';

COMMIT;
