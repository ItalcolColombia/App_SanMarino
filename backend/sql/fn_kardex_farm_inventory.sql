-- ============================================================================
-- fn_kardex_farm_inventory(...)
-- Kardex (histórico + saldo acumulado) del inventario Colombia (modelo A:
-- farm_inventory_movements). Reemplaza el cómputo en memoria de
-- FarmInventoryReportService.GetKardexAsync (C#, foreach acumulando saldo).
--
-- El servicio ahora DELEGA aquí vía SqlQueryRaw; arma el mismo KardexItemDto sin
-- alterar valores. Idempotente (CREATE OR REPLACE / DROP IF EXISTS). Migración hecha
-- a mano (no altera el ModelSnapshot). Fuente de verdad: FarmInventoryReportService.cs.
--
-- Equivalencia con el C# (replicada EXACTO):
--   * movement_type se persiste como STRING (varchar(20)) con el nombre del enum
--     (Entry/Exit/TransferIn/TransferOut/Adjust). El signo replica el switch C#:
--       Entry, TransferIn      -> +1
--       Exit,  TransferOut     -> -1
--       Adjust                 -> quantity >= 0 ? +1 : -1
--       (cualquier otro)       -> 0   (== el _ => 0m del C#)
--   * delta = sign * quantity;  Cantidad emitida = delta (con signo).
--   * saldo = SUM(delta) OVER (PARTITION BY catalog_item_id ORDER BY created_at, id).
--     El C# ordena solo por created_at (orden indeterminado ante empates); añadir el
--     desempate por id lo hace DETERMINISTA — es una mejora, no una regresión: el saldo
--     FINAL es idéntico (la suma es conmutativa) y solo puede diferir un saldo intermedio
--     cuando dos movimientos comparten exactamente el mismo created_at.
--   * Filtros: farm_id + catalog_item_id siempre; company_id y pais_id cuando el request
--     los aporta (>0); from/to sobre created_at (>= / <=).
--   * Fecha emitida = created_at en UTC (== m.CreatedAt.UtcDateTime del C#): se devuelve
--     (created_at AT TIME ZONE 'UTC') como timestamp sin zona.
--
-- Nota: la validación de pertenencia de la granja a la empresa del usuario (y el retorno
-- vacío si no matchea) se mantiene EN EL SERVICIO (C#), antes de invocar la fn.
-- ============================================================================

-- Signo por tipo de movimiento (== switch C# de FarmInventoryReportService.GetKardexAsync).
-- movement_type es string; Adjust depende del signo de quantity; tipos no mapeados -> 0.
CREATE OR REPLACE FUNCTION fn_kardex_signo(p_movement_type text, p_quantity numeric)
RETURNS numeric LANGUAGE sql IMMUTABLE AS $$
    SELECT CASE p_movement_type
        WHEN 'Entry'       THEN 1
        WHEN 'TransferIn'  THEN 1
        WHEN 'Exit'        THEN -1
        WHEN 'TransferOut' THEN -1
        WHEN 'Adjust'      THEN CASE WHEN p_quantity >= 0 THEN 1 ELSE -1 END
        ELSE 0
    END::numeric;
$$;

CREATE OR REPLACE FUNCTION fn_kardex_farm_inventory(
    p_farm_id         integer,
    p_catalog_item_id integer,
    p_company_id      integer DEFAULT NULL,   -- filtra solo si > 0
    p_pais_id         integer DEFAULT NULL,   -- filtra solo si > 0
    p_from            timestamptz DEFAULT NULL,
    p_to              timestamptz DEFAULT NULL
)
RETURNS TABLE(
    fecha       timestamp,          -- created_at en UTC (== CreatedAt.UtcDateTime)
    tipo        text,               -- movement_type (nombre del enum)
    referencia  text,
    cantidad    numeric,            -- delta con signo (== KardexItemDto.Cantidad)
    unidad      text,
    saldo       numeric,            -- acumulado (window function)
    motivo      text
)
LANGUAGE sql STABLE AS $fn$
    SELECT
        (m.created_at AT TIME ZONE 'UTC')::timestamp                        AS fecha,
        m.movement_type                                                     AS tipo,
        m.reference                                                         AS referencia,
        (fn_kardex_signo(m.movement_type, m.quantity) * m.quantity)         AS cantidad,
        m.unit                                                              AS unidad,
        SUM(fn_kardex_signo(m.movement_type, m.quantity) * m.quantity)
            OVER (PARTITION BY m.catalog_item_id ORDER BY m.created_at, m.id
                  ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)         AS saldo,
        m.reason                                                            AS motivo
    FROM farm_inventory_movements m
    WHERE m.farm_id = p_farm_id
      AND m.catalog_item_id = p_catalog_item_id
      AND (p_company_id IS NULL OR p_company_id <= 0 OR m.company_id = p_company_id)
      AND (p_pais_id    IS NULL OR p_pais_id    <= 0 OR m.pais_id    = p_pais_id)
      AND (p_from IS NULL OR m.created_at >= p_from)
      AND (p_to   IS NULL OR m.created_at <= p_to)
    ORDER BY m.created_at, m.id;
$fn$;
