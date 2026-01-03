-- Agregar columnas para guardar el consumo original con su unidad
-- Esto permite mostrar/editarlo correctamente cuando se edita un registro

DO $$
BEGIN
    -- Agregar consumo original hembras
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'seguimiento_lote_levante' AND column_name = 'consumo_original_hembras'
    ) THEN
        ALTER TABLE public.seguimiento_lote_levante 
        ADD COLUMN consumo_original_hembras DOUBLE PRECISION NULL;
        RAISE NOTICE 'Columna consumo_original_hembras agregada.';
    ELSE
        RAISE NOTICE 'Columna consumo_original_hembras ya existe.';
    END IF;

    -- Agregar unidad consumo original hembras
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'seguimiento_lote_levante' AND column_name = 'unidad_consumo_original_hembras'
    ) THEN
        ALTER TABLE public.seguimiento_lote_levante 
        ADD COLUMN unidad_consumo_original_hembras VARCHAR(10) NULL;
        RAISE NOTICE 'Columna unidad_consumo_original_hembras agregada.';
    ELSE
        RAISE NOTICE 'Columna unidad_consumo_original_hembras ya existe.';
    END IF;

    -- Agregar consumo original machos
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'seguimiento_lote_levante' AND column_name = 'consumo_original_machos'
    ) THEN
        ALTER TABLE public.seguimiento_lote_levante 
        ADD COLUMN consumo_original_machos DOUBLE PRECISION NULL;
        RAISE NOTICE 'Columna consumo_original_machos agregada.';
    ELSE
        RAISE NOTICE 'Columna consumo_original_machos ya existe.';
    END IF;

    -- Agregar unidad consumo original machos
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'seguimiento_lote_levante' AND column_name = 'unidad_consumo_original_machos'
    ) THEN
        ALTER TABLE public.seguimiento_lote_levante 
        ADD COLUMN unidad_consumo_original_machos VARCHAR(10) NULL;
        RAISE NOTICE 'Columna unidad_consumo_original_machos agregada.';
    ELSE
        RAISE NOTICE 'Columna unidad_consumo_original_machos ya existe.';
    END IF;

END $$;










