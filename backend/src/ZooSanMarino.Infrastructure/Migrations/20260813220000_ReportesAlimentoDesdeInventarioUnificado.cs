using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Fase D del plan <c>fase_de_desarrollo/santa_reyes_silos_bodegas_inventario_plan.md</c>: los
    /// reportes <b>Contable</b> y <b>Técnico</b> leen el ALIMENTO del módulo de inventario que la
    /// empresa declare.
    ///
    /// <para>
    /// El problema es anterior a los silos y lo destapó el go-live de Santa Reyes: los dos reportes
    /// leen <c>farm_inventory_movements</c> (el módulo VIEJO) y SR no tiene ni una fila ahí —todo su
    /// alimento entra por <c>InventarioGestionController</c>—, así que Entradas / Traslados /
    /// Retiros / Saldo de bultos le salen en CERO, y el <c>catch { return 0; }</c> del Técnico lo
    /// devuelve sin avisar.
    /// </para>
    ///
    /// <para>
    /// La columna es <c>NOT NULL DEFAULT false</c>: con el flag apagado la consulta es exactamente la
    /// de siempre, así que Sanmarino, Demo, Ecuador y Panamá no ven cambiar ni una celda. Se enciende
    /// <b>solo para Santa Reyes</b>, que hoy vería ceros.
    /// </para>
    ///
    /// <para>
    /// ⚠️ Dato para la próxima decisión (medido en la copia local del dump de prod):
    /// <c>farm_inventory_movements</c> de Sanmarino tiene 324 filas y su última es del
    /// <b>2026-07-17</b>, mientras <c>inventario_gestion_movimiento</c> tiene 869 y llega al
    /// <b>2026-08-13</b>. O sea que el reporte de Sanmarino ya está mostrando un mes de menos. No se
    /// enciende acá porque cambia números que alguien concilió: va con verificación explícita.
    /// </para>
    ///
    /// Idempotente (<c>IF NOT EXISTS</c> + <c>UPDATE ... IS DISTINCT FROM</c>). Escrita a mano.
    /// </summary>
    public partial class ReportesAlimentoDesdeInventarioUnificado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.companies
    ADD COLUMN IF NOT EXISTS reportes_alimento_desde_inventario_unificado boolean NOT NULL DEFAULT false;
");

            // Seed: solo Santa Reyes. El `IS DISTINCT FROM` evita tocar la fila (y su updated_at) si
            // ya estaba en true — la migración se puede volver a correr sin ensuciar el histórico.
            migrationBuilder.Sql(@"
UPDATE public.companies
SET reportes_alimento_desde_inventario_unificado = true
WHERE name = 'Santa Reyes'
  AND reportes_alimento_desde_inventario_unificado IS DISTINCT FROM true;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.companies DROP COLUMN IF EXISTS reportes_alimento_desde_inventario_unificado;
");
        }
    }
}
