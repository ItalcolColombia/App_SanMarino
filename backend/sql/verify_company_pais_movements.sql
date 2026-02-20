-- =====================================================
-- Script para verificar y agregar campos company_id y pais_id a farm_inventory_movements
-- Verifica que estos campos existan y se guarden correctamente
-- =====================================================

BEGIN;

DO $$
BEGIN
    -- 1. Verificar y agregar company_id si no existe
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
        
        -- Agregar índice
        CREATE INDEX IF NOT EXISTS ix_farm_inventory_movements_company_id 
        ON farm_inventory_movements(company_id);
        
        COMMENT ON COLUMN farm_inventory_movements.company_id IS 
            'ID de la empresa asociada al movimiento (obtenido de la granja)';
        
        RAISE NOTICE 'Columna company_id agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna company_id ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- 2. Verificar y agregar pais_id si no existe
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
        INNER JOIN departamentos d ON f.departamento_id = d.departamento_id
        WHERE fim.farm_id = f.id AND fim.pais_id IS NULL;
        
        -- Hacer NOT NULL después de actualizar
        ALTER TABLE farm_inventory_movements
        ALTER COLUMN pais_id SET NOT NULL;
        
        -- Agregar foreign key
        ALTER TABLE farm_inventory_movements
        ADD CONSTRAINT fk_farm_inventory_movements_pais_id 
        FOREIGN KEY (pais_id) REFERENCES paises(pais_id) ON DELETE CASCADE;
        
        -- Agregar índice
        CREATE INDEX IF NOT EXISTS ix_farm_inventory_movements_pais_id 
        ON farm_inventory_movements(pais_id);
        
        COMMENT ON COLUMN farm_inventory_movements.pais_id IS 
            'ID del país asociado al movimiento (obtenido de la granja a través del departamento)';
        
        RAISE NOTICE 'Columna pais_id agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna pais_id ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- 3. Verificar que los registros existentes tengan company_id y pais_id
    -- Si hay registros sin estos valores, actualizarlos
    UPDATE farm_inventory_movements fim
    SET company_id = f.company_id
    FROM farms f
    WHERE fim.farm_id = f.id 
      AND (fim.company_id IS NULL OR fim.company_id = 0);
    
    UPDATE farm_inventory_movements fim
    SET pais_id = d.pais_id
    FROM farms f
    INNER JOIN departamentos d ON f.departamento_id = d.departamento_id
    WHERE fim.farm_id = f.id 
      AND (fim.pais_id IS NULL OR fim.pais_id = 0);

    RAISE NOTICE 'Verificación y actualización de company_id y pais_id completada.';
END $$;

COMMIT;

-- Mostrar resumen de columnas relacionadas con empresa y país
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_schema = 'public' 
  AND table_name = 'farm_inventory_movements'
  AND column_name IN ('company_id', 'pais_id', 'farm_id', 'item_type')
ORDER BY column_name;

-- Verificar índices relacionados
SELECT 
    indexname,
    indexdef
FROM pg_indexes
WHERE schemaname = 'public' 
  AND tablename = 'farm_inventory_movements'
  AND (indexname LIKE '%company%' OR indexname LIKE '%pais%' OR indexname LIKE '%item_type%')
ORDER BY indexname;
