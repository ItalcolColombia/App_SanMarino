using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Registra en ItalJira los dos casos que pidió el usuario el 15ago26 —el módulo de Empresas y
    /// los tabs sensibles de Roles— ya <c>CERRADO</c>s, más un tercer caso <b>abierto</b> por el
    /// hallazgo que esta entrega no resuelve.
    /// </summary>
    /// <remarks>
    /// <b>De dónde salió.</b> El usuario pidió validar el módulo Empresa, organizar sus modales
    /// («se desbordan») y ocultar los tabs Permisos y Menús de Roles para todo el que no sea el
    /// administrador de la aplicación con perfil <c>Admin</c>.
    ///
    /// <para>
    /// <b>Se siembran CERRADOs</b> porque el usuario lo pidió explícitamente: el desarrollo se hizo
    /// en la misma sesión que levantó los casos, así que abrirlos para cerrarlos acto seguido sería
    /// ruido en el tablero. Por eso llevan <c>fecha_solucion</c>, <c>fecha_cierre_solicitante</c> y
    /// <c>cerrado_por_user_id</c>: un CERRADO sin esas tres columnas se ve raro en el detalle.
    /// </para>
    ///
    /// <para>
    /// <b>El hallazgo pendiente va en su propio caso, abierto.</b> Los getters de plantilla del
    /// wizard de empresa (<c>filteredRoles</c>, <c>selectedRolesPermissions</c>,
    /// <c>previewRolePermissions</c>) alocan un array nuevo por ciclo de change detection — el
    /// patrón que CLAUDE.md prohíbe. No se tocó: memoizarlos es un refactor aparte y marcarlo
    /// resuelto sería mentir.
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
    public partial class SeedTicketEmpresaModalesYCatalogosGlobales : Migration
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
    v_tk_empresa  bigint;
    v_tk_catalogo bigint;
    v_tk_abierto  bigint;
    v_orden       integer;
BEGIN
    -- Identidad POR EMAIL: los guid difieren entre local y produccion.
    SELECT u.id INTO v_user_guid
    FROM public.users u
    JOIN public.user_logins ul ON ul.user_id = u.id
    JOIN public.logins l       ON l.id = ul.login_id
    WHERE lower(l.email) = 'moiesbbuga@gmail.com'
    LIMIT 1;

    IF v_user_guid IS NULL THEN
        RAISE NOTICE 'Empresas/catalogos globales: no existe moiesbbuga@gmail.com en este entorno; omitido.';
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
    WHERE h.titulo = 'Administracion: el formulario de Empresas y los catalogos globales de Roles'
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
             'Administracion: el formulario de Empresas y los catalogos globales de Roles',
             'Dos pedidos del usuario sobre las pantallas de administracion, trabajados juntos porque comparten el mismo diagnostico: pantallas que crecieron sin que nadie revisara el encuadre.

1) EMPRESAS. El wizard no tenia alto maximo ni scroll propio. Desde que el paso 2 sumo los 14 flags de comportamiento (12ago-15ago26) el modal supera la altura de la ventana, se centra igual y el boton Guardar Empresa queda fuera de pantalla. Validando el modulo aparecio ademas algo peor: el checkbox Acceso movil estaba bindeado DENTRO del grupo visualPermissions, que no lo contiene, asi que Angular reventaba el render del paso 2 completo.

2) ROLES. Los tabs Permisos y Menus no administran el rol: administran los catalogos GLOBALES del sistema (las keys de permiso y el arbol de menus que comparten todas las empresas). Los veia cualquiera con acceso al modulo, y el backend tampoco los cerraba.

Plan: fase_de_desarrollo/empresa_modales_y_catalogos_globales_plan.md
Tracker: bloque V4 de tracker_estado.md',
             'LISTO', 'ALTA', v_user_guid, v_orden,
             12.00, DATE '2026-08-15', DATE '2026-08-15',
             timezone('utc', now()), timezone('utc', now()),
             'frontend,backend,seguridad,administracion',
             v_company, v_cedula, timezone('utc', now()))
        RETURNING id INTO v_historia_id;

        v_his_codigo := 'HIS-2026-' || lpad(v_historia_id::text, 4, '0');
        UPDATE public.historias SET codigo = v_his_codigo WHERE id = v_historia_id;
    END IF;

    -- El codigo es la clave de idempotencia de las tareas: si quedo NULL, el NOT EXISTS de abajo
    -- comparararia contra NULL y reinsertaria las tareas en cada corrida.
    IF v_his_codigo IS NULL THEN
        v_his_codigo := 'HIS-2026-' || lpad(v_historia_id::text, 4, '0');
        UPDATE public.historias SET codigo = v_his_codigo WHERE id = v_historia_id AND codigo IS NULL;
    END IF;

    -- ═══════════════ 2) CASO 1 — EMPRESAS (CERRADO) ═══════════════
    SELECT t.id INTO v_tk_empresa
    FROM public.tickets t
    WHERE t.titulo = 'Empresas: el paso 2 del formulario reventaba y el modal se salia de la pantalla'
      AND t.deleted_at IS NULL
    LIMIT 1;

    IF v_tk_empresa IS NULL THEN
        SELECT COALESCE(MAX(t.orden_tablero) + 1, 0) INTO v_orden
        FROM public.tickets t WHERE t.estado = 'CERRADO' AND t.deleted_at IS NULL;

        INSERT INTO public.tickets
            (pais_id, tipo, estado, titulo, descripcion, solucion_descripcion,
             assigned_to_user_guid, created_by_user_guid, prioridad, orden_tablero,
             horas_estimadas, fecha_inicio_plan, fecha_fin_plan, fecha_primera_apertura,
             fecha_solucion, fecha_cierre_solicitante, cerrado_por_user_id,
             historia_id, status, notificado_correo,
             company_id, created_by_user_id, created_at)
        VALUES
            (v_pais, 'DESARROLLO', 'CERRADO',
             'Empresas: el paso 2 del formulario reventaba y el modal se salia de la pantalla',
             'PEDIDO DEL USUARIO (15ago26): validar el modulo Empresa y organizar los modales y las opciones, porque los modales se desbordan.

LO QUE SE ENCONTRO, de menor a mayor gravedad.

1) EL MODAL SE DESBORDA. El contenedor del wizard era max-w-5xl p-6 sin alto maximo ni scroll propio, dentro de un overlay que lo centra. Cuando el contenido supera la altura de la ventana el modal se sale por arriba y por abajo: el footer con Guardar Empresa queda fuera de pantalla y no hay forma de scrollear hasta el. Aparecio ahora porque el paso 2 crecio con los 14 flags de comportamiento.

2) LAS OPCIONES ESTABAN AMONTONADAS. En el paso 2 todo lo que no eran roles se apilaba en UNA sola media columna: permisos resultantes, detalle del rol, permisos de modulos, los 14 flags y el alimento previo al encaset. Contra una columna izquierda que solo tenia la lista de roles.

3) EL PASO 2 NO RENDERIZABA. El checkbox Acceso movil estaba dentro de <div formGroupName=""visualPermissions"">, pero mobileAccess se declara en la RAIZ del formulario. Angular resuelve formControlName contra el contenedor mas cercano, asi que buscaba visualPermissions.mobileAccess, no lo encontraba y tiraba Cannot find control. Reproducido en Chrome headless. Es de la primera version del archivo: nadie podia llegar al paso 2, o sea que NO se podia crear ni guardar una empresa desde la pantalla.

4) SI FALLABA EL GUARDADO SE PERDIA TODO. El cierre del modal estaba en el finalize del observable, que corre tambien por el camino de error: un fallo de red o una validacion del backend cerraba el modal y borraba lo cargado, dejando solo un toast rojo pidiendo intentar de nuevo sobre un formulario que ya no existia.

5) EL BUILD DEL FRONT ESTABA ROTO EN MAIN. ng build fallaba por presupuesto: el bundle inicial pesaba 2.07 MB contra un maximo de error de 2.05 MB. Verificado en el arbol limpio, antes de tocar nada.',
             'RESUELTO.

QUE SE CAMBIO
- Primitivas cm-modal* en el SCSS del modulo, calcadas de las rm-modal* que ya usaba Roles: overlay
  con padding, caja con max-height 92vh en columna, header y footer fijos y scroll SOLO en el
  cuerpo. Los cuatro modales del modulo (wizard, ver menu, asignar menu, permisos de empresa) pasan
  a esa estructura. El boton Guardar Empresa ya no se puede quedar fuera de pantalla.
- Paso 2 reorganizado por filas, de lo especifico a lo transversal: resumen, luego Roles y Permisos
  en dos columnas, y a ancho completo Accesos y modulos, Comportamiento del sistema (los 14 flags
  entran de a tres por fila en vez de apilarse) y Alimento previo al encaset.
- Acceso movil sale del formGroupName y cuelga de la raiz, que es donde vive el control. Cubierto
  por frontend/src/tests/company-mobile-access-binding.spec.ts, que fija las dos mitades: dentro del
  grupo revienta, en la raiz escribe sobre el control correcto.
- El cierre del modal se mueve del finalize al next: si el guardado falla, el formulario queda
  abierto con todo lo cargado.
- Empresas y Roles pasan a loadComponent (lazy). Estaban importadas de forma estatica en
  app.config.ts, asi que viajaban en el bundle inicial de todos los usuarios. El inicial baja de
  2.07 MB a 1.84 MB y ng build vuelve a pasar.

VERIFICACION
- yarn build: 0 errores. Queda el warning de presupuesto de 1.5 MB, que es el preexistente aceptado.
- Specs del front en Chrome headless: 8 en verde.
- dotnet build 0 errores y dotnet test 2600 en verde (no se toco logica de backend en este caso).',
             v_user_guid, v_user_guid, 'ALTA', v_orden,
             7.00, DATE '2026-08-15', DATE '2026-08-15', timezone('utc', now()),
             timezone('utc', now()), timezone('utc', now()), v_cedula,
             v_historia_id, 'A', false,
             v_company, v_cedula, timezone('utc', now()))
        RETURNING id INTO v_tk_empresa;

        UPDATE public.tickets
           SET codigo = 'TK-2026-' || lpad(v_tk_empresa::text, 6, '0')
         WHERE id = v_tk_empresa;
    END IF;

    -- ═══════════ 3) CASO 2 — CATALOGOS GLOBALES (CERRADO) ═══════════
    SELECT t.id INTO v_tk_catalogo
    FROM public.tickets t
    WHERE t.titulo = 'Roles: los catalogos globales de permisos y menus los podia tocar cualquiera'
      AND t.deleted_at IS NULL
    LIMIT 1;

    IF v_tk_catalogo IS NULL THEN
        SELECT COALESCE(MAX(t.orden_tablero) + 1, 0) INTO v_orden
        FROM public.tickets t WHERE t.estado = 'CERRADO' AND t.deleted_at IS NULL;

        INSERT INTO public.tickets
            (pais_id, tipo, estado, titulo, descripcion, solucion_descripcion,
             assigned_to_user_guid, created_by_user_guid, prioridad, orden_tablero,
             horas_estimadas, fecha_inicio_plan, fecha_fin_plan, fecha_primera_apertura,
             fecha_solucion, fecha_cierre_solicitante, cerrado_por_user_id,
             historia_id, status, notificado_correo,
             company_id, created_by_user_id, created_at)
        VALUES
            (v_pais, 'DESARROLLO', 'CERRADO',
             'Roles: los catalogos globales de permisos y menus los podia tocar cualquiera',
             'PEDIDO DEL USUARIO (15ago26): los tabs Permisos y Menus del modulo de Roles son delicados; no deben estar disponibles para las personas que tienen el permiso del modulo de roles y permisos, solo para el administrador de la aplicacion con perfil Admin.

POR QUE TIENE RAZON. Esos dos tabs no administran el rol: administran los catalogos GLOBALES del sistema. El tab Permisos crea, edita y borra las keys de permiso; el tab Menus crea, edita, borra y reordena el arbol de menus. Las dos estructuras las comparten TODAS las empresas y todos los paises: borrar una key o un item ahi se lo lleva puesto a todo el mundo a la vez. El modulo de Roles y Permisos lo tiene mucha gente (administradores de empresa, lideres de implementacion, soporte).

Y EL BACKEND TAMPOCO LO IMPEDIA, que es lo mas grave:
- PermissionController no tenia un solo [Authorize]. Solo lo cubria la FallbackPolicy, que pide token valido y nada mas: cualquier sesion podia POST, PUT y DELETE sobre el catalogo de permisos.
- MenuController y los endpoints menus/* de RoleController usaban la policy CanManageMenus, pero esa policy estaba definida en Program.cs como RequireAuthenticatedUser, con un TODO de seguridad escrito al lado. O sea: no filtraba nada.

Ocultar el tab sin tocar el backend habria sido teatro.',
             'RESUELTO en las dos capas.

FRONT (que se muestra), fail-closed
- Funcion pura funciones/catalogos-globales.funcion.ts: esAdminDeAplicacion(roles) + puedeVerTab().
  isAdminUser arranca en false; si la sesion no llega o falla, el modulo muestra solo Roles.
- Los dos tabs, sus botones de accion y sus dos modales CRUD quedan detras de verTab().
- irATab() reemplaza al activeTab=... inline y es el unico que escribe el tab: un tab reservado no
  se activa ni por codigo. abrirModalPermisos() y abrirModalMenu() tambien cortan de entrada.

BACK (que se puede)
- Calculo puro Application/Calculos/CatalogoGlobalAutorizacionCalculos.cs, con la misma regla.
- Policy AdminAplicacion en Program.cs, resuelta con ese calculo sobre los claims de rol.
- Aplicada a las ESCRITURAS de PermissionController (POST, PUT, DELETE), MenuController (POST, PUT,
  DELETE) y RoleController menus/* (POST, PUT, DELETE). Ahora responden 403 a quien no sea el admin
  de la aplicacion, aunque llame la API a mano.
- Las LECTURAS quedan abiertas a proposito: un usuario no admin necesita GET /api/Permission para
  poder asignarle permisos a un rol, y GET /api/Menu/tree para que la columna Menus de la tabla de
  roles muestre etiquetas en vez de ids. Cerrarlas romperia el modulo para todos.

QUE CUENTA COMO PERFIL ADMIN. Nombre de rol exactamente admin o administrador, ignorando mayusculas
y espacios al borde. Comparacion EXACTA, nunca por substring: en la base conviven Admin Panama,
Admin Demo, Ecuador Administrador, Santa Reyes Administrador y ADMINISTRADOR DE GRANJA, que son
administradores DE SU EMPRESA y no deben entrar. Hoy solo matchea el rol Admin (id 1, 2 usuarios).

VERIFICACION
- 19 tests xUnit nuevos (CatalogoGlobalAutorizacionCalculosTests) que fijan justamente esa frontera:
  si alguien cambia la comparacion exacta por un contains, se ponen en rojo. dotnet test 2600 en verde.
- 6 specs del front sobre la funcion espejo, en Chrome headless.
- dotnet build 0 errores. yarn build 0 errores.',
             v_user_guid, v_user_guid, 'ALTA', v_orden,
             5.00, DATE '2026-08-15', DATE '2026-08-15', timezone('utc', now()),
             timezone('utc', now()), timezone('utc', now()), v_cedula,
             v_historia_id, 'A', false,
             v_company, v_cedula, timezone('utc', now()))
        RETURNING id INTO v_tk_catalogo;

        UPDATE public.tickets
           SET codigo = 'TK-2026-' || lpad(v_tk_catalogo::text, 6, '0')
         WHERE id = v_tk_catalogo;
    END IF;

    -- ═══════════════════════ 4) TAREAS (todas LISTO) ═══════════════════════
    SELECT COALESCE(MAX(t.orden) + 1, 0) INTO v_orden
    FROM public.ticket_tareas t WHERE t.estado = 'LISTO' AND t.deleted_at IS NULL;

    INSERT INTO public.ticket_tareas
        (ticket_id, historia_id, codigo, tipo, estado, prioridad, titulo, descripcion,
         asignado_user_guid, orden, horas_estimadas, fecha_inicio_plan, fecha_fin_real, etiquetas,
         company_id, created_by_user_id, created_at)
    SELECT CASE WHEN t.n <= 5 THEN v_tk_empresa ELSE v_tk_catalogo END, v_historia_id,
           v_his_codigo || '-T' || t.n,
           t.tipo, 'LISTO', t.prioridad, t.titulo, t.descripcion,
           v_user_guid, v_orden + t.n - 1, t.horas::numeric(8,2),
           DATE '2026-08-15', timezone('utc', now()), t.etiquetas,
           v_company, v_cedula, timezone('utc', now())
    FROM (VALUES
        (1, 'TAREA', 'ALTA', 'C1.1 Primitivas cm-modal* en el SCSS del modulo',
            'Overlay con padding, caja max-height 92vh en columna, header y footer con flex-shrink 0 y cuerpo flex 1 con overflow-y auto. Calcadas de las rm-modal* de role-management, que ya estaban probadas: no se inventa un primitivo nuevo. El min-height 0 del cuerpo es obligatorio, sin el un hijo flex no se deja encoger y el scroll nunca aparece.', 1.5, 'frontend,ux'),
        (2, 'TAREA', 'ALTA', 'C1.2 Los cuatro modales de Empresas pasan a esa estructura',
            'Wizard, ver menu de la empresa, asignar menu y permisos de la empresa. Los tres ultimos ya tenian max-h 85vh pero con Tailwind suelto y repetido, y sus overlays sin padding tocaban los bordes en pantallas chicas.', 1, 'frontend,ux'),
        (3, 'TAREA', 'ALTA', 'C1.3 Paso 2 reorganizado por filas',
            'Resumen; despues Roles y Permisos en dos columnas; y a ancho completo Accesos y modulos, Comportamiento del sistema y Alimento previo al encaset. Los 14 flags entran de a tres por fila en vez de apilarse de a uno en media columna. Mismos controles, misma informacion: cambia la distribucion.', 1.5, 'frontend,ux'),
        (4, 'BUG', 'ALTA', 'C1.4 Acceso movil sale del formGroupName equivocado',
            'mobileAccess se declara en la raiz del formulario pero se bindeaba dentro del grupo visualPermissions, que solo tiene dashboard, reports, farms y users. Angular buscaba visualPermissions.mobileAccess y tiraba Cannot find control, reventando el render del paso 2 completo. Viene de la primera version del archivo. Spec company-mobile-access-binding.spec.ts que fija las dos mitades de la regla.', 2, 'frontend,bug'),
        (5, 'BUG', 'ALTA', 'C1.5 El modal ya no se cierra cuando falla el guardado + build del front reparado',
            'El cierre estaba en el finalize, que corre tambien en el camino de error: se perdia todo lo cargado. Pasa al next. Aparte, ng build ya fallaba en main por presupuesto (2.07 MB de bundle inicial contra 2.05 MB de maximo). Empresas y Roles estaban importadas de forma estatica en app.config.ts y viajaban en el inicial de todos; pasan a loadComponent y el inicial baja a 1.84 MB.', 1, 'frontend,bug,build'),
        (6, 'TAREA', 'ALTA', 'C2.1 Funcion pura catalogos-globales.funcion.ts',
            'esAdminDeAplicacion(roles) con comparacion exacta e ignorando mayusculas, puedeVerTab(tab, esAdmin) y tabPorDefecto(). Fail-closed: sin roles, null o undefined devuelve false. El componente pasa a orquestador: lee la sesion y delega la decision.', 1, 'frontend,seguridad'),
        (7, 'TAREA', 'ALTA', 'C2.2 Los tabs, sus botones y sus modales quedan detras de verTab()',
            'Y irATab() se vuelve el unico escritor de activeTab, asi que un tab reservado no se activa ni por codigo ni por un estado viejo. abrirModalPermisos() y abrirModalMenu() tambien cortan de entrada. Ocultar el boton no alcanza si el estado se puede escribir desde cualquier lado.', 1, 'frontend,seguridad'),
        (8, 'TAREA', 'ALTA', 'C2.3 Calculo puro CatalogoGlobalAutorizacionCalculos + 19 tests',
            'Static, sin EF, en Application/Calculos. Los tests fijan la frontera que hace util a la regla: los seis roles reales de la base cuyo nombre contiene Admin pero que administran su empresa (Admin Panama, Admin Demo, Ecuador Administrador, Santa Reyes Administrador, ADMINISTRADOR DE GRANJA, Administrador de Empresa) NO pueden escribir el catalogo global. Si alguien cambia la comparacion exacta por un contains, se ponen en rojo.', 1.5, 'backend,tests,seguridad'),
        (9, 'TAREA', 'ALTA', 'C2.4 Policy AdminAplicacion sobre las escrituras',
            'Aplicada a POST, PUT y DELETE de PermissionController y MenuController y a menus/* de RoleController. PermissionController no tenia un solo Authorize: solo lo cubria la FallbackPolicy. Las lecturas se dejan abiertas a proposito, porque el modulo de Roles las necesita para armar un rol y para mostrar etiquetas de menu en la tabla.', 1.5, 'backend,seguridad')
    ) AS t(n, tipo, prioridad, titulo, descripcion, horas, etiquetas)
    WHERE NOT EXISTS (
        SELECT 1 FROM public.ticket_tareas x WHERE x.codigo = v_his_codigo || '-T' || t.n
    );

    -- ═══════════════════════ 5) HORAS (= estimadas, sin medicion real) ═══════════════════════
    INSERT INTO public.ticket_tiempos (tarea_id, user_guid, user_id, fecha, horas, descripcion, created_at)
    SELECT t.id, v_user_guid, v_cedula, current_date, t.horas_estimadas,
           'Trabajo completado. Horas segun la estimacion acordada (no hay medicion real por tarea).',
           timezone('utc', now())
      FROM public.ticket_tareas t
     WHERE t.historia_id = v_historia_id
       AND t.horas_estimadas IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM public.ticket_tiempos tt WHERE tt.tarea_id = t.id);

    -- ═══════════ 6) CASO APARTE: el hallazgo que NO se resuelve ═══════════
    -- Abierto a proposito. Marcarlo cerrado seria mentir, y meterlo como tarea de la historia de
    -- arriba impediria dejarla en LISTO.
    SELECT t.id INTO v_tk_abierto
    FROM public.tickets t
    WHERE t.titulo = 'Wizard de empresa: getters de plantilla que alocan un array por ciclo'
      AND t.deleted_at IS NULL
    LIMIT 1;

    IF v_tk_abierto IS NULL THEN
        SELECT COALESCE(MAX(t.orden_tablero) + 1, 0) INTO v_orden
        FROM public.tickets t WHERE t.estado = 'EN_ANALISIS' AND t.deleted_at IS NULL;

        INSERT INTO public.tickets
            (pais_id, tipo, estado, titulo, descripcion,
             assigned_to_user_guid, created_by_user_guid, prioridad, orden_tablero,
             fecha_primera_apertura, status, notificado_correo,
             company_id, created_by_user_id, created_at)
        VALUES
            (v_pais, 'DESARROLLO', 'EN_ANALISIS',
             'Wizard de empresa: getters de plantilla que alocan un array por ciclo',
             'HALLAZGO (15ago26), de la misma validacion del modulo Empresa. NO se resolvio: es un refactor aparte y no queria mezclarse con el fix del modal.

QUE PASA. En company-management.component.ts, filteredRoles, selectedRolesPermissions y previewRolePermissions son getters llamados desde la plantilla que devuelven un array NUEVO en cada ciclo de change detection: filterRoles() filtra, getCombinedPermissions() arma un Set y lo ordena, getRolePermissions() mapea y ordena. Es exactamente el patron que CLAUDE.md prohibe y que ya tiene memoria propia por NG0103.

POR QUE NO REVIENTA HOY. El componente es Eager y los @for trackean por valor, asi que se nota como trabajo de mas en cada ciclo, no como error en pantalla. El mismo modulo ya resolvio el caso gemelo bien: permEditItemsFiltrados es un CAMPO que se recalcula desde el input, con el comentario explicando por que no es un getter.

QUE HAY QUE HACER. Mismo tratamiento: pasar los tres a campos recalculados cuando cambia la entrada (roleFilter, roleIds, previewRoleId), no por ciclo. Es mecanico pero toca el flujo de seleccion de roles, asi que merece su propia validacion.',
             v_user_guid, v_user_guid, 'BAJA', v_orden,
             timezone('utc', now()), 'A', false,
             v_company, v_cedula, timezone('utc', now()))
        RETURNING id INTO v_tk_abierto;

        UPDATE public.tickets
           SET codigo = 'TK-2026-' || lpad(v_tk_abierto::text, 6, '0')
         WHERE id = v_tk_abierto;
    END IF;

    RAISE NOTICE 'Empresas/catalogos globales: casos % y % CERRADOS (historia %), caso % abierto por los getters.',
        v_tk_empresa, v_tk_catalogo, v_historia_id, v_tk_abierto;
END $$;
");
        }

        /// <inheritdoc />
        /// <remarks>Borra las tareas, los worklogs, los tres casos y la historia sembrados acá.</remarks>
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
    WHERE h.titulo = 'Administracion: el formulario de Empresas y los catalogos globales de Roles'
    LIMIT 1;

    IF v_historia_id IS NOT NULL THEN
        DELETE FROM public.ticket_tiempos
         WHERE tarea_id IN (SELECT id FROM public.ticket_tareas WHERE historia_id = v_historia_id);
        DELETE FROM public.ticket_tareas
         WHERE historia_id = v_historia_id AND codigo LIKE v_his_codigo || '-T%';
    END IF;

    DELETE FROM public.tickets
     WHERE titulo IN (
        'Empresas: el paso 2 del formulario reventaba y el modal se salia de la pantalla',
        'Roles: los catalogos globales de permisos y menus los podia tocar cualquiera',
        'Wizard de empresa: getters de plantilla que alocan un array por ciclo');

    IF v_historia_id IS NOT NULL THEN
        DELETE FROM public.historias WHERE id = v_historia_id;
    END IF;
END $$;
");
        }
    }
}
