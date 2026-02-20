-- =====================================================
-- Script consolidado para verificar y agregar TODOS los campos necesarios
-- en farm_inventory_movements
-- =====================================================
-- Este script verifica y agrega:
-- 1. company_id y pais_id (empresa y país desde la granja)
-- 2. item_type (tipo de item del catálogo)
-- 3. origin, destination, reference, reason (campos de movimiento)
-- 4. Todos los campos específicos de movimiento de alimento
-- =====================================================

DO $$
BEGIN
    -- ============================================
    -- 1. CAMPOS DE EMPRESA Y PAÍS
    -- ============================================
    
    -- company_id
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
        
        -- Verificar y actualizar registros que puedan tener company_id NULL o 0
        UPDATE farm_inventory_movements fim
        SET company_id = f.company_id
        FROM farms f
        WHERE fim.farm_id = f.id 
          AND (fim.company_id IS NULL OR fim.company_id = 0);
    END IF;

    -- pais_id
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
        
        -- Verificar y actualizar registros que puedan tener pais_id NULL o 0
        UPDATE farm_inventory_movements fim
        SET pais_id = d.pais_id
        FROM farms f
        INNER JOIN departamentos d ON f.departamento_id = d.departamento_id
        WHERE fim.farm_id = f.id 
          AND (fim.pais_id IS NULL OR fim.pais_id = 0);
    END IF;

    -- ============================================
    -- 2. CAMPO ITEM_TYPE (tipo de item del catálogo)
    -- ============================================
    
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
        
        -- Agregar índice
        CREATE INDEX IF NOT EXISTS ix_farm_inventory_movements_item_type 
        ON farm_inventory_movements(item_type);
        
        COMMENT ON COLUMN farm_inventory_movements.item_type IS 
            'Tipo de item del catálogo: alimento, vacuna, medicamento, accesorio, biologico, consumible, otro';
        
        RAISE NOTICE 'Columna item_type agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna item_type ya existe en la tabla farm_inventory_movements.';
        
        -- Actualizar registros que puedan tener item_type NULL
        UPDATE farm_inventory_movements fim
        SET item_type = ci.item_type
        FROM catalogo_items ci
        WHERE fim.catalog_item_id = ci.id 
          AND fim.item_type IS NULL;
    END IF;

    -- ============================================
    -- 3. CAMPOS DE MOVIMIENTO (origin, destination, reference, reason)
    -- ============================================
    
    -- origin
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'origin'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN origin VARCHAR(100);
        
        COMMENT ON COLUMN farm_inventory_movements.origin IS 
            'Origen para entradas (ej: "Planta Sanmarino", "Planta Itacol")';
        
        RAISE NOTICE 'Columna origin agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna origin ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- destination
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'destination'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN destination VARCHAR(100);
        
        COMMENT ON COLUMN farm_inventory_movements.destination IS 
            'Destino para salidas (ej: "Venta", "Movimiento", "Devolución")';
        
        RAISE NOTICE 'Columna destination agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna destination ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- reference (ya debería existir, pero verificamos)
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'reference'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN reference VARCHAR(50);
        
        COMMENT ON COLUMN farm_inventory_movements.reference IS 
            'Referencia del movimiento (número de documento, factura, etc.)';
        
        RAISE NOTICE 'Columna reference agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna reference ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- reason (ya debería existir, pero verificamos)
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'reason'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN reason VARCHAR(200);
        
        COMMENT ON COLUMN farm_inventory_movements.reason IS 
            'Motivo del movimiento';
        
        RAISE NOTICE 'Columna reason agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna reason ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- ============================================
    -- 4. CAMPOS ESPECÍFICOS DE MOVIMIENTO DE ALIMENTO
    -- ============================================
    
    -- documento_origen
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'documento_origen'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN documento_origen VARCHAR(50);
        
        CREATE INDEX IF NOT EXISTS ix_farm_inventory_movements_documento_origen 
        ON farm_inventory_movements(documento_origen);
        
        COMMENT ON COLUMN farm_inventory_movements.documento_origen IS 
            'Tipo de documento origen: Autoconsumo (autofacturado), Remisión facturada (RVN), Entrada de inventario (EAN)';
        
        RAISE NOTICE 'Columna documento_origen agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna documento_origen ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- tipo_entrada
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'tipo_entrada'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN tipo_entrada VARCHAR(50);
        
        CREATE INDEX IF NOT EXISTS ix_farm_inventory_movements_tipo_entrada 
        ON farm_inventory_movements(tipo_entrada);
        
        COMMENT ON COLUMN farm_inventory_movements.tipo_entrada IS 
            'Tipo de entrada: Entrada Nueva, Traslado entre galpon, Traslados entre granjas';
        
        RAISE NOTICE 'Columna tipo_entrada agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna tipo_entrada ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- galpon_destino_id
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'galpon_destino_id'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN galpon_destino_id VARCHAR(50);
        
        COMMENT ON COLUMN farm_inventory_movements.galpon_destino_id IS 
            'ID del galpón destino para movimientos a galpones específicos';
        
        RAISE NOTICE 'Columna galpon_destino_id agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna galpon_destino_id ya existe en la tabla farm_inventory_movements.';
    END IF;

    -- fecha_movimiento
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'farm_inventory_movements' 
        AND column_name = 'fecha_movimiento'
    ) THEN
        ALTER TABLE farm_inventory_movements
        ADD COLUMN fecha_movimiento TIMESTAMPTZ;
        
        CREATE INDEX IF NOT EXISTS ix_farm_inventory_movements_fecha_movimiento 
        ON farm_inventory_movements(fecha_movimiento);
        
        COMMENT ON COLUMN farm_inventory_movements.fecha_movimiento IS 
            'Fecha del movimiento (puede ser diferente a created_at para movimientos retroactivos)';
        
        RAISE NOTICE 'Columna fecha_movimiento agregada a la tabla farm_inventory_movements.';
    ELSE
        RAISE NOTICE 'Columna fecha_movimiento ya existe en la tabla farm_inventory_movements.';
    END IF;

    RAISE NOTICE 'Verificación y creación de campos completada.';
EXCEPTION
    WHEN OTHERS THEN
        RAISE NOTICE 'Error durante la ejecución: %', SQLERRM;
        RAISE;
END $$;

-- ============================================
-- RESUMEN: Mostrar todas las columnas importantes
-- ============================================
SELECT 
    column_name, 
    data_type, 
    character_maximum_length,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_schema = 'public' 
  AND table_name = 'farm_inventory_movements'
  AND column_name IN (
    'farm_id', 
    'catalog_item_id', 
    'item_type',
    'company_id', 
    'pais_id',
    'quantity',
    'unit',
    'reference',
    'reason',
    'origin',
    'destination',
    'documento_origen',
    'tipo_entrada',
    'galpon_destino_id',
    'fecha_movimiento'
  )
ORDER BY 
    CASE column_name
        WHEN 'farm_id' THEN 1
        WHEN 'catalog_item_id' THEN 2
        WHEN 'item_type' THEN 3
        WHEN 'company_id' THEN 4
        WHEN 'pais_id' THEN 5
        WHEN 'quantity' THEN 6
        WHEN 'unit' THEN 7
        WHEN 'reference' THEN 8
        WHEN 'reason' THEN 9
        WHEN 'origin' THEN 10
        WHEN 'destination' THEN 11
        ELSE 12
    END;

-- Verificar índices relacionados
SELECT 
    indexname,
    indexdef
FROM pg_indexes
WHERE schemaname = 'public' 
  AND tablename = 'farm_inventory_movements'
  AND (indexname LIKE '%company%' 
       OR indexname LIKE '%pais%' 
       OR indexname LIKE '%item_type%'
       OR indexname LIKE '%farm%'
       OR indexname LIKE '%catalog%')
ORDER BY indexname;
