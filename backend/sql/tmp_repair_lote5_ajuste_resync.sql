-- Reparación puntual (BD local): revertir el doble ajuste del lote 5 causado por la fórmula
-- de conservación que restaba también los ajustes de re-sync (corregido en el servicio:
-- ahora el re-sync se audita como 'AjusteResync' y no participa en la conservación).
BEGIN;

-- 0) Ampliar el CHECK de tipo_registro para admitir 'AjusteResync'
ALTER TABLE historial_lote_pollo_engorde DROP CONSTRAINT IF EXISTS ck_hlpe_tipo_registro;
ALTER TABLE historial_lote_pollo_engorde ADD CONSTRAINT ck_hlpe_tipo_registro
    CHECK (tipo_registro IN ('Inicio', 'Ajuste', 'AjusteResync'));

-- 1) Restaurar maestro del lote 5 al estado correcto post-primer-resync (1101 H / 739 M)
UPDATE lote_ave_engorde SET hembras_l = 1101, machos_l = 739 WHERE lote_ave_engorde_id = 5;

-- 2) Eliminar la fila duplicada del segundo resync (la más reciente de hoy)
DELETE FROM historial_lote_pollo_engorde
WHERE id = (SELECT id FROM historial_lote_pollo_engorde
            WHERE lote_ave_engorde_id = 5 AND tipo_registro = 'Ajuste' AND fecha_registro::date = CURRENT_DATE
            ORDER BY fecha_registro DESC LIMIT 1);

-- 3) Retipificar la fila legítima del resync (no debe participar en la conservación)
UPDATE historial_lote_pollo_engorde
SET tipo_registro = 'AjusteResync'
WHERE lote_ave_engorde_id = 5 AND tipo_registro = 'Ajuste' AND fecha_registro::date = CURRENT_DATE;

COMMIT;

SELECT lote_ave_engorde_id AS id, hembras_l, machos_l FROM lote_ave_engorde WHERE lote_ave_engorde_id = 5;
SELECT lote_ave_engorde_id AS lote, tipo_registro, aves_hembras, aves_machos
FROM historial_lote_pollo_engorde
WHERE lote_ave_engorde_id = 5 AND tipo_registro <> 'Inicio';
