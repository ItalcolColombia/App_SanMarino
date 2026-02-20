-- Permite NULL en farms.regional_id para alinear con el modelo (RegionalId opcional).
-- Ejecutar contra la base de datos donde corre la API.
-- Uso: psql -U postgres -d TuBaseDeDatos -f allow_null_regional_id_farms.sql

ALTER TABLE farms
  ALTER COLUMN regional_id DROP NOT NULL;
