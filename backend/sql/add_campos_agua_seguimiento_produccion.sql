-- ============================================================
-- AGREGAR CAMPOS DE AGUA AL SEGUIMIENTO DIARIO PRODUCCIÓN
-- Para Ecuador y Panamá
-- ============================================================
-- Este script agrega los campos relacionados con el consumo
-- y calidad del agua en el seguimiento diario de producción
-- ============================================================

BEGIN;

-- Agregar campo de consumo de agua diario (si no existe)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public'
        AND table_name = 'produccion_diaria' 
        AND column_name = 'consumo_agua_diario'
    ) THEN
        ALTER TABLE produccion_diaria
        ADD COLUMN consumo_agua_diario DOUBLE PRECISION;
        
        COMMENT ON COLUMN produccion_diaria.consumo_agua_diario IS 
            'Consumo diario de agua en litros (Ecuador y Panamá)';
    END IF;
END $$;

-- Verificar y agregar campos de PH, ORP y Temperatura si no existen
DO $$
BEGIN
    -- PH
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public'
        AND table_name = 'produccion_diaria' 
        AND column_name = 'consumo_agua_ph'
    ) THEN
        ALTER TABLE produccion_diaria
        ADD COLUMN consumo_agua_ph DOUBLE PRECISION;
        
        COMMENT ON COLUMN produccion_diaria.consumo_agua_ph IS 
            'Nivel de PH del agua (Ecuador y Panamá)';
    END IF;

    -- ORP
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public'
        AND table_name = 'produccion_diaria' 
        AND column_name = 'consumo_agua_orp'
    ) THEN
        ALTER TABLE produccion_diaria
        ADD COLUMN consumo_agua_orp DOUBLE PRECISION;
        
        COMMENT ON COLUMN produccion_diaria.consumo_agua_orp IS 
            'Nivel de ORP (Oxidación-Reducción Potencial) del agua en mV (Ecuador y Panamá)';
    END IF;

    -- Temperatura
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public'
        AND table_name = 'produccion_diaria' 
        AND column_name = 'consumo_agua_temperatura'
    ) THEN
        ALTER TABLE produccion_diaria
        ADD COLUMN consumo_agua_temperatura DOUBLE PRECISION;
        
        COMMENT ON COLUMN produccion_diaria.consumo_agua_temperatura IS 
            'Temperatura del agua en °C (Ecuador y Panamá)';
    END IF;
END $$;

COMMIT;

-- Verificación
SELECT 
    column_name,
    data_type,
    is_nullable
FROM information_schema.columns
WHERE table_schema = 'public'
    AND table_name = 'produccion_diaria'
    AND column_name IN ('consumo_agua_diario', 'consumo_agua_ph', 'consumo_agua_orp', 'consumo_agua_temperatura')
ORDER BY column_name;
