-- =============================================================================
-- Desactiva el cierre AUTOMÁTICO del lote de levante (postura) al llegar a
-- edad >= 26 semanas. Ese comportamiento lo hacía el trigger
-- trg_lpl_cerrar_produccion en lote_postura_levante (AFTER INSERT/UPDATE).
--
-- Motivo típico: el cierre y la creación del lote de producción deben hacerse
-- solo por la acción manual en la API (Cerrar levante / flujo explícito), no
-- al registrar seguimiento diario u otras actualizaciones del LPL.
--
-- Ejecutar UNA VEZ en la base de datos (psql, DBeaver, etc.).
-- La función trg_lote_postura_levante_cerrar_produccion() NO se elimina;
-- solo se quita el trigger. Para volver a activar el cierre automático, ejecute
-- el final de: sql/trigger_lote_postura_levante_cerrar_produccion.sql
-- (desde DROP TRIGGER ... CREATE TRIGGER ... inclusive).
-- =============================================================================

DROP TRIGGER IF EXISTS trg_lpl_cerrar_produccion ON public.lote_postura_levante;
