-- ============================================================
-- AGREGAR COLUMNA ITEMS_ADICIONALES JSONB AL SEGUIMIENTO DIARIO LEVANTE
-- Para almacenar otros tipos de ítems (vacunas, medicamentos, etc.)
-- sin afectar la estructura actual de alimentos que se usa en reportes
-- ============================================================

BEGIN;

-- Agregar columna items_adicionales si no existe
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'seguimiento_lote_levante' 
        AND column_name = 'items_adicionales'
    ) THEN
        ALTER TABLE seguimiento_lote_levante 
        ADD COLUMN items_adicionales JSONB NULL;
        
        RAISE NOTICE 'Columna items_adicionales agregada a la tabla seguimiento_lote_levante.';
    ELSE
        RAISE NOTICE 'Columna items_adicionales ya existe en la tabla seguimiento_lote_levante.';
    END IF;
END $$;

-- Crear índice GIN para búsquedas eficientes en JSONB (opcional pero recomendado)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = 'public'
        AND tablename = 'seguimiento_lote_levante' 
        AND indexname = 'ix_seguimiento_lote_levante_items_adicionales'
    ) THEN
        CREATE INDEX ix_seguimiento_lote_levante_items_adicionales 
        ON seguimiento_lote_levante USING GIN (items_adicionales);
        
        RAISE NOTICE 'Índice GIN para items_adicionales creado.';
    ELSE
        RAISE NOTICE 'Índice GIN para items_adicionales ya existe.';
    END IF;
END $$;

-- Comentario en la columna
COMMENT ON COLUMN seguimiento_lote_levante.items_adicionales IS 
'JSONB que almacena ítems adicionales (vacunas, medicamentos, accesorios, etc.) que no son alimentos. 
Estructura: {"itemsHembras": [{"tipoItem": "vacuna", "catalogItemId": 123, "cantidad": 100, "unidad": "unidades"}], 
"itemsMachos": [...]}. Los alimentos se mantienen en los campos tradicionales para compatibilidad con reportes.';

COMMIT;
