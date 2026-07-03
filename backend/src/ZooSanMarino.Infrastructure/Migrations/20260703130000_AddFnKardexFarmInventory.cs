using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Crea fn_kardex_farm_inventory (+ helper fn_kardex_signo): saldo acumulado del kardex
    /// del inventario Colombia (modelo A, farm_inventory_movements) calculado en la BD con
    /// window function, para que FarmInventoryReportService.GetKardexAsync delegue el cálculo
    /// (SqlQueryRaw) en vez de acumular con un foreach en memoria.
    /// Idempotente (CREATE OR REPLACE / DROP IF EXISTS). Migración hecha a mano (no altera el
    /// ModelSnapshot). Fuente/spec: backend/sql/fn_kardex_farm_inventory.sql.
    /// </summary>
    public partial class AddFnKardexFarmInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- ============================================================================
-- fn_kardex_farm_inventory(...) — kardex + saldo acumulado del inventario Colombia.
-- Reemplaza el foreach en memoria de FarmInventoryReportService.GetKardexAsync.
-- Replica EXACTO la aritmética C#:
--   * movement_type es STRING (varchar(20)); signo por CASE (== switch C#):
--       Entry, TransferIn -> +1 ; Exit, TransferOut -> -1 ;
--       Adjust -> quantity>=0 ? +1 : -1 ; (otro) -> 0
--   * saldo = SUM(delta) OVER (PARTITION BY catalog_item_id ORDER BY created_at, id).
--     La fn ya filtra farm_id, así que la partición por catalog_item_id equivale a
--     (farm_id, catalog_item_id). Desempate por id => determinista (mejora; saldo final igual).
--   * Fecha = created_at en UTC (== CreatedAt.UtcDateTime).
--   * Filtros company/pais opcionales (>0); from/to sobre created_at.
-- Golden de equivalencia verificado contra el C# para toda la data Colombia local (18/18 pares, 0 diferencias).
-- Fuente de verdad: FarmInventoryReportService.cs.
-- ============================================================================

-- Signo por tipo de movimiento (== switch C#). movement_type es string; Adjust depende del
-- signo de quantity; tipos no mapeados -> 0.
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
    p_company_id      integer DEFAULT NULL,
    p_pais_id         integer DEFAULT NULL,
    p_from            timestamptz DEFAULT NULL,
    p_to              timestamptz DEFAULT NULL
)
RETURNS TABLE(
    fecha       timestamp,
    tipo        text,
    referencia  text,
    cantidad    numeric,
    unidad      text,
    saldo       numeric,
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
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP FUNCTION IF EXISTS fn_kardex_farm_inventory(integer, integer, integer, integer, timestamptz, timestamptz);
DROP FUNCTION IF EXISTS fn_kardex_signo(text, numeric);
");
        }
    }
}
