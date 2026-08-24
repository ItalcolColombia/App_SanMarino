using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Registra el caso de <b>DUDAS</b> con las <b>6 definiciones que faltan del cliente</b> para
    /// poder cerrar V52 (Requerimientos de Italapp para Santa Reyes): F5.3, F7.3, F8.1, F8.3,
    /// F9.2c y F10.1. Nace <b>ABIERTO</b>, con una subtarea <c>BLOQUEADA</c> por cada una.
    /// </summary>
    /// <remarks>
    /// <b>Por qué existen estos 6 y no se construyeron.</b> No son trabajo pendiente por falta de
    /// tiempo: son puntos donde el texto del cliente admite <b>dos lecturas que producen software
    /// distinto</b>, y tres de ellos viven en módulos COMPARTIDOS multi-empresa
    /// (<c>movimientos-aves</c>, traslados, reportes financieros) con historial propio de bugs de
    /// doble conteo. Adivinar ahí no es «avanzar»: es meter una regresión en producción para
    /// Sanmarino, Panamá y Ecuador a cambio de una funcionalidad que probablemente tampoco es la
    /// que el cliente pidió. El resto de V52 (F0-F4, F6, F7.1/F7.2/F7.4, F8.2, F9.1/F9.2, F10.2,
    /// F11) sí está construido y validado.
    ///
    /// <b>Por qué es UN caso con 6 subtareas y no 6 casos.</b> Comparten solicitante, destinatario y
    /// condición de cierre —una sola reunión con Santa Reyes las responde todas— y cerrarlas por
    /// separado obligaría a repetir seis veces el mismo contexto. Es lo contrario del criterio de
    /// <c>SeedTicketsAjusteEncasetamientoLote</c>, donde eran dos incidentes con reporte y arreglo
    /// propios; acá es una sola conversación.
    ///
    /// <b>Por qué <c>DUDAS</c> y no <c>REQUERIMIENTO</c>.</b> El requerimiento ya existe y es
    /// <c>TK-2026-000172</c>. Esto es lo que le falta a ese requerimiento para poder ejecutarse:
    /// preguntas, no alcance nuevo. Mezclarlas dentro del caso 172 escondería que el bloqueo está
    /// del lado del cliente.
    ///
    /// <b>El estado de las subtareas es <c>BLOQUEADA</c>, no <c>BACKLOG</c>.</b> Backlog dice «todavía
    /// no lo empezamos»; bloqueada dice «no depende de nosotros». Es la diferencia que el tablero
    /// tiene que mostrar.
    ///
    /// <b>Identidad y fail-open.</b> El administrador se resuelve por email y la empresa por nombre
    /// —nunca por id: difieren local↔prod—. Sin cualquiera de los dos no se siembra nada
    /// (<c>RAISE NOTICE</c> + <c>RETURN</c>) y la app arranca igual.
    ///
    /// <b>Idempotencia.</b> El caso se busca por <c>titulo</c> y cada subtarea por
    /// <c>(ticket_id, titulo)</c>; correr la migración dos veces no duplica ninguna fila.
    ///
    /// <b>No manda correo:</b> es SQL, no pasa por <c>IEmailQueueService</c>.
    /// <c>notificado_correo</c> queda en <c>false</c>.
    ///
    /// Migración DATA-ONLY: Designer clonado, ModelSnapshot intacto (no hay cambio de schema). El
    /// SQL vive en el partial <c>.Seed.cs</c> por tamaño.
    /// </remarks>
    public partial class SeedTicketDefinicionesPendientesSantaReyes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SEED_SQL);
        }

        /// <summary>
        /// Borra el caso sembrado y sus subtareas, localizados por el mismo <c>titulo</c> que usó el
        /// <c>Up</c>. No toca ningún otro ticket ni ninguna otra tarea.
        /// </summary>
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DOWN_SQL);
        }
    }
}
