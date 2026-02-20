-- Migración: Agregar campos de Company y Country a master_lists
-- Fecha: 2024

-- Agregar nuevas columnas a la tabla master_lists
ALTER TABLE master_lists
ADD COLUMN IF NOT EXISTS company_id INTEGER NULL,
ADD COLUMN IF NOT EXISTS company_name VARCHAR(200) NULL,
ADD COLUMN IF NOT EXISTS country_id INTEGER NULL,
ADD COLUMN IF NOT EXISTS country_name VARCHAR(200) NULL;

-- Crear índices para mejorar el rendimiento de las consultas
CREATE INDEX IF NOT EXISTS ix_master_lists_company_id ON master_lists(company_id);
CREATE INDEX IF NOT EXISTS ix_master_lists_country_id ON master_lists(country_id);
CREATE INDEX IF NOT EXISTS ix_master_lists_company_country ON master_lists(company_id, country_id);

-- Eliminar el índice único anterior de key (se reemplazará por uno compuesto)
DROP INDEX IF EXISTS ix_master_lists_key;

-- Crear índice único compuesto para (key, company_id, country_id)
-- Esto permite tener el mismo key para diferentes compañías/países
CREATE UNIQUE INDEX IF NOT EXISTS ix_master_lists_key_company_country 
ON master_lists(key, company_id, country_id)
WHERE company_id IS NOT NULL AND country_id IS NOT NULL;

-- Crear índice único para key solo (para compatibilidad con registros antiguos sin company/country)
CREATE UNIQUE INDEX IF NOT EXISTS ix_master_lists_key_null 
ON master_lists(key)
WHERE company_id IS NULL AND country_id IS NULL;

-- Comentarios en las columnas
COMMENT ON COLUMN master_lists.company_id IS 'ID de la compañía a la que pertenece esta lista maestra';
COMMENT ON COLUMN master_lists.company_name IS 'Nombre de la compañía (denormalizado para mejor rendimiento)';
COMMENT ON COLUMN master_lists.country_id IS 'ID del país al que pertenece esta lista maestra';
COMMENT ON COLUMN master_lists.country_name IS 'Nombre del país (denormalizado para mejor rendimiento)';
