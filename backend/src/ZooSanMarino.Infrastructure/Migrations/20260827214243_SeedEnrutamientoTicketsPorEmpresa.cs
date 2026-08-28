using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Enrutamiento de tickets por empresa (Sanmarino, Panamá, Ecuador). Plan:
    /// <c>fase_de_desarrollo/enrutamiento_tickets_por_empresa_plan.md</c>.
    ///
    /// <para>
    /// <b>Sanmarino</b>: SOPORTE/DUDAS → rol "Sistemas sanmarino" (ya existe, ya tiene
    /// <c>tickets.gestionar</c> y los 3 menús — solo le faltaba la regla de enrutamiento).
    /// REQUERIMIENTO → Verenice Morales (ya lo tenía como resolutor directo, pero también
    /// SOPORTE/DUDAS/DESARROLLO de más — se apagan esos tres). Su rol
    /// ("Implementador Sanmarino Colombia") no tenía <c>tickets.gestionar</c>: sin él no puede
    /// gestionar ni sus propios requerimientos (<c>TicketService.PuedeGestionar()</c>).
    /// </para>
    ///
    /// <para>
    /// <b>Panamá</b>: SOPORTE/DUDAS → rol "sistemas panama" (ya existe, sin nadie asignado
    /// todavía — decisión del usuario). Le faltaba <c>tickets.gestionar</c> (permiso de rol Y
    /// habilitado a nivel empresa, hoy apagado) y el menú "Bandeja de gestión". REQUERIMIENTO →
    /// Ricardo De la Rosa (implementador de Panamá); su rol "Admin Panama" ya tiene
    /// <c>tickets.admin</c>, así que ya es IMPLEMENTADOR y ya puede gestionar — solo le faltaba
    /// el resolutor.
    /// </para>
    ///
    /// <para>
    /// <b>Ecuador</b>: sin área de sistemas separada — SOPORTE + DUDAS + REQUERIMIENTO van los
    /// tres a Lady Malave. Su perfil (<c>ticket_perfil_usuario</c>) estaba guardado en
    /// <c>company_id</c> de Sanmarino por error (se apaga esa fila y se crea la de Ecuador). Su
    /// rol "Ecuador Administrador" tampoco tenía <c>tickets.gestionar</c> (mismo problema que
    /// Verenice) ni la empresa lo tenía habilitado.
    /// </para>
    ///
    /// <para>
    /// <b>DESARROLLO (atención global, moiesbbuga@gmail.com) no se toca acá</b>: el rol Admin
    /// (id 1) ya es resolutor de DESARROLLO en Sanmarino/Ecuador/Demo/Panamá desde antes
    /// (<c>ticket_resolutor_rol</c>). Ninguna de las tres empresas de este ticket necesita una
    /// fila nueva. Falta Santa Reyes, pero no se pidió — no se toca.
    /// </para>
    ///
    /// Migración DATA-ONLY: no cambia el modelo (diff contra el snapshot = 0 líneas fuera de este
    /// archivo). Todo localizado por nombre/email (los ids difieren entre entornos), idempotente
    /// (<c>WHERE NOT EXISTS</c> / <c>ON CONFLICT</c>), con guarda que aborta si algún id base no
    /// se resuelve en vez de insertar con NULL.
    /// </summary>
    public partial class SeedEnrutamientoTicketsPorEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(UP_SQL);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DOWN_SQL);
        }

        private const string UP_SQL = @"
DO $mig$
DECLARE
    v_company_sanmarino int;
    v_company_panama    int;
    v_company_ecuador   int;

    v_role_sistemas_sanmarino      int;
    v_role_sistemas_panama         int;
    v_role_implementador_sanmarino int;
    v_role_ecuador_administrador   int;

    v_user_verenice   uuid;
    v_user_ricardo    uuid;
    v_user_ladymalave uuid;

    v_perm_tickets_gestionar int;
    v_menu_bandeja_gestion   int;
BEGIN
    SELECT id INTO v_company_sanmarino FROM public.companies WHERE name = 'Agroavicola Sanmarino';
    SELECT id INTO v_company_panama    FROM public.companies WHERE name = 'ItalcolPanama';
    SELECT id INTO v_company_ecuador   FROM public.companies WHERE name = 'ItalcolEcuador';

    SELECT id INTO v_role_sistemas_sanmarino      FROM public.roles WHERE name = 'Sistemas sanmarino';
    SELECT id INTO v_role_sistemas_panama         FROM public.roles WHERE name = 'sistemas panama';
    SELECT id INTO v_role_implementador_sanmarino FROM public.roles WHERE name = 'Implementador Sanmarino Colombia';
    SELECT id INTO v_role_ecuador_administrador   FROM public.roles WHERE name = 'Ecuador Administrador';

    SELECT u.id INTO v_user_verenice
    FROM public.users u JOIN public.user_logins ul ON ul.user_id = u.id JOIN public.logins l ON l.id = ul.login_id
    WHERE l.email = 'verenicemorales@sanmarino.com.co';

    SELECT u.id INTO v_user_ricardo
    FROM public.users u JOIN public.user_logins ul ON ul.user_id = u.id JOIN public.logins l ON l.id = ul.login_id
    WHERE l.email = 'ricardodelarosa@italcol.com';

    SELECT u.id INTO v_user_ladymalave
    FROM public.users u JOIN public.user_logins ul ON ul.user_id = u.id JOIN public.logins l ON l.id = ul.login_id
    WHERE l.email = 'ladymalave@ecuitalcol.com';

    SELECT id INTO v_perm_tickets_gestionar FROM public.permissions WHERE key = 'tickets.gestionar';
    SELECT id INTO v_menu_bandeja_gestion   FROM public.menus WHERE route = '/tickets/gestion';

    -- Fail-closed: si algo base no se resolvió (entorno distinto a lo auditado), abortar en vez
    -- de insertar filas con NULL que quedarían inertes y confundirían el diagnóstico despues.
    IF v_company_sanmarino IS NULL OR v_company_panama IS NULL OR v_company_ecuador IS NULL
       OR v_role_sistemas_sanmarino IS NULL OR v_role_sistemas_panama IS NULL
       OR v_role_implementador_sanmarino IS NULL OR v_role_ecuador_administrador IS NULL
       OR v_user_verenice IS NULL OR v_user_ricardo IS NULL OR v_user_ladymalave IS NULL
       OR v_perm_tickets_gestionar IS NULL OR v_menu_bandeja_gestion IS NULL THEN
        RAISE EXCEPTION 'SeedEnrutamientoTicketsPorEmpresa: no se resolvio algun id base (compania/rol/usuario/permiso/menu) — revisar nombres/emails contra este entorno.';
    END IF;

    -- ================================================================================
    -- SANMARINO
    -- ================================================================================

    INSERT INTO public.ticket_resolutor_rol (role_id, tipo, pais_id, company_id, activo)
    SELECT v_role_sistemas_sanmarino, t, NULL, v_company_sanmarino, true
    FROM unnest(ARRAY['SOPORTE','DUDAS']) AS t
    WHERE NOT EXISTS (
        SELECT 1 FROM public.ticket_resolutor_rol trr
        WHERE trr.role_id = v_role_sistemas_sanmarino AND trr.tipo = t
          AND trr.pais_id IS NULL AND trr.company_id = v_company_sanmarino);

    -- Verenice: solo REQUERIMIENTO queda activo (ya lo tenia); SOPORTE/DUDAS ahora los cubre el
    -- rol de arriba, y DESARROLLO es exclusivo del resolutor global (Admin, ya configurado).
    UPDATE public.ticket_resolutores
    SET activo = false, updated_at = now()
    WHERE user_id = v_user_verenice AND company_id = v_company_sanmarino
      AND tipo IN ('SOPORTE','DUDAS','DESARROLLO') AND activo = true;

    INSERT INTO public.role_permissions (role_id, permission_id)
    SELECT v_role_implementador_sanmarino, v_perm_tickets_gestionar
    WHERE NOT EXISTS (
        SELECT 1 FROM public.role_permissions rp
        WHERE rp.role_id = v_role_implementador_sanmarino AND rp.permission_id = v_perm_tickets_gestionar);

    -- ================================================================================
    -- PANAMA
    -- ================================================================================

    INSERT INTO public.company_permissions (company_id, permission_id, is_enabled)
    VALUES (v_company_panama, v_perm_tickets_gestionar, true)
    ON CONFLICT (company_id, permission_id) DO UPDATE SET is_enabled = true;

    INSERT INTO public.role_permissions (role_id, permission_id)
    SELECT v_role_sistemas_panama, v_perm_tickets_gestionar
    WHERE NOT EXISTS (
        SELECT 1 FROM public.role_permissions rp
        WHERE rp.role_id = v_role_sistemas_panama AND rp.permission_id = v_perm_tickets_gestionar);

    INSERT INTO public.role_menus (role_id, menu_id)
    SELECT v_role_sistemas_panama, v_menu_bandeja_gestion
    WHERE NOT EXISTS (
        SELECT 1 FROM public.role_menus rm
        WHERE rm.role_id = v_role_sistemas_panama AND rm.menu_id = v_menu_bandeja_gestion);

    INSERT INTO public.ticket_resolutor_rol (role_id, tipo, pais_id, company_id, activo)
    SELECT v_role_sistemas_panama, t, NULL, v_company_panama, true
    FROM unnest(ARRAY['SOPORTE','DUDAS']) AS t
    WHERE NOT EXISTS (
        SELECT 1 FROM public.ticket_resolutor_rol trr
        WHERE trr.role_id = v_role_sistemas_panama AND trr.tipo = t
          AND trr.pais_id IS NULL AND trr.company_id = v_company_panama);

    INSERT INTO public.ticket_resolutores (user_id, tipo, pais_id, company_id, activo)
    SELECT v_user_ricardo, 'REQUERIMIENTO', NULL, v_company_panama, true
    WHERE NOT EXISTS (
        SELECT 1 FROM public.ticket_resolutores tr
        WHERE tr.user_id = v_user_ricardo AND tr.tipo = 'REQUERIMIENTO'
          AND tr.pais_id IS NULL AND tr.company_id = v_company_panama);

    -- ================================================================================
    -- ECUADOR — sin area de sistemas: Lady Malave cubre los tres tipos directamente.
    -- ================================================================================

    INSERT INTO public.company_permissions (company_id, permission_id, is_enabled)
    VALUES (v_company_ecuador, v_perm_tickets_gestionar, true)
    ON CONFLICT (company_id, permission_id) DO UPDATE SET is_enabled = true;

    INSERT INTO public.role_permissions (role_id, permission_id)
    SELECT v_role_ecuador_administrador, v_perm_tickets_gestionar
    WHERE NOT EXISTS (
        SELECT 1 FROM public.role_permissions rp
        WHERE rp.role_id = v_role_ecuador_administrador AND rp.permission_id = v_perm_tickets_gestionar);

    -- Perfil de Lady Malave estaba guardado en la empresa equivocada (Sanmarino): se apaga esa
    -- fila (no se borra, queda el rastro) y se crea la correcta en Ecuador.
    UPDATE public.ticket_perfil_usuario
    SET activo = false, updated_at = now()
    WHERE user_id = v_user_ladymalave AND company_id = v_company_sanmarino AND activo = true;

    INSERT INTO public.ticket_perfil_usuario (user_id, company_id, nivel, activo)
    SELECT v_user_ladymalave, v_company_ecuador, 'IMPLEMENTADOR', true
    WHERE NOT EXISTS (
        SELECT 1 FROM public.ticket_perfil_usuario tpu
        WHERE tpu.user_id = v_user_ladymalave AND tpu.company_id = v_company_ecuador);

    INSERT INTO public.ticket_resolutores (user_id, tipo, pais_id, company_id, activo)
    SELECT v_user_ladymalave, t, NULL, v_company_ecuador, true
    FROM unnest(ARRAY['SOPORTE','DUDAS','REQUERIMIENTO']) AS t
    WHERE NOT EXISTS (
        SELECT 1 FROM public.ticket_resolutores tr
        WHERE tr.user_id = v_user_ladymalave AND tr.tipo = t
          AND tr.pais_id IS NULL AND tr.company_id = v_company_ecuador);
END
$mig$;
";

        // Revierte SOLO lo que esta migración agregó/encendió. Lo que desactivó (Verenice,
        // Lady Malave) lo reactiva — no intenta adivinar si había otra razón para que
        // estuviera apagado, porque antes de este Up() no lo estaba.
        private const string DOWN_SQL = @"
DO $mig$
DECLARE
    v_company_sanmarino int;
    v_company_panama    int;
    v_company_ecuador   int;

    v_role_sistemas_sanmarino      int;
    v_role_sistemas_panama         int;
    v_role_implementador_sanmarino int;
    v_role_ecuador_administrador   int;

    v_user_verenice   uuid;
    v_user_ricardo    uuid;
    v_user_ladymalave uuid;

    v_perm_tickets_gestionar int;
    v_menu_bandeja_gestion   int;
BEGIN
    SELECT id INTO v_company_sanmarino FROM public.companies WHERE name = 'Agroavicola Sanmarino';
    SELECT id INTO v_company_panama    FROM public.companies WHERE name = 'ItalcolPanama';
    SELECT id INTO v_company_ecuador   FROM public.companies WHERE name = 'ItalcolEcuador';

    SELECT id INTO v_role_sistemas_sanmarino      FROM public.roles WHERE name = 'Sistemas sanmarino';
    SELECT id INTO v_role_sistemas_panama         FROM public.roles WHERE name = 'sistemas panama';
    SELECT id INTO v_role_implementador_sanmarino FROM public.roles WHERE name = 'Implementador Sanmarino Colombia';
    SELECT id INTO v_role_ecuador_administrador   FROM public.roles WHERE name = 'Ecuador Administrador';

    SELECT u.id INTO v_user_verenice
    FROM public.users u JOIN public.user_logins ul ON ul.user_id = u.id JOIN public.logins l ON l.id = ul.login_id
    WHERE l.email = 'verenicemorales@sanmarino.com.co';

    SELECT u.id INTO v_user_ricardo
    FROM public.users u JOIN public.user_logins ul ON ul.user_id = u.id JOIN public.logins l ON l.id = ul.login_id
    WHERE l.email = 'ricardodelarosa@italcol.com';

    SELECT u.id INTO v_user_ladymalave
    FROM public.users u JOIN public.user_logins ul ON ul.user_id = u.id JOIN public.logins l ON l.id = ul.login_id
    WHERE l.email = 'ladymalave@ecuitalcol.com';

    SELECT id INTO v_perm_tickets_gestionar FROM public.permissions WHERE key = 'tickets.gestionar';
    SELECT id INTO v_menu_bandeja_gestion   FROM public.menus WHERE route = '/tickets/gestion';

    IF v_company_sanmarino IS NULL OR v_company_panama IS NULL OR v_company_ecuador IS NULL THEN
        RETURN;
    END IF;

    -- Ecuador
    DELETE FROM public.ticket_resolutores
    WHERE user_id = v_user_ladymalave AND company_id = v_company_ecuador
      AND tipo IN ('SOPORTE','DUDAS','REQUERIMIENTO') AND pais_id IS NULL;

    DELETE FROM public.ticket_perfil_usuario
    WHERE user_id = v_user_ladymalave AND company_id = v_company_ecuador;

    UPDATE public.ticket_perfil_usuario
    SET activo = true, updated_at = now()
    WHERE user_id = v_user_ladymalave AND company_id = v_company_sanmarino;

    IF v_role_ecuador_administrador IS NOT NULL AND v_perm_tickets_gestionar IS NOT NULL THEN
        DELETE FROM public.role_permissions
        WHERE role_id = v_role_ecuador_administrador AND permission_id = v_perm_tickets_gestionar;
    END IF;

    -- Ecuador no tenia fila de company_permissions para este permiso antes del Up(): se borra
    -- entera en vez de dejarla en false (asi vuelve al estado real anterior: sin configurar).
    IF v_perm_tickets_gestionar IS NOT NULL THEN
        DELETE FROM public.company_permissions
        WHERE company_id = v_company_ecuador AND permission_id = v_perm_tickets_gestionar;
    END IF;

    -- Panama
    IF v_role_sistemas_panama IS NOT NULL THEN
        DELETE FROM public.ticket_resolutor_rol
        WHERE role_id = v_role_sistemas_panama AND company_id = v_company_panama
          AND tipo IN ('SOPORTE','DUDAS') AND pais_id IS NULL;

        IF v_menu_bandeja_gestion IS NOT NULL THEN
            DELETE FROM public.role_menus
            WHERE role_id = v_role_sistemas_panama AND menu_id = v_menu_bandeja_gestion;
        END IF;

        IF v_perm_tickets_gestionar IS NOT NULL THEN
            DELETE FROM public.role_permissions
            WHERE role_id = v_role_sistemas_panama AND permission_id = v_perm_tickets_gestionar;
        END IF;
    END IF;

    DELETE FROM public.ticket_resolutores
    WHERE user_id = v_user_ricardo AND company_id = v_company_panama
      AND tipo = 'REQUERIMIENTO' AND pais_id IS NULL;

    IF v_perm_tickets_gestionar IS NOT NULL THEN
        UPDATE public.company_permissions
        SET is_enabled = false
        WHERE company_id = v_company_panama AND permission_id = v_perm_tickets_gestionar;
    END IF;

    -- Sanmarino
    IF v_role_implementador_sanmarino IS NOT NULL AND v_perm_tickets_gestionar IS NOT NULL THEN
        DELETE FROM public.role_permissions
        WHERE role_id = v_role_implementador_sanmarino AND permission_id = v_perm_tickets_gestionar;
    END IF;

    UPDATE public.ticket_resolutores
    SET activo = true, updated_at = now()
    WHERE user_id = v_user_verenice AND company_id = v_company_sanmarino
      AND tipo IN ('SOPORTE','DUDAS','DESARROLLO');

    IF v_role_sistemas_sanmarino IS NOT NULL THEN
        DELETE FROM public.ticket_resolutor_rol
        WHERE role_id = v_role_sistemas_sanmarino AND company_id = v_company_sanmarino
          AND tipo IN ('SOPORTE','DUDAS') AND pais_id IS NULL;
    END IF;
END
$mig$;
";
    }
}
