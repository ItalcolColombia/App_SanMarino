-- =====================================================================
-- 049 — updated_by_user_id en seguimiento_diario_levante_reproductoras
-- Feature 13: auditoría completa del seguimiento manual sobre traslados.
--
-- created_by_user_id ya existe; añadimos updated_by_user_id para saber
-- quién hizo el último cambio (útil cuando un usuario crea un seguimiento
-- manual encima de una fila que originalmente fue creada por un traslado).
-- =====================================================================

ALTER TABLE seguimiento_diario_levante_reproductoras
  ADD COLUMN IF NOT EXISTS updated_by_user_id VARCHAR(64) NULL;

COMMENT ON COLUMN seguimiento_diario_levante_reproductoras.updated_by_user_id IS 'GUID del usuario que actualizó por última vez la fila (NULL si nunca se ha modificado).';
