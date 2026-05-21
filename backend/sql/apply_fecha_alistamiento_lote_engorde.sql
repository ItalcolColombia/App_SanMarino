-- ============================================================
-- R3: Agregar campo fecha_alistamiento a lote_ave_engorde
-- Aplicar en DBeaver / psql conectado a la BD objetivo.
-- ============================================================

BEGIN;

-- 1. Agregar columna (idempotente: no falla si ya existe)
ALTER TABLE lote_ave_engorde
    ADD COLUMN IF NOT EXISTS fecha_alistamiento DATE NULL;

-- 2. Registrar la migración EF Core en el historial
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260521100000_AddFechaAlistamientoLoteEngorde', '9.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;

-- Verificación rápida (ejecutar aparte):
-- SELECT column_name, data_type, is_nullable
-- FROM information_schema.columns
-- WHERE table_name = 'lote_ave_engorde'
--   AND column_name = 'fecha_alistamiento';
