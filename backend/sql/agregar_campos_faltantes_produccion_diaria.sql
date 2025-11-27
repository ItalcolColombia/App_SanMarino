-- ============================================================
-- SCRIPT PARA AGREGAR CAMPOS FALTANTES EN produccion_diaria
-- ============================================================
-- Este script agrega los campos que faltan según los requerimientos
-- ============================================================

-- 1. Verificar qué campos faltan y generar los ALTER TABLE necesarios
DO $$
DECLARE
    campo_existe BOOLEAN;
BEGIN
    -- Agregar campo sel_h (Selección hembras retiradas)
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'produccion_diaria' 
        AND column_name = 'sel_h'
    ) INTO campo_existe;
    
    IF NOT campo_existe THEN
        ALTER TABLE produccion_diaria 
        ADD COLUMN sel_h INTEGER NOT NULL DEFAULT 0;
        RAISE NOTICE '✓ Campo sel_h agregado';
    ELSE
        RAISE NOTICE '→ Campo sel_h ya existe';
    END IF;
    
    -- Agregar campo cons_kg_h (Consumo alimento hembras)
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'produccion_diaria' 
        AND column_name = 'cons_kg_h'
    ) INTO campo_existe;
    
    IF NOT campo_existe THEN
        ALTER TABLE produccion_diaria 
        ADD COLUMN cons_kg_h DOUBLE PRECISION NOT NULL DEFAULT 0;
        RAISE NOTICE '✓ Campo cons_kg_h agregado';
    ELSE
        RAISE NOTICE '→ Campo cons_kg_h ya existe';
    END IF;
    
    -- Agregar campo cons_kg_m (Consumo alimento machos)
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'produccion_diaria' 
        AND column_name = 'cons_kg_m'
    ) INTO campo_existe;
    
    IF NOT campo_existe THEN
        ALTER TABLE produccion_diaria 
        ADD COLUMN cons_kg_m DOUBLE PRECISION NOT NULL DEFAULT 0;
        RAISE NOTICE '✓ Campo cons_kg_m agregado';
    ELSE
        RAISE NOTICE '→ Campo cons_kg_m ya existe';
    END IF;
    
    -- Agregar campo tipo_alimento
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'produccion_diaria' 
        AND column_name = 'tipo_alimento'
    ) INTO campo_existe;
    
    IF NOT campo_existe THEN
        ALTER TABLE produccion_diaria 
        ADD COLUMN tipo_alimento TEXT NOT NULL DEFAULT 'Standard';
        RAISE NOTICE '✓ Campo tipo_alimento agregado';
    ELSE
        RAISE NOTICE '→ Campo tipo_alimento ya existe';
    END IF;
    
    -- Agregar campo peso_huevo (si falta)
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'produccion_diaria' 
        AND column_name = 'peso_huevo'
    ) INTO campo_existe;
    
    IF NOT campo_existe THEN
        ALTER TABLE produccion_diaria 
        ADD COLUMN peso_huevo DOUBLE PRECISION NOT NULL DEFAULT 0;
        RAISE NOTICE '✓ Campo peso_huevo agregado';
    ELSE
        RAISE NOTICE '→ Campo peso_huevo ya existe';
    END IF;
    
    -- Agregar campo etapa
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'produccion_diaria' 
        AND column_name = 'etapa'
    ) INTO campo_existe;
    
    IF NOT campo_existe THEN
        ALTER TABLE produccion_diaria 
        ADD COLUMN etapa INTEGER NOT NULL DEFAULT 1;
        RAISE NOTICE '✓ Campo etapa agregado';
    ELSE
        RAISE NOTICE '→ Campo etapa ya existe';
    END IF;
    
    -- Actualizar valores por defecto en registros existentes (si aplica)
    UPDATE produccion_diaria 
    SET tipo_alimento = 'Standard' 
    WHERE tipo_alimento IS NULL OR tipo_alimento = '';
    
    UPDATE produccion_diaria 
    SET etapa = 1 
    WHERE etapa IS NULL OR (etapa < 1 OR etapa > 3);
    
    RAISE NOTICE '✓ Valores por defecto actualizados en registros existentes';
    
END $$;

-- 2. Verificar resultado final
SELECT 
    '=== CAMPOS DESPUÉS DE LA ACTUALIZACIÓN ===' AS resultado;

SELECT 
    column_name AS campo,
    data_type AS tipo,
    is_nullable AS permite_null,
    column_default AS valor_defecto
FROM information_schema.columns 
WHERE table_name = 'produccion_diaria' 
    AND column_name IN (
        'sel_h', 
        'cons_kg_h', 
        'cons_kg_m', 
        'tipo_alimento', 
        'peso_huevo', 
        'etapa'
    )
ORDER BY column_name;

-- 3. Agregar comentarios para documentación
COMMENT ON COLUMN produccion_diaria.sel_h IS 'Selección de hembras (retiradas)';
COMMENT ON COLUMN produccion_diaria.cons_kg_h IS 'Consumo de alimento hembras (kg)';
COMMENT ON COLUMN produccion_diaria.cons_kg_m IS 'Consumo de alimento machos (kg)';
COMMENT ON COLUMN produccion_diaria.tipo_alimento IS 'Tipo de alimento: Standard, Premium, Inicio, Postura, Final';
COMMENT ON COLUMN produccion_diaria.peso_huevo IS 'Peso promedio del huevo (gramos)';
COMMENT ON COLUMN produccion_diaria.etapa IS 'Etapa de producción: 1 (Semana 25-33), 2 (34-50), 3 (>50)';

-- ============================================================
-- COMANDOS ALTER TABLE INDIVIDUALES (si prefieres ejecutarlos uno por uno):
-- ============================================================

/*
-- Agregar campo sel_h
ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS sel_h INTEGER NOT NULL DEFAULT 0;

-- Agregar campo cons_kg_h
ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS cons_kg_h DOUBLE PRECISION NOT NULL DEFAULT 0;

-- Agregar campo cons_kg_m
ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS cons_kg_m DOUBLE PRECISION NOT NULL DEFAULT 0;

-- Agregar campo tipo_alimento
ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS tipo_alimento TEXT NOT NULL DEFAULT 'Standard';

-- Agregar campo peso_huevo
ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS peso_huevo DOUBLE PRECISION NOT NULL DEFAULT 0;

-- Agregar campo etapa
ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS etapa INTEGER NOT NULL DEFAULT 1;
*/





