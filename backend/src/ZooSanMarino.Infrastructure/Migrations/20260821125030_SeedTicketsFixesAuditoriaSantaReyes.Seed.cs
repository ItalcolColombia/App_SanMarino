using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Cuerpo SQL del seed de los dos casos de la auditoría de no-regresión de Santa Reyes sobre
    /// postura (21 de agosto de 2026). Vive en su propio archivo (<c>partial</c>) por tamaño: la
    /// documentación de qué hace y por qué está en
    /// <c>20260821125030_SeedTicketsFixesAuditoriaSantaReyes.cs</c>.
    /// </summary>
    /// <remarks>
    /// Los textos se escriben sin acentos, igual que el resto de los seeds del módulo de tickets.
    /// </remarks>
    public partial class SeedTicketsFixesAuditoriaSantaReyes
    {
        private const string SEED_SQL = @"
-- ─────────────────────────────────────────────────────────────────────────────
-- Dos casos de soporte, ya SOLUCIONADOS y CERRADOS, de la auditoria de no-regresion
-- de Santa Reyes sobre el modulo de postura (21 de agosto de 2026). Creados por el
-- propio administrador (moiesbbuga@gmail.com), a nombre de la empresa Santa Reyes.
-- ─────────────────────────────────────────────────────────────────────────────
DO $$
DECLARE
    v_admin_guid uuid;
    v_admin_ced  integer;
    v_sr_company integer;
    v_pais       integer;
    v_ahora      timestamptz := timezone('utc', now());

    c_tk1_titulo constant varchar(200) :=
        'Traslado de huevos: el total queda desactualizado al editar cantidades de un traslado pendiente';
    c_tk2_titulo constant varchar(200) :=
        'Reporte Tecnico Produccion: la guia genetica compartida no filtraba por empresa';

    v_ticket_id  bigint;
    v_orden      integer;
BEGIN
    -- ═══════════════ 0) ADMINISTRADOR: creador, solicitante y resolutor de los dos casos ═══════════════
    -- Identidad POR EMAIL, nunca por guid fijo: los ids difieren entre local y produccion.
    SELECT u.id INTO v_admin_guid
    FROM public.users u
    JOIN public.user_logins ul ON ul.user_id = u.id
    JOIN public.logins l       ON l.id = ul.login_id
    WHERE lower(l.email) = 'moiesbbuga@gmail.com'
    LIMIT 1;

    -- Fail-open silencioso: sin el administrador no se siembra nada y la app arranca igual.
    IF v_admin_guid IS NULL THEN
        RAISE NOTICE 'Tickets auditoria Santa Reyes: no existe moiesbbuga@gmail.com en este entorno; omitido.';
        RETURN;
    END IF;

    -- El int de auditoria del modulo NO es la cedula (puede no entrar en integer): se reusa el
    -- que ya usan sus propios casos.
    SELECT t.created_by_user_id INTO v_admin_ced
    FROM public.tickets t WHERE t.created_by_user_guid = v_admin_guid ORDER BY t.id DESC LIMIT 1;
    IF v_admin_ced IS NULL THEN
        SELECT CASE WHEN u.cedula ~ '^[0-9]{1,9}$' THEN u.cedula::integer ELSE 0 END
          INTO v_admin_ced FROM public.users u WHERE u.id = v_admin_guid;
    END IF;
    v_admin_ced := COALESCE(v_admin_ced, 0);

    -- ═══════════════ 1) EMPRESA SANTA REYES: forzada por nombre, nunca por id ═══════════════
    -- El pedido es explicito: el caso es PARA Santa Reyes (el modulo auditado es el que ellos usan),
    -- no para la empresa por defecto del administrador que lo crea.
    SELECT c.id INTO v_sr_company
    FROM public.companies c
    WHERE lower(c.name) LIKE '%santa%reyes%'
    ORDER BY c.id
    LIMIT 1;

    IF v_sr_company IS NULL THEN
        RAISE NOTICE 'Tickets auditoria Santa Reyes: no existe la empresa Santa Reyes en este entorno; omitido.';
        RETURN;
    END IF;

    SELECT uc.pais_id INTO v_pais
    FROM public.user_companies uc
    WHERE uc.company_id = v_sr_company AND uc.pais_id IS NOT NULL
    LIMIT 1;
    v_pais := COALESCE(v_pais, 1);

    -- ═══════════════ 2) CASO 1 — TrasladoHuevos.TotalHuevos obsoleto al editar un traslado pendiente ═══════════════
    SELECT t.id INTO v_ticket_id
    FROM public.tickets t
    WHERE t.titulo = c_tk1_titulo AND t.deleted_at IS NULL
    LIMIT 1;

    IF v_ticket_id IS NULL THEN
        SELECT COALESCE(MAX(t.orden_tablero) + 1, 0) INTO v_orden
        FROM public.tickets t WHERE t.estado = 'CERRADO' AND t.deleted_at IS NULL;

        INSERT INTO public.tickets
            (pais_id, tipo, estado, titulo, descripcion,
             assigned_to_user_guid, assigned_to_user_id,
             created_by_user_guid,
             fecha_primera_apertura, fecha_solucion, solucion_descripcion,
             fecha_cierre_solicitante, cerrado_por_user_id,
             prioridad, orden_tablero, status, notificado_correo,
             company_id, created_by_user_id, created_at)
        VALUES
            (v_pais, 'SOPORTE', 'CERRADO', c_tk1_titulo,
             'NOVEDAD DETECTADA (auditoria de no-regresion de Santa Reyes sobre el modulo de postura, 21 de agosto de 2026). Al implementar la Fase F10 de Santa Reyes (traslado de huevos por items, commit 650f43a), TrasladoHuevos.TotalHuevos paso de ser una propiedad calculada en vivo (suma de las 11 columnas legacy, siempre al dia) a una columna persistida, fijada solo al crear el traslado.

CAUSA. ActualizarTrasladoHuevosAsync permite editar las 11 cantidades de un traslado en estado Pendiente, pero nunca recalculaba TotalHuevos despues de aplicar el cambio. El camino de edicion esta conectado en el frontend (modal-traslado-huevos.component.ts). Si se editaban las cantidades de un traslado pendiente antes de completarlo, el total quedaba con el valor de la creacion, no el editado.

IMPACTO. No es exclusivo de Santa Reyes: afecta a cualquier empresa que edite un traslado de huevos pendiente antes de completarlo. El total obsoleto se usa al completar el traslado en tres lugares: el espejo de produccion (EspejoHuevoProduccionSyncService), el descuento de produccion diaria (AplicarDescuentoEnProduccionDiariaAsync) y el listado (columna Total huevos y los resumenes Ventas completadas / Traslados completados).',
             v_admin_guid, v_admin_ced,
             v_admin_guid,
             v_ahora, v_ahora,
             'Se agrego el recalculo de TotalHuevos dentro de ActualizarTrasladoHuevosAsync (TrasladoHuevosService.cs), inmediatamente despues de aplicar los cambios a las 11 cantidades legacy: si el traslado edita alguna cantidad y no usa el desglose por items (Metadata == null), TotalHuevos se recalcula como la suma de las 11 columnas ya actualizadas, antes de guardar.

Los traslados que usan el desglose por items (empresas con clasificacion_huevo_por_items=true) no se ven afectados por este cambio: sus 11 columnas legacy son 0 por diseno y su total sigue viniendo de HuevoItemsCalculos.SumarTotal, fijado en la creacion.

ARCHIVO. backend/src/ZooSanMarino.Infrastructure/Services/TrasladoHuevosService.cs (metodo ActualizarTrasladoHuevosAsync).

VALIDACION. dotnet build (0 errores, mismos 21 warnings preexistentes) + dotnet test (2975/2975, sin regresiones).',
             v_ahora, v_admin_ced,
             'ALTA', v_orden, 'A', false,
             v_sr_company, v_admin_ced, v_ahora)
        RETURNING id INTO v_ticket_id;

        UPDATE public.tickets
           SET codigo = 'TK-2026-' || lpad(v_ticket_id::text, 6, '0')
         WHERE id = v_ticket_id;
    END IF;

    RAISE NOTICE 'Ticket TotalHuevos obsoleto sembrado: caso % / empresa %', v_ticket_id, v_sr_company;

    -- ═══════════════ 3) CASO 2 — guia genetica compartida sin filtro de empresa (preexistente) ═══════════════
    v_ticket_id := NULL;

    SELECT t.id INTO v_ticket_id
    FROM public.tickets t
    WHERE t.titulo = c_tk2_titulo AND t.deleted_at IS NULL
    LIMIT 1;

    IF v_ticket_id IS NULL THEN
        SELECT COALESCE(MAX(t.orden_tablero) + 1, 0) INTO v_orden
        FROM public.tickets t WHERE t.estado = 'CERRADO' AND t.deleted_at IS NULL;

        INSERT INTO public.tickets
            (pais_id, tipo, estado, titulo, descripcion,
             assigned_to_user_guid, assigned_to_user_id,
             created_by_user_guid,
             fecha_primera_apertura, fecha_solucion, solucion_descripcion,
             fecha_cierre_solicitante, cerrado_por_user_id,
             prioridad, orden_tablero, status, notificado_correo,
             company_id, created_by_user_id, created_at)
        VALUES
            (v_pais, 'SOPORTE', 'CERRADO', c_tk2_titulo,
             'NOVEDAD DETECTADA (auditoria de no-regresion de Santa Reyes sobre el modulo de postura, 21 de agosto de 2026). A diferencia del caso anterior, este bug es PREEXISTENTE y no lo causo Santa Reyes — aparecio al auditar el mismo modulo.

CAUSA. ReporteTecnicoProduccionService.cs tenia 3 consultas directas a la guia genetica compartida (ProduccionAvicolaRaw) sin filtrar por company_id, solo por raza y por anio. El consumidor (ObtenerGuiaParaSemana) toma el primer resultado que devuelve la consulta, sin ORDER BY.

IMPACTO CONFIRMADO CON DATOS REALES de la base local. Sanmarino (empresa 1) y Demo (empresa 4) comparten raza AP y anio de guia 2026, con 77 de 77 valores de semana solapados entre las dos: el Reporte Tecnico de Produccion de Sanmarino podia mostrar silenciosamente valores de guia genetica (porcentaje de produccion, peso de huevo, huevos totales por ave, uniformidad) que en realidad pertenecen a Demo, o al reves, dependiendo de que fila devolviera el plan de ejecucion de Postgres en cada consulta.',
             v_admin_guid, v_admin_ced,
             v_admin_guid,
             v_ahora, v_ahora,
             'Se agrego el filtro p.CompanyId == _currentUser.CompanyId a las 3 consultas a ProduccionAvicolaRaw en ReporteTecnicoProduccionService.cs (lineas ~1107, ~1373 y ~1716), igual que ya lo hace el resto de las consultas de ese mismo archivo y que ReporteTecnicoService.cs (que ya estaba filtrado correctamente). Cada empresa ve ahora unicamente su propia guia genetica en este reporte.

ARCHIVO. backend/src/ZooSanMarino.Infrastructure/Services/ReporteTecnicoProduccionService.cs.

VALIDACION. dotnet build (0 errores, mismos 21 warnings preexistentes) + dotnet test (2975/2975, sin regresiones).',
             v_ahora, v_admin_ced,
             'CRITICA', v_orden, 'A', false,
             v_sr_company, v_admin_ced, v_ahora)
        RETURNING id INTO v_ticket_id;

        UPDATE public.tickets
           SET codigo = 'TK-2026-' || lpad(v_ticket_id::text, 6, '0')
         WHERE id = v_ticket_id;
    END IF;

    RAISE NOTICE 'Ticket guia genetica sin company_id sembrado: caso % / empresa %', v_ticket_id, v_sr_company;
END $$;
";

        private const string DOWN_SQL = @"
DO $$
DECLARE
    c_tk1_titulo constant varchar(200) :=
        'Traslado de huevos: el total queda desactualizado al editar cantidades de un traslado pendiente';
    c_tk2_titulo constant varchar(200) :=
        'Reporte Tecnico Produccion: la guia genetica compartida no filtraba por empresa';
BEGIN
    DELETE FROM public.tickets WHERE titulo IN (c_tk1_titulo, c_tk2_titulo);
END $$;
";
    }
}
