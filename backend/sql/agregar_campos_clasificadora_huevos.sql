-- ============================================================
-- SCRIPT PARA AGREGAR CAMPOS DE CLASIFICADORA DE HUEVOS
-- Tabla: produccion_diaria
-- ============================================================
-- Campos para clasificación detallada de huevos
-- (Limpio, Tratado) = HuevoInc +
-- (Sucio, Deforme, Blanco, Doble Yema, Piso, Pequeño, Roto, Desecho, Otro) = Huevo Total
-- ============================================================

-- 1. Campos que se suman a HuevoInc (Incubables)
ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS huevo_limpio INTEGER NOT NULL DEFAULT 0;

ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS huevo_tratado INTEGER NOT NULL DEFAULT 0;

-- 2. Campos que se suman a Huevo Total (incluidos en el total pero no incubables)
ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS huevo_sucio INTEGER NOT NULL DEFAULT 0;

ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS huevo_deforme INTEGER NOT NULL DEFAULT 0;

ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS huevo_blanco INTEGER NOT NULL DEFAULT 0;

ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS huevo_doble_yema INTEGER NOT NULL DEFAULT 0;

ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS huevo_piso INTEGER NOT NULL DEFAULT 0;

ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS huevo_pequeno INTEGER NOT NULL DEFAULT 0;

ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS huevo_roto INTEGER NOT NULL DEFAULT 0;

ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS huevo_desecho INTEGER NOT NULL DEFAULT 0;

ALTER TABLE produccion_diaria 
ADD COLUMN IF NOT EXISTS huevo_otro INTEGER NOT NULL DEFAULT 0;

-- ============================================================
-- COMENTARIOS PARA DOCUMENTACIÓN
-- ============================================================

COMMENT ON COLUMN produccion_diaria.huevo_limpio IS 'Huevos limpios (se suman a HuevoInc)';
COMMENT ON COLUMN produccion_diaria.huevo_tratado IS 'Huevos tratados (se suman a HuevoInc)';
COMMENT ON COLUMN produccion_diaria.huevo_sucio IS 'Huevos sucios (se suman a Huevo Total)';
COMMENT ON COLUMN produccion_diaria.huevo_deforme IS 'Huevos deformes (se suman a Huevo Total)';
COMMENT ON COLUMN produccion_diaria.huevo_blanco IS 'Huevos blancos (se suman a Huevo Total)';
COMMENT ON COLUMN produccion_diaria.huevo_doble_yema IS 'Huevos con doble yema (se suman a Huevo Total)';
COMMENT ON COLUMN produccion_diaria.huevo_piso IS 'Huevos de piso (se suman a Huevo Total)';
COMMENT ON COLUMN produccion_diaria.huevo_pequeno IS 'Huevos pequeños (se suman a Huevo Total)';
COMMENT ON COLUMN produccion_diaria.huevo_roto IS 'Huevos rotos (se suman a Huevo Total)';
COMMENT ON COLUMN produccion_diaria.huevo_desecho IS 'Huevos de desecho (se suman a Huevo Total)';
COMMENT ON COLUMN produccion_diaria.huevo_otro IS 'Otros tipos de huevos (se suman a Huevo Total)';

-- ============================================================
-- VERIFICACIÓN
-- ============================================================

SELECT 
    column_name AS campo,
    data_type AS tipo,
    is_nullable AS permite_null,
    column_default AS valor_defecto
FROM information_schema.columns 
WHERE table_name = 'produccion_diaria' 
    AND column_name LIKE 'huevo_%'
ORDER BY column_name;





