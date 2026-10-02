using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Límite de semanas del seguimiento diario de LEVANTE por empresa (02-oct-2026; plan
    /// <c>fase_de_desarrollo/levante_hasta_semana_por_empresa_plan.md</c>).
    /// <para>
    /// <c>companies.levante_hasta_semana</c> (integer NULL): última semana de vida del lote que admite
    /// seguimiento diario de levante (hasta su último día); desde la siguiente se rechaza para forzar el
    /// cierre del lote. NULL = sin límite, el comportamiento de siempre para toda empresa. Sin seed: se
    /// configura desde Configuración → Empresas.
    /// </para>
    /// <para>Idempotente: <c>ADD COLUMN IF NOT EXISTS</c>.</para>
    /// </summary>
    public partial class AddLevanteHastaSemana : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE companies ADD COLUMN IF NOT EXISTS levante_hasta_semana integer NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE companies DROP COLUMN IF EXISTS levante_hasta_semana;");
        }
    }
}
