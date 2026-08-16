using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Registra en ItalJira el fix de <b>`ENGORDE_EC` apuntando a una tabla fantasma</b>: la historia,
    /// el caso ya <c>SOLUCIONADO</c> con sus 6 tareas en <c>LISTO</c> y las horas imputadas, más un
    /// <b>segundo caso abierto</b> con el hallazgo que esta entrega NO resuelve.
    /// </summary>
    /// <remarks>
    /// <b>De dónde salió.</b> El usuario pidió (15ago26) validar módulo por módulo si los 14 flags de
    /// comportamiento están realmente implementados, con el back y el front corriendo. La auditoría
    /// encontró que el único formulario de seguimiento de pollo engorde hace su CRUD contra el
    /// controller de <b>Ecuador</b> —que persiste en la tabla COMPARTIDA y separaba como
    /// <c>ENGORDE_EC</c>— pero pide pendientes y valida como <c>ENGORDE</c>, mientras las tres ramas
    /// <c>ENGORDE_EC</c> de <c>ValidacionSeguimientoService</c> leían
    /// <c>seguimiento_diario_aves_engorde_ecuador</c>, tabla que <b>no existe</b> pese a que su
    /// migración figura aplicada.
    ///
    /// <para>
    /// <b>Se siembra ya solucionado</b> porque el desarrollo se hizo en la misma sesión que lo
    /// detectó: abrirlo en curso para cerrarlo dos migraciones después sería ruido en el tablero.
    /// SOLUCIONADO y no CERRADO: el cierre lo hace el solicitante tras validar en pantalla.
    /// </para>
    ///
    /// <para>
    /// <b>El hallazgo pendiente va en su propio caso, abierto.</b> «Disponible = stock − reservas
    /// activas» no está enganchado (nadie llama a <c>ReservadoPorItemAsync</c> ni a
    /// <c>ReservadoDeAvesAsync</c>). Marcarlo solucionado sería mentir, y meterlo como tarea en
    /// BACKLOG de esta historia impediría dejarla en LISTO. Queda como caso propio en
    /// <c>EN_ANALISIS</c> para que no se pierda.
    /// </para>
    ///
    /// <para>
    /// <b>Horas imputadas = estimadas.</b> No hay medición real por tarea; queda dicho en el worklog
    /// para que nadie lo lea como una medición.
    /// </para>
    ///
    /// <para>
    /// <b>Identidad por email</b>, nunca por guid fijo (los ids difieren local↔prod). Fail-open: sin
    /// el usuario en el entorno, <c>RAISE NOTICE</c> y <c>RETURN</c> — un seed no puede tumbar el
    /// arranque de la app. <b>Idempotente:</b> historia y casos se buscan por <c>titulo</c>, las
    /// tareas por <c>codigo</c>. Correrla dos veces no cambia una sola fila la segunda vez.
    /// Migración DATA-ONLY: Designer clonado, ModelSnapshot intacto.
    /// </para>
    /// </remarks>
    public partial class SeedTicketFixEngordeEcTablaCompartida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    v_user_guid   uuid;
    v_cedula      integer;
    v_company     integer;
    v_pais        integer;
    v_historia_id bigint;
    v_his_codigo  varchar(40);
    v_ticket_id   bigint;
    v_pend_id     bigint;
    v_orden       integer;
    v_solucion    text;
BEGIN
    -- Identidad POR EMAIL: los guid difieren entre local y produccion.
    SELECT u.id INTO v_user_guid
    FROM public.users u
    JOIN public.user_logins ul ON ul.user_id = u.id
    JOIN public.logins l       ON l.id = ul.login_id
    WHERE lower(l.email) = 'moiesbbuga@gmail.com'
    LIMIT 1;

    IF v_user_guid IS NULL THEN
        RAISE NOTICE 'Fix ENGORDE_EC: no existe moiesbbuga@gmail.com en este entorno; omitido.';
        RETURN;
    END IF;

    -- El int de auditoria del modulo NO es la cedula: se reusa el de sus propios tickets.
    SELECT t.created_by_user_id INTO v_cedula
    FROM public.tickets t WHERE t.created_by_user_guid = v_user_guid ORDER BY t.id DESC LIMIT 1;
    v_cedula := COALESCE(v_cedula, 0);

    SELECT t.company_id, t.pais_id INTO v_company, v_pais
    FROM public.tickets t WHERE t.created_by_user_guid = v_user_guid ORDER BY t.id DESC LIMIT 1;
    IF v_company IS NULL THEN
        SELECT c.id INTO v_company FROM public.companies c ORDER BY c.id LIMIT 1;
    END IF;
    v_company := COALESCE(v_company, 1);
    v_pais    := COALESCE(v_pais, 1);

    -- ═══════════════════════ 1) HISTORIA ═══════════════════════
    SELECT h.id, h.codigo INTO v_historia_id, v_his_codigo
    FROM public.historias h
    WHERE h.titulo = 'Fix de la doble validacion en pollo engorde: ENGORDE_EC leia una tabla que no existe'
      AND h.deleted_at IS NULL
    LIMIT 1;

    IF v_historia_id IS NULL THEN
        SELECT COALESCE(MAX(h.orden) + 1, 0) INTO v_orden
        FROM public.historias h WHERE h.estado = 'LISTO' AND h.deleted_at IS NULL;

        INSERT INTO public.historias
            (pais_id, titulo, descripcion, estado, prioridad, responsable_user_guid, orden,
             horas_estimadas, fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
             etiquetas, company_id, created_by_user_id, created_at)
        VALUES
            (v_pais,
             'Fix de la doble validacion en pollo engorde: ENGORDE_EC leia una tabla que no existe',
             'Salio de validar modulo por modulo, con el back y el front corriendo, si los 14 flags de comportamiento estan realmente implementados.

EL PROBLEMA. El unico formulario de seguimiento diario de pollo engorde hace su CRUD contra el controller de Ecuador (SeguimientoAvesEngordeEcuador), que persiste en la tabla COMPARTIDA seguimiento_diario_aves_engorde y separaba las reservas con el modulo ENGORDE_EC. Pero ese mismo formulario pide pendientes y valida con el modulo ENGORDE. Y las tres ramas ENGORDE_EC de ValidacionSeguimientoService leian seguimiento_diario_aves_engorde_ecuador, una tabla que NO EXISTE en la base pese a que su migracion figura como aplicada.

QUE PASABA CON EL FLAG ENCENDIDO (ItalcolPanama).
1) Guardar reventaba: al cortar el alta por dias vencidos se consultaba la tabla inexistente y Postgres respondia 42P01.
2) Validar no descontaba: el registro se encontraba (misma tabla) pero las reservas se habian escrito como ENGORDE_EC y la consulta las filtraba por ENGORDE. Cero filas, el registro quedaba validado = true sin descontar un solo kilo ni un ave, y la reserva quedaba activa para siempre.

LA REGLA QUE SE APLICO. Manda el codigo de hoy: el service escribe en la tabla compartida, asi que la validacion tiene que leer la compartida. No se resucita la tabla partida — el split de mayo se abandono y la entidad quedo documentada como sin uso.',
             'LISTO', 'ALTA', v_user_guid, v_orden,
             10.00, DATE '2026-08-15', DATE '2026-08-15',
             timezone('utc', now()), timezone('utc', now()),
             'backend,doble-validacion,engorde',
             v_company, v_cedula, timezone('utc', now()))
        RETURNING id INTO v_historia_id;

        v_his_codigo := 'HIS-2026-' || lpad(v_historia_id::text, 4, '0');
        UPDATE public.historias SET codigo = v_his_codigo WHERE id = v_historia_id;
    END IF;

    -- El codigo es la clave de idempotencia de las tareas: si quedo NULL, el NOT EXISTS de abajo
    -- compararia contra NULL y reinsertaria las tareas en cada corrida.
    IF v_his_codigo IS NULL THEN
        v_his_codigo := 'HIS-2026-' || lpad(v_historia_id::text, 4, '0');
        UPDATE public.historias SET codigo = v_his_codigo WHERE id = v_historia_id AND codigo IS NULL;
    END IF;

    -- ═══════════════════════ 2) CASO SOLUCIONADO ═══════════════════════
    v_solucion :=
'RESUELTO. Los dos modulos de engorde son dos services sobre UN solo esquema, no dos esquemas.

QUE SE CAMBIO
- Las tres ramas ENGORDE_EC de ValidacionSeguimientoService (leer estado, marcar validado y listar
  pendientes del lote) ahora leen la tabla COMPARTIDA seguimiento_diario_aves_engorde, que es donde
  el service de Ecuador realmente escribe.
- Se agrego ModuloSeguimiento.Canonico(): la reserva se guarda y se busca siempre con el literal
  ENGORDE, asi separar por la via de Ecuador y validar por la de Colombia se encuentran. Los dos
  literales siguen siendo validos en la API: se unifico la CLAVE, no el vocabulario.
- Reproductora era el unico de los cinco modulos que no cortaba el alta con dias vencidos sin
  confirmar, aunque el flag lo promete por escrito. Ahora lo hace.
- La entidad SeguimientoDiarioAvesEngordeEcuador quedo documentada como SIN USO, apuntando a
  SeguimientoDiarioAvesEngorde. Se deja mapeada a proposito: desmapearla cambia el ModelSnapshot y
  obliga a una migracion de esquema sobre una tabla que en unos entornos existe y en otros no.

CON EL FLAG APAGADO NO CAMBIA NADA: las ramas tocadas solo corren con la doble validacion encendida.

VERIFICACION
- dotnet build 0 errores (el unico warning, CS8625 en MigracionMovimientosAvesCalculosTests, es
  preexistente y de un archivo que nadie toco).
- dotnet test 2581 en verde, 7 tests nuevos sobre el literal canonico.

QUEDA ABIERTO EN OTRO CASO: disponible = stock menos reservas activas todavia no esta enganchado.';

    SELECT t.id INTO v_ticket_id
    FROM public.tickets t
    WHERE t.titulo = 'Pollo engorde con doble validacion: guardar falla y validar no descuenta'
      AND t.deleted_at IS NULL
    LIMIT 1;

    IF v_ticket_id IS NULL THEN
        SELECT COALESCE(MAX(t.orden_tablero) + 1, 0) INTO v_orden
        FROM public.tickets t WHERE t.estado = 'SOLUCIONADO' AND t.deleted_at IS NULL;

        INSERT INTO public.tickets
            (pais_id, tipo, estado, titulo, descripcion, solucion_descripcion,
             assigned_to_user_guid, created_by_user_guid, prioridad, orden_tablero,
             horas_estimadas, fecha_inicio_plan, fecha_fin_plan, fecha_primera_apertura,
             fecha_solucion, historia_id, status, notificado_correo,
             company_id, created_by_user_id, created_at)
        VALUES
            (v_pais, 'DESARROLLO', 'SOLUCIONADO',
             'Pollo engorde con doble validacion: guardar falla y validar no descuenta',
             'HALLAZGO (15ago26). Detectado validando modulo por modulo si los 14 flags de comportamiento estan implementados, con el back y el front corriendo.

El formulario de seguimiento diario de pollo engorde hace su CRUD contra el controller de Ecuador, que escribe en la tabla compartida y separa las reservas como ENGORDE_EC, pero pide pendientes y valida como ENGORDE. Las ramas ENGORDE_EC del servicio de validacion leian seguimiento_diario_aves_engorde_ecuador, tabla que no existe en la base.

IMPACTO CON EL FLAG ENCENDIDO (ItalcolPanama, que se activo el 15ago26):
1) Guardar un seguimiento de engorde falla al cortar el alta por dias vencidos (42P01, la tabla no existe).
2) Validar marca el registro como validado SIN descontar alimento ni aves, porque no encuentra las reservas: quedaron escritas con otro modulo. La reserva queda activa para siempre y el consumo nunca llega al inventario.

Es el peor de los dos: el primero se ve (falla a la vista), el segundo no — el usuario cree que valido y el inventario nunca se movio.

Plan: fase_de_desarrollo/fix_engorde_ec_tabla_compartida_plan.md
Tracker: bloque V3 de tracker_estado.md',
             v_solucion,
             v_user_guid, v_user_guid, 'ALTA', v_orden,
             10.00, DATE '2026-08-15', DATE '2026-08-15', timezone('utc', now()),
             timezone('utc', now()), v_historia_id, 'A', false,
             v_company, v_cedula, timezone('utc', now()))
        RETURNING id INTO v_ticket_id;

        UPDATE public.tickets
           SET codigo = 'TK-2026-' || lpad(v_ticket_id::text, 6, '0')
         WHERE id = v_ticket_id;
    END IF;

    -- ═══════════════════════ 3) TAREAS (todas LISTO) ═══════════════════════
    SELECT COALESCE(MAX(t.orden) + 1, 0) INTO v_orden
    FROM public.ticket_tareas t WHERE t.estado = 'LISTO' AND t.deleted_at IS NULL;

    INSERT INTO public.ticket_tareas
        (ticket_id, historia_id, codigo, tipo, estado, prioridad, titulo, descripcion,
         asignado_user_guid, orden, horas_estimadas, fecha_inicio_plan, fecha_fin_real, etiquetas,
         company_id, created_by_user_id, created_at)
    SELECT v_ticket_id, v_historia_id,
           v_his_codigo || '-T' || t.n,
           t.tipo, 'LISTO', t.prioridad, t.titulo, t.descripcion,
           v_user_guid, v_orden + t.n - 1, t.horas::numeric(8,2),
           DATE '2026-08-15', timezone('utc', now()), t.etiquetas,
           v_company, v_cedula, timezone('utc', now())
    FROM (VALUES
        (1, 'TAREA', 'ALTA', 'V3.0 Auditoria de los 14 flags modulo por modulo',
            'Recorrer levante, produccion, pollo engorde y reproductora verificando que cada flag de comportamiento este realmente leido en back y en front, y contrastarlo contra el estado real de companies en la base. De aca salieron los tres hallazgos.', 3, 'auditoria,multiempresa'),
        (2, 'TAREA', 'ALTA', 'V3.1 Las ramas ENGORDE_EC leen la tabla compartida',
            'LeerEstadoAsync, MarcarValidadoAsync y LeerPendientesDelLoteAsync dejan de consultar seguimiento_diario_aves_engorde_ecuador y pasan a _ctx.SeguimientoDiarioAvesEngorde, colapsando el case con el de ENGORDE. Es donde el service de Ecuador realmente escribe.', 2, 'backend,engorde'),
        (3, 'TAREA', 'ALTA', 'V3.2 Literal canonico para las reservas',
            'ModuloSeguimiento.Canonico() colapsa ENGORDE_EC a ENGORDE, y se aplica al escribir y al buscar reservas (SepararAsync, LiberarAsync, ValidarAsync, DesvalidarAsync, ReservadoDeAvesAsync, ObtenerPendientesAsync, AsegurarPuedeRegistrarDiaAsync). Sin esto, separar por Ecuador y validar por Colombia no se encuentran. Los dos literales siguen siendo validos en la API.', 2, 'backend,inventario'),
        (4, 'TAREA', 'MEDIA', 'V3.3 Bloqueo por vencidos en reproductora',
            'Era el unico de los cinco modulos que no llamaba AsegurarPuedeRegistrarDiaAsync, aunque el flag promete por escrito que un registro sin validar pasado el dia bloquea el alta de dias nuevos. Sin esto un lote acumula dias sin confirmar y el alimento queda separado sin techo.', 1, 'backend,reproductora'),
        (5, 'TAREA', 'MEDIA', 'V3.4 Documentar la entidad huerfana',
            'SeguimientoDiarioAvesEngordeEcuador queda marcada como SIN USO, explicando que el split de mayo se abandono y apuntando a SeguimientoDiarioAvesEngorde. Se deja mapeada: desmapearla cambia el ModelSnapshot y obliga a una migracion de esquema sobre una tabla que en unos entornos existe y en otros no.', 1, 'backend,documentacion'),
        (6, 'TAREA', 'ALTA', 'V3.5 Tests xUnit del literal canonico',
            'Cubren que ENGORDE_EC colapsa a ENGORDE, que el resto de modulos no se toca, que separar por una via y validar por la otra dan la MISMA clave, y que colapsar la clave no invalida el literal en la API. dotnet test: 2581 en verde.', 1, 'backend,tests')
    ) AS t(n, tipo, prioridad, titulo, descripcion, horas, etiquetas)
    WHERE NOT EXISTS (
        SELECT 1 FROM public.ticket_tareas x WHERE x.codigo = v_his_codigo || '-T' || t.n
    );

    -- ═══════════════════════ 4) HORAS (= estimadas, sin medicion real) ═══════════════════════
    INSERT INTO public.ticket_tiempos (tarea_id, user_guid, user_id, fecha, horas, descripcion, created_at)
    SELECT t.id, v_user_guid, v_cedula, current_date, t.horas_estimadas,
           'Trabajo completado. Horas segun la estimacion acordada (no hay medicion real por tarea).',
           timezone('utc', now())
      FROM public.ticket_tareas t
     WHERE t.historia_id = v_historia_id
       AND t.horas_estimadas IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM public.ticket_tiempos tt WHERE tt.tarea_id = t.id);

    -- ═══════════════════════ 5) CASO APARTE: el hallazgo que NO se resuelve ═══════════════════
    -- Va en su propio caso y ABIERTO. Marcarlo solucionado seria mentir; meterlo como tarea de la
    -- historia de arriba impediria dejarla en LISTO.
    SELECT t.id INTO v_pend_id
    FROM public.tickets t
    WHERE t.titulo = 'El disponible de inventario ignora lo que la doble validacion tiene separado'
      AND t.deleted_at IS NULL
    LIMIT 1;

    IF v_pend_id IS NULL THEN
        SELECT COALESCE(MAX(t.orden_tablero) + 1, 0) INTO v_orden
        FROM public.tickets t WHERE t.estado = 'EN_ANALISIS' AND t.deleted_at IS NULL;

        INSERT INTO public.tickets
            (pais_id, tipo, estado, titulo, descripcion,
             assigned_to_user_guid, created_by_user_guid, prioridad, orden_tablero,
             fecha_primera_apertura, status, notificado_correo,
             company_id, created_by_user_id, created_at)
        VALUES
            (v_pais, 'DESARROLLO', 'EN_ANALISIS',
             'El disponible de inventario ignora lo que la doble validacion tiene separado',
             'HALLAZGO (15ago26), de la misma auditoria que el caso del ENGORDE_EC. NO se resolvio: necesita una decision de diseno.

QUE PASA. IValidacionSeguimientoService.ReservadoPorItemAsync y ReservadoDeAvesAsync estan declarados e implementados, pero NO los llama nadie en todo el backend. El disponible que ven inventario y los formularios de los cuatro seguimientos sigue siendo el stock completo, ignorando lo que ya esta separado.

POR QUE IMPORTA. La razon de ser de la separacion es que el mismo galpon alimenta a dos lotes: si el disponible no le resta las reservas activas, los dos lotes ven los mismos kilos y se puede comprometer alimento que ya esta tomado.

LA DECISION QUE FALTA. O se le resta la reserva a Quantity en GET /api/InventarioGestion/stock —y entonces la pantalla de inventario deja de mostrar la existencia fisica, que es justo lo que operacion concilia—, o se agrega un campo Reservado/Disponible al DTO y se toca el front de los cuatro modulos. La segunda es mas trabajo pero no le miente a nadie.

Afecta solo a empresas con requiere_validacion_seguimiento_diario encendido (hoy: ItalcolPanama).',
             v_user_guid, v_user_guid, 'MEDIA', v_orden,
             timezone('utc', now()), 'A', false,
             v_company, v_cedula, timezone('utc', now()))
        RETURNING id INTO v_pend_id;

        UPDATE public.tickets
           SET codigo = 'TK-2026-' || lpad(v_pend_id::text, 6, '0')
         WHERE id = v_pend_id;
    END IF;

    RAISE NOTICE 'Fix ENGORDE_EC: caso % SOLUCIONADO (historia %), caso % abierto por el disponible.',
        v_ticket_id, v_historia_id, v_pend_id;
END $$;
");
        }

        /// <inheritdoc />
        /// <remarks>Borra las tareas, los worklogs, los dos casos y la historia sembrados acá.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    v_historia_id bigint;
    v_his_codigo  varchar(40);
BEGIN
    SELECT h.id, h.codigo INTO v_historia_id, v_his_codigo
    FROM public.historias h
    WHERE h.titulo = 'Fix de la doble validacion en pollo engorde: ENGORDE_EC leia una tabla que no existe'
    LIMIT 1;

    IF v_historia_id IS NOT NULL THEN
        DELETE FROM public.ticket_tiempos
         WHERE tarea_id IN (SELECT id FROM public.ticket_tareas WHERE historia_id = v_historia_id);
        DELETE FROM public.ticket_tareas
         WHERE historia_id = v_historia_id AND codigo LIKE v_his_codigo || '-T%';
    END IF;

    DELETE FROM public.tickets
     WHERE titulo IN (
        'Pollo engorde con doble validacion: guardar falla y validar no descuenta',
        'El disponible de inventario ignora lo que la doble validacion tiene separado');

    IF v_historia_id IS NOT NULL THEN
        DELETE FROM public.historias WHERE id = v_historia_id;
    END IF;
END $$;
");
        }
    }
}
