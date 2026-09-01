using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Renombra el menú <c>/reportes-tecnicos</c> de <b>«Reporte Técnico Sanmarino»</b> a
    /// <b>«Reporte Técnico»</b>. Sólo el rótulo: ni la ruta, ni el id, ni los permisos.
    /// </summary>
    /// <remarks>
    /// <b>Por qué.</b> Ese módulo está habilitado en <c>company_menus</c> para varias empresas —no
    /// sólo para Sanmarino—, y desde este pase el reporte se adapta al ciclo y a la clasificación de
    /// huevo de la empresa que lo abre (guía propia, huevo por ítems, etapas por raza). Un rótulo con
    /// el nombre de otra empresa hacía dudar de si el módulo era el correcto para el tenant que lo
    /// estaba mirando; de hecho fue lo primero que confundió al revisar este trabajo.
    ///
    /// <b>Localiza por <c>route</c>, jamás por id.</b> Los ids de <c>menus</c> difieren entre local y
    /// producción (acá es el 19, en otro entorno puede ser cualquiera), así que un <c>WHERE id = 19</c>
    /// renombraría el menú equivocado. La ruta es la clave estable.
    ///
    /// <b>Idempotente y sin ensuciar el histórico.</b> El <c>UPDATE</c> lleva
    /// <c>IS DISTINCT FROM</c>: si el rótulo ya es el nuevo —segunda corrida, o alguien lo cambió a
    /// mano— no toca la fila y no mueve <c>updated_at</c>. Si la fila no existe, no hace nada y no
    /// falla.
    ///
    /// <b><c>Down()</c> exacto.</b> Restaura el rótulo anterior con la misma guarda, así que revertir
    /// tampoco ensucia nada. No se respalda en una tabla auxiliar porque el valor previo es una
    /// constante conocida, no un dato de operación.
    ///
    /// <b>No toca <c>/reporte-tecnico-produccion</c></b> («Reporte Técnico Producción SanMarino»),
    /// que es otro módulo y queda fuera del alcance de este pase.
    /// </remarks>
    public partial class RenombraMenuReporteTecnicoNeutro : Migration
    {
        private const string Ruta      = "/reportes-tecnicos";
        private const string LabelNuevo  = "Reporte Técnico";
        private const string LabelAnterior = "Reporte Técnico Sanmarino";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
                UPDATE menus
                   SET label = '{LabelNuevo}'
                 WHERE route = '{Ruta}'
                   AND label IS DISTINCT FROM '{LabelNuevo}';
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
                UPDATE menus
                   SET label = '{LabelAnterior}'
                 WHERE route = '{Ruta}'
                   AND label IS DISTINCT FROM '{LabelAnterior}';
            ");
        }
    }
}
