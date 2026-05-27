using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Panamá: agrega cliente_id, zona, certificado_gab, latitud, longitud a farms.
    /// Idempotente — usa IF NOT EXISTS para que conviva con SQL aplicados manualmente
    /// o re-runs sin riesgo de fallar.
    /// </summary>
    public partial class AddPanamaFieldsToFarm : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE public.farms
                    ADD COLUMN IF NOT EXISTS cliente_id      INTEGER          NULL,
                    ADD COLUMN IF NOT EXISTS zona            VARCHAR(20)      NULL,
                    ADD COLUMN IF NOT EXISTS certificado_gab BOOLEAN          NOT NULL DEFAULT FALSE,
                    ADD COLUMN IF NOT EXISTS latitud         NUMERIC(10,7)    NULL,
                    ADD COLUMN IF NOT EXISTS longitud        NUMERIC(10,7)    NULL;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_farms_cliente_id ON public.farms(cliente_id);
                CREATE INDEX IF NOT EXISTS ix_farms_zona       ON public.farms(zona);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS public.ix_farms_cliente_id;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS public.ix_farms_zona;");

            migrationBuilder.Sql(@"
                ALTER TABLE public.farms
                    DROP COLUMN IF EXISTS cliente_id,
                    DROP COLUMN IF EXISTS zona,
                    DROP COLUMN IF EXISTS certificado_gab,
                    DROP COLUMN IF EXISTS latitud,
                    DROP COLUMN IF EXISTS longitud;
            ");
        }
    }
}
