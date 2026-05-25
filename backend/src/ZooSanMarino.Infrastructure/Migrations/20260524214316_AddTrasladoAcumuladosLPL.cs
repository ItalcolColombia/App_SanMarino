using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrasladoAcumuladosLPL : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // === Feature 13 — lote_postura_levante: acumulados de traslado ===
            migrationBuilder.Sql(@"
                ALTER TABLE public.lote_postura_levante
                    ADD COLUMN IF NOT EXISTS traslado_ingreso_hembras INTEGER NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS traslado_ingreso_machos  INTEGER NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS traslado_salida_hembras  INTEGER NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS traslado_salida_machos   INTEGER NOT NULL DEFAULT 0;
            ");

            // === Feature 13 — seguimiento_diario_levante_reproductoras: marcado traslado ===
            migrationBuilder.Sql(@"
                ALTER TABLE public.seguimiento_diario_levante_reproductoras
                    ADD COLUMN IF NOT EXISTS es_traslado                    BOOLEAN     NOT NULL DEFAULT FALSE,
                    ADD COLUMN IF NOT EXISTS traslado_lote_contraparte_id   INTEGER     NULL,
                    ADD COLUMN IF NOT EXISTS traslado_granja_contraparte_id INTEGER     NULL,
                    ADD COLUMN IF NOT EXISTS traslado_direccion             VARCHAR(10) NULL;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS idx_seguimiento_diario_lev_es_traslado
                    ON public.seguimiento_diario_levante_reproductoras(es_traslado)
                    WHERE es_traslado = TRUE;
            ");

            // CHECK constraint para asegurar valor válido en traslado_direccion (idempotente)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                  IF NOT EXISTS (
                    SELECT 1 FROM information_schema.table_constraints
                    WHERE constraint_name = 'chk_seg_diario_lev_traslado_direccion'
                  ) THEN
                    ALTER TABLE public.seguimiento_diario_levante_reproductoras
                      ADD CONSTRAINT chk_seg_diario_lev_traslado_direccion
                      CHECK (traslado_direccion IS NULL OR traslado_direccion IN ('SALIDA', 'INGRESO'));
                  END IF;
                END $$;
            ");

            // === Feature 12 (catch-up para entornos antiguos) — lote_postura_base ===
            migrationBuilder.Sql(@"
                ALTER TABLE public.lote_postura_base
                    ADD COLUMN IF NOT EXISTS erp_create DATE     NULL,
                    ADD COLUMN IF NOT EXISTS farm_id    INTEGER  NULL;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_lote_postura_base_farm_id
                    ON public.lote_postura_base(farm_id);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS public.idx_seguimiento_diario_lev_es_traslado;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS public.ix_lote_postura_base_farm_id;");

            migrationBuilder.Sql(@"
                ALTER TABLE public.seguimiento_diario_levante_reproductoras
                    DROP CONSTRAINT IF EXISTS chk_seg_diario_lev_traslado_direccion;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE public.seguimiento_diario_levante_reproductoras
                    DROP COLUMN IF EXISTS es_traslado,
                    DROP COLUMN IF EXISTS traslado_lote_contraparte_id,
                    DROP COLUMN IF EXISTS traslado_granja_contraparte_id,
                    DROP COLUMN IF EXISTS traslado_direccion;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE public.lote_postura_levante
                    DROP COLUMN IF EXISTS traslado_ingreso_hembras,
                    DROP COLUMN IF EXISTS traslado_ingreso_machos,
                    DROP COLUMN IF EXISTS traslado_salida_hembras,
                    DROP COLUMN IF EXISTS traslado_salida_machos;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE public.lote_postura_base
                    DROP COLUMN IF EXISTS erp_create,
                    DROP COLUMN IF EXISTS farm_id;
            ");
        }
    }
}
