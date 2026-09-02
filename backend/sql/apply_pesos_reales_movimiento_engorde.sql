-- ============================================================
-- R2: Agregar columnas peso_bruto_real y peso_tara_real
--     a movimiento_pollo_engorde (prorrateo proporcional).
-- Aplicar en DBeaver / psql conectado a la BD objetivo.
-- ============================================================
--
-- HISTORICO - NO CORRER. Queda como registro de lo que se hizo, pero su NUMERIC(12,3)
-- ya no es el tipo vigente: el modelo declara double? y la migracion
-- 20260902160000_AlineaTipoPesoRealMovimientoEngorde alineo las dos columnas a
-- double precision, que es lo que ya eran las otras 6 columnas peso_* de la tabla.
--
-- Y este script es el ejemplo del anti-patron que CLAUDE.md prohibe: aplicar schema a
-- mano e insertar el id en __EFMigrationsHistory. Eso dejo a la migracion
-- 20260521110000 sin su .Designer.cs -o sea invisible para EF- hasta el 2-sep-2026.
-- El schema llega por migracion; el .sql es espejo, no vehiculo.
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
