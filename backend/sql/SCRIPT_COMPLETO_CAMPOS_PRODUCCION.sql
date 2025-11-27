-- ============================================================
-- SCRIPT COMPLETO: Campos de Producción - Verificación y Creación
-- ============================================================
-- Este script verifica y agrega todos los campos necesarios
-- para el módulo de producción avícola
-- ============================================================

-- ============================================================
-- 1. TABLA: produccion_lotes
-- Campos: nucleo_p, huevos_iniciales, tipo_nido, ciclo
-- ============================================================

-- Verificar si la tabla existe
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM information_schema.tables 
        WHERE table_name = 'produccion_lotes'
    ) THEN
        RAISE EXCEPTION 'La tabla produccion_lotes no existe. Debes crearla primero con create_produccion_lote_table.sql';
    END IF;
END $$;

-- Agregar columna nucleo_p (Núcleo de Producción) - Texto libre
ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS nucleo_p VARCHAR(100);

-- Agregar otras columnas que puedan faltar
ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS huevos_iniciales INTEGER NOT NULL DEFAULT 0;

ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS tipo_nido VARCHAR(50) NOT NULL DEFAULT 'Manual';

ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS ciclo VARCHAR(50) NOT NULL DEFAULT 'normal';

ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS granja_id INTEGER;

ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS nucleo_id VARCHAR(100);

ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS galpon_id VARCHAR(100);

-- Comentarios para documentación
COMMENT ON COLUMN produccion_lotes.nucleo_p IS 'Núcleo de Producción correspondiente al cierre de semana 25 (semana 26) - Texto libre';
COMMENT ON COLUMN produccion_lotes.huevos_iniciales IS 'Número de huevos en el inicio de producción (semana 26)';
COMMENT ON COLUMN produccion_lotes.tipo_nido IS 'Tipo de nido: Jansen, Manual, Vencomatic';
COMMENT ON COLUMN produccion_lotes.ciclo IS 'Ciclo de producción: normal, 2 Replume, D: Depopulación';

-- ============================================================
-- 2. TABLA: produccion_diaria (usada por SeguimientoProduccion)
-- Campos: sel_h, cons_kg_h, cons_kg_m, tipo_alimento, etapa
-- ============================================================

-- Verificar si la tabla existe, si no crearla
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM information_schema.tables 
        WHERE table_name = 'produccion_diaria'
    ) THEN
        -- Crear la tabla completa
        CREATE TABLE produccion_diaria (
            id SERIAL PRIMARY KEY,
            fecha_registro TIMESTAMP WITH TIME ZONE NOT NULL,
            lote_id TEXT NOT NULL,
            mortalidad_hembras INTEGER NOT NULL DEFAULT 0,
            mortalidad_machos INTEGER NOT NULL DEFAULT 0,
            sel_h INTEGER NOT NULL DEFAULT 0,
            cons_kg_h DOUBLE PRECISION NOT NULL DEFAULT 0,
            cons_kg_m DOUBLE PRECISION NOT NULL DEFAULT 0,
            huevo_tot INTEGER NOT NULL DEFAULT 0,
            huevo_inc INTEGER NOT NULL DEFAULT 0,
            tipo_alimento TEXT NOT NULL DEFAULT 'Standard',
            observaciones TEXT,
            peso_huevo DOUBLE PRECISION NOT NULL DEFAULT 0,
            etapa INTEGER NOT NULL DEFAULT 1,
            lote_produccion_id INTEGER,
            CONSTRAINT fk_produccion_diaria_produccion_lotes 
                FOREIGN KEY (lote_produccion_id) 
                REFERENCES produccion_lotes(id)
        );
        
        -- Índice único por lote y fecha
        CREATE UNIQUE INDEX IF NOT EXISTS ix_produccion_diaria_lote_fecha 
            ON produccion_diaria(lote_id, fecha_registro);
        
        -- Índice por lote_id
        CREATE INDEX IF NOT EXISTS ix_produccion_diaria_lote_id 
            ON produccion_diaria(lote_id);
        
        RAISE NOTICE 'Tabla produccion_diaria creada exitosamente';
    ELSE
        RAISE NOTICE 'Tabla produccion_diaria ya existe';
    END IF;
END $$;

-- Agregar campos que puedan faltar (si la tabla ya existía)
ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS sel_h INTEGER NOT NULL DEFAULT 0;

ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS cons_kg_h DOUBLE PRECISION NOT NULL DEFAULT 0;

ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS cons_kg_m DOUBLE PRECISION NOT NULL DEFAULT 0;

ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS tipo_alimento TEXT NOT NULL DEFAULT 'Standard';

ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS etapa INTEGER NOT NULL DEFAULT 1;

-- Asegurar valores por defecto en registros existentes
UPDATE produccion_diaria 
SET tipo_alimento = 'Standard' 
WHERE tipo_alimento IS NULL OR tipo_alimento = '';

UPDATE produccion_diaria 
SET etapa = 1 
WHERE etapa IS NULL OR (etapa < 1 OR etapa > 3);

-- Comentarios para documentación
COMMENT ON COLUMN produccion_diaria.sel_h IS 'Selección de hembras (retiradas)';
COMMENT ON COLUMN produccion_diaria.cons_kg_h IS 'Consumo de alimento hembras (kg)';
COMMENT ON COLUMN produccion_diaria.cons_kg_m IS 'Consumo de alimento machos (kg)';
COMMENT ON COLUMN produccion_diaria.tipo_alimento IS 'Tipo de alimento: Standard, Premium, Inicio, Postura, Final';
COMMENT ON COLUMN produccion_diaria.etapa IS 'Etapa de producción: 1 (Semana 25-33), 2 (34-50), 3 (>50)';

-- ============================================================
-- 3. VERIFICACIÓN FINAL
-- ============================================================

-- Verificar estructura de produccion_lotes
SELECT 
    '=== PRODUCCION_LOTES ===' as tabla,
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_name = 'produccion_lotes' 
    AND column_name IN ('nucleo_p', 'huevos_iniciales', 'tipo_nido', 'ciclo', 'granja_id', 'nucleo_id', 'galpon_id')
ORDER BY column_name;

-- Verificar estructura de produccion_diaria
SELECT 
    '=== PRODUCCION_DIARIA ===' as tabla,
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_name = 'produccion_diaria' 
    AND column_name IN ('sel_h', 'cons_kg_h', 'cons_kg_m', 'tipo_alimento', 'etapa', 'peso_huevo')
ORDER BY column_name;

-- ============================================================
-- RESUMEN
-- ============================================================
-- Este script garantiza que todas las tablas y columnas necesarias
-- estén presentes para el funcionamiento del módulo de producción.
--
-- Campos agregados/verificados en produccion_lotes:
--   - nucleo_p: VARCHAR(100) - Núcleo de Producción (texto libre)
--   - huevos_iniciales: INTEGER - Huevos iniciales (semana 26)
--   - tipo_nido: VARCHAR(50) - Jansen, Manual, Vencomatic
--   - ciclo: VARCHAR(50) - normal, 2 Replume, D: Depopulación
--
-- Campos agregados/verificados en produccion_diaria:
--   - sel_h: INTEGER - Selección hembras (retiradas)
--   - cons_kg_h: DOUBLE PRECISION - Consumo alimento hembras (kg)
--   - cons_kg_m: DOUBLE PRECISION - Consumo alimento machos (kg)
--   - tipo_alimento: TEXT - Tipo de alimento utilizado
--   - etapa: INTEGER - Etapa de producción (1, 2, 3)
-- ============================================================





