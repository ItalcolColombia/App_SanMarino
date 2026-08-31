using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Cuerpo SQL del cierre de los 13 casos ya resueltos de Sanmarino, Panamá y Ecuador. Vive en su
    /// propio archivo (<c>partial</c>) por tamaño: la documentación de qué hace y por qué está en
    /// <c>20260831130000_CerrarTicketsResueltosOtrasEmpresas.cs</c>.
    /// </summary>
    /// <remarks>
    /// Los textos se escriben <b>sin acentos</b>, igual que el resto de los seeds del módulo de
    /// tickets, y sin apóstrofes para no tener que escaparlos dentro del literal SQL. La lista de
    /// casos es una tabla <c>VALUES</c> y no 13 sentencias sueltas: así el fail-safe por estado, la
    /// nota y el cierre se escriben una sola vez y ningún caso queda con un tratamiento distinto por
    /// descuido.
    /// </remarks>
    public partial class CerrarTicketsResueltosOtrasEmpresas
    {
        private const string CIERRE_SQL = @"
-- ─────────────────────────────────────────────────────────────────────────────
-- Cierre de los 13 casos de Sanmarino, Panama y Ecuador cuyo arreglo ya esta
-- verificado en el codigo y desplegado en produccion.
--   11 venian de SOLUCIONADO (esperaban la confirmacion del solicitante)
--    2 venian de EN_ANALISIS (resueltos, nadie movio la tarjeta)
-- Fail-safe: el que no este en el estado esperado NO se toca.
-- ─────────────────────────────────────────────────────────────────────────────
DO $$
DECLARE
    c_cierre constant timestamptz := '2026-08-31 18:00:00+00';

    v_admin_guid  uuid;
    v_admin_ced   integer;
    r             record;
    v_id          bigint;
    v_estado      varchar(20);
    v_fecha_sol   timestamptz;
    v_solucion    text;
    v_notificado  boolean;
    v_dias        integer;
    v_nota        text;
    v_cerrados    integer := 0;
    v_saltados    integer := 0;
BEGIN
    -- ═══════════════ 0) ADMINISTRADOR: quien cierra ═══════════════
    -- Identidad POR EMAIL, nunca por guid fijo: los ids difieren entre local y produccion.
    SELECT u.id INTO v_admin_guid
    FROM public.users u
    JOIN public.user_logins ul ON ul.user_id = u.id
    JOIN public.logins l       ON l.id = ul.login_id
    WHERE lower(l.email) = 'moiesbbuga@gmail.com'
    LIMIT 1;

    IF v_admin_guid IS NULL THEN
        RAISE NOTICE 'Cierre de casos resueltos: no existe moiesbbuga@gmail.com en este entorno; omitido.';
        RETURN;
    END IF;

    -- El int de auditoria del modulo NO es la cedula: se reusa el que ya usan sus propios casos.
    SELECT t.created_by_user_id INTO v_admin_ced
    FROM public.tickets t WHERE t.created_by_user_guid = v_admin_guid ORDER BY t.id DESC LIMIT 1;
    v_admin_ced := COALESCE(v_admin_ced, 0);

    -- ═══════════════ 1) LOS 13 CASOS, con su evidencia verificada ═══════════════
    -- solucion_nueva sale NULL para los que ya traen su descripcion de solucion; solo la llevan los
    -- dos que venian de EN_ANALISIS, que nunca la tuvieron.
    FOR r IN
        SELECT * FROM (VALUES
        ('TK-2026-000012', '%sanmarino%', 'SOLUCIONADO',
         'Se agrego el campo Fecha del movimiento, con minimo y maximo, en el modal de movimientos de aves: el dia real en que se movieron las aves lo elige quien registra y la fecha de sistema queda aparte. Verificado en el codigo y desplegado.',
         NULL::text),

        ('TK-2026-000013', '%sanmarino%', 'SOLUCIONADO',
         'La columna tipo_alimento paso de 100 a 500 caracteres en las tablas de seguimiento: al agregar el tercer alimento el texto ya no desborda y el guardado deja de fallar. Medido en la base y desplegado.',
         NULL::text),

        ('TK-2026-000014', '%sanmarino%', 'SOLUCIONADO',
         'Misma causa que el caso anterior: tipo_alimento paso de 100 a 500 caracteres, tambien en las tablas de engorde. La fila que quedaba inconclusa ya se puede completar. Medido en la base y desplegado.',
         NULL::text),

        ('TK-2026-000015', '%ecuador%', 'SOLUCIONADO',
         'La grilla del lote ya no arrastra el alimento del ciclo siguiente: un lote sin liquidar se quedaba sin tope porque el galpon seguia recibiendo alimento y las consultas filtran por ubicacion, no por lote. Corregido y desplegado.',
         NULL::text),

        ('TK-2026-000020', '%sanmarino%', 'SOLUCIONADO',
         'No era una falla del sistema: faltaban dias en el archivo de carga masiva. Verificado sobre la base de produccion que el lote tiene sus 168 registros, 24 semanas exactas, y que el cierre no exige nada mas.',
         NULL::text),

        ('TK-2026-000163', '%panama%', 'SOLUCIONADO',
         'Los ingresos duplicados se corrigieron con los datos que compartio la operacion. Verificado hoy sobre la base: no queda ningun grupo de ingresos repetidos con la misma granja, item, cantidad, dia y galpon en la empresa.',
         NULL::text),

        ('TK-2026-000164', '%panama%', 'SOLUCIONADO',
         'La doble validacion quedo entregada: guardar exige el alimento y SEPARA alimento y aves, validar los descuenta en una transaccion, editar reescribe la separacion y desvalidar los devuelve. Con la columna Estado y el motivo del rechazo en modal. Desplegado.',
         NULL::text),

        ('TK-2026-000165', '%panama%', 'SOLUCIONADO',
         'Los dos modulos de engorde son dos servicios sobre un solo esquema, no dos esquemas: las ramas de validacion ya leen la tabla compartida y la reserva se guarda y se busca con un unico literal, asi que separar por una via y validar por la otra se encuentran. Verificado en el codigo y desplegado.',
         NULL::text),

        ('TK-2026-000166', '%panama%', 'EN_ANALISIS',
         'El disponible ya descuenta lo que la doble validacion tiene separado: la consulta que responde el stock agrupa las reservas activas incluyendo el SILO en la clave y publica lo reservado, dejando la existencia fisica intacta para que operacion siga conciliando contra ella. Esa era exactamente la decision de diseno que este caso esperaba, y se resolvio en la unica consulta que responde el saldo para que ninguna pantalla tenga que acordarse de restarlo por su cuenta.',
         'El disponible ya descuenta lo que la doble validacion tiene separado: la consulta que responde el stock agrupa las reservas activas incluyendo el SILO en la clave y publica lo reservado, dejando la existencia fisica intacta para que operacion siga conciliando contra ella. Esa era exactamente la decision de diseno que este caso planteaba como pendiente, y se resolvio en la unica consulta que responde el saldo para que ninguna pantalla tenga que acordarse de restarlo por su cuenta. El chequeo equivalente para las aves tambien quedo enganchado.'),

        ('TK-2026-000176', '%ecuador%', 'SOLUCIONADO',
         'Las grillas mostraban el saldo vivo bajo el rotulo de aves encasetadas. Ahora cada columna dice lo que es: el encasetamiento es historico del lote y no se mueve con el registro diario. Desplegado.',
         NULL::text),

        ('TK-2026-000177', '%ecuador%', 'SOLUCIONADO',
         'Ya se pueden corregir las aves de un lote que ya tiene seguimiento, sin borrar registros: el inicial se reemplaza y el saldo vivo se corre por la diferencia. Restar por debajo de lo ya consumido se rechaza entero, diciendo el dia y las aves que faltan. Desplegado.',
         NULL::text),

        ('TK-2026-000185', '%ecuador%', 'SOLUCIONADO',
         'El boton Actualizar ya no queda apagado al editar un lote de pollo engorde en las empresas con programacion. Desplegado.',
         NULL::text),

        ('TK-2026-000187', '%panama%', 'EN_ANALISIS',
         'Corregido: el primer dia con registro es el dia 1 y no existe el dia cero. La reproductora hereda la hora de llegada del lote de pollo engorde, que es donde se captura, asi que la numeracion de los dias y los guardas del primer registro por fin disparan. Los indicadores diarios de engorde pasan de la edad cruda al dia de negocio, sin tocar la edad interna con la que cruza la guia genetica.',
         'Corregido: el primer dia con registro es el dia 1 y no existe el dia cero. La reproductora hereda la hora de llegada del lote de pollo engorde, que es donde se captura y que las reproductoras tenian en blanco, asi que la numeracion de los dias y los guardas del primer registro por fin disparan. Los indicadores diarios de engorde pasan de la edad cruda al dia de negocio 1-based, sin tocar la edad interna con la que cruza la guia genetica ni la aritmetica de ganancia diaria.')
        ) AS t(codigo, empresa_like, estado_esperado, evidencia, solucion_nueva)
    LOOP
        v_id := NULL;

        SELECT tk.id, tk.estado, tk.fecha_solucion, tk.solucion_descripcion, tk.notificado_correo
          INTO v_id, v_estado, v_fecha_sol, v_solucion, v_notificado
        FROM public.tickets tk
        JOIN public.companies c ON c.id = tk.company_id
        WHERE tk.codigo = r.codigo
          AND lower(c.name) LIKE r.empresa_like
          AND tk.deleted_at IS NULL
        ORDER BY tk.id
        LIMIT 1;

        IF v_id IS NULL THEN
            RAISE NOTICE 'Cierre de casos resueltos: % no existe en este entorno; omitido.', r.codigo;
            v_saltados := v_saltados + 1;
            CONTINUE;
        END IF;

        -- FAIL-SAFE: si ya lo cerraron, o alguien lo reabrio a otro estado, no se fuerza nada.
        -- Cerrar a ciegas un caso que el solicitante reabrio seria peor que no cerrarlo.
        IF v_estado IS DISTINCT FROM r.estado_esperado THEN
            RAISE NOTICE 'Cierre de casos resueltos: % esta en % y se esperaba %; NO se toca.',
                r.codigo, v_estado, r.estado_esperado;
            v_saltados := v_saltados + 1;
            CONTINUE;
        END IF;

        -- ── a) Los que venian de EN_ANALISIS nunca tuvieron descripcion de solucion ──
        IF r.solucion_nueva IS NOT NULL THEN
            v_fecha_sol := COALESCE(v_fecha_sol, c_cierre);
            UPDATE public.tickets tk
               SET solucion_descripcion   = COALESCE(tk.solucion_descripcion, r.solucion_nueva),
                   fecha_solucion         = COALESCE(tk.fecha_solucion, v_fecha_sol),
                   fecha_primera_apertura = COALESCE(tk.fecha_primera_apertura, tk.created_at)
             WHERE tk.id = v_id;

            SELECT tk.solucion_descripcion INTO v_solucion FROM public.tickets tk WHERE tk.id = v_id;
        END IF;

        -- ── b) Nota de SOLUCIONADO si falta ──
        -- Los tres casos que se marcaron por SQL quedaron sin ella: la linea de tiempo se DERIVA de
        -- las notas, asi que sin esta fila el caso pasa de abierto a cerrado sin decir cuando se
        -- resolvio. Se siembra con la fecha de solucion REAL, no con la de hoy, y con un prefijo
        -- propio -- el servicio escribe 'Solucionado: ', nunca este: asi el Down puede distinguir la
        -- suya de una legitima, y de paso el lector ve que la nota se anoto despues.
        IF v_solucion IS NOT NULL THEN
            INSERT INTO public.ticket_notas
                (ticket_id, user_id, nota, estado_resultante, es_interna, created_at)
            SELECT v_id, v_admin_ced,
                   'Solucionado (registro retroactivo): ' || v_solucion, 'SOLUCIONADO', false,
                   COALESCE(v_fecha_sol, c_cierre)
            WHERE NOT EXISTS (
                SELECT 1 FROM public.ticket_notas n
                WHERE n.ticket_id = v_id AND n.estado_resultante = 'SOLUCIONADO');
        END IF;

        -- ── c) Nota de cierre: dice QUIEN cerro y POR QUE ──
        v_dias := GREATEST(0, (c_cierre::date - COALESCE(v_fecha_sol, c_cierre)::date));

        IF r.solucion_nueva IS NOT NULL THEN
            v_nota := 'Cierre hecho por la gestion. Al revisar el tablero se verifico que el caso YA ESTABA RESUELTO Y DESPLEGADO en produccion, y que solo faltaba mover la tarjeta. ' || r.evidencia;
        ELSE
            v_nota := 'Cierre hecho por la gestion, no por el solicitante: el caso quedo SOLUCIONADO hace '
                   || v_dias || ' dias y no llego la confirmacion de cierre. ' || r.evidencia;
            IF v_notificado IS NOT TRUE THEN
                v_nota := v_nota || ' Se deja constancia de que a este caso no se le llego a enviar el aviso de solucion por correo, asi que quien lo reporto no tuvo como enterarse para confirmarlo.';
            END IF;
        END IF;

        v_nota := v_nota || ' Si el problema vuelve a presentarse, este caso se puede reabrir o se registra uno nuevo.';

        INSERT INTO public.ticket_notas
            (ticket_id, user_id, nota, estado_resultante, es_interna, created_at)
        SELECT v_id, v_admin_ced, v_nota, 'CERRADO', false, c_cierre
        WHERE NOT EXISTS (
            SELECT 1 FROM public.ticket_notas n
            WHERE n.ticket_id = v_id AND n.estado_resultante = 'CERRADO');

        -- ── d) Cerrar, escribiendo lo mismo que ConfirmarCierreAsync ──
        UPDATE public.tickets tk
           SET estado                   = 'CERRADO',
               fecha_cierre_solicitante = COALESCE(tk.fecha_cierre_solicitante, c_cierre),
               cerrado_por_user_id      = COALESCE(tk.cerrado_por_user_id, v_admin_ced),
               updated_by_user_id       = v_admin_ced,
               updated_at               = c_cierre
         WHERE tk.id = v_id;

        v_cerrados := v_cerrados + 1;
    END LOOP;

    RAISE NOTICE 'Cierre de casos resueltos: % cerrados, % saltados.', v_cerrados, v_saltados;
END $$;
";

        private const string REVERT_SQL = @"
-- Devuelve cada caso a su estado previo y borra SOLO lo que sembro el Up, comparando contra sus
-- propios valores: una fecha o una nota que ya venia de antes se conserva.
DO $$
DECLARE
    c_cierre constant timestamptz := '2026-08-31 18:00:00+00';

    r          record;
    v_id       bigint;
    v_solucion text;
BEGIN
    FOR r IN
        SELECT * FROM (VALUES
        ('TK-2026-000012', 'SOLUCIONADO', false),
        ('TK-2026-000013', 'SOLUCIONADO', false),
        ('TK-2026-000014', 'SOLUCIONADO', false),
        ('TK-2026-000015', 'SOLUCIONADO', false),
        ('TK-2026-000020', 'SOLUCIONADO', false),
        ('TK-2026-000163', 'SOLUCIONADO', false),
        ('TK-2026-000164', 'SOLUCIONADO', false),
        ('TK-2026-000165', 'SOLUCIONADO', false),
        ('TK-2026-000166', 'EN_ANALISIS', true),
        ('TK-2026-000176', 'SOLUCIONADO', false),
        ('TK-2026-000177', 'SOLUCIONADO', false),
        ('TK-2026-000185', 'SOLUCIONADO', false),
        ('TK-2026-000187', 'EN_ANALISIS', true)
        ) AS t(codigo, estado_previo, tenia_solucion_nueva)
    LOOP
        SELECT tk.id, tk.solucion_descripcion INTO v_id, v_solucion
        FROM public.tickets tk WHERE tk.codigo = r.codigo AND tk.deleted_at IS NULL
        ORDER BY tk.id LIMIT 1;

        CONTINUE WHEN v_id IS NULL;

        -- La nota de cierre lleva el timestamp exacto del Up.
        DELETE FROM public.ticket_notas
         WHERE ticket_id = v_id AND estado_resultante = 'CERRADO' AND created_at = c_cierre;

        -- La de SOLUCIONADO solo si la escribio el Up: su prefijo es propio y el servicio nunca lo
        -- usa, asi que una nota legitima -- que dice 'Solucionado: ' -- no puede caer aca por error.
        DELETE FROM public.ticket_notas
         WHERE ticket_id = v_id AND estado_resultante = 'SOLUCIONADO'
           AND v_solucion IS NOT NULL
           AND nota = 'Solucionado (registro retroactivo): ' || v_solucion;

        UPDATE public.tickets tk
           SET estado                   = r.estado_previo,
               fecha_cierre_solicitante = CASE WHEN tk.fecha_cierre_solicitante = c_cierre THEN NULL ELSE tk.fecha_cierre_solicitante END,
               cerrado_por_user_id      = NULL,
               solucion_descripcion     = CASE WHEN r.tenia_solucion_nueva THEN NULL ELSE tk.solucion_descripcion END,
               fecha_solucion           = CASE WHEN r.tenia_solucion_nueva AND tk.fecha_solucion = c_cierre THEN NULL ELSE tk.fecha_solucion END,
               updated_at               = c_cierre
         WHERE tk.id = v_id;
    END LOOP;
END $$;
";
    }
}
