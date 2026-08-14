-- ═══════════════════════════════════════════════════════════════════════════════
-- Bitácora de julio y agosto 2026 — alineación MANUAL de producción
-- Contenido: un CASO (ticket) CERRADO por cada trabajo, enlazado a su tarea de ItalJira.
-- ═══════════════════════════════════════════════════════════════════════════════
-- Equivale, línea por línea, a la migración 20260814030000_SeedCasosCerradosBitacora.
-- Generado por fase_de_desarrollo/generadores/italjira_bitacora/generar_sql_prod.py — no editar
-- a mano: se cambia la migración y se regenera.
--
-- ¿POR QUÉ UNA FUNCIÓN? La consola de DB Studio rechaza cualquier ';' interno
-- (DbStudioSqlCalculos.ContainsMultipleStatements), así que un bloque DO no entra. El PASO 1 hay
-- que correrlo con psql / pgAdmin / DBeaver una sola vez; el PASO 2 sí entra por DB Studio.
--
-- ES IDEMPOTENTE y no choca con el despliegue: cuando la migración corra al arrancar la app va a
-- encontrar todo hecho y no va a tocar nada — EF solo la registra en __EFMigrationsHistory.
--
-- ───────────────────────────────────────────────────────────────────────────────
-- PASO 0 (opcional, una sentencia). Verificar el punto de partida:
--
--   SELECT (SELECT count(*) FROM public.tickets) AS casos,
--          (SELECT count(*) FROM public.ticket_tareas WHERE codigo LIKE 'SES-2026%') AS tareas_bitacora,
--          (SELECT count(*) FROM public.tickets WHERE descripcion LIKE '[Bitácora%') AS ya_aplicado
--
--   tareas_bitacora debe ser 39: esta migración NECESITA la anterior aplicada.
--
-- PASO 1 — crear la función (todo lo que sigue en este archivo).
-- PASO 2 — ejecutarla (una sentencia sola):
--
--   SELECT * FROM public.fn_casos_cerrados_bitacora()
--
-- PASO 3 — opcional, soltarla cuando ya no haga falta:
--
--   DROP FUNCTION public.fn_casos_cerrados_bitacora()
-- ───────────────────────────────────────────────────────────────────────────────

-- ─────────────────────────────────────────────────────────────────────────────
-- Un CASO (ticket) por cada trabajo de la bitácora de julio-agosto 2026.
-- Mismo origen que 20260814010000 (sesiones reales + commits): esto no agrega información
-- nueva, la publica en el módulo de Tickets, que es donde el usuario espera ver el trabajo
-- solucionado y cerrado. Cada caso queda ENLAZADO a su tarea de ItalJira (ticket_tareas.ticket_id).
-- ─────────────────────────────────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION public.fn_casos_cerrados_bitacora()
RETURNS TABLE (metrica text, valor bigint)
LANGUAGE plpgsql
AS $fn$
DECLARE
    v_user_guid uuid;
    v_cedula    integer;
    v_company   integer;
    v_pais      integer;
    v_ticket    bigint;
    v_next      integer;
    v_orden     integer;
BEGIN
    SELECT u.id INTO v_user_guid
    FROM public.users u
    JOIN public.user_logins ul ON ul.user_id = u.id
    JOIN public.logins l       ON l.id = ul.login_id
    WHERE lower(l.email) = 'moiesbbuga@gmail.com'
    LIMIT 1;

    IF v_user_guid IS NULL THEN
        RETURN QUERY SELECT 'OMITIDO: Casos de la bitácora: no existe moiesbbuga@gmail.com; omitido.'::text, 0::bigint;
        RETURN;
    END IF;

    SELECT t.created_by_user_id INTO v_cedula
    FROM public.tickets t WHERE t.created_by_user_guid = v_user_guid ORDER BY t.id DESC LIMIT 1;
    IF v_cedula IS NULL THEN
        SELECT CASE WHEN u.cedula ~ '^[0-9]{1,9}$' THEN u.cedula::integer ELSE 0 END
          INTO v_cedula FROM public.users u WHERE u.id = v_user_guid;
    END IF;
    v_cedula := COALESCE(v_cedula, 0);

    SELECT t.company_id, t.pais_id INTO v_company, v_pais
    FROM public.tickets t ORDER BY t.id DESC LIMIT 1;
    IF v_company IS NULL THEN
        SELECT c.id INTO v_company FROM public.companies c ORDER BY c.id LIMIT 1;
    END IF;
    v_company := COALESCE(v_company, 1);
    v_pais    := COALESCE(v_pais, 1);

    -- El correlativo arranca donde quedó el de la base: local y producción NO están en el mismo
    -- número, así que jamás se puede hardcodear.
    SELECT COALESCE(MAX(NULLIF(regexp_replace(codigo, '^TK-[0-9]{4}-', ''), '')::integer), 0) + 1
      INTO v_next
    FROM public.tickets
    WHERE codigo ~ '^TK-[0-9]{4}-[0-9]+$';

    SELECT COALESCE(MAX(orden_tablero) + 1, 0) INTO v_orden
    FROM public.tickets WHERE estado = 'CERRADO' AND deleted_at IS NULL;

    -- ── HIS-2026-0001-T10 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Tickets: notificados + notificaciones por correo (creación/cierre) + transferir + correos ""pro"" con logo', '[Bitácora jul-ago 2026] · tarea HIS-2026-0001-T10
Pedido del usuario: «t6engo estos requerimientos para postura , levante y produccion tengo este requerimeintos de colombia en excel donde expresa que neceista la locion sonbre esta necesidad necesito que me crees un loop para corregir todo el excel que te voy a pasar que tiene requermientos por cada parte generarlo muy profesional y nivel senior , mejroalo y estudia todo el codigo ya que todo esta en la aplciacion donde tenemos tabla genenica para sanmarino colombia tnemos una tbla tambien la idea es que siempre este»
Registrado desde el trabajo real del área de desarrollo (sesión 29953769, 2026-07-01).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-01 07:17:27+00', TIMESTAMPTZ '2026-07-01 14:36:25+00',
           'Qué se hizo (3 commits): chore: ciclo 2 - entorno local de validacion + baseline tests + barrido front; feat(C1): graficas levante consumen el endpoint BD (front ya no calcula); chore(inventario): S2 elimina ruta huérfana /inventario-management
Bugs encontrados en el camino: 0.
Evidencia: 60 archivos tocados · 7,6 h de sesión real · commits e9e72f2, e6e008d, d9e9377
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-01 14:36:25+00', v_cedula, false,
           'MEDIA', v_orden, 12.00, DATE '2026-07-01', DATE '2026-07-01',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-01 07:17:27+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0001-T10' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0001-T10'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0001-T10'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0011-T2 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Alineación Postura Colombia (Levante + Producción) contra Guía Genética', '[Bitácora jul-ago 2026] · tarea HIS-2026-0011-T2
Pedido del usuario: «t6engo estos requerimientos para postura , levante y produccion tengo este requerimeintos de colombia en excel donde expresa que neceista la locion sonbre esta necesidad necesito que me crees un loop para corregir todo el excel que te voy a pasar que tiene requermientos por cada parte generarlo muy profesional y nivel senior , mejroalo y estudia todo el codigo ya que todo esta en la aplciacion donde tenemos tabla genenica para sanmarino colombia tnemos una tbla tambien la idea es que siempre este»
Registrado desde el trabajo real del área de desarrollo (sesión 29953769, 2026-07-01).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-01 07:17:27+00', TIMESTAMPTZ '2026-07-01 14:36:25+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 16 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-01 14:36:25+00', v_cedula, false,
           'MEDIA', v_orden, 16.00, DATE '2026-07-01', DATE '2026-07-01',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-01 07:17:27+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0011-T2' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0011-T2'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0011-T2'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0001-T11 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'SoporteBot — Loop de soporte automatizado sobre el módulo de Tickets', '[Bitácora jul-ago 2026] · tarea HIS-2026-0001-T11
Pedido del usuario: «valdiame dentro del proyecto tengo ticket entonces en mi perfil que es el moiesbbuga@gmail.com y soy el desarrollador principal y necesito resolver errores o novedades y necesito validar la informacion pero quiero automatizar crear un hook o algo que entre a produccion y filtre traiga lo que se encuentre tome el ticket inicie lo que se va arealizar y pase a un loop de desarrollo y leugo notifique cuando se sierra en las opcioens de notificar que tiene el modulo ticket , y la idea es que sea un puente de alguna forma que claude pueda entrar y realziar solcuioens que tiene aplciadas ami o soluc»
Registrado desde el trabajo real del área de desarrollo (sesión 7c4d7cfb, 2026-07-03).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-03 07:37:43+00', TIMESTAMPTZ '2026-07-03 12:43:12+00',
           'Qué se hizo (34 commits): test(fase3/S4): Colombia->ModeloBNivelGranja + no-afectacion contable + evidencia BD; docs: Fase 3 paso 2 QA ESTABLE + plan paso 3 (alineacion front Colombia modelo B); feat(fase3/S3-S1): menú Colombia → inventario modelo B (/gestion-inventario + catálogo); feat(fase3/S3-S2): ingreso/traslado/recepción nivel granja para Colombia (modelo B); feat(fase3/S3-S3): gestion-inventario nivel granja para Colombia (front); feat(backend): alimento galpón/granja configurable + de-dup parser metadata; refactor(front): rebrand UX pro paleta Italcol naranja/dorado/blanco + menú pro + gestión-inventario; docs: planes de fase (alimento configurable, fn_metadata, refactor UX pro, service-token, soporte-bot) + tracker
Bugs encontrados en el camino: 4 — cada uno queda como subtarea BUG de la tarea HIS-2026-0001-T11.
Evidencia: 175 archivos tocados · 21,7 h de sesión real · commits ffd50c6, 77621d1, 2390238, adadfc8, edd4ebb, f23e14b, 733a1f2, 5a992fa, 22ce51a, f9a8f99
Estimación: 20 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-03 12:43:12+00', v_cedula, false,
           'MEDIA', v_orden, 20.00, DATE '2026-07-03', DATE '2026-07-03',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-03 07:37:43+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0001-T11' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0001-T11'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0001-T11'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0007-T6 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — `fn_metadata_items_kg` (parseo de metadata en Postgres) + equivalencia', '[Bitácora jul-ago 2026] · tarea HIS-2026-0007-T6
Pedido del usuario: «valdiame dentro del proyecto tengo ticket entonces en mi perfil que es el moiesbbuga@gmail.com y soy el desarrollador principal y necesito resolver errores o novedades y necesito validar la informacion pero quiero automatizar crear un hook o algo que entre a produccion y filtre traiga lo que se encuentre tome el ticket inicie lo que se va arealizar y pase a un loop de desarrollo y leugo notifique cuando se sierra en las opcioens de notificar que tiene el modulo ticket , y la idea es que sea un puente de alguna forma que claude pueda entrar y realziar solcuioens que tiene aplciadas ami o soluc»
Registrado desde el trabajo real del área de desarrollo (sesión 7c4d7cfb, 2026-07-03).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-03 07:37:43+00', TIMESTAMPTZ '2026-07-03 12:43:12+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-03 12:43:12+00', v_cedula, false,
           'MEDIA', v_orden, 8.00, DATE '2026-07-03', DATE '2026-07-03',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-03 07:37:43+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0007-T6' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0007-T6'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0007-T6'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0007-T7 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Unificar inventario Colombia en el módulo nuevo + migración de datos', '[Bitácora jul-ago 2026] · tarea HIS-2026-0007-T7
Pedido del usuario: «valdiame dentro del proyecto tengo ticket entonces en mi perfil que es el moiesbbuga@gmail.com y soy el desarrollador principal y necesito resolver errores o novedades y necesito validar la informacion pero quiero automatizar crear un hook o algo que entre a produccion y filtre traiga lo que se encuentre tome el ticket inicie lo que se va arealizar y pase a un loop de desarrollo y leugo notifique cuando se sierra en las opcioens de notificar que tiene el modulo ticket , y la idea es que sea un puente de alguna forma que claude pueda entrar y realziar solcuioens que tiene aplciadas ami o soluc»
Registrado desde el trabajo real del área de desarrollo (sesión 7c4d7cfb, 2026-07-03).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-03 07:37:43+00', TIMESTAMPTZ '2026-07-03 12:43:12+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 24 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-03 12:43:12+00', v_cedula, false,
           'MEDIA', v_orden, 24.00, DATE '2026-07-03', DATE '2026-07-03',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-03 07:37:43+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0007-T7' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0007-T7'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0007-T7'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0007-T8 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Mejora UX módulo Gastos de inventario (Ecuador · No alimentos · stock granja)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0007-T8
Pedido del usuario: «valdiame dentro del proyecto tengo ticket entonces en mi perfil que es el moiesbbuga@gmail.com y soy el desarrollador principal y necesito resolver errores o novedades y necesito validar la informacion pero quiero automatizar crear un hook o algo que entre a produccion y filtre traiga lo que se encuentre tome el ticket inicie lo que se va arealizar y pase a un loop de desarrollo y leugo notifique cuando se sierra en las opcioens de notificar que tiene el modulo ticket , y la idea es que sea un puente de alguna forma que claude pueda entrar y realziar solcuioens que tiene aplciadas ami o soluc»
Registrado desde el trabajo real del área de desarrollo (sesión 7c4d7cfb, 2026-07-03).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-03 07:37:43+00', TIMESTAMPTZ '2026-07-03 12:43:12+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-03 12:43:12+00', v_cedula, false,
           'MEDIA', v_orden, 8.00, DATE '2026-07-03', DATE '2026-07-03',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-03 07:37:43+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0007-T8' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0007-T8'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0007-T8'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0012-T11 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Alimento a nivel galpón vs granja — CONFIGURABLE (empresa + granja)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0012-T11
Pedido del usuario: «valdiame dentro del proyecto tengo ticket entonces en mi perfil que es el moiesbbuga@gmail.com y soy el desarrollador principal y necesito resolver errores o novedades y necesito validar la informacion pero quiero automatizar crear un hook o algo que entre a produccion y filtre traiga lo que se encuentre tome el ticket inicie lo que se va arealizar y pase a un loop de desarrollo y leugo notifique cuando se sierra en las opcioens de notificar que tiene el modulo ticket , y la idea es que sea un puente de alguna forma que claude pueda entrar y realziar solcuioens que tiene aplciadas ami o soluc»
Registrado desde el trabajo real del área de desarrollo (sesión 7c4d7cfb, 2026-07-03).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-03 07:37:43+00', TIMESTAMPTZ '2026-07-03 12:43:12+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-03 12:43:12+00', v_cedula, false,
           'MEDIA', v_orden, 12.00, DATE '2026-07-03', DATE '2026-07-03',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-03 07:37:43+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0012-T11' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0012-T11'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0012-T11'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0016-T5 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           '[ARCHIVADO 2026-07-03] Tracker — Fase 3 (paso 2/3): consumo Colombia modelo A → modelo B', '[Bitácora jul-ago 2026] · tarea HIS-2026-0016-T5
Pedido del usuario: «valdiame dentro del proyecto tengo ticket entonces en mi perfil que es el moiesbbuga@gmail.com y soy el desarrollador principal y necesito resolver errores o novedades y necesito validar la informacion pero quiero automatizar crear un hook o algo que entre a produccion y filtre traiga lo que se encuentre tome el ticket inicie lo que se va arealizar y pase a un loop de desarrollo y leugo notifique cuando se sierra en las opcioens de notificar que tiene el modulo ticket , y la idea es que sea un puente de alguna forma que claude pueda entrar y realziar solcuioens que tiene aplciadas ami o soluc»
Registrado desde el trabajo real del área de desarrollo (sesión 7c4d7cfb, 2026-07-03).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-03 07:37:43+00', TIMESTAMPTZ '2026-07-03 12:43:12+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-03 12:43:12+00', v_cedula, false,
           'MEDIA', v_orden, 2.00, DATE '2026-07-03', DATE '2026-07-03',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-03 07:37:43+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0016-T5' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0016-T5'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0016-T5'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0017-T2 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Refactor UX/Visual PRO del Frontend (Angular) · Paleta Italcol naranja/rojo/blanco', '[Bitácora jul-ago 2026] · tarea HIS-2026-0017-T2
Pedido del usuario: «valdiame dentro del proyecto tengo ticket entonces en mi perfil que es el moiesbbuga@gmail.com y soy el desarrollador principal y necesito resolver errores o novedades y necesito validar la informacion pero quiero automatizar crear un hook o algo que entre a produccion y filtre traiga lo que se encuentre tome el ticket inicie lo que se va arealizar y pase a un loop de desarrollo y leugo notifique cuando se sierra en las opcioens de notificar que tiene el modulo ticket , y la idea es que sea un puente de alguna forma que claude pueda entrar y realziar solcuioens que tiene aplciadas ami o soluc»
Registrado desde el trabajo real del área de desarrollo (sesión 7c4d7cfb, 2026-07-03).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-03 07:37:43+00', TIMESTAMPTZ '2026-07-03 12:43:12+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 16 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-03 12:43:12+00', v_cedula, false,
           'MEDIA', v_orden, 16.00, DATE '2026-07-03', DATE '2026-07-03',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-03 07:37:43+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0017-T2' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0017-T2'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0017-T2'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0017-T3 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Sistema de diseño compartido (`shared/ui/`) + reducción de duplicación front', '[Bitácora jul-ago 2026] · tarea HIS-2026-0017-T3
Pedido del usuario: «valdiame dentro del proyecto tengo ticket entonces en mi perfil que es el moiesbbuga@gmail.com y soy el desarrollador principal y necesito resolver errores o novedades y necesito validar la informacion pero quiero automatizar crear un hook o algo que entre a produccion y filtre traiga lo que se encuentre tome el ticket inicie lo que se va arealizar y pase a un loop de desarrollo y leugo notifique cuando se sierra en las opcioens de notificar que tiene el modulo ticket , y la idea es que sea un puente de alguna forma que claude pueda entrar y realziar solcuioens que tiene aplciadas ami o soluc»
Registrado desde el trabajo real del área de desarrollo (sesión 7c4d7cfb, 2026-07-03).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-03 07:37:43+00', TIMESTAMPTZ '2026-07-03 12:43:12+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 20 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-03 12:43:12+00', v_cedula, false,
           'MEDIA', v_orden, 20.00, DATE '2026-07-03', DATE '2026-07-03',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-03 07:37:43+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0017-T3' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0017-T3'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0017-T3'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0019-T15 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Upgrade Angular 20 → 22 (+ refactor de deprecaciones)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0019-T15
Pedido del usuario: «valdiame dentro del proyecto tengo ticket entonces en mi perfil que es el moiesbbuga@gmail.com y soy el desarrollador principal y necesito resolver errores o novedades y necesito validar la informacion pero quiero automatizar crear un hook o algo que entre a produccion y filtre traiga lo que se encuentre tome el ticket inicie lo que se va arealizar y pase a un loop de desarrollo y leugo notifique cuando se sierra en las opcioens de notificar que tiene el modulo ticket , y la idea es que sea un puente de alguna forma que claude pueda entrar y realziar solcuioens que tiene aplciadas ami o soluc»
Registrado desde el trabajo real del área de desarrollo (sesión 7c4d7cfb, 2026-07-03).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-03 07:37:43+00', TIMESTAMPTZ '2026-07-03 12:43:12+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 20 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-03 12:43:12+00', v_cedula, false,
           'MEDIA', v_orden, 20.00, DATE '2026-07-03', DATE '2026-07-03',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-03 07:37:43+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0019-T15' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0019-T15'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0019-T15'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0019-T16 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Upgrade backend .NET 9 → 10 (LTS)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0019-T16
Pedido del usuario: «valdiame dentro del proyecto tengo ticket entonces en mi perfil que es el moiesbbuga@gmail.com y soy el desarrollador principal y necesito resolver errores o novedades y necesito validar la informacion pero quiero automatizar crear un hook o algo que entre a produccion y filtre traiga lo que se encuentre tome el ticket inicie lo que se va arealizar y pase a un loop de desarrollo y leugo notifique cuando se sierra en las opcioens de notificar que tiene el modulo ticket , y la idea es que sea un puente de alguna forma que claude pueda entrar y realziar solcuioens que tiene aplciadas ami o soluc»
Registrado desde el trabajo real del área de desarrollo (sesión 7c4d7cfb, 2026-07-03).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-03 07:37:43+00', TIMESTAMPTZ '2026-07-03 12:43:12+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-03 12:43:12+00', v_cedula, false,
           'MEDIA', v_orden, 12.00, DATE '2026-07-03', DATE '2026-07-03',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-03 07:37:43+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0019-T16' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0019-T16'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0019-T16'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260709-9cca ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Seguimiento Diario Levante: dos tipos de alimento (hembras y machos) por registro', '[Bitácora jul-ago 2026] · tarea SES-20260709-9cca
Pedido del usuario: «en el modulo de seguimiento diario levante tengo que agregar al momento de realziar un nuevo registro , quiero tener dos tipso de alimento uno para hembras y el otro para machos asi seleciono el tipo de alimento que estoy alimentando para el mocho y para la hembra , y asi serian dos tipos de alimento o el mismo para macho o para ehmbras aqui dejo el pantallso y quiero que cuando selecione el aliemnt ome debe mostrar la cantidad de alimento que tiene , debemos tambian separa el alimento que si es el mismo separo lo que estoy colocando en el consumo asi si no hay alimento solo se el echo a he»
Registrado desde el trabajo real del área de desarrollo (sesión 9cca1c87, 2026-07-09).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-09 13:46:28+00', TIMESTAMPTZ '2026-07-10 02:14:15+00',
           'Qué se hizo (1 commit): refactor(inventario): migrar modal levante a ItemInventarioDto y eliminar alias TS deprecado (frontend 100% neutro)
Bugs encontrados en el camino: 0.
Evidencia: 31 archivos tocados · 4,2 h de sesión real · commits c5ef5f9
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-10 02:14:15+00', v_cedula, false,
           'MEDIA', v_orden, 10.00, DATE '2026-07-09', DATE '2026-07-10',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-09 13:46:28+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260709-9cca' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260709-9cca'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260709-9cca'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260710-50cd ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Un solo comando para levantar back y front en .NET 10 (make dev)', '[Bitácora jul-ago 2026] · tarea SES-20260710-50cd
Pedido del usuario: «como levanto el back y el front por que en tro en linea de comando y me da error si no el make lo editamos para acomodar con le .net 10 y el front que tenga un solo comand opara levantar dev»
Registrado desde el trabajo real del área de desarrollo (sesión 50cd7cfb, 2026-07-10).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-10 05:39:32+00', TIMESTAMPTZ '2026-07-10 07:12:48+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea SES-20260710-50cd.
Evidencia: 6 archivos tocados · 0,6 h de sesión real · commits a1f0af3
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-10 07:12:48+00', v_cedula, false,
           'MEDIA', v_orden, 2.00, DATE '2026-07-10', DATE '2026-07-10',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-10 05:39:32+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260710-50cd' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260710-50cd'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260710-50cd'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260710-ff01 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Seguimiento pollo engorde: el tipo de ítem sale del formulario y el alimento queda definido', '[Bitácora jul-ago 2026] · tarea SES-20260710-ff01
Pedido del usuario: «en el modulo de seguimeinto diario pollo engorde necesito al moment ode realizar un registro nuevo por defecto el campo tipo de iten se quite y este el alimento definido sin mostrar solo mostraria alimento donde lecionan el alimento tamibne quiero saber que pasa cuando se agrega dos alimentos para machos yo debo selecionar eso por que hay momento que oueden mesclar el alimento viejo con el nuevo y eso da una cantidad de consumo se puede decir que de alimento A. se comio 50 kg y del B. 20 el macho entonces eso debo tenerlo mapiado en la tabla y en el seguimeinto diario que muestra individual l»
Registrado desde el trabajo real del área de desarrollo (sesión ff01dc07, 2026-07-10).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-10 06:28:45+00', TIMESTAMPTZ '2026-07-10 07:33:23+00',
           'Qué se hizo (3 commits): refactor(inventario): naming neutro del catálogo (ItemInventario) compartido EC/PA/CO; procesos de mirgaciiones; docs(inventario-rename): ratificar decisiones 2a sesion (conservar simbolos EC/PA, dejar modal levante, diferir Fase C BD)
Bugs encontrados en el camino: 0.
Evidencia: 13 archivos tocados · 1,1 h de sesión real · commits c2dd7a2, 7f077ac, 07a94b7
Estimación: 3 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-10 07:33:23+00', v_cedula, false,
           'MEDIA', v_orden, 3.00, DATE '2026-07-10', DATE '2026-07-10',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-10 06:28:45+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260710-ff01' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260710-ff01'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260710-ff01'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0007-T11 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Seguimiento diario: catálogo de alimento desde inventario NUEVO + alimento distinto para machos', '[Bitácora jul-ago 2026] · tarea HIS-2026-0007-T11
Pedido del usuario: «ya tenia una solucion para este problema pero nunca se mergio o no se desarrollo primero tengo que validar que tenga mos la lsita de aliemntos del inventario neuvo que se esta implmentado ya que esta apuntando al viejo y debe traerme lso que estan completos , ya que tambien me debe mostrar el tipo de alimento para macho ya que puedo alimentar el macho con con otro alimento entonces tenismoa que agregarlo a la base de datos y al reprote de seguimeinto diario y que visual mente se vea la division pero dbe sumarce el sonsumo si es el mismo alimento lo suma y realiza el descuento : esta es la co»
Registrado desde el trabajo real del área de desarrollo (sesión 3273cd7a, 2026-07-10).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-10 17:01:55+00', TIMESTAMPTZ '2026-07-10 19:37:56+00',
           'Qué se hizo (7 commits): feat(seguimiento-levante): ItemSeguimientoDto gana campo nombre; feat(seguimiento-produccion): ItemSeguimientoDto gana campo nombre; feat(seguimiento-levante): Colombia lee alimento del inventario nuevo + alimento independiente por sexo; feat(seguimiento-levante): UI bloques Hembras/Machos independientes; feat(seguimiento-produccion): Colombia lee alimento del inventario nuevo; docs(seguimiento-inventario): plan de catalogo de alimento nuevo + alimento por sexo; docs(seguimiento-inventario): tracker de estado
Bugs encontrados en el camino: 4 — cada uno queda como subtarea BUG de la tarea HIS-2026-0007-T11.
Evidencia: 18 archivos tocados · 1,7 h de sesión real · commits fb0cd36, 89e1f5b, 8e9bbc1, 58099b4, 0e8aaba, 4136a12, c9dbe6a, 09df059, f3f9c1d, 97ba976
Estimación: 14 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-10 19:37:56+00', v_cedula, false,
           'MEDIA', v_orden, 14.00, DATE '2026-07-10', DATE '2026-07-10',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-10 17:01:55+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0007-T11' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0007-T11'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0007-T11'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260710-2cdf ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Explicación del CI/CD actual: qué despliega, cómo y con qué credenciales', '[Bitácora jul-ago 2026] · tarea SES-20260710-2cdf
Pedido del usuario: «el proyecto actual mente con ci/cd como realiza todo : Hola Moises como vas?, realmente para que nose me pierda tu contacto y no perder la pregunta que me surgio y es tu haces despliegue de infra en AWS a traves de github? o los pipes son solo para el despliegue de codigo?»
Registrado desde el trabajo real del área de desarrollo (sesión 2cdf3319, 2026-07-10).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-10 17:05:35+00', TIMESTAMPTZ '2026-07-10 17:47:51+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 5 archivos tocados · 0,2 h de sesión real
Estimación: 1 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-10 17:47:51+00', v_cedula, false,
           'MEDIA', v_orden, 1.00, DATE '2026-07-10', DATE '2026-07-10',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-10 17:05:35+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260710-2cdf' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260710-2cdf'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260710-2cdf'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0006-T2 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Fase 3 — Migraciones Masivas: Ventas + Movimiento Aves + Movimiento Huevos (ESPECIFICACIÓN)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0006-T2
Pedido del usuario: «Requerimiento de Desarrollo Módulo de Migraciones Masivas - Postura Objetivo Se debe crear un nuevo módulo independiente encargado de realizar la migración masiva de información mediante archivos Excel. Este módulo permitirá reducir el proceso manual de parametrización inicial de una empresa y facilitar la carga de información histórica de Postura. No reemplaza los módulos existentes. Todos los módulos actuales continúan siendo la fuente oficial de información. El módulo únicamente automatiza la creación masiva utilizando las mismas reglas de negocio existentes. Objetivos del módulo El módulo»
Registrado desde el trabajo real del área de desarrollo (sesión 3602f5ab, 8ed99c77, 5b68e3ea, 2026-07-12).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-12 12:32:26+00', TIMESTAMPTZ '2026-08-01 01:53:55+00',
           'Qué se hizo (1 commit): fy
Bugs encontrados en el camino: 2 — cada uno queda como subtarea BUG de la tarea HIS-2026-0006-T2.
Evidencia: 109 archivos tocados · 2,8 h de sesión real · commits 1126280, 2eab7f8, 4e49369
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-01 01:53:55+00', v_cedula, false,
           'MEDIA', v_orden, 6.00, DATE '2026-07-12', DATE '2026-08-01',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-12 12:32:26+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0006-T2' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0006-T2'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0006-T2'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0006-T5 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Módulo de Migraciones Masivas (Postura)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0006-T5
Pedido del usuario: «Requerimiento de Desarrollo Módulo de Migraciones Masivas - Postura Objetivo Se debe crear un nuevo módulo independiente encargado de realizar la migración masiva de información mediante archivos Excel. Este módulo permitirá reducir el proceso manual de parametrización inicial de una empresa y facilitar la carga de información histórica de Postura. No reemplaza los módulos existentes. Todos los módulos actuales continúan siendo la fuente oficial de información. El módulo únicamente automatiza la creación masiva utilizando las mismas reglas de negocio existentes. Objetivos del módulo El módulo»
Registrado desde el trabajo real del área de desarrollo (sesión 3602f5ab, 8ed99c77, 2026-07-12).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-12 12:32:26+00', TIMESTAMPTZ '2026-07-12 21:58:56+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 32 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-12 21:58:56+00', v_cedula, false,
           'MEDIA', v_orden, 32.00, DATE '2026-07-12', DATE '2026-07-12',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-12 12:32:26+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0006-T5' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0006-T5'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0006-T5'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0006-T4 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Migraciones Masivas: línea ENGORDE (Lotes / Seguimiento / Venta)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0006-T4
Pedido del usuario: «vamos a crear migraciones para lotes pollo engorde ,seguimiento diario pollo engorde , venta de pollo engorde valida eso para crearle las migraciones masivo ya tenemos el de granja , nucleo y galpon ,»
Registrado desde el trabajo real del área de desarrollo (sesión 8ed99c77, 2026-07-12).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-12 21:13:02+00', TIMESTAMPTZ '2026-07-12 21:58:56+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 20 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-12 21:58:56+00', v_cedula, false,
           'MEDIA', v_orden, 20.00, DATE '2026-07-12', DATE '2026-07-12',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-12 21:13:02+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0006-T4' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0006-T4'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0006-T4'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0010-T10 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Bloquear venta de lotes cerrados / corridas anteriores en ""Venta por granja"" (Movimientos Pollo Engorde)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0010-T10
Pedido del usuario: «en el modulo de ventas necesito que cunado ya un lote este cerrado dentro de ungalpon o ya este otra corrida en el mismo galpon es decir si ya estan en la corrida 2603 ya , no dejar que realizen descuentos de los lotes anteriores si no tiene el persmiso de venta de lotes cerrado o anteriores , asi evitamos que usuarios cojan aves de lotes que ya estan cuadrados y lso metan en la venta de la aplicacion esto es el el modulo de venta pollo engorde : dejo un ejemplo : de la granja : Granja: Kilometro 61 (varios galpones / lotes) , el back debe brindar esa parte tmabine pero mas el front para qu»
Registrado desde el trabajo real del área de desarrollo (sesión 3074a312, 2026-07-14).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-14 11:13:51+00', TIMESTAMPTZ '2026-07-14 11:37:41+00',
           'Qué se hizo (4 commits): feat(movimientos-pollo-engorde): bloquear venta de lotes cerrados o corridas anteriores; feat(migraciones-masivas): esquema unico de plantilla/validacion, historial paginado y fix de descuento incremental de aves; feat(migraciones-masivas): permisos por linea (carga_masiva_pollo_engorde / carga_masiva_postura); docs(tracker): cerrar tracker de permisos carga masiva con hash del commit
Bugs encontrados en el camino: 0.
Evidencia: 16 archivos tocados · 0,4 h de sesión real · commits 62ede31, af3ad69, 354368f, cd3ca63
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-14 11:37:41+00', v_cedula, false,
           'MEDIA', v_orden, 6.00, DATE '2026-07-14', DATE '2026-07-14',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-14 11:13:51+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0010-T10' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0010-T10'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0010-T10'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260714-b0af ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'El select de alimentos de Seguimiento Producción listaba ítems sin stock', '[Bitácora jul-ago 2026] · tarea SES-20260714-b0af
Pedido del usuario: «en el modulo de seguimiento diario produccion al momento de abrir el modal de registrar un nuevo segumiento , en el select que me muestra los aliemtnos necesito que me lsite los alimentos que tiene inventario pro que actual mente me los muestra todos a si no tenga invetario este es el servicio que utiliza si algo creemos un nuevo api que identifique que es del modulo de seguimeinto diario produccion y aplciamos la logica que necestiamos ya que no sabemos donde mas necesitemos ese servicio o le agregamos un condicion que cuando enviaseguimiento_produccion , aplcia el flitro de solo los que tn»
Registrado desde el trabajo real del área de desarrollo (sesión b0af1fb8, 2026-07-14).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-14 12:16:31+00', TIMESTAMPTZ '2026-07-14 12:26:23+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea SES-20260714-b0af.
Evidencia: 4 archivos tocados · 0,2 h de sesión real · commits 19d2f58
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-14 12:26:23+00', v_cedula, false,
           'ALTA', v_orden, 2.00, DATE '2026-07-14', DATE '2026-07-14',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-14 12:16:31+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260714-b0af' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260714-b0af'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260714-b0af'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260714-bab8 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'El consumo de Seguimiento Producción no descontaba el inventario (ítems camino-2)', '[Bitácora jul-ago 2026] · tarea SES-20260714-bab8
Pedido del usuario: «realziae un seguimiento daiario porduccion al momento de realizar el descuento del consumo no se aplico el el descuento al inventario de alimento , ya estamos apuntando a un nueva tabala que implemntadmos de inventario entonces peude ser que el cambio no funcione , pero no esta apciando el consumo del inventario y tmaibne si en un consumo que coloco tipo embra coloco 100 y el iten tiene 120 , para el consumo de macho que esta abajo debe mostrar los 20 solamente , ya que si no se controla esto pueden hacer un consumo de 100 en los dos y quedaria en negativo de mas proque no se controla la exi»
Registrado desde el trabajo real del área de desarrollo (sesión bab8ee83, 2026-07-14).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-14 12:33:18+00', TIMESTAMPTZ '2026-07-14 14:05:39+00',
           'Qué se hizo (1 commit): Merge pull request #31 from ItalcolColombia/claude/infallible-brahmagupta-90c317
Bugs encontrados en el camino: 2 — cada uno queda como subtarea BUG de la tarea SES-20260714-bab8.
Evidencia: 6 archivos tocados · 0,7 h de sesión real · commits 13ce348, 99c8736, 92087b4
Estimación: 5 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-14 14:05:39+00', v_cedula, false,
           'ALTA', v_orden, 5.00, DATE '2026-07-14', DATE '2026-07-14',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-14 12:33:18+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260714-bab8' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260714-bab8'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260714-bab8'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0015-T5 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'DB Studio — Rediseño ""pro"", endurecimiento y permisos por tabla', '[Bitácora jul-ago 2026] · tarea HIS-2026-0015-T5
Pedido del usuario: «tengo el modulo de db_studio quiero tamibne poder realiza copia de de segudiad quiere decir back y que sean descargable asi no entrar a la aplciacion en produccion ya que me cuesta mucho para estar entrando para lo que son copias de seguridad y que descargue en fomratos sql y debe tener la siguitne estrutura de descargas sanmarino-(fecha actual)-produccion»
Registrado desde el trabajo real del área de desarrollo (sesión 264dbd27, 2026-07-14).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-14 14:26:36+00', TIMESTAMPTZ '2026-07-14 16:42:57+00',
           'Qué se hizo (3 commits): migracion(seguimiento-produccion): backfill idempotente de company_id en seguimiento_diario_produccion; Merge pull request #32 from ItalcolColombia/claude/infallible-brahmagupta-90c317; feat(db-studio): copia de seguridad completa descargable (SQL)
Bugs encontrados en el camino: 0.
Evidencia: 28 archivos tocados · 1,6 h de sesión real · commits 8c92c8c, 5e5461b, 786de13
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-14 16:42:57+00', v_cedula, false,
           'MEDIA', v_orden, 12.00, DATE '2026-07-14', DATE '2026-07-14',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-14 14:26:36+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0015-T5' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0015-T5'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0015-T5'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0015-T7 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'DB Studio — Copia de seguridad completa descargable (SQL)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0015-T7
Pedido del usuario: «tengo el modulo de db_studio quiero tamibne poder realiza copia de de segudiad quiere decir back y que sean descargable asi no entrar a la aplciacion en produccion ya que me cuesta mucho para estar entrando para lo que son copias de seguridad y que descargue en fomratos sql y debe tener la siguitne estrutura de descargas sanmarino-(fecha actual)-produccion»
Registrado desde el trabajo real del área de desarrollo (sesión 264dbd27, 2026-07-14).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-14 14:26:36+00', TIMESTAMPTZ '2026-07-14 16:42:57+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-14 16:42:57+00', v_cedula, false,
           'MEDIA', v_orden, 10.00, DATE '2026-07-14', DATE '2026-07-14',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-14 14:26:36+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0015-T7' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0015-T7'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0015-T7'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0012-T13 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Fix: ""aves vivas"" (tabla diaria / liquidación) ignora mortalidad en caja (mort_caja_h/m)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0012-T13
Pedido del usuario: «tengo un error en los datos que muestra en aves vivas y lo que muestra en cantidad de aves disponible que trae el seguimeit o aqui dejo el servicio que muestre que hay 17 aves disponibles vivas pero el otro servicio que muestra en la parte superir dice que solo hay 0 machos y 0 hembras disponibles quiero valdiar pro que el seguimeitn omuestra lso 17 o que paso paso la imagen de la informacion del lote y todo y en el ultimo registro esta la novedad que dejo especificado encintra si es ventas sin aprovar o que paso en si pro que esta ese descueido y podra aver mas lotes de la correida 03 en otra»
Registrado desde el trabajo real del área de desarrollo (sesión c108d4ad, 2026-07-14).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-14 16:54:02+00', TIMESTAMPTZ '2026-07-14 17:39:00+00',
           'Qué se hizo (2 commits): refactor(devpilot): Refactor SeguimientoAvesEngordeService (1884 líneas); refactor(devpilot): Refactor IndicadorEcuadorService (1185) y SeguimientoAvesEngordeEcuadorService (1087)
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea HIS-2026-0012-T13.
Evidencia: 21 archivos tocados · 0,8 h de sesión real · commits 7e524f8, 473f5ac, c6bbd29
Estimación: 4 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-14 17:39:00+00', v_cedula, false,
           'ALTA', v_orden, 4.00, DATE '2026-07-14', DATE '2026-07-14',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-14 16:54:02+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0012-T13' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0012-T13'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0012-T13'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0003-T2 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           '🔄 CONTEXTO DE TRASPASO — módulo de Vacunación (cronogramas por lote)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0003-T2
Pedido del usuario: «tenia una sesion que era para crear los modulos de vacunacion pero no la veo la sesion»
Registrado desde el trabajo real del área de desarrollo (sesión a3d18c7f, 3693d66d, 2026-07-14).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-14 17:28:23+00', TIMESTAMPTZ '2026-07-15 14:06:18+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 3 archivos tocados · 0,2 h de sesión real
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-15 14:06:18+00', v_cedula, false,
           'MEDIA', v_orden, 2.00, DATE '2026-07-14', DATE '2026-07-15',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-14 17:28:23+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0003-T2' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0003-T2'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0003-T2'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0019-T10 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           '🔄 CONTEXTO DE TRASPASO — sesión de refactor/optimización multi-país', '[Bitácora jul-ago 2026] · tarea HIS-2026-0019-T10
Pedido del usuario: «tenia una sesion que era para crear los modulos de vacunacion pero no la veo la sesion»
Registrado desde el trabajo real del área de desarrollo (sesión a3d18c7f, 2026-07-14).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-14 17:28:23+00', TIMESTAMPTZ '2026-07-14 17:37:58+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-14 17:37:58+00', v_cedula, false,
           'MEDIA', v_orden, 2.00, DATE '2026-07-14', DATE '2026-07-14',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-14 17:28:23+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0019-T10' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0019-T10'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0019-T10'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0003-T1 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Módulo Vacunación — cronogramas por lote/granja/galpón', '[Bitácora jul-ago 2026] · tarea HIS-2026-0003-T1
Pedido del usuario: «fase_de_desarrollo/CONTEXTO_TRASPASO_VACUNACION.md»
Registrado desde el trabajo real del área de desarrollo (sesión 3693d66d, 2026-07-14).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-14 17:38:48+00', TIMESTAMPTZ '2026-07-15 14:06:18+00',
           'Qué se hizo (1 commit): feat(vacunacion): agrega modulo de cronogramas de vacunacion por lote
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea HIS-2026-0003-T1.
Evidencia: 84 archivos tocados · 2,0 h de sesión real · commits 57763f6, d44cb07
Estimación: 24 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-15 14:06:18+00', v_cedula, false,
           'MEDIA', v_orden, 24.00, DATE '2026-07-14', DATE '2026-07-15',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-14 17:38:48+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0003-T1' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0003-T1'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0003-T1'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260714-21a8 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Vacunación: el cronograma no traía nada por permisos faltantes', '[Bitácora jul-ago 2026] · tarea SES-20260714-21a8
Pedido del usuario: «en el modulo de vacunancion cuando le doy clic en cronograma no trae nada no esta funcional , enotnces validar los otros tres modulos que funciones»
Registrado desde el trabajo real del área de desarrollo (sesión 21a8d370, 2026-07-14).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-14 19:32:39+00', TIMESTAMPTZ '2026-07-15 13:19:04+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea SES-20260714-21a8.
Evidencia: 12 archivos tocados · 0,4 h de sesión real · commits 9c87ec6
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-15 13:19:04+00', v_cedula, false,
           'ALTA', v_orden, 2.00, DATE '2026-07-14', DATE '2026-07-15',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-14 19:32:39+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260714-21a8' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260714-21a8'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260714-21a8'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0012-T14 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Puente de consulta: migración/sincronización Pollo Engorde desde ZooPanamaPollo', '[Bitácora jul-ago 2026] · tarea HIS-2026-0012-T14
Pedido del usuario: «necesito crea un puente de consulta para realziar migracion de lotes granjas y organizarlo aqui y tambine seguimiento diario , seguimeinto reproductora , todo es de modulo de pollo engorde de un swagerr la idea es colocar el año de los lotes y me traiga tdos lo que este y se sincronizan con el modulo de pollo engorde , por que no tiene ventas , no tiene traslados registrados es muy sensillo pero queir que valides pero nunca utilzies update o eliminar en el swagger como regla , investiga cada servicio para poder tener alineado con lo que necesitamos trar a nuestro sistemas :»
Registrado desde el trabajo real del área de desarrollo (sesión 918247c3, 2026-07-15).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-15 14:11:34+00', TIMESTAMPTZ '2026-07-16 17:16:05+00',
           'Qué se hizo (1 commit): feat(engorde): puente de sincronizacion con ZooPanamaPollo (Integracion Panama)
Bugs encontrados en el camino: 0.
Evidencia: 58 archivos tocados · 4,5 h de sesión real · commits d16c1c8
Estimación: 16 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-16 17:16:05+00', v_cedula, false,
           'MEDIA', v_orden, 16.00, DATE '2026-07-15', DATE '2026-07-16',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-15 14:11:34+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0012-T14' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0012-T14'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0012-T14'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0003-T3 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Mejora integral del módulo Vacunación (performance + UI/UX + reportería)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0003-T3
Pedido del usuario: «los modulos que etan en vacinacion organizalos mejor y diseño ui y ux desing de caurdo a los colores de la aplciacion y esta lento en los select o al momento de trar inofmacion los serviciso que tenga el 100 del codigo en la base dedatos en funciones que reflejen el modulo y la funcion que realzian asi realziamos mas velocidad compleeta en la aplciacion pero los modulos de vacnacion mejoralos completamente mas profesionales y mejores en usabilidad y reproteria tambien»
Registrado desde el trabajo real del área de desarrollo (sesión 74e83ed2, 2026-07-16).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-16 05:08:00+00', TIMESTAMPTZ '2026-07-16 06:07:03+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 60 archivos tocados · 1,0 h de sesión real
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-16 06:07:03+00', v_cedula, false,
           'MEDIA', v_orden, 12.00, DATE '2026-07-16', DATE '2026-07-16',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-16 05:08:00+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0003-T3' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0003-T3'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0003-T3'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0014-T5 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Matriz Verenice rev 6-jul-26 · Postura Colombia (validación + corrección)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0014-T5
Pedido del usuario: «estos son de modulos postura , seguimiento diario levante , seguimeinto diario produccion , movimiento de aves , movimiento de huevos , reprotes , sanmarino que tiene las dos opciones de levante y produccion , estos son requeirmeintos para esta linea que es todo los modulos de postura , validalos y valida sobre el modulo para identificar el error y saber que si ya esta solucionado o precente la falla , te paso el las credenciales de para acceder y validar esta faceta completa investiga a profundo c»
Registrado desde el trabajo real del área de desarrollo (sesión 0a8877b7, 2026-07-16).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-16 05:17:57+00', TIMESTAMPTZ '2026-07-17 17:48:13+00',
           'Qué se hizo (11 commits): feature de vacunacion; feat(produccion-back): filter-data con encaset, semana 25, etapa, %retiro real+guia y enforcement; feat(seguimiento-levante-back): guardas de encaset, consumo vs saldo por sexo y bloqueo de lote cerrado; test(produccion): alinear etapa 26-33 y agregar tests de %retiro; feat(db): vista Power BI y migracion EF de funciones/vista de indicadores postura; chore(sql): script idempotente de correccion de datos postura (Fase 0, NO aplicar sin OK); feat(seguimiento-levante-ui): glosario, consumo/retiro por sexo, reporte semanal y avisos; feat(lote-traslado-ui): bloquear encaset futuro y fecha de traslado en hora local
Bugs encontrados en el camino: 2 — cada uno queda como subtarea BUG de la tarea HIS-2026-0014-T5.
Evidencia: 7 archivos tocados · 2,1 h de sesión real · commits 2c1f396, 957330f, b917ad9, ea585fd, 27add00, 2a86978, e0a0fe3, 51a25f7, 4109b01, fd3e7f8
Estimación: 14 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-17 17:48:13+00', v_cedula, false,
           'MEDIA', v_orden, 14.00, DATE '2026-07-16', DATE '2026-07-17',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-16 05:17:57+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0014-T5' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0014-T5'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0014-T5'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0007-T13 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Inventario Gestión: scoping multi-empresa / multi-país consistente + ítems de Panamá', '[Bitácora jul-ago 2026] · tarea HIS-2026-0007-T13
Pedido del usuario: «tengo un error el modulo de inventraio quedo para milti empresa entonces las granjas que me debe trar son las que el usuario tiene en sesion y pertenece y las granjas son las que el usaurio tiene asignadas tmaibne por que actual mente me trae las de pollo engorde qeu este mo dulo estaba para pollo engorde antes , pero ahroa quedo para postura la cuestion de alimento me trae solo las de ecuador deberia ser si es diferente a ecuador trae las del paise que es y las granjas del usaurio que estan en otro tabla difeernete a las de pollo engorde eso se me paso por que en produccion no esta aplciado»
Registrado desde el trabajo real del área de desarrollo (sesión a1eb99ed, 2026-07-17).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-17 17:30:28+00', TIMESTAMPTZ '2026-07-17 20:39:31+00',
           'Qué se hizo (1 commit): Merge pull request #37 from ItalcolColombia/fix/inventario-scoping-multiempresa
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea HIS-2026-0007-T13.
Evidencia: 19 archivos tocados · 1,6 h de sesión real · commits 8a94e61, d7c6b53
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-17 20:39:31+00', v_cedula, false,
           'MEDIA', v_orden, 8.00, DATE '2026-07-17', DATE '2026-07-17',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-17 17:30:28+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0007-T13' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0007-T13'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0007-T13'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0008-T6 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Liquidación Panamá por CORRIDA (tab Pollo Engorde del módulo Indicador)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0008-T6
Pedido del usuario: «necesito organizar la logica en el modulo de liquidacion ya que tenemos no me busca por corrida sino por el lote ve la logica de panama y integrala a lo que se tiene ahroa ya que actual mente esta funcional para ecuador es integrar esta opcon para cuando es panama que es diferrente y valdia lo de trar la data correcta de que se tieen cargada : en lso dos tap que se tiene el tap de indicador general si me trae los datos de panama , pro ahroa esta en el tap pollo engorde que esta amarrado a la logica de ecuador»
Registrado desde el trabajo real del área de desarrollo (sesión 430fae23, 2026-07-20).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-20 13:18:57+00', TIMESTAMPTZ '2026-07-20 17:25:56+00',
           'Qué se hizo (2 commits): feat(liquidacion-panama): busqueda por corrida en el tab Pollo Engorde del indicador; feat(liquidacion-panama): busqueda por corrida en el tab Pollo Engorde del indicador
Bugs encontrados en el camino: 0.
Evidencia: 26 archivos tocados · 1,1 h de sesión real · commits f4179af, ae86bbd
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-20 17:25:56+00', v_cedula, false,
           'MEDIA', v_orden, 10.00, DATE '2026-07-20', DATE '2026-07-20',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-20 13:18:57+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0008-T6' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0008-T6'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0008-T6'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0002-T1 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Módulo ""Implementación"" (cronogramas de entrega por empresa con checklist confirmable)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0002-T1
Pedido del usuario: «me gustaria un modulo de implementacion donde coloco por empresa y asigno a roles , donde creare un cronograma de implemntacion con check como ejemplo , una que sea parametrizaciones , y si se cumple da chekc y coloca la fecha y el usuario al que se le asigno el confirma en su perfil que se cumplio al final del chekc que se tenga asi garantizamos entregas de la aplicacion y cpaciotacioens por usuarios asi gestionamos qeu se entrega y controlar mejro la uditoria de la aplciacion y sea por empresa y usuario pais es para poder entregar check list de implementacioens de la aplicaicon y crear crono»
Registrado desde el trabajo real del área de desarrollo (sesión b02eb1e1, 2026-07-20).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-20 13:26:15+00', TIMESTAMPTZ '2026-07-20 21:33:23+00',
           'Qué se hizo (3 commits): feat(implementacion): modulo de cronogramas de entrega por empresa con checklist confirmable; feat(implementacion): modulo de cronogramas de entrega por empresa con checklist confirmable; Merge branch ''postura-verenice-rev-6jul26''
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea HIS-2026-0002-T1.
Evidencia: 51 archivos tocados · 2,5 h de sesión real · commits f39b627, 0d82106, 765e806, c23d9bc
Estimación: 20 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-20 21:33:23+00', v_cedula, false,
           'MEDIA', v_orden, 20.00, DATE '2026-07-20', DATE '2026-07-20',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-20 13:26:15+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0002-T1' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0002-T1'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0002-T1'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0012-T16 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Reporte Diario Costos (Pollo Engorde) + Lote Base global', '[Bitácora jul-ago 2026] · tarea HIS-2026-0012-T16
Pedido del usuario: «necesito crear este reporte para pollo engorde con los seguimiento diarios que se tiene enotnces debe ser asi este reprote valida y saca toda la informacion de donde la necestamos»
Registrado desde el trabajo real del área de desarrollo (sesión fda9c853, 2026-07-20).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-20 21:29:51+00', TIMESTAMPTZ '2026-07-22 01:03:37+00',
           'Qué se hizo (2 commits): feat(engorde): reporte diario costos por granja + lote base global con permisos; feat(engorde): lote base obligatorio en Panama con tab de gestion y vigencia anual
Bugs encontrados en el camino: 0.
Evidencia: 58 archivos tocados · 2,1 h de sesión real · commits eda83c9, 640d2a5
Estimación: 14 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-22 01:03:37+00', v_cedula, false,
           'MEDIA', v_orden, 14.00, DATE '2026-07-20', DATE '2026-07-22',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-20 21:29:51+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0012-T16' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0012-T16'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0012-T16'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0002-T2 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Módulo Implementación (checklists) v2: rediseño + firmas de participantes', '[Bitácora jul-ago 2026] · tarea HIS-2026-0002-T2
Pedido del usuario: «el modulo de cherlis organizalo el diseno y sus filtros por que nunca cargan se quedan pensando a que la peticion retorno y esta muy fuera del diseno la estaequica del modulo,debe tener algo como al crear un cornograma de chek coloco una descriccion , y una fecha de implemntacion de los cket de entrega y que sierva tambine para capacitaciones , y luego de eso cuando creo el cronograma paso a crear sus iten de valdiacion donde coloco fechas decir el cronograma es implementacion panama , del 1 al 6 de julio en la descriccon coloco, integrar intalgranja en todo panama etc , ycuando gaurdo , ya co»
Registrado desde el trabajo real del área de desarrollo (sesión 2358689a, 97a5e50d, 2026-07-21).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-21 04:11:16+00', TIMESTAMPTZ '2026-07-22 00:54:34+00',
           'Qué se hizo (1 commit): feat(implementacion): checklist v2 con firmas de participantes y tipo de cronograma
Bugs encontrados en el camino: 2 — cada uno queda como subtarea BUG de la tarea HIS-2026-0002-T2.
Evidencia: 60 archivos tocados · 0,9 h de sesión real · commits 28f9336, dba28e9, c4755b9
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-22 00:54:34+00', v_cedula, false,
           'MEDIA', v_orden, 10.00, DATE '2026-07-21', DATE '2026-07-22',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-21 04:11:16+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0002-T2' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0002-T2'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0002-T2'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260721-1a99 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'SQL de diagnóstico: por qué el admin de Panamá solo veía dos granjas', '[Bitácora jul-ago 2026] · tarea SES-20260721-1a99
Pedido del usuario: «dame un sql para sacar las ranjas de panama es que el usuario qeu tengo como admin.panmaa solome trae dos granjas enotnces quiero tirar en base de datos de peoduccion si en la migracion desde el modulo migracion panama paso algo o no se asignaron al usuario admin panama la migracion de infomrcion: Request URL Request Method GET Status Code 200 OK Remote Address 18.119.197.100:443 Referrer Policy strict-origin-when-cross-origin cache-control no-store, no-cache, must-revalidate, max-age=0 content-s»
Registrado desde el trabajo real del área de desarrollo (sesión 1a993293, 2026-07-21).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-21 12:55:00+00', TIMESTAMPTZ '2026-07-21 19:57:01+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 6 archivos tocados · 0,3 h de sesión real
Estimación: 1 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-21 19:57:01+00', v_cedula, false,
           'MEDIA', v_orden, 1.00, DATE '2026-07-21', DATE '2026-07-21',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-21 12:55:00+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260721-1a99' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260721-1a99'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260721-1a99'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0007-T14 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Fix — Consumo de inventario Colombia multi-empresa (error 400 ""no tiene equivalente"")', '[Bitácora jul-ago 2026] · tarea HIS-2026-0007-T14
Pedido del usuario: «tenog un error al momento de realziar un consumo en el modulo de levante que es el liguinte : Request URL Request Method POST Status Code 400 Bad Request Remote Address 52.14.252.89:443 Referrer Policy strict-origin-when-cross-origin 1. 2. 3. 4. 5. 6. ﻿ main-K645UVIE.js:1095 ✅ Sesión guardada. Verificación: 1. Object main-K645UVIE.js:1095 ✅ Menú desencriptado correctamente 1. Object ﻿{"fechaRegistro":"2026-07-30T17:00:00.000Z","loteId":"123","lotePosturaLevanteId":15,"mortalidadHembras":0,"mortalidadMachos":0,"selH":0,»
Registrado desde el trabajo real del área de desarrollo (sesión 5ae70915, 2026-07-21).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-21 20:05:11+00', TIMESTAMPTZ '2026-07-21 20:46:25+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea HIS-2026-0007-T14.
Evidencia: 28 archivos tocados · 0,7 h de sesión real · commits 1c172df
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-21 20:46:25+00', v_cedula, false,
           'ALTA', v_orden, 6.00, DATE '2026-07-21', DATE '2026-07-21',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-21 20:05:11+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0007-T14' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0007-T14'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0007-T14'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0012-T15 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Fix: fechas muestran un día menos — módulo pollo engorde (lotes, reproductoras y seguimientos)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0012-T15
Pedido del usuario: «en el modulo de pollo engorde y al momento de crear un lote pollo engorde , crear los lotes reproductoras y al momento de realziarle seguimiento al lote pollo engorde y seguimiento a reproductora pollo engorde esta tomando una fecha menos de la que esta registrada entonces no me esta mostrando la fecha correcta la que coloco con la que muestra en la tabla o en el seguimiento de los dos modulos validar el formatiador de fecha no me quite un dia habil»
Registrado desde el trabajo real del área de desarrollo (sesión ac6c64f1, 97a5e50d, 2026-07-21).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-21 20:51:39+00', TIMESTAMPTZ '2026-07-22 00:54:34+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 42 archivos tocados · 0,7 h de sesión real
Estimación: 5 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-22 00:54:34+00', v_cedula, false,
           'ALTA', v_orden, 5.00, DATE '2026-07-21', DATE '2026-07-22',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-21 20:51:39+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0012-T15' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0012-T15'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0012-T15'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0004-T3 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Ajuste rate limiting / bloqueo por IP (prod: ""Tu IP ha sido bloqueada temporalmente"")', '[Bitácora jul-ago 2026] · tarea HIS-2026-0004-T3
Pedido del usuario: «en produccion tengo este erro quiero validar si tengo un servico que me cambia el estado y cuanto tiempo es la espera para que se desbloque los usuarios que se loquearon si no parz cambiar el tiempo de espera en produccion a menos tiempo pero que evite ataques : 🚫 Acceso Bloqueado: Tu IP ha sido bloqueada temporalmente. Intenta nuevamente más tarde.»
Registrado desde el trabajo real del área de desarrollo (sesión 3a26f219, 97a5e50d, 2026-07-21).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-21 20:58:28+00', TIMESTAMPTZ '2026-07-22 00:54:34+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 11 archivos tocados · 0,2 h de sesión real
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-22 00:54:34+00', v_cedula, false,
           'MEDIA', v_orden, 2.00, DATE '2026-07-21', DATE '2026-07-22',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-21 20:58:28+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0004-T3' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0004-T3'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0004-T3'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0005-T3 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Administrador de Empresa: visibilidad global de granjas en asignación de usuarios', '[Bitácora jul-ago 2026] · tarea HIS-2026-0005-T3
Pedido del usuario: «1. Módulo Roles: Nueva configuración (Vista exclusiva Super Admin) En el módulo de Roles, se agregará una opción accesible únicamente para el rol Super Admin / Admin General. Configuración del Formulario de Rol: * Nombre del Rol: (Ej. Administrador Panamá) * País: [ Dropdown: Panamá ] * Empresa: [ Dropdown: Intalcol ] * [ ☑ ] Es Administrador de Empresa/País (Checkbox o Switch toggle) Nota técnica: Al activar esta casilla, este rol adquiere un permiso global a nivel de base de datos para heredar todas las entidades activas de la empresa seleccionada. 2. Módulo Usuarios: Comportamiento de Asi»
Registrado desde el trabajo real del área de desarrollo (sesión 0429cf21, 2026-07-22).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-22 01:21:30+00', TIMESTAMPTZ '2026-07-22 02:53:09+00',
           'Qué se hizo (1 commit): feat(roles): flag Administrador de Empresa (solo Super Admin) + visibilidad global de granjas al asignar usuarios
Bugs encontrados en el camino: 0.
Evidencia: 29 archivos tocados · 0,7 h de sesión real · commits aa49466
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-22 02:53:09+00', v_cedula, false,
           'MEDIA', v_orden, 8.00, DATE '2026-07-22', DATE '2026-07-22',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 01:21:30+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0005-T3' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0005-T3'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0005-T3'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0004-T4 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Sesión deslizante por inactividad (auto-logout 5 min + desconexión)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0004-T4
Pedido del usuario: «en el modulo de seguimiento diario reporductora pollo engorde tenemos que agregar un validador de cada registro ya que actual mente si tengo uno o dos o mas lotes reproductoraas en un lote se sincroniza automatica mente en en seguimiento pollo engorde , pero esta ves tenemo que validar que cuando tengamos un cehckt que nos confirme si la informacion esta correcta se sincroniza con lla misma ogica que esta acutla , pero si es esto es para poder tener avilitado la fase de sincronizacion con una validacion extra , y validamso que descpues de confirmar no se peude editar el registro pero como es»
Registrado desde el trabajo real del área de desarrollo (sesión aeb83bdd, 2026-07-22).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-22 01:40:41+00', TIMESTAMPTZ '2026-07-22 11:31:38+00',
           'Qué se hizo (1 commit): dev
Bugs encontrados en el camino: 0.
Evidencia: 43 archivos tocados · 1,1 h de sesión real · commits 4067a23
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-22 11:31:38+00', v_cedula, false,
           'MEDIA', v_orden, 8.00, DATE '2026-07-22', DATE '2026-07-22',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 01:40:41+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0004-T4' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0004-T4'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0004-T4'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0009-T8 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Confirmación por registro en Seguimiento Diario Reproductora (Pollo Engorde)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0009-T8
Pedido del usuario: «en el modulo de seguimiento diario reporductora pollo engorde tenemos que agregar un validador de cada registro ya que actual mente si tengo uno o dos o mas lotes reproductoraas en un lote se sincroniza automatica mente en en seguimiento pollo engorde , pero esta ves tenemo que validar que cuando tengamos un cehckt que nos confirme si la informacion esta correcta se sincroniza con lla misma ogica que esta acutla , pero si es esto es para poder tener avilitado la fase de sincronizacion con una validacion extra , y validamso que descpues de confirmar no se peude editar el registro pero como es»
Registrado desde el trabajo real del área de desarrollo (sesión aeb83bdd, 2026-07-22).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-22 01:40:41+00', TIMESTAMPTZ '2026-07-22 11:31:38+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-22 11:31:38+00', v_cedula, false,
           'MEDIA', v_orden, 8.00, DATE '2026-07-22', DATE '2026-07-22',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 01:40:41+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0009-T8' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0009-T8'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0009-T8'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0005-T7 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Lote base de pollo engorde: creación simple + asignación de granjas + visibilidad por granja', '[Bitácora jul-ago 2026] · tarea HIS-2026-0005-T7
Pedido del usuario: «en el modulo de crear lote baase para pollo engorde vamos a cambiar la logica del ese modulo la idea es que solo nos pedira nombre de lote tomara la fecha de activacion y captura el usuario que lo realizo , luego de eso , l oque va realizar es cuando tengamos el lote creado base , tendresmo una opcion para asignar granjas la misma que tenemos en usuario que adinamos granja , si el usuario tiene como administrador de la empresa le trae todas la granjas , la idea es eso , en este modulo que en la parte qeu aparece el lote trae las granjas para asingar y este filtro es para que este lote sea vi»
Registrado desde el trabajo real del área de desarrollo (sesión 244026a1, 2026-07-22).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-22 15:55:24+00', TIMESTAMPTZ '2026-07-22 16:59:34+00',
           'Qué se hizo (1 commit): feat(engorde): lote base pollo engorde por granja + creacion solo-nombre, sin vigencia por año
Bugs encontrados en el camino: 0.
Evidencia: 32 archivos tocados · 1,1 h de sesión real · commits 39bc689
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-22 16:59:34+00', v_cedula, false,
           'MEDIA', v_orden, 10.00, DATE '2026-07-22', DATE '2026-07-22',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 15:55:24+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0005-T7' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0005-T7'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0005-T7'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0012-T18 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Unidad `qq` (quintal) en el alimento del seguimiento pollo engorde', '[Bitácora jul-ago 2026] · tarea HIS-2026-0012-T18
Pedido del usuario: «necesito que en modulo de seguimeinto pollo engorde en la parte de crear un registro de seguimiento tengamos en la conversion de donde me sasake kg y g agregamso la conversion de qq a kilos ellos va agregar qq en panama dejarla por decto de primera en panama y que realzie la conversion en la parte de abajo muestre lo que se va a guardar en consumo en kg siempre»
Registrado desde el trabajo real del área de desarrollo (sesión c748f70e, 2026-07-22).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-22 16:08:46+00', TIMESTAMPTZ '2026-07-22 17:06:06+00',
           'Qué se hizo (1 commit): feat(engorde): unidad qq (quintal) en alimento del seguimiento pollo engorde
Bugs encontrados en el camino: 0.
Evidencia: 11 archivos tocados · 0,4 h de sesión real · commits 2e68db6
Estimación: 3 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-22 17:06:06+00', v_cedula, false,
           'MEDIA', v_orden, 3.00, DATE '2026-07-22', DATE '2026-07-22',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 16:08:46+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0012-T18' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0012-T18'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0012-T18'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0008-T8 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Cierre del lote reproductora engorde por CONFIRMACIÓN (no por registro)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0008-T8
Pedido del usuario: «necesito el permiso de confirmar registro en pollo engorde que tengo aqui en pruebas en una migracion pro que no esta aplicada»
Registrado desde el trabajo real del área de desarrollo (sesión 42005ea3, 871c1f23, 2026-07-22).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-22 16:46:51+00', TIMESTAMPTZ '2026-07-22 18:30:09+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea HIS-2026-0008-T8.
Evidencia: 10 archivos tocados · 0,8 h de sesión real · commits 0fcda75
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-22 18:30:09+00', v_cedula, false,
           'MEDIA', v_orden, 6.00, DATE '2026-07-22', DATE '2026-07-22',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 16:46:51+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0008-T8' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0008-T8'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0008-T8'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0005-T6 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Gestión de Granjas: cascada al eliminar + refresco entre tabs + scoping por granja asignada', '[Bitácora jul-ago 2026] · tarea HIS-2026-0005-T6
Pedido del usuario: «en el modulo gestion de granjas validame que cuando yo elimine una granja se desabiltia sus nucleos y galpones , de una y actualzia los servicios de cada tap y igual cuando creo algo actualzia los servicios de galpon y nucleo para que tenga al dia todo , ya que ahro si creo una granja y paso al nucleo tengo que cargar la aplciacion o al momento de eliminar no elimina todo y me trae toda la ifnormacion , tmaibne necesito que me traiga los nucleo y galpones que corresoinden a mi usuario asingados a la granja , si tengo una granja me refleja su informacion ya que actual mente en algunso caso me t»
Registrado desde el trabajo real del área de desarrollo (sesión 0973632d, 2026-07-22).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-22 17:21:19+00', TIMESTAMPTZ '2026-07-22 17:49:10+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 31 archivos tocados · 0,5 h de sesión real
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-22 17:49:10+00', v_cedula, false,
           'MEDIA', v_orden, 8.00, DATE '2026-07-22', DATE '2026-07-22',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 17:21:19+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0005-T6' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0005-T6'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0005-T6'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0012-T17 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Numeración de corrida por lote base + galpón (Panamá) en Lote Pollo Engorde', '[Bitácora jul-ago 2026] · tarea HIS-2026-0012-T17
Pedido del usuario: «en el modulo que creamos los lotes pollo engorde actual mente nos muestra los lotes base que tenemso creados ahora necesito que valide si el lote base esta ya en el galpon selecionado se le asinga el siguinte nuemero es decir si es el primero coge el nombre del lote 96 y referencia de primero 1 , y el nombre a mostrar seria el 96 - 1 entonces este es el nombre del lote pero tamibne guardamos el lote base de pollo enrde el id o el nombre ya que seria una casilla donde vemos el lote base que queda asociado y el campo nombre lote se crea con el lote lote base y el numero de la corrida que seri»
Registrado desde el trabajo real del área de desarrollo (sesión 2b4164b7, 2026-07-22).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-22 17:40:11+00', TIMESTAMPTZ '2026-07-22 18:10:09+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea HIS-2026-0012-T17.
Evidencia: 18 archivos tocados · 0,5 h de sesión real · commits 944c9c6
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-22 18:10:09+00', v_cedula, false,
           'MEDIA', v_orden, 8.00, DATE '2026-07-22', DATE '2026-07-22',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 17:40:11+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0012-T17' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0012-T17'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0012-T17'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0014-T6 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Seguimiento Diario Producción: heredar Lote padre al cerrar Levante (Postura)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0014-T6
Pedido del usuario: «tengo este error caundo estemos utilizando el tack vamos a compartir con otra sesion la idea es que no borres el archivo sino agrega las tareas de esta sesion para que continue la solucion este solucion es para postura que son levante y produccion : el modulo de seguimiento diario produccion el lote tiene lote base y no deja guardar un seguimiento por que dice que no tiene lote base asignado ya que el lote base no es obligatorio para guardar el registro , pero para los lote que si lo tiene por que falla ya que esto es en el modulo de levante , produccion , lo que pasa es cuando cierro un lote»
Registrado desde el trabajo real del área de desarrollo (sesión 58564f2d, 2026-07-22).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-22 17:53:53+00', TIMESTAMPTZ '2026-07-22 18:29:56+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea HIS-2026-0014-T6.
Evidencia: 15 archivos tocados · 0,6 h de sesión real · commits 967e490
Estimación: 5 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-22 18:29:56+00', v_cedula, false,
           'MEDIA', v_orden, 5.00, DATE '2026-07-22', DATE '2026-07-22',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 17:53:53+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0014-T6' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0014-T6'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0014-T6'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0008-T7 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — ""Reabrir lote"" reproductora engorde no persiste (confirma sin aplicar)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0008-T7
Pedido del usuario: «en el modulo de seguimiento reproductora pollo engorde tenemso una opcion que se llama reabrir lote , pero no esta funcionando la idea es que pueda abrir el seguimiento ya que no deja cuando dejo una nota aparece que esta confirmado pero cuando paso a eliminar unregistro dice que no se peude hasta abrir el seguimiento quiere decir que confirma sin aplciarlo»
Registrado desde el trabajo real del área de desarrollo (sesión 871c1f23, 2026-07-22).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-22 18:13:48+00', TIMESTAMPTZ '2026-07-22 18:30:09+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea HIS-2026-0008-T7.
Evidencia: 10 archivos tocados · 0,3 h de sesión real · commits da3bf77
Estimación: 3 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-22 18:30:09+00', v_cedula, false,
           'ALTA', v_orden, 3.00, DATE '2026-07-22', DATE '2026-07-22',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 18:13:48+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0008-T7' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0008-T7'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0008-T7'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0009-T4 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan: Mixto + Consumo de Agua + Reapertura con Novedad — Cruce Reproductora → Pollo Engorde', '[Bitácora jul-ago 2026] · tarea HIS-2026-0009-T4
Pedido del usuario: «en el modulo de seguimiento reproductora pollo engorde tenemso una opcion que se llama reabrir lote , pero no esta funcionando la idea es que pueda abrir el seguimiento ya que no deja cuando dejo una nota aparece que esta confirmado pero cuando paso a eliminar unregistro dice que no se peude hasta abrir el seguimiento quiere decir que confirma sin aplciarlo»
Registrado desde el trabajo real del área de desarrollo (sesión 871c1f23, 2026-07-22).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-22 18:13:48+00', TIMESTAMPTZ '2026-07-22 18:30:09+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 14 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-22 18:30:09+00', v_cedula, false,
           'MEDIA', v_orden, 14.00, DATE '2026-07-22', DATE '2026-07-22',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 18:13:48+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0009-T4' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0009-T4'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0009-T4'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0009-T7 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Ajustes de creación/edición en Lote Reproductora Aves de Engorde', '[Bitácora jul-ago 2026] · tarea HIS-2026-0009-T7
Pedido del usuario: «en el modulo de lote reproductora pollo engorde donde creamos los lotes al momento de crearlos vamos a quitar el campo codigo reproductora , en nombre reproductora que sea obligatorio pero no coloque el nomrbe del lote pricipal sino que este null vacio para que el usaurio lo asigne : en edad captura la edad qeu deve tener el lote hasta finalziar el lote es que ahroa muestra la edad real con la fecha del sistema enocnes si la edad es de 1 a 7 valdia con el dia de hoy y puede darme que es 14 en el campo edad y valdiar si elimino un registro que tiene datos ya cargadso debe obligar a que elimin»
Registrado desde el trabajo real del área de desarrollo (sesión 22a48a6c, 2026-07-22).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-22 18:21:02+00', TIMESTAMPTZ '2026-07-22 18:58:21+00',
           'Qué se hizo (1 commit): feat(engorde): ajustes lote reproductora — creación sin código, edad congela al cerrar, borrado seguro y permisos
Bugs encontrados en el camino: 0.
Evidencia: 18 archivos tocados · 0,6 h de sesión real · commits 97665c4
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-22 18:58:21+00', v_cedula, false,
           'MEDIA', v_orden, 6.00, DATE '2026-07-22', DATE '2026-07-22',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 18:21:02+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0009-T7' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0009-T7'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0009-T7'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0005-T5 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — CRUD de ubicación seguro: mover/editar/eliminar Núcleo · Galpón · Lote (transversal multipaís)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0005-T5
Pedido del usuario: «tengo un error si voy a editar un nucleo que le cambio el nombre o la granja que tiene asignada , los galpones no se actualizan y si solo quiero cambiar el galpon de nucleo se crea otro galpon pero solo es una edicion interna del galpon no es eliminacion ni cambiando a otra granja , la idea es que es un crud que puedo cambiar un lote de ubicacion en su granja o la granja en otro galpon o nucleo a otra granja , y igual es lo de eliminar que tenga el lfujo completo ya que en produccion paso edite un galpon de nucleo y se creo otro registro enotnces quedo el erro arriba hasta que migre la inform»
Registrado desde el trabajo real del área de desarrollo (sesión 499407d8, 2026-07-22).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-22 18:27:26+00', TIMESTAMPTZ '2026-07-22 19:39:14+00',
           'Qué se hizo (1 commit): feat(ubicacion): mover/eliminar seguro de nucleo/galpon/lote (transversal, sin duplicar ni huerfanos)
Bugs encontrados en el camino: 0.
Evidencia: 37 archivos tocados · 1,2 h de sesión real · commits 100c343
Estimación: 14 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-22 19:39:14+00', v_cedula, false,
           'MEDIA', v_orden, 14.00, DATE '2026-07-22', DATE '2026-07-22',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 18:27:26+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0005-T5' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0005-T5'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0005-T5'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0009-T9 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Seguimiento Diario Reproductora Pollo Engorde: fechas y edición', '[Bitácora jul-ago 2026] · tarea HIS-2026-0009-T9
Pedido del usuario: «estoy en el modulo Seguimiento Diario Reproductora Pollo Engorde y tengo un error en fechas en produccion realizaron esto y primero es que el primer registro esta sumando un dia mas al mostrar entonces no se sincronizo los primero consumo y no cuadra validar que la fecha de creacion del regitro sea lo mismo que meustra en la tabla ya que si no lo suma le quita un dia entonces tenemos ese error de sincronizacion , tamibne que no me deje colocar un dia menos de la fecha de encacetamiento del lote repeoductora , tambien valdia que si abro el seguimeinto pueda editar la fecha y algunos campos co»
Registrado desde el trabajo real del área de desarrollo (sesión 2fcb6305, 2026-07-22).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-22 19:40:55+00', TIMESTAMPTZ '2026-07-22 20:09:04+00',
           'Qué se hizo (1 commit): modal seguimiento reproductora
Bugs encontrados en el camino: 0.
Evidencia: 16 archivos tocados · 0,5 h de sesión real · commits 111bc9d
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-22 20:09:04+00', v_cedula, false,
           'MEDIA', v_orden, 6.00, DATE '2026-07-22', DATE '2026-07-22',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 19:40:55+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0009-T9' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0009-T9'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0009-T9'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0005-T4 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Alinear nombres de Lote Pollo Engorde (Panamá) al lote base asignado', '[Bitácora jul-ago 2026] · tarea HIS-2026-0005-T4
Pedido del usuario: «ahora necesito que tengamos una migracion de los lotes de pollo engorde de panama con el cambio de que ahroa el nombre del lote se asigna del lote base selecionado ya tengo esos lotes en produccion creados anterior mente no cumple con el prefijo del numero de identificacion del lote para los nombre enotnces ahroa subo la solucion pero me toca crear una migracion que corrija los nombres si no lo tiene de acurdo al lote base que tiene asignados , es para alinear , en el track tengo otra sesion al finalziar esta no bbore el track y agregas al final lo que necesitas aqui en la solucion y realiza»
Registrado desde el trabajo real del área de desarrollo (sesión 25e1c3a2, 2026-07-22).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-22 19:43:19+00', TIMESTAMPTZ '2026-07-22 20:09:28+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea HIS-2026-0005-T4.
Evidencia: 12 archivos tocados · 0,4 h de sesión real · commits 4893032
Estimación: 5 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-22 20:09:28+00', v_cedula, false,
           'ALTA', v_orden, 5.00, DATE '2026-07-22', DATE '2026-07-22',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 19:43:19+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0005-T4' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0005-T4'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0005-T4'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0011-T3 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Guía genética Panamá: Ross 308 AP 2022 (mixto) + repunte de lotes', '[Bitácora jul-ago 2026] · tarea HIS-2026-0011-T3
Pedido del usuario: «valida este archivo y asigna estas tablas geneticas por raza a la empresa panama creame la migracion de asignacion de la tbal genetica ya que con la mixta esta trabjando panama del ño 2022 entonces por ahroa no me interesa cargar macho ni hembras solo mixtas por raza valida el modulo que tenemos de tabla genetica que utiliza ecuador para utilizarlo y realizar la migracion correcta en el pais y eliminamos la que esta cargada actual mente en panama por que no es la correcta»
Registrado desde el trabajo real del área de desarrollo (sesión 87437f4d, 2026-07-22).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-22 20:22:38+00', TIMESTAMPTZ '2026-07-22 21:01:17+00',
           'Qué se hizo (1 commit): feat(engorde): guia genetica Panama Ross 308 AP 2022 mixto + repunte lotes
Bugs encontrados en el camino: 0.
Evidencia: 12 archivos tocados · 0,6 h de sesión real · commits 85cc582
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-22 21:01:17+00', v_cedula, false,
           'MEDIA', v_orden, 8.00, DATE '2026-07-22', DATE '2026-07-22',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 20:22:38+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0011-T3' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0011-T3'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0011-T3'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0009-T10 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — UX cascada numerada en «Lote Reproductora Aves de Engorde»', '[Bitácora jul-ago 2026] · tarea HIS-2026-0009-T10
Pedido del usuario: «el diseno y estetica que tengo en el modulo Seguimiento Diario Reproductora Pollo Engorde quiero tenerlo en el momento de crealo ya que me meustra en numero tambine la forma de secuencia que se tiene que hacer»
Registrado desde el trabajo real del área de desarrollo (sesión 32596426, 2026-07-22).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-22 20:37:48+00', TIMESTAMPTZ '2026-07-23 13:07:13+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 20 archivos tocados · 1,0 h de sesión real
Estimación: 5 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-23 13:07:13+00', v_cedula, false,
           'MEDIA', v_orden, 5.00, DATE '2026-07-22', DATE '2026-07-23',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 20:37:48+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0009-T10' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0009-T10'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0009-T10'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0012-T20 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — UX cascada numerada + info colapsable + scroll único en «Seguimiento diario pollo de engorde»', '[Bitácora jul-ago 2026] · tarea HIS-2026-0012-T20
Pedido del usuario: «el diseno y estetica que tengo en el modulo Seguimiento Diario Reproductora Pollo Engorde quiero tenerlo en el momento de crealo ya que me meustra en numero tambine la forma de secuencia que se tiene que hacer»
Registrado desde el trabajo real del área de desarrollo (sesión 32596426, 2026-07-22).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-22 20:37:48+00', TIMESTAMPTZ '2026-07-23 13:07:13+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 5 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-23 13:07:13+00', v_cedula, false,
           'MEDIA', v_orden, 5.00, DATE '2026-07-22', DATE '2026-07-23',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 20:37:48+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0012-T20' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0012-T20'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0012-T20'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0012-T19 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Código ERP de engorde a nivel GRANJA con avance automático al cerrar ciclo — Panamá', '[Bitácora jul-ago 2026] · tarea HIS-2026-0012-T19
Pedido del usuario: «ahroa necesito que el codigo erp que se solicita en el modulo de pollo engorde al moemnto de crear el lote ese codigo erp va definido al momento de crear la granja este cambio es solo para panama , ya que cuando cree el codigo erp todos los lotes que creee en la granja capturan el codigo erp que debe estar en la granja , la idea es que cuando se cierra o liquida un lote completo en una granja es decir si el lote base 17 que se cro en la granja maria que tiene galpon 1 tiene el 17-1 y 17-2 y en el galpon 3 tiene el 17-1 la idea es caundo se cierra todo los lotes en esa granja de ese lote pa»
Registrado desde el trabajo real del área de desarrollo (sesión 46047129, 2026-07-22).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-22 20:55:02+00', TIMESTAMPTZ '2026-07-23 15:30:30+00',
           'Qué se hizo (2 commits): ux(engorde): cascada numerada, info colapsable y scroll unico en seguimiento y reproductora; feat(engorde): codigo ERP por granja Panama con avance automatico al cerrar el ciclo
Bugs encontrados en el camino: 0.
Evidencia: 21 archivos tocados · 0,9 h de sesión real · commits 63a46a9, a4aa012
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-23 15:30:30+00', v_cedula, false,
           'MEDIA', v_orden, 10.00, DATE '2026-07-22', DATE '2026-07-23',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 20:55:02+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0012-T19' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0012-T19'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0012-T19'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0006-T10 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Migraciones Masivas: línea Seguimiento Reproductora Engorde + alineación Seguimiento Pollo Engorde', '[Bitácora jul-ago 2026] · tarea HIS-2026-0006-T10
Pedido del usuario: «tenemos un modulo para hacer carga masiva de seguimiento pollo engorde reproductora y el seguimeinto de lote pollo engorde , que cada uno tiene la logica que tiene el front entonces necesito que tenga todo actualziado por que le meti otras validaciones , como en las reprroductoras deben tener una confirmacion si las estoy cargando en carga masiva eso va en acetacion de una entonces valida esos modulo con la migraciones masivas y me das tamibne la plantilla para cada uno de ellos cuando la escoja para cargar»
Registrado desde el trabajo real del área de desarrollo (sesión 92cbee1c, 2026-07-23).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-23 15:31:50+00', TIMESTAMPTZ '2026-07-23 19:01:08+00',
           'Qué se hizo (3 commits): feat(migraciones): carga masiva seguimiento reproductora engorde con confirmacion automatica; feat(migraciones): seguimiento engorde por nombres + alimentos del inventario en carga masiva; ux(migraciones): plantilla reproductora sin columnas de ubicacion (el lote sale del filtro)
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea HIS-2026-0006-T10.
Evidencia: 41 archivos tocados · 1,6 h de sesión real · commits 93f5199, d95edd5, b73d727, d509c93
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-23 19:01:08+00', v_cedula, false,
           'MEDIA', v_orden, 12.00, DATE '2026-07-23', DATE '2026-07-23',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-23 15:31:50+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0006-T10' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0006-T10'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0006-T10'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0009-T11 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Seguimiento reproductora engorde — el día del encasetamiento cuenta como DÍA 1', '[Bitácora jul-ago 2026] · tarea HIS-2026-0009-T11
Pedido del usuario: «TENGO UN ERROR AL MOEMNTO DE CARGAR UCA CARGA MASIVA DE REPRODUCTORA DE POLLO ENGORDE ENTONCES AHROA TTRATE DE CARGAR CON UNA FECHA 16/07/2026 Y TRATO DE APLICAR AL MIMOS DIA QUE TIENE ENCAQCETAMIENTO LA REPRODUCTORA ME SALE QUE NO SE UEDE LA MISMA FECHA DE ENCETAMIENTO SI LA IDEA ES QUE SEA LA MISAM FECHA , LO QUE NO SE PUEDE ES QUE SEA EL 15»
Registrado desde el trabajo real del área de desarrollo (sesión fe5752b1, 2026-07-24).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-24 17:36:34+00', TIMESTAMPTZ '2026-07-25 05:34:56+00',
           'Qué se hizo (1 commit): fixx del cambios
Bugs encontrados en el camino: 0.
Evidencia: 21 archivos tocados · 2,0 h de sesión real · commits dd2c923
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-25 05:34:56+00', v_cedula, false,
           'MEDIA', v_orden, 6.00, DATE '2026-07-24', DATE '2026-07-25',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-24 17:36:34+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0009-T11' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0009-T11'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0009-T11'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0012-T21 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Limpieza seguimientos diarios Panamá (reproductora + pollo engorde) para re-carga masiva', '[Bitácora jul-ago 2026] · tarea HIS-2026-0012-T21
Pedido del usuario: «ahroa necesito que os seguimientos diario reprodcutora y seguimiento diario pollo engorde de todos los lotes que son de panama limpiarlos para dejarlo ya en carga masiva que se implemento para evitar erroes que se tenga en la digitacion pero es limpiar los registros de seguimeinto diario entonce descargue la base de datos de produccion al local para que lo realizemos y realizemos la limpieza de los seguimientos si cumple pasamos a crear una migraicion para desplegar a produccion»
Registrado desde el trabajo real del área de desarrollo (sesión bcf9f0db, 2026-07-25).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-25 16:46:56+00', TIMESTAMPTZ '2026-07-25 18:47:42+00',
           'Qué se hizo (1 commit): feat(panama): limpieza total seguimientos diarios e inventario alimento para re-carga masiva
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea HIS-2026-0012-T21.
Evidencia: 17 archivos tocados · 0,8 h de sesión real · commits c7b7ba7, 0cb8eec
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-25 18:47:42+00', v_cedula, false,
           'MEDIA', v_orden, 6.00, DATE '2026-07-25', DATE '2026-07-25',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-25 16:46:56+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0012-T21' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0012-T21'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0012-T21'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0002-T3 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Implementación empresa SANTA REYES (Colombia, postura comercial)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0002-T3
Pedido del usuario: «este es un desarrolo nuevo para una empresa nueva que entra en colombia , que es Santa Reyes , hay que creale toda la secuencia para crear empresas , esta empresa no exites actual hay datos que tiene que ser ficticios de los los campos que no se tengan , tendremos roles admin , implementador , tendrna los mismos permisos para la empresa , los modulos que utilizaran son todo los de levante y»
Registrado desde el trabajo real del área de desarrollo (sesión 2672b21b, 2026-07-25).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-25 17:03:51+00', TIMESTAMPTZ '2026-07-25 22:16:08+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 9 archivos tocados · 4,0 h de sesión real
Estimación: 32 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-25 22:16:08+00', v_cedula, false,
           'MEDIA', v_orden, 32.00, DATE '2026-07-25', DATE '2026-07-25',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-25 17:03:51+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0002-T3' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0002-T3'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0002-T3'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0017-T4 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Diseño unificado de filtros «Selección de contexto» en TODOS los módulos', '[Bitácora jul-ago 2026] · tarea HIS-2026-0017-T4
Pedido del usuario: «quiero que en todo los modulos que utilizen filtado de infrmacion aplicar este mismo dise;o para que quede definidos en todo y el diseno completo que esta aqui en cada arte que va un filtro o un select tenga este diseno lanzas de acuerdo agentes cin opus , sonnet o fable donde corresponde por esfuerzo lista simepre tosos los modulos luego aplcialo en el plan para que realizes en secuencias hasta terminar y siemroe hay otra sesiones realizando trabajos en el track entonces debe convivir esta sesion y las otras»
Registrado desde el trabajo real del área de desarrollo (sesión 5baf8ec3, 2026-07-25).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-25 20:56:09+00', TIMESTAMPTZ '2026-07-25 23:36:05+00',
           'Qué se hizo (2 commits): feat(santa-reyes): implementacion completa empresa Santa Reyes (fases 1-5) + ux filtros de contexto unificados; feat(demo): activar features Santa Reyes en la empresa Demo para evaluacion del cliente
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea HIS-2026-0017-T4.
Evidencia: 14 archivos tocados · 2,7 h de sesión real · commits 7347cf8, 49e3800, 4691c49
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-25 23:36:05+00', v_cedula, false,
           'MEDIA', v_orden, 12.00, DATE '2026-07-25', DATE '2026-07-25',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-25 20:56:09+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0017-T4' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0017-T4'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0017-T4'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260725-ee29 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Super grafo del proyecto: mejoras de contexto y reducción de tokens', '[Bitácora jul-ago 2026] · tarea SES-20260725-ee29
Pedido del usuario: «como va el cereblo super grafo que esta concetado con claude , se tiene que mejora algo para hacer mas intelignrete , exerto y que este reduccioendo token y apreda»
Registrado desde el trabajo real del área de desarrollo (sesión ee29da40, 2026-07-25).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-25 20:57:13+00', TIMESTAMPTZ '2026-07-26 00:49:38+00',
           'Qué se hizo (1 commit): merge: fix fn_rekey_nucleo copia codigo/descripcion bodega al mover nucleo (migracion 20260725210000)
Bugs encontrados en el camino: 0.
Evidencia: 1 archivos tocados · 1,5 h de sesión real · commits 7bdf712
Estimación: 3 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-26 00:49:38+00', v_cedula, false,
           'MEDIA', v_orden, 3.00, DATE '2026-07-25', DATE '2026-07-26',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-25 20:57:13+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260725-ee29' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260725-ee29'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260725-ee29'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0005-T8 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Corrección migración Santa Reyes: lotes del Excel → LOTE BASE (no lotes seguimiento)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0005-T8
Pedido del usuario: «en la migracion para eanta reyes de los lotes me creo los lotes seguimiento pero los lotes que estan hya seria lo lotes base ya que esos no son con las aves de encacetamiento entonces estari mal la migracion , entonces corrige esto y valida que en lote base que nos hace falta en campos corrijamos y apliquemos bien la migracion y impia lo que se creo con la corrida de la migracion , santa reyes y en demo >»
Registrado desde el trabajo real del área de desarrollo (sesión 355c5ce7, 2026-07-26).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-26 02:51:38+00', TIMESTAMPTZ '2026-07-26 04:09:13+00',
           'Qué se hizo (1 commit): Merge remote-tracking branch ''origin/main''
Bugs encontrados en el camino: 0.
Evidencia: 15 archivos tocados · 1,3 h de sesión real · commits 8c1ae34
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-26 04:09:13+00', v_cedula, false,
           'ALTA', v_orden, 6.00, DATE '2026-07-26', DATE '2026-07-26',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-26 02:51:38+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0005-T8' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0005-T8'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0005-T8'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0014-T8 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Demo vuelve a la clasificación de huevos CLÁSICA (Sanmarino) en seguimiento diario producción', '[Bitácora jul-ago 2026] · tarea HIS-2026-0014-T8
Pedido del usuario: «en la migracion para eanta reyes de los lotes me creo los lotes seguimiento pero los lotes que estan hya seria lo lotes base ya que esos no son con las aves de encacetamiento entonces estari mal la migracion , entonces corrige esto y valida que en lote base que nos hace falta en campos corrijamos y apliquemos bien la migracion y impia lo que se creo con la corrida de la migracion , santa reyes y en demo >»
Registrado desde el trabajo real del área de desarrollo (sesión 355c5ce7, 2026-07-26).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-26 02:51:38+00', TIMESTAMPTZ '2026-07-26 04:09:13+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 4 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-26 04:09:13+00', v_cedula, false,
           'MEDIA', v_orden, 4.00, DATE '2026-07-26', DATE '2026-07-26',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-26 02:51:38+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0014-T8' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0014-T8'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0014-T8'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0014-T7 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Fix: Seguimiento diario de producción falla con ""El lote postura producción no tiene LoteId asociado"" (400)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0014-T7
Pedido del usuario: «tengo un error en produccion al momento de realziar un seguimeinto diario en produccion pase lo que esta en produccion a local y tengo un error de la fase de produccion , que no tengo un dato que necesita para crear un seguimiento diario , ya que los lotes creados no tiene lotebase , asingado jajajajajaj la cosa es que puede haber lotes que no tenga lote base entonces puede ser que no tenga , en esta parte si tien lote base creado entonces deberia cogerlo corrigajos para que en produccion lo pase > esto paso en la empresa demo no quiero que pase en empresas qeu despleigue postura entonces cor»
Registrado desde el trabajo real del área de desarrollo (sesión e7b77f42, 2026-07-26).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-26 05:08:42+00', TIMESTAMPTZ '2026-07-26 06:15:08+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 3 — cada uno queda como subtarea BUG de la tarea HIS-2026-0014-T7.
Evidencia: 13 archivos tocados · 1,1 h de sesión real · commits c5b74a4, 645535b, f783bf5
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-26 06:15:08+00', v_cedula, false,
           'ALTA', v_orden, 6.00, DATE '2026-07-26', DATE '2026-07-26',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-26 05:08:42+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0014-T7' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0014-T7'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0014-T7'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0004-T5 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Alcance granular por usuario-granja (núcleo / galpón / lote o global)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0004-T5
Pedido del usuario: «necesito agregar una funcion nueva al momento de asignar una granja a un usuario dentro del mismo modulo podre selecionar tamibne a que nucleo , galpon y hasta el lote tiene permiso ese usuario o dejarlo global por granja se puede tamibne la idea es que se pueda aplicar ese nivel de filtro , enotnces con ese cambio en todo los filter que se tiene se debe aplicar esta condicion ya que no podria traer toda la info de la grnja ahroa si tiene un lote o galpon solo le trae la informacion de ese lugar realiza la validacion completa levanta agentes y validad si salen con sonnet o opus o fable 5 en ca»
Registrado desde el trabajo real del área de desarrollo (sesión 2d7dbcc4, 2026-07-26).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-26 06:34:45+00', TIMESTAMPTZ '2026-07-26 14:34:44+00',
           'Qué se hizo (1 commit): feat(seguridad): alcance granular usuario-granja (nucleo/galpon/lote o global) aplicado a filtros y datos
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea HIS-2026-0004-T5.
Evidencia: 73 archivos tocados · 1,9 h de sesión real · commits d492eed, 9534528
Estimación: 20 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-26 14:34:44+00', v_cedula, false,
           'MEDIA', v_orden, 20.00, DATE '2026-07-26', DATE '2026-07-26',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-26 06:34:45+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0004-T5' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0004-T5'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0004-T5'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0014-T9 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Reporte Técnico Semanal Postura (Sanmarino): Levante + Producción vs Guía Genética', '[Bitácora jul-ago 2026] · tarea HIS-2026-0014-T9
Pedido del usuario: «en sanmarino tenemos estos dos reprotes semanales de sanmarino solo cuando se cree se aplicaran a la empresa sanmarino en reprote seran dos levante y postura , entonces puede ser uno solo modulo que tenga las dos opcioens para generar y comparas con la guia genetica cargada para sanmarino > no soros tenemso todo lo que son seguimientos diarios levante y produccion y tenemos lotes bases y tenemos todo lo que son consumos mortalidades , tmabne los huevos tenemso la carga basia»
Registrado desde el trabajo real del área de desarrollo (sesión 4cb1bac3, 2026-07-26).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-26 14:57:32+00', TIMESTAMPTZ '2026-07-26 20:06:29+00',
           'Qué se hizo (3 commits): feat(reportes): modulo Reporte Tecnico Semanal postura (Levante + Produccion vs guia genetica); feat(reportes): bloque POLLITOS del Reporte Tecnico Semanal con HI Cargado real; docs(tracker): cierre fase 2 del Reporte Tecnico Semanal
Bugs encontrados en el camino: 0.
Evidencia: 43 archivos tocados · 2,4 h de sesión real · commits 3dd1f4a, 0b3b79f, 6abd735
Estimación: 20 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-26 20:06:29+00', v_cedula, false,
           'MEDIA', v_orden, 20.00, DATE '2026-07-26', DATE '2026-07-26',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-26 14:57:32+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0014-T9' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0014-T9'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0014-T9'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0013-T5 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Tab «Huevos» en Seguimiento Diario Levante (semana 14+) y arrastre automático a Producción al liquidar', '[Bitácora jul-ago 2026] · tarea HIS-2026-0013-T5
Pedido del usuario: «realiza un cambio en el seguimiento diario levante que apatir de la semna 14 debe tener un campo que se llama huevo que es lo mismo que se realiza en seguimiento diario produccion que se clasifica los heuvos que tengan por dia en esa fase , la idea final es que cuando se realize la liquidacion esos heuvos pasan automatica mente para la primerea semana de produccion , y eso es cuando se liquide levante , aparece el tota lde huevos y los tipos de heuvos qeu se octuvieron en levante y cuando se levanta automatica mente produccion se crea el primer registro de huevos sumando todos entonces si es e»
Registrado desde el trabajo real del área de desarrollo (sesión 57b94fef, 2026-07-26).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-26 22:24:46+00', TIMESTAMPTZ '2026-07-27 01:38:13+00',
           'Qué se hizo (5 commits): feat(levante): huevos desde semana 14 con arrastre al primer registro de produccion; docs(tracker): cierre de huevos en levante semana 14 + arrastre a produccion; docs(levante): contexto de traspaso y fase 7 de alineacion de huevos en levante; feat(levante): columnas de huevos en la tabla diaria y su Excel + fix del trigger del espejo; docs(levante): diseno resuelto de P2 (carga masiva con huevos) en el contexto de traspaso
Bugs encontrados en el camino: 0.
Evidencia: 16 archivos tocados · 2,2 h de sesión real · commits 34e47aa, 4b7282b, 2bf84f6, 1d19c24, 8ab19ec
Estimación: 14 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-27 01:38:13+00', v_cedula, false,
           'MEDIA', v_orden, 14.00, DATE '2026-07-26', DATE '2026-07-27',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-26 22:24:46+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0013-T5' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0013-T5'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0013-T5'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0013-T6 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           '🔄 CONTEXTO DE TRASPASO — Huevos en Seguimiento Levante (semana 14+) y arrastre a Producción', '[Bitácora jul-ago 2026] · tarea HIS-2026-0013-T6
Pedido del usuario: «realiza un cambio en el seguimiento diario levante que apatir de la semna 14 debe tener un campo que se llama huevo que es lo mismo que se realiza en seguimiento diario produccion que se clasifica los heuvos que tengan por dia en esa fase , la idea final es que cuando se realize la liquidacion esos heuvos pasan automatica mente para la primerea semana de produccion , y eso es cuando se liquide levante , aparece el tota lde huevos y los tipos de heuvos qeu se octuvieron en levante y cuando se levanta automatica mente produccion se crea el primer registro de huevos sumando todos entonces si es e»
Registrado desde el trabajo real del área de desarrollo (sesión 57b94fef, 2026-07-26).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-26 22:24:46+00', TIMESTAMPTZ '2026-07-27 01:38:13+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-27 01:38:13+00', v_cedula, false,
           'MEDIA', v_orden, 2.00, DATE '2026-07-26', DATE '2026-07-27',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-26 22:24:46+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0013-T6' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0013-T6'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0013-T6'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260726-64d2 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'El modal de alcance de granja quedaba colgado en «Cargando…» (Angular 22 = OnPush por defecto)', '[Bitácora jul-ago 2026] · tarea SES-20260726-64d2
Pedido del usuario: «tengo un error que visualizo es que cuando entro a usuario y voy a asignarle dentro de la granja que tiene un alcance a que solo vea galpones un usuario veo que lso servicios retornan todo lo que necesita , pero el modal queda cargado y nunca mustra nada > este es un error que ha venido pasando mucho en el front cuando se utiliza el desarrollo de algo nuevo tenemos que colocar la forma del arreglo en el cerebro y claude para que siemrpe lo tenga precente al desarrollar un modelo o modal nuevo»
Registrado desde el trabajo real del área de desarrollo (sesión 64d22f2d, 2026-07-26).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-26 22:28:22+00', TIMESTAMPTZ '2026-07-26 23:05:33+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea SES-20260726-64d2.
Evidencia: 7 archivos tocados · 0,6 h de sesión real · commits 14a8bfa
Estimación: 3 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-26 23:05:33+00', v_cedula, false,
           'ALTA', v_orden, 3.00, DATE '2026-07-26', DATE '2026-07-26',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-26 22:28:22+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260726-64d2' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260726-64d2'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260726-64d2'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0006-T11 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Venta Pollo Engorde: peso diferido en Panamá + carga masiva completa', '[Bitácora jul-ago 2026] · tarea HIS-2026-0006-T11
Pedido del usuario: «para pollos engorde necesito en el modulo carga masiva necesito realizar carga masiva de las ventas quiere decir todos los campos que utilizo en ventas necesito colocarlo en la apliacion del peso , tamibne necesito que en panama cuando esten realizando regsitros de venta no debe pedir obligatorio lo que es el peso tara y peso bruto ya que eso datos lo tiene al dia siguinte entonces hay colocan el peso al momento de realzia la confirmacion de la venta se abre el modal de registro de peso para que coloquen los datos de peso que de la venta asi obligamos que al momento de la venta no sea obligat»
Registrado desde el trabajo real del área de desarrollo (sesión 3cc1a34a, 2026-07-27).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-27 00:01:11+00', TIMESTAMPTZ '2026-07-27 01:19:03+00',
           'Qué se hizo (1 commit): feat(engorde): peso bascula diferido en ventas (Panama) + carga masiva de ventas multi-lote
Bugs encontrados en el camino: 0.
Evidencia: 41 archivos tocados · 1,3 h de sesión real · commits 6dd4d53
Estimación: 14 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-27 01:19:03+00', v_cedula, false,
           'MEDIA', v_orden, 14.00, DATE '2026-07-27', DATE '2026-07-27',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-27 00:01:11+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0006-T11' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0006-T11'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0006-T11'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0007-T16 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Recepción de tránsito con distribución en varios galpones (Gestión de Inventario)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0007-T16
Pedido del usuario: «en el modulo de gestion de inventario , en el tap de trancito necesito que cuando se aceptar el traslado de alimento , y se acepta en un solo galpon para cuando es alimento ya si es otro item ya es sobre la granja entonces no aplciaria esta logica , la idea es que si llega 1000 kg de alimento entonces necesito dentro de la granja pueda distribuir sobre los galpones que exitan en la granja puedo distribuir lo que llega entre los galpones , ya que actual mente solo se recibe sobre uno , entonces yo puedo resibir pero distribuir sobre varios galpones»
Registrado desde el trabajo real del área de desarrollo (sesión a5b3405f, 2026-07-27).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-27 00:05:29+00', TIMESTAMPTZ '2026-07-27 00:36:10+00',
           'Qué se hizo (1 commit): feat(inventario): recepcion de transito distribuida entre varios galpones
Bugs encontrados en el camino: 0.
Evidencia: 16 archivos tocados · 0,5 h de sesión real · commits b124bf6
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-27 00:36:10+00', v_cedula, false,
           'MEDIA', v_orden, 8.00, DATE '2026-07-27', DATE '2026-07-27',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-27 00:05:29+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0007-T16' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0007-T16'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0007-T16'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0008-T9 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Reapertura validada de Levante + Cierre/Reapertura de Lote de Producción', '[Bitácora jul-ago 2026] · tarea HIS-2026-0008-T9
Pedido del usuario: «neceisto hacer un cambio cuando realizo cierre de seguimiento levante al pasarlo a produccion entonces neceito valdiar al momento de abrir un seguimeinto , primero debe validar que no tenga seguimiento diario en produccion si lo tiene que exigir eliminar el lote seguimiento produccion que se tiene para volver a reabrir el lote produccion y deja bien especificado lo que se realiza , si no tiene seguimeint olo dejara abirir , pero si ya se creo el lote produccion entonces lo elimina y espera al moemnto de cerrar el lote levante otra ves para crearlo actualizado , > Seguimiento Diario de Levante»
Registrado desde el trabajo real del área de desarrollo (sesión 9e83e65a, 2026-07-27).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-27 02:07:32+00', TIMESTAMPTZ '2026-07-27 03:14:11+00',
           'Qué se hizo (1 commit): feat(postura): reapertura de levante validada + cierre/reapertura de lote de produccion
Bugs encontrados en el camino: 0.
Evidencia: 33 archivos tocados · 1,1 h de sesión real · commits 5f2a175
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-27 03:14:11+00', v_cedula, false,
           'MEDIA', v_orden, 10.00, DATE '2026-07-27', DATE '2026-07-27',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-27 02:07:32+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0008-T9' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0008-T9'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0008-T9'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0019-T20 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — PWA offline-first con sincronización diferida', '[Bitácora jul-ago 2026] · tarea HIS-2026-0019-T20
Pedido del usuario: «realiza un analisi completo de la apciacion ara que sea 100 pwa fuera de linea de todos sus modulos al final se sincronize con la aplciacion en nueve cuendo tenga red la idea es que ya podemos trabjar fuera de linea con lo que tenemos contrido y si sigemos contruyendo seria que todo sea para las dos funcioens por ahroa que se establiza las funciones y deja alineado no contrior la app movil si no una pwa qeu se actualzie siempre que tnga nuevas cosas validmeos el proceso como seria y me muestraslo que encunteres , tener precente que lo fuera de lineas seria la informacion de los lotes seguimie»
Registrado desde el trabajo real del área de desarrollo (sesión b7039178, 20dc0bca, 2026-07-27).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-27 02:30:37+00', TIMESTAMPTZ '2026-08-10 09:24:03+00',
           'Qué se hizo (2 commits): docs(pwa): analisis completo y plan de PWA offline-first con sincronizacion diferida; feat(pwa): alistamiento para campo — persistencia de cuota y D6 (nada de snapshot multiempresa)
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea HIS-2026-0019-T20.
Evidencia: 30 archivos tocados · 1,3 h de sesión real · commits eb76034, b8821cb, 4616dfa
Estimación: 16 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-10 09:24:03+00', v_cedula, false,
           'MEDIA', v_orden, 16.00, DATE '2026-07-27', DATE '2026-08-10',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-27 02:30:37+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0019-T20' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0019-T20'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0019-T20'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260727-8b92 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'PWA Fase 0: higiene de entrega y sesión que sobrevive a la falta de red', '[Bitácora jul-ago 2026] · tarea SES-20260727-8b92
Pedido del usuario: «vamos a desarrollar este plan completo # Plan — PWA offline-first con sincronización diferida **Fecha:** 2026-07-26 **Estado:** ANÁLISIS COMPLETO / DISEÑO PROPUESTO — pendiente de decisiones del usuario antes de implementar **Alcance pedido:** que los módulos operativos funcionen sin red y sincronicen al recuperar conexión; PWA autoactualizable; **no** app móvil nativa; que lo que se construya de ahora en más nazca sirviendo para los dos modos. **Módulos operativos nombrados por el usuario:** gestión de lotes · seguimiento levante · seguimiento producción · pollo engorde · reproductora pollo»
Registrado desde el trabajo real del área de desarrollo (sesión 8b92c475, 2026-07-27).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-27 04:25:53+00', TIMESTAMPTZ '2026-07-27 14:08:05+00',
           'Qué se hizo (3 commits): chore(pwa): Fase 0.C - higiene de entrega para poder sostener un Service Worker; feat(pwa): Fase 0.B parcial - la sesion sobrevive a la falta de red (B2, B3, B7); docs(pwa): README de core/auth/funciones con la convencion y el porque de aislar las reglas de sesion
Bugs encontrados en el camino: 0.
Evidencia: 39 archivos tocados · 1,6 h de sesión real · commits 76a2903, f139dfd, 73b14d3
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-27 14:08:05+00', v_cedula, false,
           'MEDIA', v_orden, 8.00, DATE '2026-07-27', DATE '2026-07-27',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-27 04:25:53+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260727-8b92' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260727-8b92'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260727-8b92'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0012-T22 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Hora de encasetamiento: define el primer día con registro (engorde y reproductora)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0012-T22
Pedido del usuario: «en la migraciion masiva que se tiene para pollo engorde del seguimiento tenemos algo logico un excel que me das me mustras el alimento de hembras y machos en e consumo pero el consumo es mixto , pero si tengo el de qq mixto pero no tengo si le agrego el de kg de alimeto como seria te paso un ejemplo que me distes y valida si quito las dos filas de consumo macho y hembras y pongo conumo mixto kg carga el conumo para ese registro del dia»
Registrado desde el trabajo real del área de desarrollo (sesión 74faf114, 81854151, 2026-07-27).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-27 14:49:02+00', TIMESTAMPTZ '2026-07-30 19:16:47+00',
           'Qué se hizo (3 commits): feat(engorde): carga masiva MIXTA para Panama y descuento real de aves por mortalidad; feat(engorde): la hora de encasetamiento define el primer dia con registro; feat(engorde): regla de la hora de llegada por empresa + numeracion correcta del dia
Bugs encontrados en el camino: 3 — cada uno queda como subtarea BUG de la tarea HIS-2026-0012-T22.
Evidencia: 54 archivos tocados · 4,3 h de sesión real · commits 04e4118, f5765c7, 56edf3a, 7639b79, 528b283, 769a48c
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-30 19:16:47+00', v_cedula, false,
           'MEDIA', v_orden, 12.00, DATE '2026-07-27', DATE '2026-07-30',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-27 14:49:02+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0012-T22' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0012-T22'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0012-T22'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0012-T24 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Seguimiento pollo engorde MIXTO (Panamá): Excel mixto + descuento de aves mixtas', '[Bitácora jul-ago 2026] · tarea HIS-2026-0012-T24
Pedido del usuario: «en la migraciion masiva que se tiene para pollo engorde del seguimiento tenemos algo logico un excel que me das me mustras el alimento de hembras y machos en e consumo pero el consumo es mixto , pero si tengo el de qq mixto pero no tengo si le agrego el de kg de alimeto como seria te paso un ejemplo que me distes y valida si quito las dos filas de consumo macho y hembras y pongo conumo mixto kg carga el conumo para ese registro del dia»
Registrado desde el trabajo real del área de desarrollo (sesión 74faf114, 2026-07-27).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-27 14:49:02+00', TIMESTAMPTZ '2026-07-27 19:24:41+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-27 19:24:41+00', v_cedula, false,
           'MEDIA', v_orden, 12.00, DATE '2026-07-27', DATE '2026-07-27',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-27 14:49:02+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0012-T24' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0012-T24'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0012-T24'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0019-T21 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Fix — el deploy del frontend muere en el build de Docker (`MODULE_NOT_FOUND`)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0019-T21
Pedido del usuario: «realize ahroa un despelgue en con ci/cd y me salio enrror al despelgar el front agrego el log del despleigue y solucionalo para despelgar otra ves»
Registrado desde el trabajo real del área de desarrollo (sesión 50822f43, 2026-07-27).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-27 17:47:01+00', TIMESTAMPTZ '2026-07-27 19:53:27+00',
           'Qué se hizo (2 commits): docs(tracker): deploy del frontend verificado en prod tras el fix de .dockerignore; docs(tracker): registro del despliegue 30299439870 verificado en produccion
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea HIS-2026-0019-T21.
Evidencia: 4 archivos tocados · 1,1 h de sesión real · commits b0e38d3, 7c08df9, c30272c
Estimación: 4 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-27 19:53:27+00', v_cedula, false,
           'ALTA', v_orden, 4.00, DATE '2026-07-27', DATE '2026-07-27',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-27 17:47:01+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0019-T21' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0019-T21'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0019-T21'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0004-T6 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Fix — el reCAPTCHA de Google desapareció del login en producción', '[Bitácora jul-ago 2026] · tarea HIS-2026-0004-T6
Pedido del usuario: «tengo este error en produccion con el despelgue que no sale la utengticacion de google y es como si fuera dev en produccion»
Registrado desde el trabajo real del área de desarrollo (sesión f0ee4da2, 2026-07-27).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-27 19:56:04+00', TIMESTAMPTZ '2026-07-27 20:04:23+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea HIS-2026-0004-T6.
Evidencia: 5 archivos tocados · 0,1 h de sesión real · commits 2f46837
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-27 20:04:23+00', v_cedula, false,
           'MEDIA', v_orden, 2.00, DATE '2026-07-27', DATE '2026-07-27',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-27 19:56:04+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0004-T6' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0004-T6'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0004-T6'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0012-T23 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Pollo engorde: numeración de día 1-based y pesaje al cierre de semana', '[Bitácora jul-ago 2026] · tarea HIS-2026-0012-T23
Pedido del usuario: «en una anterior sesion trabaje algo con una regla de llegada o la hora de ahabrir un lote enotnces toma el seguinte registro como primera edad o despues de la 1 de la tarde toma el seguindo dia si llego el 08 despeus de las 1 pm la edad 1 seria para el 09 no 2 pero si llega antes el 08 es la edad 1 y el 09 seria 2 , en las reproductora se aplico pero en el lote pollo engorde de seguimeint ono esta implemntada la logica ahroa relaize una carga de datos y veo que cuando realizo cruce de las reproductoras no cuadro con edad 1 sino que comenseo en edad 0 enotnces hay se descuadra lo de pedir el p»
Registrado desde el trabajo real del área de desarrollo (sesión 81854151, 2026-07-27).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-27 20:48:39+00', TIMESTAMPTZ '2026-07-30 19:16:47+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-30 19:16:47+00', v_cedula, false,
           'MEDIA', v_orden, 8.00, DATE '2026-07-27', DATE '2026-07-30',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-27 20:48:39+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0012-T23' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0012-T23'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0012-T23'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0006-T12 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Alimento (ingresos/traslados) en el MISMO archivo de carga masiva de seguimiento engorde', '[Bitácora jul-ago 2026] · tarea HIS-2026-0006-T12
Pedido del usuario: «te voy a pasar dos archivos uno para que valides la poblacion del seguimeinto diario y entiendes la logica que se esta implementando en el archivo entonces validarlo que funcione y leugo realizas la prueba de cargarlo a seguimienit opollo engorde y identificar errores y corregisrlos , y te pasare lo que es el historico de alimento que se consumio el glpon y lo qeu debe quedar al fina en inventario para el alimento la idea es incontrar si en el mis»
Registrado desde el trabajo real del área de desarrollo (sesión 2bb86ee7, 2026-07-28).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-28 01:03:02+00', TIMESTAMPTZ '2026-07-28 06:43:13+00',
           'Qué se hizo (6 commits): feat(engorde): el alimento entra en el mismo archivo de carga masiva y el inventario cuadra; feat(engorde): movimiento Consumo en la hoja Alimento y reparacion del galpon 6; feat(engorde): un solo archivo para todo el lote, una hoja por modulo; chore(engorde): recarga del galpon 6 completa desde un unico archivo de 3 hojas; docs(engorde): archivo unico de carga para el lote 13-1 con guia y ejemplos; feat(engorde): una fecha ya cargada se reemplaza con lo que trae el archivo
Bugs encontrados en el camino: 3 — cada uno queda como subtarea BUG de la tarea HIS-2026-0006-T12.
Evidencia: 26 archivos tocados · 5,7 h de sesión real · commits 85da238, 9d2b6c3, 3145f01, 6e7987a, 0723fde, 1a6af9b, eb8c38f, 36a8bab, 54ce0e1
Estimación: 20 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-28 06:43:13+00', v_cedula, false,
           'MEDIA', v_orden, 20.00, DATE '2026-07-28', DATE '2026-07-28',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-28 01:03:02+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0006-T12' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0006-T12'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0006-T12'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0015-T8 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Informe RA Pesadas (Parámetros + Gráficos)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0015-T8
Pedido del usuario: «valida para crear estos reportes en sanmarino si va esto en varios reprotes o uno solo que tenga varios tap de las hojas la gua gtenetica que aparece aqui es vieja ya la aplciacion esta con la neuva valia par implemtnar estos reprotes y que esten alineados y cumplan todo»
Registrado desde el trabajo real del área de desarrollo (sesión 2dbe27a8, 2026-07-28).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-28 06:50:03+00', TIMESTAMPTZ '2026-07-29 03:55:51+00',
           'Qué se hizo (11 commits): docs(postura): validacion del Informe RA Pesadas - plan y tracker; docs(postura): decisiones D1-D5 del Informe RA Pesadas; feat(postura): capa SQL del Resumen Semanal del Informe RA Pesadas; feat(postura): endpoint del Resumen Semanal del Informe RA Pesadas; feat(postura): front del Resumen Semanal del Informe RA Pesadas; docs(postura): tracker de la carga masiva con inventario; feat(postura): hojas ALIMLev y CLAS Huevo del Informe RA Pesadas; feat(postura): cierre del Informe RA Pesadas - export y menu
Bugs encontrados en el camino: 3 — cada uno queda como subtarea BUG de la tarea HIS-2026-0015-T8.
Evidencia: 29 archivos tocados · 4,8 h de sesión real · commits 4ce11be, 2eeac5a, dc7834a, 2e6484f, 1b236bb, 3760b15, 51628ac, a1e5b96, 145348b, add95cd
Estimación: 24 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-29 03:55:51+00', v_cedula, false,
           'MEDIA', v_orden, 24.00, DATE '2026-07-28', DATE '2026-07-29',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-28 06:50:03+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0015-T8' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0015-T8'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0015-T8'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0006-T13 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Carga masiva de Postura (Levante + Producción): alimento con inventario real, huevos completos y validaciones a paridad con el alta manual', '[Bitácora jul-ago 2026] · tarea HIS-2026-0006-T13
Pedido del usuario: «en el modulo de carga masiva manual tenemos el de postura levante y produccion , por ahroa produccion es para sanmarino la carga masiva ya que en produccion santa reyes es diferente los tipos de huevos y alimentos ,e ntonces la logica es diferente pro empresa , enotnces vamos validar que la migracion manual funcione al momento de crear lovantes y produccion , con los campos que pide tenga claros el proceso de carga y la parte de ingreso de alimento tmaibne ya que tiene el modulo de ingreso de aliemnto para el galpon y con el valdiamos que tengamos inventario para consumir y llevemos un inven»
Registrado desde el trabajo real del área de desarrollo (sesión ec7d32dc, 2026-07-28).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-28 18:40:12+00', TIMESTAMPTZ '2026-07-28 21:35:53+00',
           'Qué se hizo (1 commit): feat(postura): la carga masiva de levante y produccion mueve inventario de alimento
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea HIS-2026-0006-T13.
Evidencia: 33 archivos tocados · 1,9 h de sesión real · commits 7846200, f359290
Estimación: 16 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-28 21:35:53+00', v_cedula, false,
           'MEDIA', v_orden, 16.00, DATE '2026-07-28', DATE '2026-07-28',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-28 18:40:12+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0006-T13' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0006-T13'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0006-T13'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0012-T26 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Cuadre de aves y alimento en pollo engorde (Panamá)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0012-T26
Pedido del usuario: «tengo un error en produccion ya descargue la base de datos de produccion en panama en pollo engorde ya se cargo todos los lotes con sus seguimiento diario de pollo engorde y seguimeinto de reproductora , ahroa tenemos que hacer cuadre de cada lo te pollo engorde , primero que la primera face de sus reproductora cuadre en el seguimeinto pollo enogrde de sus 7 dias que tengamos las aves que nos muestra en mixto y estamos descontando cuadre correctamente y alineamos tambine el aliemtno ya en el modulo de gestion de inventario ya esta lo que debe tener en alimento y en el seguimeint odiario pollo»
Registrado desde el trabajo real del área de desarrollo (sesión bd2fc0e8, 2026-07-29).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-29 18:38:16+00', TIMESTAMPTZ '2026-07-30 01:06:51+00',
           'Qué se hizo (4 commits): docs(tracker): validacion de las migraciones de cuadre sobre el dump de produccion actual; docs(tracker): validacion cruzada del cuadre con el Reporte Diario de Costos Engorde; docs(tracker): registro del despliegue a produccion y su verificacion post-deploy; docs(engorde): requerimiento del cuadre de Ecuador para otra sesion
Bugs encontrados en el camino: 5 — cada uno queda como subtarea BUG de la tarea HIS-2026-0012-T26.
Evidencia: 24 archivos tocados · 4,7 h de sesión real · commits 05ded34, 2af742d, 088a97c, 7f3a28c, 21e53ab, 2cc4855, 2f58e22, a050ec7, 9a753ea
Estimación: 16 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-30 01:06:51+00', v_cedula, false,
           'MEDIA', v_orden, 16.00, DATE '2026-07-29', DATE '2026-07-30',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-29 18:38:16+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0012-T26' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0012-T26'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0012-T26'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0012-T27 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Requerimiento — Cuadre de alimento y aves en pollo engorde (ItalcolEcuador)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0012-T27
Pedido del usuario: «tengo un error en produccion ya descargue la base de datos de produccion en panama en pollo engorde ya se cargo todos los lotes con sus seguimiento diario de pollo engorde y seguimeinto de reproductora , ahroa tenemos que hacer cuadre de cada lo te pollo engorde , primero que la primera face de sus reproductora cuadre en el seguimeinto pollo enogrde de sus 7 dias que tengamos las aves que nos muestra en mixto y estamos descontando cuadre correctamente y alineamos tambine el aliemtno ya en el modulo de gestion de inventario ya esta lo que debe tener en alimento y en el seguimeint odiario pollo»
Registrado desde el trabajo real del área de desarrollo (sesión bd2fc0e8, 91a7cb88, 2026-07-29).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-29 18:38:16+00', TIMESTAMPTZ '2026-07-30 19:20:14+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 3 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-30 19:20:14+00', v_cedula, false,
           'MEDIA', v_orden, 3.00, DATE '2026-07-29', DATE '2026-07-30',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-29 18:38:16+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0012-T27' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0012-T27'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0012-T27'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0008-T10 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Congelar la liquidación de un lote de pollo engorde', '[Bitácora jul-ago 2026] · tarea HIS-2026-0008-T10
Pedido del usuario: «tenog un reporte de ecuador sobre una granja con un erro primero tengo que encontrar el error o el descuedre que se tiene antes de tocar ya descargue la base de datos de poduccion en local para que se balide : Buenos día estimado Moises, solicito su ayuda con el siguiente hallazgo tenemos una diferencia en el alimento desde el primer Dia esto se puede ver recién hoy que no se está considerando el saldo correcto nos ingresó 12000- 480 de consumo deberíamos tener 11520 pero el aplicativo nos está mostrando 3560 pero solo es en lo visual porque en el stock si tenemos lo correcto»
Registrado desde el trabajo real del área de desarrollo (sesión 91a7cb88, 70a7e970, 2026-07-30).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-30 01:03:22+00', TIMESTAMPTZ '2026-07-31 20:20:19+00',
           'Qué se hizo (8 commits): docs(engorde): diagnostico del saldo de alimento de Ecuador - la grilla recalcula una apertura fantasma; docs(engorde): validacion de cierre lote/ciclo/galpon en Ecuador - 25 de 35 galpones OK; perf(engorde): indice por granja+fecha en el historico unificado; feat(engorde): prevencion de descuadres de alimento - los 5 puntos; docs(engorde): instructivo de operacion para Costos de Ecuador y Panama; docs(engorde): el instructivo identifica los galpones por nombre, nucleo e id; docs(engorde): el instructivo abre con el estado real por corrida, antes y despues; feat(engorde): liquidacion congelada - un lote liquidado ya no cambia solo
Bugs encontrados en el camino: 4 — cada uno queda como subtarea BUG de la tarea HIS-2026-0008-T10.
Evidencia: 65 archivos tocados · 7,3 h de sesión real · commits 7b26052, 4923e2b, f718a3e, c346f35, 4d3e61f, ae1df1a, 9ad5492, e68a9b6, e2a8a3d, a396d1f
Estimación: 16 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-31 20:20:19+00', v_cedula, false,
           'MEDIA', v_orden, 16.00, DATE '2026-07-30', DATE '2026-07-31',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-30 01:03:22+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0008-T10' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0008-T10'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0008-T10'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0012-T25 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Diagnóstico — Saldo de alimento en pantalla ≠ stock (ItalcolEcuador)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0012-T25
Pedido del usuario: «tenog un reporte de ecuador sobre una granja con un erro primero tengo que encontrar el error o el descuedre que se tiene antes de tocar ya descargue la base de datos de poduccion en local para que se balide : Buenos día estimado Moises, solicito su ayuda con el siguiente hallazgo tenemos una diferencia en el alimento desde el primer Dia esto se puede ver recién hoy que no se está considerando el saldo correcto nos ingresó 12000- 480 de consumo deberíamos tener 11520 pero el aplicativo nos está mostrando 3560 pero solo es en lo visual porque en el stock si tenemos lo correcto»
Registrado desde el trabajo real del área de desarrollo (sesión 91a7cb88, 2026-07-30).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-30 01:03:22+00', TIMESTAMPTZ '2026-07-30 19:20:14+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-30 19:20:14+00', v_cedula, false,
           'MEDIA', v_orden, 6.00, DATE '2026-07-30', DATE '2026-07-30',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-30 01:03:22+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0012-T25' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0012-T25'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0012-T25'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0012-T28 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — La apertura de alimento deja de heredar el ciclo anterior del galpón', '[Bitácora jul-ago 2026] · tarea HIS-2026-0012-T28
Pedido del usuario: «tenog un reporte de ecuador sobre una granja con un erro primero tengo que encontrar el error o el descuedre que se tiene antes de tocar ya descargue la base de datos de poduccion en local para que se balide : Buenos día estimado Moises, solicito su ayuda con el siguiente hallazgo tenemos una diferencia en el alimento desde el primer Dia esto se puede ver recién hoy que no se está considerando el saldo correcto nos ingresó 12000- 480 de consumo deberíamos tener 11520 pero el aplicativo nos está mostrando 3560 pero solo es en lo visual porque en el stock si tenemos lo correcto»
Registrado desde el trabajo real del área de desarrollo (sesión 91a7cb88, 2026-07-30).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-30 01:03:22+00', TIMESTAMPTZ '2026-07-30 19:20:14+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-30 19:20:14+00', v_cedula, false,
           'ALTA', v_orden, 8.00, DATE '2026-07-30', DATE '2026-07-30',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-30 01:03:22+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0012-T28' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0012-T28'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0012-T28'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0012-T29 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Que estos descuadres no se puedan repetir', '[Bitácora jul-ago 2026] · tarea HIS-2026-0012-T29
Pedido del usuario: «tenog un reporte de ecuador sobre una granja con un erro primero tengo que encontrar el error o el descuedre que se tiene antes de tocar ya descargue la base de datos de poduccion en local para que se balide : Buenos día estimado Moises, solicito su ayuda con el siguiente hallazgo tenemos una diferencia en el alimento desde el primer Dia esto se puede ver recién hoy que no se está considerando el saldo correcto nos ingresó 12000- 480 de consumo deberíamos tener 11520 pero el aplicativo nos está mostrando 3560 pero solo es en lo visual porque en el stock si tenemos lo correcto»
Registrado desde el trabajo real del área de desarrollo (sesión 91a7cb88, 2026-07-30).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-30 01:03:22+00', TIMESTAMPTZ '2026-07-30 19:20:14+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-30 19:20:14+00', v_cedula, false,
           'ALTA', v_orden, 10.00, DATE '2026-07-30', DATE '2026-07-30',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-30 01:03:22+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0012-T29' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0012-T29'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0012-T29'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0012-T30 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Saldos de alimento en pollo engorde — estado real y qué queda por revisar', '[Bitácora jul-ago 2026] · tarea HIS-2026-0012-T30
Pedido del usuario: «tenog un reporte de ecuador sobre una granja con un erro primero tengo que encontrar el error o el descuedre que se tiene antes de tocar ya descargue la base de datos de poduccion en local para que se balide : Buenos día estimado Moises, solicito su ayuda con el siguiente hallazgo tenemos una diferencia en el alimento desde el primer Dia esto se puede ver recién hoy que no se está considerando el saldo correcto nos ingresó 12000- 480 de consumo deberíamos tener 11520 pero el aplicativo nos está mostrando 3560 pero solo es en lo visual porque en el stock si tenemos lo correcto»
Registrado desde el trabajo real del área de desarrollo (sesión 91a7cb88, 70a7e970, 2026-07-30).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-30 01:03:22+00', TIMESTAMPTZ '2026-07-31 20:20:19+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 3 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-07-31 20:20:19+00', v_cedula, false,
           'MEDIA', v_orden, 3.00, DATE '2026-07-30', DATE '2026-07-31',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-30 01:03:22+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0012-T30' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0012-T30'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0012-T30'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0006-T14 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Carga masiva Seguimiento Diario Levante: movimientos de aves + tab huevos fijo + ocultar estructura', '[Bitácora jul-ago 2026] · tarea HIS-2026-0006-T14
Pedido del usuario: «Vamos a ir al módulo carga masiva manual, migración manual. Entonces, en ese migración manual tenemos una fase que se llama unas fases que son para cargar masivamente galpones, granjas. Eso lo vamos a deshabilitar para que no aparezca visualmente. En el en el que vamos a trabajar ahorita es en el de carga masiva de seguimiento diario levante. Entonces, ¿este qué va a hacer? Pues va a tener toda la lógica de de lo que cuando yo hago un seguimiento diario, el registro de seguimiento diario, pero en este voy a tener que colocar ingresos. También va a haber una hoja, cuando yo descargue la plantil»
Registrado desde el trabajo real del área de desarrollo (sesión 5b68e3ea, 2026-07-31).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-07-31 19:04:50+00', TIMESTAMPTZ '2026-08-01 01:53:55+00',
           'Qué se hizo (4 commits): feat(migracion): hoja Movimientos Aves en carga masiva de levante + tab de huevos fijo; feat(migracion): venta de aves en la hoja Movimientos Aves + fixes cazados por el E2E de ciclo completo; docs(tracker): cierre del lote 130 validado - LPP creado con 9495/929 aves, 130 huevos arrastrados y elegible para carga masiva…; feat(migracion): carga masiva de produccion completa - agua y pesaje, movimientos de huevos a planta/venta y aves en ambas fases
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea HIS-2026-0006-T14.
Evidencia: 34 archivos tocados · 2,6 h de sesión real · commits 3453b09, fd6e51f, 12e0ebe, 21a5c81, b64898f
Estimación: 16 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-01 01:53:55+00', v_cedula, false,
           'MEDIA', v_orden, 16.00, DATE '2026-07-31', DATE '2026-08-01',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-07-31 19:04:50+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0006-T14' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0006-T14'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0006-T14'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0014-T11 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Seguimiento Diario de PRODUCCIÓN: fn SQL canónica + reducción de services + invariantes', '[Bitácora jul-ago 2026] · tarea HIS-2026-0014-T11
Pedido del usuario: «Vamos a hacer una mejora ESTRUCTURAL del módulo de Seguimiento Diario de PRODUCCIÓN (postura): pasar la lectura de la tabla a una FUNCIÓN SQL canónica (estilo engorde), mover a SQL las lecturas/agregaciones pesadas que hoy viven en los services, blindar invariantes con triggers donde corresponda, y limpiar la calidad del código (menos subconsultas, menos N+1, partials y cálculo puro con tests). Trabajá con plan en fase_de_desarrollo/ y tracker (bloque NUEVO AL FINAL de tracker_estado.md — hay sesiones paralelas, no pises nada). == ESTADO ACTUAL (verificado 01-ago-2026, no re-descubras esto) =»
Registrado desde el trabajo real del área de desarrollo (sesión 4d108bcf, 2026-08-01).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-01 01:55:38+00', TIMESTAMPTZ '2026-08-01 07:07:21+00',
           'Qué se hizo (5 commits): feat(produccion): fn_seguimiento_diario_produccion canonica - grilla, header y fns semanales sobre una sola formula; espejo…; docs(tracker): cierre del bloque fn canonica de seguimiento produccion - fases 1-4 validadas, smoke verde y particion en partials…; feat(produccion): fn v2 filas TSD visibles en grilla LPP; writer legacy anclado a mediodia con rango de dia; Reporte Contable…; merge(reporte-contable): reconciliacion con la sesion del chip - calculo puro + tests + alcance padre y sublotes; feat(espejo-huevos): DROP historico_semanal + indice GIN (columna muerta, OK explicito) - entidad y configuration sin la…
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea HIS-2026-0014-T11.
Evidencia: 34 archivos tocados · 2,8 h de sesión real · commits 4034b8f, 5aff254, 5a3b220, 6de9ea9, c4741a0, f6ac8c7
Estimación: 20 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-01 07:07:21+00', v_cedula, false,
           'MEDIA', v_orden, 20.00, DATE '2026-08-01', DATE '2026-08-01',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-01 01:55:38+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0014-T11' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0014-T11'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0014-T11'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0007-T18 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Gastos de inventario: reporte sin eliminados + hoja de existencias completas', '[Bitácora jul-ago 2026] · tarea HIS-2026-0007-T18
Pedido del usuario: «en el modulo de gastos de inventario al momento de descargar un reprote si esta descargando los eliminados y no se si esta regresando al invientario cuando se elimina lo que se regsitro ya que al momento de descargar el reprote lo trae y anterior mente se realizo un la implemtnacion de este caso pero no se implemntado la solucion o no se termino , entonces tengo este error entonces dejo la novedad por parte del usaurio y la imagen y el rprote descargado qeu pasa con ecuador ya que este es un modulo trasvesar entre enpresas es un erro que puede pasar en todas, ya que en el servicio que me mues»
Registrado desde el trabajo real del área de desarrollo (sesión 02ddb5a6, 2026-08-05).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-05 15:10:59+00', TIMESTAMPTZ '2026-08-05 15:58:01+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea HIS-2026-0007-T18.
Evidencia: 24 archivos tocados · 0,8 h de sesión real · commits 116e052
Estimación: 5 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-05 15:58:01+00', v_cedula, false,
           'MEDIA', v_orden, 5.00, DATE '2026-08-05', DATE '2026-08-05',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-05 15:10:59+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0007-T18' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0007-T18'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0007-T18'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0010-T12 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Fix — «Aves disponibles» difiere entre Seguimiento diario y Venta (pollo engorde)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0010-T12
Pedido del usuario: «encontre una novedad en seguimiento diario , y ventas en el seguimiento diario en la parte qeu me da las aves disponibles esta sumando las aves del otro lote que esta cerrado que esta en 7 y mas 32 da 40 aves disponibles eso dice en el seguimeinto diario pero en la venta dice que tiene 32 y las 32 deben ser las correctas ya que no se puede sumar aves entre lote de pollo engorde hay un error de la logica que se aplico y esta mostrando datos que no son correcto o no estan disponibles de ambos ya que ventas depende de lo disponible que deja seguimeinto dairio y seguimeinto diario va dejando de»
Registrado desde el trabajo real del área de desarrollo (sesión 600a26a2, 2026-08-05).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-05 15:33:56+00', TIMESTAMPTZ '2026-08-05 17:04:50+00',
           'Qué se hizo (1 commit): merge(pollo-engorde): reconciliacion con 75f7980 - correccion de datos + baseline de las bajas
Bugs encontrados en el camino: 3 — cada uno queda como subtarea BUG de la tarea HIS-2026-0010-T12.
Evidencia: 16 archivos tocados · 1,5 h de sesión real · commits 933b3b1, 3998aa2, 75f7980, b9cab63
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-05 17:04:50+00', v_cedula, false,
           'ALTA', v_orden, 8.00, DATE '2026-08-05', DATE '2026-08-05',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-05 15:33:56+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0010-T12' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0010-T12'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0010-T12'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0018-T2 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Migrar el envío de correo a Microsoft Graph API (retiro de auth básica SMTP)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0018-T2
Pedido del usuario: «TENGO UN ERROR AL MOMENTO DE ENVIAR CORREO ELETRONICO EN PRODUCCION YA UQE PARA ESTE 2026 EL PROTOCOLO DE ENVIO CAMBIO ENTONCES NECESITO CAMIBNAR ESO EN LOS ENCARGADOS DE ENVIO DE CORREO CONFIGURADOS EN EL PORYECTO»
Registrado desde el trabajo real del área de desarrollo (sesión 86121869, 2026-08-05).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-05 17:47:35+00', TIMESTAMPTZ '2026-08-05 22:52:02+00',
           'Qué se hizo (3 commits): merge(main): integrar la correccion de la referencia Inicio con la migracion de correo a Graph; refactor(correo): dejar un solo transporte SMTP y revertir el emisor por Graph; docs(gastos-inventario): validacion sobre la BD restaurada de prod + correccion de atribucion
Bugs encontrados en el camino: 4 — cada uno queda como subtarea BUG de la tarea HIS-2026-0018-T2.
Evidencia: 23 archivos tocados · 1,6 h de sesión real · commits cadd84f, abe3643, 31e3654, d341223, c7b6834, 587d6cc, 2cab258
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-05 22:52:02+00', v_cedula, false,
           'MEDIA', v_orden, 10.00, DATE '2026-08-05', DATE '2026-08-05',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-05 17:47:35+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0018-T2' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0018-T2'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0018-T2'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0007-T17 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Descargar Excel del stock de TODAS las granjas — Gestión de Inventario', '[Bitácora jul-ago 2026] · tarea HIS-2026-0007-T17
Pedido del usuario: «tengo un requerimeinto nuevo , en el modulo de gestion de inventario necesito que descargeu el stock disponible de todas las granjas con su galpon correspondiente si e alimento si es otro item sobre la granja entonces hay me debe traer todas las las granjas al descargar el excel ya que me piden descargar en excel lo que esta en la palicaicon > BUENAS TARDES ESTIMADO MOISES, SOLICITO SU AYUDA EN PODER DESCARGAR EN EXCEL EL STOCK QUE TENEMOS EN CADA BODEGA PARA PODER REALIZAR UN COMPARATIVO,»
Registrado desde el trabajo real del área de desarrollo (sesión bac7ce9f, 2026-08-05).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-05 18:56:48+00', TIMESTAMPTZ '2026-08-05 19:55:40+00',
           'Qué se hizo (2 commits): feat(gestion-inventario): descargar en Excel el stock de todas las granjas; feat(gestion-inventario): el Excel de stock sale en dos hojas por concepto
Bugs encontrados en el camino: 0.
Evidencia: 16 archivos tocados · 1,0 h de sesión real · commits 6b1f635, 19adf57
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-05 19:55:40+00', v_cedula, false,
           'MEDIA', v_orden, 6.00, DATE '2026-08-05', DATE '2026-08-05',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-05 18:56:48+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0007-T17' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0007-T17'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0007-T17'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0010-T13 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Traslado de aves: destino cross-granja/galpón en Engorde + fecha de registro visible', '[Bitácora jul-ago 2026] · tarea HIS-2026-0010-T13
Pedido del usuario: «al momento de realizar traslado de aves tanto de pollo engorde y postura en levante o produccion debe tenerla forma de realizar traslados a otraws granjas otros galpones pero tamibne tener un campo de fecha de traslado y otro que es la fecha de creacion del registro ya que se tiene que son dos tipos de datos diferentes entonces el usuario en la web modifica la fecha de traslado de aves o de lote entonces pro eso dehamso el created_at como la fecha de creacion en el sistema»
Registrado desde el trabajo real del área de desarrollo (sesión a7c907b3, 2026-08-06).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-06 01:34:48+00', TIMESTAMPTZ '2026-08-06 05:19:02+00',
           'Qué se hizo (2 commits): feat(traslado-aves): destino en otra granja/galpon para engorde y fecha de registro visible; feat(cohortes): un lote que recibe aves guarda de donde vienen y con que edad
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea HIS-2026-0010-T13.
Evidencia: 52 archivos tocados · 2,6 h de sesión real · commits 00ff4b5, 881812d, d50cd9c
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-06 05:19:02+00', v_cedula, false,
           'MEDIA', v_orden, 12.00, DATE '2026-08-06', DATE '2026-08-06',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-06 01:34:48+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0010-T13' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0010-T13'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0010-T13'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0010-T14 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Trazabilidad de cohortes: cuántas aves, de dónde y con qué edad en el lote receptor', '[Bitácora jul-ago 2026] · tarea HIS-2026-0010-T14
Pedido del usuario: «al momento de realizar traslado de aves tanto de pollo engorde y postura en levante o produccion debe tenerla forma de realizar traslados a otraws granjas otros galpones pero tamibne tener un campo de fecha de traslado y otro que es la fecha de creacion del registro ya que se tiene que son dos tipos de datos diferentes entonces el usuario en la web modifica la fecha de traslado de aves o de lote entonces pro eso dehamso el created_at como la fecha de creacion en el sistema»
Registrado desde el trabajo real del área de desarrollo (sesión a7c907b3, 2026-08-06).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-06 01:34:48+00', TIMESTAMPTZ '2026-08-06 05:19:02+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-06 05:19:02+00', v_cedula, false,
           'MEDIA', v_orden, 10.00, DATE '2026-08-06', DATE '2026-08-06',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-06 01:34:48+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0010-T14' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0010-T14'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0010-T14'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0012-T33 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — `tipo_alimento` desborda `varchar(100)` y tumba el guardado del seguimiento diario', '[Bitácora jul-ago 2026] · tarea HIS-2026-0012-T33
Pedido del usuario: «me reprotan el siguitne error cuando realizan un seguimeinto diario a un lote en particular en sanmarino colombia , no lo entiendo mas o menos y valida tu realziando todo el flujo para poder darme detalle del error: Luego de ingresar datos en el lote A374A e intentar guardar sale aviso de falla en guardado y al volver a entrar no aparece la información.»
Registrado desde el trabajo real del área de desarrollo (sesión 7132c5db, 2026-08-06).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-06 05:25:57+00', TIMESTAMPTZ '2026-08-06 08:02:59+00',
           'Qué se hizo (2 commits): docs(carga-masiva): E2E del lote S-369 en local — carga validada y 3 defectos de reporte; docs(carga-masiva): ciclo completo del S-369 en local y una venta que el reporte de produccion no ve
Bugs encontrados en el camino: 2 — cada uno queda como subtarea BUG de la tarea HIS-2026-0012-T33.
Evidencia: 24 archivos tocados · 1,9 h de sesión real · commits b947cf2, ccb372b, 2a35d63, 92e1cb5
Estimación: 5 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-06 08:02:59+00', v_cedula, false,
           'ALTA', v_orden, 5.00, DATE '2026-08-06', DATE '2026-08-06',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-06 05:25:57+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0012-T33' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0012-T33'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0012-T33'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0006-T15 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Archivos de carga masiva del lote S-369AB (postura: levante + producción + alimento)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0006-T15
Pedido del usuario: «estoy realizando el carga masiva de postura de levante y produccion con alimento tamibne , la idea es cargar ek seguimeinto dairio entonces me pasaron estos archivos donde tinen toda la informacion del lote seguimeinto de cada una de las fases entonces en la carpeta estan los archivos entonces tenemos que estudiar estos archivos para sacar el archivo de carga masivo constouirlo con la informacion que esta aqui S369 de la regional Centro es el lote que no esta creado pero seria para crearlo en una granja de granja pruebas hasta galpon de prubas entonces necesito primero con estos archivos contr»
Registrado desde el trabajo real del área de desarrollo (sesión 4186dd9a, cc4398d8, 2026-08-06).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-06 05:39:03+00', TIMESTAMPTZ '2026-08-07 21:29:48+00',
           'Qué se hizo (5 commits): docs(carga-masiva): archivos de migracion del lote S-369AB (levante + produccion + alimento); docs(carga-masiva): validacion exhaustiva del S-369 y el origen de los 5 huevos; feat(postura): unifica el tab Indicadores de levante y produccion y quita el Reporte semana; docs(postura): handoff de los hallazgos de la sesion para continuar en otra ventana; docs(postura): manual de carga masiva para implementacion
Bugs encontrados en el camino: 7 — cada uno queda como subtarea BUG de la tarea HIS-2026-0006-T15.
Evidencia: 47 archivos tocados · 9,2 h de sesión real · commits c110718, 1398335, 219f05f, 148f061, 4f7b83e, 2ac57a8, 2d26fae, 22f3be2, b34e629, 91533a0
Estimación: 16 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-07 21:29:48+00', v_cedula, false,
           'MEDIA', v_orden, 16.00, DATE '2026-08-06', DATE '2026-08-07',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-06 05:39:03+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0006-T15' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0006-T15'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0006-T15'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0013-T7 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Tab «Indicadores» de Levante y Producción — validación contra la guía genética + unificación UX', '[Bitácora jul-ago 2026] · tarea HIS-2026-0013-T7
Pedido del usuario: «estoy realizando el carga masiva de postura de levante y produccion con alimento tamibne , la idea es cargar ek seguimeinto dairio entonces me pasaron estos archivos donde tinen toda la informacion del lote seguimeinto de cada una de las fases entonces en la carpeta estan los archivos entonces tenemos que estudiar estos archivos para sacar el archivo de carga masivo constouirlo con la informacion que esta aqui S369 de la regional Centro es el lote que no esta creado pero seria para crearlo en una granja de granja pruebas hasta galpon de prubas entonces necesito primero con estos archivos contr»
Registrado desde el trabajo real del área de desarrollo (sesión 4186dd9a, 2026-08-06).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-06 05:39:03+00', TIMESTAMPTZ '2026-08-07 06:48:48+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-07 06:48:48+00', v_cedula, false,
           'MEDIA', v_orden, 10.00, DATE '2026-08-06', DATE '2026-08-07',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-06 05:39:03+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0013-T7' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0013-T7'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0013-T7'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0014-T12 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Handoff — hallazgos de la sesión de postura (06-07 ago 2026)', '[Bitácora jul-ago 2026] · tarea HIS-2026-0014-T12
Pedido del usuario: «estoy realizando el carga masiva de postura de levante y produccion con alimento tamibne , la idea es cargar ek seguimeinto dairio entonces me pasaron estos archivos donde tinen toda la informacion del lote seguimeinto de cada una de las fases entonces en la carpeta estan los archivos entonces tenemos que estudiar estos archivos para sacar el archivo de carga masivo constouirlo con la informacion que esta aqui S369 de la regional Centro es el lote que no esta creado pero seria para crearlo en una granja de granja pruebas hasta galpon de prubas entonces necesito primero con estos archivos contr»
Registrado desde el trabajo real del área de desarrollo (sesión 4186dd9a, 2026-08-06).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-06 05:39:03+00', TIMESTAMPTZ '2026-08-07 06:48:48+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-07 06:48:48+00', v_cedula, false,
           'MEDIA', v_orden, 2.00, DATE '2026-08-06', DATE '2026-08-07',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-06 05:39:03+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0014-T12' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0014-T12'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0014-T12'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0001-T12 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Tickets como CASOS tipo Jira: tareas, tablero, tiempos y solicitante delegado', '[Bitácora jul-ago 2026] · tarea HIS-2026-0001-T12
Pedido del usuario: «en el modulo ticket en el modulo de mi solicitudes puedo crear solicitudes para mi si soy el mismo usuario , tengo que esta loueado tengo que colocar de que usurio del sistema biene la soliccutu ya que puedo resolver casos que no estan montado en la aplciacion por un usuario entonces cosas que voy incontrando en si , y quiero tener un modulo tipo jira que tome ticket como casos y pueda creaer tareas historicas etc , como en sira y moverlos como en gira y colocar tiempos de solucion y todo lo necsario que conlleven y con fases de desarrollo , analisis documentacion , en revicion , solucionado»
Registrado desde el trabajo real del área de desarrollo (sesión f4ac9295, 2026-08-06).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-06 18:57:23+00', TIMESTAMPTZ '2026-08-07 06:59:44+00',
           'Qué se hizo (3 commits): feat(tickets): los tickets pasan a ser casos tipo Jira, con tareas, tablero y tiempos; feat(tickets): panel de control del administrador y reporte detallado a Excel; feat(tickets): una sola barra de filtros para tablero, roadmap y panel
Bugs encontrados en el camino: 3 — cada uno queda como subtarea BUG de la tarea HIS-2026-0001-T12.
Evidencia: 74 archivos tocados · 4,1 h de sesión real · commits 4bf63d1, 152be88, d536926, 588dc94, 0ce0485, 4f61046
Estimación: 24 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-07 06:59:44+00', v_cedula, false,
           'MEDIA', v_orden, 24.00, DATE '2026-08-06', DATE '2026-08-07',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-06 18:57:23+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0001-T12' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0001-T12'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0001-T12'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── HIS-2026-0014-T13 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Plan — Reconciliar el espejo `.sql` de `fn_indicadores_produccion_postura` + `uniformidad_guia` NULL', '[Bitácora jul-ago 2026] · tarea HIS-2026-0014-T13
Pedido del usuario: «# Handoff — hallazgos de la sesión de postura (06-07 ago 2026) Todo lo de acá ya está **commiteado en `main`** y **aplicado en la BD local**. En producción se aplica solo en el próximo deploy (EF corre las migraciones al arrancar). Origen: cargar el lote histórico **S-369** (levante + producción + alimento) desde tres Excel y hacer que los reportes de la app coincidan con ellos. Al hacerlo salieron a la luz una docena de defectos que **no eran de este lote** sino del código, y que estaban vivos en producción para todas las empresas. --- ## 1 · Commits de esta sesión (postura) | Commit | Q»
Registrado desde el trabajo real del área de desarrollo (sesión 2ec6763f, 2026-08-07).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-07 06:51:48+00', TIMESTAMPTZ '2026-08-07 07:34:17+00',
           'Qué se hizo (3 commits): chore(postura): detector de sobregiro de aves para decidir el bloqueo del seguimiento; feat(postura): la fn emite el %Seleccion de machos (la tabla mostraba %Sel H sin su par); Merge branch ''main'' into claude/mystifying-haslett-917cc0
Bugs encontrados en el camino: 2 — cada uno queda como subtarea BUG de la tarea HIS-2026-0014-T13.
Evidencia: 4 archivos tocados · 0,7 h de sesión real · commits f8f887a, 9f56da1, 2eb2382, d9d45bb, 6be9031
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-07 07:34:17+00', v_cedula, false,
           'MEDIA', v_orden, 6.00, DATE '2026-08-07', DATE '2026-08-07',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-07 06:51:48+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'HIS-2026-0014-T13' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'HIS-2026-0014-T13'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'HIS-2026-0014-T13'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260807-276f ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'ItalJira: la gestión del área de desarrollo sale de Tickets a un módulo propio', '[Bitácora jul-ago 2026] · tarea SES-20260807-276f
Pedido del usuario: «ahroa en el modulo de ticket donde recibo el ticket y gestiono la apliicacion encesiot que esmo modulo este bien acomodado donde gestione lostiempo y tareas historias de casos etc , cuando se crea por un usuario es una tarea sin historia pero si es una historia un proceso que realize manual desde el area de desarrollo donde implemnteo el desarrollo directamente ya sea el area de requerimeinto o el administrador se asigne o me asigne trabajos , tamibne vamos a organizar que lso tiempo de entrega y finalizacion ay que puedo crea una historia que se llama modulo de ticket y dentro de ella tendr»
Registrado desde el trabajo real del área de desarrollo (sesión 276ffba3, 2026-08-07).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-07 07:09:20+00', TIMESTAMPTZ '2026-08-07 09:11:13+00',
           'Qué se hizo (1 commit): feat(italjira): saca la gestion del area de desarrollo de Tickets a un modulo propio
Bugs encontrados en el camino: 0.
Evidencia: 61 archivos tocados · 2,0 h de sesión real · commits 5f5eb9a
Estimación: 16 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-07 09:11:13+00', v_cedula, false,
           'MEDIA', v_orden, 16.00, DATE '2026-08-07', DATE '2026-08-07',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-07 07:09:20+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260807-276f' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260807-276f'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260807-276f'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260807-bd7a ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'make dev-back cierra la instancia anterior antes de compilar (bin bloqueado)', '[Bitácora jul-ago 2026] · tarea SES-20260807-bd7a
Pedido del usuario: «al levantar el back me sale error > PS C:\Users\SAN MARINO\Desktop\App_SanMarino> make dev-back powershell -NoProfile -ExecutionPolicy Bypass -File dev-back.ps1 [dev-back] dotnet: 10.0.301 (esperado 10.x) [dev-back] ASPNETCORE_ENVIRONMENT = Development [dev-back] Backend -> (Swagger: Using launch settings from C:\Users\SAN MARINO\Desktop\App_SanMarino\backend\src\ZooSanMarino.API\Properties\launchSettings.json... Building... C:\Users\SAN MARINO\.dotnet\sdk\10.0.301\Microsoft.Common.CurrentVersion.targets(5096,5): warning MSB3026: Could no»
Registrado desde el trabajo real del área de desarrollo (sesión bd7a9927, 2026-08-07).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-07 09:15:11+00', TIMESTAMPTZ '2026-08-07 09:43:47+00',
           'Qué se hizo (3 commits): chore(dev): dev-back baja el backend previo antes de compilar; Revert "chore(dev): dev-back baja el backend previo antes de compilar"; chore(dev): make dev-back cierra la instancia anterior via dev-kill-back.cmd
Bugs encontrados en el camino: 0.
Evidencia: 3 archivos tocados · 0,5 h de sesión real · commits 9f31dec, 3875462, e44ea0d
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-07 09:43:47+00', v_cedula, false,
           'MEDIA', v_orden, 2.00, DATE '2026-08-07', DATE '2026-08-07',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-07 09:15:11+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260807-bd7a' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260807-bd7a'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260807-bd7a'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260807-3610 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Conciliación del lote K345 (NIZA III): aplicación vs ERP en el traslape levante-producción', '[Bitácora jul-ago 2026] · tarea SES-20260807-3610
Pedido del usuario: «tengo que realizar un analisis de un lote en particular que es el que esta en niza iii que cumplio su etapa de levante y produccion costos valido la aplciacion y comparar con el erp entonces ahroa necesito valdiar y encontrar donde esta la diferencia tan grandes o proceso humanos para contestarle enotnces ya esta actualzia la base de datos con lo que esta en produccion enotnces aqui pego lo que esta en el correo de lo que realizaron enotnces ahora tengo que contestar con por area de desarrollo de la plataforma > Buen dia A continuación relaciono la conciliación   LOTE K345 LVTE A»
Registrado desde el trabajo real del área de desarrollo (sesión 361053cc, 2026-08-07).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-07 09:23:43+00', TIMESTAMPTZ '2026-08-07 10:43:47+00',
           'Qué se hizo (3 commits): docs(conciliacion): analisis lote K345 NIZA III aplicacion vs ERP; feat(reportes): Seleccion y movimiento de huevo en el informe contable; carga masiva de levante a paridad; feat(postura): bloquea el doble conteo cuando un dia se registra en levante y en produccion
Bugs encontrados en el camino: 0.
Evidencia: 23 archivos tocados · 1,3 h de sesión real · commits 69853d3, d299a8a, 3347fbf
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-07 10:43:47+00', v_cedula, false,
           'MEDIA', v_orden, 6.00, DATE '2026-08-07', DATE '2026-08-07',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-07 09:23:43+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260807-3610' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260807-3610'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260807-3610'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260807-8e56 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Reporte Diario Área de Costos para POSTURA (levante + producción) — Sanmarino', '[Bitácora jul-ago 2026] · tarea SES-20260807-8e56
Pedido del usuario: «neceisto crear un reorte area de costos para la empresa de sanmarino colombia , la idea es que los reportes son diarios sobre lote base y los filtros que nos muestra y ese es el dise;o del reporte con sus hojas o tap que nos mostrara entonces validamos informacion con el lote de pruebas que se cargaron masiva mente que son 369B con ese vamos a trabjar ya que lo cargamos desde archivos que son veridicos»
Registrado desde el trabajo real del área de desarrollo (sesión 8e56bd43, 2026-08-07).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-07 10:56:25+00', TIMESTAMPTZ '2026-08-07 12:10:36+00',
           'Qué se hizo (3 commits): @ feat(reportes): Reporte Diario Area de Costos para POSTURA (levante + produccion); chore(tracker): cierra el bloque del alcance de nombre de lote por galpon; docs(postura): manual de carga masiva en Word (17 pag.) + PDF
Bugs encontrados en el camino: 2 — cada uno queda como subtarea BUG de la tarea SES-20260807-8e56.
Evidencia: 31 archivos tocados · 1,2 h de sesión real · commits 3469004, 9ddbbc8, 3ce5360, 92cd918, 8d5565c
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-07 12:10:36+00', v_cedula, false,
           'MEDIA', v_orden, 12.00, DATE '2026-08-07', DATE '2026-08-07',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-07 10:56:25+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260807-8e56' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260807-8e56'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260807-8e56'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260807-9c89 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'El nombre de lote se validaba único por granja cuando es único por GALPÓN', '[Bitácora jul-ago 2026] · tarea SES-20260807-9c89
Pedido del usuario: «tengo este error en ticket de un seguimeinto diario no le deja y la ubicacion es la siuintes > Falla en fecha registro levante semana 6 lote A374A galpón 4 > Descripción El aplicativo no permitió el registro de información de la fecha 22 de noviembre con sus datos. Quedó una fila inconclusa»
Registrado desde el trabajo real del área de desarrollo (sesión 9c898bbe, 2026-08-07).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-07 11:32:15+00', TIMESTAMPTZ '2026-08-07 12:10:22+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea SES-20260807-9c89.
Evidencia: 11 archivos tocados · 0,6 h de sesión real · commits 226a5a4
Estimación: 4 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-07 12:10:22+00', v_cedula, false,
           'ALTA', v_orden, 4.00, DATE '2026-08-07', DATE '2026-08-07',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-07 11:32:15+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260807-9c89' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260807-9c89'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260807-9c89'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260807-893a ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Gastos de inventario (Ecuador): rango de fechas al descargar el Excel', '[Bitácora jul-ago 2026] · tarea SES-20260807-893a
Pedido del usuario: «el modulo de gastos de inventario neceito realziar esta cambio de mejora al moemnto de descargar el excel y ver el resultado en la tabla > ESTIMADO MOISE, SOLICITO SU AYUDA QUE AL MOMENTO DE DESCARGAR PUEDA ELEGIR DE QUE FECHA HASTA QUE FECHA NECESITO EL CONSUMO DE PRODUCTOS, PARA ASI NO TENER QUE BAJAR TODO LOS CONSUMOS REALIZADOS»
Registrado desde el trabajo real del área de desarrollo (sesión 893addf3, 2026-08-07).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-07 12:16:11+00', TIMESTAMPTZ '2026-08-07 12:35:34+00',
           'Qué se hizo (1 commit): ecuador agregar fechas rangos en gastos de invnetario
Bugs encontrados en el camino: 0.
Evidencia: 17 archivos tocados · 0,3 h de sesión real · commits 90f97ad
Estimación: 3 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-07 12:35:34+00', v_cedula, false,
           'MEDIA', v_orden, 3.00, DATE '2026-08-07', DATE '2026-08-07',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-07 12:16:11+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260807-893a' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260807-893a'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260807-893a'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260807-bd43 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Migración manual: se retiran Ventas y Movimientos, permiso propio y tiles por permiso', '[Bitácora jul-ago 2026] · tarea SES-20260807-bd43
Pedido del usuario: «necesito acomodar mas este modulo de migracion manual ya que este modulo ya tenemos reducido en las cargas masivas los archivos , de ventas y traslados se acen desde el seguimeinto diario entonces no tiene que cargar mas informacion entonces quitamos las cagitas de movimeinto y ventas por que eso se realiza en los seguimeintos >»
Registrado desde el trabajo real del área de desarrollo (sesión bd434cce, 2026-08-07).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-07 21:52:14+00', TIMESTAMPTZ '2026-08-07 23:02:47+00',
           'Qué se hizo (2 commits): feat(migraciones): retira los tipos Ventas/Movimiento de Aves/Movimiento de Huevos; feat(migraciones): permiso de postura, tiles por permiso y modulo solo para Sanmarino
Bugs encontrados en el camino: 0.
Evidencia: 21 archivos tocados · 1,2 h de sesión real · commits cbc922c, 07c9c0c
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-07 23:02:47+00', v_cedula, false,
           'MEDIA', v_orden, 6.00, DATE '2026-08-07', DATE '2026-08-07',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-07 21:52:14+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260807-bd43' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260807-bd43'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260807-bd43'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260807-7ad9 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'El Reporte Diario de Costos de POSTURA nunca mostraba el levante', '[Bitácora jul-ago 2026] · tarea SES-20260807-7ad9
Pedido del usuario: «el reprote que se creo para constos de sanmarino Reporte Diario Área de Costos — Postura tiene un error cuando escojo lotes que tiene levante y produccion o levante solamenteo o solo produccion no trae nada como el de la granja niza iii o la que utilizamos de prubas para cargar entonces no esta funcioanndo ya descargue la base de datos de producion actual con los cambios despelgados con ante mano , tmaibne veo un problema que puede ser que un lote base este en barias granjas ya que en niza paso pero no la an movido lo que se ralizo levante en niza iii se pasara a niza i lo que esta en la fase»
Registrado desde el trabajo real del área de desarrollo (sesión 7ad9a6f8, 2026-08-07).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-07 22:04:30+00', TIMESTAMPTZ '2026-08-07 23:08:37+00',
           'Qué se hizo (1 commit): chore(tracker): cierra el bloque del reporte de costos de postura
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea SES-20260807-7ad9.
Evidencia: 18 archivos tocados · 1,1 h de sesión real · commits c6ba60f, 425001e
Estimación: 4 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-07 23:08:37+00', v_cedula, false,
           'ALTA', v_orden, 4.00, DATE '2026-08-07', DATE '2026-08-07',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-07 22:04:30+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260807-7ad9' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260807-7ad9'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260807-7ad9'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260808-8849 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Un lote sin liquidar absorbía el ciclo siguiente del galpón (Ecuador)', '[Bitácora jul-ago 2026] · tarea SES-20260808-8849
Pedido del usuario: «el lote que me reporta ecuador es un lote que ya esta cerrado sin actividad entonces tenemos este problema tamibne me guastaria que pueda validar que solo pueda agregar manual mente los alimento en gestion de einventario del mes actual que se encuentra asi se evita meter meses antes > Buen Dia estimado Moises, me puede ayudar validando el reporte de granja KM 86 lote 01 galpón 1 y 02 tenemos ingreso del mes de julio cuando el lote cerro en abril, adjunto una imagen para su revision»
Registrado desde el trabajo real del área de desarrollo (sesión 8849a5a1, 2026-08-08).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-08 02:00:49+00', TIMESTAMPTZ '2026-08-08 03:06:37+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea SES-20260808-8849.
Evidencia: 28 archivos tocados · 1,1 h de sesión real · commits 7339c61
Estimación: 5 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-08 03:06:37+00', v_cedula, false,
           'ALTA', v_orden, 5.00, DATE '2026-08-08', DATE '2026-08-08',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-08 02:00:49+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260808-8849' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260808-8849'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260808-8849'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260808-9212 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Alimento previo al encaset: fecha real de llegada e ingreso inicial del ciclo visible', '[Bitácora jul-ago 2026] · tarea SES-20260808-9212
Pedido del usuario: «en gestion de invetario y encaetameito de un lote es la forma de como nosotros le asingamos el primer alimento a ese lote que tiene ese galpon especificifco que tiene alimento es decir ahroa tengo un problema que es es ro que actuale mente tengo que decirle a cada persona si el aliemnto llego tres o dos dias antes o una semana antes tiene que realizan el ingreso edl primer dia del consumo para que el reprote lo tome en el seguimeinto dairio y cuadren lso valores entonces como podrai realziar esa parte o organizar ya que esto es tanto para postura y pollo engorde pasa eso , tamibne es por que c»
Registrado desde el trabajo real del área de desarrollo (sesión 92124d1d, 2026-08-08).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-08 02:26:13+00', TIMESTAMPTZ '2026-08-09 07:19:56+00',
           'Qué se hizo (3 commits): feat(inventario,engorde,postura): fecha real de llegada del alimento + ingreso inicial del ciclo visible; docs(tracker): auditoria de cierre del alimento previo al encaset; docs(tracker): la v16 de engorde se intento 3 veces y se revirtio
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea SES-20260808-9212.
Evidencia: 6 archivos tocados · 4,2 h de sesión real · commits 801b14f, 362155c, d6aeccb, 8424557
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-09 07:19:56+00', v_cedula, false,
           'MEDIA', v_orden, 12.00, DATE '2026-08-08', DATE '2026-08-09',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-08 02:26:13+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260808-9212' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260808-9212'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260808-9212'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260809-a721 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'PWA F1-F2 + stock atómico: app instalable, consulta offline y escrituras concurrentes a salvo', '[Bitácora jul-ago 2026] · tarea SES-20260809-a721
Pedido del usuario: «coloca lo faltande en acomodar todo para que sea pwa la app 100% tomando todos los arreglaso posibles que encontres y lo dejes listo al final de la sesion toma los mejores caminos y realiza pruebas en vivo de cada funcionamiento»
Registrado desde el trabajo real del área de desarrollo (sesión a721c8a5, 2026-08-09).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-09 20:16:40+00', TIMESTAMPTZ '2026-08-10 04:06:41+00',
           'Qué se hizo (5 commits): feat(pwa): la app se vuelve una PWA instalable, autoactualizable y con kill switch; feat(pwa): consulta offline - la app deja de quedar vacia sin red; feat(sync): lapidas de borrado + auditoria del estado real de F0.A; docs(f0a): A6 medido y cerrado como no-se-cambia; la colision del plan no existe en los datos; feat(engorde): detector de atribucion de lote — el cuadre es CIEGO a este defecto (A9, paso 1)
Bugs encontrados en el camino: 3 — cada uno queda como subtarea BUG de la tarea SES-20260809-a721.
Evidencia: 68 archivos tocados · 2,6 h de sesión real · commits 8ecb7c6, c55a8e1, 60d3125, f82874e, f70603d, 44b2400, 502ad98, 813e9f5
Estimación: 14 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-10 04:06:41+00', v_cedula, false,
           'MEDIA', v_orden, 14.00, DATE '2026-08-09', DATE '2026-08-10',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-09 20:16:40+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260809-a721' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260809-a721'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260809-a721'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260811-d0bf ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Programación de lotes de engorde para Ecuador + gasto contra lote PROGRAMADO', '[Bitácora jul-ago 2026] · tarea SES-20260811-d0bf
Pedido del usuario: «ahroa necesito crear un un modulo o agregarlo tambine a ecuador la necesidad de lote base que tiene panama donde crean la programacion de lotes y se asignan a una granja para que al momento de crar un lote de pollo engorde aparesca si esta singado a la granja por el que tiene el permiso de este lote progrmacion necesito que valides esa parte para que ecuador tambine lo tenga y la parte del nombre sera ya la que este definidad asi apareceran lotes y dejaran de aparecer tamibne entonces aqui dejo la necesidad de ecuador > Descripción Buenas tardes estimado Moises, solicito su ayuda con un módul»
Registrado desde el trabajo real del área de desarrollo (sesión d0bf32ae, 2026-08-11).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-11 20:58:39+00', TIMESTAMPTZ '2026-08-11 23:14:39+00',
           'Qué se hizo (14 commits): feat(companies): dos flags tipados para la programacion de lotes de engorde; feat(inventario): el gasto puede colgar de un lote PROGRAMADO; feat(engorde): el nombre del lote deja de asumir el sufijo de Panama; feat(inventario): reglas puras del gasto programado y su re-atribucion; feat(inventario): registrar y listar el gasto contra un lote programado; feat(inventario): fn_inventario_gastos_search devuelve el lote programado; feat(engorde): al encasetar, los gastos de la programacion pasan al lote real; chore(db): migraciones de esquema de la programacion de lotes
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea SES-20260811-d0bf.
Evidencia: 22 archivos tocados · 2,3 h de sesión real · commits 27f1348, 495d7c4, 252015b, 3682e63, d766a84, 8ebede6, 118ea8d, 067453e, 3232254, ed055fa
Estimación: 14 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-11 23:14:39+00', v_cedula, false,
           'MEDIA', v_orden, 14.00, DATE '2026-08-11', DATE '2026-08-11',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-11 20:58:39+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260811-d0bf' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260811-d0bf'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260811-d0bf'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260811-cbb2 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'EN_ANALISIS',
           'Auditoría de impacto de la columna mixto en los reportes de Panamá', '[Bitácora jul-ago 2026] · tarea SES-20260811-cbb2
Pedido del usuario: «realiza los archivos word de estas sin pdf , de aceurdo a cada formato es un trabajo que sera a mano pero me daras todo el disno completo de como esta , dame los word en el escritorio»
Registrado desde el trabajo real del área de desarrollo (sesión cbb290ec, 2026-08-11).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-11 22:43:54+00', NULL,
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 1,8 h de sesión real
Estimación: 4 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)
Estado real: quedó en análisis, no se cerró.', NULL, NULL, false,
           'MEDIA', v_orden, 4.00, DATE '2026-08-11', DATE '2026-08-12',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-11 22:43:54+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260811-cbb2' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260811-cbb2'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260811-cbb2'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260811-7a82 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'El alimento mixto de engorde deja de contarse como consumo de hembras (app y Power BI)', '[Bitácora jul-ago 2026] · tarea SES-20260811-7a82
Pedido del usuario: «en el modulo de seguimiento diario pollo engorde , en el alimento e los 7 dias si debe aparecer por genero en el seguimiento diario pero cuando ya se cumple los 7 dias que se realiza desde este modulo debemos tener un campo nuevo que se llame alimento mixto asi no se convinan ya que actua mente muestra que esta en hembras entonces por medio de la informacion se confunde visual mente esto es mas un cambio visual y del excel al momento de descargar , te coloco el archivo excel Consumo hembras (kg)»
Registrado desde el trabajo real del área de desarrollo (sesión 7a82bef9, 2026-08-11).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-11 23:30:05+00', TIMESTAMPTZ '2026-08-12 02:33:45+00',
           'Qué se hizo (4 commits): feat(engorde): el alimento mixto deja de contarse como consumo de hembras; docs(engorde): auditoria de impacto de la columna mixto en los reportes de Panama; feat(powerbi): el consumo mixto de engorde deja de publicarse como consumo de hembras; docs(powerbi): el espejo SQL de la vista de engorde vuelve a ser fiel a lo desplegado
Bugs encontrados en el camino: 0.
Evidencia: 12 archivos tocados · 1,0 h de sesión real · commits dd85c51, 694836b, 2f2a00a, 156a0d1
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-12 02:33:45+00', v_cedula, false,
           'MEDIA', v_orden, 6.00, DATE '2026-08-11', DATE '2026-08-12',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-11 23:30:05+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260811-7a82' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260811-7a82'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260811-7a82'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260812-93b9 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Permisos por empresa: cada empresa define qué permisos existen para ella', '[Bitácora jul-ago 2026] · tarea SES-20260812-93b9
Pedido del usuario: «ahroa que estoy valdiando queiro saber si tengo una migracion que crea los permiso para el modulo de migracion manual para postura por que no lo veo tmaibne me gustaria definir los permisos que podran ver por empresa ya que hay permisos de modulos que no se utilizan digamos en las empresas de ecuador y panama que colombia no lo tiene y viseversas deberia tener ese parametro en el modulo de empresa asi como especifico el menu tamibne los persmiso que debe tener esa empresa en particular , y asi el modulo de permisos al crear el rol depende de lo que selecione que puedan ver»
Registrado desde el trabajo real del área de desarrollo (sesión 93b94a8b, 2026-08-12).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-12 02:35:23+00', TIMESTAMPTZ '2026-08-12 04:26:18+00',
           'Qué se hizo (5 commits): docs(tracker): commit del gate del borde marcado; feat(permisos): cada empresa define qué permisos existen para ella; feat(permisos): el backend también rechaza el permiso que la empresa no habilita; docs(tracker): el backend queda arriba a proposito para la validacion; docs(tracker): validacion de F3.1 con los dos perfiles de operario reales
Bugs encontrados en el camino: 0.
Evidencia: 46 archivos tocados · 1,9 h de sesión real · commits 3407cb2, cf9ed0f, 3e0c2a3, f01a165, 574cfb0
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-12 04:26:18+00', v_cedula, false,
           'MEDIA', v_orden, 12.00, DATE '2026-08-12', DATE '2026-08-12',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-12 02:35:23+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260812-93b9' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260812-93b9'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260812-93b9'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260812-f5d0 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'El gate del borde del front exigía que la PWA no existiera y tumbaba el deploy', '[Bitácora jul-ago 2026] · tarea SES-20260812-f5d0
Pedido del usuario: «valida porque al momento de realizar un despliegue el frot genero error en el git aqui dejo el archivo zip del log del front que genero error , con eso validar el error y solucioanrlo»
Registrado desde el trabajo real del área de desarrollo (sesión f5d064a6, 2026-08-12).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-12 02:36:38+00', TIMESTAMPTZ '2026-08-12 02:53:08+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea SES-20260812-f5d0.
Evidencia: 4 archivos tocados · 0,3 h de sesión real · commits 6f410db
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-12 02:53:08+00', v_cedula, false,
           'ALTA', v_orden, 2.00, DATE '2026-08-12', DATE '2026-08-12',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-12 02:36:38+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260812-f5d0' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260812-f5d0'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260812-f5d0'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260812-0d35 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'PWA F3: captura offline con idempotencia real (postura, engorde y reproductora)', '[Bitácora jul-ago 2026] · tarea SES-20260812-0d35
Pedido del usuario: «para lo de pwa que faltaria mas»
Registrado desde el trabajo real del área de desarrollo (sesión 0d35be4e, 2026-08-12).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-12 04:27:20+00', TIMESTAMPTZ '2026-08-12 08:04:08+00',
           'Qué se hizo (8 commits): feat(pwa): captura offline con idempotencia real (F3.1); feat(pwa): el operario ya sabe donde quedo lo que capturo sin red; feat(pwa): captura offline tambien en produccion (F3.2); feat(pwa): captura offline de engorde, pollo y reproductora (F3.3); docs(tracker): la reproductora de pollo engorde es modulo exclusivo de Panama; docs(pwa): auditoria de acceso offline — menu muerto, primer ingreso y acciones operativas; docs(tracker): punto de retoma de la PWA para continuar en otra sesion; feat(menus): el menu de reproductora de postura queda definido pero sin asignar, con la etiqueta corregida
Bugs encontrados en el camino: 0.
Evidencia: 46 archivos tocados · 3,6 h de sesión real · commits c44e0a4, de3ea10, b681a50, 505c13b, b56459c, 30c6865, 88f1d3d, 6980fa3
Estimación: 14 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-12 08:04:08+00', v_cedula, false,
           'MEDIA', v_orden, 14.00, DATE '2026-08-12', DATE '2026-08-12',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-12 04:27:20+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260812-0d35' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260812-0d35'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260812-0d35'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260812-2d5d ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Estado medido de la PWA y brecha real para salir a producción', '[Bitácora jul-ago 2026] · tarea SES-20260812-2d5d
Pedido del usuario: «validemso lo que tenemos en el pwa a y lo que falta por terminar y que pueda salir a funcionar»
Registrado desde el trabajo real del área de desarrollo (sesión 2d5d63ea, 2026-08-12).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-12 09:20:57+00', TIMESTAMPTZ '2026-08-12 10:11:56+00',
           'Qué se hizo (2 commits): docs(pwa): validacion medida del estado y la brecha real para salir a produccion; feat(sql): invariante que prueba que company_permissions no dejo a nadie sin acceso
Bugs encontrados en el camino: 0.
Evidencia: 10 archivos tocados · 0,8 h de sesión real · commits 71836ff, 8f1cb56
Estimación: 4 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-12 10:11:56+00', v_cedula, false,
           'MEDIA', v_orden, 4.00, DATE '2026-08-12', DATE '2026-08-12',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-12 09:20:57+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260812-2d5d' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260812-2d5d'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260812-2d5d'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260812-a3c4 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'La recuperación de contraseña estaba cortada: el correo imprimía el token como contraseña', '[Bitácora jul-ago 2026] · tarea SES-20260812-a3c4
Pedido del usuario: «realiza pruebas locales sin modificar nada solo enocntrar que funcione el envio de correos eletronicos de la aplicacion ya que anterior mente se habia realizado algo cuando en pruebas controladas estaba solucioando solo cambiarle el protocolo que tenia a tls para que funcionara el envio de correo es que neceisto la opcio nde recuperacion de contrase;as»
Registrado desde el trabajo real del área de desarrollo (sesión a3c49b65, 2026-08-12).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-12 11:10:40+00', TIMESTAMPTZ '2026-08-12 19:54:44+00',
           'Qué se hizo (2 commits): feat(correos): la recuperacion de contraseña estaba cortada, no solo el SMTP; feat(correos): el encabezado pasa a ser el de la pantalla de ingreso
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea SES-20260812-a3c4.
Evidencia: 38 archivos tocados · 2,5 h de sesión real · commits dcba98f, 29dfdfd, 565164a
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-12 19:54:44+00', v_cedula, false,
           'ALTA', v_orden, 8.00, DATE '2026-08-12', DATE '2026-08-12',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-12 11:10:40+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260812-a3c4' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260812-a3c4'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260812-a3c4'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260812-484c ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Santa Reyes — Silos y bodegas como ubicación real del inventario (plan + Fase A)', '[Bitácora jul-ago 2026] · tarea SES-20260812-484c
Pedido del usuario: «para la empresa santa reyes de colombia necesito plantiar muy bien este plan de trabajo ya que modificara modulos existente para acomodarlos a ellos por ahroa es todo lo que es postura con este cambio desde la gestion de granja creemos el plam bien mirando bien detallado los serviios y lugares que se veran afectados funcione tmaibn de base dedatos y servicios en el back y front para acomodar para esta empresa > Para la empresa Santa Reyes va a haber un cambio lógico. Hay que mirar cómo se estructura para ellos, porque ellos manejan unas cosas que se llaman silos. Y la idea de los silos es que»
Registrado desde el trabajo real del área de desarrollo (sesión 484c317f, 2026-08-12).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-12 21:51:40+00', TIMESTAMPTZ '2026-08-12 23:59:53+00',
           'Qué se hizo (2 commits): docs(santa-reyes): plan de silos y bodegas como ubicacion real del inventario; feat(silos): la granja, el galpon y el lote declaran sus silos (Santa Reyes, Fase A)
Bugs encontrados en el camino: 0.
Evidencia: 63 archivos tocados · 2,1 h de sesión real · commits 503d5a3, 7f43581
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-12 23:59:53+00', v_cedula, false,
           'MEDIA', v_orden, 10.00, DATE '2026-08-12', DATE '2026-08-12',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-12 21:51:40+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260812-484c' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260812-484c'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260812-484c'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260813-c487 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Silos Fase B: silo_id en el stock y swap del índice único acoplado al ON CONFLICT', '[Bitácora jul-ago 2026] · tarea SES-20260813-c487
Pedido del usuario: «La Fase B es la del riesgo: ahí va el silo_id en inventario_gestion_stock y el swap del índice único, que va acoplado al ON CONFLICT de SumarStockAtomicoAsync — desalineados, revienta todo ingreso de todas las empresas. Empieza con el smoke de regresión en Sanmarino y Ecuador antes de tocar nada de Santa Reyes. ¿Sigo con la Fase B?»
Registrado desde el trabajo real del área de desarrollo (sesión c487cccd, 2026-08-13).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-13 00:17:15+00', TIMESTAMPTZ '2026-08-13 02:21:04+00',
           'Qué se hizo (2 commits): feat(silos): el saldo aprende a vivir en un silo, sin mover el de nadie (Santa Reyes, Fase B parcial); feat(silos): el movimiento ya sabe en que silo cae (Santa Reyes, Fase B backend)
Bugs encontrados en el camino: 0.
Evidencia: 19 archivos tocados · 1,4 h de sesión real · commits a15c7ac, 5f2fa35
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-13 02:21:04+00', v_cedula, false,
           'MEDIA', v_orden, 10.00, DATE '2026-08-13', DATE '2026-08-13',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-13 00:17:15+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260813-c487' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260813-c487'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260813-c487'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260813-35a8 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Silos Fase B (front): el operario elige en qué silo cae el alimento', '[Bitácora jul-ago 2026] · tarea SES-20260813-35a8
Pedido del usuario: «Lo que falta La pantalla 5 del front (/gestion-inventario: selector de silo en ingreso y traslado, columna Silo en las grillas, recepción por silo, export) y tres lecturas del backend que todavía no proyectan el silo: GetIngresosAsync, GetTrasladosAsync y GetFilterDataAsync. El tracker lo refleja línea por línea. ¿Sigo con eso?»
Registrado desde el trabajo real del área de desarrollo (sesión 35a89cd4, 2026-08-13).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-13 04:23:44+00', TIMESTAMPTZ '2026-08-13 05:22:37+00',
           'Qué se hizo (1 commit): feat(silos): el operario ya puede decir en que silo cae el alimento (Santa Reyes, Fase B cerrada)
Bugs encontrados en el camino: 0.
Evidencia: 12 archivos tocados · 1,0 h de sesión real · commits 72b4bf2
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-13 05:22:37+00', v_cedula, false,
           'MEDIA', v_orden, 8.00, DATE '2026-08-13', DATE '2026-08-13',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-13 04:23:44+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260813-35a8' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260813-35a8'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260813-35a8'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260813-b66e ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Silos Fase C: el consumo diario y los gastos dicen de qué silo salen', '[Bitácora jul-ago 2026] · tarea SES-20260813-b66e
Pedido del usuario: «Queda la Fase C: consumo por silo desde el seguimiento diario (ItemConsumoKey con siloId, ColombiaInventarioConsumoService, pantallas 6-7). ¿Sigo con eso?»
Registrado desde el trabajo real del área de desarrollo (sesión b66ee068, 2026-08-13).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-13 06:31:07+00', TIMESTAMPTZ '2026-08-14 00:40:03+00',
           'Qué se hizo (3 commits): feat(silos): el consumo diario ya dice de que silo sale (Santa Reyes, Fase C); feat(silos): Gastos por silo y los reportes leen el alimento donde la empresa lo tiene; feat(reportes): Sanmarino tambien lee el alimento del inventario unificado
Bugs encontrados en el camino: 0.
Evidencia: 27 archivos tocados · 1,6 h de sesión real · commits c5f67fa, 22a0ac3, ab6e97d
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-14 00:40:03+00', v_cedula, false,
           'MEDIA', v_orden, 10.00, DATE '2026-08-13', DATE '2026-08-14',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-13 06:31:07+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260813-b66e' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260813-b66e'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260813-b66e'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260813-7866 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Con el flag de silos puesto, el consumo no encontraba su propio ítem (Santa Reyes)', '[Bitácora jul-ago 2026] · tarea SES-20260813-7866
Pedido del usuario: «realiza la prueba y luego sigue con el de > Lo que quedó afuera: el smoke de Santa Reyes (casos 18-24) y el caso 23. No es un bloqueo de código: la BD local no tiene ningún lote de SR (lotes de granja 109 = 0, lote_silos vacía — el smoke de la Fase B se restauró). Para correrlo hay que fabricar antes núcleo + galpón + lote + lote_postura_levante + lote_silos + un ingreso al silo. ¿Armo ese fixture y corro los casos ON, o seguimos con la Fase D?»
Registrado desde el trabajo real del área de desarrollo (sesión 7866b0a5, 2026-08-13).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-13 11:36:49+00', TIMESTAMPTZ '2026-08-13 16:35:39+00',
           'Qué se hizo (1 commit): docs(silos): el smoke ahora cubre el ciclo completo de produccion, no solo el alta
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea SES-20260813-7866.
Evidencia: 7 archivos tocados · 0,6 h de sesión real · commits 86111b6, 803f170
Estimación: 4 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-13 16:35:39+00', v_cedula, false,
           'ALTA', v_orden, 4.00, DATE '2026-08-13', DATE '2026-08-13',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-13 11:36:49+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260813-7866' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260813-7866'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260813-7866'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260813-de48 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Silos Fase D: el reporte de existencias repetía el ítem una vez por silo', '[Bitácora jul-ago 2026] · tarea SES-20260813-de48
Pedido del usuario: «Sigo con la Fase D — empezando por fn_inventario_gastos_existencias, que hoy asume una fila de stock por granja+ítem y con N silos multiplicaría filas.»
Registrado desde el trabajo real del área de desarrollo (sesión de48b9bb, 2026-08-13).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-13 17:10:48+00', TIMESTAMPTZ '2026-08-13 20:06:28+00',
           'Qué se hizo (3 commits): docs(silos): la carga masiva y los reportes no necesitan silo, pero los reportes leen la tabla vieja; docs(silos): las dos precondiciones de prod que fallarian en silencio al desplegar; chore(silos): chequeo de go-live de Santa Reyes contra datos reales de produccion
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea SES-20260813-de48.
Evidencia: 5 archivos tocados · 0,7 h de sesión real · commits 6e3b167, b546c06, 0529bec, 584394e
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-13 20:06:28+00', v_cedula, false,
           'ALTA', v_orden, 6.00, DATE '2026-08-13', DATE '2026-08-13',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-13 17:10:48+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260813-de48' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260813-de48'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260813-de48'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260813-bb55 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'Gerencia — Panel de control de ItalJira en solo lectura (permiso tickets.indicadores)', '[Bitácora jul-ago 2026] · tarea SES-20260813-bb55
Pedido del usuario: «en los modulos que tengo de italjira puedo darle permisos aun rol en especifico para que pueda ver solo ese item delmenu y darle permiso algunos iten internos que solo sea el de Panel de control ya que lo quiero agregar a un modulo particular que seria el de gerencia pero si se uede queiro validar ya que le tenai unas reglas que solo el rol admin podra verlo»
Registrado desde el trabajo real del área de desarrollo (sesión bb55e00f, 2026-08-13).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-13 17:14:24+00', TIMESTAMPTZ '2026-08-13 18:22:12+00',
           'Qué se hizo (1 commit): feat(gerencia): el gerente ya puede ver los indicadores sin poder tocar nada
Bugs encontrados en el camino: 0.
Evidencia: 23 archivos tocados · 1,1 h de sesión real · commits c1ed6e3
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-13 18:22:12+00', v_cedula, false,
           'MEDIA', v_orden, 8.00, DATE '2026-08-13', DATE '2026-08-13',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-13 17:14:24+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260813-bb55' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260813-bb55'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260813-bb55'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260813-7218 ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'CERRADO',
           'La copia descargable de DB Studio dejaba 4 funciones sin crear al restaurar', '[Bitácora jul-ago 2026] · tarea SES-20260813-7218
Pedido del usuario: «estoy cargando la bse de datos de produccion a local eliminando la base de atos para cargarla desde limpio y me sale este error que biene de produccion pero en produccion no falla > ERROR: function fn_seguimiento_diario_engorde(integer) does not exist LINE 100794: FROM fn_seguimiento_diario_engorde(p_lote_id) f ^ HINT: No function matches the given name and argument types. You might need to add explicit type casts. SQL state: 42883 Character: 24159793»
Registrado desde el trabajo real del área de desarrollo (sesión 72180ffd, 2026-08-13).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-13 20:17:44+00', TIMESTAMPTZ '2026-08-13 21:27:34+00',
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 1 — cada uno queda como subtarea BUG de la tarea SES-20260813-7218.
Evidencia: 6 archivos tocados · 1,2 h de sesión real · commits 9e9e24a
Estimación: 4 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', TIMESTAMPTZ '2026-08-13 21:27:34+00', v_cedula, false,
           'ALTA', v_orden, 4.00, DATE '2026-08-13', DATE '2026-08-13',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-13 20:17:44+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260813-7218' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260813-7218'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260813-7218'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;

    -- ── SES-20260814-880f ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', 'EN_ANALISIS',
           'Bitácora ItalJira de julio y agosto 2026: horas, solución y bugs por sesión', '[Bitácora jul-ago 2026] · tarea SES-20260814-880f
Pedido del usuario: «quiero crear una migracion con todas la tareas y historias que se ah venido realizando en los ticket co ntiempos estimados y en el la fase de solcuion por que se ah solucionado y errores bug que se ah encontrado tamibne de acuerdo a todas la sesiones de este mes y el anterior»
Registrado desde el trabajo real del área de desarrollo (sesión 880f7278, 2026-08-14).',
           v_user_guid, v_user_guid, TIMESTAMPTZ '2026-08-14 00:46:29+00', NULL,
           'Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados en el camino: 0.
Evidencia: 7 archivos tocados · 0,3 h de sesión real
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)
Estado real: quedó en análisis, no se cerró.', NULL, NULL, false,
           'MEDIA', v_orden, 8.00, DATE '2026-08-14', DATE '2026-08-14',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '2026-08-14 00:46:29+00'
    FROM public.ticket_tareas t
    WHERE t.codigo = 'SES-20260814-880f' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = 'SES-20260814-880f'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = 'SES-20260814-880f'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;
    RETURN QUERY
    SELECT 'casos creados por la bitácora'::text, count(*)
      FROM public.tickets WHERE descripcion LIKE '[Bitácora jul-ago 2026]%'
    UNION ALL SELECT 'de ellos, CERRADO'::text, count(*)
      FROM public.tickets WHERE descripcion LIKE '[Bitácora jul-ago 2026]%' AND estado = 'CERRADO'
    UNION ALL SELECT 'de ellos, EN_ANALISIS'::text, count(*)
      FROM public.tickets WHERE descripcion LIKE '[Bitácora jul-ago 2026]%' AND estado = 'EN_ANALISIS'
    UNION ALL SELECT 'con descripción de la solución'::text, count(*)
      FROM public.tickets WHERE descripcion LIKE '[Bitácora jul-ago 2026]%'
                            AND solucion_descripcion IS NOT NULL
    UNION ALL SELECT 'tareas/bugs enlazados a un caso'::text, count(*)
      FROM public.ticket_tareas WHERE ticket_id IS NOT NULL
    UNION ALL SELECT 'correos enviados (debe ser 0)'::text, count(*)
      FROM public.tickets WHERE descripcion LIKE '[Bitácora jul-ago 2026]%' AND notificado_correo;

END
$fn$;
