-- Agregar columna metadata JSONB para campos adicionales/extras
-- Esto permite almacenar información adicional sin afectar la estructura base
-- que otros servicios utilizan

DO $$
BEGIN
    -- Agregar columna metadata si no existe
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'seguimiento_lote_levante' AND column_name = 'metadata'
    ) THEN
        ALTER TABLE public.seguimiento_lote_levante 
        ADD COLUMN metadata JSONB NULL;
        RAISE NOTICE 'Columna metadata agregada a la tabla seguimiento_lote_levante.';
    ELSE
        RAISE NOTICE 'Columna metadata ya existe en la tabla seguimiento_lote_levante.';
    END IF;

    -- Crear índice GIN para búsquedas eficientes en JSONB (opcional pero recomendado)
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE tablename = 'seguimiento_lote_levante' AND indexname = 'ix_seguimiento_lote_levante_metadata'
    ) THEN
        CREATE INDEX ix_seguimiento_lote_levante_metadata 
        ON public.seguimiento_lote_levante USING GIN (metadata);
        RAISE NOTICE 'Índice GIN para metadata creado.';
    ELSE
        RAISE NOTICE 'Índice GIN para metadata ya existe.';
    END IF;

END $$;

