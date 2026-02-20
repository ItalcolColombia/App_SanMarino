-- =====================================================
-- Script para agregar campos de agua a seguimiento_lote_levante
-- ZooSanMarino - PostgreSQL
-- =====================================================
-- Campos de agua (solo para Ecuador y Panamá)
-- =====================================================

DO $$
BEGIN
    -- Agregar consumo_agua_diario
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'seguimiento_lote_levante' 
        AND column_name = 'consumo_agua_diario'
    ) THEN
        ALTER TABLE seguimiento_lote_levante
        ADD COLUMN consumo_agua_diario NUMERIC(12,3);
        
        COMMENT ON COLUMN seguimiento_lote_levante.consumo_agua_diario IS 'Consumo diario de agua en litros';
        
        RAISE NOTICE 'Columna consumo_agua_diario agregada.';
    ELSE
        RAISE NOTICE 'Columna consumo_agua_diario ya existe.';
    END IF;

    -- Agregar consumo_agua_ph
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'seguimiento_lote_levante' 
        AND column_name = 'consumo_agua_ph'
    ) THEN
        ALTER TABLE seguimiento_lote_levante
        ADD COLUMN consumo_agua_ph NUMERIC(5,2);
        
        COMMENT ON COLUMN seguimiento_lote_levante.consumo_agua_ph IS 'Nivel de PH del agua (0-14)';
        
        RAISE NOTICE 'Columna consumo_agua_ph agregada.';
    ELSE
        RAISE NOTICE 'Columna consumo_agua_ph ya existe.';
    END IF;

    -- Agregar consumo_agua_orp
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'seguimiento_lote_levante' 
        AND column_name = 'consumo_agua_orp'
    ) THEN
        ALTER TABLE seguimiento_lote_levante
        ADD COLUMN consumo_agua_orp NUMERIC(10,2);
        
        COMMENT ON COLUMN seguimiento_lote_levante.consumo_agua_orp IS 'Nivel de ORP (Oxidación-Reducción Potencial) del agua en mV';
        
        RAISE NOTICE 'Columna consumo_agua_orp agregada.';
    ELSE
        RAISE NOTICE 'Columna consumo_agua_orp ya existe.';
    END IF;

    -- Agregar consumo_agua_temperatura
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'seguimiento_lote_levante' 
        AND column_name = 'consumo_agua_temperatura'
    ) THEN
        ALTER TABLE seguimiento_lote_levante
        ADD COLUMN consumo_agua_temperatura NUMERIC(5,2);
        
        COMMENT ON COLUMN seguimiento_lote_levante.consumo_agua_temperatura IS 'Temperatura del agua en °C';
        
        RAISE NOTICE 'Columna consumo_agua_temperatura agregada.';
    ELSE
        RAISE NOTICE 'Columna consumo_agua_temperatura ya existe.';
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'Error al agregar columnas de agua: %', SQLERRM;
END $$;

-- Verificar que las columnas se agregaron correctamente
SELECT 
    column_name, 
    data_type, 
    numeric_precision,
    numeric_scale,
    is_nullable
FROM information_schema.columns
WHERE table_schema = 'public' 
  AND table_name = 'seguimiento_lote_levante'
  AND column_name IN ('consumo_agua_diario', 'consumo_agua_ph', 'consumo_agua_orp', 'consumo_agua_temperatura')
ORDER BY column_name;
