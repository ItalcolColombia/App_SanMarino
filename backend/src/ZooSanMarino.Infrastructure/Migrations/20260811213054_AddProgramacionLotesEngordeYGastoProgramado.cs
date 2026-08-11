using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Programación de lotes de pollo engorde:
    /// <list type="bullet">
    /// <item><c>companies.programacion_lotes_engorde</c> — flag tipado por comportamiento: el lote base
    /// (asignado por granja) es obligatorio y da el nombre del lote, y el gasto de inventario puede
    /// cargarse contra un lote PROGRAMADO que todavía no existe.</item>
    /// <item><c>inventario_gasto.lote_base_engorde_id</c> — destino «lote programado» del gasto
    /// (desinsectación previa al encaset), excluyente con <c>lote_ave_engorde_id</c>.</item>
    /// </list>
    /// <para>
    /// Todo idempotente (<c>IF NOT EXISTS</c> / <c>DO $$</c>) porque el deploy corre las migraciones al
    /// arrancar y el entorno local puede tener parte ya aplicada. Default <c>false</c> ⇒ ninguna empresa
    /// cambia de comportamiento con esta migración; el encendido va en los seeds.
    /// </para>
    /// </summary>
    public partial class AddProgramacionLotesEngordeYGastoProgramado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE companies
                  ADD COLUMN IF NOT EXISTS programacion_lotes_engorde boolean NOT NULL DEFAULT false;

                ALTER TABLE public.inventario_gasto
                  ADD COLUMN IF NOT EXISTS lote_base_engorde_id integer NULL;

                CREATE INDEX IF NOT EXISTS ix_inventario_gasto_lote_base_engorde_id
                    ON public.inventario_gasto (lote_base_engorde_id);

                -- Los ""pendientes"": gastos de una programación que todavía no tiene lote real. Es el
                -- conjunto que barre la re-atribución al crear el lote.
                CREATE INDEX IF NOT EXISTS ix_inventario_gasto_lote_base_pendiente
                    ON public.inventario_gasto (company_id, lote_base_engorde_id, farm_id)
                 WHERE lote_base_engorde_id IS NOT NULL AND lote_ave_engorde_id IS NULL;

                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                         WHERE conname = 'fk_inventario_gasto_lote_base_engorde_lote_base_engorde_id'
                    ) THEN
                        ALTER TABLE public.inventario_gasto
                          ADD CONSTRAINT fk_inventario_gasto_lote_base_engorde_lote_base_engorde_id
                          FOREIGN KEY (lote_base_engorde_id)
                          REFERENCES public.lote_base_engorde (id) ON DELETE RESTRICT;
                    END IF;

                    -- Un gasto cuelga de un lote REAL o de uno PROGRAMADO, nunca de los dos: la
                    -- re-atribución limpia lote_base_engorde_id al asignar el lote real, así que la
                    -- BD puede exigirlo y el invariante no depende de que cada service se acuerde.
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                         WHERE conname = 'ck_inventario_gasto_lote_real_xor_programado'
                    ) THEN
                        ALTER TABLE public.inventario_gasto
                          ADD CONSTRAINT ck_inventario_gasto_lote_real_xor_programado
                          CHECK (NOT (lote_ave_engorde_id IS NOT NULL AND lote_base_engorde_id IS NOT NULL));
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE public.inventario_gasto
                    DROP CONSTRAINT IF EXISTS ck_inventario_gasto_lote_real_xor_programado,
                    DROP CONSTRAINT IF EXISTS fk_inventario_gasto_lote_base_engorde_lote_base_engorde_id;

                DROP INDEX IF EXISTS public.ix_inventario_gasto_lote_base_pendiente;
                DROP INDEX IF EXISTS public.ix_inventario_gasto_lote_base_engorde_id;

                ALTER TABLE public.inventario_gasto DROP COLUMN IF EXISTS lote_base_engorde_id;
                ALTER TABLE companies DROP COLUMN IF EXISTS programacion_lotes_engorde;
            ");
        }
    }
}
