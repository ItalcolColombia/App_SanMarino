using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Migración DATA-ONLY: enciende <c>companies.programacion_lotes_engorde</c> para
    /// <b>ItalcolEcuador</b> — el pedido de Ecuador (programar los lotes del año y dar de baja los
    /// insumos de desinsectación contra un lote que todavía no está activo).
    /// <para>
    /// ⚠️ <b>Va separada del seed de Panamá a propósito.</b> Con el flag encendido el lote base es
    /// OBLIGATORIO al crear un lote: si Ecuador todavía no cargó la programación del año (al medir,
    /// tenía <b>0 lotes base</b> contra 121 lotes creados a mano por los técnicos), los técnicos no
    /// pueden crear lotes. Aplicar sólo cuando la programación esté cargada y asignada a sus granjas;
    /// el <c>Down</c> la apaga sin tocar nada más.
    /// </para>
    /// <para>
    /// Idempotente: lookup por <c>name</c> (los ids difieren local↔prod) y <c>IS DISTINCT FROM</c>.
    /// No toca lotes ni gastos existentes: sólo cambia el comportamiento de las creaciones nuevas.
    /// </para>
    /// </summary>
    public partial class SeedProgramacionLotesEngordeEcuador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE companies
                   SET programacion_lotes_engorde = true
                 WHERE name = 'ItalcolEcuador'
                   AND programacion_lotes_engorde IS DISTINCT FROM true;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE companies
                   SET programacion_lotes_engorde = false
                 WHERE name = 'ItalcolEcuador'
                   AND programacion_lotes_engorde IS DISTINCT FROM false;
            ");
        }
    }
}
