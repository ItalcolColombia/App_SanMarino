using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// fn_seguimiento_diario_produccion v1 — grilla diaria CANÓNICA de producción (postura),
    /// patrón fn_seguimiento_diario_engorde: LANGUAGE sql STABLE (inlineable en LATERAL),
    /// fuente dual + dedup día Bogotá, universo seguimientos ∪ movimientos, saldo de aves
    /// CON error de sexaje (D4) y movimientos filtrados desde fecha_inicio_produccion.
    /// SQL en el partial .Fn.cs, sincronizado con backend/sql/fn_seguimiento_diario_produccion.sql.
    /// Plan: fase_de_desarrollo/seguimiento_produccion_fn_canonica_plan.md
    /// Idempotente: CREATE OR REPLACE.
    /// </summary>
    public partial class AddFnSeguimientoDiarioProduccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DROP previo: CREATE OR REPLACE no puede cambiar la forma del RETURNS TABLE si una
            // versión previa quedó instalada con otra firma de retorno (la fn nace en esta
            // migración, así que el DROP es inocuo e idempotente).
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_seguimiento_diario_produccion(INT, INT);");
            migrationBuilder.Sql(FnSeguimientoDiarioProduccionV1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // v1: no hay versión anterior que restaurar.
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_seguimiento_diario_produccion(INT, INT);");
        }
    }
}
