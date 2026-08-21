using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Registra el caso de soporte de Lady Malave (validacion de indicadores del seguimiento
    /// diario de pollo engorde: ganancia diaria) ya <b>SOLUCIONADO</b>, con la descripcion de la
    /// solucion aplicada, para que ella pueda confirmarlo y cerrarlo desde la pantalla de tickets.
    /// </summary>
    /// <remarks>
    /// <b>Quien crea y quien resuelve.</b> El caso lo crea la propia solicitante (Lady Malave,
    /// ecuitalcol) — no hay solicitante delegado aca, a diferencia de
    /// <c>SeedTicketPlanItalappSantaReyes</c> — y queda auto-asignado al administrador
    /// (<c>moiesbbuga@gmail.com</c>), que es quien valido y aplico el fix.
    ///
    /// <b>Por que nace en SOLUCIONADO y no en ABIERTO.</b> El fix (division de la ganancia diaria
    /// entre los dias reales transcurridos desde el ultimo pesaje, ver
    /// <c>fase_de_desarrollo/ganancia_diaria_engorde_intervalo_pesaje_plan.md</c>) se implemento y
    /// se valido en la misma sesion de trabajo que registra este caso: no tiene sentido sembrarlo
    /// ABIERTO para inmediatamente despues moverlo — se siembra ya en el estado real que tiene hoy.
    /// <c>CERRADO</c> es la unica transicion que le falta y la hace el solicitante desde la
    /// pantalla (regla del modulo: "el cierre lo confirma el solicitante"), por eso el seed se
    /// detiene en SOLUCIONADO.
    ///
    /// <b>Identidad y fail-open.</b> Nada se referencia por guid ni por id literal (difieren
    /// local↔prod): tanto el administrador como Lady Malave se resuelven por email/nombre. Sin
    /// CUALQUIERA de los dos no se siembra nada (a diferencia del solicitante delegado opcional
    /// del seed de Santa Reyes, aca ambas identidades son necesarias: sin Lady no hay dueño del
    /// caso, sin el administrador no hay a quien asignarlo) — <c>RAISE NOTICE</c> + <c>RETURN</c>,
    /// la app arranca igual. La empresa/pais del caso son los de Lady (el solicitante), nunca los
    /// del administrador.
    ///
    /// <b>Idempotencia.</b> El caso se busca por <c>titulo</c>; correr la migracion dos veces no
    /// duplica la fila.
    ///
    /// <b>No manda correo:</b> es SQL, no pasa por <c>IEmailQueueService</c>.
    /// <c>notificado_correo</c> queda en <c>false</c>.
    ///
    /// Migracion DATA-ONLY: Designer clonado, ModelSnapshot intacto (no hay cambio de schema). El
    /// SQL vive en el partial <c>.Seed.cs</c> por tamaño.
    /// </remarks>
    public partial class SeedTicketGananciaDiariaEngordeLadyMalave : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SEED_SQL);
        }

        /// <summary>
        /// Borra exactamente el caso sembrado, localizado por el mismo <c>titulo</c> que usó el
        /// <c>Up</c>. No toca ningún otro ticket.
        /// </summary>
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DOWN_SQL);
        }
    }
}
