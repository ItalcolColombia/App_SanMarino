using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Panamá: agrega qq_mixtas, qq_hembras, qq_machos (numeric(10,2)) a los seguimientos diarios de:
    ///   • seguimiento_lote_levante (Reproductora R1/R2/R3)
    ///   • seguimiento_diario_aves_engorde (Engorde general)
    ///   • seguimiento_diario_lote_reproductora_aves_engorde (Apoyo)
    ///   • seguimiento_diario_aves_engorde_panama (Engorde Panamá específico)
    /// Idempotente — incluye también la tabla Panamá porque su entity declara los QQ desde antes
    /// pero el snapshot no incluyó esa migración (drift entre el modelo y la BD).
    /// Los campos solo se exponen y editan en el frontend cuando el país activo = PANAMA.
    /// </summary>
    public partial class AddQqFieldsToSeguimientos : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE public.seguimiento_lote_levante
                    ADD COLUMN IF NOT EXISTS qq_mixtas  NUMERIC(10,2) NULL,
                    ADD COLUMN IF NOT EXISTS qq_hembras NUMERIC(10,2) NULL,
                    ADD COLUMN IF NOT EXISTS qq_machos  NUMERIC(10,2) NULL;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE public.seguimiento_diario_aves_engorde
                    ADD COLUMN IF NOT EXISTS qq_mixtas  NUMERIC(10,2) NULL,
                    ADD COLUMN IF NOT EXISTS qq_hembras NUMERIC(10,2) NULL,
                    ADD COLUMN IF NOT EXISTS qq_machos  NUMERIC(10,2) NULL;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE public.seguimiento_diario_lote_reproductora_aves_engorde
                    ADD COLUMN IF NOT EXISTS qq_mixtas  NUMERIC(10,2) NULL,
                    ADD COLUMN IF NOT EXISTS qq_hembras NUMERIC(10,2) NULL,
                    ADD COLUMN IF NOT EXISTS qq_machos  NUMERIC(10,2) NULL;
            ");

            // Sincroniza también la tabla Panamá si existe (en algunos entornos locales no se ha creado todavía).
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                  IF EXISTS (
                    SELECT 1 FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = 'seguimiento_diario_aves_engorde_panama'
                  ) THEN
                    ALTER TABLE public.seguimiento_diario_aves_engorde_panama
                        ADD COLUMN IF NOT EXISTS qq_mixtas  NUMERIC(10,2) NULL,
                        ADD COLUMN IF NOT EXISTS qq_hembras NUMERIC(10,2) NULL,
                        ADD COLUMN IF NOT EXISTS qq_machos  NUMERIC(10,2) NULL;
                  END IF;
                END $$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE public.seguimiento_lote_levante
                    DROP COLUMN IF EXISTS qq_mixtas,
                    DROP COLUMN IF EXISTS qq_hembras,
                    DROP COLUMN IF EXISTS qq_machos;

                ALTER TABLE public.seguimiento_diario_aves_engorde
                    DROP COLUMN IF EXISTS qq_mixtas,
                    DROP COLUMN IF EXISTS qq_hembras,
                    DROP COLUMN IF EXISTS qq_machos;

                ALTER TABLE public.seguimiento_diario_lote_reproductora_aves_engorde
                    DROP COLUMN IF EXISTS qq_mixtas,
                    DROP COLUMN IF EXISTS qq_hembras,
                    DROP COLUMN IF EXISTS qq_machos;

                DO $$
                BEGIN
                  IF EXISTS (
                    SELECT 1 FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = 'seguimiento_diario_aves_engorde_panama'
                  ) THEN
                    ALTER TABLE public.seguimiento_diario_aves_engorde_panama
                        DROP COLUMN IF EXISTS qq_mixtas,
                        DROP COLUMN IF EXISTS qq_hembras,
                        DROP COLUMN IF EXISTS qq_machos;
                  END IF;
                END $$;
            ");
        }
    }
}
