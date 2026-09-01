using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Agrega <c>email_queue.next_retry_at</c>: a partir de cuándo se puede volver a intentar un
    /// correo que falló.
    /// </summary>
    /// <remarks>
    /// <b>Por qué.</b> Los reintentos salían en el ciclo de polling siguiente, uno detrás de otro.
    /// Con el buzón emisor bloqueado en el proveedor, eso son tres autenticaciones fallidas seguidas
    /// por correo —entre el 26 y el 28-ago fueron <b>30</b>—, y los intentos fallidos repetidos son
    /// justamente lo que dispara y sostiene el lockout de la cuenta. Ahora la espera crece: 1, 5 y
    /// 15 minutos (<c>EmailErrorCalculos.EsperaAntesDelProximoIntento</c>).
    ///
    /// <b>Nullable, y <c>null</c> significa «ya mismo».</b> Todo correo nuevo nace sin fecha y las
    /// filas anteriores a esta columna quedan igual, así que el comportamiento del primer intento no
    /// cambia para nada de lo ya encolado.
    ///
    /// El índice parcial cubre exactamente la consulta del procesador —pendientes que ya vencieron—,
    /// que corre cada 30 segundos.
    ///
    /// Idempotente: <c>ADD COLUMN IF NOT EXISTS</c> + <c>CREATE INDEX IF NOT EXISTS</c>.
    /// Plan: <c>fase_de_desarrollo/correo_reintentos_y_diagnostico_plan.md</c>.
    /// </remarks>
    public partial class EmailQueueNextRetryAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.email_queue
    ADD COLUMN IF NOT EXISTS next_retry_at timestamp without time zone;

COMMENT ON COLUMN public.email_queue.next_retry_at IS
    'A partir de cuando se puede reintentar. NULL = ya mismo. La espera crece 1/5/15 min para no sostener el bloqueo del buzon emisor.';

CREATE INDEX IF NOT EXISTS ix_email_queue_pendientes_vencidos
    ON public.email_queue (next_retry_at, created_at)
    WHERE status = 'pending';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP INDEX IF EXISTS public.ix_email_queue_pendientes_vencidos;
ALTER TABLE public.email_queue DROP COLUMN IF EXISTS next_retry_at;
");
        }
    }
}
