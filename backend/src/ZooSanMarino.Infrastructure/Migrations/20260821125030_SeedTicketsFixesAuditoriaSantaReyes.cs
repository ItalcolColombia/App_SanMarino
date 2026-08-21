using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Registra dos casos de soporte ya <b>SOLUCIONADOS y CERRADOS</b>, encontrados durante la
    /// auditoría de no-regresión de Santa Reyes sobre el módulo de postura (21 de agosto de 2026):
    /// (1) el total de un traslado de huevos quedaba desactualizado al editar sus cantidades antes
    /// de completarlo, y (2) el Reporte Técnico de Producción consultaba la guía genética
    /// compartida sin filtrar por empresa.
    /// </summary>
    /// <remarks>
    /// <b>Quién crea y quién resuelve.</b> Los dos casos nacen a nombre del propio administrador
    /// (<c>moiesbbuga@gmail.com</c>) — no hay solicitante delegado, es su propia novedad — y quedan
    /// auto-asignados a él mismo, que es quien detectó, aplicó y validó el fix en la misma sesión.
    ///
    /// <b>Por qué nacen en CERRADO y no en SOLUCIONADO.</b> A diferencia de
    /// <c>SeedTicketGananciaDiariaEngordeLadyMalave</c> (donde el cierre lo confirma el solicitante
    /// desde la pantalla, en una sesión aparte), acá el solicitante y quien pide el cierre son la
    /// misma persona en la misma sesión: no tiene sentido sembrarlo a medio camino para
    /// inmediatamente después cerrarlo. Se completan los tres campos del cierre
    /// (<c>Estado=CERRADO</c>, <c>FechaCierreSolicitante</c>, <c>CerradoPorUserId</c>), mismo
    /// patrón que <c>TicketService.Gestion.cs</c> usa al cerrar un caso desde la pantalla.
    ///
    /// <b>Por qué la empresa es Santa Reyes y no la del administrador.</b> El módulo auditado
    /// (postura: traslado de huevos y reporte técnico de producción) es el que usa Santa Reyes, y
    /// el pedido explícito fue "para la empresa Santa Reyes porque es para ellos" — se fuerza
    /// <c>company_id</c> a Santa Reyes (resuelta por nombre, nunca por id) en vez de heredar la
    /// empresa por defecto del creador, igual que <c>SeedTicketPlanItalappSantaReyes</c> hace
    /// cuando no hay solicitante delegado.
    ///
    /// <b>Por qué son DOS casos y no uno.</b> Son dos bugs de causa y archivo distintos —
    /// <c>TrasladoHuevosService.cs</c> (afecta a cualquier empresa que edite un traslado de huevos
    /// pendiente) y <c>ReporteTecnicoProduccionService.cs</c> (bug preexistente, no causado por
    /// Santa Reyes, de fuga de datos entre empresas en la guía genética compartida) — cada uno con
    /// su propia causa y su propia solución, mismo criterio que el resto del módulo de tickets
    /// (un caso por incidente).
    ///
    /// <b>Identidad y fail-open.</b> Nada se referencia por guid ni por id literal (difieren
    /// local↔prod): el administrador se resuelve por email y la empresa por nombre. Sin
    /// CUALQUIERA de los dos no se siembra nada — <c>RAISE NOTICE</c> + <c>RETURN</c>, la app
    /// arranca igual.
    ///
    /// <b>Idempotencia.</b> Cada caso se busca por <c>titulo</c>; correr la migración dos veces no
    /// duplica ninguna fila.
    ///
    /// <b>No manda correo:</b> es SQL, no pasa por <c>IEmailQueueService</c>.
    /// <c>notificado_correo</c> queda en <c>false</c>.
    ///
    /// Migración DATA-ONLY: Designer clonado, ModelSnapshot intacto (no hay cambio de schema). El
    /// SQL vive en el partial <c>.Seed.cs</c> por tamaño (dos casos).
    /// </remarks>
    public partial class SeedTicketsFixesAuditoriaSantaReyes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SEED_SQL);
        }

        /// <summary>
        /// Borra exactamente los dos casos sembrados, localizados por el mismo <c>titulo</c> que
        /// usó el <c>Up</c>. No toca ningún otro ticket.
        /// </summary>
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DOWN_SQL);
        }
    }
}
