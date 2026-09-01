using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Pasa <b><c>TK-2026-000183</c></b> («REPORTE DE CAROLINA», ItalcolEcuador) de
    /// <c>EN_IMPLEMENTACION</c> a <b><c>SOLUCIONADO</c></b>, con su descripción de solución y su nota.
    /// </summary>
    /// <remarks>
    /// <b>Por qué el estado viaja en una migración y no se cambia a mano.</b> Es lo único que llega a
    /// producción, y además <b>se ordena solo</b>: las migraciones corren al arrancar la app, y ésta
    /// va después de <c>20260901140000</c> (el dato de CAROLINA) y <c>20260901150000</c> (el duplicado
    /// de G0483). Cuando el ticket se marque solucionado en producción, el arreglo de código y las dos
    /// correcciones de datos ya están aplicados en ese mismo deploy — no hay ventana en la que el caso
    /// diga «solucionado» sobre algo que todavía no llegó.
    ///
    /// <b>El correo al solicitante no sale por acá, y tampoco saldría desde el tablero.</b>
    /// <c>TicketService.CambiarEstadoAsync</c> encola el aviso resolviendo al solicitante por guid o
    /// por cédula; este caso tiene <c>solicitante_user_guid</c> y <c>solicitante_user_id</c> en
    /// <c>NULL</c>, y su <c>created_by_user_id</c> (968091594) es el <i>hash</i> del id de usuario, no
    /// una cédula: <b>ningún usuario tiene esa cédula</b>, así que <c>ResolveSolicitanteEmailAsync</c>
    /// devuelve vacío y el correo no se envía por ninguna vía. Se deja <c>notificado_correo</c> en
    /// <c>false</c>, que es la verdad, en vez de marcarlo notificado sin haber notificado a nadie.
    ///
    /// <b>Fail-safe.</b> Solo actúa si el caso sigue en <c>EN_IMPLEMENTACION</c>; si alguien ya lo
    /// movió, <c>RAISE NOTICE</c> y no toca nada. La transición es legal en la máquina de estados de
    /// la aplicación (<c>FasesTrabajo → Solucionado</c>), así que esto no inventa un camino propio.
    /// Idempotente: la segunda corrida no encuentra el estado de partida, y la nota se siembra con
    /// <c>WHERE NOT EXISTS</c> sobre su propio texto.
    ///
    /// Plan: <c>fase_de_desarrollo/eliminar_stock_no_bajaba_la_tabla_diaria_plan.md</c>.
    /// Migración DATA-ONLY: Designer clonado, ModelSnapshot intacto.
    /// </remarks>
    public partial class SolucionarTicketCarolinaSaldoAlimento : Migration
    {
        private const string Codigo = "TK-2026-000183";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    c_codigo  constant varchar(20) := '" + Codigo + @"';
    c_ahora   constant timestamptz := '2026-09-01 22:00:00+00';
    c_solucion constant text :=
        'Corregido. El dia 1 mostraba 5.600 kg de saldo contra un ingreso de 2.880 porque la remision '
        || '56114 quedo cargada dos veces en el galpon 1 (la real del 02-abr y un duplicado del 07-abr '
        || 'sin remision), y al duplicado se le aplico ""Eliminar registro de stock"": ese camino bajaba '
        || 'el stock y NO la tabla diaria. Ahora el dia 1 dice 2.720 kg con ingreso 0 y el lote cierra '
        || 'en 0, igual que el galpon 2, que tuvo el mismo encasetamiento y la misma remision. '
        || 'Ademas se corrigio el camino que lo produjo, para que eliminar un registro de stock baje '
        || 'los dos lados a la vez, y se agrego un aviso que avisa antes de registrar una salida que '
        || 'dejaria el dia en negativo.';

    v_admin_guid uuid;
    v_admin_ced  integer;
    v_id         bigint;
    v_estado     varchar(20);
    v_nota       text;
BEGIN
    SELECT t.id, t.estado INTO v_id, v_estado
      FROM public.tickets t
     WHERE t.codigo = c_codigo AND t.deleted_at IS NULL;

    IF v_id IS NULL THEN
        RAISE NOTICE '%: no existe en este entorno; omitido.', c_codigo;
        RETURN;
    END IF;

    IF v_estado <> 'EN_IMPLEMENTACION' THEN
        RAISE NOTICE '%: esta en % y no en EN_IMPLEMENTACION; no se toca.', c_codigo, v_estado;
        RETURN;
    END IF;

    -- Identidad POR EMAIL, nunca por guid fijo: los ids difieren entre local y produccion.
    SELECT u.id INTO v_admin_guid
      FROM public.users u
      JOIN public.user_logins ul ON ul.user_id = u.id
      JOIN public.logins l       ON l.id = ul.login_id
     WHERE lower(l.email) = 'moiesbbuga@gmail.com'
     LIMIT 1;

    -- El int de auditoria del modulo NO es la cedula: se reusa el que ya usan sus propios casos.
    SELECT t.created_by_user_id INTO v_admin_ced
      FROM public.tickets t WHERE t.created_by_user_guid = v_admin_guid ORDER BY t.id DESC LIMIT 1;
    v_admin_ced := COALESCE(v_admin_ced, 0);

    UPDATE public.tickets t
       SET estado               = 'SOLUCIONADO',
           solucion_descripcion = c_solucion,
           fecha_solucion       = COALESCE(t.fecha_solucion, c_ahora),
           updated_at           = c_ahora,
           updated_by_user_id   = COALESCE(v_admin_ced, t.updated_by_user_id)
     WHERE t.id = v_id;

    -- Mismo texto que escribe el servicio al solucionar (prefijo 'Solucionado: '), para que el
    -- historial del caso se lea igual que si el cambio hubiera salido del tablero.
    v_nota := 'Solucionado: ' || c_solucion;

    INSERT INTO public.ticket_notas (ticket_id, user_id, nota, estado_resultante, es_interna, created_at)
    SELECT v_id, v_admin_ced, v_nota, 'SOLUCIONADO', false, c_ahora
     WHERE NOT EXISTS (SELECT 1 FROM public.ticket_notas n
                        WHERE n.ticket_id = v_id AND n.nota = v_nota);

    RAISE NOTICE '%: pasado a SOLUCIONADO (ticket %).', c_codigo, v_id;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    c_codigo constant varchar(20) := '" + Codigo + @"';

    v_id     bigint;
    v_estado varchar(20);
BEGIN
    SELECT t.id, t.estado INTO v_id, v_estado
      FROM public.tickets t WHERE t.codigo = c_codigo AND t.deleted_at IS NULL;

    IF v_id IS NULL OR v_estado <> 'SOLUCIONADO' THEN
        RAISE NOTICE '% (Down): no esta en SOLUCIONADO; no se toca.', c_codigo;
        RETURN;
    END IF;

    -- Borra SOLO la nota que sembro el Up (por su texto exacto y su estado resultante).
    DELETE FROM public.ticket_notas n
     WHERE n.ticket_id = v_id
       AND n.estado_resultante = 'SOLUCIONADO'
       AND n.nota LIKE 'Solucionado: Corregido. El dia 1 mostraba 5.600 kg%';

    UPDATE public.tickets t
       SET estado               = 'EN_IMPLEMENTACION',
           solucion_descripcion = NULL,
           fecha_solucion       = NULL
     WHERE t.id = v_id;

    RAISE NOTICE '% (Down): devuelto a EN_IMPLEMENTACION.', c_codigo;
END $$;
");
        }
    }
}
