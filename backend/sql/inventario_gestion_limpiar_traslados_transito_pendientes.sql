-- =============================================================================
-- Limpieza: traslados inter-granja en TRÁNSITO PENDIENTES (sin recepción en destino)
-- =============================================================================
-- Objetivo: borrar los movimientos de solicitud/salida que aún no tienen
--           TrasladoInterGranjaEntrada, para poder volver a registrar traslados
--           con la lógica nueva del backend.
--
-- Tabla: public.inventario_gestion_movimiento
-- Tipos relevantes (ver inventario_gestion_movement_types.sql):
--   TrasladoInterGranjaPendiente  → solicitud antigua (origen NO descontado al crear)
--   TrasladoInterGranjaSalida     → salida actual (origen SÍ descontado al enviar)
--   TrasladoInterGranjaEntrada    → recepción (cierra el tránsito)
--
-- IMPORTANTE:
--   - Para TrasladoInterGranjaSalida este script DEVUELVE la cantidad al stock
--     en origen (misma granja + ítem + núcleo/galpón que el movimiento).
--   - Para TrasladoInterGranjaPendiente solo se elimina el movimiento (no había
--     descuento de stock al crear la solicitud).
--   - NO borra recepciones ni movimientos ya cerrados (Entrada / Rechazado).
--
-- Uso recomendado:
--   1) Ejecutar solo la sección "1) VISTA PREVIA" y revisar filas.
--   2) Opcional: filtrar por company_id descomentando el filtro en el bloque 2).
--   3) Ejecutar 2) + 3) dentro de una transacción; hacer COMMIT o ROLLBACK.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1) VISTA PREVIA (solo lectura): qué grupos y movimientos se consideran "abiertos"
-- -----------------------------------------------------------------------------
WITH grupos_con_entrada AS (
    SELECT DISTINCT transfer_group_id
    FROM public.inventario_gestion_movimiento
    WHERE transfer_group_id IS NOT NULL
      AND movement_type = 'TrasladoInterGranjaEntrada'
),
grupos_abiertos AS (
    SELECT DISTINCT m.transfer_group_id
    FROM public.inventario_gestion_movimiento m
    WHERE m.transfer_group_id IS NOT NULL
      AND m.movement_type IN ('TrasladoInterGranjaPendiente', 'TrasladoInterGranjaSalida')
      AND m.transfer_group_id NOT IN (SELECT transfer_group_id FROM grupos_con_entrada)
)
SELECT
    m.id AS movimiento_id,
    m.transfer_group_id,
    m.movement_type,
    m.company_id,
    m.farm_id AS granja_origen_id,
    m.nucleo_id,
    m.galpon_id,
    m.from_farm_id AS granja_destino_hint_id,
    m.item_inventario_ecuador_id,
    m.quantity,
    m.unit,
    m.created_at,
    CASE
        WHEN m.movement_type = 'TrasladoInterGranjaSalida'
        THEN 'Se devolverá esta cantidad al stock en origen antes de borrar el movimiento.'
        ELSE 'Solo se borra el movimiento (solicitud antigua; stock origen no se había descontado al crear).'
    END AS efecto_en_stock
FROM public.inventario_gestion_movimiento m
WHERE m.transfer_group_id IN (SELECT transfer_group_id FROM grupos_abiertos)
  AND m.movement_type IN ('TrasladoInterGranjaPendiente', 'TrasladoInterGranjaSalida')
ORDER BY m.created_at DESC;


-- =============================================================================
-- 2) y 3) EJECUCIÓN: restaurar stock (solo Salida) y eliminar movimientos
--     Copie desde BEGIN hasta COMMIT / ROLLBACK y ejecútelo en una sesión.
-- =============================================================================

/*
BEGIN;

-- Opcional: limitar a una empresa — añada en ambos WHERE del UPDATE y del DELETE:
--   AND m.company_id = 1

-- 2) Devolver cantidad al stock de origen solo para TrasladoInterGranjaSalida
UPDATE public.inventario_gestion_stock s
SET
    quantity = s.quantity + m.quantity,
    updated_at = now()
FROM public.inventario_gestion_movimiento m
WHERE m.transfer_group_id IS NOT NULL
  AND m.movement_type = 'TrasladoInterGranjaSalida'
  AND m.transfer_group_id NOT IN (
      SELECT DISTINCT transfer_group_id
      FROM public.inventario_gestion_movimiento
      WHERE transfer_group_id IS NOT NULL
        AND movement_type = 'TrasladoInterGranjaEntrada'
  )
  AND s.farm_id = m.farm_id
  AND s.item_inventario_ecuador_id = m.item_inventario_ecuador_id
  AND s.nucleo_id IS NOT DISTINCT FROM m.nucleo_id
  AND s.galpon_id IS NOT DISTINCT FROM m.galpon_id;
  -- Si alguna fila de stock no existe (caso anómalo), UPDATE no afecta esa salida:
  -- revise la vista previa y el inventario antes de COMMIT.

-- 3) Eliminar movimientos pendientes de tránsito (Pendiente + Salida sin Entrada)
DELETE FROM public.inventario_gestion_movimiento m
WHERE m.transfer_group_id IS NOT NULL
  AND m.movement_type IN ('TrasladoInterGranjaPendiente', 'TrasladoInterGranjaSalida')
  AND m.transfer_group_id NOT IN (
      SELECT DISTINCT transfer_group_id
      FROM public.inventario_gestion_movimiento
      WHERE transfer_group_id IS NOT NULL
        AND movement_type = 'TrasladoInterGranjaEntrada'
  );

-- Verifique filas afectadas; si todo es correcto:
COMMIT;
-- o deshacer:
-- ROLLBACK;
*/


-- =============================================================================
-- 4) EJEMPLO: una solicitud concreta (ej. Sacachun 3b → CAROLINA, AV0316, 15 kg)
-- =============================================================================
-- En la fila de tránsito: farm_id = GRANJA ORIGEN, from_farm_id = GRANJA DESTINO.
-- "Solicitud antigua" en pantalla = movement_type = 'TrasladoInterGranjaPendiente'
--   → al borrar NO hace falta sumar stock en origen (no se había descontado).
-- Si fuera 'TrasladoInterGranjaSalida', antes de borrar use el UPDATE del bloque 2)
-- o el script completo de arriba.

-- 4a) Localizar (ajuste nombres de granja si difieren en BD: mayúsculas/espacios)
SELECT
    m.id,
    m.transfer_group_id,
    m.movement_type,
    m.quantity,
    m.created_at,
    fo.name AS granja_origen,
    fd.name AS granja_destino,
    ii.codigo,
    ii.nombre AS item_nombre
FROM public.inventario_gestion_movimiento m
JOIN public.farms fo ON fo.id = m.farm_id
JOIN public.farms fd ON fd.id = m.from_farm_id
JOIN public.item_inventario_ecuador ii ON ii.id = m.item_inventario_ecuador_id
WHERE m.transfer_group_id IS NOT NULL
  AND m.movement_type IN ('TrasladoInterGranjaPendiente', 'TrasladoInterGranjaSalida')
  AND NOT EXISTS (
      SELECT 1 FROM public.inventario_gestion_movimiento e
      WHERE e.transfer_group_id = m.transfer_group_id
        AND e.movement_type = 'TrasladoInterGranjaEntrada'
  )
  AND ii.codigo = 'AV0316'
  AND m.quantity = 15
  AND fo.name ILIKE '%Sacachun%'
  AND fd.name ILIKE '%CAROLINA%'
  AND m.created_at::date = DATE '2026-03-23';
-- Si no devuelve filas: quite el filtro de fecha o use rango:
--   AND m.created_at >= TIMESTAMPTZ '2026-03-23 12:00:00-05'
--   AND m.created_at <  TIMESTAMPTZ '2026-03-23 15:00:00-05'

-- 4b) Borrar SOLO esa fila (sustituya :movimiento_id por el id del SELECT anterior).
--     Para Pendiente antigua no se actualiza inventario_gestion_stock.
/*
BEGIN;
DELETE FROM public.inventario_gestion_movimiento
WHERE id = :movimiento_id
  AND movement_type = 'TrasladoInterGranjaPendiente'
  AND transfer_group_id NOT IN (
      SELECT DISTINCT transfer_group_id FROM public.inventario_gestion_movimiento
      WHERE movement_type = 'TrasladoInterGranjaEntrada' AND transfer_group_id IS NOT NULL
  );
COMMIT;
*/

-- 4c) Alternativa segura por transfer_group_id (un grupo = un movimiento de salida pendiente):
/*
BEGIN;
DELETE FROM public.inventario_gestion_movimiento
WHERE transfer_group_id = 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'::uuid;
COMMIT;
*/
