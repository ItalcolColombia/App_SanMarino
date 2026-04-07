-- Permite cantidades = 0 cuando el movimiento está Anulado (soft-delete)
-- sin violar el CHECK ck_mpe_cantidades.
--
-- Necesario para el flujo "corregir-ventas-completadas", ya que puede reducir un movimiento Completado
-- hasta 0 y luego marcarlo como 'Anulado'.
--
-- Ejecutar UNA sola vez en bases ya creadas con la restricción antigua.

ALTER TABLE movimiento_pollo_engorde DROP CONSTRAINT IF EXISTS ck_mpe_cantidades;

ALTER TABLE movimiento_pollo_engorde
ADD CONSTRAINT ck_mpe_cantidades CHECK (
    cantidad_hembras >= 0 AND cantidad_machos >= 0 AND cantidad_mixtas >= 0
    AND (
        -- Regla normal: todo movimiento activo debe tener al menos 1 ave
        (cantidad_hembras + cantidad_machos + cantidad_mixtas) > 0
        -- Excepción: cuando está anulado / eliminado, puede quedar en 0
        OR estado = 'Anulado'
        OR deleted_at IS NOT NULL
    )
);

