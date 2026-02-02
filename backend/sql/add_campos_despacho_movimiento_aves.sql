-- Script para agregar campos de despacho a la tabla movimiento_aves (Ecuador)
-- Fecha: 2026-02-02
-- Descripción: Agrega campos específicos para movimientos de salida (despacho) de aves
-- Estos campos son requeridos para el módulo de despacho en Ecuador

BEGIN;

DO $$
BEGIN
    -- 1. Edad de las aves (en días)
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'movimiento_aves' 
        AND column_name = 'edad_aves'
    ) THEN
        ALTER TABLE movimiento_aves
        ADD COLUMN edad_aves INTEGER NULL;
        
        RAISE NOTICE 'Columna edad_aves agregada a la tabla movimiento_aves.';
    ELSE
        RAISE NOTICE 'Columna edad_aves ya existe en la tabla movimiento_aves.';
    END IF;

    -- 2. Raza de las aves
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'movimiento_aves' 
        AND column_name = 'raza'
    ) THEN
        ALTER TABLE movimiento_aves
        ADD COLUMN raza VARCHAR(100) NULL;
        
        RAISE NOTICE 'Columna raza agregada a la tabla movimiento_aves.';
    ELSE
        RAISE NOTICE 'Columna raza ya existe en la tabla movimiento_aves.';
    END IF;

    -- 3. Placa del vehículo
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'movimiento_aves' 
        AND column_name = 'placa'
    ) THEN
        ALTER TABLE movimiento_aves
        ADD COLUMN placa VARCHAR(20) NULL;
        
        RAISE NOTICE 'Columna placa agregada a la tabla movimiento_aves.';
    ELSE
        RAISE NOTICE 'Columna placa ya existe en la tabla movimiento_aves.';
    END IF;

    -- 4. Hora de salida
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'movimiento_aves' 
        AND column_name = 'hora_salida'
    ) THEN
        ALTER TABLE movimiento_aves
        ADD COLUMN hora_salida TIME NULL;
        
        RAISE NOTICE 'Columna hora_salida agregada a la tabla movimiento_aves.';
    ELSE
        RAISE NOTICE 'Columna hora_salida ya existe en la tabla movimiento_aves.';
    END IF;

    -- 5. Guía Agrocalidad
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'movimiento_aves' 
        AND column_name = 'guia_agrocalidad'
    ) THEN
        ALTER TABLE movimiento_aves
        ADD COLUMN guia_agrocalidad VARCHAR(100) NULL;
        
        RAISE NOTICE 'Columna guia_agrocalidad agregada a la tabla movimiento_aves.';
    ELSE
        RAISE NOTICE 'Columna guia_agrocalidad ya existe en la tabla movimiento_aves.';
    END IF;

    -- 6. Sellos
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'movimiento_aves' 
        AND column_name = 'sellos'
    ) THEN
        ALTER TABLE movimiento_aves
        ADD COLUMN sellos VARCHAR(500) NULL;
        
        RAISE NOTICE 'Columna sellos agregada a la tabla movimiento_aves.';
    ELSE
        RAISE NOTICE 'Columna sellos ya existe en la tabla movimiento_aves.';
    END IF;

    -- 7. Ayuno (horas de ayuno o indicador booleano)
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'movimiento_aves' 
        AND column_name = 'ayuno'
    ) THEN
        ALTER TABLE movimiento_aves
        ADD COLUMN ayuno VARCHAR(50) NULL;
        
        RAISE NOTICE 'Columna ayuno agregada a la tabla movimiento_aves.';
    ELSE
        RAISE NOTICE 'Columna ayuno ya existe en la tabla movimiento_aves.';
    END IF;

    -- 8. Conductor
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'movimiento_aves' 
        AND column_name = 'conductor'
    ) THEN
        ALTER TABLE movimiento_aves
        ADD COLUMN conductor VARCHAR(200) NULL;
        
        RAISE NOTICE 'Columna conductor agregada a la tabla movimiento_aves.';
    ELSE
        RAISE NOTICE 'Columna conductor ya existe en la tabla movimiento_aves.';
    END IF;

    -- 9. Total de pollos por galpón (unidades)
    -- Este campo puede ser calculado, pero se almacena para referencia rápida
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'movimiento_aves' 
        AND column_name = 'total_pollos_galpon'
    ) THEN
        ALTER TABLE movimiento_aves
        ADD COLUMN total_pollos_galpon INTEGER NULL;
        
        RAISE NOTICE 'Columna total_pollos_galpon agregada a la tabla movimiento_aves.';
    ELSE
        RAISE NOTICE 'Columna total_pollos_galpon ya existe en la tabla movimiento_aves.';
    END IF;

    -- 10. Peso Bruto (kg)
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'movimiento_aves' 
        AND column_name = 'peso_bruto'
    ) THEN
        ALTER TABLE movimiento_aves
        ADD COLUMN peso_bruto DOUBLE PRECISION NULL;
        
        RAISE NOTICE 'Columna peso_bruto agregada a la tabla movimiento_aves.';
    ELSE
        RAISE NOTICE 'Columna peso_bruto ya existe en la tabla movimiento_aves.';
    END IF;

    -- 11. Peso Tara (kg)
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = 'movimiento_aves' 
        AND column_name = 'peso_tara'
    ) THEN
        ALTER TABLE movimiento_aves
        ADD COLUMN peso_tara DOUBLE PRECISION NULL;
        
        RAISE NOTICE 'Columna peso_tara agregada a la tabla movimiento_aves.';
    ELSE
        RAISE NOTICE 'Columna peso_tara ya existe en la tabla movimiento_aves.';
    END IF;
END $$;

-- Agregar comentarios a las columnas para documentación
COMMENT ON COLUMN movimiento_aves.edad_aves IS 'Edad de las aves en días al momento del despacho (Ecuador)';
COMMENT ON COLUMN movimiento_aves.raza IS 'Raza de las aves despachadas (Ecuador)';
COMMENT ON COLUMN movimiento_aves.placa IS 'Placa del vehículo utilizado para el despacho (Ecuador)';
COMMENT ON COLUMN movimiento_aves.hora_salida IS 'Hora de salida del vehículo con las aves (Ecuador)';
COMMENT ON COLUMN movimiento_aves.guia_agrocalidad IS 'Número de guía Agrocalidad para el despacho (Ecuador)';
COMMENT ON COLUMN movimiento_aves.sellos IS 'Información de sellos aplicados al despacho (Ecuador)';
COMMENT ON COLUMN movimiento_aves.ayuno IS 'Información sobre el ayuno de las aves (horas o indicador) (Ecuador)';
COMMENT ON COLUMN movimiento_aves.conductor IS 'Nombre del conductor del vehículo de despacho (Ecuador)';
COMMENT ON COLUMN movimiento_aves.total_pollos_galpon IS 'Total de pollos por galpón al momento del despacho (Ecuador)';
COMMENT ON COLUMN movimiento_aves.peso_bruto IS 'Peso bruto del despacho en kg (Ecuador)';
COMMENT ON COLUMN movimiento_aves.peso_tara IS 'Peso tara (peso del vehículo/contenedor vacío) en kg (Ecuador)';

       COMMIT;
