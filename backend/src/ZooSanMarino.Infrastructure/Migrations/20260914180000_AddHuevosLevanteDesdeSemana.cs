using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Huevos en LEVANTE desde una semana configurable por empresa (capacitación Santa Reyes,
    /// 14-sep-2026; plan <c>fase_de_desarrollo/huevos_levante_por_items_santa_reyes_plan.md</c>).
    /// <para>
    /// <c>companies.huevos_levante_desde_semana</c> (integer NULL): semana de vida desde la que el
    /// seguimiento diario de levante captura huevos. NULL = sin límite, el comportamiento de siempre
    /// para toda empresa; Santa Reyes = 18.
    /// </para>
    /// <para>
    /// Idempotente: <c>ADD COLUMN IF NOT EXISTS</c> + <c>UPDATE … IS DISTINCT FROM</c>. Ordena después
    /// del seed que crea la empresa (<c>20260725190000_SeedEmpresaSantaReyes</c>).
    /// </para>
    /// </summary>
    public partial class AddHuevosLevanteDesdeSemana : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE companies ADD COLUMN IF NOT EXISTS huevos_levante_desde_semana integer NULL;");

            migrationBuilder.Sql(@"
UPDATE public.companies
   SET huevos_levante_desde_semana = 18
 WHERE name = 'Santa Reyes'
   AND huevos_levante_desde_semana IS DISTINCT FROM 18;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE companies DROP COLUMN IF EXISTS huevos_levante_desde_semana;");
        }
    }
}
