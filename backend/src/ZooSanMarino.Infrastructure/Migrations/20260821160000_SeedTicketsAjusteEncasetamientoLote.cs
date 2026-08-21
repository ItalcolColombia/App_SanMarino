using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Registra los dos casos de soporte del <b>encasetamiento de un lote</b> (21 de agosto de
    /// 2026), ya <b>SOLUCIONADOS y CERRADOS</b>:
    /// (1) no existía forma correcta de corregir las aves de un lote que ya tenía seguimiento —
    /// editarlo reescribía el encasetamiento con el saldo ya consumido (commit <c>a9fd721</c>); y
    /// (2) las grillas y el panel de detalle mostraban ese mismo saldo bajo el rótulo «aves
    /// encasetadas», así que las columnas no sumaban (commit <c>299c816</c>).
    /// </summary>
    /// <remarks>
    /// <b>Por qué son DOS casos y no uno.</b> Comparten la causa de fondo —la misma columna
    /// significa cosas opuestas en <c>lote_ave_engorde</c> y en <c>lotes</c>— pero son dos
    /// incidentes distintos, con reporte, alcance y arreglo propios: el primero <b>corrompía datos</b>
    /// al guardar y lo levantó operación pidiendo poder sumar aves; el segundo es de
    /// <b>presentación</b>, no corrompe nada, y lo levantó operación al ver que las columnas no
    /// cuadraban. Un caso por incidente, mismo criterio que el resto del módulo.
    ///
    /// <b>Por qué la empresa es ItalcolEcuador.</b> Los dos casos se reportaron y se midieron sobre
    /// pollo engorde de Ecuador (el caso nombrado por operación es el lote 24, y el impacto medido
    /// es de 123 de sus 124 lotes). Se resuelve por nombre, nunca por id: difieren local↔prod.
    ///
    /// <b>Por qué nacen en CERRADO.</b> Igual que <c>SeedTicketsFixesAuditoriaSantaReyes</c>: el
    /// administrador detectó, aplicó y validó los dos fixes en la misma sesión, así que sembrarlos
    /// a medio camino para cerrarlos acto seguido no aporta nada. Se completan los tres campos del
    /// cierre (<c>Estado=CERRADO</c>, <c>fecha_cierre_solicitante</c>, <c>cerrado_por_user_id</c>),
    /// mismo patrón que <c>TicketService.Gestion.cs</c> al cerrar desde la pantalla.
    ///
    /// <b>Identidad y fail-open.</b> El administrador se resuelve por email y la empresa por nombre.
    /// Sin cualquiera de los dos no se siembra nada — <c>RAISE NOTICE</c> + <c>RETURN</c>, la app
    /// arranca igual.
    ///
    /// <b>Idempotencia.</b> Cada caso se busca por <c>titulo</c>; correr la migración dos veces no
    /// duplica ninguna fila.
    ///
    /// <b>No manda correo:</b> es SQL, no pasa por <c>IEmailQueueService</c>.
    /// <c>notificado_correo</c> queda en <c>false</c>.
    ///
    /// Migración DATA-ONLY: Designer clonado, ModelSnapshot intacto (no hay cambio de schema). El
    /// SQL vive en el partial <c>.Seed.cs</c> por tamaño.
    /// </remarks>
    public partial class SeedTicketsAjusteEncasetamientoLote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SEED_SQL);
        }

        /// <summary>
        /// Borra exactamente los dos casos sembrados, localizados por el mismo <c>titulo</c> que usó
        /// el <c>Up</c>. No toca ningún otro ticket.
        /// </summary>
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DOWN_SQL);
        }
    }
}
