using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVentaFacturaMermaSobranteEngorde : Migration
    {
        // NOTA (Parte B/C): el diff autogenerado incluía ~144 operaciones espurias por una
        // deriva del ModelSnapshot heredada del merge de Panamá (PR #6): columnas como
        // users.zona, *.qq_*, lesiones, etc. ya existen en la BD (sus migraciones son
        // idempotentes y corrieron). Se conserva el ModelSnapshot regenerado (queda alineado
        // con las entidades = fuente de verdad), pero el Up()/Down() se reescribe a SQL
        // idempotente con SOLO las columnas reales de este requerimiento, para no re-agregar
        // columnas existentes (evita el crash de deploy descrito en CLAUDE.md).

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── lote_ave_engorde: mermas (R1) + sobrante de aves (R2) ──
            migrationBuilder.Sql(@"
                ALTER TABLE public.lote_ave_engorde
                    ADD COLUMN IF NOT EXISTS merma_unidades integer NULL,
                    ADD COLUMN IF NOT EXISTS merma_kilos numeric(18,3) NULL,
                    ADD COLUMN IF NOT EXISTS merma_registrada_at timestamp with time zone NULL,
                    ADD COLUMN IF NOT EXISTS merma_registrada_por_user_id character varying(450) NULL,
                    ADD COLUMN IF NOT EXISTS aves_sobrante integer NOT NULL DEFAULT 0;
            ");

            // ── movimiento_pollo_engorde: factura única (R3.3) + sobrante por movimiento (R2) ──
            migrationBuilder.Sql(@"
                ALTER TABLE public.movimiento_pollo_engorde
                    ADD COLUMN IF NOT EXISTS factura_id uuid NULL,
                    ADD COLUMN IF NOT EXISTS aves_sobrante integer NOT NULL DEFAULT 0;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_mpe_factura_id
                    ON public.movimiento_pollo_engorde (factura_id)
                    WHERE factura_id IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS public.ix_mpe_factura_id;");
            migrationBuilder.Sql(@"
                ALTER TABLE public.movimiento_pollo_engorde
                    DROP COLUMN IF EXISTS factura_id,
                    DROP COLUMN IF EXISTS aves_sobrante;
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE public.lote_ave_engorde
                    DROP COLUMN IF EXISTS merma_unidades,
                    DROP COLUMN IF EXISTS merma_kilos,
                    DROP COLUMN IF EXISTS merma_registrada_at,
                    DROP COLUMN IF EXISTS merma_registrada_por_user_id,
                    DROP COLUMN IF EXISTS aves_sobrante;
            ");
        }
    }
}
