-- Agregar columna metadata JSONB a la tabla produccion_diaria
-- Esta columna almacena campos adicionales como consumo original con unidad, tipo de ítem, etc.

DO $$
BEGIN
    -- Agregar columna metadata si no existe
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' 
        AND table_name = 'produccion_diaria' 
        AND column_name = 'metadata'
    ) THEN
        ALTER TABLE public.produccion_diaria 
        ADD COLUMN metadata JSONB NULL;
        RAISE NOTICE 'Columna metadata agregada a la tabla produccion_diaria.';
    ELSE
        RAISE NOTICE 'Columna metadata ya existe en la tabla produccion_diaria.';
    END IF;

    -- Crear índice GIN para búsquedas eficientes en JSONB (opcional pero recomendado)
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = 'public'
        AND tablename = 'produccion_diaria' 
        AND indexname = 'idx_produccion_diaria_metadata_gin'
    ) THEN
        CREATE INDEX idx_produccion_diaria_metadata_gin 
        ON public.produccion_diaria USING GIN (metadata);
        RAISE NOTICE 'Índice GIN para metadata creado en produccion_diaria.';
    ELSE
        RAISE NOTICE 'Índice GIN para metadata ya existe en produccion_diaria.';
    END IF;

    -- Comentario en la columna
    COMMENT ON COLUMN public.produccion_diaria.metadata IS 
    'Campos adicionales almacenados en formato JSONB: consumoOriginalHembras, unidadConsumoOriginalHembras, consumoOriginalMachos, unidadConsumoOriginalMachos, tipoItemHembras, tipoItemMachos, tipoAlimentoHembras, tipoAlimentoMachos';

END $$;
