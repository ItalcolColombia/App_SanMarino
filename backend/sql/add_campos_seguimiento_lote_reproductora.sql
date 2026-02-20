-- ============================================================
-- Script para agregar campos nuevos a lote_seguimientos
-- Similar a seguimiento_lote_levante
-- ============================================================
-- Campos a agregar:
-- - Campos de agua (solo para Ecuador y Panamá)
-- - Campos de peso y uniformidad
-- - Metadata JSONB para campos adicionales
-- - ItemsAdicionales JSONB para ítems no alimentarios
-- - ConsumoKgMachos
-- ============================================================

-- 1. Agregar campos de agua (double precision)
DO $$
BEGIN
    -- Consumo Agua Diario
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'lote_seguimientos' 
        AND column_name = 'consumo_agua_diario'
    ) THEN
        ALTER TABLE public.lote_seguimientos 
        ADD COLUMN consumo_agua_diario double precision;
        RAISE NOTICE 'Columna consumo_agua_diario agregada';
    ELSE
        RAISE NOTICE 'Columna consumo_agua_diario ya existe';
    END IF;

    -- PH del Agua
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'lote_seguimientos' 
        AND column_name = 'consumo_agua_ph'
    ) THEN
        ALTER TABLE public.lote_seguimientos 
        ADD COLUMN consumo_agua_ph double precision;
        RAISE NOTICE 'Columna consumo_agua_ph agregada';
    ELSE
        RAISE NOTICE 'Columna consumo_agua_ph ya existe';
    END IF;

    -- ORP del Agua
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'lote_seguimientos' 
        AND column_name = 'consumo_agua_orp'
    ) THEN
        ALTER TABLE public.lote_seguimientos 
        ADD COLUMN consumo_agua_orp double precision;
        RAISE NOTICE 'Columna consumo_agua_orp agregada';
    ELSE
        RAISE NOTICE 'Columna consumo_agua_orp ya existe';
    END IF;

    -- Temperatura del Agua
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'lote_seguimientos' 
        AND column_name = 'consumo_agua_temperatura'
    ) THEN
        ALTER TABLE public.lote_seguimientos 
        ADD COLUMN consumo_agua_temperatura double precision;
        RAISE NOTICE 'Columna consumo_agua_temperatura agregada';
    ELSE
        RAISE NOTICE 'Columna consumo_agua_temperatura ya existe';
    END IF;
END $$;

-- 2. Agregar campos de peso y uniformidad
DO $$
BEGIN
    -- Peso Promedio Hembras
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'lote_seguimientos' 
        AND column_name = 'peso_prom_h'
    ) THEN
        ALTER TABLE public.lote_seguimientos 
        ADD COLUMN peso_prom_h double precision;
        RAISE NOTICE 'Columna peso_prom_h agregada';
    ELSE
        RAISE NOTICE 'Columna peso_prom_h ya existe';
    END IF;

    -- Peso Promedio Machos
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'lote_seguimientos' 
        AND column_name = 'peso_prom_m'
    ) THEN
        ALTER TABLE public.lote_seguimientos 
        ADD COLUMN peso_prom_m double precision;
        RAISE NOTICE 'Columna peso_prom_m agregada';
    ELSE
        RAISE NOTICE 'Columna peso_prom_m ya existe';
    END IF;

    -- Uniformidad Hembras
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'lote_seguimientos' 
        AND column_name = 'uniformidad_h'
    ) THEN
        ALTER TABLE public.lote_seguimientos 
        ADD COLUMN uniformidad_h double precision;
        RAISE NOTICE 'Columna uniformidad_h agregada';
    ELSE
        RAISE NOTICE 'Columna uniformidad_h ya existe';
    END IF;

    -- Uniformidad Machos
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'lote_seguimientos' 
        AND column_name = 'uniformidad_m'
    ) THEN
        ALTER TABLE public.lote_seguimientos 
        ADD COLUMN uniformidad_m double precision;
        RAISE NOTICE 'Columna uniformidad_m agregada';
    ELSE
        RAISE NOTICE 'Columna uniformidad_m ya existe';
    END IF;

    -- CV Hembras
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'lote_seguimientos' 
        AND column_name = 'cv_h'
    ) THEN
        ALTER TABLE public.lote_seguimientos 
        ADD COLUMN cv_h double precision;
        RAISE NOTICE 'Columna cv_h agregada';
    ELSE
        RAISE NOTICE 'Columna cv_h ya existe';
    END IF;

    -- CV Machos
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'lote_seguimientos' 
        AND column_name = 'cv_m'
    ) THEN
        ALTER TABLE public.lote_seguimientos 
        ADD COLUMN cv_m double precision;
        RAISE NOTICE 'Columna cv_m agregada';
    ELSE
        RAISE NOTICE 'Columna cv_m ya existe';
    END IF;
END $$;

-- 3. Agregar campo de consumo para machos
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'lote_seguimientos' 
        AND column_name = 'consumo_kg_machos'
    ) THEN
        ALTER TABLE public.lote_seguimientos 
        ADD COLUMN consumo_kg_machos double precision;
        RAISE NOTICE 'Columna consumo_kg_machos agregada';
    ELSE
        RAISE NOTICE 'Columna consumo_kg_machos ya existe';
    END IF;
END $$;

-- 4. Agregar campos JSONB para metadata e items adicionales
DO $$
BEGIN
    -- Metadata JSONB
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'lote_seguimientos' 
        AND column_name = 'metadata'
    ) THEN
        ALTER TABLE public.lote_seguimientos 
        ADD COLUMN metadata jsonb DEFAULT '{}'::jsonb;
        RAISE NOTICE 'Columna metadata agregada';
    ELSE
        RAISE NOTICE 'Columna metadata ya existe';
    END IF;

    -- Items Adicionales JSONB
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'lote_seguimientos' 
        AND column_name = 'items_adicionales'
    ) THEN
        ALTER TABLE public.lote_seguimientos 
        ADD COLUMN items_adicionales jsonb DEFAULT '{}'::jsonb;
        RAISE NOTICE 'Columna items_adicionales agregada';
    ELSE
        RAISE NOTICE 'Columna items_adicionales ya existe';
    END IF;
END $$;

-- 5. Agregar campo ciclo
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'lote_seguimientos' 
        AND column_name = 'ciclo'
    ) THEN
        ALTER TABLE public.lote_seguimientos 
        ADD COLUMN ciclo character varying(50) DEFAULT 'Normal';
        RAISE NOTICE 'Columna ciclo agregada';
    ELSE
        RAISE NOTICE 'Columna ciclo ya existe';
    END IF;
END $$;

-- 6. Agregar comentarios a las columnas
COMMENT ON COLUMN public.lote_seguimientos.consumo_agua_diario IS 'Consumo diario de agua en litros (solo para Ecuador y Panamá)';
COMMENT ON COLUMN public.lote_seguimientos.consumo_agua_ph IS 'Nivel de PH del agua (0-14)';
COMMENT ON COLUMN public.lote_seguimientos.consumo_agua_orp IS 'Nivel de ORP (Oxidación-Reducción Potencial) del agua en mV';
COMMENT ON COLUMN public.lote_seguimientos.consumo_agua_temperatura IS 'Temperatura del agua en °C';
COMMENT ON COLUMN public.lote_seguimientos.peso_prom_h IS 'Peso promedio de hembras en kg';
COMMENT ON COLUMN public.lote_seguimientos.peso_prom_m IS 'Peso promedio de machos en kg';
COMMENT ON COLUMN public.lote_seguimientos.uniformidad_h IS 'Uniformidad de hembras (0-100)';
COMMENT ON COLUMN public.lote_seguimientos.uniformidad_m IS 'Uniformidad de machos (0-100)';
COMMENT ON COLUMN public.lote_seguimientos.cv_h IS 'Coeficiente de variación de hembras';
COMMENT ON COLUMN public.lote_seguimientos.cv_m IS 'Coeficiente de variación de machos';
COMMENT ON COLUMN public.lote_seguimientos.consumo_kg_machos IS 'Consumo de alimento en kg para machos';
COMMENT ON COLUMN public.lote_seguimientos.metadata IS 'Metadata JSONB para campos adicionales (consumo original con unidad, etc.)';
COMMENT ON COLUMN public.lote_seguimientos.items_adicionales IS 'Items adicionales JSONB para almacenar otros tipos de ítems (vacunas, medicamentos, etc.) que NO son alimentos';
COMMENT ON COLUMN public.lote_seguimientos.ciclo IS 'Ciclo de producción: Normal o Reforzado';

-- 7. Verificar estructura final
SELECT 
    column_name,
    data_type,
    character_maximum_length,
    numeric_precision,
    numeric_scale,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_schema = 'public'
AND table_name = 'lote_seguimientos'
ORDER BY ordinal_position;
