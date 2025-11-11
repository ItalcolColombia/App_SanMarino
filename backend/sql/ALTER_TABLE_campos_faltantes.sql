-- ============================================================
-- COMANDOS ALTER TABLE PARA AGREGAR CAMPOS FALTANTES
-- Ejecutar estos comandos uno por uno o todos juntos
-- ============================================================

-- 1. Agregar campo sel_h (Selección hembras retiradas)
ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS sel_h INTEGER NOT NULL DEFAULT 0;

-- 2. Agregar campo cons_kg_h (Consumo alimento hembras en kg)
ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS cons_kg_h DOUBLE PRECISION NOT NULL DEFAULT 0;

-- 3. Agregar campo cons_kg_m (Consumo alimento machos en kg)
ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS cons_kg_m DOUBLE PRECISION NOT NULL DEFAULT 0;

-- 4. Agregar campo tipo_alimento (Tipo de alimento utilizado)
ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS tipo_alimento TEXT NOT NULL DEFAULT 'Standard';

-- 5. Agregar campo peso_huevo (Peso promedio del huevo en gramos)
ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS peso_huevo DOUBLE PRECISION NOT NULL DEFAULT 0;

-- 6. Agregar campo etapa (Etapa de producción: 1, 2, 3)
ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS etapa INTEGER NOT NULL DEFAULT 1;

-- ============================================================
-- ACTUALIZAR VALORES EN REGISTROS EXISTENTES (si los hay)
-- ============================================================

-- Asegurar que tipo_alimento tenga valor por defecto
UPDATE produccion_diaria 
SET tipo_alimento = 'Standard' 
WHERE tipo_alimento IS NULL OR tipo_alimento = '';

-- Asegurar que etapa tenga valor válido (1, 2, o 3)
UPDATE produccion_diaria 
SET etapa = 1 
WHERE etapa IS NULL OR etapa < 1 OR etapa > 3;

-- ============================================================
-- VERIFICACIÓN FINAL
-- ============================================================

SELECT 
    column_name AS campo,
    data_type AS tipo,
    is_nullable AS permite_null,
    column_default AS valor_defecto
FROM information_schema.columns 
WHERE table_name = 'produccion_diaria' 
    AND column_name IN ('sel_h', 'cons_kg_h', 'cons_kg_m', 'tipo_alimento', 'peso_huevo', 'etapa')
ORDER BY column_name;


