using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Flag tipado <c>companies.semanas_ciclo_postura_por_raza</c> (F3 del plan Italapp Santa Reyes,
    /// ver §6 de <c>fase_de_desarrollo/santa_reyes_requerimientos_italapp_plan.md</c>): la etapa del
    /// ciclo de vida del ave (alistamiento/levante/levante en producción/postura) se calcula por
    /// semana y por raza en vez de los cortes fijos 26-33/34-50/&gt;50.
    /// <para>
    /// Se enciende SOLO para Santa Reyes (sembrada por <c>20260725190000_SeedEmpresaSantaReyes</c>,
    /// que corre antes) — esta misma migración construye la lógica que lo consume
    /// (<c>SemanasCicloPosturaCalculos</c>), así que no queda un toggle sin efecto.
    /// Idempotente (ADD COLUMN IF NOT EXISTS + UPDATE con guarda): re-ejecutable sin romper.
    /// </para>
    /// </summary>
    public partial class AddFlagSemanasCicloPosturaPorRaza : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE companies ADD COLUMN IF NOT EXISTS semanas_ciclo_postura_por_raza boolean NOT NULL DEFAULT false;");

            migrationBuilder.Sql(@"
UPDATE public.companies
   SET semanas_ciclo_postura_por_raza = true
 WHERE name = 'Santa Reyes'
   AND semanas_ciclo_postura_por_raza IS DISTINCT FROM true;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE companies DROP COLUMN IF EXISTS semanas_ciclo_postura_por_raza;");
        }
    }
}
