using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// D1 (espejo de huevos, opción b): el recálculo C# absoluto
    /// (EspejoHuevoProduccionSyncService.RecalcularEspejoHuevoProduccionAsync) es el ÚNICO dueño de
    /// espejo_huevo_produccion. Se retira el trigger legacy tr_espejo_huevo_produccion_aiud, que:
    ///  • vive sobre seguimiento_diario_levante (herencia de renames) y está MUERTO en el flujo
    ///    vivo (0 filas tipo_seguimiento='produccion' en esa tabla, verificado en prod-dump);
    ///  • era una trampa armada de doble conteo (si algo volvía a escribir tipo='produccion' ahí,
    ///    sumaba al espejo y el próximo recálculo absoluto lo pisaba — ping-pong de valores);
    ///  • tenía bugs propios: fallback company := 1 hardcodeado, rama UPDATE sin historico_semanal
    ///    ni manejo de cambio de LPP.
    /// historico_semanal (jsonb) queda como columna muerta documentada (vacía en el 100 % de las
    /// filas, cero lectores en back/front/sql); su DROP sería una migración aparte con OK explícito.
    /// Idempotente: DROP ... IF EXISTS.
    /// </summary>
    public partial class RetirarTriggerEspejoHuevoProduccionLegacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS tr_espejo_huevo_produccion_aiud ON public.seguimiento_diario_levante;
                DROP FUNCTION IF EXISTS public.fn_espejo_huevo_produccion_upsert();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op deliberado: restaurar el trigger legacy reinstalaría la trampa de doble conteo.
            // La definición histórica queda en backend/sql/trigger_espejo_huevo_produccion_seguimiento_diario.sql
            // (marcada como RETIRADA) por si un rollback manual la necesitara.
        }
    }
}
