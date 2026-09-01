using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Reabre <b><c>TK-2026-000020</c></b> («La información de levante incompleta en carga masiva lote
    /// S369») y reemplaza su descripción de solución por la <b>instrucción correcta</b>: antes de
    /// reimportar hay que cargar la entrada de alimento que falta.
    /// </summary>
    /// <remarks>
    /// <b>Qué estaba mal.</b> El caso se declaró SOLUCIONADO el 14-ago diciendo que bastaba «volver a
    /// subir el archivo completo», y omitía lo único que bloquea: la importación simula el balance de
    /// alimento y <b>rechaza el archivo entero</b> si el stock de la granja no alcanza. El usuario lo
    /// intentó el 18-ago y las tres corridas quedaron registradas en <c>migracion_masiva</c> (ids 169,
    /// 170 y 171) con <b>0 filas procesadas</b>; la 171 subió las 175 filas, omitió correctamente las
    /// 168 ya cargadas y no entró ninguna de las 7 nuevas. El error, textual: «No alcanza el stock de
    /// POLLA LEVANTE REPRODUCTORA PESADA en la granja: el archivo consume 846.500 kg y solo hay
    /// 464.190 kg (faltan 382.310 kg)». Nadie miró esos intentos antes de cerrar el caso.
    ///
    /// <b>No hay bug que arreglar acá.</b> El guard de stock es un invariante correcto: importar un
    /// consumo que la granja no tenía dejaría el inventario mintiendo. Lo que faltaba era decirle al
    /// usuario el paso previo. Por eso esta migración no toca un solo dato de levante ni de inventario.
    ///
    /// <b>Por qué se reabre el mismo caso si <c>CERRADO</c> es terminal.</b>
    /// <c>TicketEstados.Transiciones[Cerrado]</c> está vacío a propósito y hay tests que lo blindan,
    /// porque un caso cerrado lo cerraron <b>las dos partes</b>. Acá eso no ocurrió: lo cerró la
    /// gestión —la migración <c>20260831130000</c>, el 31-ago—, el solicitante nunca confirmó nada y
    /// ni siquiera se le envió el aviso de solución (<c>notificado_correo = false</c>). Reabrirlo
    /// repone el estado que una migración le impuso sin que la otra parte participara. <b>La máquina
    /// de estados de la aplicación no se toca.</b>
    ///
    /// <b>Fail-safe.</b> Solo actúa si el caso sigue en <c>CERRADO</c>: si alguien ya lo movió, se
    /// saltea con <c>RAISE NOTICE</c>. Idempotente: la segunda corrida no encuentra nada que hacer,
    /// y la nota se siembra con <c>WHERE NOT EXISTS</c> sobre su propio texto.
    ///
    /// Plan: <c>fase_de_desarrollo/reabrir_ticket_s369_instruccion_correcta_plan.md</c>.
    /// Migración DATA-ONLY: Designer clonado, ModelSnapshot intacto.
    /// </remarks>
    public partial class ReabrirTicketS369InstruccionCorrecta : Migration
    {
        private const string Codigo = "TK-2026-000020";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    c_codigo  constant varchar(20)   := '" + Codigo + @"';
    c_ahora   constant timestamptz   := '2026-09-01 14:00:00+00';

    v_id      bigint;
    v_estado  varchar(20);
    v_admin   integer;
    v_sol     text;
    v_nota    text;
BEGIN
    SELECT t.id, t.estado INTO v_id, v_estado
    FROM public.tickets t
    WHERE t.codigo = c_codigo AND t.deleted_at IS NULL
    LIMIT 1;

    IF v_id IS NULL THEN
        RAISE NOTICE 'Reapertura %: no existe en este entorno; omitido.', c_codigo;
        RETURN;
    END IF;

    -- Fail-safe: solo se reabre lo que sigue cerrado. Si alguien ya lo movio, se respeta.
    IF v_estado IS DISTINCT FROM 'CERRADO' THEN
        RAISE NOTICE 'Reapertura %: esta en % y no en CERRADO; NO se toca.', c_codigo, v_estado;
        RETURN;
    END IF;

    SELECT t.created_by_user_id INTO v_admin FROM public.tickets t WHERE t.id = v_id;

    v_sol :=
'INSTRUCCION CORREGIDA (01-sep-2026). La respuesta anterior estaba incompleta y por eso la carga nunca entro.

QUE FALTABA. Volver a subir el archivo completo es correcto -la importacion es idempotente por lote y fecha, y los dias ya cargados se omiten solos-, PERO la importacion tambien simula el balance de alimento y RECHAZA EL ARCHIVO ENTERO si el stock de la granja no alcanza. Como el consumo de los 7 dias faltantes no tiene respaldo en el inventario de MANGOS, no entra ninguna fila. El usuario lo intento el 18-ago y las tres corridas quedaron registradas con 0 filas procesadas; el mensaje fue: no alcanza el stock de POLLA LEVANTE REPRODUCTORA PESADA en la granja, el archivo consume 846.500 kg y solo hay 464.190 kg, faltan 382.310 kg.

ESTO NO ES UNA FALLA DEL SISTEMA. Importar un consumo que la granja nunca tuvo dejaria el inventario mintiendo. El guard esta bien puesto; lo que faltaba era decir el paso previo.

COMO SE HACE, EN ESTE ORDEN:
1) Registrar primero la ENTRADA DE ALIMENTO que falta: POLLA LEVANTE REPRODUCTORA PESADA en la granja MANGOS, con fecha ANTERIOR O IGUAL al primer dia faltante y por los kilos que realmente entraron. Se puede cargar en la hoja Alimento del mismo archivo, o por el modulo de inventario.
2) Recien despues subir el archivo completo de 175 filas. Los 168 dias ya cargados se omiten solos y entran los 7 nuevos.
3) ANTES de importar, usar el boton Validar. Corre la misma simulacion sin escribir nada y dice el deficit exacto: es lo que evita otro intento a ciegas.
4) Los DOS sublotes descuentan del MISMO stock, porque en esta empresa el alimento se lleva a nivel de granja y no de galpon. La entrada tiene que alcanzar para S369A y para S369B, o se cargan dos entradas.

DATOS MEDIDOS HOY. S369A tiene 168 dias (29/08/2025 al 12/02/2026) y S369B tambien 168 (04/09/2025 al 18/02/2026); a los dos les faltan los dias 169 al 175. El stock de POLLA LEVANTE REPRODUCTORA PESADA en MANGOS es de 464,190 kg y no se movio desde el 12-ago. El deficit de 382.310 kg es el medido para S369A; el de S369B nunca se llego a medir porque esa carga no se intento, y sale del paso 3.

Se reabre el caso: quedo cerrado por la gestion sin que el solicitante lo confirmara, y ademas nunca se le envio el aviso de solucion, asi que no tuvo forma de responder.';

    v_nota :=
'Caso REABIERTO. La instruccion que se habia dado estaba incompleta: decia que bastaba volver a subir el archivo completo y no mencionaba que la importacion rechaza el archivo entero si el stock de alimento de la granja no alcanza. Hubo tres intentos el 18-ago, todos con 0 filas procesadas, que nadie reviso antes de cerrar el caso. La descripcion de la solucion quedo reemplazada por el procedimiento correcto, en orden, con los numeros medidos. No hay cambio de sistema pendiente: el guard de stock es correcto y la carga la ejecuta el usuario.';

    UPDATE public.tickets t
       SET estado                   = 'EN_ANALISIS',
           solucion_descripcion     = v_sol,
           fecha_solucion           = NULL,
           fecha_cierre_solicitante = NULL,
           cerrado_por_user_id      = NULL,
           updated_by_user_id       = v_admin,
           updated_at               = c_ahora
     WHERE t.id = v_id;

    INSERT INTO public.ticket_notas
        (ticket_id, user_id, nota, estado_resultante, es_interna, created_at)
    SELECT v_id, COALESCE(v_admin, 0), v_nota, 'EN_ANALISIS', false, c_ahora
    WHERE NOT EXISTS (
        SELECT 1 FROM public.ticket_notas n
        WHERE n.ticket_id = v_id AND n.created_at = c_ahora AND n.estado_resultante = 'EN_ANALISIS');

    RAISE NOTICE 'Reapertura %: caso reabierto en EN_ANALISIS con la instruccion corregida.', c_codigo;
END $$;
");
        }

        /// <summary>
        /// Vuelve a dejarlo como estaba: <c>CERRADO</c> con las marcas de cierre que había puesto la
        /// migración anterior, y borra la nota de reapertura.
        /// </summary>
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    c_codigo constant varchar(20) := '" + Codigo + @"';
    c_ahora  constant timestamptz := '2026-09-01 14:00:00+00';
    c_cierre constant timestamptz := '2026-08-31 18:00:00+00';
    v_id     bigint;
    v_admin  integer;
BEGIN
    SELECT t.id INTO v_id
    FROM public.tickets t WHERE t.codigo = c_codigo AND t.deleted_at IS NULL LIMIT 1;
    IF v_id IS NULL THEN RETURN; END IF;

    -- `cerrado_por_user_id` lo había puesto la migración 20260831130000 con el int de auditoría del
    -- ADMINISTRADOR, no con el creador del caso (que es la persona que lo reportó). Se resuelve igual
    -- que allá —por email— para que el Down restaure el valor exacto y no uno parecido.
    SELECT t2.created_by_user_id INTO v_admin
    FROM public.tickets t2
    JOIN public.users u ON u.id = t2.created_by_user_guid
    JOIN public.user_logins ul ON ul.user_id = u.id
    JOIN public.logins l ON l.id = ul.login_id
    WHERE lower(l.email) = 'moiesbbuga@gmail.com'
    ORDER BY t2.id DESC
    LIMIT 1;

    DELETE FROM public.ticket_notas
     WHERE ticket_id = v_id AND created_at = c_ahora AND estado_resultante = 'EN_ANALISIS';

    UPDATE public.tickets
       SET estado                   = 'CERRADO',
           fecha_solucion           = '2026-08-14 00:00:00+00',
           fecha_cierre_solicitante = c_cierre,
           cerrado_por_user_id      = v_admin,
           updated_at               = c_cierre
     WHERE id = v_id;
END $$;
");
        }
    }
}
