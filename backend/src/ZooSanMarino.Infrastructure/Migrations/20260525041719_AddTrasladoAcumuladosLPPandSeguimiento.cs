using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Feature 14 — paridad con Levante en el módulo Producción.
    ///
    /// Aplica las columnas dedicadas de traslado en:
    ///   • lote_postura_produccion: 4 acumulados (traslado_ingreso/salida h/m).
    ///   • produccion_seguimiento: 4 splits H/M dedicados + flags + auditoría.
    /// Idempotente — usa IF NOT EXISTS para que conviva con SQL aplicados manualmente.
    /// </summary>
    public partial class AddTrasladoAcumuladosLPPandSeguimiento : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // === LotePosturaProduccion: acumulados ===
            migrationBuilder.Sql(@"
                ALTER TABLE public.lote_postura_produccion
                    ADD COLUMN IF NOT EXISTS traslado_ingreso_hembras INTEGER NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS traslado_ingreso_machos  INTEGER NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS traslado_salida_hembras  INTEGER NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS traslado_salida_machos   INTEGER NOT NULL DEFAULT 0;
            ");

            // === ProduccionSeguimiento: splits H/M + flags + sel/err + auditoría ===
            migrationBuilder.Sql(@"
                ALTER TABLE public.produccion_seguimiento
                    ADD COLUMN IF NOT EXISTS traslado_ingreso_hembras INTEGER NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS traslado_ingreso_machos  INTEGER NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS traslado_salida_hembras  INTEGER NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS traslado_salida_machos   INTEGER NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS es_traslado                    BOOLEAN     NOT NULL DEFAULT FALSE,
                    ADD COLUMN IF NOT EXISTS traslado_lote_contraparte_id   INTEGER     NULL,
                    ADD COLUMN IF NOT EXISTS traslado_granja_contraparte_id INTEGER     NULL,
                    ADD COLUMN IF NOT EXISTS traslado_direccion             VARCHAR(10) NULL,
                    ADD COLUMN IF NOT EXISTS sel_h                INTEGER NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS sel_m                INTEGER NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS error_sexaje_hembras INTEGER NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS error_sexaje_machos  INTEGER NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS updated_by_user_id   INTEGER NULL;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS idx_produccion_seguimiento_es_traslado
                    ON public.produccion_seguimiento(es_traslado)
                    WHERE es_traslado = TRUE;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                  IF NOT EXISTS (
                    SELECT 1 FROM information_schema.table_constraints
                    WHERE constraint_name = 'chk_produccion_seguimiento_traslado_direccion'
                  ) THEN
                    ALTER TABLE public.produccion_seguimiento
                      ADD CONSTRAINT chk_produccion_seguimiento_traslado_direccion
                      CHECK (traslado_direccion IS NULL OR traslado_direccion IN ('SALIDA', 'INGRESO'));
                  END IF;
                END $$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS public.idx_produccion_seguimiento_es_traslado;");

            migrationBuilder.Sql(@"
                ALTER TABLE public.produccion_seguimiento
                    DROP CONSTRAINT IF EXISTS chk_produccion_seguimiento_traslado_direccion;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE public.produccion_seguimiento
                    DROP COLUMN IF EXISTS traslado_ingreso_hembras,
                    DROP COLUMN IF EXISTS traslado_ingreso_machos,
                    DROP COLUMN IF EXISTS traslado_salida_hembras,
                    DROP COLUMN IF EXISTS traslado_salida_machos,
                    DROP COLUMN IF EXISTS es_traslado,
                    DROP COLUMN IF EXISTS traslado_lote_contraparte_id,
                    DROP COLUMN IF EXISTS traslado_granja_contraparte_id,
                    DROP COLUMN IF EXISTS traslado_direccion,
                    DROP COLUMN IF EXISTS sel_h,
                    DROP COLUMN IF EXISTS sel_m,
                    DROP COLUMN IF EXISTS error_sexaje_hembras,
                    DROP COLUMN IF EXISTS error_sexaje_machos,
                    DROP COLUMN IF EXISTS updated_by_user_id;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE public.lote_postura_produccion
                    DROP COLUMN IF EXISTS traslado_ingreso_hembras,
                    DROP COLUMN IF EXISTS traslado_ingreso_machos,
                    DROP COLUMN IF EXISTS traslado_salida_hembras,
                    DROP COLUMN IF EXISTS traslado_salida_machos;
            ");
        }
    }
}
