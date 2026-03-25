-- Permite estado 'Anulado' al anular/eliminar un movimiento (soft-delete) sin violar ck_mpe_estado.
-- Ejecutar una vez en bases ya creadas con la restricción antigua.

ALTER TABLE movimiento_pollo_engorde DROP CONSTRAINT IF EXISTS ck_mpe_estado;
ALTER TABLE movimiento_pollo_engorde ADD CONSTRAINT ck_mpe_estado CHECK (estado IN ('Pendiente', 'Completado', 'Cancelado', 'Anulado'));
