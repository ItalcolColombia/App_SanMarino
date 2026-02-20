-- =====================================================
-- Script SEGURO para agregar campos de empresa y país a las tablas de inventario
-- ZooSanMarino - PostgreSQL
-- =====================================================
-- Este script maneja errores de forma independiente para cada operación
-- y puede ejecutarse incluso si hay transacciones abortadas previas
-- =====================================================

-- Primero, asegurarse de que no hay transacciones abortadas
DO $$
BEGIN
    -- Intentar hacer rollback si hay una transacción abortada
    EXCEPTION WHEN OTHERS THEN
        NULL; -- Ignorar errores aquí
END $$;

-- ============================================
-- 1. Agregar company_id a farm_product_inventory
-- ============================================
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_product_inventory' 
        AND column_name = 'company_id'
    ) THEN
        ALTER TABLE farm_product_inventory
        ADD COLUMN company_id INTEGER NULL;
        
        RAISE NOTICE 'Columna company_id agregada a farm_product_inventory.';
        
        -- Actualizar registros existentes con company_id desde farms
        UPDATE farm_product_inventory fpi
        SET company_id = f.company_id
        FROM farms f
        WHERE fpi.farm_id = f.id AND fpi.company_id IS NULL;
        
        RAISE NOTICE 'Registros existentes actualizados con company_id.';
        
        -- Hacer NOT NULL después de actualizar
        ALTER TABLE farm_product_inventory
        ALTER COLUMN company_id SET NOT NULL;
        
        RAISE NOTICE 'Columna company_id configurada como NOT NULL.';
        
        -- Agregar foreign key
        BEGIN
            ALTER TABLE farm_product_inventory
            ADD CONSTRAINT fk_farm_product_inventory_company_id 
            FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE;
            RAISE NOTICE 'Foreign key fk_farm_product_inventory_company_id agregada.';
        EXCEPTION WHEN duplicate_object THEN
            RAISE NOTICE 'La constraint fk_farm_product_inventory_company_id ya existe.';
        END;
        
        -- Agregar índice
        CREATE INDEX IF NOT EXISTS ix_farm_product_inventory_company_id 
        ON farm_product_inventory (company_id);
        
        RAISE NOTICE 'Índice ix_farm_product_inventory_company_id creado.';
    ELSE
        RAISE NOTICE 'Columna company_id ya existe en farm_product_inventory.';
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'Error al procesar company_id en farm_product_inventory: %', SQLERRM;
END $$;

-- ============================================
-- 2. Agregar pais_id a farm_product_inventory
-- ============================================
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_product_inventory' 
        AND column_name = 'pais_id'
    ) THEN
        ALTER TABLE farm_product_inventory
        ADD COLUMN pais_id INTEGER NULL;
        
        RAISE NOTICE 'Columna pais_id agregada a farm_product_inventory.';
        
        -- Actualizar registros existentes con pais_id desde farms a través de departamentos
        UPDATE farm_product_inventory fpi
        SET pais_id = d.pais_id
        FROM farms f
        JOIN departamentos d ON f.departamento_id = d.departamento_id
        WHERE fpi.farm_id = f.id AND fpi.pais_id IS NULL;
        
        RAISE NOTICE 'Registros existentes actualizados con pais_id.';
        
        -- Hacer NOT NULL después de actualizar
        ALTER TABLE farm_product_inventory
        ALTER COLUMN pais_id SET NOT NULL;
        
        RAISE NOTICE 'Columna pais_id configurada como NOT NULL.';
        
        -- Agregar foreign key
        BEGIN
            ALTER TABLE farm_product_inventory
            ADD CONSTRAINT fk_farm_product_inventory_pais_id 
            FOREIGN KEY (pais_id) REFERENCES paises(pais_id) ON DELETE CASCADE;
            RAISE NOTICE 'Foreign key fk_farm_product_inventory_pais_id agregada.';
        EXCEPTION WHEN duplicate_object THEN
            RAISE NOTICE 'La constraint fk_farm_product_inventory_pais_id ya existe.';
        END;
        
        -- Agregar índice
        CREATE INDEX IF NOT EXISTS ix_farm_product_inventory_pais_id 
        ON farm_product_inventory (pais_id);
        
        RAISE NOTICE 'Índice ix_farm_product_inventory_pais_id creado.';
    ELSE
        RAISE NOTICE 'Columna pais_id ya existe en farm_product_inventory.';
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'Error al procesar pais_id en farm_product_inventory: %', SQLERRM;
END $$;

-- ============================================
-- 3. Agregar company_id a farm_inventory_movements
-- ============================================
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'company_id'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN company_id INTEGER NULL;
        
        RAISE NOTICE 'Columna company_id agregada a farm_inventory_movements.';
        
        -- Actualizar registros existentes con company_id desde farms
        UPDATE farm_inventory_movements fim
        SET company_id = f.company_id
        FROM farms f
        WHERE fim.farm_id = f.id AND fim.company_id IS NULL;
        
        RAISE NOTICE 'Registros existentes actualizados con company_id.';
        
        -- Hacer NOT NULL después de actualizar
        ALTER TABLE farm_inventory_movements
        ALTER COLUMN company_id SET NOT NULL;
        
        RAISE NOTICE 'Columna company_id configurada como NOT NULL.';
        
        -- Agregar foreign key
        BEGIN
            ALTER TABLE farm_inventory_movements
            ADD CONSTRAINT fk_farm_inventory_movements_company_id 
            FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE;
            RAISE NOTICE 'Foreign key fk_farm_inventory_movements_company_id agregada.';
        EXCEPTION WHEN duplicate_object THEN
            RAISE NOTICE 'La constraint fk_farm_inventory_movements_company_id ya existe.';
        END;
        
        -- Agregar índice
        CREATE INDEX IF NOT EXISTS ix_farm_inventory_movements_company_id 
        ON farm_inventory_movements (company_id);
        
        RAISE NOTICE 'Índice ix_farm_inventory_movements_company_id creado.';
    ELSE
        RAISE NOTICE 'Columna company_id ya existe en farm_inventory_movements.';
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'Error al procesar company_id en farm_inventory_movements: %', SQLERRM;
END $$;

-- ============================================
-- 4. Agregar pais_id a farm_inventory_movements
-- ============================================
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'pais_id'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN pais_id INTEGER NULL;
        
        RAISE NOTICE 'Columna pais_id agregada a farm_inventory_movements.';
        
        -- Actualizar registros existentes con pais_id desde farms a través de departamentos
        UPDATE farm_inventory_movements fim
        SET pais_id = d.pais_id
        FROM farms f
        JOIN departamentos d ON f.departamento_id = d.departamento_id
        WHERE fim.farm_id = f.id AND fim.pais_id IS NULL;
        
        RAISE NOTICE 'Registros existentes actualizados con pais_id.';
        
        -- Hacer NOT NULL después de actualizar
        ALTER TABLE farm_inventory_movements
        ALTER COLUMN pais_id SET NOT NULL;
        
        RAISE NOTICE 'Columna pais_id configurada como NOT NULL.';
        
        -- Agregar foreign key
        BEGIN
            ALTER TABLE farm_inventory_movements
            ADD CONSTRAINT fk_farm_inventory_movements_pais_id 
            FOREIGN KEY (pais_id) REFERENCES paises(pais_id) ON DELETE CASCADE;
            RAISE NOTICE 'Foreign key fk_farm_inventory_movements_pais_id agregada.';
        EXCEPTION WHEN duplicate_object THEN
            RAISE NOTICE 'La constraint fk_farm_inventory_movements_pais_id ya existe.';
        END;
        
        -- Agregar índice
        CREATE INDEX IF NOT EXISTS ix_farm_inventory_movements_pais_id 
        ON farm_inventory_movements (pais_id);
        
        RAISE NOTICE 'Índice ix_farm_inventory_movements_pais_id creado.';
    ELSE
        RAISE NOTICE 'Columna pais_id ya existe en farm_inventory_movements.';
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'Error al procesar pais_id en farm_inventory_movements: %', SQLERRM;
END $$;

-- ============================================
-- RESUMEN: Verificar que las columnas existan
-- ============================================
SELECT 
    'farm_product_inventory' as tabla,
    column_name, 
    data_type, 
    is_nullable
FROM information_schema.columns
WHERE table_schema = 'public' 
  AND table_name = 'farm_product_inventory'
  AND column_name IN ('company_id', 'pais_id')
ORDER BY column_name;

SELECT 
    'farm_inventory_movements' as tabla,
    column_name, 
    data_type, 
    is_nullable
FROM information_schema.columns
WHERE table_schema = 'public' 
  AND table_name = 'farm_inventory_movements'
  AND column_name IN ('company_id', 'pais_id')
ORDER BY column_name;
