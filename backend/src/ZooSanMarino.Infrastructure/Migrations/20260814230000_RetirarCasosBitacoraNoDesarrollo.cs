using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Retira del tablero los <b>dos casos</b> que el seed de la bitácora
    /// (<c>20260814030000_SeedCasosCerradosBitacora</c>) dejó abiertos y que no son trabajo del
    /// área de desarrollo: <i>«Auditoría de impacto de la columna mixto en los reportes de
    /// Panamá»</i> y <i>«Bitácora ItalJira de julio y agosto 2026: horas, solución y bugs por
    /// sesión»</i> (en local <c>TK-2026-000146</c> y <c>TK-2026-000161</c>; el correlativo NO es el
    /// mismo en producción).
    /// </summary>
    /// <remarks>
    /// <b>Por qué existen:</b> aquel seed publicó un caso por cada sesión de la bitácora. De las
    /// 137, 135 entraron CERRADO y estas 2 quedaron EN_ANALISIS porque su sesión no se cerró — así
    /// que son las únicas que siguen ocupando la bandeja como si fueran casos vivos, cuando en
    /// realidad son registro de bitácora, no un pedido a resolver.
    ///
    /// <b>Qué hace (dos pasos, en este orden):</b>
    /// <list type="number">
    /// <item><b>Desenlaza la tarea</b> (<c>ticket_tareas.ticket_id = NULL</c>). La sesión
    /// <c>SES-*</c> sigue viva colgando de su historia — es historial real y no se toca; lo que se
    /// retira es el CASO. Además <c>fk_ticket_tareas_tickets_ticket_id</c> es
    /// <c>ON DELETE CASCADE</c>: dejar la tarea enlazada a un caso retirado la deja apuntando a
    /// algo que ya no se puede abrir.</item>
    /// <item><b>Baja lógica del caso</b> — <c>deleted_at</c> + <c>status = 'I'</c>, exactamente lo
    /// que escribe el botón «Eliminar» del módulo (<c>TicketService.DeleteAsync</c>). Todas las
    /// lecturas de tickets e ItalJira filtran <c>deleted_at IS NULL</c>, así que desaparecen de la
    /// bandeja, del tablero, del roadmap y de los indicadores. Se prefiere la baja lógica al
    /// <c>DELETE</c> físico porque es reversible y porque el <c>DELETE</c> arrastraría en cascada
    /// la tarea de bitácora y sus subtareas BUG.</item>
    /// </list>
    ///
    /// <b>Identificación estable, nunca por id ni por código:</b> <c>TK-2026-NNNNNN</c> se deriva
    /// del id y local↔producción no están en el mismo correlativo. Se localizan por la <b>primera
    /// línea</b> de la descripción, que el seed escribió con el código de sesión
    /// (<c>SES-20260811-cbb2</c> y <c>SES-20260814-880f</c>): esos códigos son literales del seed,
    /// idénticos en todos los entornos.
    ///
    /// <b>Fail-open y guarda:</b> si en el entorno no hay nada que retirar (el seed pudo no haber
    /// sembrado), <c>RAISE NOTICE</c> y <c>RETURN</c> — un seed no puede tumbar el arranque de la
    /// app. Y si el patrón llegara a enganchar <b>más de 2</b> casos, no toca ninguno y avisa: es
    /// preferible dejarlos visibles a retirar de más.
    ///
    /// <b>Idempotencia:</b> el <c>UPDATE</c> exige <c>deleted_at IS NULL</c>; en la segunda pasada
    /// no hay filas candidatas y no cambia nada.
    ///
    /// Migración DATA-ONLY: Designer clonado, ModelSnapshot intacto.
    /// </remarks>
    public partial class RetirarCasosBitacoraNoDesarrollo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    v_ids bigint[];
    v_n   integer;
BEGIN
    -- Los casos se localizan por la PRIMERA LINEA de la descripcion, que el seed de la bitacora
    -- termina con el codigo de la sesion. El codigo TK-2026-NNNNNN sale del id y no sirve: local
    -- y produccion no estan en el mismo correlativo.
    SELECT array_agg(t.id) INTO v_ids
    FROM public.tickets t
    WHERE t.deleted_at IS NULL
      AND (split_part(t.descripcion, chr(10), 1) LIKE '%tarea SES-20260811-cbb2'
        OR split_part(t.descripcion, chr(10), 1) LIKE '%tarea SES-20260814-880f');

    -- Fail-open: sin casos que retirar la migracion no hace nada y la app arranca igual.
    IF v_ids IS NULL THEN
        RAISE NOTICE 'Retiro de casos de bitacora: no hay nada que retirar en este entorno.';
        RETURN;
    END IF;

    -- Guarda: si el patron engancha de mas, no se toca NADA. Preferimos dejarlos visibles.
    IF array_length(v_ids, 1) > 2 THEN
        RAISE NOTICE 'Retiro de casos de bitacora: el patron engancho % casos (se esperaban 2); no se toco ninguno.',
                     array_length(v_ids, 1);
        RETURN;
    END IF;

    -- 1) Desenlazar la tarea SES-* ANTES de dar de baja el caso: la sesion de bitacora es historial
    --    real y sigue viva colgando de su historia; lo que se retira es el CASO. Ademas
    --    fk_ticket_tareas_tickets_ticket_id es ON DELETE CASCADE.
    UPDATE public.ticket_tareas
       SET ticket_id = NULL
     WHERE ticket_id = ANY(v_ids);

    -- 2) Baja LOGICA: la misma que escribe el boton Eliminar del modulo (TicketService.DeleteAsync).
    --    updated_by_user_id queda como esta: una migracion no tiene usuario de sesion.
    UPDATE public.tickets
       SET deleted_at = timezone('utc', now()),
           status     = 'I',
           updated_at = timezone('utc', now())
     WHERE id = ANY(v_ids)
       AND deleted_at IS NULL;

    GET DIAGNOSTICS v_n = ROW_COUNT;
    RAISE NOTICE 'Retiro de casos de bitacora: % caso(s) retirado(s) del tablero.', v_n;
END $$;
");
        }

        /// <summary>
        /// Devuelve los dos casos al tablero y vuelve a enlazarles su tarea de bitácora (y las
        /// subtareas de esa tarea, tal como las enlazó el seed original). Solo re-enlaza tareas que
        /// hoy estén sueltas, para no robarle la tarea a otro caso.
        /// </summary>
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN
        SELECT t.id AS ticket_id, s.codigo AS tarea_codigo
        FROM (VALUES ('SES-20260811-cbb2'), ('SES-20260814-880f')) AS s(codigo)
        JOIN public.tickets t
          ON split_part(t.descripcion, chr(10), 1) LIKE '%tarea ' || s.codigo
        WHERE t.deleted_at IS NOT NULL
    LOOP
        UPDATE public.tickets
           SET deleted_at = NULL,
               status     = 'A',
               updated_at = timezone('utc', now())
         WHERE id = r.ticket_id;

        UPDATE public.ticket_tareas x
           SET ticket_id = r.ticket_id
         WHERE x.deleted_at IS NULL
           AND x.ticket_id IS NULL
           AND (x.codigo = r.tarea_codigo
                OR x.parent_tarea_id IN (
                     SELECT y.id FROM public.ticket_tareas y WHERE y.codigo = r.tarea_codigo));
    END LOOP;
END $$;
");
        }
    }
}
