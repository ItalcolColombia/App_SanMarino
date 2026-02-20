-- Campos de despacho/salida para Movimiento de Pollo Engorde (venta de aves)
-- Despacho: número despacho, granja, fecha, galpón, unidades, edad, sexo, total pollos galpón,
-- raza, placa, hora salida, guía agrocalidad, sellos, ayuno, conductor, peso bruto, peso tara.

DO $$
BEGIN
    -- numero_despacho
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'movimiento_pollo_engorde' AND column_name = 'numero_despacho') THEN
        ALTER TABLE movimiento_pollo_engorde ADD COLUMN numero_despacho VARCHAR(50) NULL;
        RAISE NOTICE 'Columna numero_despacho agregada.';
    END IF;
    -- edad_aves
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'movimiento_pollo_engorde' AND column_name = 'edad_aves') THEN
        ALTER TABLE movimiento_pollo_engorde ADD COLUMN edad_aves INTEGER NULL;
        RAISE NOTICE 'Columna edad_aves agregada.';
    END IF;
    -- total_pollos_galpon
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'movimiento_pollo_engorde' AND column_name = 'total_pollos_galpon') THEN
        ALTER TABLE movimiento_pollo_engorde ADD COLUMN total_pollos_galpon INTEGER NULL;
        RAISE NOTICE 'Columna total_pollos_galpon agregada.';
    END IF;
    -- raza
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'movimiento_pollo_engorde' AND column_name = 'raza') THEN
        ALTER TABLE movimiento_pollo_engorde ADD COLUMN raza VARCHAR(100) NULL;
        RAISE NOTICE 'Columna raza agregada.';
    END IF;
    -- placa
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'movimiento_pollo_engorde' AND column_name = 'placa') THEN
        ALTER TABLE movimiento_pollo_engorde ADD COLUMN placa VARCHAR(20) NULL;
        RAISE NOTICE 'Columna placa agregada.';
    END IF;
    -- hora_salida
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'movimiento_pollo_engorde' AND column_name = 'hora_salida') THEN
        ALTER TABLE movimiento_pollo_engorde ADD COLUMN hora_salida TIME NULL;
        RAISE NOTICE 'Columna hora_salida agregada.';
    END IF;
    -- guia_agrocalidad
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'movimiento_pollo_engorde' AND column_name = 'guia_agrocalidad') THEN
        ALTER TABLE movimiento_pollo_engorde ADD COLUMN guia_agrocalidad VARCHAR(100) NULL;
        RAISE NOTICE 'Columna guia_agrocalidad agregada.';
    END IF;
    -- sellos
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'movimiento_pollo_engorde' AND column_name = 'sellos') THEN
        ALTER TABLE movimiento_pollo_engorde ADD COLUMN sellos VARCHAR(500) NULL;
        RAISE NOTICE 'Columna sellos agregada.';
    END IF;
    -- ayuno
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'movimiento_pollo_engorde' AND column_name = 'ayuno') THEN
        ALTER TABLE movimiento_pollo_engorde ADD COLUMN ayuno VARCHAR(50) NULL;
        RAISE NOTICE 'Columna ayuno agregada.';
    END IF;
    -- conductor
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'movimiento_pollo_engorde' AND column_name = 'conductor') THEN
        ALTER TABLE movimiento_pollo_engorde ADD COLUMN conductor VARCHAR(200) NULL;
        RAISE NOTICE 'Columna conductor agregada.';
    END IF;
    -- peso_bruto
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'movimiento_pollo_engorde' AND column_name = 'peso_bruto') THEN
        ALTER TABLE movimiento_pollo_engorde ADD COLUMN peso_bruto DOUBLE PRECISION NULL;
        RAISE NOTICE 'Columna peso_bruto agregada.';
    END IF;
    -- peso_tara
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'movimiento_pollo_engorde' AND column_name = 'peso_tara') THEN
        ALTER TABLE movimiento_pollo_engorde ADD COLUMN peso_tara DOUBLE PRECISION NULL;
        RAISE NOTICE 'Columna peso_tara agregada.';
    END IF;
END $$;

COMMENT ON COLUMN movimiento_pollo_engorde.numero_despacho IS 'Número único del despacho (salida/venta)';
COMMENT ON COLUMN movimiento_pollo_engorde.edad_aves IS 'Edad de las aves en días';
COMMENT ON COLUMN movimiento_pollo_engorde.total_pollos_galpon IS 'Total de pollos por galpón (unidades)';
COMMENT ON COLUMN movimiento_pollo_engorde.peso_bruto IS 'Peso bruto total (kg)';
COMMENT ON COLUMN movimiento_pollo_engorde.peso_tara IS 'Peso tara (kg). Peso neto = peso_bruto - peso_tara';
