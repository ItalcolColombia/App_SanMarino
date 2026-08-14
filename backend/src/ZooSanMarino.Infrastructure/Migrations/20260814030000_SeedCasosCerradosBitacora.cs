using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Publica la bitácora de julio-agosto 2026 en el módulo de <b>Tickets</b>: un CASO por cada
    /// trabajo, en estado <c>CERRADO</c>, con su descripción de la solución. La migración anterior
    /// (<c>20260814010000</c>) dejó ese trabajo como historias y tareas de ItalJira; eso no aparece
    /// en «Mis solicitudes», ni en la Bandeja de gestión, ni suma en el panel de indicadores,
    /// porque esas tres pantallas leen <c>tickets</c>, no <c>ticket_tareas</c>.
    /// </summary>
    /// <remarks>
    /// <b>Qué crea:</b> 137 casos — <b>135 CERRADO</b> y <b>2 EN_ANALISIS</b>. Un caso solo se da
    /// por cerrado si su tarea quedó en LISTO; las dos sesiones que terminaron en análisis entran
    /// abiertas y sin fecha de solución. Cerrar lo que no se cerró sería justo el dato falso que
    /// esta bitácora trata de evitar.
    ///
    /// <b>Reparto del contenido:</b> <c>descripcion</c> lleva el pedido textual del usuario y
    /// <c>solucion_descripcion</c> lleva qué se hizo (los commits), los bugs encontrados, la
    /// evidencia y la estimación. Es la «fase de solución» que se pidió, en el campo que el módulo
    /// ya tiene para eso.
    ///
    /// <b>Enlace con ItalJira:</b> cada caso adopta su tarea y las subtareas BUG de esa tarea
    /// (<c>ticket_tareas.ticket_id</c>), así el mismo trabajo se ve desde los dos lados en vez de
    /// quedar duplicado. El caso hereda además la <c>historia_id</c> de la tarea.
    ///
    /// <b>Correlativo:</b> <c>TK-2026-NNNNNN</c> continúa desde el máximo que exista en la base —
    /// local y producción no están en el mismo número, así que no se puede hardcodear. El
    /// <c>orden_tablero</c> sigue al último de la columna CERRADO.
    ///
    /// <b>No manda un solo correo:</b> es SQL, no pasa por el servicio de notificación;
    /// <c>notificado_correo</c> queda en false y <c>correo_notificado_a</c> en NULL.
    ///
    /// <b>Sin SLA:</b> <c>fecha_limite</c> queda NULL a propósito — estos casos no tuvieron
    /// compromiso de fecha y no deben ensuciar el semáforo de SLA de julio-agosto.
    ///
    /// <b>Idempotencia:</b> el INSERT toma la tarea solo si todavía tiene <c>ticket_id IS NULL</c>,
    /// y el enlace se escribe en el mismo bloque. En la segunda pasada no entra ninguna.
    ///
    /// <b>Identidad y fail-open:</b> igual que el resto de los seeds del módulo — usuario por email
    /// (<c>moiesbbuga@gmail.com</c>), y si no existe, <c>RAISE NOTICE</c> + <c>RETURN</c>.
    ///
    /// Migración DATA-ONLY: Designer clonado, ModelSnapshot intacto.
    /// </remarks>
    public partial class SeedCasosCerradosBitacora : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SEED_SQL);
        }

        /// <summary>
        /// Borra solo los casos de esta migración (los reconoce por la marca de la descripción).
        /// <b>El desenlace va primero:</b> <c>fk_ticket_tareas_tickets_ticket_id</c> es
        /// <c>ON DELETE CASCADE</c>, así que borrar los casos con las tareas todavía colgando se
        /// llevaría por delante las 137 tareas de ItalJira y sus 99 subtareas BUG.
        /// </summary>
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DOWN_SQL);
        }
    }
}
