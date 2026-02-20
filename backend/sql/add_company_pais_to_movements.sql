-- =====================================================
-- Script para agregar company_id y pais_id a farm_inventory_movements
-- =====================================================
-- Estos campos se obtienen automáticamente desde la granja en el backend
-- pero deben existir en la tabla para poder guardarlos
-- =====================================================

-- Agregar company_id
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'company_id'
    ) THEN
        -- Agregar columna como NULL inicialmente
        ALTER TABLE farm_inventory_movements
        ADD COLUMN company_id INTEGER NULL;
        
        RAISE NOTICE 'Columna company_id agregada.';
        
        -- Actualizar registros existentes con company_id desde farms
        UPDATE farm_inventory_movements fim
        SET company_id = f.company_id
        FROM farms f
        WHERE fim.farm_id = f.id 
          AND fim.company_id IS NULL;
        
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
        ON farm_inventory_movements(company_id);
        
        RAISE NOTICE 'Índice ix_farm_inventory_movements_company_id creado.';
        
        COMMENT ON COLUMN farm_inventory_movements.company_id IS 
            'ID de la empresa asociada al movimiento (obtenido automáticamente de la granja)';
    ELSE
        RAISE NOTICE 'Columna company_id ya existe. Verificando datos...';
        
        -- Verificar y actualizar registros que puedan tener company_id NULL o 0
        UPDATE farm_inventory_movements fim
        SET company_id = f.company_id
        FROM farms f
        WHERE fim.farm_id = f.id 
          AND (fim.company_id IS NULL OR fim.company_id = 0);
        
        RAISE NOTICE 'Verificación de company_id completada.';
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'Error al procesar company_id: %', SQLERRM;
END $$;

-- Agregar pais_id
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'pais_id'
    ) THEN
        -- Agregar columna como NULL inicialmente
        ALTER TABLE farm_inventory_movements
        ADD COLUMN pais_id INTEGER NULL;
        
        RAISE NOTICE 'Columna pais_id agregada.';
        
        -- Actualizar registros existentes con pais_id desde farms a través de departamentos
        UPDATE farm_inventory_movements fim
        SET pais_id = d.pais_id
        FROM farms f
        INNER JOIN departamentos d ON f.departamento_id = d.departamento_id
        WHERE fim.farm_id = f.id 
          AND fim.pais_id IS NULL;
        
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
        ON farm_inventory_movements(pais_id);
        
        RAISE NOTICE 'Índice ix_farm_inventory_movements_pais_id creado.';
        
        COMMENT ON COLUMN farm_inventory_movements.pais_id IS 
            'ID del país asociado al movimiento (obtenido automáticamente de la granja a través del departamento)';
    ELSE
        RAISE NOTICE 'Columna pais_id ya existe. Verificando datos...';
        
        -- Verificar y actualizar registros que puedan tener pais_id NULL o 0
        UPDATE farm_inventory_movements fim
        SET pais_id = d.pais_id
        FROM farms f
        INNER JOIN departamentos d ON f.departamento_id = d.departamento_id
        WHERE fim.farm_id = f.id 
          AND (fim.pais_id IS NULL OR fim.pais_id = 0);
        
        RAISE NOTICE 'Verificación de pais_id completada.';
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'Error al procesar pais_id: %', SQLERRM;
END $$;

-- =====================================================
-- RESUMEN: Verificar que las columnas existan
-- =====================================================
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_schema = 'public' 
  AND table_name = 'farm_inventory_movements'
  AND column_name IN ('company_id', 'pais_id')
ORDER BY column_name;

-- Verificar foreign keys
SELECT 
    tc.constraint_name,
    tc.table_name,
    kcu.column_name,
    ccu.table_name AS foreign_table_name,
    ccu.column_name AS foreign_column_name
FROM information_schema.table_constraints AS tc
JOIN information_schema.key_column_usage AS kcu
    ON tc.constraint_name = kcu.constraint_name
    AND tc.table_schema = kcu.table_schema
JOIN information_schema.constraint_column_usage AS ccu
    ON ccu.constraint_name = tc.constraint_name
    AND ccu.table_schema = tc.table_schema
WHERE tc.constraint_type = 'FOREIGN KEY'
    AND tc.table_name = 'farm_inventory_movements'
    AND (kcu.column_name = 'company_id' OR kcu.column_name = 'pais_id')
ORDER BY tc.constraint_name;

-- Verificar índices
SELECT 
    indexname,
    indexdef
FROM pg_indexes
WHERE schemaname = 'public' 
  AND tablename = 'farm_inventory_movements'
  AND (indexname LIKE '%company%' OR indexname LIKE '%pais%')
ORDER BY indexname;
