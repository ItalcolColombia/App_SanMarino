using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Enciende <c>reportes_alimento_desde_inventario_unificado</c> en <b>Agroavicola Sanmarino</b>
    /// (decisión del usuario, 2026-08-13).
    ///
    /// <para>
    /// <b>Por qué.</b> Sanmarino ya opera sobre el módulo de inventario unificado, pero sus reportes
    /// Contable y Técnico seguían leyendo <c>farm_inventory_movements</c>: esa tabla se quedó en el
    /// <b>2026-07-17</b> (324 filas) mientras <c>inventario_gestion_movimiento</c> llega al
    /// <b>2026-08-13</b> (869). O sea que el reporte venía mostrando <b>de menos</b>, y en las granjas
    /// que nunca escribieron en la tabla vieja mostraba directamente CERO —el
    /// <c>catch { return 0; }</c> del Técnico lo devolvía sin avisar—.
    /// </para>
    ///
    /// <para>
    /// <b>Los números se mueven, y está medido</b> (A/B sobre la copia local del dump de prod,
    /// apagado → encendido → apagado vuelve exacto al baseline):
    /// <list type="bullet">
    /// <item>Contable, lote A374B (granja 20): entradas 2.907 → 2.867 bultos; retiros 2.608,675 → 2.626,975.</item>
    /// <item>Técnico, lote S369B (granja 12): ingresos de alimento <b>0 → 249.860 kg</b> (suma de las
    /// 168 filas diarias) — esa granja no tiene ni una fila en la tabla vieja.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// Localiza por <c>identifier</c> (el NIT, '100063') y no por nombre: el nombre es texto libre y
    /// un espacio o una tilde de más dejarían la migración sin efecto y <b>sin error</b>. Demo,
    /// Ecuador y Panamá no se tocan: siguen leyendo la tabla vieja.
    /// </para>
    ///
    /// Idempotente (<c>IS DISTINCT FROM</c>: no reescribe la fila si ya estaba encendido).
    /// Ordenada DESPUÉS de <c>20260813220000_ReportesAlimentoDesdeInventarioUnificado</c>, que es la
    /// que crea la columna.
    /// </summary>
    public partial class ReportesUnificadoSanmarino : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE public.companies
SET reportes_alimento_desde_inventario_unificado = true
WHERE identifier = '100063'
  AND reportes_alimento_desde_inventario_unificado IS DISTINCT FROM true;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Volver a la tabla vieja es exactamente el estado anterior al encendido.
            migrationBuilder.Sql(@"
UPDATE public.companies
SET reportes_alimento_desde_inventario_unificado = false
WHERE identifier = '100063'
  AND reportes_alimento_desde_inventario_unificado IS DISTINCT FROM false;
");
        }
    }
}
