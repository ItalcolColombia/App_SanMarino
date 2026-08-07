using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Cuerpo del seed histórico de ItalJira. Vive en su propio archivo (partial) porque son ~1.900
    /// líneas de SQL GENERADAS a partir del historial real del repositorio: separarlo deja legible
    /// el archivo de la migración, que es donde está la documentación de qué hace y por qué.
    /// </summary>
    /// <remarks>
    /// No editar a mano: se regenera desde los planes de <c>fase_de_desarrollo/</c> y sus fechas de
    /// git. Si hay que corregir un título o una fecha, corregir el plan y regenerar.
    /// </remarks>
    public partial class SeedHistorialDesarrolloItalJira
    {
        private const string SEED_SQL = @"-- ─────────────────────────────────────────────────────────────────────────────
-- Histórico REAL del desarrollo de la aplicación (lo que nunca pasó por un ticket).
-- Fuente: los planes de fase_de_desarrollo/ y sus fechas de git — alta del archivo
-- (primer commit que lo agregó) y último toque. Generado, no inventado.
-- Todo queda CERRADO (LISTO) y asignado a moiesbbuga@gmail.com, que fue quien lo hizo.
-- ─────────────────────────────────────────────────────────────────────────────
DO $$
DECLARE
    v_user_guid uuid;
    v_cedula    integer;
    v_company   integer;
    v_pais      integer;
    v_hist      bigint;
BEGIN
    -- Identidad POR EMAIL, nunca por guid fijo: los ids difieren entre local y producción.
    SELECT u.id
      INTO v_user_guid
    FROM public.users u
    JOIN public.user_logins ul ON ul.user_id = u.id
    JOIN public.logins l       ON l.id = ul.login_id
    WHERE lower(l.email) = 'moiesbbuga@gmail.com'
    LIMIT 1;

    -- Fail-open silencioso: si el usuario todavía no existe en este entorno, no se siembra nada
    -- y la migración NO tumba el arranque. Se vuelve a correr cuando exista.
    IF v_user_guid IS NULL THEN
        RAISE NOTICE 'ItalJira: no existe moiesbbuga@gmail.com en este entorno; histórico omitido.';
        RETURN;
    END IF;

    -- El int de auditoría del módulo NO es siempre la cédula: la de este usuario (3177120174) no
    -- entra en un integer. El valor correcto es el que ya usan sus propios tickets.
    SELECT t.created_by_user_id INTO v_cedula
    FROM public.tickets t
    WHERE t.created_by_user_guid = v_user_guid
    ORDER BY t.id DESC LIMIT 1;

    IF v_cedula IS NULL THEN
        SELECT CASE WHEN u.cedula ~ '^[0-9]{1,9}$' THEN u.cedula::integer ELSE 0 END
          INTO v_cedula
        FROM public.users u WHERE u.id = v_user_guid;
    END IF;
    v_cedula := COALESCE(v_cedula, 0);

    -- Empresa y país: los del módulo de tickets si ya hay casos; si no, los primeros activos.
    SELECT t.company_id, t.pais_id INTO v_company, v_pais
    FROM public.tickets t ORDER BY t.id DESC LIMIT 1;

    IF v_company IS NULL THEN
        SELECT c.id INTO v_company FROM public.companies c ORDER BY c.id LIMIT 1;
    END IF;
    v_company := COALESCE(v_company, 1);
    v_pais    := COALESCE(v_pais, 1);

    -- ═══ Tickets y soporte (12 trabajos) ═══
    INSERT INTO public.historias (codigo, pais_id, titulo, descripcion, estado, prioridad,
        responsable_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT 'HIS-2026-0001', v_pais, 'Tickets y soporte', 'Historia derivada del historial real del repositorio: agrupa los trabajos con plan propio en fase_de_desarrollo/ correspondientes a este módulo.', 'LISTO', 'MEDIA',
           v_user_guid, 0, DATE '2026-06-04', DATE '2026-08-06',
           TIMESTAMPTZ '2026-06-04 12:00:00+00', TIMESTAMPTZ '2026-08-06 12:00:00+00', 'tickets,soporte', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-04 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.historias h WHERE h.codigo = 'HIS-2026-0001');

    SELECT id INTO v_hist FROM public.historias WHERE codigo = 'HIS-2026-0001';

    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0001-T1', 'TAREA', 'LISTO', 'MEDIA',
           '14 — Módulo: Sistema Centralizado de Tickets de Soporte y Requerimientos', 'Plan: fase_de_desarrollo/14_modulo_tickets_soporte_plan.md', v_user_guid, 0, DATE '2026-06-04', DATE '2026-06-04',
           TIMESTAMPTZ '2026-06-04 12:00:00+00', TIMESTAMPTZ '2026-06-04 18:00:00+00', 'tickets,soporte', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-04 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = '14 — Módulo: Sistema Centralizado de Tickets de Soporte y Requerimientos');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0001-T2', 'TAREA', 'LISTO', 'MEDIA',
           '15 — Tickets: Rediseño UX (pro/responsive) + Adjuntos + Código de Gestión', 'Plan: fase_de_desarrollo/15_tickets_ux_redesign_y_features.md', v_user_guid, 1, DATE '2026-06-05', DATE '2026-06-05',
           TIMESTAMPTZ '2026-06-05 12:00:00+00', TIMESTAMPTZ '2026-06-05 18:00:00+00', 'tickets,soporte', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-05 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = '15 — Tickets: Rediseño UX (pro/responsive) + Adjuntos + Código de Gestión');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0001-T3', 'TAREA', 'LISTO', 'MEDIA',
           '16 — Tickets: Perfiles de atención, asignación por país/tipo y niveles de solicitante', 'Plan: fase_de_desarrollo/16_tickets_asignacion_perfiles_niveles_plan.md', v_user_guid, 2, DATE '2026-06-05', DATE '2026-06-05',
           TIMESTAMPTZ '2026-06-05 12:00:00+00', TIMESTAMPTZ '2026-06-05 18:00:00+00', 'tickets,soporte', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-05 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = '16 — Tickets: Perfiles de atención, asignación por país/tipo y niveles de solicitante');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0001-T4', 'BUG', 'LISTO', 'MEDIA',
           'Plan: Fix Tickets — Cross-Company Resolutores + Nivel + Bandeja Asignados', 'Plan: fase_de_desarrollo/tickets_fix_cross_company_resolutores.md', v_user_guid, 3, DATE '2026-06-05', DATE '2026-06-05',
           TIMESTAMPTZ '2026-06-05 12:00:00+00', TIMESTAMPTZ '2026-06-05 18:00:00+00', 'tickets,soporte', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-05 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan: Fix Tickets — Cross-Company Resolutores + Nivel + Bandeja Asignados');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0001-T5', 'TAREA', 'LISTO', 'MEDIA',
           'Plan: Tickets — Cierre doble confirmación + Adjuntos + Correo + Gestión segregada', 'Plan: fase_de_desarrollo/tickets_cierre_doble_adjuntos_correo.md', v_user_guid, 4, DATE '2026-06-05', DATE '2026-06-05',
           TIMESTAMPTZ '2026-06-05 12:00:00+00', TIMESTAMPTZ '2026-06-05 18:00:00+00', 'tickets,soporte', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-05 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan: Tickets — Cierre doble confirmación + Adjuntos + Correo + Gestión segregada');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0001-T6', 'TAREA', 'LISTO', 'MEDIA',
           '17 — Tickets: reorganizar ""quién crea"" vs ""quién recibe"" (solicitante vs resolutor)', 'Plan: fase_de_desarrollo/17_tickets_reorg_solicitante_resolutor_plan.md', v_user_guid, 5, DATE '2026-06-23', DATE '2026-06-23',
           TIMESTAMPTZ '2026-06-23 12:00:00+00', TIMESTAMPTZ '2026-06-23 18:00:00+00', 'tickets,soporte', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-23 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = '17 — Tickets: reorganizar ""quién crea"" vs ""quién recibe"" (solicitante vs resolutor)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0001-T7', 'BUG', 'LISTO', 'MEDIA',
           'Plan: Fix — El solicitante (aunque sea admin) no gestiona su propio ticket', 'Plan: fase_de_desarrollo/tickets_fix_solicitante_no_gestiona_propio_plan.md', v_user_guid, 6, DATE '2026-06-25', DATE '2026-06-25',
           TIMESTAMPTZ '2026-06-25 12:00:00+00', TIMESTAMPTZ '2026-06-25 18:00:00+00', 'tickets,soporte', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-25 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan: Fix — El solicitante (aunque sea admin) no gestiona su propio ticket');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0001-T8', 'BUG', 'LISTO', 'MEDIA',
           'Plan: Fix — Resolutor por ROL global/cross-company al crear ticket', 'Plan: fase_de_desarrollo/tickets_fix_resolutor_rol_global_cross_company_plan.md', v_user_guid, 7, DATE '2026-06-25', DATE '2026-06-25',
           TIMESTAMPTZ '2026-06-25 12:00:00+00', TIMESTAMPTZ '2026-06-25 18:00:00+00', 'tickets,soporte', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-25 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan: Fix — Resolutor por ROL global/cross-company al crear ticket');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0001-T9', 'BUG', 'LISTO', 'MEDIA',
           'Fix — WAF bloquea rutas `/admin` en módulo Tickets (403 en transferencia)', 'Plan: fase_de_desarrollo/fix_waf_tickets_admin_route_plan.md', v_user_guid, 8, DATE '2026-06-30', DATE '2026-06-30',
           TIMESTAMPTZ '2026-06-30 12:00:00+00', TIMESTAMPTZ '2026-06-30 18:00:00+00', 'tickets,soporte', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-30 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Fix — WAF bloquea rutas `/admin` en módulo Tickets (403 en transferencia)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0001-T10', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Tickets: notificados + notificaciones por correo (creación/cierre) + transferir + correos ""pro"" con logo', 'Plan: fase_de_desarrollo/tickets_notificados_flujos_plan.md', v_user_guid, 9, DATE '2026-07-01', DATE '2026-07-01',
           TIMESTAMPTZ '2026-07-01 12:00:00+00', TIMESTAMPTZ '2026-07-01 18:00:00+00', 'tickets,soporte', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-01 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Tickets: notificados + notificaciones por correo (creación/cierre) + transferir + correos ""pro"" con logo');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0001-T11', 'TAREA', 'LISTO', 'MEDIA',
           'SoporteBot — Loop de soporte automatizado sobre el módulo de Tickets', 'Plan: fase_de_desarrollo/soporte_bot_loop_tickets_plan.md', v_user_guid, 10, DATE '2026-07-04', DATE '2026-07-04',
           TIMESTAMPTZ '2026-07-04 12:00:00+00', TIMESTAMPTZ '2026-07-04 18:00:00+00', 'tickets,soporte', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-04 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'SoporteBot — Loop de soporte automatizado sobre el módulo de Tickets');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0001-T12', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Tickets como CASOS tipo Jira: tareas, tablero, tiempos y solicitante delegado', 'Plan: fase_de_desarrollo/18_tickets_jira_casos_tareas_tablero_plan.md', v_user_guid, 11, DATE '2026-08-06', DATE '2026-08-06',
           TIMESTAMPTZ '2026-08-06 12:00:00+00', TIMESTAMPTZ '2026-08-06 18:00:00+00', 'tickets,soporte', v_company, v_cedula,
           TIMESTAMPTZ '2026-08-06 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Tickets como CASOS tipo Jira: tareas, tablero, tiempos y solicitante delegado');

    -- ═══ Implementación y entrega por empresa (3 trabajos) ═══
    INSERT INTO public.historias (codigo, pais_id, titulo, descripcion, estado, prioridad,
        responsable_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT 'HIS-2026-0002', v_pais, 'Implementación y entrega por empresa', 'Historia derivada del historial real del repositorio: agrupa los trabajos con plan propio en fase_de_desarrollo/ correspondientes a este módulo.', 'LISTO', 'MEDIA',
           v_user_guid, 1, DATE '2026-07-20', DATE '2026-07-25',
           TIMESTAMPTZ '2026-07-20 12:00:00+00', TIMESTAMPTZ '2026-07-25 12:00:00+00', 'implementacion', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-20 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.historias h WHERE h.codigo = 'HIS-2026-0002');

    SELECT id INTO v_hist FROM public.historias WHERE codigo = 'HIS-2026-0002';

    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0002-T1', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Módulo ""Implementación"" (cronogramas de entrega por empresa con checklist confirmable)', 'Plan: fase_de_desarrollo/modulo_implementacion_plan.md', v_user_guid, 0, DATE '2026-07-20', DATE '2026-07-20',
           TIMESTAMPTZ '2026-07-20 12:00:00+00', TIMESTAMPTZ '2026-07-20 18:00:00+00', 'implementacion', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-20 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Módulo ""Implementación"" (cronogramas de entrega por empresa con checklist confirmable)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0002-T2', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Módulo Implementación (checklists) v2: rediseño + firmas de participantes', 'Plan: fase_de_desarrollo/implementacion_checklist_v2_plan.md', v_user_guid, 1, DATE '2026-07-21', DATE '2026-07-21',
           TIMESTAMPTZ '2026-07-21 12:00:00+00', TIMESTAMPTZ '2026-07-21 18:00:00+00', 'implementacion', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-21 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Módulo Implementación (checklists) v2: rediseño + firmas de participantes');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0002-T3', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Implementación empresa SANTA REYES (Colombia, postura comercial)', 'Plan: fase_de_desarrollo/santa_reyes_implementacion_plan.md', v_user_guid, 2, DATE '2026-07-25', DATE '2026-07-25',
           TIMESTAMPTZ '2026-07-25 12:00:00+00', TIMESTAMPTZ '2026-07-25 18:00:00+00', 'implementacion', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-25 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Implementación empresa SANTA REYES (Colombia, postura comercial)');

    -- ═══ Vacunación (3 trabajos) ═══
    INSERT INTO public.historias (codigo, pais_id, titulo, descripcion, estado, prioridad,
        responsable_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT 'HIS-2026-0003', v_pais, 'Vacunación', 'Historia derivada del historial real del repositorio: agrupa los trabajos con plan propio en fase_de_desarrollo/ correspondientes a este módulo.', 'LISTO', 'MEDIA',
           v_user_guid, 2, DATE '2026-07-14', DATE '2026-07-16',
           TIMESTAMPTZ '2026-07-14 12:00:00+00', TIMESTAMPTZ '2026-07-16 12:00:00+00', 'vacunacion', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-14 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.historias h WHERE h.codigo = 'HIS-2026-0003');

    SELECT id INTO v_hist FROM public.historias WHERE codigo = 'HIS-2026-0003';

    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0003-T1', 'TAREA', 'LISTO', 'MEDIA',
           'Módulo Vacunación — cronogramas por lote/granja/galpón', 'Plan: fase_de_desarrollo/vacunacion_cronograma_plan.md', v_user_guid, 0, DATE '2026-07-14', DATE '2026-07-14',
           TIMESTAMPTZ '2026-07-14 12:00:00+00', TIMESTAMPTZ '2026-07-14 18:00:00+00', 'vacunacion', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-14 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Módulo Vacunación — cronogramas por lote/granja/galpón');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0003-T2', 'DOCUMENTACION', 'LISTO', 'MEDIA',
           '🔄 CONTEXTO DE TRASPASO — módulo de Vacunación (cronogramas por lote)', 'Plan: fase_de_desarrollo/CONTEXTO_TRASPASO_VACUNACION.md', v_user_guid, 1, DATE '2026-07-14', DATE '2026-07-14',
           TIMESTAMPTZ '2026-07-14 12:00:00+00', TIMESTAMPTZ '2026-07-14 18:00:00+00', 'vacunacion', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-14 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = '🔄 CONTEXTO DE TRASPASO — módulo de Vacunación (cronogramas por lote)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0003-T3', 'MEJORA', 'LISTO', 'MEDIA',
           'Plan — Mejora integral del módulo Vacunación (performance + UI/UX + reportería)', 'Plan: fase_de_desarrollo/vacunacion_mejora_integral_plan.md', v_user_guid, 2, DATE '2026-07-16', DATE '2026-07-16',
           TIMESTAMPTZ '2026-07-16 12:00:00+00', TIMESTAMPTZ '2026-07-16 18:00:00+00', 'vacunacion', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-16 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Mejora integral del módulo Vacunación (performance + UI/UX + reportería)');

    -- ═══ Seguridad, sesión y alcance de acceso (6 trabajos) ═══
    INSERT INTO public.historias (codigo, pais_id, titulo, descripcion, estado, prioridad,
        responsable_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT 'HIS-2026-0004', v_pais, 'Seguridad, sesión y alcance de acceso', 'Historia derivada del historial real del repositorio: agrupa los trabajos con plan propio en fase_de_desarrollo/ correspondientes a este módulo.', 'LISTO', 'MEDIA',
           v_user_guid, 3, DATE '2026-06-11', DATE '2026-07-27',
           TIMESTAMPTZ '2026-06-11 12:00:00+00', TIMESTAMPTZ '2026-07-27 12:00:00+00', 'seguridad,auth', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-11 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.historias h WHERE h.codigo = 'HIS-2026-0004');

    SELECT id INTO v_hist FROM public.historias WHERE codigo = 'HIS-2026-0004';

    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0004-T1', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Endurecimiento de seguridad de Login y Registro', 'Plan: fase_de_desarrollo/seguridad_login_registro_plan.md', v_user_guid, 0, DATE '2026-06-11', DATE '2026-06-11',
           TIMESTAMPTZ '2026-06-11 12:00:00+00', TIMESTAMPTZ '2026-06-11 18:00:00+00', 'seguridad,auth', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-11 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Endurecimiento de seguridad de Login y Registro');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0004-T2', 'TAREA', 'LISTO', 'MEDIA',
           'PAT / Service Access Token — Plan', 'Plan: fase_de_desarrollo/service_access_token_plan.md', v_user_guid, 1, DATE '2026-07-04', DATE '2026-07-04',
           TIMESTAMPTZ '2026-07-04 12:00:00+00', TIMESTAMPTZ '2026-07-04 18:00:00+00', 'seguridad,auth', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-04 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'PAT / Service Access Token — Plan');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0004-T3', 'MEJORA', 'LISTO', 'MEDIA',
           'Plan — Ajuste rate limiting / bloqueo por IP (prod: ""Tu IP ha sido bloqueada temporalmente"")', 'Plan: fase_de_desarrollo/rate_limiting_ajuste_bloqueo_ip_plan.md', v_user_guid, 2, DATE '2026-07-21', DATE '2026-07-21',
           TIMESTAMPTZ '2026-07-21 12:00:00+00', TIMESTAMPTZ '2026-07-21 18:00:00+00', 'seguridad,auth', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-21 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Ajuste rate limiting / bloqueo por IP (prod: ""Tu IP ha sido bloqueada temporalmente"")');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0004-T4', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Sesión deslizante por inactividad (auto-logout 5 min + desconexión)', 'Plan: fase_de_desarrollo/sesion_deslizante_inactividad_plan.md', v_user_guid, 3, DATE '2026-07-22', DATE '2026-07-22',
           TIMESTAMPTZ '2026-07-22 12:00:00+00', TIMESTAMPTZ '2026-07-22 18:00:00+00', 'seguridad,auth', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-22 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Sesión deslizante por inactividad (auto-logout 5 min + desconexión)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0004-T5', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Alcance granular por usuario-granja (núcleo / galpón / lote o global)', 'Plan: fase_de_desarrollo/scope_ubicacion_usuario_granja_plan.md', v_user_guid, 4, DATE '2026-07-26', DATE '2026-07-26',
           TIMESTAMPTZ '2026-07-26 12:00:00+00', TIMESTAMPTZ '2026-07-26 18:00:00+00', 'seguridad,auth', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-26 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Alcance granular por usuario-granja (núcleo / galpón / lote o global)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0004-T6', 'TAREA', 'LISTO', 'MEDIA',
           'Fix — el reCAPTCHA de Google desapareció del login en producción', 'Plan: fase_de_desarrollo/csp_recaptcha_login_plan.md', v_user_guid, 5, DATE '2026-07-27', DATE '2026-07-27',
           TIMESTAMPTZ '2026-07-27 12:00:00+00', TIMESTAMPTZ '2026-07-27 18:00:00+00', 'seguridad,auth', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-27 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Fix — el reCAPTCHA de Google desapareció del login en producción');

    -- ═══ Usuarios, roles, menús y ubicaciones (8 trabajos) ═══
    INSERT INTO public.historias (codigo, pais_id, titulo, descripcion, estado, prioridad,
        responsable_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT 'HIS-2026-0005', v_pais, 'Usuarios, roles, menús y ubicaciones', 'Historia derivada del historial real del repositorio: agrupa los trabajos con plan propio en fase_de_desarrollo/ correspondientes a este módulo.', 'LISTO', 'MEDIA',
           v_user_guid, 4, DATE '2026-05-27', DATE '2026-07-26',
           TIMESTAMPTZ '2026-05-27 12:00:00+00', TIMESTAMPTZ '2026-07-26 12:00:00+00', 'usuarios,roles,granjas', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-27 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.historias h WHERE h.codigo = 'HIS-2026-0005');

    SELECT id INTO v_hist FROM public.historias WHERE codigo = 'HIS-2026-0005';

    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0005-T1', 'TAREA', 'LISTO', 'MEDIA',
           'Plan: Campos Cliente/Zona Panamá en Modal de Crear/Editar Granja', 'Plan: fase_de_desarrollo/farm_panama_cliente_zona_plan.md', v_user_guid, 0, DATE '2026-05-27', DATE '2026-05-27',
           TIMESTAMPTZ '2026-05-27 12:00:00+00', TIMESTAMPTZ '2026-05-27 18:00:00+00', 'usuarios,roles,granjas', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-27 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan: Campos Cliente/Zona Panamá en Modal de Crear/Editar Granja');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0005-T2', 'TAREA', 'LISTO', 'MEDIA',
           'Plan: Usuarios de Plataforma (sin correo real)', 'Plan: fase_de_desarrollo/usuarios_plataforma_plan.md', v_user_guid, 1, DATE '2026-06-03', DATE '2026-06-03',
           TIMESTAMPTZ '2026-06-03 12:00:00+00', TIMESTAMPTZ '2026-06-03 18:00:00+00', 'usuarios,roles,granjas', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-03 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan: Usuarios de Plataforma (sin correo real)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0005-T3', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Administrador de Empresa: visibilidad global de granjas en asignación de usuarios', 'Plan: fase_de_desarrollo/admin_empresa_granjas_plan.md', v_user_guid, 2, DATE '2026-07-21', DATE '2026-07-21',
           TIMESTAMPTZ '2026-07-21 12:00:00+00', TIMESTAMPTZ '2026-07-21 18:00:00+00', 'usuarios,roles,granjas', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-21 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Administrador de Empresa: visibilidad global de granjas en asignación de usuarios');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0005-T4', 'BUG', 'LISTO', 'MEDIA',
           'Plan — Alinear nombres de Lote Pollo Engorde (Panamá) al lote base asignado', 'Plan: fase_de_desarrollo/fix_nombres_lote_engorde_panama_por_lote_base_plan.md', v_user_guid, 3, DATE '2026-07-22', DATE '2026-07-22',
           TIMESTAMPTZ '2026-07-22 12:00:00+00', TIMESTAMPTZ '2026-07-22 18:00:00+00', 'usuarios,roles,granjas', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-22 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Alinear nombres de Lote Pollo Engorde (Panamá) al lote base asignado');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0005-T5', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — CRUD de ubicación seguro: mover/editar/eliminar Núcleo · Galpón · Lote (transversal multipaís)', 'Plan: fase_de_desarrollo/gestion_ubicacion_nucleo_galpon_lote_plan.md', v_user_guid, 4, DATE '2026-07-22', DATE '2026-07-22',
           TIMESTAMPTZ '2026-07-22 12:00:00+00', TIMESTAMPTZ '2026-07-22 18:00:00+00', 'usuarios,roles,granjas', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-22 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — CRUD de ubicación seguro: mover/editar/eliminar Núcleo · Galpón · Lote (transversal multipaís)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0005-T6', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Gestión de Granjas: cascada al eliminar + refresco entre tabs + scoping por granja asignada', 'Plan: fase_de_desarrollo/gestion_granjas_cascada_refresh_plan.md', v_user_guid, 5, DATE '2026-07-22', DATE '2026-07-22',
           TIMESTAMPTZ '2026-07-22 12:00:00+00', TIMESTAMPTZ '2026-07-22 18:00:00+00', 'usuarios,roles,granjas', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-22 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Gestión de Granjas: cascada al eliminar + refresco entre tabs + scoping por granja asignada');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0005-T7', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Lote base de pollo engorde: creación simple + asignación de granjas + visibilidad por granja', 'Plan: fase_de_desarrollo/lote_base_engorde_por_granja_plan.md', v_user_guid, 6, DATE '2026-07-22', DATE '2026-07-22',
           TIMESTAMPTZ '2026-07-22 12:00:00+00', TIMESTAMPTZ '2026-07-22 18:00:00+00', 'usuarios,roles,granjas', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-22 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Lote base de pollo engorde: creación simple + asignación de granjas + visibilidad por granja');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0005-T8', 'BUG', 'LISTO', 'MEDIA',
           'Plan — Corrección migración Santa Reyes: lotes del Excel → LOTE BASE (no lotes seguimiento)', 'Plan: fase_de_desarrollo/lote_base_santa_reyes_correccion_plan.md', v_user_guid, 7, DATE '2026-07-26', DATE '2026-07-26',
           TIMESTAMPTZ '2026-07-26 12:00:00+00', TIMESTAMPTZ '2026-07-26 18:00:00+00', 'usuarios,roles,granjas', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-26 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Corrección migración Santa Reyes: lotes del Excel → LOTE BASE (no lotes seguimiento)');

    -- ═══ Carga masiva y migraciones masivas (15 trabajos) ═══
    INSERT INTO public.historias (codigo, pais_id, titulo, descripcion, estado, prioridad,
        responsable_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT 'HIS-2026-0006', v_pais, 'Carga masiva y migraciones masivas', 'Historia derivada del historial real del repositorio: agrupa los trabajos con plan propio en fase_de_desarrollo/ correspondientes a este módulo.', 'LISTO', 'MEDIA',
           v_user_guid, 5, DATE '2026-05-28', DATE '2026-08-06',
           TIMESTAMPTZ '2026-05-28 12:00:00+00', TIMESTAMPTZ '2026-08-06 12:00:00+00', 'carga-masiva,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-28 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.historias h WHERE h.codigo = 'HIS-2026-0006');

    SELECT id INTO v_hist FROM public.historias WHERE codigo = 'HIS-2026-0006';

    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0006-T1', 'TAREA', 'LISTO', 'MEDIA',
           'Plan de Desarrollo — Migración masiva: recalcular `saldo_alimento_kg` para todos los lotes engorde', 'Plan: fase_de_desarrollo/13_migracion_masiva_saldo_alimento.md', v_user_guid, 0, DATE '2026-05-28', DATE '2026-05-28',
           TIMESTAMPTZ '2026-05-28 12:00:00+00', TIMESTAMPTZ '2026-05-28 18:00:00+00', 'carga-masiva,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-28 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan de Desarrollo — Migración masiva: recalcular `saldo_alimento_kg` para todos los lotes engorde');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0006-T2', 'DOCUMENTACION', 'LISTO', 'MEDIA',
           'Fase 3 — Migraciones Masivas: Ventas + Movimiento Aves + Movimiento Huevos (ESPECIFICACIÓN)', 'Plan: fase_de_desarrollo/migraciones_masivas_fase3_spec.md', v_user_guid, 1, DATE '2026-07-12', DATE '2026-07-12',
           TIMESTAMPTZ '2026-07-12 12:00:00+00', TIMESTAMPTZ '2026-07-12 18:00:00+00', 'carga-masiva,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-12 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Fase 3 — Migraciones Masivas: Ventas + Movimiento Aves + Movimiento Huevos (ESPECIFICACIÓN)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0006-T3', 'BUG', 'LISTO', 'MEDIA',
           'Plan — Fix migración masiva de Granjas (regionales + departamentos)', 'Plan: fase_de_desarrollo/migraciones_masivas_fix_granjas_plan.md', v_user_guid, 2, DATE '2026-07-12', DATE '2026-07-12',
           TIMESTAMPTZ '2026-07-12 12:00:00+00', TIMESTAMPTZ '2026-07-12 18:00:00+00', 'carga-masiva,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-12 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Fix migración masiva de Granjas (regionales + departamentos)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0006-T4', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Migraciones Masivas: línea ENGORDE (Lotes / Seguimiento / Venta)', 'Plan: fase_de_desarrollo/migraciones_masivas_engorde_plan.md', v_user_guid, 3, DATE '2026-07-12', DATE '2026-07-12',
           TIMESTAMPTZ '2026-07-12 12:00:00+00', TIMESTAMPTZ '2026-07-12 18:00:00+00', 'carga-masiva,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-12 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Migraciones Masivas: línea ENGORDE (Lotes / Seguimiento / Venta)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0006-T5', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Módulo de Migraciones Masivas (Postura)', 'Plan: fase_de_desarrollo/migraciones_masivas_plan.md', v_user_guid, 4, DATE '2026-07-12', DATE '2026-07-12',
           TIMESTAMPTZ '2026-07-12 12:00:00+00', TIMESTAMPTZ '2026-07-12 18:00:00+00', 'carga-masiva,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-12 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Módulo de Migraciones Masivas (Postura)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0006-T6', 'BUG', 'LISTO', 'MEDIA',
           'Plan — Fix: descuento de aves en migración masiva de Seguimiento Levante', 'Plan: fase_de_desarrollo/migracion_seguimiento_levante_aves_fix_plan.md', v_user_guid, 5, DATE '2026-07-13', DATE '2026-07-13',
           TIMESTAMPTZ '2026-07-13 12:00:00+00', TIMESTAMPTZ '2026-07-13 18:00:00+00', 'carga-masiva,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-13 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Fix: descuento de aves en migración masiva de Seguimiento Levante');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0006-T7', 'MEJORA', 'LISTO', 'MEDIA',
           'Plan — Mejoras del módulo Migraciones Masivas (Postura + Engorde)', 'Plan: fase_de_desarrollo/migraciones_masivas_mejoras_plan.md', v_user_guid, 6, DATE '2026-07-14', DATE '2026-07-14',
           TIMESTAMPTZ '2026-07-14 12:00:00+00', TIMESTAMPTZ '2026-07-14 18:00:00+00', 'carga-masiva,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-14 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Mejoras del módulo Migraciones Masivas (Postura + Engorde)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0006-T8', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Permisos de acceso a Migraciones Masivas (Postura / Pollo Engorde)', 'Plan: fase_de_desarrollo/migraciones_masivas_permiso_carga_masiva_plan.md', v_user_guid, 7, DATE '2026-07-14', DATE '2026-07-14',
           TIMESTAMPTZ '2026-07-14 12:00:00+00', TIMESTAMPTZ '2026-07-14 18:00:00+00', 'carga-masiva,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-14 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Permisos de acceso a Migraciones Masivas (Postura / Pollo Engorde)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0006-T9', 'DOCUMENTACION', 'LISTO', 'MEDIA',
           'Resultado — Mejoras del módulo Migraciones Masivas (Postura + Engorde)', 'Plan: fase_de_desarrollo/migraciones_masivas_mejoras_resultado.md', v_user_guid, 8, DATE '2026-07-14', DATE '2026-07-14',
           TIMESTAMPTZ '2026-07-14 12:00:00+00', TIMESTAMPTZ '2026-07-14 18:00:00+00', 'carga-masiva,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-14 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Resultado — Mejoras del módulo Migraciones Masivas (Postura + Engorde)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0006-T10', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Migraciones Masivas: línea Seguimiento Reproductora Engorde + alineación Seguimiento Pollo Engorde', 'Plan: fase_de_desarrollo/migracion_masiva_seguimiento_engorde_reproductora_plan.md', v_user_guid, 9, DATE '2026-07-23', DATE '2026-07-23',
           TIMESTAMPTZ '2026-07-23 12:00:00+00', TIMESTAMPTZ '2026-07-23 18:00:00+00', 'carga-masiva,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-23 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Migraciones Masivas: línea Seguimiento Reproductora Engorde + alineación Seguimiento Pollo Engorde');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0006-T11', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Venta Pollo Engorde: peso diferido en Panamá + carga masiva completa', 'Plan: fase_de_desarrollo/venta_engorde_panama_peso_diferido_y_carga_masiva_plan.md', v_user_guid, 10, DATE '2026-07-26', DATE '2026-07-26',
           TIMESTAMPTZ '2026-07-26 12:00:00+00', TIMESTAMPTZ '2026-07-26 18:00:00+00', 'carga-masiva,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-26 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Venta Pollo Engorde: peso diferido en Panamá + carga masiva completa');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0006-T12', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Alimento (ingresos/traslados) en el MISMO archivo de carga masiva de seguimiento engorde', 'Plan: fase_de_desarrollo/carga_masiva_alimento_engorde_plan.md', v_user_guid, 11, DATE '2026-07-27', DATE '2026-07-27',
           TIMESTAMPTZ '2026-07-27 12:00:00+00', TIMESTAMPTZ '2026-07-27 18:00:00+00', 'carga-masiva,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-27 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Alimento (ingresos/traslados) en el MISMO archivo de carga masiva de seguimiento engorde');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0006-T13', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Carga masiva de Postura (Levante + Producción): alimento con inventario real, huevos completos y validaciones a paridad con el alta manual', 'Plan: fase_de_desarrollo/migracion_masiva_postura_alimento_inventario_plan.md', v_user_guid, 12, DATE '2026-07-28', DATE '2026-07-28',
           TIMESTAMPTZ '2026-07-28 12:00:00+00', TIMESTAMPTZ '2026-07-28 18:00:00+00', 'carga-masiva,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-28 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Carga masiva de Postura (Levante + Producción): alimento con inventario real, huevos completos y validaciones a paridad con el alta manual');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0006-T14', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Carga masiva Seguimiento Diario Levante: movimientos de aves + tab huevos fijo + ocultar estructura', 'Plan: fase_de_desarrollo/carga_masiva_levante_movimientos_aves_plan.md', v_user_guid, 13, DATE '2026-07-31', DATE '2026-07-31',
           TIMESTAMPTZ '2026-07-31 12:00:00+00', TIMESTAMPTZ '2026-07-31 18:00:00+00', 'carga-masiva,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-31 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Carga masiva Seguimiento Diario Levante: movimientos de aves + tab huevos fijo + ocultar estructura');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0006-T15', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Archivos de carga masiva del lote S-369AB (postura: levante + producción + alimento)', 'Plan: fase_de_desarrollo/carga_masiva_s369ab_postura_plan.md', v_user_guid, 14, DATE '2026-08-06', DATE '2026-08-06',
           TIMESTAMPTZ '2026-08-06 12:00:00+00', TIMESTAMPTZ '2026-08-06 18:00:00+00', 'carga-masiva,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-08-06 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Archivos de carga masiva del lote S-369AB (postura: levante + producción + alimento)');

    -- ═══ Inventario, gastos y stock (19 trabajos) ═══
    INSERT INTO public.historias (codigo, pais_id, titulo, descripcion, estado, prioridad,
        responsable_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT 'HIS-2026-0007', v_pais, 'Inventario, gastos y stock', 'Historia derivada del historial real del repositorio: agrupa los trabajos con plan propio en fase_de_desarrollo/ correspondientes a este módulo.', 'LISTO', 'MEDIA',
           v_user_guid, 6, DATE '2026-05-28', DATE '2026-08-05',
           TIMESTAMPTZ '2026-05-28 12:00:00+00', TIMESTAMPTZ '2026-08-05 12:00:00+00', 'inventario,gastos', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-28 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.historias h WHERE h.codigo = 'HIS-2026-0007');

    SELECT id INTO v_hist FROM public.historias WHERE codigo = 'HIS-2026-0007';

    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0007-T1', 'BUG', 'LISTO', 'MEDIA',
           'Plan de Desarrollo — Fix: `SeguimientoAvesEngordeEcuadorService` descontar inventario y afectar saldos', 'Plan: fase_de_desarrollo/11_fix_seguimiento_ecuador_descuento_inventario.md', v_user_guid, 0, DATE '2026-05-28', DATE '2026-05-28',
           TIMESTAMPTZ '2026-05-28 12:00:00+00', TIMESTAMPTZ '2026-05-28 18:00:00+00', 'inventario,gastos', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-28 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan de Desarrollo — Fix: `SeguimientoAvesEngordeEcuadorService` descontar inventario y afectar saldos');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0007-T2', 'TAREA', 'LISTO', 'MEDIA',
           '19 — Reconciliación stock de inventario ↔ saldo de alimento del seguimiento (engorde)', 'Plan: fase_de_desarrollo/19_reconciliacion_inventario_vs_seguimiento_alimento.md', v_user_guid, 1, DATE '2026-06-01', DATE '2026-06-01',
           TIMESTAMPTZ '2026-06-01 12:00:00+00', TIMESTAMPTZ '2026-06-01 18:00:00+00', 'inventario,gastos', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-01 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = '19 — Reconciliación stock de inventario ↔ saldo de alimento del seguimiento (engorde)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0007-T3', 'DOCUMENTACION', 'LISTO', 'MEDIA',
           'Inventario Colombia — Mapa de módulos, rutas, consumo y unificación', 'Plan: fase_de_desarrollo/inventario_colombia_mapa.md', v_user_guid, 2, DATE '2026-07-02', DATE '2026-07-02',
           TIMESTAMPTZ '2026-07-02 12:00:00+00', TIMESTAMPTZ '2026-07-02 18:00:00+00', 'inventario,gastos', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-02 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Inventario Colombia — Mapa de módulos, rutas, consumo y unificación');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0007-T4', 'DOCUMENTACION', 'LISTO', 'MEDIA',
           'Mapa del Inventario Ecuador/Panamá (flujo TRÁNSITO) — para unificación con Colombia', 'Plan: fase_de_desarrollo/inventario_ecuador_mapa.md', v_user_guid, 3, DATE '2026-07-02', DATE '2026-07-02',
           TIMESTAMPTZ '2026-07-02 12:00:00+00', TIMESTAMPTZ '2026-07-02 18:00:00+00', 'inventario,gastos', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-02 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Mapa del Inventario Ecuador/Panamá (flujo TRÁNSITO) — para unificación con Colombia');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0007-T5', 'MEJORA', 'LISTO', 'MEDIA',
           'Plan — Unificación de inventarios (criterio estable, por fases)', 'Plan: fase_de_desarrollo/inventario_unificacion_plan.md', v_user_guid, 4, DATE '2026-07-02', DATE '2026-07-02',
           TIMESTAMPTZ '2026-07-02 12:00:00+00', TIMESTAMPTZ '2026-07-02 18:00:00+00', 'inventario,gastos', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-02 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Unificación de inventarios (criterio estable, por fases)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0007-T6', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — `fn_metadata_items_kg` (parseo de metadata en Postgres) + equivalencia', 'Plan: fase_de_desarrollo/fn_metadata_items_kg_plan.md', v_user_guid, 5, DATE '2026-07-04', DATE '2026-07-04',
           TIMESTAMPTZ '2026-07-04 12:00:00+00', TIMESTAMPTZ '2026-07-04 18:00:00+00', 'inventario,gastos', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-04 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — `fn_metadata_items_kg` (parseo de metadata en Postgres) + equivalencia');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0007-T7', 'MEJORA', 'LISTO', 'MEDIA',
           'Plan — Unificar inventario Colombia en el módulo nuevo + migración de datos', 'Plan: fase_de_desarrollo/unificacion_inventario_colombia_plan.md', v_user_guid, 6, DATE '2026-07-05', DATE '2026-07-05',
           TIMESTAMPTZ '2026-07-05 12:00:00+00', TIMESTAMPTZ '2026-07-05 18:00:00+00', 'inventario,gastos', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-05 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Unificar inventario Colombia en el módulo nuevo + migración de datos');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0007-T8', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Mejora UX módulo Gastos de inventario (Ecuador · No alimentos · stock granja)', 'Plan: fase_de_desarrollo/gastos_inventario_ux_plan.md', v_user_guid, 7, DATE '2026-07-06', DATE '2026-07-06',
           TIMESTAMPTZ '2026-07-06 12:00:00+00', TIMESTAMPTZ '2026-07-06 18:00:00+00', 'inventario,gastos', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-06 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Mejora UX módulo Gastos de inventario (Ecuador · No alimentos · stock granja)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0007-T9', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Ecuador: (A) Gastos de inventario sin exigir galpón/núcleo + (B) Liquidación Técnica Pollo Engorde (fechas)', 'Plan: fase_de_desarrollo/33_gastos_inventario_galpon_y_liquidacion_fechas_plan.md', v_user_guid, 8, DATE '2026-07-10', DATE '2026-07-10',
           TIMESTAMPTZ '2026-07-10 12:00:00+00', TIMESTAMPTZ '2026-07-10 18:00:00+00', 'inventario,gastos', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-10 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Ecuador: (A) Gastos de inventario sin exigir galpón/núcleo + (B) Liquidación Técnica Pollo Engorde (fechas)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0007-T10', 'MEJORA', 'LISTO', 'MEDIA',
           'Plan — Rename neutro del módulo de Inventario (Ecuador → neutro, multipaís)', 'Plan: fase_de_desarrollo/inventario_rename_neutro_plan.md', v_user_guid, 9, DATE '2026-07-10', DATE '2026-07-10',
           TIMESTAMPTZ '2026-07-10 12:00:00+00', TIMESTAMPTZ '2026-07-10 18:00:00+00', 'inventario,gastos', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-10 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Rename neutro del módulo de Inventario (Ecuador → neutro, multipaís)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0007-T11', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Seguimiento diario: catálogo de alimento desde inventario NUEVO + alimento distinto para machos', 'Plan: fase_de_desarrollo/inventario_nuevo_y_alimento_macho_seguimiento_plan.md', v_user_guid, 10, DATE '2026-07-10', DATE '2026-07-10',
           TIMESTAMPTZ '2026-07-10 12:00:00+00', TIMESTAMPTZ '2026-07-10 18:00:00+00', 'inventario,gastos', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-10 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Seguimiento diario: catálogo de alimento desde inventario NUEVO + alimento distinto para machos');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0007-T12', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Gastos de inventario: filtros solo Granja+Corrida y Concepto filtrado por stock', 'Plan: fase_de_desarrollo/gastos_inventario_filtros_concepto_stock_plan.md', v_user_guid, 11, DATE '2026-07-11', DATE '2026-07-11',
           TIMESTAMPTZ '2026-07-11 12:00:00+00', TIMESTAMPTZ '2026-07-11 18:00:00+00', 'inventario,gastos', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-11 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Gastos de inventario: filtros solo Granja+Corrida y Concepto filtrado por stock');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0007-T13', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Inventario Gestión: scoping multi-empresa / multi-país consistente + ítems de Panamá', 'Plan: fase_de_desarrollo/inventario_multiempresa_scoping_plan.md', v_user_guid, 12, DATE '2026-07-17', DATE '2026-07-17',
           TIMESTAMPTZ '2026-07-17 12:00:00+00', TIMESTAMPTZ '2026-07-17 18:00:00+00', 'inventario,gastos', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-17 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Inventario Gestión: scoping multi-empresa / multi-país consistente + ítems de Panamá');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0007-T14', 'BUG', 'LISTO', 'MEDIA',
           'Fix — Consumo de inventario Colombia multi-empresa (error 400 ""no tiene equivalente"")', 'Plan: fase_de_desarrollo/fix_consumo_inventario_colombia_multiempresa_plan.md', v_user_guid, 13, DATE '2026-07-21', DATE '2026-07-21',
           TIMESTAMPTZ '2026-07-21 12:00:00+00', TIMESTAMPTZ '2026-07-21 18:00:00+00', 'inventario,gastos', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-21 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Fix — Consumo de inventario Colombia multi-empresa (error 400 ""no tiene equivalente"")');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0007-T15', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — `fn_rekey_nucleo` copia `codigo_bodega`/`descripcion_bodega` al mover núcleo', 'Plan: fase_de_desarrollo/fn_mover_ubicacion_codigo_bodega_plan.md', v_user_guid, 14, DATE '2026-07-25', DATE '2026-07-25',
           TIMESTAMPTZ '2026-07-25 12:00:00+00', TIMESTAMPTZ '2026-07-25 18:00:00+00', 'inventario,gastos', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-25 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — `fn_rekey_nucleo` copia `codigo_bodega`/`descripcion_bodega` al mover núcleo');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0007-T16', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Recepción de tránsito con distribución en varios galpones (Gestión de Inventario)', 'Plan: fase_de_desarrollo/recepcion_transito_distribucion_galpones_plan.md', v_user_guid, 15, DATE '2026-07-26', DATE '2026-07-26',
           TIMESTAMPTZ '2026-07-26 12:00:00+00', TIMESTAMPTZ '2026-07-26 18:00:00+00', 'inventario,gastos', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-26 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Recepción de tránsito con distribución en varios galpones (Gestión de Inventario)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0007-T17', 'TAREA', 'LISTO', 'MEDIA',
           'Descargar Excel del stock de TODAS las granjas — Gestión de Inventario', 'Plan: fase_de_desarrollo/exportar_stock_inventario_excel_plan.md', v_user_guid, 16, DATE '2026-08-05', DATE '2026-08-05',
           TIMESTAMPTZ '2026-08-05 12:00:00+00', TIMESTAMPTZ '2026-08-05 18:00:00+00', 'inventario,gastos', v_company, v_cedula,
           TIMESTAMPTZ '2026-08-05 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Descargar Excel del stock de TODAS las granjas — Gestión de Inventario');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0007-T18', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Gastos de inventario: reporte sin eliminados + hoja de existencias completas', 'Plan: fase_de_desarrollo/gastos_inventario_reporte_estado_existencias_plan.md', v_user_guid, 17, DATE '2026-08-05', DATE '2026-08-05',
           TIMESTAMPTZ '2026-08-05 12:00:00+00', TIMESTAMPTZ '2026-08-05 18:00:00+00', 'inventario,gastos', v_company, v_cedula,
           TIMESTAMPTZ '2026-08-05 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Gastos de inventario: reporte sin eliminados + hoja de existencias completas');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0007-T19', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — las 10 líneas de gasto con `concepto = ''insumo''` (item 57 · AV0351)', 'Plan: fase_de_desarrollo/concepto_insumo_snapshot_gastos_plan.md', v_user_guid, 18, DATE '2026-08-05', DATE '2026-08-05',
           TIMESTAMPTZ '2026-08-05 12:00:00+00', TIMESTAMPTZ '2026-08-05 18:00:00+00', 'inventario,gastos', v_company, v_cedula,
           TIMESTAMPTZ '2026-08-05 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — las 10 líneas de gasto con `concepto = ''insumo''` (item 57 · AV0351)');

    -- ═══ Liquidación y cierre de lotes (10 trabajos) ═══
    INSERT INTO public.historias (codigo, pais_id, titulo, descripcion, estado, prioridad,
        responsable_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT 'HIS-2026-0008', v_pais, 'Liquidación y cierre de lotes', 'Historia derivada del historial real del repositorio: agrupa los trabajos con plan propio en fase_de_desarrollo/ correspondientes a este módulo.', 'LISTO', 'MEDIA',
           v_user_guid, 7, DATE '2026-05-21', DATE '2026-07-31',
           TIMESTAMPTZ '2026-05-21 12:00:00+00', TIMESTAMPTZ '2026-07-31 12:00:00+00', 'liquidacion,cierre', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-21 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.historias h WHERE h.codigo = 'HIS-2026-0008');

    SELECT id INTO v_hist FROM public.historias WHERE codigo = 'HIS-2026-0008';

    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0008-T1', 'TAREA', 'LISTO', 'MEDIA',
           'Feature 11 — Reporte de Liquidación Técnica (Estilo Excel)', 'Plan: fase_de_desarrollo/11_reporte_liquidacion_tecnica.md', v_user_guid, 0, DATE '2026-05-21', DATE '2026-05-21',
           TIMESTAMPTZ '2026-05-21 12:00:00+00', TIMESTAMPTZ '2026-05-21 18:00:00+00', 'liquidacion,cierre', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-21 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Feature 11 — Reporte de Liquidación Técnica (Estilo Excel)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0008-T2', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Liquidación / Indicadores Panamá (Pollo Engorde)', 'Plan: fase_de_desarrollo/20_liquidacion_indicadores_panama_plan.md', v_user_guid, 1, DATE '2026-06-01', DATE '2026-06-01',
           TIMESTAMPTZ '2026-06-01 12:00:00+00', TIMESTAMPTZ '2026-06-01 18:00:00+00', 'liquidacion,cierre', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-01 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Liquidación / Indicadores Panamá (Pollo Engorde)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0008-T3', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Merma en Liquidación Ecuador + Peso real por lote en ventas multi-lote', 'Plan: fase_de_desarrollo/merma_liquidacion_ecuador_plan.md', v_user_guid, 2, DATE '2026-06-11', DATE '2026-06-11',
           TIMESTAMPTZ '2026-06-11 12:00:00+00', TIMESTAMPTZ '2026-06-11 18:00:00+00', 'liquidacion,cierre', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-11 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Merma en Liquidación Ecuador + Peso real por lote en ventas multi-lote');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0008-T4', 'BUG', 'LISTO', 'MEDIA',
           'Plan — Corrección descuadre Liquidación Pollo Engorde Ecuador (lote 2601 / granja 38)', 'Plan: fase_de_desarrollo/descuadre_liquidacion_pollo_engorde_ecuador_plan.md', v_user_guid, 3, DATE '2026-06-27', DATE '2026-06-27',
           TIMESTAMPTZ '2026-06-27 12:00:00+00', TIMESTAMPTZ '2026-06-27 18:00:00+00', 'liquidacion,cierre', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-27 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Corrección descuadre Liquidación Pollo Engorde Ecuador (lote 2601 / granja 38)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0008-T5', 'DOCUMENTACION', 'LISTO', 'MEDIA',
           'Plan — Verificador / Auditoría de Liquidación Pollo Engorde (Ecuador)', 'Plan: fase_de_desarrollo/auditoria_liquidacion_engorde_plan.md', v_user_guid, 4, DATE '2026-06-27', DATE '2026-06-27',
           TIMESTAMPTZ '2026-06-27 12:00:00+00', TIMESTAMPTZ '2026-06-27 18:00:00+00', 'liquidacion,cierre', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-27 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Verificador / Auditoría de Liquidación Pollo Engorde (Ecuador)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0008-T6', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Liquidación Panamá por CORRIDA (tab Pollo Engorde del módulo Indicador)', 'Plan: fase_de_desarrollo/liquidacion_panama_por_corrida_plan.md', v_user_guid, 5, DATE '2026-07-20', DATE '2026-07-20',
           TIMESTAMPTZ '2026-07-20 12:00:00+00', TIMESTAMPTZ '2026-07-20 18:00:00+00', 'liquidacion,cierre', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-20 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Liquidación Panamá por CORRIDA (tab Pollo Engorde del módulo Indicador)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0008-T7', 'BUG', 'LISTO', 'MEDIA',
           'Plan — ""Reabrir lote"" reproductora engorde no persiste (confirma sin aplicar)', 'Plan: fase_de_desarrollo/reabrir_lote_reproductora_no_persiste_plan.md', v_user_guid, 6, DATE '2026-07-22', DATE '2026-07-22',
           TIMESTAMPTZ '2026-07-22 12:00:00+00', TIMESTAMPTZ '2026-07-22 18:00:00+00', 'liquidacion,cierre', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-22 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — ""Reabrir lote"" reproductora engorde no persiste (confirma sin aplicar)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0008-T8', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Cierre del lote reproductora engorde por CONFIRMACIÓN (no por registro)', 'Plan: fase_de_desarrollo/cierre_lote_reproductora_por_confirmacion_plan.md', v_user_guid, 7, DATE '2026-07-22', DATE '2026-07-22',
           TIMESTAMPTZ '2026-07-22 12:00:00+00', TIMESTAMPTZ '2026-07-22 18:00:00+00', 'liquidacion,cierre', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-22 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Cierre del lote reproductora engorde por CONFIRMACIÓN (no por registro)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0008-T9', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Reapertura validada de Levante + Cierre/Reapertura de Lote de Producción', 'Plan: fase_de_desarrollo/cierre_levante_reapertura_validada_y_cierre_produccion_plan.md', v_user_guid, 8, DATE '2026-07-26', DATE '2026-07-26',
           TIMESTAMPTZ '2026-07-26 12:00:00+00', TIMESTAMPTZ '2026-07-26 18:00:00+00', 'liquidacion,cierre', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-26 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Reapertura validada de Levante + Cierre/Reapertura de Lote de Producción');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0008-T10', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Congelar la liquidación de un lote de pollo engorde', 'Plan: fase_de_desarrollo/congelar_liquidacion_lote_engorde_plan.md', v_user_guid, 9, DATE '2026-07-31', DATE '2026-07-31',
           TIMESTAMPTZ '2026-07-31 12:00:00+00', TIMESTAMPTZ '2026-07-31 18:00:00+00', 'liquidacion,cierre', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-31 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Congelar la liquidación de un lote de pollo engorde');

    -- ═══ Reproductoras y cruce a pollo engorde (11 trabajos) ═══
    INSERT INTO public.historias (codigo, pais_id, titulo, descripcion, estado, prioridad,
        responsable_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT 'HIS-2026-0009', v_pais, 'Reproductoras y cruce a pollo engorde', 'Historia derivada del historial real del repositorio: agrupa los trabajos con plan propio en fase_de_desarrollo/ correspondientes a este módulo.', 'LISTO', 'MEDIA',
           v_user_guid, 8, DATE '2026-06-01', DATE '2026-07-24',
           TIMESTAMPTZ '2026-06-01 12:00:00+00', TIMESTAMPTZ '2026-07-24 12:00:00+00', 'reproductoras,engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-01 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.historias h WHERE h.codigo = 'HIS-2026-0009');

    SELECT id INTO v_hist FROM public.historias WHERE codigo = 'HIS-2026-0009';

    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0009-T1', 'MEJORA', 'LISTO', 'MEDIA',
           'Plan: Optimización Seguimiento Diario Reproductora', 'Plan: fase_de_desarrollo/seguimiento_diario_reproductora_optimizacion_plan.md', v_user_guid, 0, DATE '2026-06-01', DATE '2026-06-01',
           TIMESTAMPTZ '2026-06-01 12:00:00+00', TIMESTAMPTZ '2026-06-01 18:00:00+00', 'reproductoras,engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-01 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan: Optimización Seguimiento Diario Reproductora');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0009-T2', 'TAREA', 'LISTO', 'MEDIA',
           'Plan: Cruce Automático Seguimiento Reproductora → Seguimiento Pollo Engorde', 'Plan: fase_de_desarrollo/cruce_reproductora_a_pollo_engorde_plan.md', v_user_guid, 1, DATE '2026-06-02', DATE '2026-06-02',
           TIMESTAMPTZ '2026-06-02 12:00:00+00', TIMESTAMPTZ '2026-06-02 18:00:00+00', 'reproductoras,engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-02 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan: Cruce Automático Seguimiento Reproductora → Seguimiento Pollo Engorde');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0009-T3', 'MEJORA', 'LISTO', 'MEDIA',
           'Plan: Mejoras Módulo Lote Reproductora Aves de Engorde', 'Plan: fase_de_desarrollo/lote_reproductora_mejoras_modal_tabla_plan.md', v_user_guid, 2, DATE '2026-06-03', DATE '2026-06-03',
           TIMESTAMPTZ '2026-06-03 12:00:00+00', TIMESTAMPTZ '2026-06-03 18:00:00+00', 'reproductoras,engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-03 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan: Mejoras Módulo Lote Reproductora Aves de Engorde');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0009-T4', 'TAREA', 'LISTO', 'MEDIA',
           'Plan: Mixto + Consumo de Agua + Reapertura con Novedad — Cruce Reproductora → Pollo Engorde', 'Plan: fase_de_desarrollo/22_mixto_agua_reapertura_cruce_reproductora_engorde_plan.md', v_user_guid, 3, DATE '2026-06-07', DATE '2026-06-07',
           TIMESTAMPTZ '2026-06-07 12:00:00+00', TIMESTAMPTZ '2026-06-07 18:00:00+00', 'reproductoras,engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-07 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan: Mixto + Consumo de Agua + Reapertura con Novedad — Cruce Reproductora → Pollo Engorde');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0009-T5', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Carga de prueba Lote 31 «Doña María D-1» (Excel → BD)', 'Plan: fase_de_desarrollo/30_carga_prueba_lote31_engorde_reproductoras_plan.md', v_user_guid, 4, DATE '2026-06-10', DATE '2026-06-10',
           TIMESTAMPTZ '2026-06-10 12:00:00+00', TIMESTAMPTZ '2026-06-10 18:00:00+00', 'reproductoras,engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-10 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Carga de prueba Lote 31 «Doña María D-1» (Excel → BD)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0009-T6', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Tab «R. Reproductora» en Seguimiento Diario Pollo Engorde', 'Plan: fase_de_desarrollo/31_tab_reproductora_seguimiento_engorde_plan.md', v_user_guid, 5, DATE '2026-06-10', DATE '2026-06-10',
           TIMESTAMPTZ '2026-06-10 12:00:00+00', TIMESTAMPTZ '2026-06-10 18:00:00+00', 'reproductoras,engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-10 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Tab «R. Reproductora» en Seguimiento Diario Pollo Engorde');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0009-T7', 'MEJORA', 'LISTO', 'MEDIA',
           'Plan — Ajustes de creación/edición en Lote Reproductora Aves de Engorde', 'Plan: fase_de_desarrollo/lote_reproductora_engorde_ajustes_creacion_plan.md', v_user_guid, 6, DATE '2026-07-22', DATE '2026-07-22',
           TIMESTAMPTZ '2026-07-22 12:00:00+00', TIMESTAMPTZ '2026-07-22 18:00:00+00', 'reproductoras,engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-22 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Ajustes de creación/edición en Lote Reproductora Aves de Engorde');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0009-T8', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Confirmación por registro en Seguimiento Diario Reproductora (Pollo Engorde)', 'Plan: fase_de_desarrollo/confirmacion_seguimiento_reproductora_engorde_plan.md', v_user_guid, 7, DATE '2026-07-22', DATE '2026-07-22',
           TIMESTAMPTZ '2026-07-22 12:00:00+00', TIMESTAMPTZ '2026-07-22 18:00:00+00', 'reproductoras,engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-22 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Confirmación por registro en Seguimiento Diario Reproductora (Pollo Engorde)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0009-T9', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Seguimiento Diario Reproductora Pollo Engorde: fechas y edición', 'Plan: fase_de_desarrollo/seguimiento_reproductora_engorde_fechas_edicion_plan.md', v_user_guid, 8, DATE '2026-07-22', DATE '2026-07-22',
           TIMESTAMPTZ '2026-07-22 12:00:00+00', TIMESTAMPTZ '2026-07-22 18:00:00+00', 'reproductoras,engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-22 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Seguimiento Diario Reproductora Pollo Engorde: fechas y edición');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0009-T10', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — UX cascada numerada en «Lote Reproductora Aves de Engorde»', 'Plan: fase_de_desarrollo/lote_reproductora_engorde_ux_cascada_plan.md', v_user_guid, 9, DATE '2026-07-23', DATE '2026-07-23',
           TIMESTAMPTZ '2026-07-23 12:00:00+00', TIMESTAMPTZ '2026-07-23 18:00:00+00', 'reproductoras,engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-23 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — UX cascada numerada en «Lote Reproductora Aves de Engorde»');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0009-T11', 'TAREA', 'LISTO', 'MEDIA',
           'Seguimiento reproductora engorde — el día del encasetamiento cuenta como DÍA 1', 'Plan: fase_de_desarrollo/seguimiento_reproductora_engorde_edad_cero_plan.md', v_user_guid, 10, DATE '2026-07-24', DATE '2026-07-24',
           TIMESTAMPTZ '2026-07-24 12:00:00+00', TIMESTAMPTZ '2026-07-24 18:00:00+00', 'reproductoras,engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-24 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Seguimiento reproductora engorde — el día del encasetamiento cuenta como DÍA 1');

    -- ═══ Movimientos de aves, traslados y ventas (14 trabajos) ═══
    INSERT INTO public.historias (codigo, pais_id, titulo, descripcion, estado, prioridad,
        responsable_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT 'HIS-2026-0010', v_pais, 'Movimientos de aves, traslados y ventas', 'Historia derivada del historial real del repositorio: agrupa los trabajos con plan propio en fase_de_desarrollo/ correspondientes a este módulo.', 'LISTO', 'MEDIA',
           v_user_guid, 9, DATE '2026-05-25', DATE '2026-08-05',
           TIMESTAMPTZ '2026-05-25 12:00:00+00', TIMESTAMPTZ '2026-08-05 12:00:00+00', 'movimientos,traslados', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-25 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.historias h WHERE h.codigo = 'HIS-2026-0010');

    SELECT id INTO v_hist FROM public.historias WHERE codigo = 'HIS-2026-0010';

    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0010-T1', 'MEJORA', 'LISTO', 'MEDIA',
           'Feature 13 — Traslado de Aves Mejorado (Levante)', 'Plan: fase_de_desarrollo/13_traslado_aves_mejorado_plan.md', v_user_guid, 0, DATE '2026-05-25', DATE '2026-05-25',
           TIMESTAMPTZ '2026-05-25 12:00:00+00', TIMESTAMPTZ '2026-05-25 18:00:00+00', 'movimientos,traslados', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-25 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Feature 13 — Traslado de Aves Mejorado (Levante)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0010-T2', 'TAREA', 'LISTO', 'MEDIA',
           'Feature 14 — Traslado de Aves en Producción (paridad con Levante)', 'Plan: fase_de_desarrollo/14_traslado_aves_produccion_plan.md', v_user_guid, 1, DATE '2026-05-25', DATE '2026-05-25',
           TIMESTAMPTZ '2026-05-25 12:00:00+00', TIMESTAMPTZ '2026-05-25 18:00:00+00', 'movimientos,traslados', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-25 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Feature 14 — Traslado de Aves en Producción (paridad con Levante)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0010-T3', 'TAREA', 'LISTO', 'MEDIA',
           'Plan Técnico — Feature 12: Lotes y Traslados de Aves', 'Plan: fase_de_desarrollo/12_lotes_y_traslados_plan.md', v_user_guid, 2, DATE '2026-05-25', DATE '2026-05-25',
           TIMESTAMPTZ '2026-05-25 12:00:00+00', TIMESTAMPTZ '2026-05-25 18:00:00+00', 'movimientos,traslados', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-25 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan Técnico — Feature 12: Lotes y Traslados de Aves');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0010-T4', 'BUG', 'LISTO', 'MEDIA',
           'Plan: Fix Producción 405 Method Not Allowed — Backend Traslados', 'Plan: fase_de_desarrollo/fix_produccion_405_traslados_plan.md', v_user_guid, 3, DATE '2026-05-26', DATE '2026-05-26',
           TIMESTAMPTZ '2026-05-26 12:00:00+00', TIMESTAMPTZ '2026-05-26 18:00:00+00', 'movimientos,traslados', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-26 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan: Fix Producción 405 Method Not Allowed — Backend Traslados');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0010-T5', 'MEJORA', 'LISTO', 'MEDIA',
           '23 — Refactor Clean Code: módulo `movimientos-pollo-engorde` (base multi-país)', 'Plan: fase_de_desarrollo/23_refactor_clean_code_movimientos_pollo_engorde_plan.md', v_user_guid, 4, DATE '2026-06-07', DATE '2026-06-07',
           TIMESTAMPTZ '2026-06-07 12:00:00+00', TIMESTAMPTZ '2026-06-07 18:00:00+00', 'movimientos,traslados', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-07 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = '23 — Refactor Clean Code: módulo `movimientos-pollo-engorde` (base multi-país)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0010-T6', 'TAREA', 'LISTO', 'MEDIA',
           '24 — Permisos por botón en `movimientos-pollo-engorde`', 'Plan: fase_de_desarrollo/24_permisos_botones_movimientos_pollo_engorde_plan.md', v_user_guid, 5, DATE '2026-06-07', DATE '2026-06-07',
           TIMESTAMPTZ '2026-06-07 12:00:00+00', TIMESTAMPTZ '2026-06-07 18:00:00+00', 'movimientos,traslados', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-07 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = '24 — Permisos por botón en `movimientos-pollo-engorde`');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0010-T7', 'MEJORA', 'LISTO', 'MEDIA',
           '25 — Refactor Clean Code (Backend): `MovimientoPolloEngordeService`', 'Plan: fase_de_desarrollo/25_refactor_clean_code_backend_movimientos_pollo_engorde_service_plan.md', v_user_guid, 6, DATE '2026-06-07', DATE '2026-06-07',
           TIMESTAMPTZ '2026-06-07 12:00:00+00', TIMESTAMPTZ '2026-06-07 18:00:00+00', 'movimientos,traslados', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-07 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = '25 — Refactor Clean Code (Backend): `MovimientoPolloEngordeService`');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0010-T8', 'TAREA', 'LISTO', 'MEDIA',
           '26 — Extracción de math puro a `Application/Calculos/` (Movimientos Pollo Engorde)', 'Plan: fase_de_desarrollo/26_extraccion_math_puro_movimientos_pollo_engorde_plan.md', v_user_guid, 7, DATE '2026-06-07', DATE '2026-06-07',
           TIMESTAMPTZ '2026-06-07 12:00:00+00', TIMESTAMPTZ '2026-06-07 18:00:00+00', 'movimientos,traslados', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-07 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = '26 — Extracción de math puro a `Application/Calculos/` (Movimientos Pollo Engorde)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0010-T9', 'TAREA', 'LISTO', 'MEDIA',
           '27 — Venta de Pollo Engorde Panamá (modal + servicio separados)', 'Plan: fase_de_desarrollo/27_venta_pollo_engorde_panama_plan.md', v_user_guid, 8, DATE '2026-06-07', DATE '2026-06-07',
           TIMESTAMPTZ '2026-06-07 12:00:00+00', TIMESTAMPTZ '2026-06-07 18:00:00+00', 'movimientos,traslados', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-07 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = '27 — Venta de Pollo Engorde Panamá (modal + servicio separados)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0010-T10', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Bloquear venta de lotes cerrados / corridas anteriores en ""Venta por granja"" (Movimientos Pollo Engorde)', 'Plan: fase_de_desarrollo/venta_granja_bloqueo_lotes_cerrados_plan.md', v_user_guid, 9, DATE '2026-07-14', DATE '2026-07-14',
           TIMESTAMPTZ '2026-07-14 12:00:00+00', TIMESTAMPTZ '2026-07-14 18:00:00+00', 'movimientos,traslados', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-14 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Bloquear venta de lotes cerrados / corridas anteriores en ""Venta por granja"" (Movimientos Pollo Engorde)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0010-T11', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Traslado de aves desde seguimiento: fechas puras ancladas a MEDIODÍA', 'Plan: fase_de_desarrollo/traslado_aves_seg_fechas_mediodia_plan.md', v_user_guid, 10, DATE '2026-07-31', DATE '2026-07-31',
           TIMESTAMPTZ '2026-07-31 12:00:00+00', TIMESTAMPTZ '2026-07-31 18:00:00+00', 'movimientos,traslados', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-31 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Traslado de aves desde seguimiento: fechas puras ancladas a MEDIODÍA');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0010-T12', 'BUG', 'LISTO', 'MEDIA',
           'Fix — «Aves disponibles» difiere entre Seguimiento diario y Venta (pollo engorde)', 'Plan: fase_de_desarrollo/fix_disponibilidad_aves_venta_engorde_plan.md', v_user_guid, 11, DATE '2026-08-05', DATE '2026-08-05',
           TIMESTAMPTZ '2026-08-05 12:00:00+00', TIMESTAMPTZ '2026-08-05 18:00:00+00', 'movimientos,traslados', v_company, v_cedula,
           TIMESTAMPTZ '2026-08-05 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Fix — «Aves disponibles» difiere entre Seguimiento diario y Venta (pollo engorde)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0010-T13', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Traslado de aves: destino cross-granja/galpón en Engorde + fecha de registro visible', 'Plan: fase_de_desarrollo/traslado_aves_destino_cross_granja_y_fecha_registro_plan.md', v_user_guid, 12, DATE '2026-08-05', DATE '2026-08-05',
           TIMESTAMPTZ '2026-08-05 12:00:00+00', TIMESTAMPTZ '2026-08-05 18:00:00+00', 'movimientos,traslados', v_company, v_cedula,
           TIMESTAMPTZ '2026-08-05 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Traslado de aves: destino cross-granja/galpón en Engorde + fecha de registro visible');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0010-T14', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Trazabilidad de cohortes: cuántas aves, de dónde y con qué edad en el lote receptor', 'Plan: fase_de_desarrollo/cohortes_edades_lote_receptor_plan.md', v_user_guid, 13, DATE '2026-08-05', DATE '2026-08-05',
           TIMESTAMPTZ '2026-08-05 12:00:00+00', TIMESTAMPTZ '2026-08-05 18:00:00+00', 'movimientos,traslados', v_company, v_cedula,
           TIMESTAMPTZ '2026-08-05 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Trazabilidad de cohortes: cuántas aves, de dónde y con qué edad en el lote receptor');

    -- ═══ Guías genéticas y uniformidad (3 trabajos) ═══
    INSERT INTO public.historias (codigo, pais_id, titulo, descripcion, estado, prioridad,
        responsable_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT 'HIS-2026-0011', v_pais, 'Guías genéticas y uniformidad', 'Historia derivada del historial real del repositorio: agrupa los trabajos con plan propio en fase_de_desarrollo/ correspondientes a este módulo.', 'LISTO', 'MEDIA',
           v_user_guid, 10, DATE '2026-06-10', DATE '2026-07-22',
           TIMESTAMPTZ '2026-06-10 12:00:00+00', TIMESTAMPTZ '2026-07-22 12:00:00+00', 'guia-genetica', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-10 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.historias h WHERE h.codigo = 'HIS-2026-0011');

    SELECT id INTO v_hist FROM public.historias WHERE codigo = 'HIS-2026-0011';

    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0011-T1', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Decimales en campos de peso/uniformidad del modal «Nuevo Lote de Engorde»', 'Plan: fase_de_desarrollo/29_decimales_pesos_uniformidad_lote_engorde_plan.md', v_user_guid, 0, DATE '2026-06-10', DATE '2026-06-10',
           TIMESTAMPTZ '2026-06-10 12:00:00+00', TIMESTAMPTZ '2026-06-10 18:00:00+00', 'guia-genetica', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-10 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Decimales en campos de peso/uniformidad del modal «Nuevo Lote de Engorde»');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0011-T2', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Alineación Postura Colombia (Levante + Producción) contra Guía Genética', 'Plan: fase_de_desarrollo/postura_colombia_alineacion_guia_plan.md', v_user_guid, 1, DATE '2026-07-01', DATE '2026-07-01',
           TIMESTAMPTZ '2026-07-01 12:00:00+00', TIMESTAMPTZ '2026-07-01 18:00:00+00', 'guia-genetica', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-01 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Alineación Postura Colombia (Levante + Producción) contra Guía Genética');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0011-T3', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Guía genética Panamá: Ross 308 AP 2022 (mixto) + repunte de lotes', 'Plan: fase_de_desarrollo/guia_genetica_panama_ross308ap_2022_plan.md', v_user_guid, 2, DATE '2026-07-22', DATE '2026-07-22',
           TIMESTAMPTZ '2026-07-22 12:00:00+00', TIMESTAMPTZ '2026-07-22 18:00:00+00', 'guia-genetica', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-22 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Guía genética Panamá: Ross 308 AP 2022 (mixto) + repunte de lotes');

    -- ═══ Pollo de engorde — seguimiento y saldos (33 trabajos) ═══
    INSERT INTO public.historias (codigo, pais_id, titulo, descripcion, estado, prioridad,
        responsable_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT 'HIS-2026-0012', v_pais, 'Pollo de engorde — seguimiento y saldos', 'Historia derivada del historial real del repositorio: agrupa los trabajos con plan propio en fase_de_desarrollo/ correspondientes a este módulo.', 'LISTO', 'MEDIA',
           v_user_guid, 11, DATE '2026-05-11', DATE '2026-08-06',
           TIMESTAMPTZ '2026-05-11 12:00:00+00', TIMESTAMPTZ '2026-08-06 12:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-11 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.historias h WHERE h.codigo = 'HIS-2026-0012');

    SELECT id INTO v_hist FROM public.historias WHERE codigo = 'HIS-2026-0012';

    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T1', 'BUG', 'LISTO', 'MEDIA',
           'BUG — Conciliación de Saldo Inicial de Aves (Pollo Engorde)', 'Plan: fase_de_desarrollo/07_bug_conciliacion_saldo_inicial_aves.md', v_user_guid, 0, DATE '2026-05-11', DATE '2026-05-11',
           TIMESTAMPTZ '2026-05-11 12:00:00+00', TIMESTAMPTZ '2026-05-11 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-11 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'BUG — Conciliación de Saldo Inicial de Aves (Pollo Engorde)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T2', 'TAREA', 'LISTO', 'MEDIA',
           'Plan de Desarrollo — Función DB: Tabla Diaria Seguimiento Pollo Engorde', 'Plan: fase_de_desarrollo/09_fn_seguimiento_diario_engorde.md', v_user_guid, 1, DATE '2026-05-20', DATE '2026-05-20',
           TIMESTAMPTZ '2026-05-20 12:00:00+00', TIMESTAMPTZ '2026-05-20 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-20 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan de Desarrollo — Función DB: Tabla Diaria Seguimiento Pollo Engorde');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T3', 'MEJORA', 'LISTO', 'MEDIA',
           'Plan 10 — Fase de Mejoras Integrales: Módulo Pollo de Engorde', 'Plan: fase_de_desarrollo/10_mejoras_integrales_engorde.md', v_user_guid, 2, DATE '2026-05-21', DATE '2026-05-21',
           TIMESTAMPTZ '2026-05-21 12:00:00+00', TIMESTAMPTZ '2026-05-21 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-21 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan 10 — Fase de Mejoras Integrales: Módulo Pollo de Engorde');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T4', 'BUG', 'LISTO', 'MEDIA',
           'Plan de Desarrollo — Fix SQL: saldo de apertura no debe heredar inventario de lote anterior', 'Plan: fase_de_desarrollo/12_fix_saldo_apertura_lote_anterior.md', v_user_guid, 3, DATE '2026-05-28', DATE '2026-05-28',
           TIMESTAMPTZ '2026-05-28 12:00:00+00', TIMESTAMPTZ '2026-05-28 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-28 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan de Desarrollo — Fix SQL: saldo de apertura no debe heredar inventario de lote anterior');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T5', 'BUG', 'LISTO', 'MEDIA',
           'Plan de Desarrollo — Fix: `fn_seguimiento_diario_engorde` (Saldos y Primer Ingreso)', 'Plan: fase_de_desarrollo/10_fix_fn_seguimiento_diario_engorde_saldos.md', v_user_guid, 4, DATE '2026-05-28', DATE '2026-05-28',
           TIMESTAMPTZ '2026-05-28 12:00:00+00', TIMESTAMPTZ '2026-05-28 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-28 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan de Desarrollo — Fix: `fn_seguimiento_diario_engorde` (Saldos y Primer Ingreso)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T6', 'BUG', 'LISTO', 'MEDIA',
           '18 — Validación y corrección masiva del saldo de alimento (pollo engorde, lotes ""2602"")', 'Plan: fase_de_desarrollo/18_validacion_correccion_saldo_alimento_engorde_2602.md', v_user_guid, 5, DATE '2026-06-01', DATE '2026-06-01',
           TIMESTAMPTZ '2026-06-01 12:00:00+00', TIMESTAMPTZ '2026-06-01 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-01 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = '18 — Validación y corrección masiva del saldo de alimento (pollo engorde, lotes ""2602"")');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T7', 'BUG', 'LISTO', 'MEDIA',
           'Plan — Validación y corrección de aves disponibles (lotes pollo engorde ""2601"")', 'Plan: fase_de_desarrollo/correccion_aves_disponibles_engorde_2601_plan.md', v_user_guid, 6, DATE '2026-06-10', DATE '2026-06-10',
           TIMESTAMPTZ '2026-06-10 12:00:00+00', TIMESTAMPTZ '2026-06-10 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-10 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Validación y corrección de aves disponibles (lotes pollo engorde ""2601"")');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T8', 'BUG', 'LISTO', 'MEDIA',
           'Plan — Corrección global de saldos de aves en lotes pollo engorde (caso ""2602"" / lote 73)', 'Plan: fase_de_desarrollo/correccion_saldos_engorde_2602_global_plan.md', v_user_guid, 7, DATE '2026-06-11', DATE '2026-06-11',
           TIMESTAMPTZ '2026-06-11 12:00:00+00', TIMESTAMPTZ '2026-06-11 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-11 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Corrección global de saldos de aves en lotes pollo engorde (caso ""2602"" / lote 73)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T9', 'TAREA', 'LISTO', 'MEDIA',
           '32 — Informe Semanal Pollo de Engorde (Panamá) — PLAN', 'Plan: fase_de_desarrollo/32_informe_semanal_pollo_engorde_panama_plan.md', v_user_guid, 8, DATE '2026-06-24', DATE '2026-06-24',
           TIMESTAMPTZ '2026-06-24 12:00:00+00', TIMESTAMPTZ '2026-06-24 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-24 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = '32 — Informe Semanal Pollo de Engorde (Panamá) — PLAN');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T10', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Validación y alineación de vistas Pollo Engorde Ecuador con sus funciones corregidas', 'Plan: fase_de_desarrollo/validacion_vistas_engorde_ecuador_plan.md', v_user_guid, 9, DATE '2026-06-24', DATE '2026-06-24',
           TIMESTAMPTZ '2026-06-24 12:00:00+00', TIMESTAMPTZ '2026-06-24 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-24 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Validación y alineación de vistas Pollo Engorde Ecuador con sus funciones corregidas');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T11', 'TAREA', 'LISTO', 'MEDIA',
           'Alimento a nivel galpón vs granja — CONFIGURABLE (empresa + granja)', 'Plan: fase_de_desarrollo/alimento_nivel_galpon_configurable_plan.md', v_user_guid, 10, DATE '2026-07-04', DATE '2026-07-04',
           TIMESTAMPTZ '2026-07-04 12:00:00+00', TIMESTAMPTZ '2026-07-04 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-04 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Alimento a nivel galpón vs granja — CONFIGURABLE (empresa + granja)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T12', 'MEJORA', 'LISTO', 'MEDIA',
           'Plan — Refactor SeguimientoAvesEngordeService (1884 líneas)', 'Plan: fase_de_desarrollo/refactor_seguimiento_aves_engorde_service_plan.md', v_user_guid, 11, DATE '2026-07-11', DATE '2026-07-11',
           TIMESTAMPTZ '2026-07-11 12:00:00+00', TIMESTAMPTZ '2026-07-11 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-11 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Refactor SeguimientoAvesEngordeService (1884 líneas)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T13', 'BUG', 'LISTO', 'MEDIA',
           'Plan — Fix: ""aves vivas"" (tabla diaria / liquidación) ignora mortalidad en caja (mort_caja_h/m)', 'Plan: fase_de_desarrollo/fix_aves_vivas_mort_caja_engorde_plan.md', v_user_guid, 12, DATE '2026-07-14', DATE '2026-07-14',
           TIMESTAMPTZ '2026-07-14 12:00:00+00', TIMESTAMPTZ '2026-07-14 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-14 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Fix: ""aves vivas"" (tabla diaria / liquidación) ignora mortalidad en caja (mort_caja_h/m)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T14', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Puente de consulta: migración/sincronización Pollo Engorde desde ZooPanamaPollo', 'Plan: fase_de_desarrollo/puente_panama_engorde_plan.md', v_user_guid, 13, DATE '2026-07-16', DATE '2026-07-16',
           TIMESTAMPTZ '2026-07-16 12:00:00+00', TIMESTAMPTZ '2026-07-16 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-16 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Puente de consulta: migración/sincronización Pollo Engorde desde ZooPanamaPollo');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T15', 'BUG', 'LISTO', 'MEDIA',
           'Fix: fechas muestran un día menos — módulo pollo engorde (lotes, reproductoras y seguimientos)', 'Plan: fase_de_desarrollo/fix_fecha_menos_un_dia_engorde_plan.md', v_user_guid, 14, DATE '2026-07-21', DATE '2026-07-21',
           TIMESTAMPTZ '2026-07-21 12:00:00+00', TIMESTAMPTZ '2026-07-21 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-21 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Fix: fechas muestran un día menos — módulo pollo engorde (lotes, reproductoras y seguimientos)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T16', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Reporte Diario Costos (Pollo Engorde) + Lote Base global', 'Plan: fase_de_desarrollo/reporte_diario_costos_engorde_plan.md', v_user_guid, 15, DATE '2026-07-21', DATE '2026-07-21',
           TIMESTAMPTZ '2026-07-21 12:00:00+00', TIMESTAMPTZ '2026-07-21 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-21 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Reporte Diario Costos (Pollo Engorde) + Lote Base global');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T17', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Numeración de corrida por lote base + galpón (Panamá) en Lote Pollo Engorde', 'Plan: fase_de_desarrollo/lote_engorde_corrida_panama_plan.md', v_user_guid, 16, DATE '2026-07-22', DATE '2026-07-22',
           TIMESTAMPTZ '2026-07-22 12:00:00+00', TIMESTAMPTZ '2026-07-22 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-22 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Numeración de corrida por lote base + galpón (Panamá) en Lote Pollo Engorde');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T18', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Unidad `qq` (quintal) en el alimento del seguimiento pollo engorde', 'Plan: fase_de_desarrollo/qq_a_kg_alimento_seguimiento_engorde_plan.md', v_user_guid, 17, DATE '2026-07-22', DATE '2026-07-22',
           TIMESTAMPTZ '2026-07-22 12:00:00+00', TIMESTAMPTZ '2026-07-22 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-22 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Unidad `qq` (quintal) en el alimento del seguimiento pollo engorde');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T19', 'TAREA', 'LISTO', 'MEDIA',
           'Código ERP de engorde a nivel GRANJA con avance automático al cerrar ciclo — Panamá', 'Plan: fase_de_desarrollo/codigo_erp_granja_engorde_panama_plan.md', v_user_guid, 18, DATE '2026-07-23', DATE '2026-07-23',
           TIMESTAMPTZ '2026-07-23 12:00:00+00', TIMESTAMPTZ '2026-07-23 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-23 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Código ERP de engorde a nivel GRANJA con avance automático al cerrar ciclo — Panamá');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T20', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — UX cascada numerada + info colapsable + scroll único en «Seguimiento diario pollo de engorde»', 'Plan: fase_de_desarrollo/seguimiento_pollo_engorde_ux_cascada_scroll_plan.md', v_user_guid, 19, DATE '2026-07-23', DATE '2026-07-23',
           TIMESTAMPTZ '2026-07-23 12:00:00+00', TIMESTAMPTZ '2026-07-23 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-23 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — UX cascada numerada + info colapsable + scroll único en «Seguimiento diario pollo de engorde»');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T21', 'MEJORA', 'LISTO', 'MEDIA',
           'Plan — Limpieza seguimientos diarios Panamá (reproductora + pollo engorde) para re-carga masiva', 'Plan: fase_de_desarrollo/limpieza_seguimientos_engorde_panama_plan.md', v_user_guid, 20, DATE '2026-07-25', DATE '2026-07-25',
           TIMESTAMPTZ '2026-07-25 12:00:00+00', TIMESTAMPTZ '2026-07-25 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-25 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Limpieza seguimientos diarios Panamá (reproductora + pollo engorde) para re-carga masiva');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T22', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Hora de encasetamiento: define el primer día con registro (engorde y reproductora)', 'Plan: fase_de_desarrollo/hora_encasetamiento_primer_registro_plan.md', v_user_guid, 21, DATE '2026-07-27', DATE '2026-07-27',
           TIMESTAMPTZ '2026-07-27 12:00:00+00', TIMESTAMPTZ '2026-07-27 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-27 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Hora de encasetamiento: define el primer día con registro (engorde y reproductora)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T23', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Pollo engorde: numeración de día 1-based y pesaje al cierre de semana', 'Plan: fase_de_desarrollo/dia_negocio_engorde_pesaje_plan.md', v_user_guid, 22, DATE '2026-07-27', DATE '2026-07-27',
           TIMESTAMPTZ '2026-07-27 12:00:00+00', TIMESTAMPTZ '2026-07-27 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-27 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Pollo engorde: numeración de día 1-based y pesaje al cierre de semana');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T24', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Seguimiento pollo engorde MIXTO (Panamá): Excel mixto + descuento de aves mixtas', 'Plan: fase_de_desarrollo/seguimiento_engorde_mixto_panama_plan.md', v_user_guid, 23, DATE '2026-07-27', DATE '2026-07-27',
           TIMESTAMPTZ '2026-07-27 12:00:00+00', TIMESTAMPTZ '2026-07-27 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-27 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Seguimiento pollo engorde MIXTO (Panamá): Excel mixto + descuento de aves mixtas');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T25', 'DOCUMENTACION', 'LISTO', 'MEDIA',
           'Diagnóstico — Saldo de alimento en pantalla ≠ stock (ItalcolEcuador)', 'Plan: fase_de_desarrollo/cuadre_engorde_ecuador_diagnostico_saldo_alimento.md', v_user_guid, 24, DATE '2026-07-29', DATE '2026-07-29',
           TIMESTAMPTZ '2026-07-29 12:00:00+00', TIMESTAMPTZ '2026-07-29 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-29 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Diagnóstico — Saldo de alimento en pantalla ≠ stock (ItalcolEcuador)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T26', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Cuadre de aves y alimento en pollo engorde (Panamá)', 'Plan: fase_de_desarrollo/cuadre_engorde_panama_aves_alimento_plan.md', v_user_guid, 25, DATE '2026-07-29', DATE '2026-07-29',
           TIMESTAMPTZ '2026-07-29 12:00:00+00', TIMESTAMPTZ '2026-07-29 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-29 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Cuadre de aves y alimento en pollo engorde (Panamá)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T27', 'DOCUMENTACION', 'LISTO', 'MEDIA',
           'Requerimiento — Cuadre de alimento y aves en pollo engorde (ItalcolEcuador)', 'Plan: fase_de_desarrollo/cuadre_engorde_ecuador_requerimiento.md', v_user_guid, 26, DATE '2026-07-29', DATE '2026-07-29',
           TIMESTAMPTZ '2026-07-29 12:00:00+00', TIMESTAMPTZ '2026-07-29 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-29 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Requerimiento — Cuadre de alimento y aves en pollo engorde (ItalcolEcuador)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T28', 'BUG', 'LISTO', 'MEDIA',
           'Plan — La apertura de alimento deja de heredar el ciclo anterior del galpón', 'Plan: fase_de_desarrollo/fix_apertura_alimento_ciclo_anterior_plan.md', v_user_guid, 27, DATE '2026-07-30', DATE '2026-07-30',
           TIMESTAMPTZ '2026-07-30 12:00:00+00', TIMESTAMPTZ '2026-07-30 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-30 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — La apertura de alimento deja de heredar el ciclo anterior del galpón');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T29', 'BUG', 'LISTO', 'MEDIA',
           'Plan — Que estos descuadres no se puedan repetir', 'Plan: fase_de_desarrollo/prevencion_descuadres_alimento_engorde_plan.md', v_user_guid, 28, DATE '2026-07-30', DATE '2026-07-30',
           TIMESTAMPTZ '2026-07-30 12:00:00+00', TIMESTAMPTZ '2026-07-30 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-30 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Que estos descuadres no se puedan repetir');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T30', 'DOCUMENTACION', 'LISTO', 'MEDIA',
           'Saldos de alimento en pollo engorde — estado real y qué queda por revisar', 'Plan: fase_de_desarrollo/INSTRUCTIVO_OPERACION_saldos_alimento_engorde.md', v_user_guid, 29, DATE '2026-07-30', DATE '2026-07-31',
           TIMESTAMPTZ '2026-07-30 12:00:00+00', TIMESTAMPTZ '2026-07-31 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-30 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Saldos de alimento en pollo engorde — estado real y qué queda por revisar');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T31', 'BUG', 'LISTO', 'MEDIA',
           'Corrección de la referencia `Inicio` + liquidación de corridas anteriores (pollo engorde)', 'Plan: fase_de_desarrollo/correccion_referencia_inicio_engorde_plan.md', v_user_guid, 30, DATE '2026-08-05', DATE '2026-08-05',
           TIMESTAMPTZ '2026-08-05 12:00:00+00', TIMESTAMPTZ '2026-08-05 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-08-05 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Corrección de la referencia `Inicio` + liquidación de corridas anteriores (pollo engorde)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T32', 'BUG', 'LISTO', 'MEDIA',
           'Fix — el borrado/edición de un seguimiento viejo infla el maestro de aves (pollo engorde)', 'Plan: fase_de_desarrollo/fix_baseline_bajas_seguimiento_engorde_plan.md', v_user_guid, 31, DATE '2026-08-05', DATE '2026-08-05',
           TIMESTAMPTZ '2026-08-05 12:00:00+00', TIMESTAMPTZ '2026-08-05 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-08-05 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Fix — el borrado/edición de un seguimiento viejo infla el maestro de aves (pollo engorde)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0012-T33', 'BUG', 'LISTO', 'MEDIA',
           'Plan — `tipo_alimento` desborda `varchar(100)` y tumba el guardado del seguimiento diario', 'Plan: fase_de_desarrollo/tipo_alimento_varchar_desborde_plan.md', v_user_guid, 32, DATE '2026-08-06', DATE '2026-08-06',
           TIMESTAMPTZ '2026-08-06 12:00:00+00', TIMESTAMPTZ '2026-08-06 18:00:00+00', 'engorde', v_company, v_cedula,
           TIMESTAMPTZ '2026-08-06 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — `tipo_alimento` desborda `varchar(100)` y tumba el guardado del seguimiento diario');

    -- ═══ Postura — Levante (7 trabajos) ═══
    INSERT INTO public.historias (codigo, pais_id, titulo, descripcion, estado, prioridad,
        responsable_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT 'HIS-2026-0013', v_pais, 'Postura — Levante', 'Historia derivada del historial real del repositorio: agrupa los trabajos con plan propio en fase_de_desarrollo/ correspondientes a este módulo.', 'LISTO', 'MEDIA',
           v_user_guid, 12, DATE '2026-05-08', DATE '2026-08-07',
           TIMESTAMPTZ '2026-05-08 12:00:00+00', TIMESTAMPTZ '2026-08-07 12:00:00+00', 'postura,levante', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-08 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.historias h WHERE h.codigo = 'HIS-2026-0013');

    SELECT id INTO v_hist FROM public.historias WHERE codigo = 'HIS-2026-0013';

    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0013-T1', 'DOCUMENTACION', 'LISTO', 'MEDIA',
           'Diferencia: Reporte LEVANTE vs PRODUCCIÓN', 'Plan: fase_de_desarrollo/DIFERENCIA_LEVANTE_VS_PRODUCCION.md', v_user_guid, 0, DATE '2026-05-08', DATE '2026-05-08',
           TIMESTAMPTZ '2026-05-08 12:00:00+00', TIMESTAMPTZ '2026-05-08 18:00:00+00', 'postura,levante', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-08 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Diferencia: Reporte LEVANTE vs PRODUCCIÓN');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0013-T2', 'DOCUMENTACION', 'LISTO', 'MEDIA',
           'IV. Diccionario de Datos y Mapeo Relacional - Fase Levante', 'Plan: fase_de_desarrollo/diccionario_datos_levante.md', v_user_guid, 1, DATE '2026-05-08', DATE '2026-05-08',
           TIMESTAMPTZ '2026-05-08 12:00:00+00', TIMESTAMPTZ '2026-05-08 18:00:00+00', 'postura,levante', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-08 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'IV. Diccionario de Datos y Mapeo Relacional - Fase Levante');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0013-T3', 'TAREA', 'LISTO', 'MEDIA',
           'Requerimiento: Reporte Técnico Levante', 'Plan: fase_de_desarrollo/01_req_reporte_levante.md', v_user_guid, 2, DATE '2026-05-08', DATE '2026-05-08',
           TIMESTAMPTZ '2026-05-08 12:00:00+00', TIMESTAMPTZ '2026-05-08 18:00:00+00', 'postura,levante', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-08 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Requerimiento: Reporte Técnico Levante');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0013-T4', 'TAREA', 'LISTO', 'MEDIA',
           'Plan C1 — Indicadores de levante (postura Colombia) → función SQL', 'Plan: fase_de_desarrollo/c1_indicadores_levante_a_sql_plan.md', v_user_guid, 3, DATE '2026-07-02', DATE '2026-07-02',
           TIMESTAMPTZ '2026-07-02 12:00:00+00', TIMESTAMPTZ '2026-07-02 18:00:00+00', 'postura,levante', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-02 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan C1 — Indicadores de levante (postura Colombia) → función SQL');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0013-T5', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Tab «Huevos» en Seguimiento Diario Levante (semana 14+) y arrastre automático a Producción al liquidar', 'Plan: fase_de_desarrollo/huevos_levante_semana14_arrastre_produccion_plan.md', v_user_guid, 4, DATE '2026-07-26', DATE '2026-07-26',
           TIMESTAMPTZ '2026-07-26 12:00:00+00', TIMESTAMPTZ '2026-07-26 18:00:00+00', 'postura,levante', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-26 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Tab «Huevos» en Seguimiento Diario Levante (semana 14+) y arrastre automático a Producción al liquidar');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0013-T6', 'DOCUMENTACION', 'LISTO', 'MEDIA',
           '🔄 CONTEXTO DE TRASPASO — Huevos en Seguimiento Levante (semana 14+) y arrastre a Producción', 'Plan: fase_de_desarrollo/CONTEXTO_TRASPASO_HUEVOS_LEVANTE.md', v_user_guid, 5, DATE '2026-07-26', DATE '2026-07-26',
           TIMESTAMPTZ '2026-07-26 12:00:00+00', TIMESTAMPTZ '2026-07-26 18:00:00+00', 'postura,levante', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-26 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = '🔄 CONTEXTO DE TRASPASO — Huevos en Seguimiento Levante (semana 14+) y arrastre a Producción');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0013-T7', 'TAREA', 'LISTO', 'MEDIA',
           'Tab «Indicadores» de Levante y Producción — validación contra la guía genética + unificación UX', 'Plan: fase_de_desarrollo/19_indicadores_levante_produccion_ux_plan.md', v_user_guid, 6, DATE '2026-08-07', DATE '2026-08-07',
           TIMESTAMPTZ '2026-08-07 12:00:00+00', TIMESTAMPTZ '2026-08-07 18:00:00+00', 'postura,levante', v_company, v_cedula,
           TIMESTAMPTZ '2026-08-07 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Tab «Indicadores» de Levante y Producción — validación contra la guía genética + unificación UX');

    -- ═══ Postura — Producción (13 trabajos) ═══
    INSERT INTO public.historias (codigo, pais_id, titulo, descripcion, estado, prioridad,
        responsable_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT 'HIS-2026-0014', v_pais, 'Postura — Producción', 'Historia derivada del historial real del repositorio: agrupa los trabajos con plan propio en fase_de_desarrollo/ correspondientes a este módulo.', 'LISTO', 'MEDIA',
           v_user_guid, 13, DATE '2026-05-08', DATE '2026-08-07',
           TIMESTAMPTZ '2026-05-08 12:00:00+00', TIMESTAMPTZ '2026-08-07 12:00:00+00', 'postura,produccion', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-08 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.historias h WHERE h.codigo = 'HIS-2026-0014');

    SELECT id INTO v_hist FROM public.historias WHERE codigo = 'HIS-2026-0014';

    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0014-T1', 'TAREA', 'LISTO', 'MEDIA',
           'Plan de Desarrollo — Reporte Técnico de Producción', 'Plan: fase_de_desarrollo/02_req_reporte_produccion.md', v_user_guid, 0, DATE '2026-05-08', DATE '2026-05-08',
           TIMESTAMPTZ '2026-05-08 12:00:00+00', TIMESTAMPTZ '2026-05-08 18:00:00+00', 'postura,produccion', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-08 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan de Desarrollo — Reporte Técnico de Producción');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0014-T2', 'TAREA', 'LISTO', 'MEDIA',
           '📋 Plan #16 — Mapeo Indicador Ecuador + Plan función SQL `fn_indicadores_pollo_engorde`', 'Plan: fase_de_desarrollo/16_mapeo_indicador_ecuador_y_plan_fn_sql.md', v_user_guid, 1, DATE '2026-05-29', DATE '2026-05-31',
           TIMESTAMPTZ '2026-05-29 12:00:00+00', TIMESTAMPTZ '2026-05-31 18:00:00+00', 'postura,produccion', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-29 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = '📋 Plan #16 — Mapeo Indicador Ecuador + Plan función SQL `fn_indicadores_pollo_engorde`');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0014-T3', 'BUG', 'LISTO', 'MEDIA',
           'Plan — Fix Ajuste de Aves / % Ajuste (Indicador Ecuador)', 'Plan: fase_de_desarrollo/indicador_ecuador_fix_ajuste_aves_plan.md', v_user_guid, 2, DATE '2026-06-25', DATE '2026-06-25',
           TIMESTAMPTZ '2026-06-25 12:00:00+00', TIMESTAMPTZ '2026-06-25 18:00:00+00', 'postura,produccion', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-25 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Fix Ajuste de Aves / % Ajuste (Indicador Ecuador)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0014-T4', 'TAREA', 'LISTO', 'MEDIA',
           'C2 — Indicadores de PRODUCCIÓN postura → función SQL', 'Plan: fase_de_desarrollo/c2_indicadores_produccion_a_sql.md', v_user_guid, 3, DATE '2026-07-02', DATE '2026-07-02',
           TIMESTAMPTZ '2026-07-02 12:00:00+00', TIMESTAMPTZ '2026-07-02 18:00:00+00', 'postura,produccion', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-02 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'C2 — Indicadores de PRODUCCIÓN postura → función SQL');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0014-T5', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Matriz Verenice rev 6-jul-26 · Postura Colombia (validación + corrección)', 'Plan: fase_de_desarrollo/postura_verenice_rev_6jul26_plan.md', v_user_guid, 4, DATE '2026-07-16', DATE '2026-07-16',
           TIMESTAMPTZ '2026-07-16 12:00:00+00', TIMESTAMPTZ '2026-07-16 18:00:00+00', 'postura,produccion', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-16 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Matriz Verenice rev 6-jul-26 · Postura Colombia (validación + corrección)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0014-T6', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Seguimiento Diario Producción: heredar Lote padre al cerrar Levante (Postura)', 'Plan: fase_de_desarrollo/seguimiento_produccion_hereda_lote_padre_plan.md', v_user_guid, 5, DATE '2026-07-22', DATE '2026-07-22',
           TIMESTAMPTZ '2026-07-22 12:00:00+00', TIMESTAMPTZ '2026-07-22 18:00:00+00', 'postura,produccion', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-22 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Seguimiento Diario Producción: heredar Lote padre al cerrar Levante (Postura)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0014-T7', 'BUG', 'LISTO', 'MEDIA',
           'Fix: Seguimiento diario de producción falla con ""El lote postura producción no tiene LoteId asociado"" (400)', 'Plan: fase_de_desarrollo/fix_seguimiento_produccion_lote_id_plan.md', v_user_guid, 6, DATE '2026-07-26', DATE '2026-07-26',
           TIMESTAMPTZ '2026-07-26 12:00:00+00', TIMESTAMPTZ '2026-07-26 18:00:00+00', 'postura,produccion', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-26 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Fix: Seguimiento diario de producción falla con ""El lote postura producción no tiene LoteId asociado"" (400)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0014-T8', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Demo vuelve a la clasificación de huevos CLÁSICA (Sanmarino) en seguimiento diario producción', 'Plan: fase_de_desarrollo/demo_huevos_clasico_sanmarino_plan.md', v_user_guid, 7, DATE '2026-07-26', DATE '2026-07-26',
           TIMESTAMPTZ '2026-07-26 12:00:00+00', TIMESTAMPTZ '2026-07-26 18:00:00+00', 'postura,produccion', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-26 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Demo vuelve a la clasificación de huevos CLÁSICA (Sanmarino) en seguimiento diario producción');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0014-T9', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Reporte Técnico Semanal Postura (Sanmarino): Levante + Producción vs Guía Genética', 'Plan: fase_de_desarrollo/reporte_tecnico_semanal_postura_plan.md', v_user_guid, 8, DATE '2026-07-26', DATE '2026-07-26',
           TIMESTAMPTZ '2026-07-26 12:00:00+00', TIMESTAMPTZ '2026-07-26 18:00:00+00', 'postura,produccion', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-26 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Reporte Técnico Semanal Postura (Sanmarino): Levante + Producción vs Guía Genética');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0014-T10', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Reporte Contable · sección ""Movimientos de Huevos"" dual-fuente (legacy + seguimiento_diario_produccion)', 'Plan: fase_de_desarrollo/reporte_contable_movimientos_huevos_dual_fuente_plan.md', v_user_guid, 9, DATE '2026-08-01', DATE '2026-08-01',
           TIMESTAMPTZ '2026-08-01 12:00:00+00', TIMESTAMPTZ '2026-08-01 18:00:00+00', 'postura,produccion', v_company, v_cedula,
           TIMESTAMPTZ '2026-08-01 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Reporte Contable · sección ""Movimientos de Huevos"" dual-fuente (legacy + seguimiento_diario_produccion)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0014-T11', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Seguimiento Diario de PRODUCCIÓN: fn SQL canónica + reducción de services + invariantes', 'Plan: fase_de_desarrollo/seguimiento_produccion_fn_canonica_plan.md', v_user_guid, 10, DATE '2026-08-01', DATE '2026-08-01',
           TIMESTAMPTZ '2026-08-01 12:00:00+00', TIMESTAMPTZ '2026-08-01 18:00:00+00', 'postura,produccion', v_company, v_cedula,
           TIMESTAMPTZ '2026-08-01 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Seguimiento Diario de PRODUCCIÓN: fn SQL canónica + reducción de services + invariantes');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0014-T12', 'DOCUMENTACION', 'LISTO', 'MEDIA',
           'Handoff — hallazgos de la sesión de postura (06-07 ago 2026)', 'Plan: fase_de_desarrollo/20_handoff_postura_hallazgos_sesion.md', v_user_guid, 11, DATE '2026-08-07', DATE '2026-08-07',
           TIMESTAMPTZ '2026-08-07 12:00:00+00', TIMESTAMPTZ '2026-08-07 18:00:00+00', 'postura,produccion', v_company, v_cedula,
           TIMESTAMPTZ '2026-08-07 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Handoff — hallazgos de la sesión de postura (06-07 ago 2026)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0014-T13', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Reconciliar el espejo `.sql` de `fn_indicadores_produccion_postura` + `uniformidad_guia` NULL', 'Plan: fase_de_desarrollo/reconciliacion_espejo_fn_indicadores_produccion_plan.md', v_user_guid, 12, DATE '2026-08-07', DATE '2026-08-07',
           TIMESTAMPTZ '2026-08-07 12:00:00+00', TIMESTAMPTZ '2026-08-07 18:00:00+00', 'postura,produccion', v_company, v_cedula,
           TIMESTAMPTZ '2026-08-07 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Reconciliar el espejo `.sql` de `fn_indicadores_produccion_postura` + `uniformidad_guia` NULL');

    -- ═══ Reportes, informes y tableros (8 trabajos) ═══
    INSERT INTO public.historias (codigo, pais_id, titulo, descripcion, estado, prioridad,
        responsable_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT 'HIS-2026-0015', v_pais, 'Reportes, informes y tableros', 'Historia derivada del historial real del repositorio: agrupa los trabajos con plan propio en fase_de_desarrollo/ correspondientes a este módulo.', 'LISTO', 'MEDIA',
           v_user_guid, 14, DATE '2026-05-08', DATE '2026-07-28',
           TIMESTAMPTZ '2026-05-08 12:00:00+00', TIMESTAMPTZ '2026-07-28 12:00:00+00', 'reportes,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-08 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.historias h WHERE h.codigo = 'HIS-2026-0015');

    SELECT id INTO v_hist FROM public.historias WHERE codigo = 'HIS-2026-0015';

    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0015-T1', 'TAREA', 'LISTO', 'MEDIA',
           'Fase 5 — Exportación de Reportes a Excel', 'Plan: fase_de_desarrollo/05_exportacion_excel_reportes.md', v_user_guid, 0, DATE '2026-05-08', DATE '2026-05-08',
           TIMESTAMPTZ '2026-05-08 12:00:00+00', TIMESTAMPTZ '2026-05-08 18:00:00+00', 'reportes,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-08 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Fase 5 — Exportación de Reportes a Excel');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0015-T2', 'TAREA', 'LISTO', 'MEDIA',
           'Plan de Desarrollo — Reportes con TABs y Diseño de Hojas', 'Plan: fase_de_desarrollo/03_req_reportes_tabs.md', v_user_guid, 1, DATE '2026-05-08', DATE '2026-05-08',
           TIMESTAMPTZ '2026-05-08 12:00:00+00', TIMESTAMPTZ '2026-05-08 18:00:00+00', 'reportes,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-08 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan de Desarrollo — Reportes con TABs y Diseño de Hojas');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0015-T3', 'TAREA', 'LISTO', 'MEDIA',
           'Validación de Campos — Excel vs Especificación Fase 4', 'Plan: fase_de_desarrollo/04_validacion_campos_excel.md', v_user_guid, 2, DATE '2026-05-08', DATE '2026-05-08',
           TIMESTAMPTZ '2026-05-08 12:00:00+00', TIMESTAMPTZ '2026-05-08 18:00:00+00', 'reportes,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-08 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Validación de Campos — Excel vs Especificación Fase 4');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0015-T4', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Pestaña ""Gráficas"" en Seguimiento Diario Pollo Engorde (Ecuador vs Panamá)', 'Plan: fase_de_desarrollo/21_graficas_productividad_panama_plan.md', v_user_guid, 3, DATE '2026-06-01', DATE '2026-06-01',
           TIMESTAMPTZ '2026-06-01 12:00:00+00', TIMESTAMPTZ '2026-06-01 18:00:00+00', 'reportes,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-01 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Pestaña ""Gráficas"" en Seguimiento Diario Pollo Engorde (Ecuador vs Panamá)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0015-T5', 'TAREA', 'LISTO', 'MEDIA',
           'DB Studio — Rediseño ""pro"", endurecimiento y permisos por tabla', 'Plan: fase_de_desarrollo/db_studio_plan.md', v_user_guid, 4, DATE '2026-06-07', DATE '2026-06-07',
           TIMESTAMPTZ '2026-06-07 12:00:00+00', TIMESTAMPTZ '2026-06-07 18:00:00+00', 'reportes,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-07 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'DB Studio — Rediseño ""pro"", endurecimiento y permisos por tabla');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0015-T6', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Migrar reportes de solo lectura a funciones SQL (PL/pgSQL)', 'Plan: fase_de_desarrollo/reportes_a_sql_plan.md', v_user_guid, 5, DATE '2026-07-02', DATE '2026-07-02',
           TIMESTAMPTZ '2026-07-02 12:00:00+00', TIMESTAMPTZ '2026-07-02 18:00:00+00', 'reportes,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-02 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Migrar reportes de solo lectura a funciones SQL (PL/pgSQL)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0015-T7', 'TAREA', 'LISTO', 'MEDIA',
           'DB Studio — Copia de seguridad completa descargable (SQL)', 'Plan: fase_de_desarrollo/db_studio_backup_descargable_plan.md', v_user_guid, 6, DATE '2026-07-14', DATE '2026-07-14',
           TIMESTAMPTZ '2026-07-14 12:00:00+00', TIMESTAMPTZ '2026-07-14 18:00:00+00', 'reportes,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-14 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'DB Studio — Copia de seguridad completa descargable (SQL)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0015-T8', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Informe RA Pesadas (Parámetros + Gráficos)', 'Plan: fase_de_desarrollo/informe_ra_pesadas_parametros_plan.md', v_user_guid, 7, DATE '2026-07-28', DATE '2026-07-28',
           TIMESTAMPTZ '2026-07-28 12:00:00+00', TIMESTAMPTZ '2026-07-28 18:00:00+00', 'reportes,excel', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-28 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Informe RA Pesadas (Parámetros + Gráficos)');

    -- ═══ Multi-empresa y multipaís (6 trabajos) ═══
    INSERT INTO public.historias (codigo, pais_id, titulo, descripcion, estado, prioridad,
        responsable_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT 'HIS-2026-0016', v_pais, 'Multi-empresa y multipaís', 'Historia derivada del historial real del repositorio: agrupa los trabajos con plan propio en fase_de_desarrollo/ correspondientes a este módulo.', 'LISTO', 'MEDIA',
           v_user_guid, 15, DATE '2026-05-27', DATE '2026-07-25',
           TIMESTAMPTZ '2026-05-27 12:00:00+00', TIMESTAMPTZ '2026-07-25 12:00:00+00', 'multiempresa,paises', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-27 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.historias h WHERE h.codigo = 'HIS-2026-0016');

    SELECT id INTO v_hist FROM public.historias WHERE codigo = 'HIS-2026-0016';

    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0016-T1', 'TAREA', 'LISTO', 'MEDIA',
           '16_migracion_lesiones_plan.md', 'Plan: fase_de_desarrollo/16_migracion_lesiones_plan.md', v_user_guid, 0, DATE '2026-05-27', DATE '2026-05-27',
           TIMESTAMPTZ '2026-05-27 12:00:00+00', TIMESTAMPTZ '2026-05-27 18:00:00+00', 'multiempresa,paises', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-27 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = '16_migracion_lesiones_plan.md');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0016-T2', 'TAREA', 'LISTO', 'MEDIA',
           'Plan de Desarrollo — Mejoras Panamá (Clientes, Zonas, Lesiones, Granja, Seguimiento Diario)', 'Plan: fase_de_desarrollo/panama_zona_clientes_lesiones_plan.md', v_user_guid, 1, DATE '2026-05-27', DATE '2026-05-27',
           TIMESTAMPTZ '2026-05-27 12:00:00+00', TIMESTAMPTZ '2026-05-27 18:00:00+00', 'multiempresa,paises', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-27 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan de Desarrollo — Mejoras Panamá (Clientes, Zonas, Lesiones, Granja, Seguimiento Diario)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0016-T3', 'MEJORA', 'LISTO', 'MEDIA',
           'Plan — Refactorización y optimización multi-país (rama `refactor/optimizacion-multipais`)', 'Plan: fase_de_desarrollo/refactor_multipais_optimizacion_plan.md', v_user_guid, 2, DATE '2026-07-01', DATE '2026-07-01',
           TIMESTAMPTZ '2026-07-01 12:00:00+00', TIMESTAMPTZ '2026-07-01 18:00:00+00', 'multiempresa,paises', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-01 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Refactorización y optimización multi-país (rama `refactor/optimizacion-multipais`)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0016-T4', 'DOCUMENTACION', 'LISTO', 'MEDIA',
           'Diagnóstico E2E multi-país — flujo completo por perfil', 'Plan: fase_de_desarrollo/diagnostico_e2e_paises.md', v_user_guid, 3, DATE '2026-07-02', DATE '2026-07-02',
           TIMESTAMPTZ '2026-07-02 12:00:00+00', TIMESTAMPTZ '2026-07-02 18:00:00+00', 'multiempresa,paises', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-02 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Diagnóstico E2E multi-país — flujo completo por perfil');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0016-T5', 'DOCUMENTACION', 'LISTO', 'MEDIA',
           '[ARCHIVADO 2026-07-03] Tracker — Fase 3 (paso 2/3): consumo Colombia modelo A → modelo B', 'Plan: fase_de_desarrollo/tracker_fase3_paso3_colombia_ARCHIVE.md', v_user_guid, 4, DATE '2026-07-04', DATE '2026-07-04',
           TIMESTAMPTZ '2026-07-04 12:00:00+00', TIMESTAMPTZ '2026-07-04 18:00:00+00', 'multiempresa,paises', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-04 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = '[ARCHIVADO 2026-07-03] Tracker — Fase 3 (paso 2/3): consumo Colombia modelo A → modelo B');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0016-T6', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Consolidar migraciones Santa Reyes + activar features en la empresa Demo', 'Plan: fase_de_desarrollo/activar_features_santa_reyes_demo_plan.md', v_user_guid, 5, DATE '2026-07-25', DATE '2026-07-25',
           TIMESTAMPTZ '2026-07-25 12:00:00+00', TIMESTAMPTZ '2026-07-25 18:00:00+00', 'multiempresa,paises', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-25 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Consolidar migraciones Santa Reyes + activar features en la empresa Demo');

    -- ═══ Sistema de diseño y experiencia de uso (4 trabajos) ═══
    INSERT INTO public.historias (codigo, pais_id, titulo, descripcion, estado, prioridad,
        responsable_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT 'HIS-2026-0017', v_pais, 'Sistema de diseño y experiencia de uso', 'Historia derivada del historial real del repositorio: agrupa los trabajos con plan propio en fase_de_desarrollo/ correspondientes a este módulo.', 'LISTO', 'MEDIA',
           v_user_guid, 16, DATE '2026-06-08', DATE '2026-07-25',
           TIMESTAMPTZ '2026-06-08 12:00:00+00', TIMESTAMPTZ '2026-07-25 12:00:00+00', 'ux,design-system', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-08 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.historias h WHERE h.codigo = 'HIS-2026-0017');

    SELECT id INTO v_hist FROM public.historias WHERE codigo = 'HIS-2026-0017';

    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0017-T1', 'TAREA', 'LISTO', 'MEDIA',
           'Plan: Separación logo → tabla `logo_companias` + Clean Code company-management', 'Plan: fase_de_desarrollo/28_logo_companias_separacion_tabla_plan.md', v_user_guid, 0, DATE '2026-06-08', DATE '2026-06-08',
           TIMESTAMPTZ '2026-06-08 12:00:00+00', TIMESTAMPTZ '2026-06-08 18:00:00+00', 'ux,design-system', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-08 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan: Separación logo → tabla `logo_companias` + Clean Code company-management');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0017-T2', 'MEJORA', 'LISTO', 'MEDIA',
           'Plan — Refactor UX/Visual PRO del Frontend (Angular) · Paleta Italcol naranja/rojo/blanco', 'Plan: fase_de_desarrollo/refactor_ux_pro_front_plan.md', v_user_guid, 1, DATE '2026-07-04', DATE '2026-07-04',
           TIMESTAMPTZ '2026-07-04 12:00:00+00', TIMESTAMPTZ '2026-07-04 18:00:00+00', 'ux,design-system', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-04 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Refactor UX/Visual PRO del Frontend (Angular) · Paleta Italcol naranja/rojo/blanco');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0017-T3', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Sistema de diseño compartido (`shared/ui/`) + reducción de duplicación front', 'Plan: fase_de_desarrollo/design_system_shared_ui_plan.md', v_user_guid, 2, DATE '2026-07-06', DATE '2026-07-06',
           TIMESTAMPTZ '2026-07-06 12:00:00+00', TIMESTAMPTZ '2026-07-06 18:00:00+00', 'ux,design-system', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-06 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Sistema de diseño compartido (`shared/ui/`) + reducción de duplicación front');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0017-T4', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Diseño unificado de filtros «Selección de contexto» en TODOS los módulos', 'Plan: fase_de_desarrollo/diseno_filtros_unificado_plan.md', v_user_guid, 3, DATE '2026-07-25', DATE '2026-07-25',
           TIMESTAMPTZ '2026-07-25 12:00:00+00', TIMESTAMPTZ '2026-07-25 18:00:00+00', 'ux,design-system', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-25 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Diseño unificado de filtros «Selección de contexto» en TODOS los módulos');

    -- ═══ Integraciones, correo y datos externos (2 trabajos) ═══
    INSERT INTO public.historias (codigo, pais_id, titulo, descripcion, estado, prioridad,
        responsable_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT 'HIS-2026-0018', v_pais, 'Integraciones, correo y datos externos', 'Historia derivada del historial real del repositorio: agrupa los trabajos con plan propio en fase_de_desarrollo/ correspondientes a este módulo.', 'LISTO', 'MEDIA',
           v_user_guid, 17, DATE '2026-05-31', DATE '2026-08-05',
           TIMESTAMPTZ '2026-05-31 12:00:00+00', TIMESTAMPTZ '2026-08-05 12:00:00+00', 'integraciones,correo', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-31 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.historias h WHERE h.codigo = 'HIS-2026-0018');

    SELECT id INTO v_hist FROM public.historias WHERE codigo = 'HIS-2026-0018';

    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0018-T1', 'DOCUMENTACION', 'LISTO', 'MEDIA',
           'Validación Entidades Backend ↔ Tablas de Base de Datos', 'Plan: fase_de_desarrollo/17_validacion_entidades_vs_bd_PARTE_A_mapeo.md', v_user_guid, 0, DATE '2026-05-31', DATE '2026-05-31',
           TIMESTAMPTZ '2026-05-31 12:00:00+00', TIMESTAMPTZ '2026-05-31 18:00:00+00', 'integraciones,correo', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-31 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Validación Entidades Backend ↔ Tablas de Base de Datos');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0018-T2', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — Migrar el envío de correo a Microsoft Graph API (retiro de auth básica SMTP)', 'Plan: fase_de_desarrollo/envio_correo_graph_api_plan.md', v_user_guid, 1, DATE '2026-08-05', DATE '2026-08-05',
           TIMESTAMPTZ '2026-08-05 12:00:00+00', TIMESTAMPTZ '2026-08-05 18:00:00+00', 'integraciones,correo', v_company, v_cedula,
           TIMESTAMPTZ '2026-08-05 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Migrar el envío de correo a Microsoft Graph API (retiro de auth básica SMTP)');

    -- ═══ Plataforma, deploy y deuda técnica (21 trabajos) ═══
    INSERT INTO public.historias (codigo, pais_id, titulo, descripcion, estado, prioridad,
        responsable_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT 'HIS-2026-0019', v_pais, 'Plataforma, deploy y deuda técnica', 'Historia derivada del historial real del repositorio: agrupa los trabajos con plan propio en fase_de_desarrollo/ correspondientes a este módulo.', 'LISTO', 'MEDIA',
           v_user_guid, 18, DATE '2026-05-08', DATE '2026-07-27',
           TIMESTAMPTZ '2026-05-08 12:00:00+00', TIMESTAMPTZ '2026-07-27 12:00:00+00', 'plataforma,devops', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-08 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.historias h WHERE h.codigo = 'HIS-2026-0019');

    SELECT id INTO v_hist FROM public.historias WHERE codigo = 'HIS-2026-0019';

    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0019-T1', 'MEJORA', 'LISTO', 'MEDIA',
           'Refactorización — Nombres de Tablas en BD', 'Plan: fase_de_desarrollo/06_refactorizacion_nombres_tablas.md', v_user_guid, 0, DATE '2026-05-08', DATE '2026-05-08',
           TIMESTAMPTZ '2026-05-08 12:00:00+00', TIMESTAMPTZ '2026-05-08 18:00:00+00', 'plataforma,devops', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-08 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Refactorización — Nombres de Tablas en BD');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0019-T2', 'TAREA', 'LISTO', 'MEDIA',
           'Plan de Desarrollo — Deploy Cross-Platform (Mac + Windows)', 'Plan: fase_de_desarrollo/08_deploy_cross_platform.md', v_user_guid, 1, DATE '2026-05-14', DATE '2026-05-14',
           TIMESTAMPTZ '2026-05-14 12:00:00+00', TIMESTAMPTZ '2026-05-14 18:00:00+00', 'plataforma,devops', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-14 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan de Desarrollo — Deploy Cross-Platform (Mac + Windows)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0019-T3', 'BUG', 'LISTO', 'MEDIA',
           'Plan: Corregir migración de clientes para producción', 'Plan: fase_de_desarrollo/15_fix_missing_clientes_migration_plan.md', v_user_guid, 2, DATE '2026-05-27', DATE '2026-05-27',
           TIMESTAMPTZ '2026-05-27 12:00:00+00', TIMESTAMPTZ '2026-05-27 18:00:00+00', 'plataforma,devops', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-27 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan: Corregir migración de clientes para producción');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0019-T4', 'TAREA', 'LISTO', 'MEDIA',
           'Plan de Desarrollo — Deploy AWS automático + Mostrar movimientos sin seguimiento diario', 'Plan: fase_de_desarrollo/14_deploy_aws_y_movs_sin_seguimiento.md', v_user_guid, 3, DATE '2026-05-28', DATE '2026-05-28',
           TIMESTAMPTZ '2026-05-28 12:00:00+00', TIMESTAMPTZ '2026-05-28 18:00:00+00', 'plataforma,devops', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-28 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan de Desarrollo — Deploy AWS automático + Mostrar movimientos sin seguimiento diario');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0019-T5', 'TAREA', 'LISTO', 'MEDIA',
           '📋 Spec para handoff — Seguimiento Diario Pollo Engorde (Ecuador)', 'Plan: fase_de_desarrollo/15_spec_para_otro_chat.md', v_user_guid, 4, DATE '2026-05-28', DATE '2026-05-28',
           TIMESTAMPTZ '2026-05-28 12:00:00+00', TIMESTAMPTZ '2026-05-28 18:00:00+00', 'plataforma,devops', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-28 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = '📋 Spec para handoff — Seguimiento Diario Pollo Engorde (Ecuador)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0019-T6', 'DOCUMENTACION', 'LISTO', 'MEDIA',
           '17 · Validación Entidades Backend ↔ Base de Datos · Alineación para Producción', 'Plan: fase_de_desarrollo/17_validacion_entidades_vs_bd_INDICE.md', v_user_guid, 5, DATE '2026-05-31', DATE '2026-05-31',
           TIMESTAMPTZ '2026-05-31 12:00:00+00', TIMESTAMPTZ '2026-05-31 18:00:00+00', 'plataforma,devops', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-31 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = '17 · Validación Entidades Backend ↔ Base de Datos · Alineación para Producción');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0019-T7', 'DOCUMENTACION', 'LISTO', 'MEDIA',
           'Validación Entidades ↔ BD — PARTE B: Auditoría de Funciones, Triggers y Vistas', 'Plan: fase_de_desarrollo/17_validacion_entidades_vs_bd_PARTE_B_auditoria.md', v_user_guid, 6, DATE '2026-05-31', DATE '2026-05-31',
           TIMESTAMPTZ '2026-05-31 12:00:00+00', TIMESTAMPTZ '2026-05-31 18:00:00+00', 'plataforma,devops', v_company, v_cedula,
           TIMESTAMPTZ '2026-05-31 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Validación Entidades ↔ BD — PARTE B: Auditoría de Funciones, Triggers y Vistas');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0019-T8', 'BUG', 'LISTO', 'MEDIA',
           'Fix — Migración `AddDbStudioGrantsAndAudit` rompe el arranque (AWS + local)', 'Plan: fase_de_desarrollo/fix_migracion_dbstudio_audit_plan.md', v_user_guid, 7, DATE '2026-06-10', DATE '2026-06-10',
           TIMESTAMPTZ '2026-06-10 12:00:00+00', TIMESTAMPTZ '2026-06-10 18:00:00+00', 'plataforma,devops', v_company, v_cedula,
           TIMESTAMPTZ '2026-06-10 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Fix — Migración `AddDbStudioGrantsAndAudit` rompe el arranque (AWS + local)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0019-T9', 'DOCUMENTACION', 'LISTO', 'MEDIA',
           'Validación — cómputo que puede pasar a la BD (agilizar, reducir consumo, front sin cálculos)', 'Plan: fase_de_desarrollo/candidatos_computo_a_bd.md', v_user_guid, 8, DATE '2026-07-02', DATE '2026-07-02',
           TIMESTAMPTZ '2026-07-02 12:00:00+00', TIMESTAMPTZ '2026-07-02 18:00:00+00', 'plataforma,devops', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-02 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Validación — cómputo que puede pasar a la BD (agilizar, reducir consumo, front sin cálculos)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0019-T10', 'DOCUMENTACION', 'LISTO', 'MEDIA',
           '🔄 CONTEXTO DE TRASPASO — sesión de refactor/optimización multi-país', 'Plan: fase_de_desarrollo/CONTEXTO_TRASPASO_SESION.md', v_user_guid, 9, DATE '2026-07-02', DATE '2026-07-02',
           TIMESTAMPTZ '2026-07-02 12:00:00+00', TIMESTAMPTZ '2026-07-02 18:00:00+00', 'plataforma,devops', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-02 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = '🔄 CONTEXTO DE TRASPASO — sesión de refactor/optimización multi-país');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0019-T11', 'TAREA', 'LISTO', 'MEDIA',
           'Fase 2 — Análisis de IMPACTO / QA (habilitar descuento de inventario en Colombia)', 'Plan: fase_de_desarrollo/fase2_impacto_qa.md', v_user_guid, 10, DATE '2026-07-03', DATE '2026-07-03',
           TIMESTAMPTZ '2026-07-03 12:00:00+00', TIMESTAMPTZ '2026-07-03 18:00:00+00', 'plataforma,devops', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-03 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Fase 2 — Análisis de IMPACTO / QA (habilitar descuento de inventario en Colombia)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0019-T12', 'TAREA', 'LISTO', 'MEDIA',
           'Fase 2 — Definición de Negocio: Colombia descuenta stock desde seguimientos', 'Plan: fase_de_desarrollo/fase2_negocio_definicion.md', v_user_guid, 11, DATE '2026-07-03', DATE '2026-07-03',
           TIMESTAMPTZ '2026-07-03 12:00:00+00', TIMESTAMPTZ '2026-07-03 18:00:00+00', 'plataforma,devops', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-03 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Fase 2 — Definición de Negocio: Colombia descuenta stock desde seguimientos');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0019-T13', 'TAREA', 'LISTO', 'MEDIA',
           'Fase 2 — Plan: Colombia descuenta stock desde seguimientos (acople de inventarios)', 'Plan: fase_de_desarrollo/fase2_plan.md', v_user_guid, 12, DATE '2026-07-03', DATE '2026-07-03',
           TIMESTAMPTZ '2026-07-03 12:00:00+00', TIMESTAMPTZ '2026-07-03 18:00:00+00', 'plataforma,devops', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-03 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Fase 2 — Plan: Colombia descuenta stock desde seguimientos (acople de inventarios)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0019-T14', 'TAREA', 'LISTO', 'MEDIA',
           'Fase 3 — Paso 2: switch de consumo de inventario de Colombia (modelo A → modelo B)', 'Plan: fase_de_desarrollo/fase3_paso2_plan.md', v_user_guid, 13, DATE '2026-07-03', DATE '2026-07-03',
           TIMESTAMPTZ '2026-07-03 12:00:00+00', TIMESTAMPTZ '2026-07-03 18:00:00+00', 'plataforma,devops', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-03 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Fase 3 — Paso 2: switch de consumo de inventario de Colombia (modelo A → modelo B)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0019-T15', 'MEJORA', 'LISTO', 'MEDIA',
           'Plan — Upgrade Angular 20 → 22 (+ refactor de deprecaciones)', 'Plan: fase_de_desarrollo/upgrade_angular_20_a_22_plan.md', v_user_guid, 14, DATE '2026-07-04', DATE '2026-07-04',
           TIMESTAMPTZ '2026-07-04 12:00:00+00', TIMESTAMPTZ '2026-07-04 18:00:00+00', 'plataforma,devops', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-04 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Upgrade Angular 20 → 22 (+ refactor de deprecaciones)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0019-T16', 'MEJORA', 'LISTO', 'MEDIA',
           'Plan — Upgrade backend .NET 9 → 10 (LTS)', 'Plan: fase_de_desarrollo/upgrade_dotnet_9_a_10_plan.md', v_user_guid, 15, DATE '2026-07-04', DATE '2026-07-04',
           TIMESTAMPTZ '2026-07-04 12:00:00+00', TIMESTAMPTZ '2026-07-04 18:00:00+00', 'plataforma,devops', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-04 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Upgrade backend .NET 9 → 10 (LTS)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0019-T17', 'TAREA', 'LISTO', 'MEDIA',
           'Plan: Commit de cambios actuales + Refactor de deuda técnica en backend', 'Plan: fase_de_desarrollo/commit_y_deuda_backend_plan.md', v_user_guid, 16, DATE '2026-07-10', DATE '2026-07-10',
           TIMESTAMPTZ '2026-07-10 12:00:00+00', TIMESTAMPTZ '2026-07-10 18:00:00+00', 'plataforma,devops', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-10 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan: Commit de cambios actuales + Refactor de deuda técnica en backend');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0019-T18', 'MEJORA', 'LISTO', 'MEDIA',
           'Plan — Reducción de deuda técnica: archivos largos (backend + frontend)', 'Plan: fase_de_desarrollo/refactor_archivos_largos_plan.md', v_user_guid, 17, DATE '2026-07-11', DATE '2026-07-12',
           TIMESTAMPTZ '2026-07-11 12:00:00+00', TIMESTAMPTZ '2026-07-12 18:00:00+00', 'plataforma,devops', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-11 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Reducción de deuda técnica: archivos largos (backend + frontend)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0019-T19', 'MEJORA', 'LISTO', 'MEDIA',
           'Plan — Limpieza del artefacto `$safeNavigationMigration(...)` en templates HTML', 'Plan: fase_de_desarrollo/limpieza_safe_navigation_migration_plan.md', v_user_guid, 18, DATE '2026-07-25', DATE '2026-07-25',
           TIMESTAMPTZ '2026-07-25 12:00:00+00', TIMESTAMPTZ '2026-07-25 18:00:00+00', 'plataforma,devops', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-25 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — Limpieza del artefacto `$safeNavigationMigration(...)` en templates HTML');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0019-T20', 'TAREA', 'LISTO', 'MEDIA',
           'Plan — PWA offline-first con sincronización diferida', 'Plan: fase_de_desarrollo/pwa_offline_first_plan.md', v_user_guid, 19, DATE '2026-07-26', DATE '2026-07-26',
           TIMESTAMPTZ '2026-07-26 12:00:00+00', TIMESTAMPTZ '2026-07-26 18:00:00+00', 'plataforma,devops', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-26 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Plan — PWA offline-first con sincronización diferida');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0019-T21', 'BUG', 'LISTO', 'MEDIA',
           'Fix — el deploy del frontend muere en el build de Docker (`MODULE_NOT_FOUND`)', 'Plan: fase_de_desarrollo/fix_deploy_frontend_dockerignore_plan.md', v_user_guid, 20, DATE '2026-07-27', DATE '2026-07-27',
           TIMESTAMPTZ '2026-07-27 12:00:00+00', TIMESTAMPTZ '2026-07-27 18:00:00+00', 'plataforma,devops', v_company, v_cedula,
           TIMESTAMPTZ '2026-07-27 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Fix — el deploy del frontend muere en el build de Docker (`MODULE_NOT_FOUND`)');

    -- ═══ ItalJira — gestión del proyecto (esta entrega) ═══
    INSERT INTO public.historias (codigo, pais_id, titulo, descripcion, estado, prioridad,
        responsable_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT 'HIS-2026-0020', v_pais, 'ItalJira — gestión del proyecto',
           'Centralizador de la gestión del área de desarrollo: historias, tareas, subtareas, tiempos y roadmap, fuera del módulo de Tickets.',
           'EN_CURSO', 'ALTA', v_user_guid, 19, DATE '2026-08-07', DATE '2026-08-07',
           TIMESTAMPTZ '2026-08-07 12:00:00+00', NULL, 'italjira,tickets', v_company, v_cedula,
           TIMESTAMPTZ '2026-08-07 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.historias h WHERE h.codigo = 'HIS-2026-0020');

    SELECT id INTO v_hist FROM public.historias WHERE codigo = 'HIS-2026-0020';

    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0020-T1', 'TAREA', 'LISTO', 'ALTA',
           'Modelo de historias, tareas y subtareas (tabla historias)', 'Plan: fase_de_desarrollo/italjira_gestion_historias_tareas_plan.md', v_user_guid, 0,
           DATE '2026-08-07', DATE '2026-08-07',
           TIMESTAMPTZ '2026-08-07 12:00:00+00', TIMESTAMPTZ '2026-08-07 18:00:00+00', 'italjira', v_company, v_cedula,
           TIMESTAMPTZ '2026-08-07 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Modelo de historias, tareas y subtareas (tabla historias)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0020-T2', 'TAREA', 'LISTO', 'ALTA',
           'Servicio y API de ItalJira: backlog, tablero y roadmap', 'Plan: fase_de_desarrollo/italjira_gestion_historias_tareas_plan.md', v_user_guid, 1,
           DATE '2026-08-07', DATE '2026-08-07',
           TIMESTAMPTZ '2026-08-07 12:00:00+00', TIMESTAMPTZ '2026-08-07 18:00:00+00', 'italjira', v_company, v_cedula,
           TIMESTAMPTZ '2026-08-07 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Servicio y API de ItalJira: backlog, tablero y roadmap');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0020-T3', 'TAREA', 'LISTO', 'ALTA',
           'Menú ItalJira fuera de Tickets (mudanza en sitio de las vistas de gestión)', 'Plan: fase_de_desarrollo/italjira_gestion_historias_tareas_plan.md', v_user_guid, 2,
           DATE '2026-08-07', DATE '2026-08-07',
           TIMESTAMPTZ '2026-08-07 12:00:00+00', TIMESTAMPTZ '2026-08-07 18:00:00+00', 'italjira', v_company, v_cedula,
           TIMESTAMPTZ '2026-08-07 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Menú ItalJira fuera de Tickets (mudanza en sitio de las vistas de gestión)');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0020-T4', 'TAREA', 'LISTO', 'ALTA',
           'Backlog visual: arbol historia - tarea - subtarea/bug', 'Plan: fase_de_desarrollo/italjira_gestion_historias_tareas_plan.md', v_user_guid, 3,
           DATE '2026-08-07', DATE '2026-08-07',
           TIMESTAMPTZ '2026-08-07 12:00:00+00', TIMESTAMPTZ '2026-08-07 18:00:00+00', 'italjira', v_company, v_cedula,
           TIMESTAMPTZ '2026-08-07 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Backlog visual: arbol historia - tarea - subtarea/bug');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, v_hist, 'HIS-2026-0020-T5', 'TAREA', 'LISTO', 'ALTA',
           'Histórico real del desarrollo sembrado por migración', 'Plan: fase_de_desarrollo/italjira_gestion_historias_tareas_plan.md', v_user_guid, 4,
           DATE '2026-08-07', DATE '2026-08-07',
           TIMESTAMPTZ '2026-08-07 12:00:00+00', TIMESTAMPTZ '2026-08-07 18:00:00+00', 'italjira', v_company, v_cedula,
           TIMESTAMPTZ '2026-08-07 12:00:00+00'
    WHERE NOT EXISTS (SELECT 1 FROM public.ticket_tareas x WHERE x.historia_id = v_hist AND x.titulo = 'Histórico real del desarrollo sembrado por migración');

END $$;

";
    }
}
