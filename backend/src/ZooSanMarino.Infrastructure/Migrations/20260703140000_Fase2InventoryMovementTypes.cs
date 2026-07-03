using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Fase 2 (S2) — habilita los movement_type automáticos del descuento Colombia (modelo A):
    ///   * Amplía farm_inventory_movements.movement_type de varchar(20) a varchar(30) para que
    ///     quepa "DevolucionSeguimiento" (21 chars). DDL NO destructivo (sin CHECK en la columna).
    ///   * Actualiza fn_kardex_signo agregando ConsumoSeguimiento(-1) y DevolucionSeguimiento(+1),
    ///     alineada con FarmInventoryKardexCalculos.Signo (golden) y backend/sql/fn_kardex_farm_inventory.sql.
    /// Migración IDEMPOTENTE hecha a mano (ALTER TYPE es no-op si ya es varchar(30);
    /// CREATE OR REPLACE en la fn). Timestamp 20260703140000 (posterior a AddFnKardexFarmInventory).
    /// </summary>
    public partial class Fase2InventoryMovementTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // (1) Ampliar movement_type a varchar(30). Idempotente: ALTER TYPE a un varchar más ancho
            // es seguro y no-op si la columna ya tiene ese tipo. NO usar AlterColumn (no idempotente).
            migrationBuilder.Sql(@"
ALTER TABLE public.farm_inventory_movements
    ALTER COLUMN movement_type TYPE character varying(30);
");

            // (2) fn_kardex_signo con los tipos Fase 2. CREATE OR REPLACE = idempotente.
            // Debe replicar EXACTO el switch de FarmInventoryKardexCalculos.Signo.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_kardex_signo(p_movement_type text, p_quantity numeric)
RETURNS numeric LANGUAGE sql IMMUTABLE AS $$
    SELECT CASE p_movement_type
        WHEN 'Entry'                 THEN 1
        WHEN 'TransferIn'            THEN 1
        WHEN 'Exit'                  THEN -1
        WHEN 'TransferOut'           THEN -1
        WHEN 'Adjust'                THEN CASE WHEN p_quantity >= 0 THEN 1 ELSE -1 END
        WHEN 'ConsumoSeguimiento'    THEN -1   -- Fase 2: consumo automático Colombia
        WHEN 'DevolucionSeguimiento' THEN 1    -- Fase 2: devolución automática Colombia
        ELSE 0
    END::numeric;
$$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revertir fn_kardex_signo a la versión previa (sin los tipos Fase 2).
            migrationBuilder.Sql(@"
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
");

            // Estrechar movement_type de vuelta a varchar(20). Solo seguro si no hay valores > 20 chars
            // (los tipos Fase 2 deben haberse limpiado antes). Se deja por completitud del Down.
            migrationBuilder.Sql(@"
ALTER TABLE public.farm_inventory_movements
    ALTER COLUMN movement_type TYPE character varying(20);
");
        }
    }
}
