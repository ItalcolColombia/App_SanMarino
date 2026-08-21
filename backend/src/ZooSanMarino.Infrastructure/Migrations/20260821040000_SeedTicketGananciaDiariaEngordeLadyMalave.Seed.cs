using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Cuerpo SQL del seed del ticket de Lady Malave (ganancia diaria, seguimiento diario de pollo
    /// engorde). Vive en su propio archivo (<c>partial</c>) por tamaño: la documentación de qué
    /// hace y por qué está en <c>20260821040000_SeedTicketGananciaDiariaEngordeLadyMalave.cs</c>.
    /// </summary>
    /// <remarks>
    /// Los textos se escriben sin acentos, igual que el resto de los seeds del módulo de tickets.
    /// </remarks>
    public partial class SeedTicketGananciaDiariaEngordeLadyMalave
    {
        private const string SEED_SQL = @"
-- ─────────────────────────────────────────────────────────────────────────────
-- Ticket de Lady Malave: validacion de indicadores del seguimiento diario de
-- pollo engorde (ganancia diaria). Creado por ella, auto-asignado y ya
-- SOLUCIONADO por el administrador que aplico el fix.
-- ─────────────────────────────────────────────────────────────────────────────
DO $$
DECLARE
    c_tk_titulo constant varchar(200) :=
        'Validacion indicadores seguimiento diario pollo engorde: ganancia diaria no divide entre los dias de pesaje';

    v_admin_guid        uuid;
    v_admin_assigned_id integer;
    v_lady_guid         uuid;
    v_lady_ced          integer;
    v_company           integer;
    v_pais              integer;
    v_ticket_id         bigint;
    v_orden             integer;
    v_ahora             timestamptz := timezone('utc', now());
BEGIN
    -- ═══════════════ 0) ADMINISTRADOR: resolutor autoasignado ═══════════════
    -- Identidad POR EMAIL, nunca por guid fijo: los ids difieren entre local y produccion.
    SELECT u.id INTO v_admin_guid
    FROM public.users u
    JOIN public.user_logins ul ON ul.user_id = u.id
    JOIN public.logins l       ON l.id = ul.login_id
    WHERE lower(l.email) = 'moiesbbuga@gmail.com'
    LIMIT 1;

    -- Fail-open silencioso: sin el administrador no se siembra nada y la app arranca igual.
    IF v_admin_guid IS NULL THEN
        RAISE NOTICE 'Ticket ganancia diaria (Lady Malave): no existe moiesbbuga@gmail.com en este entorno; omitido.';
        RETURN;
    END IF;

    -- El int de auditoria del modulo NO es la cedula (puede no entrar en integer): se reusa el
    -- que ya usan sus propios casos como asignado; si no tiene ninguno, el de creador.
    SELECT t.assigned_to_user_id INTO v_admin_assigned_id
    FROM public.tickets t WHERE t.assigned_to_user_guid = v_admin_guid ORDER BY t.id DESC LIMIT 1;
    IF v_admin_assigned_id IS NULL THEN
        SELECT t.created_by_user_id INTO v_admin_assigned_id
        FROM public.tickets t WHERE t.created_by_user_guid = v_admin_guid ORDER BY t.id DESC LIMIT 1;
    END IF;
    IF v_admin_assigned_id IS NULL THEN
        SELECT CASE WHEN u.cedula ~ '^[0-9]{1,9}$' THEN u.cedula::integer ELSE 0 END
          INTO v_admin_assigned_id FROM public.users u WHERE u.id = v_admin_guid;
    END IF;
    v_admin_assigned_id := COALESCE(v_admin_assigned_id, 0);

    -- ═══════════════ 1) LADY MALAVE: solicitante y creadora del caso ═══════════════
    -- Por email (ecuitalcol) o, si no matchea, por nombre y apellido.
    SELECT u.id,
           CASE WHEN u.cedula ~ '^[0-9]{1,9}$' THEN u.cedula::integer ELSE NULL END
      INTO v_lady_guid, v_lady_ced
    FROM public.users u
    LEFT JOIN public.user_logins ul ON ul.user_id = u.id
    LEFT JOIN public.logins l       ON l.id = ul.login_id
    WHERE lower(coalesce(l.email, '')) LIKE '%ladymalave%'
       OR (lower(coalesce(u.first_name, '')) LIKE '%lady%'
           AND lower(coalesce(u.sur_name, '')) LIKE '%malave%')
    ORDER BY u.created_at, u.id
    LIMIT 1;

    -- Sin Lady Malave el caso no tiene dueño: a diferencia del solicitante delegado (opcional)
    -- del seed de Santa Reyes, aca la solicitante ES la creadora — fail-open, no se siembra nada.
    IF v_lady_guid IS NULL THEN
        RAISE NOTICE 'Ticket ganancia diaria: no se encontro a Lady Malave en este entorno; omitido.';
        RETURN;
    END IF;

    IF v_lady_ced IS NULL THEN
        SELECT t.created_by_user_id INTO v_lady_ced
        FROM public.tickets t WHERE t.created_by_user_guid = v_lady_guid ORDER BY t.id DESC LIMIT 1;
    END IF;
    v_lady_ced := COALESCE(v_lady_ced, 0);

    -- ═══════════════ 2) EMPRESA Y PAIS DEL CASO: los de Lady, no los del administrador ═══════════════
    SELECT uc.company_id, uc.pais_id INTO v_company, v_pais
    FROM public.user_companies uc
    WHERE uc.user_id = v_lady_guid
    ORDER BY uc.is_default DESC, uc.company_id
    LIMIT 1;

    IF v_company IS NULL THEN
        SELECT t.company_id, t.pais_id INTO v_company, v_pais
        FROM public.tickets t WHERE t.created_by_user_guid = v_lady_guid ORDER BY t.id DESC LIMIT 1;
    END IF;
    v_company := COALESCE(v_company, 1);
    v_pais    := COALESCE(v_pais, 1);

    -- ═══════════════ 3) CASO (ticket) — creado por Lady, ya SOLUCIONADO ═══════════════
    SELECT t.id INTO v_ticket_id
    FROM public.tickets t
    WHERE t.titulo = c_tk_titulo AND t.deleted_at IS NULL
    LIMIT 1;

    IF v_ticket_id IS NULL THEN
        SELECT COALESCE(MAX(t.orden_tablero) + 1, 0) INTO v_orden
        FROM public.tickets t WHERE t.estado = 'SOLUCIONADO' AND t.deleted_at IS NULL;

        INSERT INTO public.tickets
            (pais_id, tipo, estado, titulo, descripcion,
             assigned_to_user_guid, assigned_to_user_id,
             created_by_user_guid,
             fecha_primera_apertura, fecha_solucion, solucion_descripcion,
             prioridad, orden_tablero, status, notificado_correo,
             company_id, created_by_user_id, created_at)
        VALUES
            (v_pais, 'SOPORTE', 'SOLUCIONADO', c_tk_titulo,
             'Validacion de indicadores del modulo de seguimiento diario de pollo engorde (tabla comparativo Registro vs Guia Ecuador mixto), reportada por Lady Malave:

- Peso corporal: OK.
- Ganancia diaria: REQUIERE VALIDACION. Los pesajes se realizan a diario durante la 1ra semana y luego cada 4 dias; cuando el pesaje deja de ser diario la formula debe ser (peso del dia - peso del dia anterior) / 4, y el calculo actual es (peso del dia - peso del dia anterior), sin dividir.
- Alimento diario: OK.
- Alimento acumulado: OK.
- Conversion: OK.
- Mortalidad + seleccion: OK.

El area de Costos necesita este indicador correcto para poder hacerle seguimiento.

Recomendacion validada para generalizar la regla (no asumir siempre 4 dias fijos): comparar el peso actual, cuando lo tiene, contra el ULTIMO peso efectivamente registrado antes de ese, dividiendo entre los dias reales transcurridos entre ambos pesajes.',
             v_admin_guid, v_admin_assigned_id,
             v_lady_guid,
             v_ahora, v_ahora,
             'Se corrigio el calculo de ganancia diaria (g) de la tabla de indicadores diarios de pollo engorde (comparativo Registro vs Guia Ecuador mixto).

CAUSA. El calculo ya comparaba el peso del dia contra el ULTIMO peso efectivamente registrado (no contra el dia calendario anterior), pero no dividia esa diferencia entre los dias transcurridos desde ese ultimo pesaje. En los tramos donde el pesaje pasa a ser cada 4 dias (despues de la 1ra semana, que se pesa a diario), la tabla mostraba de golpe la ganancia ACUMULADA de 4 dias en la fila de un solo dia, muy por encima de la columna Guia.

FIX. Se agrego el seguimiento del dia de vida del ultimo pesaje real junto al ultimo peso registrado, y la ganancia diaria ahora se calcula como (peso actual - ultimo peso registrado) / dias reales transcurridos desde ese pesaje. Con pesaje diario (1ra semana) el divisor es 1 y el resultado no cambia; con pesaje cada 4 dias el divisor es 4; y se generaliza a cualquier otro intervalo real (no queda un /4 fijo), siguiendo la recomendacion de comparar siempre contra el ultimo peso realmente tomado.

ARCHIVO. frontend/src/app/features/engorde-comun/services/indicadores-diarios-engorde-compute.service.ts (unico calculo real de esta tabla; el servicio homologo de aves-engorde es un re-export del mismo archivo, no una segunda implementacion).

VALIDACION. yarn build (0 errores) + tests nuevos del servicio (pesaje diario sin cambio, pesaje cada 4 dias, un intervalo distinto de 4, un dia sin peso registrado en medio del tramo, primer pesaje contra el peso inicial del lote): los 5 casos pasan.',
             'ALTA', v_orden, 'A', false,
             v_company, v_lady_ced, v_ahora)
        RETURNING id INTO v_ticket_id;

        UPDATE public.tickets
           SET codigo = 'TK-2026-' || lpad(v_ticket_id::text, 6, '0')
         WHERE id = v_ticket_id;
    END IF;

    RAISE NOTICE 'Ticket ganancia diaria (Lady Malave) sembrado: caso % / empresa %', v_ticket_id, v_company;
END $$;
";

        private const string DOWN_SQL = @"
DO $$
DECLARE
    c_tk_titulo constant varchar(200) :=
        'Validacion indicadores seguimiento diario pollo engorde: ganancia diaria no divide entre los dias de pesaje';
BEGIN
    DELETE FROM public.tickets WHERE titulo = c_tk_titulo;
END $$;
";
    }
}
