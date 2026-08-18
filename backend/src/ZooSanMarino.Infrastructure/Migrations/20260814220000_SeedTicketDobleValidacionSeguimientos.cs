using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Registra en ItalJira el trabajo de la <b>doble validación de los seguimientos diarios</b>:
    /// un caso para moiesbbuga@gmail.com, la historia que lo agrupa y sus 14 tareas con
    /// <c>horas_estimadas</c>.
    /// </summary>
    /// <remarks>
    /// <b>Qué pidió el usuario (14ago26):</b> que ningún seguimiento diario se pueda guardar sin
    /// alimento (tipo y cantidad; en engorde sobre el bloque Mixto, en postura sobre hembras y/o
    /// machos); que el rechazo por fecha repetida o por campos obligatorios vacíos muestre un
    /// <b>modal con el motivo</b> —hoy hay flujos donde no se ve—; y que el descuento de alimento y
    /// aves deje de aplicarse al guardar: hasta que un usuario con permiso <b>valide</b> el
    /// registro, este se puede editar y eliminar, y lo consumido queda <b>separado (reservado)</b>,
    /// no descontado —el mismo galpón alimenta a dos lotes y hay que poder distinguir qué se llevó
    /// cada uno—. Editar antes de validar reescribe la separación, sin cálculos de retorno. Los
    /// registros se validan con un día de diferencia como máximo: pasado ese plazo la fila se pinta
    /// roja, muestra estado «En retraso» y al entrar al lote aparece un modal de alerta.
    ///
    /// <b>Decisiones que tomó el usuario sobre el plan:</b> el descuento diferido se activa con un
    /// <b>flag por empresa</b> apagado por defecto (con el flag OFF el comportamiento actual queda
    /// idéntico); el seguimiento de reproductora, que ya tenía <c>confirmado</c>, se <b>unifica</b>
    /// al modelo nuevo; los registros vencidos sin validar <b>bloquean</b> el alta de días nuevos en
    /// ese lote; y todo lo ya existente se marca validado para no revertir ningún saldo.
    ///
    /// Plan completo: <c>fase_de_desarrollo/doble_validacion_seguimientos_diarios_plan.md</c>.
    /// Tracker: bloque <c>V1</c> de <c>tracker_estado.md</c>.
    ///
    /// <b>El caso queda en SOLUCIONADO al terminar el desarrollo, no acá:</b> el usuario cierra el
    /// caso él mismo después de validar en pantalla (esa es la segunda parte del cierre, con
    /// <c>fecha_cierre_solicitante</c>).
    ///
    /// <b>Identidad y fail-open:</b> el usuario se resuelve <b>por email</b>, nunca por guid fijo
    /// (los ids difieren local↔prod); si no existe en el entorno, <c>RAISE NOTICE</c> y
    /// <c>RETURN</c> sin sembrar nada — un seed no puede tumbar el arranque de la app. El int de
    /// auditoría no es la cédula (3177120174 no entra en <c>integer</c>): se toma el
    /// <c>created_by_user_id</c> que ya usan sus propios tickets.
    ///
    /// <b>Idempotencia:</b> la historia y el caso se buscan por <c>titulo</c>; las tareas por
    /// <c>codigo</c> (<c>HIS-2026-NNNN-Tn</c>, derivado del id real de la historia). Correrla dos
    /// veces no cambia una sola fila la segunda vez.
    ///
    /// Migración DATA-ONLY: Designer clonado, ModelSnapshot intacto.
    /// </remarks>
    public partial class SeedTicketDobleValidacionSeguimientos : Migration
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
    v_orden       integer;
BEGIN
    -- Identidad POR EMAIL, nunca por guid fijo: los ids difieren entre local y produccion.
    SELECT u.id INTO v_user_guid
    FROM public.users u
    JOIN public.user_logins ul ON ul.user_id = u.id
    JOIN public.logins l       ON l.id = ul.login_id
    WHERE lower(l.email) = 'moiesbbuga@gmail.com'
    LIMIT 1;

    -- Fail-open silencioso: sin el usuario no se siembra nada y la app arranca igual.
    IF v_user_guid IS NULL THEN
        RAISE NOTICE 'Ticket doble validacion: no existe moiesbbuga@gmail.com en este entorno; omitido.';
        RETURN;
    END IF;

    -- El int de auditoria del modulo NO es la cedula: se reusa el de sus propios tickets.
    SELECT t.created_by_user_id INTO v_cedula
    FROM public.tickets t WHERE t.created_by_user_guid = v_user_guid ORDER BY t.id DESC LIMIT 1;
    IF v_cedula IS NULL THEN
        SELECT CASE WHEN u.cedula ~ '^[0-9]{1,9}$' THEN u.cedula::integer ELSE 0 END
          INTO v_cedula FROM public.users u WHERE u.id = v_user_guid;
    END IF;
    v_cedula := COALESCE(v_cedula, 0);

    SELECT t.company_id, t.pais_id INTO v_company, v_pais
    FROM public.tickets t WHERE t.created_by_user_guid = v_user_guid ORDER BY t.id DESC LIMIT 1;
    IF v_company IS NULL THEN
        SELECT c.id INTO v_company FROM public.companies c ORDER BY c.id LIMIT 1;
    END IF;
    v_company := COALESCE(v_company, 1);
    v_pais    := COALESCE(v_pais, 1);

    -- ═══════════════════════ 1) HISTORIA (epica) ═══════════════════════
    SELECT h.id, h.codigo INTO v_historia_id, v_his_codigo
    FROM public.historias h
    WHERE h.titulo = 'Doble validacion de los seguimientos diarios (levante, produccion, pollo engorde y reproductora)'
      AND h.deleted_at IS NULL
    LIMIT 1;

    IF v_historia_id IS NULL THEN
        SELECT COALESCE(MAX(h.orden) + 1, 0) INTO v_orden
        FROM public.historias h WHERE h.estado = 'EN_CURSO' AND h.deleted_at IS NULL;

        INSERT INTO public.historias
            (pais_id, titulo, descripcion, estado, prioridad, responsable_user_guid, orden,
             horas_estimadas, fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, etiquetas,
             company_id, created_by_user_id, created_at)
        VALUES
            (v_pais,
             'Doble validacion de los seguimientos diarios (levante, produccion, pollo engorde y reproductora)',
             'Los cuatro seguimientos diarios descuentan alimento y aves en el mismo momento en que se guarda el registro. Como despues se puede editar y borrar libremente, la operacion termina corrigiendo a mano por debajo cada vez que la captura sale mal.

QUE SE CONSTRUYE.
1) Alimento obligatorio: no se guarda un seguimiento sin tipo de alimento y cantidad. En pollo engorde tiene que ir en el bloque Mixto; en levante y produccion, en hembras, en machos o en los dos; reproductora tambien exige registro.
2) Modal con el motivo: si la fecha ya existe o falta un dato obligatorio, se muestra un modal que dice exactamente que pasa. Hoy hay flujos donde el rechazo no se ve y el usuario cree que guardo.
3) Doble validacion con permiso: al guardar, el registro queda PENDIENTE y se puede editar y eliminar. El alimento y las aves NO se descuentan: quedan SEPARADOS (reservados), porque el mismo galpon alimenta a dos lotes y hay que poder distinguir que se llevo cada uno. Editar antes de validar reescribe la separacion, sin calculos de retorno. Recien al validar se aplica el consumo real al inventario y el descuento de aves.
4) Semaforo de retraso: un registro sin validar pasado el dia se pinta rojo, muestra el estado En retraso con icono de alarma, dispara un modal de alerta al entrar al lote y BLOQUEA el alta de dias nuevos en ese lote hasta ponerse al dia.

DECISIONES TOMADAS (14ago26).
- El descuento diferido se activa con un flag POR EMPRESA apagado por defecto: con el flag OFF el comportamiento actual queda identico.
- El seguimiento de reproductora, que ya tenia confirmado, se unifica al modelo nuevo (separar al guardar, descontar y cruzar al validar).
- Los registros vencidos sin validar bloquean dias nuevos.
- Todo lo ya existente se marca validado: no se revierte ningun saldo ni se pinta la tabla en rojo de golpe.

Plan: fase_de_desarrollo/doble_validacion_seguimientos_diarios_plan.md
Tracker: bloque V1 de tracker_estado.md',
             'EN_CURSO', 'ALTA', v_user_guid, v_orden,
             56.00, DATE '2026-08-14', DATE '2026-08-29', timezone('utc', now()),
             'seguimiento,inventario,validacion,multiempresa',
             v_company, v_cedula, timezone('utc', now()))
        RETURNING id INTO v_historia_id;

        v_his_codigo := 'HIS-2026-' || lpad(v_historia_id::text, 4, '0');
        UPDATE public.historias SET codigo = v_his_codigo WHERE id = v_historia_id;
    END IF;

    -- El codigo de la historia es la clave de idempotencia de las tareas. Si la historia existia
    -- con codigo NULL, el WHERE NOT EXISTS de abajo comparara contra NULL, no encontrara nada y
    -- volvera a insertar las 14 tareas en cada corrida. Se completa antes de seguir.
    IF v_his_codigo IS NULL THEN
        v_his_codigo := 'HIS-2026-' || lpad(v_historia_id::text, 4, '0');
        UPDATE public.historias SET codigo = v_his_codigo WHERE id = v_historia_id AND codigo IS NULL;
    END IF;

    -- ═══════════════════════ 2) CASO (ticket) ═══════════════════════
    SELECT t.id INTO v_ticket_id
    FROM public.tickets t
    WHERE t.titulo = 'Doble validacion de los seguimientos diarios: alimento obligatorio, motivo del rechazo y descuento solo al validar'
      AND t.deleted_at IS NULL
    LIMIT 1;

    IF v_ticket_id IS NULL THEN
        SELECT COALESCE(MAX(t.orden_tablero) + 1, 0) INTO v_orden
        FROM public.tickets t WHERE t.estado = 'EN_IMPLEMENTACION' AND t.deleted_at IS NULL;

        INSERT INTO public.tickets
            (pais_id, tipo, estado, titulo, descripcion,
             assigned_to_user_guid, created_by_user_guid, prioridad, orden_tablero,
             horas_estimadas, fecha_inicio_plan, fecha_fin_plan, fecha_primera_apertura,
             historia_id, status, notificado_correo,
             company_id, created_by_user_id, created_at)
        VALUES
            (v_pais, 'DESARROLLO', 'EN_IMPLEMENTACION',
             'Doble validacion de los seguimientos diarios: alimento obligatorio, motivo del rechazo y descuento solo al validar',
             'PEDIDO (14ago26). En los modulos de seguimiento diario de levante, produccion, pollo engorde y reproductora de pollo engorde hay que poner una regla: no se puede hacer un seguimiento diario sin agregar alimento, con su tipo de alimento y su cantidad de consumo. En engorde tiene que ir en el campo Mixto; en postura (levante y produccion) tiene que ir en alguno de los generos, macho o hembra, o en los dos; reproductora de pollo engorde tambien tiene que tener registros.

Si se guarda con una fecha igual a la de otro registro, o con datos obligatorios vacios, tiene que salir un modal avisando que es lo que esta pasando, porque hay momentos en que no muestra el motivo. Tambien hay que validar todo el flujo y mejorarlo en cada uno de los seguimientos.

DOBLE VALIDACION. Despues de guardar el seguimiento se necesita una doble validacion, y eso va a ser un permiso. Lo que no este validado se va a poder editar y eliminar. Si no se valida, NO se aplica el descuento de alimento ni de aves: hoy el descuento se hace de una, y necesitamos que tenga este control, porque el registro se puede editar o eliminar antes de confirmar que esta bien. Lo que si tiene que hacer es dejar las aves y el alimento en un estado de SEPARACION, porque el mismo galpon que tiene el alimento se puede usar en dos lotes y hay que separar lo que se consumio. Asi, si se edita, no hay que hacer calculos de retorno: simplemente se separa para su consumo.

RETRASO. Los registros se tienen que validar con un dia de diferencia como maximo. Si no estan confirmados, la fila del registro pendiente se pinta de color rojo, muestra un estado de retraso y un simbolo de alarma; y cada vez que se entre al lote aparece un modal con un logo de alerta en rojo avisando que faltan registros por confirmar.

POR QUE. En los seguimientos se equivocan y toca hacer muchas cosas por debajo; esto se corrige con la doble validacion. El seguimiento de reproductora de pollo engorde ya tiene algo asi.

ALCANCE ACORDADO. Flag por empresa apagado por defecto (con el flag OFF nada cambia), reproductora se unifica al modelo nuevo, los vencidos bloquean dias nuevos y el historico se marca validado.

Plan: fase_de_desarrollo/doble_validacion_seguimientos_diarios_plan.md
Tracker: bloque V1 de tracker_estado.md',
             v_user_guid, v_user_guid, 'ALTA', v_orden,
             56.00, DATE '2026-08-14', DATE '2026-08-29', timezone('utc', now()),
             v_historia_id, 'A', false,
             v_company, v_cedula, timezone('utc', now()))
        RETURNING id INTO v_ticket_id;

        UPDATE public.tickets
           SET codigo = 'TK-2026-' || lpad(v_ticket_id::text, 6, '0')
         WHERE id = v_ticket_id;
    END IF;

    -- ═══════════════════════ 3) TAREAS ═══════════════════════
    SELECT COALESCE(MAX(t.orden) + 1, 0) INTO v_orden
    FROM public.ticket_tareas t WHERE t.estado = 'BACKLOG' AND t.deleted_at IS NULL;

    INSERT INTO public.ticket_tareas
        (ticket_id, historia_id, codigo, tipo, estado, prioridad, titulo, descripcion,
         asignado_user_guid, orden, horas_estimadas, fecha_inicio_plan, etiquetas,
         company_id, created_by_user_id, created_at)
    SELECT v_ticket_id, v_historia_id,
           v_his_codigo || '-T' || t.n,
           t.tipo, 'BACKLOG', t.prioridad, t.titulo, t.descripcion,
           v_user_guid, v_orden + t.n - 1, t.horas::numeric(8,2), DATE '2026-08-14', t.etiquetas,
           v_company, v_cedula, timezone('utc', now())
    FROM (VALUES
        (1, 'TAREA', 'ALTA', 'V1.1 Flag de empresa requiere_validacion_seguimiento_diario',
            'Columna nueva en companies (bool NOT NULL DEFAULT false), nombrada por el comportamiento y no por el tenant. Hay que agregarla en las CUATRO proyecciones de CompanyDto (CompanyService.ToDto, CompanyService.Crud, CompanyResolver, CompanyPaisService) mas Create/UpdateCompanyDto, y leerla en el front con ActiveCompanyConfigService (cache 5 min, fail-closed: si falla, false).', 3, 'backend,frontend,multiempresa'),
        (2, 'TAREA', 'ALTA', 'V1.2 Estado de validacion por registro en las 4 tablas',
            'Columnas validado / validado_at / validado_por en seguimiento_diario_levante, seguimiento_diario_produccion, seguimiento_diario_aves_engorde y seguimiento_diario_aves_engorde_ecuador. Reproductora NO lleva columna nueva: reusa el confirmado que ya tiene, porque lo lee la funcion SQL del cruce a pollo engorde. Backfill validado = true en todo lo existente.', 2, 'backend,bd'),
        (3, 'TAREA', 'ALTA', 'V1.3 Tablas de separacion: seguimiento_reserva_alimento y seguimiento_reserva_aves',
            'Las dos tablas que sostienen la SEPARACION. Alimento: ubicacion completa (granja, nucleo, galpon, silo) mas item, origen (modulo y seguimiento), cantidad y estado ACTIVA/APLICADA/LIBERADA, con indice unico parcial por origen e item sobre las ACTIVAS para que sea idempotente. Aves: bajas separadas por hembras, machos y mixtas. Las reservas NO son movimientos y no deben llegar al historico unificado.', 3, 'backend,bd,inventario'),
        (4, 'TAREA', 'ALTA', 'V1.4 Calculos puros + tests xUnit',
            'ValidacionSeguimientoCalculos (estado derivado VALIDADO/PENDIENTE/EN_RETRASO, fecha limite, bloqueo por vencidos, gate del flag), AlimentoObligatorioCalculos (Mixto en engorde, hembras y/o machos en postura) y ReservaSeguimientoCalculos (armado de la reserva desde la metadata y diff en edicion). Tests obligatorios: con el flag OFF el comportamiento previo tiene que quedar identico, mensajes incluidos.', 5, 'backend,tests'),
        (5, 'TAREA', 'ALTA', 'V1.5 Servicio de validacion y reservas + endpoints',
            'ValidacionSeguimientoService: validar (transaccion unica que aplica consumo, descuenta aves, marca reservas APLICADA y el registro validado), des-validar con permiso aparte y listado de pendientes por lote. Endpoints POST validar, POST desvalidar y GET pendientes.', 6, 'backend,api'),
        (6, 'TAREA', 'CRITICA', 'V1.6 Descuento diferido detras del flag en los 5 Crud',
            'Es la tarea de mas riesgo. Levante, produccion, engorde Colombia, engorde Ecuador y reproductora: el descuento actual queda detras del gate del flag y, con el flag encendido, se reemplaza por la creacion de reservas. El descuento de aves de postura esta centralizado en SeguimientoDiarioService (nota A7): se gatea ahi, no se duplica. Engorde tiene DOS servicios con Crud paralelo, va en los dos o queda cojo en Ecuador.', 8, 'backend,inventario'),
        (7, 'TAREA', 'ALTA', 'V1.7 Disponible = stock menos reservas activas',
            'El disponible que ve el formulario pasa a descontar las reservas ACTIVAS de esa ubicacion e item. Es lo que hace que el mismo galpon compartido por dos lotes deje de mostrarles el disponible completo a los dos. Una sola formula: se cambia en el unico lugar que lo calcula, no en cada formulario.', 4, 'backend,frontend,inventario'),
        (8, 'TAREA', 'ALTA', 'V1.8 Alimento obligatorio en los 4 modulos',
            'Front (bloquea el submit y explica que falta) y backend (defensa en profundidad: la carga masiva y la PWA entran por el mismo servicio). En engorde el bloque exigido es el Mixto; en levante y produccion, hembras y/o machos; reproductora exige registro. El mensaje nombra el bloque y la fecha.', 5, 'backend,frontend'),
        (9, 'TAREA', 'ALTA', 'V1.9 Modal con el motivo del rechazo',
            'Servicio compartido en shared/services (mismo patron que ConfirmDialogService, monta el modal dinamicamente). Cubre fecha duplicada, alimento faltante, campos obligatorios vacios y cualquier 400 del backend, mostrando el mensaje real del servidor en vez de un generico. El toast queda para el exito y los avisos que no piden accion.', 4, 'frontend'),
        (10, 'TAREA', 'ALTA', 'V1.10 Semaforo de retraso: fila roja, alarma y modal al entrar al lote',
            'Columna Estado, badge En retraso, fila roja e icono de alarma en la tabla diaria de los cuatro modulos, mas el modal rojo al entrar al lote con el conteo y las fechas pendientes. La funcion que decide el color y el estado es UNA sola, compartida por los cuatro. Los componentes nuevos llevan changeDetection Eager explicito.', 6, 'frontend'),
        (11, 'TAREA', 'MEDIA', 'V1.11 Bloqueo de dias nuevos mientras haya vencidos',
            'Con registros vencidos sin validar, el lote no acepta un seguimiento nuevo. El bloqueo vive en el backend y el front lo anticipa para no dejar llenar el formulario en vano; el mensaje dice cuales hay que validar.', 2, 'backend,frontend'),
        (12, 'TAREA', 'MEDIA', 'V1.12 Permisos de validacion + seed idempotente',
            'seguimiento_levante.validar, seguimiento_produccion.validar y seguimiento_engorde.validar; reproductora reusa el seguimiento_reproductora_engorde.confirmar que ya existe. Seed idempotente que los otorga a los roles que YA tienen el menu correspondiente, localizando los menus por route y nunca por id fijo.', 2, 'backend,permisos'),
        (13, 'DOCUMENTACION', 'MEDIA', 'V1.13 Registrar el caso, la historia y las tareas en ItalJira',
            'Esta misma migracion: caso, historia y tareas con horas estimadas, asignadas a moiesbbuga@gmail.com. Data-only e idempotente, resolviendo la identidad por email.', 1, 'documentacion'),
        (14, 'TAREA', 'CRITICA', 'V1.14 Validacion: build, tests y smoke doble',
            'dotnet build, dotnet test y yarn build. Gate multipais obligatorio porque se toca el saldo de alimento de engorde: verificar_paridad_saldo_engorde.sql antes y despues, con 0 en toda empresa que no sea la de prueba, y el cuadre de alimento en 0 descuadrados. Smoke doble: una empresa con el flag OFF (cero cambios visibles) y la empresa con el flag ON recorriendo guardar, editar, eliminar, validar y retraso.', 5, 'tests,devops')
    ) AS t(n, tipo, prioridad, titulo, descripcion, horas, etiquetas)
    WHERE NOT EXISTS (
        SELECT 1 FROM public.ticket_tareas x WHERE x.codigo = v_his_codigo || '-T' || t.n
    );

    RAISE NOTICE 'Ticket doble validacion sembrado: historia % / caso %', v_his_codigo, v_ticket_id;
END $$;
");
        }

        /// <summary>
        /// Borra exactamente lo que sembró: las tareas por su código, el caso y la historia por su
        /// título. No toca nada más.
        /// </summary>
        /// <inheritdoc />
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
    WHERE h.titulo = 'Doble validacion de los seguimientos diarios (levante, produccion, pollo engorde y reproductora)'
    LIMIT 1;

    IF v_historia_id IS NULL THEN
        RETURN;
    END IF;

    DELETE FROM public.ticket_tareas WHERE historia_id = v_historia_id AND codigo LIKE v_his_codigo || '-T%';
    DELETE FROM public.tickets
     WHERE titulo = 'Doble validacion de los seguimientos diarios: alimento obligatorio, motivo del rechazo y descuento solo al validar';
    DELETE FROM public.historias WHERE id = v_historia_id;
END $$;
");
        }
    }
}
