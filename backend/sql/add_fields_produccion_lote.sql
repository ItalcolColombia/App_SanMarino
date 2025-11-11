-- ============================================================
-- SQL para agregar campos a produccion_lote
-- ============================================================
-- Esta migración agrega los campos necesarios para el registro
-- inicial de producción cuando un lote llega a la semana 26
-- ============================================================

-- 1. Verificar si la tabla existe
SELECT EXISTS (
   SELECT FROM information_schema.tables 
   WHERE table_name = 'produccion_lotes'
);

-- 2. Agregar columna huevos_iniciales
ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS huevos_iniciales INTEGER NOT NULL DEFAULT 0;

-- 3. Agregar columna tipo_nido (Jansen, Manual, Vencomatic)
ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS tipo_nido VARCHAR(50) NOT NULL DEFAULT 'Manual';

-- 4. Agregar columna nucleo_p (Núcleo de Producción)
ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS nucleo_p VARCHAR(100);

-- 5. Agregar columna ciclo (normal, 2 Replume, D: Depopulación)
ALTER TABLE produccion_lotes 
ADD COLUMN IF NOT EXISTS ciclo VARCHAR(50) NOT NULL DEFAULT 'normal';

-- 6. Verificar que se agregaron las columnas
SELECT column_name, data_type, column_default, is_nullable
FROM information_schema.columns 
WHERE table_name = 'produccion_lotes' 
ORDER BY ordinal_position;

-- 7. Ver datos de ejemplo (opcional)
-- SELECT 
--     id,
--     lote_id,
--     fecha_inicio,
--     aves_iniciales_h,
--     aves_iniciales_m,
--     huevos_iniciales,
--     tipo_nido,
--     nucleo_p,
--     ciclo,
--     observaciones
-- FROM produccion_lote
-- LIMIT 5;

