-- ============================================================================
-- ⛔ PROPUESTA DE LIMPIEZA — NO EJECUTAR SIN OK EXPLÍCITO DEL EQUIPO ⛔
-- Rama: refactor/optimizacion-multipais · Fase 4 del plan
--
-- Prerrequisitos ANTES de ejecutar:
--   1. Correr backend/sql/verificacion_tablas_sin_uso.sql en PROD.
--   2. Confirmar 0 dependencias y que los datos son desechables (scratch/backup).
--   3. Backup/snapshot de RDS vigente.
--   4. Ejecutar primero en LOCAL, validar app completa, luego prod.
--
-- Justificación por tabla (auditoría de código 2026-07-01):
--   _backup_*/_migracion_*  → tablas scratch de one-shots ya aplicados (mayo-jun 2026).
--   guia_semana, user_paises → sin entidad en Domain/, sin servicio, sin SQL vivo
--     que las use; solo aparecen en la migración AddMissingDbFunctionsTriggersAndViews.
-- ============================================================================

BEGIN;

-- Descomentar UNA VEZ verificado todo lo anterior:
-- (fork aves-engorde-panama eliminado 2026-07-02: entidad retirada del modelo EF;
--  verificar count(*)=0 en prod antes de soltar la tabla)
-- DROP TABLE IF EXISTS public.seguimiento_diario_aves_engorde_panama;
-- DROP TABLE IF EXISTS public._backup_cuadre_expected_2026_06_01;
-- DROP TABLE IF EXISTS public._migracion_saldo_alimento_2026_05_28;
-- DROP TABLE IF EXISTS public._migracion_saldo_alimento_2026_05_31;
-- DROP TABLE IF EXISTS public._migracion_saldo_alimento_m1_2026_05_31;
-- DROP TABLE IF EXISTS public.guia_semana;
-- DROP TABLE IF EXISTS public.user_paises;

COMMIT;
