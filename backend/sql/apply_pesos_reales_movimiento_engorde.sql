-- ============================================================
-- R2: Agregar columnas peso_bruto_real y peso_tara_real
--     a movimiento_pollo_engorde (prorrateo proporcional).
-- Aplicar en DBeaver / psql conectado a la BD objetivo.
-- ============================================================

BEGIN;

-- 1. Agregar columnas (idempotentes: no fallan si ya existen)
ALTER TABLE movimiento_pollo_engorde
    ADD COLUMN IF NOT EXISTS peso_bruto_real NUMERIC(12,3) NULL;

ALTER TABLE movimiento_pollo_engorde
    ADD COLUMN IF NOT EXISTS peso_tara_real NUMERIC(12,3) NULL;

-- 2. Registrar la migración EF Core en el historial
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260521110000_AddPesosRealesMovimientoEngorde', '9.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;

-- Verificación rápida (ejecutar aparte):
-- SELECT column_name, data_type, is_nullable
-- FROM information_schema.columns
-- WHERE table_name = 'movimiento_pollo_engorde'
--   AND column_name IN ('peso_bruto_real', 'peso_tara_real');
