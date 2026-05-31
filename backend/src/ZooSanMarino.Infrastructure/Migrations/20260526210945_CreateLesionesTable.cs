using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Panamá: crea tabla lesiones (un registro por lesión observada en un lote).
    /// Una sola tabla cubre los tres módulos (REPRODUCTORA/APOYO/ENGORDE) vía la
    /// columna modulo_origen. Idempotente — CREATE TABLE IF NOT EXISTS + índices IF NOT EXISTS.
    /// </summary>
    public partial class CreateLesionesTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS public.lesiones (
                    id                    BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    cliente_id            INTEGER          NULL,
                    farm_id               INTEGER          NOT NULL,
                    galpon_id             VARCHAR(50)      NULL,
                    lote_id               INTEGER          NULL,
                    lote_reproductora_id  VARCHAR(50)      NULL,
                    edad_dias             INTEGER          NULL,
                    aves_macho            INTEGER          NULL,
                    aves_hembra           INTEGER          NULL,
                    aves_mixtas           INTEGER          NULL,
                    tipo_lesion           VARCHAR(120)     NOT NULL,
                    observaciones         TEXT             NULL,
                    fecha_registro        TIMESTAMPTZ      NOT NULL DEFAULT timezone('utc', now()),
                    modulo_origen         VARCHAR(20)      NOT NULL,
                    status                VARCHAR(1)       NOT NULL DEFAULT 'A',
                    company_id            INTEGER          NOT NULL,
                    created_by_user_id    INTEGER          NOT NULL,
                    created_at            TIMESTAMPTZ      NOT NULL DEFAULT timezone('utc', now()),
                    updated_by_user_id    INTEGER          NULL,
                    updated_at            TIMESTAMPTZ      NULL,
                    deleted_at            TIMESTAMPTZ      NULL
                );
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_lesiones_cliente_id     ON public.lesiones(cliente_id);
                CREATE INDEX IF NOT EXISTS ix_lesiones_company_id     ON public.lesiones(company_id);
                CREATE INDEX IF NOT EXISTS ix_lesiones_farm_id        ON public.lesiones(farm_id);
                CREATE INDEX IF NOT EXISTS ix_lesiones_fecha_registro ON public.lesiones(fecha_registro);
                CREATE INDEX IF NOT EXISTS ix_lesiones_lote_id        ON public.lesiones(lote_id);
                CREATE INDEX IF NOT EXISTS ix_lesiones_modulo_origen  ON public.lesiones(modulo_origen);
            ");

            // Restricción de valores válidos para modulo_origen (CHECK constraint idempotente)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                  IF NOT EXISTS (
                    SELECT 1 FROM information_schema.table_constraints
                    WHERE constraint_name = 'chk_lesiones_modulo_origen'
                  ) THEN
                    ALTER TABLE public.lesiones
                      ADD CONSTRAINT chk_lesiones_modulo_origen
                      CHECK (modulo_origen IN ('REPRODUCTORA', 'APOYO', 'ENGORDE'));
                  END IF;
                END $$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS public.lesiones;");
        }
    }
}
