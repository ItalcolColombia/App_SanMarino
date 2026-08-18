using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Quita la clave foránea de <c>seguimiento_reserva_alimento.item_inventario_ecuador_id</c> hacia
    /// <c>item_inventario_ecuador</c>.
    /// </summary>
    /// <remarks>
    /// <b>La columna es POLIMÓRFICA y la FK lo negaba.</b> La entidad ya lo documentaba: el id vive en
    /// <c>item_inventario_ecuador</c> cuando <c>es_item_inventario = true</c> (camino 2, Ecuador y
    /// Panamá) y en <c>catalogo_items</c> cuando es <c>false</c> (camino 1, Colombia). Los dos rangos
    /// de id se solapan a propósito —por eso existe el discriminador—, así que una FK a una sola de
    /// las dos tablas es incorrecta por construcción.
    ///
    /// <para>
    /// <b>Qué rompía.</b> En la base local hay 435 <c>catalogo_items</c> y 208 no existen como
    /// <c>item_inventario_ecuador.id</c>. Con la doble validación encendida, separar el consumo de un
    /// seguimiento de levante o de producción de una empresa de Colombia insertaba ese
    /// <c>catalogo_items.id</c> en la columna y la FK lo rechazaba: <c>SepararAsync</c> se caía y
    /// guardar el seguimiento devolvía 500. Los otros 227 pasaban la FK apuntando a un ítem sin
    /// relación —benigno solo porque <c>es_item_inventario</c> se persiste y el aplicador resuelve
    /// A→B por código al validar—.
    /// </para>
    ///
    /// <para>
    /// No se detectó antes porque la única empresa con el flag encendido (ItalcolPanama) opera camino
    /// 2: manda el mismo id por los dos campos y la FK siempre casaba. El agujero estaba justo en las
    /// empresas de postura, que son las de Colombia.
    /// </para>
    ///
    /// <para>
    /// <b>No se reemplaza por otra FK</b>: la integridad de un id polimórfico no la da una restricción
    /// de columna, la da el discriminador más la resolución por código del aplicador.
    /// </para>
    /// </remarks>
    public partial class QuitarFkPolimorficaReservaAlimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotentes: en entornos donde la tabla se creó sin la FK, el DROP normal abortaría.
            migrationBuilder.Sql(@"
ALTER TABLE public.seguimiento_reserva_alimento
    DROP CONSTRAINT IF EXISTS fk_seguimiento_reserva_alimento_item_inventario_ecuador_item_i;
DROP INDEX IF EXISTS public.ix_seguimiento_reserva_alimento_item_inventario_ecuador_id;
");
            // El índice compuesto por ubicación+ítem+estado NO se toca: es el que sirve al disponible.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Solo se restaura el índice. La FK NO vuelve: reponerla sería reintroducir el defecto
            // —rechazaría otra vez toda separación de Colombia—.
            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_seguimiento_reserva_alimento_item_inventario_ecuador_id
    ON public.seguimiento_reserva_alimento (item_inventario_ecuador_id);
");
        }
    }
}
