using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Cierra en ItalJira todo el trabajo de <b>Santa Reyes</b> que ya está construido, probado y
    /// desplegado: el caso <i>Requerimientos de Italapp</i> con su historia y sus <b>42</b> tareas,
    /// y el caso de las <b>6 definiciones</b> del cliente con sus 6 tareas.
    /// </summary>
    /// <remarks>
    /// <b>Por qué existe.</b> Los dos casos nacieron por seed
    /// (<c>20260819120000_SeedTicketPlanItalappSantaReyes</c> y
    /// <c>20260821190000_SeedTicketDefinicionesPendientesSantaReyes</c>) con el caso en
    /// <c>ABIERTO</c>, las 42 tareas en <c>BACKLOG</c> y las 6 definiciones en <c>BLOQUEADA</c>,
    /// porque en ese momento nada había arrancado. La ejecución completa (F0…F12) se hizo, se probó
    /// y se desplegó entre el 20 y el 31 de agosto de 2026, pero <b>el tablero nunca se movió</b>:
    /// eran los 2 únicos casos en ABIERTO y las 42 únicas tareas en BACKLOG de toda la base.
    ///
    /// <b>Qué NO se entregó, y queda escrito.</b> Tres puntos dependen del cliente y se cierran
    /// dejando constancia en la solución del caso y en la nota de cierre, no en silencio:
    /// <c>F8.1</c>/<c>SR-DEF-3</c> (7 ítems de huevo —4 ENYEMADO y 3 DECOLORADO— existen en el
    /// catálogo <b>sin código ERP</b>: el <c>Items.xlsx</c> del cliente trae 21 ítems y ninguno
    /// Enyemado, mientras el <c>.docx</c> sí lo pide; los dos documentos se contradicen y no se
    /// inventan códigos), <c>F8.3</c>/<c>SR-DEF-4</c> (panel de eficiencia, depende del anterior) y
    /// <c>F11.3</c> (pruebas asistidas con el usuario, fuera del repo).
    ///
    /// <b>Espeja al servicio, no inventa un cierre propio.</b> <c>TicketService.CambiarEstadoAsync</c>
    /// escribe <c>solucion_descripcion</c> + <c>fecha_solucion</c> al marcar SOLUCIONADO, y
    /// <c>ConfirmarCierreAsync</c> escribe <c>fecha_cierre_solicitante</c> +
    /// <c>cerrado_por_user_id</c> al cerrar; cada paso deja su fila en <c>ticket_notas</c>. La
    /// migración escribe exactamente eso, las 4 notas incluidas, porque la línea de tiempo del caso
    /// <b>se deriva</b> de notas + tareas (<c>TicketTimelineCalculos</c>): sin las notas el caso se
    /// vería cerrado sin que nada explique cuándo ni por qué. Las fechas reales de las tareas siguen
    /// la misma regla que <c>TicketTareaCalculos.SellarFechasReales</c> (LISTO sella el fin).
    ///
    /// <b>Fechas deterministas, no <c>now()</c>.</b> El fin real de cada tarea es la fecha en que su
    /// paquete cerró de verdad —21-ago (V52), 24-ago (X18: machos en ventas, comprobante de traslado,
    /// bodega destino) o 31-ago (alias de raza en SQL y tipos de huevo en el alta del lote)—, no la
    /// del deploy. Así el cronograma del tablero no miente y la migración da el mismo resultado en
    /// local y en producción.
    ///
    /// <b>Identidad y fail-open.</b> Nada se referencia por id ni por guid literal (difieren
    /// local↔prod): el administrador se resuelve por email, la empresa por nombre, la historia y los
    /// casos por <c>titulo</c>, las tareas por <c>codigo</c> o por el prefijo <c>F&lt;n&gt;</c> de su
    /// título. Sin el administrador o sin la empresa, <c>RAISE NOTICE</c> + <c>RETURN</c>: con
    /// <c>Database__RunMigrations=true</c> un seed no puede tumbar el arranque de la app.
    ///
    /// <b>Idempotencia.</b> Cada <c>UPDATE</c> filtra con <c>IS DISTINCT FROM</c> y las notas van con
    /// <c>WHERE NOT EXISTS</c> por su texto: correrla dos veces no mueve una sola fila. No toca
    /// ningún caso, historia ni tarea de otra empresa.
    ///
    /// Plan: <c>fase_de_desarrollo/cierre_tickets_santa_reyes_italjira_plan.md</c>.
    /// Migración DATA-ONLY: Designer clonado, ModelSnapshot intacto. El SQL vive en el partial
    /// <c>.Seed.cs</c> por tamaño.
    /// </remarks>
    public partial class CerrarPlanItalappSantaReyes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CIERRE_SQL);
        }

        /// <summary>
        /// Devuelve los dos casos a <c>ABIERTO</c>, la historia y sus 42 tareas a <c>BACKLOG</c> y
        /// las 6 <c>SR-DEF</c> a <c>BLOQUEADA</c>, limpia las fechas reales y de cierre, y borra las
        /// 4 notas que sembró el <c>Up</c>. Localiza todo por el mismo <c>titulo</c>/<c>codigo</c>.
        /// </summary>
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(REVERT_SQL);
        }
    }
}
