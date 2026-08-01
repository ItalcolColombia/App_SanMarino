using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// fn_seguimiento_diario_produccion v2: las filas de traslado TSD (lote_postura_produccion_id
    /// NULL, matching por lote base) se vuelven VISIBLES en la rama LPP de la grilla, marcadas con
    /// la columna nueva fila_sin_lpp. Las 3 fns semanales las EXCLUYEN explícitamente
    /// (AND NOT fila_sin_lpp) — su salida queda byte a byte idéntica (un día solo-traslado no es
    /// «día con registro» para los indicadores, igual que antes). El saldo no cambia: esas filas
    /// traen mort/sel/err = 0 y el movimiento ya entra por movimiento_aves.
    /// La firma de retorno de la fn diaria cambia ⇒ DROP + CREATE (no alcanza CREATE OR REPLACE).
    /// Down() restaura las 4 versiones anteriores verbatim.
    /// </summary>
    public partial class FnSeguimientoProduccionV2FilasTsdVisibles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_seguimiento_diario_produccion(INT, INT);");
            migrationBuilder.Sql(FnDiariaV2);
            migrationBuilder.Sql(FnIndicadoresV2);
            migrationBuilder.Sql(FnClasificacionV2);
            migrationBuilder.Sql(FnResumenV2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_seguimiento_diario_produccion(INT, INT);");
            migrationBuilder.Sql(FnDiariaPrev);
            migrationBuilder.Sql(FnIndicadoresPrev);
            migrationBuilder.Sql(FnClasificacionPrev);
            migrationBuilder.Sql(FnResumenPrev);
        }
    }
}
