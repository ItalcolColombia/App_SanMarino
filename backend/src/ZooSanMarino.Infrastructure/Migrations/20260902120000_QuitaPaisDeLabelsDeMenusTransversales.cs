using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Le quita la palabra «Ecuador» al rótulo de los menús cuyo módulo es <b>transversal</b>
    /// (lo usan varias empresas y varios países). Sólo el <c>label</c>: ni la ruta, ni el
    /// <c>key</c>, ni los permisos, ni <c>role_menus</c>/<c>company_menus</c>.
    /// </summary>
    /// <remarks>
    /// <b>Por qué.</b> Los cuatro módulos que toca nacieron para Ecuador y hoy los comparten
    /// Ecuador, Panamá y Colombia: el catálogo de ítems de inventario, la guía genética (su tabla
    /// tiene <c>pais_id</c> y guarda la guía de todas las empresas, la Ross 308 AP de Panamá
    /// incluida) y el indicador de engorde (su módulo front ya contiene componentes de Panamá).
    /// Un rótulo con el nombre de un país hace dudar de si el módulo es el correcto para el tenant
    /// que lo está mirando — el mismo motivo de
    /// <c>20260901220000_RenombraMenuReporteTecnicoNeutro</c>, del que este pase es la continuación.
    ///
    /// <b>Medido antes de escribirla (2026-09-02, BD local).</b> Los rótulos de esos cuatro menús
    /// <i>ya</i> están neutros acá («Guía Genética Pollo Engorde», «Liquidacion tecnica», «Ítems
    /// inventario», «Gastos de inventario»): lo que lleva el país es el <c>route</c> y el
    /// <c>key</c>, que se tratan aparte porque mueven el enrutado del front. O sea que en local
    /// esta migración es un <b>no-op</b>. Va igual porque no se puede medir producción desde acá y
    /// el costo de que sobre es cero.
    ///
    /// <b>Por qué recorta en vez de asignar un rótulo fijo.</b> Un <c>SET label = 'Guía genética'</c>
    /// pisaría «Guía Genética Pollo Engorde», que es un rótulo mejor y deliberado. El
    /// <c>regexp_replace</c> le saca <i>sólo</i> el país y respeta lo que cada entorno haya
    /// elegido para el resto.
    ///
    /// <b>Localiza por <c>route</c>, jamás por id</b> (los ids de <c>menus</c> difieren
    /// local↔producción), y es <b>idempotente</b>: la guarda <c>ILIKE '%ecuador%'</c> deja de
    /// matchear apenas se aplica, así que una segunda corrida no toca la fila ni mueve
    /// <c>updated_at</c>. Si la fila no existe, no hace nada y no falla.
    ///
    /// <b><c>Down()</c> simétrico.</b> Restaura la constante conocida sólo si el rótulo quedó
    /// exactamente en el valor que produciría este <c>Up()</c>. Así, revertir en un entorno donde
    /// el <c>Up()</c> fue no-op tampoco toca nada.
    /// </remarks>
    public partial class QuitaPaisDeLabelsDeMenusTransversales : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE menus
                   SET label = btrim(regexp_replace(label, '\s*Ecuador\s*', ' ', 'gi'))
                 WHERE route IN (
                           '/config/item-inventario-ecuador',
                           '/config/guia-genetica-ecuador',
                           '/indicador-ecuador',
                           '/inventario-gastos'
                       )
                   AND label ILIKE '%ecuador%';
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE menus SET label = 'Guía genética Ecuador'
                 WHERE route = '/config/guia-genetica-ecuador' AND label = 'Guía genética';

                UPDATE menus SET label = 'Indicador Ecuador'
                 WHERE route = '/indicador-ecuador' AND label = 'Indicador';
            ");
        }
    }
}
