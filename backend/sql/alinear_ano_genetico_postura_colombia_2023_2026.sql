-- ============================================================================
-- Alineación de año genético — Postura Colombia (Agroavícola Sanmarino, company_id=1)
-- ----------------------------------------------------------------------------
-- Contexto: la guía genética 2026 (guia_genetica_sanmarino_colombia) YA está
-- cargada para raza AP, pero los lotes de postura de company 1 apuntaban al
-- año 2023, por lo que los comparativos (reportes/liquidaciones/gráficas)
-- resolvían contra la guía vieja.
--
-- Este script NO es una migración EF a propósito: cambia DATOS de UNA empresa
-- puntual (no schema) y el año genético también es editable por lote desde la
-- UI (Lote Postura → editar). Correr en cada deploy sería incorrecto.
--
-- Alcance: SOLO company_id = 1, raza 'AP', ano_tabla_genetica = 2023 → 2026.
-- Idempotente: si ya están en 2026, no hace nada.
-- Reversible: cambiar 2026→2023 en los mismos WHERE para deshacer.
--
-- Aplicado en LOCAL (sanmarinoapplocal) el 2026-07-01. Para PROD: revisar y
-- ejecutar manualmente, o editar cada lote por la UI.
-- ============================================================================

BEGIN;

-- Lotes base
UPDATE lotes
   SET ano_tabla_genetica = 2026,
       updated_at = now()
 WHERE company_id = 1
   AND raza = 'AP'
   AND ano_tabla_genetica = 2023
   AND deleted_at IS NULL;

-- Lote Postura Levante
UPDATE lote_postura_levante
   SET ano_tabla_genetica = 2026,
       updated_at = now()
 WHERE company_id = 1
   AND raza = 'AP'
   AND ano_tabla_genetica = 2023
   AND deleted_at IS NULL;

-- Lote Postura Producción
UPDATE lote_postura_produccion
   SET ano_tabla_genetica = 2026,
       updated_at = now()
 WHERE company_id = 1
   AND raza = 'AP'
   AND ano_tabla_genetica = 2023
   AND deleted_at IS NULL;

COMMIT;
