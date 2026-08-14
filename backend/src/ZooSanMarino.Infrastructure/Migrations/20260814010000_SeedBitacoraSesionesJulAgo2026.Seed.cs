using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Cuerpo del seed de la bitácora de julio-agosto 2026. Vive en su propio archivo (partial)
    /// porque es SQL GENERADO: la documentación de qué hace y por qué está en la migración.
    /// </summary>
    /// <remarks>
    /// No editar a mano: se regenera con <c>fase_de_desarrollo/generadores/italjira_bitacora/</c>
    /// (extraer_sesiones.py → cruzar.py → armar_items.py → generar_seed.py) a partir de las
    /// transcripciones de sesión, del historial de git y del archivo de horas versionado en
    /// <c>fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json</c>.
    /// </remarks>
    public partial class SeedBitacoraSesionesJulAgo2026
    {
        private const string SEED_SQL = @"-- ─────────────────────────────────────────────────────────────────────────────
-- Bitácora REAL de julio y agosto 2026 en ItalJira.
-- Fuente: las 134 sesiones de trabajo del período (pedido textual del usuario, fechas y
-- duración medidas) cruzadas con los 447 commits del repositorio. Los bugs son los commits
-- fix(...) de cada ventana. La ÚNICA cifra estimada es horas_estimadas, asignada por juicio
-- (rúbrica y valor por ítem en fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json).
-- ─────────────────────────────────────────────────────────────────────────────
DO $$
DECLARE
    v_user_guid uuid;
    v_cedula    integer;
    v_company   integer;
    v_pais      integer;
BEGIN
    -- Identidad POR EMAIL, nunca por guid fijo: los ids difieren entre local y producción.
    SELECT u.id INTO v_user_guid
    FROM public.users u
    JOIN public.user_logins ul ON ul.user_id = u.id
    JOIN public.logins l       ON l.id = ul.login_id
    WHERE lower(l.email) = 'moiesbbuga@gmail.com'
    LIMIT 1;

    -- Fail-open silencioso: sin el usuario no se siembra nada y la app arranca igual.
    IF v_user_guid IS NULL THEN
        RAISE NOTICE 'ItalJira bitácora: no existe moiesbbuga@gmail.com en este entorno; omitida.';
        RETURN;
    END IF;

    -- El int de auditoría del módulo NO es la cédula (3177120174 no entra en integer).
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

    -- ═══ 1) Enriquecer las tareas ya sembradas (horas + pedido + solución + evidencia) ═══
    -- El guard exige que la descripción siga siendo EXACTAMENTE la que escribió el seed
    -- anterior: si alguien la editó a mano, esta migración no la pisa.
    UPDATE public.ticket_tareas
       SET horas_estimadas = 12.00, descripcion = 'Plan: fase_de_desarrollo/tickets_notificados_flujos_plan.md

── Bitácora de la sesión (2026-07-01) ──
Pedido: «t6engo estos requerimientos para postura , levante y produccion tengo este requerimeintos de colombia en excel donde expresa que neceista la locion sonbre esta necesidad necesito que me crees un loop para corregir todo el excel que te voy a pasar que tiene requermientos por cada parte generarlo muy profesional y nivel senior , mejroalo y estudia todo el codigo ya que todo esta en la aplciacion donde tenemos tabla genenica para sanmarino colombia tnemos una tbla tambien la idea es que siempre este»
Solución (3 commits): chore: ciclo 2 - entorno local de validacion + baseline tests + barrido front; feat(C1): graficas levante consumen el endpoint BD (front ya no calcula); chore(inventario): S2 elimina ruta huérfana /inventario-management
Bugs encontrados: 0.
Evidencia: 60 archivos tocados · 7,6 h de sesión real · commits e9e72f2, e6e008d, d9e9377 · sesión 29953769
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0001-T10' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/tickets_notificados_flujos_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 16.00, descripcion = 'Plan: fase_de_desarrollo/postura_colombia_alineacion_guia_plan.md

── Bitácora de la sesión (2026-07-01) ──
Pedido: «t6engo estos requerimientos para postura , levante y produccion tengo este requerimeintos de colombia en excel donde expresa que neceista la locion sonbre esta necesidad necesito que me crees un loop para corregir todo el excel que te voy a pasar que tiene requermientos por cada parte generarlo muy profesional y nivel senior , mejroalo y estudia todo el codigo ya que todo esta en la aplciacion donde tenemos tabla genenica para sanmarino colombia tnemos una tbla tambien la idea es que siempre este»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 29953769
Estimación: 16 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0011-T2' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/postura_colombia_alineacion_guia_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 20.00, descripcion = 'Plan: fase_de_desarrollo/soporte_bot_loop_tickets_plan.md

── Bitácora de la sesión (2026-07-03) ──
Pedido: «valdiame dentro del proyecto tengo ticket entonces en mi perfil que es el moiesbbuga@gmail.com y soy el desarrollador principal y necesito resolver errores o novedades y necesito validar la informacion pero quiero automatizar crear un hook o algo que entre a produccion y filtre traiga lo que se encuentre tome el ticket inicie lo que se va arealizar y pase a un loop de desarrollo y leugo notifique cuando se sierra en las opcioens de notificar que tiene el modulo ticket , y la idea es que sea un puente de alguna forma que claude pueda entrar y realziar solcuioens que tiene aplciadas ami o soluc»
Solución (34 commits): test(fase3/S4): Colombia->ModeloBNivelGranja + no-afectacion contable + evidencia BD; docs: Fase 3 paso 2 QA ESTABLE + plan paso 3 (alineacion front Colombia modelo B); feat(fase3/S3-S1): menú Colombia → inventario modelo B (/gestion-inventario + catálogo); feat(fase3/S3-S2): ingreso/traslado/recepción nivel granja para Colombia (modelo B); feat(fase3/S3-S3): gestion-inventario nivel granja para Colombia (front); feat(backend): alimento galpón/granja configurable + de-dup parser metadata; refactor(front): rebrand UX pro paleta Italcol naranja/dorado/blanco + menú pro + gestión-inventario; docs: planes de fase (alimento configurable, fn_metadata, refactor UX pro, service-token, soporte-bot) + tracker
Bugs encontrados: 4 — cada uno queda como subtarea BUG con su causa.
Evidencia: 175 archivos tocados · 21,7 h de sesión real · commits ffd50c6, 77621d1, 2390238, adadfc8, edd4ebb, f23e14b, 733a1f2, 5a992fa, 22ce51a, f9a8f99 · sesión 7c4d7cfb
Estimación: 20 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0001-T11' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/soporte_bot_loop_tickets_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 8.00, descripcion = 'Plan: fase_de_desarrollo/fn_metadata_items_kg_plan.md

── Bitácora de la sesión (2026-07-03) ──
Pedido: «valdiame dentro del proyecto tengo ticket entonces en mi perfil que es el moiesbbuga@gmail.com y soy el desarrollador principal y necesito resolver errores o novedades y necesito validar la informacion pero quiero automatizar crear un hook o algo que entre a produccion y filtre traiga lo que se encuentre tome el ticket inicie lo que se va arealizar y pase a un loop de desarrollo y leugo notifique cuando se sierra en las opcioens de notificar que tiene el modulo ticket , y la idea es que sea un puente de alguna forma que claude pueda entrar y realziar solcuioens que tiene aplciadas ami o soluc»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 7c4d7cfb
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0007-T6' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/fn_metadata_items_kg_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 24.00, descripcion = 'Plan: fase_de_desarrollo/unificacion_inventario_colombia_plan.md

── Bitácora de la sesión (2026-07-03) ──
Pedido: «valdiame dentro del proyecto tengo ticket entonces en mi perfil que es el moiesbbuga@gmail.com y soy el desarrollador principal y necesito resolver errores o novedades y necesito validar la informacion pero quiero automatizar crear un hook o algo que entre a produccion y filtre traiga lo que se encuentre tome el ticket inicie lo que se va arealizar y pase a un loop de desarrollo y leugo notifique cuando se sierra en las opcioens de notificar que tiene el modulo ticket , y la idea es que sea un puente de alguna forma que claude pueda entrar y realziar solcuioens que tiene aplciadas ami o soluc»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 7c4d7cfb
Estimación: 24 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0007-T7' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/unificacion_inventario_colombia_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 8.00, descripcion = 'Plan: fase_de_desarrollo/gastos_inventario_ux_plan.md

── Bitácora de la sesión (2026-07-03) ──
Pedido: «valdiame dentro del proyecto tengo ticket entonces en mi perfil que es el moiesbbuga@gmail.com y soy el desarrollador principal y necesito resolver errores o novedades y necesito validar la informacion pero quiero automatizar crear un hook o algo que entre a produccion y filtre traiga lo que se encuentre tome el ticket inicie lo que se va arealizar y pase a un loop de desarrollo y leugo notifique cuando se sierra en las opcioens de notificar que tiene el modulo ticket , y la idea es que sea un puente de alguna forma que claude pueda entrar y realziar solcuioens que tiene aplciadas ami o soluc»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 7c4d7cfb
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0007-T8' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/gastos_inventario_ux_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 12.00, descripcion = 'Plan: fase_de_desarrollo/alimento_nivel_galpon_configurable_plan.md

── Bitácora de la sesión (2026-07-03) ──
Pedido: «valdiame dentro del proyecto tengo ticket entonces en mi perfil que es el moiesbbuga@gmail.com y soy el desarrollador principal y necesito resolver errores o novedades y necesito validar la informacion pero quiero automatizar crear un hook o algo que entre a produccion y filtre traiga lo que se encuentre tome el ticket inicie lo que se va arealizar y pase a un loop de desarrollo y leugo notifique cuando se sierra en las opcioens de notificar que tiene el modulo ticket , y la idea es que sea un puente de alguna forma que claude pueda entrar y realziar solcuioens que tiene aplciadas ami o soluc»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 7c4d7cfb
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0012-T11' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/alimento_nivel_galpon_configurable_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 2.00, descripcion = 'Plan: fase_de_desarrollo/tracker_fase3_paso3_colombia_ARCHIVE.md

── Bitácora de la sesión (2026-07-03) ──
Pedido: «valdiame dentro del proyecto tengo ticket entonces en mi perfil que es el moiesbbuga@gmail.com y soy el desarrollador principal y necesito resolver errores o novedades y necesito validar la informacion pero quiero automatizar crear un hook o algo que entre a produccion y filtre traiga lo que se encuentre tome el ticket inicie lo que se va arealizar y pase a un loop de desarrollo y leugo notifique cuando se sierra en las opcioens de notificar que tiene el modulo ticket , y la idea es que sea un puente de alguna forma que claude pueda entrar y realziar solcuioens que tiene aplciadas ami o soluc»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 7c4d7cfb
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0016-T5' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/tracker_fase3_paso3_colombia_ARCHIVE.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 16.00, descripcion = 'Plan: fase_de_desarrollo/refactor_ux_pro_front_plan.md

── Bitácora de la sesión (2026-07-03) ──
Pedido: «valdiame dentro del proyecto tengo ticket entonces en mi perfil que es el moiesbbuga@gmail.com y soy el desarrollador principal y necesito resolver errores o novedades y necesito validar la informacion pero quiero automatizar crear un hook o algo que entre a produccion y filtre traiga lo que se encuentre tome el ticket inicie lo que se va arealizar y pase a un loop de desarrollo y leugo notifique cuando se sierra en las opcioens de notificar que tiene el modulo ticket , y la idea es que sea un puente de alguna forma que claude pueda entrar y realziar solcuioens que tiene aplciadas ami o soluc»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 7c4d7cfb
Estimación: 16 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0017-T2' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/refactor_ux_pro_front_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 20.00, descripcion = 'Plan: fase_de_desarrollo/design_system_shared_ui_plan.md

── Bitácora de la sesión (2026-07-03) ──
Pedido: «valdiame dentro del proyecto tengo ticket entonces en mi perfil que es el moiesbbuga@gmail.com y soy el desarrollador principal y necesito resolver errores o novedades y necesito validar la informacion pero quiero automatizar crear un hook o algo que entre a produccion y filtre traiga lo que se encuentre tome el ticket inicie lo que se va arealizar y pase a un loop de desarrollo y leugo notifique cuando se sierra en las opcioens de notificar que tiene el modulo ticket , y la idea es que sea un puente de alguna forma que claude pueda entrar y realziar solcuioens que tiene aplciadas ami o soluc»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 7c4d7cfb
Estimación: 20 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0017-T3' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/design_system_shared_ui_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 20.00, descripcion = 'Plan: fase_de_desarrollo/upgrade_angular_20_a_22_plan.md

── Bitácora de la sesión (2026-07-03) ──
Pedido: «valdiame dentro del proyecto tengo ticket entonces en mi perfil que es el moiesbbuga@gmail.com y soy el desarrollador principal y necesito resolver errores o novedades y necesito validar la informacion pero quiero automatizar crear un hook o algo que entre a produccion y filtre traiga lo que se encuentre tome el ticket inicie lo que se va arealizar y pase a un loop de desarrollo y leugo notifique cuando se sierra en las opcioens de notificar que tiene el modulo ticket , y la idea es que sea un puente de alguna forma que claude pueda entrar y realziar solcuioens que tiene aplciadas ami o soluc»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 7c4d7cfb
Estimación: 20 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0019-T15' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/upgrade_angular_20_a_22_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 12.00, descripcion = 'Plan: fase_de_desarrollo/upgrade_dotnet_9_a_10_plan.md

── Bitácora de la sesión (2026-07-03) ──
Pedido: «valdiame dentro del proyecto tengo ticket entonces en mi perfil que es el moiesbbuga@gmail.com y soy el desarrollador principal y necesito resolver errores o novedades y necesito validar la informacion pero quiero automatizar crear un hook o algo que entre a produccion y filtre traiga lo que se encuentre tome el ticket inicie lo que se va arealizar y pase a un loop de desarrollo y leugo notifique cuando se sierra en las opcioens de notificar que tiene el modulo ticket , y la idea es que sea un puente de alguna forma que claude pueda entrar y realziar solcuioens que tiene aplciadas ami o soluc»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 7c4d7cfb
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0019-T16' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/upgrade_dotnet_9_a_10_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 14.00, descripcion = 'Plan: fase_de_desarrollo/inventario_nuevo_y_alimento_macho_seguimiento_plan.md

── Bitácora de la sesión (2026-07-10) ──
Pedido: «ya tenia una solucion para este problema pero nunca se mergio o no se desarrollo primero tengo que validar que tenga mos la lsita de aliemntos del inventario neuvo que se esta implmentado ya que esta apuntando al viejo y debe traerme lso que estan completos , ya que tambien me debe mostrar el tipo de alimento para macho ya que puedo alimentar el macho con con otro alimento entonces tenismoa que agregarlo a la base de datos y al reprote de seguimeinto diario y que visual mente se vea la division pero dbe sumarce el sonsumo si es el mismo alimento lo suma y realiza el descuento : esta es la co»
Solución (7 commits): feat(seguimiento-levante): ItemSeguimientoDto gana campo nombre; feat(seguimiento-produccion): ItemSeguimientoDto gana campo nombre; feat(seguimiento-levante): Colombia lee alimento del inventario nuevo + alimento independiente por sexo; feat(seguimiento-levante): UI bloques Hembras/Machos independientes; feat(seguimiento-produccion): Colombia lee alimento del inventario nuevo; docs(seguimiento-inventario): plan de catalogo de alimento nuevo + alimento por sexo; docs(seguimiento-inventario): tracker de estado
Bugs encontrados: 4 — cada uno queda como subtarea BUG con su causa.
Evidencia: 18 archivos tocados · 1,7 h de sesión real · commits fb0cd36, 89e1f5b, 8e9bbc1, 58099b4, 0e8aaba, 4136a12, c9dbe6a, 09df059, f3f9c1d, 97ba976 · sesión 3273cd7a
Estimación: 14 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0007-T11' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/inventario_nuevo_y_alimento_macho_seguimiento_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 6.00, descripcion = 'Plan: fase_de_desarrollo/migraciones_masivas_fase3_spec.md

── Bitácora de la sesión (2026-07-12 → 2026-08-01) ──
Pedido: «Requerimiento de Desarrollo Módulo de Migraciones Masivas - Postura Objetivo Se debe crear un nuevo módulo independiente encargado de realizar la migración masiva de información mediante archivos Excel. Este módulo permitirá reducir el proceso manual de parametrización inicial de una empresa y facilitar la carga de información histórica de Postura. No reemplaza los módulos existentes. Todos los módulos actuales continúan siendo la fuente oficial de información. El módulo únicamente automatiza la creación masiva utilizando las mismas reglas de negocio existentes. Objetivos del módulo El módulo»
Solución (1 commit): fy
Bugs encontrados: 2 — cada uno queda como subtarea BUG con su causa.
Evidencia: 109 archivos tocados · 2,8 h de sesión real · commits 1126280, 2eab7f8, 4e49369 · sesión 3602f5ab, 8ed99c77, 5b68e3ea
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0006-T2' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/migraciones_masivas_fase3_spec.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 32.00, descripcion = 'Plan: fase_de_desarrollo/migraciones_masivas_plan.md

── Bitácora de la sesión (2026-07-12) ──
Pedido: «Requerimiento de Desarrollo Módulo de Migraciones Masivas - Postura Objetivo Se debe crear un nuevo módulo independiente encargado de realizar la migración masiva de información mediante archivos Excel. Este módulo permitirá reducir el proceso manual de parametrización inicial de una empresa y facilitar la carga de información histórica de Postura. No reemplaza los módulos existentes. Todos los módulos actuales continúan siendo la fuente oficial de información. El módulo únicamente automatiza la creación masiva utilizando las mismas reglas de negocio existentes. Objetivos del módulo El módulo»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 3602f5ab, 8ed99c77
Estimación: 32 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0006-T5' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/migraciones_masivas_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 20.00, descripcion = 'Plan: fase_de_desarrollo/migraciones_masivas_engorde_plan.md

── Bitácora de la sesión (2026-07-12) ──
Pedido: «vamos a crear migraciones para lotes pollo engorde ,seguimiento diario pollo engorde , venta de pollo engorde valida eso para crearle las migraciones masivo ya tenemos el de granja , nucleo y galpon ,»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 8ed99c77
Estimación: 20 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0006-T4' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/migraciones_masivas_engorde_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 6.00, descripcion = 'Plan: fase_de_desarrollo/venta_granja_bloqueo_lotes_cerrados_plan.md

── Bitácora de la sesión (2026-07-14) ──
Pedido: «en el modulo de ventas necesito que cunado ya un lote este cerrado dentro de ungalpon o ya este otra corrida en el mismo galpon es decir si ya estan en la corrida 2603 ya , no dejar que realizen descuentos de los lotes anteriores si no tiene el persmiso de venta de lotes cerrado o anteriores , asi evitamos que usuarios cojan aves de lotes que ya estan cuadrados y lso metan en la venta de la aplicacion esto es el el modulo de venta pollo engorde : dejo un ejemplo : de la granja : Granja: Kilometro 61 (varios galpones / lotes) , el back debe brindar esa parte tmabine pero mas el front para qu»
Solución (4 commits): feat(movimientos-pollo-engorde): bloquear venta de lotes cerrados o corridas anteriores; feat(migraciones-masivas): esquema unico de plantilla/validacion, historial paginado y fix de descuento incremental de aves; feat(migraciones-masivas): permisos por linea (carga_masiva_pollo_engorde / carga_masiva_postura); docs(tracker): cerrar tracker de permisos carga masiva con hash del commit
Bugs encontrados: 0.
Evidencia: 16 archivos tocados · 0,4 h de sesión real · commits 62ede31, af3ad69, 354368f, cd3ca63 · sesión 3074a312
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0010-T10' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/venta_granja_bloqueo_lotes_cerrados_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 12.00, descripcion = 'Plan: fase_de_desarrollo/db_studio_plan.md

── Bitácora de la sesión (2026-07-14) ──
Pedido: «tengo el modulo de db_studio quiero tamibne poder realiza copia de de segudiad quiere decir back y que sean descargable asi no entrar a la aplciacion en produccion ya que me cuesta mucho para estar entrando para lo que son copias de seguridad y que descargue en fomratos sql y debe tener la siguitne estrutura de descargas sanmarino-(fecha actual)-produccion»
Solución (3 commits): migracion(seguimiento-produccion): backfill idempotente de company_id en seguimiento_diario_produccion; Merge pull request #32 from ItalcolColombia/claude/infallible-brahmagupta-90c317; feat(db-studio): copia de seguridad completa descargable (SQL)
Bugs encontrados: 0.
Evidencia: 28 archivos tocados · 1,6 h de sesión real · commits 8c92c8c, 5e5461b, 786de13 · sesión 264dbd27
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0015-T5' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/db_studio_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 10.00, descripcion = 'Plan: fase_de_desarrollo/db_studio_backup_descargable_plan.md

── Bitácora de la sesión (2026-07-14) ──
Pedido: «tengo el modulo de db_studio quiero tamibne poder realiza copia de de segudiad quiere decir back y que sean descargable asi no entrar a la aplciacion en produccion ya que me cuesta mucho para estar entrando para lo que son copias de seguridad y que descargue en fomratos sql y debe tener la siguitne estrutura de descargas sanmarino-(fecha actual)-produccion»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 264dbd27
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0015-T7' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/db_studio_backup_descargable_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 4.00, descripcion = 'Plan: fase_de_desarrollo/fix_aves_vivas_mort_caja_engorde_plan.md

── Bitácora de la sesión (2026-07-14) ──
Pedido: «tengo un error en los datos que muestra en aves vivas y lo que muestra en cantidad de aves disponible que trae el seguimeit o aqui dejo el servicio que muestre que hay 17 aves disponibles vivas pero el otro servicio que muestra en la parte superir dice que solo hay 0 machos y 0 hembras disponibles quiero valdiar pro que el seguimeitn omuestra lso 17 o que paso paso la imagen de la informacion del lote y todo y en el ultimo registro esta la novedad que dejo especificado encintra si es ventas sin aprovar o que paso en si pro que esta ese descueido y podra aver mas lotes de la correida 03 en otra»
Solución (2 commits): refactor(devpilot): Refactor SeguimientoAvesEngordeService (1884 líneas); refactor(devpilot): Refactor IndicadorEcuadorService (1185) y SeguimientoAvesEngordeEcuadorService (1087)
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 21 archivos tocados · 0,8 h de sesión real · commits 7e524f8, 473f5ac, c6bbd29 · sesión c108d4ad
Estimación: 4 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0012-T13' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/fix_aves_vivas_mort_caja_engorde_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 2.00, descripcion = 'Plan: fase_de_desarrollo/CONTEXTO_TRASPASO_VACUNACION.md

── Bitácora de la sesión (2026-07-14 → 2026-07-15) ──
Pedido: «tenia una sesion que era para crear los modulos de vacunacion pero no la veo la sesion»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: 3 archivos tocados · 0,2 h de sesión real · sesión a3d18c7f, 3693d66d
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0003-T2' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/CONTEXTO_TRASPASO_VACUNACION.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 2.00, descripcion = 'Plan: fase_de_desarrollo/CONTEXTO_TRASPASO_SESION.md

── Bitácora de la sesión (2026-07-14) ──
Pedido: «tenia una sesion que era para crear los modulos de vacunacion pero no la veo la sesion»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión a3d18c7f
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0019-T10' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/CONTEXTO_TRASPASO_SESION.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 24.00, descripcion = 'Plan: fase_de_desarrollo/vacunacion_cronograma_plan.md

── Bitácora de la sesión (2026-07-14 → 2026-07-15) ──
Pedido: «fase_de_desarrollo/CONTEXTO_TRASPASO_VACUNACION.md»
Solución (1 commit): feat(vacunacion): agrega modulo de cronogramas de vacunacion por lote
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 84 archivos tocados · 2,0 h de sesión real · commits 57763f6, d44cb07 · sesión 3693d66d
Estimación: 24 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0003-T1' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/vacunacion_cronograma_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 16.00, descripcion = 'Plan: fase_de_desarrollo/puente_panama_engorde_plan.md

── Bitácora de la sesión (2026-07-15 → 2026-07-16) ──
Pedido: «necesito crea un puente de consulta para realziar migracion de lotes granjas y organizarlo aqui y tambine seguimiento diario , seguimeinto reproductora , todo es de modulo de pollo engorde de un swagerr la idea es colocar el año de los lotes y me traiga tdos lo que este y se sincronizan con el modulo de pollo engorde , por que no tiene ventas , no tiene traslados registrados es muy sensillo pero queir que valides pero nunca utilzies update o eliminar en el swagger como regla , investiga cada servicio para poder tener alineado con lo que necesitamos trar a nuestro sistemas :»
Solución (1 commit): feat(engorde): puente de sincronizacion con ZooPanamaPollo (Integracion Panama)
Bugs encontrados: 0.
Evidencia: 58 archivos tocados · 4,5 h de sesión real · commits d16c1c8 · sesión 918247c3
Estimación: 16 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0012-T14' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/puente_panama_engorde_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 12.00, descripcion = 'Plan: fase_de_desarrollo/vacunacion_mejora_integral_plan.md

── Bitácora de la sesión (2026-07-16) ──
Pedido: «los modulos que etan en vacinacion organizalos mejor y diseño ui y ux desing de caurdo a los colores de la aplciacion y esta lento en los select o al momento de trar inofmacion los serviciso que tenga el 100 del codigo en la base dedatos en funciones que reflejen el modulo y la funcion que realzian asi realziamos mas velocidad compleeta en la aplciacion pero los modulos de vacnacion mejoralos completamente mas profesionales y mejores en usabilidad y reproteria tambien»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: 60 archivos tocados · 1,0 h de sesión real · sesión 74e83ed2
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0003-T3' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/vacunacion_mejora_integral_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 14.00, descripcion = 'Plan: fase_de_desarrollo/postura_verenice_rev_6jul26_plan.md

── Bitácora de la sesión (2026-07-16 → 2026-07-17) ──
Pedido: «estos son de modulos postura , seguimiento diario levante , seguimeinto diario produccion , movimiento de aves , movimiento de huevos , reprotes , sanmarino que tiene las dos opciones de levante y produccion , estos son requeirmeintos para esta linea que es todo los modulos de postura , validalos y valida sobre el modulo para identificar el error y saber que si ya esta solucionado o precente la falla , te paso el las credenciales de para acceder y validar esta faceta completa investiga a profundo c»
Solución (11 commits): feature de vacunacion; feat(produccion-back): filter-data con encaset, semana 25, etapa, %retiro real+guia y enforcement; feat(seguimiento-levante-back): guardas de encaset, consumo vs saldo por sexo y bloqueo de lote cerrado; test(produccion): alinear etapa 26-33 y agregar tests de %retiro; feat(db): vista Power BI y migracion EF de funciones/vista de indicadores postura; chore(sql): script idempotente de correccion de datos postura (Fase 0, NO aplicar sin OK); feat(seguimiento-levante-ui): glosario, consumo/retiro por sexo, reporte semanal y avisos; feat(lote-traslado-ui): bloquear encaset futuro y fecha de traslado en hora local
Bugs encontrados: 2 — cada uno queda como subtarea BUG con su causa.
Evidencia: 7 archivos tocados · 2,1 h de sesión real · commits 2c1f396, 957330f, b917ad9, ea585fd, 27add00, 2a86978, e0a0fe3, 51a25f7, 4109b01, fd3e7f8 · sesión 0a8877b7
Estimación: 14 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0014-T5' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/postura_verenice_rev_6jul26_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 8.00, descripcion = 'Plan: fase_de_desarrollo/inventario_multiempresa_scoping_plan.md

── Bitácora de la sesión (2026-07-17) ──
Pedido: «tengo un error el modulo de inventraio quedo para milti empresa entonces las granjas que me debe trar son las que el usuario tiene en sesion y pertenece y las granjas son las que el usaurio tiene asignadas tmaibne por que actual mente me trae las de pollo engorde qeu este mo dulo estaba para pollo engorde antes , pero ahroa quedo para postura la cuestion de alimento me trae solo las de ecuador deberia ser si es diferente a ecuador trae las del paise que es y las granjas del usaurio que estan en otro tabla difeernete a las de pollo engorde eso se me paso por que en produccion no esta aplciado»
Solución (1 commit): Merge pull request #37 from ItalcolColombia/fix/inventario-scoping-multiempresa
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 19 archivos tocados · 1,6 h de sesión real · commits 8a94e61, d7c6b53 · sesión a1eb99ed
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0007-T13' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/inventario_multiempresa_scoping_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 10.00, descripcion = 'Plan: fase_de_desarrollo/liquidacion_panama_por_corrida_plan.md

── Bitácora de la sesión (2026-07-20) ──
Pedido: «necesito organizar la logica en el modulo de liquidacion ya que tenemos no me busca por corrida sino por el lote ve la logica de panama y integrala a lo que se tiene ahroa ya que actual mente esta funcional para ecuador es integrar esta opcon para cuando es panama que es diferrente y valdia lo de trar la data correcta de que se tieen cargada : en lso dos tap que se tiene el tap de indicador general si me trae los datos de panama , pro ahroa esta en el tap pollo engorde que esta amarrado a la logica de ecuador»
Solución (2 commits): feat(liquidacion-panama): busqueda por corrida en el tab Pollo Engorde del indicador; feat(liquidacion-panama): busqueda por corrida en el tab Pollo Engorde del indicador
Bugs encontrados: 0.
Evidencia: 26 archivos tocados · 1,1 h de sesión real · commits f4179af, ae86bbd · sesión 430fae23
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0008-T6' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/liquidacion_panama_por_corrida_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 20.00, descripcion = 'Plan: fase_de_desarrollo/modulo_implementacion_plan.md

── Bitácora de la sesión (2026-07-20) ──
Pedido: «me gustaria un modulo de implementacion donde coloco por empresa y asigno a roles , donde creare un cronograma de implemntacion con check como ejemplo , una que sea parametrizaciones , y si se cumple da chekc y coloca la fecha y el usuario al que se le asigno el confirma en su perfil que se cumplio al final del chekc que se tenga asi garantizamos entregas de la aplicacion y cpaciotacioens por usuarios asi gestionamos qeu se entrega y controlar mejro la uditoria de la aplciacion y sea por empresa y usuario pais es para poder entregar check list de implementacioens de la aplicaicon y crear crono»
Solución (3 commits): feat(implementacion): modulo de cronogramas de entrega por empresa con checklist confirmable; feat(implementacion): modulo de cronogramas de entrega por empresa con checklist confirmable; Merge branch ''postura-verenice-rev-6jul26''
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 51 archivos tocados · 2,5 h de sesión real · commits f39b627, 0d82106, 765e806, c23d9bc · sesión b02eb1e1
Estimación: 20 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0002-T1' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/modulo_implementacion_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 14.00, descripcion = 'Plan: fase_de_desarrollo/reporte_diario_costos_engorde_plan.md

── Bitácora de la sesión (2026-07-20 → 2026-07-22) ──
Pedido: «necesito crear este reporte para pollo engorde con los seguimiento diarios que se tiene enotnces debe ser asi este reprote valida y saca toda la informacion de donde la necestamos»
Solución (2 commits): feat(engorde): reporte diario costos por granja + lote base global con permisos; feat(engorde): lote base obligatorio en Panama con tab de gestion y vigencia anual
Bugs encontrados: 0.
Evidencia: 58 archivos tocados · 2,1 h de sesión real · commits eda83c9, 640d2a5 · sesión fda9c853
Estimación: 14 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0012-T16' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/reporte_diario_costos_engorde_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 10.00, descripcion = 'Plan: fase_de_desarrollo/implementacion_checklist_v2_plan.md

── Bitácora de la sesión (2026-07-21 → 2026-07-22) ──
Pedido: «el modulo de cherlis organizalo el diseno y sus filtros por que nunca cargan se quedan pensando a que la peticion retorno y esta muy fuera del diseno la estaequica del modulo,debe tener algo como al crear un cornograma de chek coloco una descriccion , y una fecha de implemntacion de los cket de entrega y que sierva tambine para capacitaciones , y luego de eso cuando creo el cronograma paso a crear sus iten de valdiacion donde coloco fechas decir el cronograma es implementacion panama , del 1 al 6 de julio en la descriccon coloco, integrar intalgranja en todo panama etc , ycuando gaurdo , ya co»
Solución (1 commit): feat(implementacion): checklist v2 con firmas de participantes y tipo de cronograma
Bugs encontrados: 2 — cada uno queda como subtarea BUG con su causa.
Evidencia: 60 archivos tocados · 0,9 h de sesión real · commits 28f9336, dba28e9, c4755b9 · sesión 2358689a, 97a5e50d
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0002-T2' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/implementacion_checklist_v2_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 6.00, descripcion = 'Plan: fase_de_desarrollo/fix_consumo_inventario_colombia_multiempresa_plan.md

── Bitácora de la sesión (2026-07-21) ──
Pedido: «tenog un error al momento de realziar un consumo en el modulo de levante que es el liguinte : Request URL Request Method POST Status Code 400 Bad Request Remote Address 52.14.252.89:443 Referrer Policy strict-origin-when-cross-origin 1. 2. 3. 4. 5. 6. ﻿ main-K645UVIE.js:1095 ✅ Sesión guardada. Verificación: 1. Object main-K645UVIE.js:1095 ✅ Menú desencriptado correctamente 1. Object ﻿{""fechaRegistro"":""2026-07-30T17:00:00.000Z"",""loteId"":""123"",""lotePosturaLevanteId"":15,""mortalidadHembras"":0,""mortalidadMachos"":0,""selH"":0,»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 28 archivos tocados · 0,7 h de sesión real · commits 1c172df · sesión 5ae70915
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0007-T14' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/fix_consumo_inventario_colombia_multiempresa_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 5.00, descripcion = 'Plan: fase_de_desarrollo/fix_fecha_menos_un_dia_engorde_plan.md

── Bitácora de la sesión (2026-07-21 → 2026-07-22) ──
Pedido: «en el modulo de pollo engorde y al momento de crear un lote pollo engorde , crear los lotes reproductoras y al momento de realziarle seguimiento al lote pollo engorde y seguimiento a reproductora pollo engorde esta tomando una fecha menos de la que esta registrada entonces no me esta mostrando la fecha correcta la que coloco con la que muestra en la tabla o en el seguimiento de los dos modulos validar el formatiador de fecha no me quite un dia habil»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: 42 archivos tocados · 0,7 h de sesión real · sesión ac6c64f1, 97a5e50d
Estimación: 5 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0012-T15' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/fix_fecha_menos_un_dia_engorde_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 2.00, descripcion = 'Plan: fase_de_desarrollo/rate_limiting_ajuste_bloqueo_ip_plan.md

── Bitácora de la sesión (2026-07-21 → 2026-07-22) ──
Pedido: «en produccion tengo este erro quiero validar si tengo un servico que me cambia el estado y cuanto tiempo es la espera para que se desbloque los usuarios que se loquearon si no parz cambiar el tiempo de espera en produccion a menos tiempo pero que evite ataques : 🚫 Acceso Bloqueado: Tu IP ha sido bloqueada temporalmente. Intenta nuevamente más tarde.»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: 11 archivos tocados · 0,2 h de sesión real · sesión 3a26f219, 97a5e50d
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0004-T3' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/rate_limiting_ajuste_bloqueo_ip_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 8.00, descripcion = 'Plan: fase_de_desarrollo/admin_empresa_granjas_plan.md

── Bitácora de la sesión (2026-07-22) ──
Pedido: «1. Módulo Roles: Nueva configuración (Vista exclusiva Super Admin) En el módulo de Roles, se agregará una opción accesible únicamente para el rol Super Admin / Admin General. Configuración del Formulario de Rol: * Nombre del Rol: (Ej. Administrador Panamá) * País: [ Dropdown: Panamá ] * Empresa: [ Dropdown: Intalcol ] * [ ☑ ] Es Administrador de Empresa/País (Checkbox o Switch toggle) Nota técnica: Al activar esta casilla, este rol adquiere un permiso global a nivel de base de datos para heredar todas las entidades activas de la empresa seleccionada. 2. Módulo Usuarios: Comportamiento de Asi»
Solución (1 commit): feat(roles): flag Administrador de Empresa (solo Super Admin) + visibilidad global de granjas al asignar usuarios
Bugs encontrados: 0.
Evidencia: 29 archivos tocados · 0,7 h de sesión real · commits aa49466 · sesión 0429cf21
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0005-T3' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/admin_empresa_granjas_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 8.00, descripcion = 'Plan: fase_de_desarrollo/sesion_deslizante_inactividad_plan.md

── Bitácora de la sesión (2026-07-22) ──
Pedido: «en el modulo de seguimiento diario reporductora pollo engorde tenemos que agregar un validador de cada registro ya que actual mente si tengo uno o dos o mas lotes reproductoraas en un lote se sincroniza automatica mente en en seguimiento pollo engorde , pero esta ves tenemo que validar que cuando tengamos un cehckt que nos confirme si la informacion esta correcta se sincroniza con lla misma ogica que esta acutla , pero si es esto es para poder tener avilitado la fase de sincronizacion con una validacion extra , y validamso que descpues de confirmar no se peude editar el registro pero como es»
Solución (1 commit): dev
Bugs encontrados: 0.
Evidencia: 43 archivos tocados · 1,1 h de sesión real · commits 4067a23 · sesión aeb83bdd
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0004-T4' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/sesion_deslizante_inactividad_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 8.00, descripcion = 'Plan: fase_de_desarrollo/confirmacion_seguimiento_reproductora_engorde_plan.md

── Bitácora de la sesión (2026-07-22) ──
Pedido: «en el modulo de seguimiento diario reporductora pollo engorde tenemos que agregar un validador de cada registro ya que actual mente si tengo uno o dos o mas lotes reproductoraas en un lote se sincroniza automatica mente en en seguimiento pollo engorde , pero esta ves tenemo que validar que cuando tengamos un cehckt que nos confirme si la informacion esta correcta se sincroniza con lla misma ogica que esta acutla , pero si es esto es para poder tener avilitado la fase de sincronizacion con una validacion extra , y validamso que descpues de confirmar no se peude editar el registro pero como es»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión aeb83bdd
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0009-T8' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/confirmacion_seguimiento_reproductora_engorde_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 10.00, descripcion = 'Plan: fase_de_desarrollo/lote_base_engorde_por_granja_plan.md

── Bitácora de la sesión (2026-07-22) ──
Pedido: «en el modulo de crear lote baase para pollo engorde vamos a cambiar la logica del ese modulo la idea es que solo nos pedira nombre de lote tomara la fecha de activacion y captura el usuario que lo realizo , luego de eso , l oque va realizar es cuando tengamos el lote creado base , tendresmo una opcion para asignar granjas la misma que tenemos en usuario que adinamos granja , si el usuario tiene como administrador de la empresa le trae todas la granjas , la idea es eso , en este modulo que en la parte qeu aparece el lote trae las granjas para asingar y este filtro es para que este lote sea vi»
Solución (1 commit): feat(engorde): lote base pollo engorde por granja + creacion solo-nombre, sin vigencia por año
Bugs encontrados: 0.
Evidencia: 32 archivos tocados · 1,1 h de sesión real · commits 39bc689 · sesión 244026a1
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0005-T7' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/lote_base_engorde_por_granja_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 3.00, descripcion = 'Plan: fase_de_desarrollo/qq_a_kg_alimento_seguimiento_engorde_plan.md

── Bitácora de la sesión (2026-07-22) ──
Pedido: «necesito que en modulo de seguimeinto pollo engorde en la parte de crear un registro de seguimiento tengamos en la conversion de donde me sasake kg y g agregamso la conversion de qq a kilos ellos va agregar qq en panama dejarla por decto de primera en panama y que realzie la conversion en la parte de abajo muestre lo que se va a guardar en consumo en kg siempre»
Solución (1 commit): feat(engorde): unidad qq (quintal) en alimento del seguimiento pollo engorde
Bugs encontrados: 0.
Evidencia: 11 archivos tocados · 0,4 h de sesión real · commits 2e68db6 · sesión c748f70e
Estimación: 3 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0012-T18' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/qq_a_kg_alimento_seguimiento_engorde_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 6.00, descripcion = 'Plan: fase_de_desarrollo/cierre_lote_reproductora_por_confirmacion_plan.md

── Bitácora de la sesión (2026-07-22) ──
Pedido: «necesito el permiso de confirmar registro en pollo engorde que tengo aqui en pruebas en una migracion pro que no esta aplicada»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 10 archivos tocados · 0,8 h de sesión real · commits 0fcda75 · sesión 42005ea3, 871c1f23
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0008-T8' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/cierre_lote_reproductora_por_confirmacion_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 8.00, descripcion = 'Plan: fase_de_desarrollo/gestion_granjas_cascada_refresh_plan.md

── Bitácora de la sesión (2026-07-22) ──
Pedido: «en el modulo gestion de granjas validame que cuando yo elimine una granja se desabiltia sus nucleos y galpones , de una y actualzia los servicios de cada tap y igual cuando creo algo actualzia los servicios de galpon y nucleo para que tenga al dia todo , ya que ahro si creo una granja y paso al nucleo tengo que cargar la aplciacion o al momento de eliminar no elimina todo y me trae toda la ifnormacion , tmaibne necesito que me traiga los nucleo y galpones que corresoinden a mi usuario asingados a la granja , si tengo una granja me refleja su informacion ya que actual mente en algunso caso me t»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: 31 archivos tocados · 0,5 h de sesión real · sesión 0973632d
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0005-T6' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/gestion_granjas_cascada_refresh_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 8.00, descripcion = 'Plan: fase_de_desarrollo/lote_engorde_corrida_panama_plan.md

── Bitácora de la sesión (2026-07-22) ──
Pedido: «en el modulo que creamos los lotes pollo engorde actual mente nos muestra los lotes base que tenemso creados ahora necesito que valide si el lote base esta ya en el galpon selecionado se le asinga el siguinte nuemero es decir si es el primero coge el nombre del lote 96 y referencia de primero 1 , y el nombre a mostrar seria el 96 - 1 entonces este es el nombre del lote pero tamibne guardamos el lote base de pollo enrde el id o el nombre ya que seria una casilla donde vemos el lote base que queda asociado y el campo nombre lote se crea con el lote lote base y el numero de la corrida que seri»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 18 archivos tocados · 0,5 h de sesión real · commits 944c9c6 · sesión 2b4164b7
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0012-T17' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/lote_engorde_corrida_panama_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 5.00, descripcion = 'Plan: fase_de_desarrollo/seguimiento_produccion_hereda_lote_padre_plan.md

── Bitácora de la sesión (2026-07-22) ──
Pedido: «tengo este error caundo estemos utilizando el tack vamos a compartir con otra sesion la idea es que no borres el archivo sino agrega las tareas de esta sesion para que continue la solucion este solucion es para postura que son levante y produccion : el modulo de seguimiento diario produccion el lote tiene lote base y no deja guardar un seguimiento por que dice que no tiene lote base asignado ya que el lote base no es obligatorio para guardar el registro , pero para los lote que si lo tiene por que falla ya que esto es en el modulo de levante , produccion , lo que pasa es cuando cierro un lote»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 15 archivos tocados · 0,6 h de sesión real · commits 967e490 · sesión 58564f2d
Estimación: 5 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0014-T6' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/seguimiento_produccion_hereda_lote_padre_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 3.00, descripcion = 'Plan: fase_de_desarrollo/reabrir_lote_reproductora_no_persiste_plan.md

── Bitácora de la sesión (2026-07-22) ──
Pedido: «en el modulo de seguimiento reproductora pollo engorde tenemso una opcion que se llama reabrir lote , pero no esta funcionando la idea es que pueda abrir el seguimiento ya que no deja cuando dejo una nota aparece que esta confirmado pero cuando paso a eliminar unregistro dice que no se peude hasta abrir el seguimiento quiere decir que confirma sin aplciarlo»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 10 archivos tocados · 0,3 h de sesión real · commits da3bf77 · sesión 871c1f23
Estimación: 3 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0008-T7' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/reabrir_lote_reproductora_no_persiste_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 14.00, descripcion = 'Plan: fase_de_desarrollo/22_mixto_agua_reapertura_cruce_reproductora_engorde_plan.md

── Bitácora de la sesión (2026-07-22) ──
Pedido: «en el modulo de seguimiento reproductora pollo engorde tenemso una opcion que se llama reabrir lote , pero no esta funcionando la idea es que pueda abrir el seguimiento ya que no deja cuando dejo una nota aparece que esta confirmado pero cuando paso a eliminar unregistro dice que no se peude hasta abrir el seguimiento quiere decir que confirma sin aplciarlo»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 871c1f23
Estimación: 14 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0009-T4' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/22_mixto_agua_reapertura_cruce_reproductora_engorde_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 6.00, descripcion = 'Plan: fase_de_desarrollo/lote_reproductora_engorde_ajustes_creacion_plan.md

── Bitácora de la sesión (2026-07-22) ──
Pedido: «en el modulo de lote reproductora pollo engorde donde creamos los lotes al momento de crearlos vamos a quitar el campo codigo reproductora , en nombre reproductora que sea obligatorio pero no coloque el nomrbe del lote pricipal sino que este null vacio para que el usaurio lo asigne : en edad captura la edad qeu deve tener el lote hasta finalziar el lote es que ahroa muestra la edad real con la fecha del sistema enocnes si la edad es de 1 a 7 valdia con el dia de hoy y puede darme que es 14 en el campo edad y valdiar si elimino un registro que tiene datos ya cargadso debe obligar a que elimin»
Solución (1 commit): feat(engorde): ajustes lote reproductora — creación sin código, edad congela al cerrar, borrado seguro y permisos
Bugs encontrados: 0.
Evidencia: 18 archivos tocados · 0,6 h de sesión real · commits 97665c4 · sesión 22a48a6c
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0009-T7' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/lote_reproductora_engorde_ajustes_creacion_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 14.00, descripcion = 'Plan: fase_de_desarrollo/gestion_ubicacion_nucleo_galpon_lote_plan.md

── Bitácora de la sesión (2026-07-22) ──
Pedido: «tengo un error si voy a editar un nucleo que le cambio el nombre o la granja que tiene asignada , los galpones no se actualizan y si solo quiero cambiar el galpon de nucleo se crea otro galpon pero solo es una edicion interna del galpon no es eliminacion ni cambiando a otra granja , la idea es que es un crud que puedo cambiar un lote de ubicacion en su granja o la granja en otro galpon o nucleo a otra granja , y igual es lo de eliminar que tenga el lfujo completo ya que en produccion paso edite un galpon de nucleo y se creo otro registro enotnces quedo el erro arriba hasta que migre la inform»
Solución (1 commit): feat(ubicacion): mover/eliminar seguro de nucleo/galpon/lote (transversal, sin duplicar ni huerfanos)
Bugs encontrados: 0.
Evidencia: 37 archivos tocados · 1,2 h de sesión real · commits 100c343 · sesión 499407d8
Estimación: 14 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0005-T5' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/gestion_ubicacion_nucleo_galpon_lote_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 6.00, descripcion = 'Plan: fase_de_desarrollo/seguimiento_reproductora_engorde_fechas_edicion_plan.md

── Bitácora de la sesión (2026-07-22) ──
Pedido: «estoy en el modulo Seguimiento Diario Reproductora Pollo Engorde y tengo un error en fechas en produccion realizaron esto y primero es que el primer registro esta sumando un dia mas al mostrar entonces no se sincronizo los primero consumo y no cuadra validar que la fecha de creacion del regitro sea lo mismo que meustra en la tabla ya que si no lo suma le quita un dia entonces tenemos ese error de sincronizacion , tamibne que no me deje colocar un dia menos de la fecha de encacetamiento del lote repeoductora , tambien valdia que si abro el seguimeinto pueda editar la fecha y algunos campos co»
Solución (1 commit): modal seguimiento reproductora
Bugs encontrados: 0.
Evidencia: 16 archivos tocados · 0,5 h de sesión real · commits 111bc9d · sesión 2fcb6305
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0009-T9' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/seguimiento_reproductora_engorde_fechas_edicion_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 5.00, descripcion = 'Plan: fase_de_desarrollo/fix_nombres_lote_engorde_panama_por_lote_base_plan.md

── Bitácora de la sesión (2026-07-22) ──
Pedido: «ahora necesito que tengamos una migracion de los lotes de pollo engorde de panama con el cambio de que ahroa el nombre del lote se asigna del lote base selecionado ya tengo esos lotes en produccion creados anterior mente no cumple con el prefijo del numero de identificacion del lote para los nombre enotnces ahroa subo la solucion pero me toca crear una migracion que corrija los nombres si no lo tiene de acurdo al lote base que tiene asignados , es para alinear , en el track tengo otra sesion al finalziar esta no bbore el track y agregas al final lo que necesitas aqui en la solucion y realiza»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 12 archivos tocados · 0,4 h de sesión real · commits 4893032 · sesión 25e1c3a2
Estimación: 5 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0005-T4' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/fix_nombres_lote_engorde_panama_por_lote_base_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 8.00, descripcion = 'Plan: fase_de_desarrollo/guia_genetica_panama_ross308ap_2022_plan.md

── Bitácora de la sesión (2026-07-22) ──
Pedido: «valida este archivo y asigna estas tablas geneticas por raza a la empresa panama creame la migracion de asignacion de la tbal genetica ya que con la mixta esta trabjando panama del ño 2022 entonces por ahroa no me interesa cargar macho ni hembras solo mixtas por raza valida el modulo que tenemos de tabla genetica que utiliza ecuador para utilizarlo y realizar la migracion correcta en el pais y eliminamos la que esta cargada actual mente en panama por que no es la correcta»
Solución (1 commit): feat(engorde): guia genetica Panama Ross 308 AP 2022 mixto + repunte lotes
Bugs encontrados: 0.
Evidencia: 12 archivos tocados · 0,6 h de sesión real · commits 85cc582 · sesión 87437f4d
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0011-T3' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/guia_genetica_panama_ross308ap_2022_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 5.00, descripcion = 'Plan: fase_de_desarrollo/lote_reproductora_engorde_ux_cascada_plan.md

── Bitácora de la sesión (2026-07-22 → 2026-07-23) ──
Pedido: «el diseno y estetica que tengo en el modulo Seguimiento Diario Reproductora Pollo Engorde quiero tenerlo en el momento de crealo ya que me meustra en numero tambine la forma de secuencia que se tiene que hacer»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: 20 archivos tocados · 1,0 h de sesión real · sesión 32596426
Estimación: 5 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0009-T10' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/lote_reproductora_engorde_ux_cascada_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 5.00, descripcion = 'Plan: fase_de_desarrollo/seguimiento_pollo_engorde_ux_cascada_scroll_plan.md

── Bitácora de la sesión (2026-07-22 → 2026-07-23) ──
Pedido: «el diseno y estetica que tengo en el modulo Seguimiento Diario Reproductora Pollo Engorde quiero tenerlo en el momento de crealo ya que me meustra en numero tambine la forma de secuencia que se tiene que hacer»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 32596426
Estimación: 5 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0012-T20' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/seguimiento_pollo_engorde_ux_cascada_scroll_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 10.00, descripcion = 'Plan: fase_de_desarrollo/codigo_erp_granja_engorde_panama_plan.md

── Bitácora de la sesión (2026-07-22 → 2026-07-23) ──
Pedido: «ahroa necesito que el codigo erp que se solicita en el modulo de pollo engorde al moemnto de crear el lote ese codigo erp va definido al momento de crear la granja este cambio es solo para panama , ya que cuando cree el codigo erp todos los lotes que creee en la granja capturan el codigo erp que debe estar en la granja , la idea es que cuando se cierra o liquida un lote completo en una granja es decir si el lote base 17 que se cro en la granja maria que tiene galpon 1 tiene el 17-1 y 17-2 y en el galpon 3 tiene el 17-1 la idea es caundo se cierra todo los lotes en esa granja de ese lote pa»
Solución (2 commits): ux(engorde): cascada numerada, info colapsable y scroll unico en seguimiento y reproductora; feat(engorde): codigo ERP por granja Panama con avance automatico al cerrar el ciclo
Bugs encontrados: 0.
Evidencia: 21 archivos tocados · 0,9 h de sesión real · commits 63a46a9, a4aa012 · sesión 46047129
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0012-T19' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/codigo_erp_granja_engorde_panama_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 12.00, descripcion = 'Plan: fase_de_desarrollo/migracion_masiva_seguimiento_engorde_reproductora_plan.md

── Bitácora de la sesión (2026-07-23) ──
Pedido: «tenemos un modulo para hacer carga masiva de seguimiento pollo engorde reproductora y el seguimeinto de lote pollo engorde , que cada uno tiene la logica que tiene el front entonces necesito que tenga todo actualziado por que le meti otras validaciones , como en las reprroductoras deben tener una confirmacion si las estoy cargando en carga masiva eso va en acetacion de una entonces valida esos modulo con la migraciones masivas y me das tamibne la plantilla para cada uno de ellos cuando la escoja para cargar»
Solución (3 commits): feat(migraciones): carga masiva seguimiento reproductora engorde con confirmacion automatica; feat(migraciones): seguimiento engorde por nombres + alimentos del inventario en carga masiva; ux(migraciones): plantilla reproductora sin columnas de ubicacion (el lote sale del filtro)
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 41 archivos tocados · 1,6 h de sesión real · commits 93f5199, d95edd5, b73d727, d509c93 · sesión 92cbee1c
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0006-T10' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/migracion_masiva_seguimiento_engorde_reproductora_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 6.00, descripcion = 'Plan: fase_de_desarrollo/seguimiento_reproductora_engorde_edad_cero_plan.md

── Bitácora de la sesión (2026-07-24 → 2026-07-25) ──
Pedido: «TENGO UN ERROR AL MOEMNTO DE CARGAR UCA CARGA MASIVA DE REPRODUCTORA DE POLLO ENGORDE ENTONCES AHROA TTRATE DE CARGAR CON UNA FECHA 16/07/2026 Y TRATO DE APLICAR AL MIMOS DIA QUE TIENE ENCAQCETAMIENTO LA REPRODUCTORA ME SALE QUE NO SE UEDE LA MISMA FECHA DE ENCETAMIENTO SI LA IDEA ES QUE SEA LA MISAM FECHA , LO QUE NO SE PUEDE ES QUE SEA EL 15»
Solución (1 commit): fixx del cambios
Bugs encontrados: 0.
Evidencia: 21 archivos tocados · 2,0 h de sesión real · commits dd2c923 · sesión fe5752b1
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0009-T11' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/seguimiento_reproductora_engorde_edad_cero_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 6.00, descripcion = 'Plan: fase_de_desarrollo/limpieza_seguimientos_engorde_panama_plan.md

── Bitácora de la sesión (2026-07-25) ──
Pedido: «ahroa necesito que os seguimientos diario reprodcutora y seguimiento diario pollo engorde de todos los lotes que son de panama limpiarlos para dejarlo ya en carga masiva que se implemento para evitar erroes que se tenga en la digitacion pero es limpiar los registros de seguimeinto diario entonce descargue la base de datos de produccion al local para que lo realizemos y realizemos la limpieza de los seguimientos si cumple pasamos a crear una migraicion para desplegar a produccion»
Solución (1 commit): feat(panama): limpieza total seguimientos diarios e inventario alimento para re-carga masiva
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 17 archivos tocados · 0,8 h de sesión real · commits c7b7ba7, 0cb8eec · sesión bcf9f0db
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0012-T21' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/limpieza_seguimientos_engorde_panama_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 32.00, descripcion = 'Plan: fase_de_desarrollo/santa_reyes_implementacion_plan.md

── Bitácora de la sesión (2026-07-25) ──
Pedido: «este es un desarrolo nuevo para una empresa nueva que entra en colombia , que es Santa Reyes , hay que creale toda la secuencia para crear empresas , esta empresa no exites actual hay datos que tiene que ser ficticios de los los campos que no se tengan , tendremos roles admin , implementador , tendrna los mismos permisos para la empresa , los modulos que utilizaran son todo los de levante y»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: 9 archivos tocados · 4,0 h de sesión real · sesión 2672b21b
Estimación: 32 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0002-T3' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/santa_reyes_implementacion_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 12.00, descripcion = 'Plan: fase_de_desarrollo/diseno_filtros_unificado_plan.md

── Bitácora de la sesión (2026-07-25) ──
Pedido: «quiero que en todo los modulos que utilizen filtado de infrmacion aplicar este mismo dise;o para que quede definidos en todo y el diseno completo que esta aqui en cada arte que va un filtro o un select tenga este diseno lanzas de acuerdo agentes cin opus , sonnet o fable donde corresponde por esfuerzo lista simepre tosos los modulos luego aplcialo en el plan para que realizes en secuencias hasta terminar y siemroe hay otra sesiones realizando trabajos en el track entonces debe convivir esta sesion y las otras»
Solución (2 commits): feat(santa-reyes): implementacion completa empresa Santa Reyes (fases 1-5) + ux filtros de contexto unificados; feat(demo): activar features Santa Reyes en la empresa Demo para evaluacion del cliente
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 14 archivos tocados · 2,7 h de sesión real · commits 7347cf8, 49e3800, 4691c49 · sesión 5baf8ec3
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0017-T4' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/diseno_filtros_unificado_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 6.00, descripcion = 'Plan: fase_de_desarrollo/lote_base_santa_reyes_correccion_plan.md

── Bitácora de la sesión (2026-07-26) ──
Pedido: «en la migracion para eanta reyes de los lotes me creo los lotes seguimiento pero los lotes que estan hya seria lo lotes base ya que esos no son con las aves de encacetamiento entonces estari mal la migracion , entonces corrige esto y valida que en lote base que nos hace falta en campos corrijamos y apliquemos bien la migracion y impia lo que se creo con la corrida de la migracion , santa reyes y en demo >»
Solución (1 commit): Merge remote-tracking branch ''origin/main''
Bugs encontrados: 0.
Evidencia: 15 archivos tocados · 1,3 h de sesión real · commits 8c1ae34 · sesión 355c5ce7
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0005-T8' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/lote_base_santa_reyes_correccion_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 4.00, descripcion = 'Plan: fase_de_desarrollo/demo_huevos_clasico_sanmarino_plan.md

── Bitácora de la sesión (2026-07-26) ──
Pedido: «en la migracion para eanta reyes de los lotes me creo los lotes seguimiento pero los lotes que estan hya seria lo lotes base ya que esos no son con las aves de encacetamiento entonces estari mal la migracion , entonces corrige esto y valida que en lote base que nos hace falta en campos corrijamos y apliquemos bien la migracion y impia lo que se creo con la corrida de la migracion , santa reyes y en demo >»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 355c5ce7
Estimación: 4 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0014-T8' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/demo_huevos_clasico_sanmarino_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 6.00, descripcion = 'Plan: fase_de_desarrollo/fix_seguimiento_produccion_lote_id_plan.md

── Bitácora de la sesión (2026-07-26) ──
Pedido: «tengo un error en produccion al momento de realziar un seguimeinto diario en produccion pase lo que esta en produccion a local y tengo un error de la fase de produccion , que no tengo un dato que necesita para crear un seguimiento diario , ya que los lotes creados no tiene lotebase , asingado jajajajajaj la cosa es que puede haber lotes que no tenga lote base entonces puede ser que no tenga , en esta parte si tien lote base creado entonces deberia cogerlo corrigajos para que en produccion lo pase > esto paso en la empresa demo no quiero que pase en empresas qeu despleigue postura entonces cor»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 3 — cada uno queda como subtarea BUG con su causa.
Evidencia: 13 archivos tocados · 1,1 h de sesión real · commits c5b74a4, 645535b, f783bf5 · sesión e7b77f42
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0014-T7' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/fix_seguimiento_produccion_lote_id_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 20.00, descripcion = 'Plan: fase_de_desarrollo/scope_ubicacion_usuario_granja_plan.md

── Bitácora de la sesión (2026-07-26) ──
Pedido: «necesito agregar una funcion nueva al momento de asignar una granja a un usuario dentro del mismo modulo podre selecionar tamibne a que nucleo , galpon y hasta el lote tiene permiso ese usuario o dejarlo global por granja se puede tamibne la idea es que se pueda aplicar ese nivel de filtro , enotnces con ese cambio en todo los filter que se tiene se debe aplicar esta condicion ya que no podria traer toda la info de la grnja ahroa si tiene un lote o galpon solo le trae la informacion de ese lugar realiza la validacion completa levanta agentes y validad si salen con sonnet o opus o fable 5 en ca»
Solución (1 commit): feat(seguridad): alcance granular usuario-granja (nucleo/galpon/lote o global) aplicado a filtros y datos
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 73 archivos tocados · 1,9 h de sesión real · commits d492eed, 9534528 · sesión 2d7dbcc4
Estimación: 20 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0004-T5' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/scope_ubicacion_usuario_granja_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 20.00, descripcion = 'Plan: fase_de_desarrollo/reporte_tecnico_semanal_postura_plan.md

── Bitácora de la sesión (2026-07-26) ──
Pedido: «en sanmarino tenemos estos dos reprotes semanales de sanmarino solo cuando se cree se aplicaran a la empresa sanmarino en reprote seran dos levante y postura , entonces puede ser uno solo modulo que tenga las dos opcioens para generar y comparas con la guia genetica cargada para sanmarino > no soros tenemso todo lo que son seguimientos diarios levante y produccion y tenemos lotes bases y tenemos todo lo que son consumos mortalidades , tmabne los huevos tenemso la carga basia»
Solución (3 commits): feat(reportes): modulo Reporte Tecnico Semanal postura (Levante + Produccion vs guia genetica); feat(reportes): bloque POLLITOS del Reporte Tecnico Semanal con HI Cargado real; docs(tracker): cierre fase 2 del Reporte Tecnico Semanal
Bugs encontrados: 0.
Evidencia: 43 archivos tocados · 2,4 h de sesión real · commits 3dd1f4a, 0b3b79f, 6abd735 · sesión 4cb1bac3
Estimación: 20 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0014-T9' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/reporte_tecnico_semanal_postura_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 14.00, descripcion = 'Plan: fase_de_desarrollo/huevos_levante_semana14_arrastre_produccion_plan.md

── Bitácora de la sesión (2026-07-26 → 2026-07-27) ──
Pedido: «realiza un cambio en el seguimiento diario levante que apatir de la semna 14 debe tener un campo que se llama huevo que es lo mismo que se realiza en seguimiento diario produccion que se clasifica los heuvos que tengan por dia en esa fase , la idea final es que cuando se realize la liquidacion esos heuvos pasan automatica mente para la primerea semana de produccion , y eso es cuando se liquide levante , aparece el tota lde huevos y los tipos de heuvos qeu se octuvieron en levante y cuando se levanta automatica mente produccion se crea el primer registro de huevos sumando todos entonces si es e»
Solución (5 commits): feat(levante): huevos desde semana 14 con arrastre al primer registro de produccion; docs(tracker): cierre de huevos en levante semana 14 + arrastre a produccion; docs(levante): contexto de traspaso y fase 7 de alineacion de huevos en levante; feat(levante): columnas de huevos en la tabla diaria y su Excel + fix del trigger del espejo; docs(levante): diseno resuelto de P2 (carga masiva con huevos) en el contexto de traspaso
Bugs encontrados: 0.
Evidencia: 16 archivos tocados · 2,2 h de sesión real · commits 34e47aa, 4b7282b, 2bf84f6, 1d19c24, 8ab19ec · sesión 57b94fef
Estimación: 14 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0013-T5' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/huevos_levante_semana14_arrastre_produccion_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 2.00, descripcion = 'Plan: fase_de_desarrollo/CONTEXTO_TRASPASO_HUEVOS_LEVANTE.md

── Bitácora de la sesión (2026-07-26 → 2026-07-27) ──
Pedido: «realiza un cambio en el seguimiento diario levante que apatir de la semna 14 debe tener un campo que se llama huevo que es lo mismo que se realiza en seguimiento diario produccion que se clasifica los heuvos que tengan por dia en esa fase , la idea final es que cuando se realize la liquidacion esos heuvos pasan automatica mente para la primerea semana de produccion , y eso es cuando se liquide levante , aparece el tota lde huevos y los tipos de heuvos qeu se octuvieron en levante y cuando se levanta automatica mente produccion se crea el primer registro de huevos sumando todos entonces si es e»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 57b94fef
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0013-T6' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/CONTEXTO_TRASPASO_HUEVOS_LEVANTE.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 14.00, descripcion = 'Plan: fase_de_desarrollo/venta_engorde_panama_peso_diferido_y_carga_masiva_plan.md

── Bitácora de la sesión (2026-07-27) ──
Pedido: «para pollos engorde necesito en el modulo carga masiva necesito realizar carga masiva de las ventas quiere decir todos los campos que utilizo en ventas necesito colocarlo en la apliacion del peso , tamibne necesito que en panama cuando esten realizando regsitros de venta no debe pedir obligatorio lo que es el peso tara y peso bruto ya que eso datos lo tiene al dia siguinte entonces hay colocan el peso al momento de realzia la confirmacion de la venta se abre el modal de registro de peso para que coloquen los datos de peso que de la venta asi obligamos que al momento de la venta no sea obligat»
Solución (1 commit): feat(engorde): peso bascula diferido en ventas (Panama) + carga masiva de ventas multi-lote
Bugs encontrados: 0.
Evidencia: 41 archivos tocados · 1,3 h de sesión real · commits 6dd4d53 · sesión 3cc1a34a
Estimación: 14 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0006-T11' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/venta_engorde_panama_peso_diferido_y_carga_masiva_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 8.00, descripcion = 'Plan: fase_de_desarrollo/recepcion_transito_distribucion_galpones_plan.md

── Bitácora de la sesión (2026-07-27) ──
Pedido: «en el modulo de gestion de inventario , en el tap de trancito necesito que cuando se aceptar el traslado de alimento , y se acepta en un solo galpon para cuando es alimento ya si es otro item ya es sobre la granja entonces no aplciaria esta logica , la idea es que si llega 1000 kg de alimento entonces necesito dentro de la granja pueda distribuir sobre los galpones que exitan en la granja puedo distribuir lo que llega entre los galpones , ya que actual mente solo se recibe sobre uno , entonces yo puedo resibir pero distribuir sobre varios galpones»
Solución (1 commit): feat(inventario): recepcion de transito distribuida entre varios galpones
Bugs encontrados: 0.
Evidencia: 16 archivos tocados · 0,5 h de sesión real · commits b124bf6 · sesión a5b3405f
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0007-T16' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/recepcion_transito_distribucion_galpones_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 10.00, descripcion = 'Plan: fase_de_desarrollo/cierre_levante_reapertura_validada_y_cierre_produccion_plan.md

── Bitácora de la sesión (2026-07-27) ──
Pedido: «neceisto hacer un cambio cuando realizo cierre de seguimiento levante al pasarlo a produccion entonces neceito valdiar al momento de abrir un seguimeinto , primero debe validar que no tenga seguimiento diario en produccion si lo tiene que exigir eliminar el lote seguimiento produccion que se tiene para volver a reabrir el lote produccion y deja bien especificado lo que se realiza , si no tiene seguimeint olo dejara abirir , pero si ya se creo el lote produccion entonces lo elimina y espera al moemnto de cerrar el lote levante otra ves para crearlo actualizado , > Seguimiento Diario de Levante»
Solución (1 commit): feat(postura): reapertura de levante validada + cierre/reapertura de lote de produccion
Bugs encontrados: 0.
Evidencia: 33 archivos tocados · 1,1 h de sesión real · commits 5f2a175 · sesión 9e83e65a
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0008-T9' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/cierre_levante_reapertura_validada_y_cierre_produccion_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 16.00, descripcion = 'Plan: fase_de_desarrollo/pwa_offline_first_plan.md

── Bitácora de la sesión (2026-07-27 → 2026-08-10) ──
Pedido: «realiza un analisi completo de la apciacion ara que sea 100 pwa fuera de linea de todos sus modulos al final se sincronize con la aplciacion en nueve cuendo tenga red la idea es que ya podemos trabjar fuera de linea con lo que tenemos contrido y si sigemos contruyendo seria que todo sea para las dos funcioens por ahroa que se establiza las funciones y deja alineado no contrior la app movil si no una pwa qeu se actualzie siempre que tnga nuevas cosas validmeos el proceso como seria y me muestraslo que encunteres , tener precente que lo fuera de lineas seria la informacion de los lotes seguimie»
Solución (2 commits): docs(pwa): analisis completo y plan de PWA offline-first con sincronizacion diferida; feat(pwa): alistamiento para campo — persistencia de cuota y D6 (nada de snapshot multiempresa)
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 30 archivos tocados · 1,3 h de sesión real · commits eb76034, b8821cb, 4616dfa · sesión b7039178, 20dc0bca
Estimación: 16 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0019-T20' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/pwa_offline_first_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 12.00, descripcion = 'Plan: fase_de_desarrollo/hora_encasetamiento_primer_registro_plan.md

── Bitácora de la sesión (2026-07-27 → 2026-07-30) ──
Pedido: «en la migraciion masiva que se tiene para pollo engorde del seguimiento tenemos algo logico un excel que me das me mustras el alimento de hembras y machos en e consumo pero el consumo es mixto , pero si tengo el de qq mixto pero no tengo si le agrego el de kg de alimeto como seria te paso un ejemplo que me distes y valida si quito las dos filas de consumo macho y hembras y pongo conumo mixto kg carga el conumo para ese registro del dia»
Solución (3 commits): feat(engorde): carga masiva MIXTA para Panama y descuento real de aves por mortalidad; feat(engorde): la hora de encasetamiento define el primer dia con registro; feat(engorde): regla de la hora de llegada por empresa + numeracion correcta del dia
Bugs encontrados: 3 — cada uno queda como subtarea BUG con su causa.
Evidencia: 54 archivos tocados · 4,3 h de sesión real · commits 04e4118, f5765c7, 56edf3a, 7639b79, 528b283, 769a48c · sesión 74faf114, 81854151
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0012-T22' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/hora_encasetamiento_primer_registro_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 12.00, descripcion = 'Plan: fase_de_desarrollo/seguimiento_engorde_mixto_panama_plan.md

── Bitácora de la sesión (2026-07-27) ──
Pedido: «en la migraciion masiva que se tiene para pollo engorde del seguimiento tenemos algo logico un excel que me das me mustras el alimento de hembras y machos en e consumo pero el consumo es mixto , pero si tengo el de qq mixto pero no tengo si le agrego el de kg de alimeto como seria te paso un ejemplo que me distes y valida si quito las dos filas de consumo macho y hembras y pongo conumo mixto kg carga el conumo para ese registro del dia»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 74faf114
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0012-T24' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/seguimiento_engorde_mixto_panama_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 4.00, descripcion = 'Plan: fase_de_desarrollo/fix_deploy_frontend_dockerignore_plan.md

── Bitácora de la sesión (2026-07-27) ──
Pedido: «realize ahroa un despelgue en con ci/cd y me salio enrror al despelgar el front agrego el log del despleigue y solucionalo para despelgar otra ves»
Solución (2 commits): docs(tracker): deploy del frontend verificado en prod tras el fix de .dockerignore; docs(tracker): registro del despliegue 30299439870 verificado en produccion
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 4 archivos tocados · 1,1 h de sesión real · commits b0e38d3, 7c08df9, c30272c · sesión 50822f43
Estimación: 4 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0019-T21' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/fix_deploy_frontend_dockerignore_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 2.00, descripcion = 'Plan: fase_de_desarrollo/csp_recaptcha_login_plan.md

── Bitácora de la sesión (2026-07-27) ──
Pedido: «tengo este error en produccion con el despelgue que no sale la utengticacion de google y es como si fuera dev en produccion»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 5 archivos tocados · 0,1 h de sesión real · commits 2f46837 · sesión f0ee4da2
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0004-T6' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/csp_recaptcha_login_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 8.00, descripcion = 'Plan: fase_de_desarrollo/dia_negocio_engorde_pesaje_plan.md

── Bitácora de la sesión (2026-07-27 → 2026-07-30) ──
Pedido: «en una anterior sesion trabaje algo con una regla de llegada o la hora de ahabrir un lote enotnces toma el seguinte registro como primera edad o despues de la 1 de la tarde toma el seguindo dia si llego el 08 despeus de las 1 pm la edad 1 seria para el 09 no 2 pero si llega antes el 08 es la edad 1 y el 09 seria 2 , en las reproductora se aplico pero en el lote pollo engorde de seguimeint ono esta implemntada la logica ahroa relaize una carga de datos y veo que cuando realizo cruce de las reproductoras no cuadro con edad 1 sino que comenseo en edad 0 enotnces hay se descuadra lo de pedir el p»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 81854151
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0012-T23' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/dia_negocio_engorde_pesaje_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 20.00, descripcion = 'Plan: fase_de_desarrollo/carga_masiva_alimento_engorde_plan.md

── Bitácora de la sesión (2026-07-28) ──
Pedido: «te voy a pasar dos archivos uno para que valides la poblacion del seguimeinto diario y entiendes la logica que se esta implementando en el archivo entonces validarlo que funcione y leugo realizas la prueba de cargarlo a seguimienit opollo engorde y identificar errores y corregisrlos , y te pasare lo que es el historico de alimento que se consumio el glpon y lo qeu debe quedar al fina en inventario para el alimento la idea es incontrar si en el mis»
Solución (6 commits): feat(engorde): el alimento entra en el mismo archivo de carga masiva y el inventario cuadra; feat(engorde): movimiento Consumo en la hoja Alimento y reparacion del galpon 6; feat(engorde): un solo archivo para todo el lote, una hoja por modulo; chore(engorde): recarga del galpon 6 completa desde un unico archivo de 3 hojas; docs(engorde): archivo unico de carga para el lote 13-1 con guia y ejemplos; feat(engorde): una fecha ya cargada se reemplaza con lo que trae el archivo
Bugs encontrados: 3 — cada uno queda como subtarea BUG con su causa.
Evidencia: 26 archivos tocados · 5,7 h de sesión real · commits 85da238, 9d2b6c3, 3145f01, 6e7987a, 0723fde, 1a6af9b, eb8c38f, 36a8bab, 54ce0e1 · sesión 2bb86ee7
Estimación: 20 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0006-T12' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/carga_masiva_alimento_engorde_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 24.00, descripcion = 'Plan: fase_de_desarrollo/informe_ra_pesadas_parametros_plan.md

── Bitácora de la sesión (2026-07-28 → 2026-07-29) ──
Pedido: «valida para crear estos reportes en sanmarino si va esto en varios reprotes o uno solo que tenga varios tap de las hojas la gua gtenetica que aparece aqui es vieja ya la aplciacion esta con la neuva valia par implemtnar estos reprotes y que esten alineados y cumplan todo»
Solución (11 commits): docs(postura): validacion del Informe RA Pesadas - plan y tracker; docs(postura): decisiones D1-D5 del Informe RA Pesadas; feat(postura): capa SQL del Resumen Semanal del Informe RA Pesadas; feat(postura): endpoint del Resumen Semanal del Informe RA Pesadas; feat(postura): front del Resumen Semanal del Informe RA Pesadas; docs(postura): tracker de la carga masiva con inventario; feat(postura): hojas ALIMLev y CLAS Huevo del Informe RA Pesadas; feat(postura): cierre del Informe RA Pesadas - export y menu
Bugs encontrados: 3 — cada uno queda como subtarea BUG con su causa.
Evidencia: 29 archivos tocados · 4,8 h de sesión real · commits 4ce11be, 2eeac5a, dc7834a, 2e6484f, 1b236bb, 3760b15, 51628ac, a1e5b96, 145348b, add95cd · sesión 2dbe27a8
Estimación: 24 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0015-T8' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/informe_ra_pesadas_parametros_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 16.00, descripcion = 'Plan: fase_de_desarrollo/migracion_masiva_postura_alimento_inventario_plan.md

── Bitácora de la sesión (2026-07-28) ──
Pedido: «en el modulo de carga masiva manual tenemos el de postura levante y produccion , por ahroa produccion es para sanmarino la carga masiva ya que en produccion santa reyes es diferente los tipos de huevos y alimentos ,e ntonces la logica es diferente pro empresa , enotnces vamos validar que la migracion manual funcione al momento de crear lovantes y produccion , con los campos que pide tenga claros el proceso de carga y la parte de ingreso de alimento tmaibne ya que tiene el modulo de ingreso de aliemnto para el galpon y con el valdiamos que tengamos inventario para consumir y llevemos un inven»
Solución (1 commit): feat(postura): la carga masiva de levante y produccion mueve inventario de alimento
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 33 archivos tocados · 1,9 h de sesión real · commits 7846200, f359290 · sesión ec7d32dc
Estimación: 16 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0006-T13' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/migracion_masiva_postura_alimento_inventario_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 16.00, descripcion = 'Plan: fase_de_desarrollo/cuadre_engorde_panama_aves_alimento_plan.md

── Bitácora de la sesión (2026-07-29 → 2026-07-30) ──
Pedido: «tengo un error en produccion ya descargue la base de datos de produccion en panama en pollo engorde ya se cargo todos los lotes con sus seguimiento diario de pollo engorde y seguimeinto de reproductora , ahroa tenemos que hacer cuadre de cada lo te pollo engorde , primero que la primera face de sus reproductora cuadre en el seguimeinto pollo enogrde de sus 7 dias que tengamos las aves que nos muestra en mixto y estamos descontando cuadre correctamente y alineamos tambine el aliemtno ya en el modulo de gestion de inventario ya esta lo que debe tener en alimento y en el seguimeint odiario pollo»
Solución (4 commits): docs(tracker): validacion de las migraciones de cuadre sobre el dump de produccion actual; docs(tracker): validacion cruzada del cuadre con el Reporte Diario de Costos Engorde; docs(tracker): registro del despliegue a produccion y su verificacion post-deploy; docs(engorde): requerimiento del cuadre de Ecuador para otra sesion
Bugs encontrados: 5 — cada uno queda como subtarea BUG con su causa.
Evidencia: 24 archivos tocados · 4,7 h de sesión real · commits 05ded34, 2af742d, 088a97c, 7f3a28c, 21e53ab, 2cc4855, 2f58e22, a050ec7, 9a753ea · sesión bd2fc0e8
Estimación: 16 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0012-T26' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/cuadre_engorde_panama_aves_alimento_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 3.00, descripcion = 'Plan: fase_de_desarrollo/cuadre_engorde_ecuador_requerimiento.md

── Bitácora de la sesión (2026-07-29 → 2026-07-30) ──
Pedido: «tengo un error en produccion ya descargue la base de datos de produccion en panama en pollo engorde ya se cargo todos los lotes con sus seguimiento diario de pollo engorde y seguimeinto de reproductora , ahroa tenemos que hacer cuadre de cada lo te pollo engorde , primero que la primera face de sus reproductora cuadre en el seguimeinto pollo enogrde de sus 7 dias que tengamos las aves que nos muestra en mixto y estamos descontando cuadre correctamente y alineamos tambine el aliemtno ya en el modulo de gestion de inventario ya esta lo que debe tener en alimento y en el seguimeint odiario pollo»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión bd2fc0e8, 91a7cb88
Estimación: 3 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0012-T27' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/cuadre_engorde_ecuador_requerimiento.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 16.00, descripcion = 'Plan: fase_de_desarrollo/congelar_liquidacion_lote_engorde_plan.md

── Bitácora de la sesión (2026-07-30 → 2026-07-31) ──
Pedido: «tenog un reporte de ecuador sobre una granja con un erro primero tengo que encontrar el error o el descuedre que se tiene antes de tocar ya descargue la base de datos de poduccion en local para que se balide : Buenos día estimado Moises, solicito su ayuda con el siguiente hallazgo tenemos una diferencia en el alimento desde el primer Dia esto se puede ver recién hoy que no se está considerando el saldo correcto nos ingresó 12000- 480 de consumo deberíamos tener 11520 pero el aplicativo nos está mostrando 3560 pero solo es en lo visual porque en el stock si tenemos lo correcto»
Solución (8 commits): docs(engorde): diagnostico del saldo de alimento de Ecuador - la grilla recalcula una apertura fantasma; docs(engorde): validacion de cierre lote/ciclo/galpon en Ecuador - 25 de 35 galpones OK; perf(engorde): indice por granja+fecha en el historico unificado; feat(engorde): prevencion de descuadres de alimento - los 5 puntos; docs(engorde): instructivo de operacion para Costos de Ecuador y Panama; docs(engorde): el instructivo identifica los galpones por nombre, nucleo e id; docs(engorde): el instructivo abre con el estado real por corrida, antes y despues; feat(engorde): liquidacion congelada - un lote liquidado ya no cambia solo
Bugs encontrados: 4 — cada uno queda como subtarea BUG con su causa.
Evidencia: 65 archivos tocados · 7,3 h de sesión real · commits 7b26052, 4923e2b, f718a3e, c346f35, 4d3e61f, ae1df1a, 9ad5492, e68a9b6, e2a8a3d, a396d1f · sesión 91a7cb88, 70a7e970
Estimación: 16 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0008-T10' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/congelar_liquidacion_lote_engorde_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 6.00, descripcion = 'Plan: fase_de_desarrollo/cuadre_engorde_ecuador_diagnostico_saldo_alimento.md

── Bitácora de la sesión (2026-07-30) ──
Pedido: «tenog un reporte de ecuador sobre una granja con un erro primero tengo que encontrar el error o el descuedre que se tiene antes de tocar ya descargue la base de datos de poduccion en local para que se balide : Buenos día estimado Moises, solicito su ayuda con el siguiente hallazgo tenemos una diferencia en el alimento desde el primer Dia esto se puede ver recién hoy que no se está considerando el saldo correcto nos ingresó 12000- 480 de consumo deberíamos tener 11520 pero el aplicativo nos está mostrando 3560 pero solo es en lo visual porque en el stock si tenemos lo correcto»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 91a7cb88
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0012-T25' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/cuadre_engorde_ecuador_diagnostico_saldo_alimento.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 8.00, descripcion = 'Plan: fase_de_desarrollo/fix_apertura_alimento_ciclo_anterior_plan.md

── Bitácora de la sesión (2026-07-30) ──
Pedido: «tenog un reporte de ecuador sobre una granja con un erro primero tengo que encontrar el error o el descuedre que se tiene antes de tocar ya descargue la base de datos de poduccion en local para que se balide : Buenos día estimado Moises, solicito su ayuda con el siguiente hallazgo tenemos una diferencia en el alimento desde el primer Dia esto se puede ver recién hoy que no se está considerando el saldo correcto nos ingresó 12000- 480 de consumo deberíamos tener 11520 pero el aplicativo nos está mostrando 3560 pero solo es en lo visual porque en el stock si tenemos lo correcto»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 91a7cb88
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0012-T28' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/fix_apertura_alimento_ciclo_anterior_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 10.00, descripcion = 'Plan: fase_de_desarrollo/prevencion_descuadres_alimento_engorde_plan.md

── Bitácora de la sesión (2026-07-30) ──
Pedido: «tenog un reporte de ecuador sobre una granja con un erro primero tengo que encontrar el error o el descuedre que se tiene antes de tocar ya descargue la base de datos de poduccion en local para que se balide : Buenos día estimado Moises, solicito su ayuda con el siguiente hallazgo tenemos una diferencia en el alimento desde el primer Dia esto se puede ver recién hoy que no se está considerando el saldo correcto nos ingresó 12000- 480 de consumo deberíamos tener 11520 pero el aplicativo nos está mostrando 3560 pero solo es en lo visual porque en el stock si tenemos lo correcto»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 91a7cb88
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0012-T29' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/prevencion_descuadres_alimento_engorde_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 3.00, descripcion = 'Plan: fase_de_desarrollo/INSTRUCTIVO_OPERACION_saldos_alimento_engorde.md

── Bitácora de la sesión (2026-07-30 → 2026-07-31) ──
Pedido: «tenog un reporte de ecuador sobre una granja con un erro primero tengo que encontrar el error o el descuedre que se tiene antes de tocar ya descargue la base de datos de poduccion en local para que se balide : Buenos día estimado Moises, solicito su ayuda con el siguiente hallazgo tenemos una diferencia en el alimento desde el primer Dia esto se puede ver recién hoy que no se está considerando el saldo correcto nos ingresó 12000- 480 de consumo deberíamos tener 11520 pero el aplicativo nos está mostrando 3560 pero solo es en lo visual porque en el stock si tenemos lo correcto»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 91a7cb88, 70a7e970
Estimación: 3 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0012-T30' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/INSTRUCTIVO_OPERACION_saldos_alimento_engorde.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 16.00, descripcion = 'Plan: fase_de_desarrollo/carga_masiva_levante_movimientos_aves_plan.md

── Bitácora de la sesión (2026-07-31 → 2026-08-01) ──
Pedido: «Vamos a ir al módulo carga masiva manual, migración manual. Entonces, en ese migración manual tenemos una fase que se llama unas fases que son para cargar masivamente galpones, granjas. Eso lo vamos a deshabilitar para que no aparezca visualmente. En el en el que vamos a trabajar ahorita es en el de carga masiva de seguimiento diario levante. Entonces, ¿este qué va a hacer? Pues va a tener toda la lógica de de lo que cuando yo hago un seguimiento diario, el registro de seguimiento diario, pero en este voy a tener que colocar ingresos. También va a haber una hoja, cuando yo descargue la plantil»
Solución (4 commits): feat(migracion): hoja Movimientos Aves en carga masiva de levante + tab de huevos fijo; feat(migracion): venta de aves en la hoja Movimientos Aves + fixes cazados por el E2E de ciclo completo; docs(tracker): cierre del lote 130 validado - LPP creado con 9495/929 aves, 130 huevos arrastrados y elegible para carga masiva…; feat(migracion): carga masiva de produccion completa - agua y pesaje, movimientos de huevos a planta/venta y aves en ambas fases
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 34 archivos tocados · 2,6 h de sesión real · commits 3453b09, fd6e51f, 12e0ebe, 21a5c81, b64898f · sesión 5b68e3ea
Estimación: 16 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0006-T14' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/carga_masiva_levante_movimientos_aves_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 20.00, descripcion = 'Plan: fase_de_desarrollo/seguimiento_produccion_fn_canonica_plan.md

── Bitácora de la sesión (2026-08-01) ──
Pedido: «Vamos a hacer una mejora ESTRUCTURAL del módulo de Seguimiento Diario de PRODUCCIÓN (postura): pasar la lectura de la tabla a una FUNCIÓN SQL canónica (estilo engorde), mover a SQL las lecturas/agregaciones pesadas que hoy viven en los services, blindar invariantes con triggers donde corresponda, y limpiar la calidad del código (menos subconsultas, menos N+1, partials y cálculo puro con tests). Trabajá con plan en fase_de_desarrollo/ y tracker (bloque NUEVO AL FINAL de tracker_estado.md — hay sesiones paralelas, no pises nada). == ESTADO ACTUAL (verificado 01-ago-2026, no re-descubras esto) =»
Solución (5 commits): feat(produccion): fn_seguimiento_diario_produccion canonica - grilla, header y fns semanales sobre una sola formula; espejo…; docs(tracker): cierre del bloque fn canonica de seguimiento produccion - fases 1-4 validadas, smoke verde y particion en partials…; feat(produccion): fn v2 filas TSD visibles en grilla LPP; writer legacy anclado a mediodia con rango de dia; Reporte Contable…; merge(reporte-contable): reconciliacion con la sesion del chip - calculo puro + tests + alcance padre y sublotes; feat(espejo-huevos): DROP historico_semanal + indice GIN (columna muerta, OK explicito) - entidad y configuration sin la…
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 34 archivos tocados · 2,8 h de sesión real · commits 4034b8f, 5aff254, 5a3b220, 6de9ea9, c4741a0, f6ac8c7 · sesión 4d108bcf
Estimación: 20 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0014-T11' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/seguimiento_produccion_fn_canonica_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 5.00, descripcion = 'Plan: fase_de_desarrollo/gastos_inventario_reporte_estado_existencias_plan.md

── Bitácora de la sesión (2026-08-05) ──
Pedido: «en el modulo de gastos de inventario al momento de descargar un reprote si esta descargando los eliminados y no se si esta regresando al invientario cuando se elimina lo que se regsitro ya que al momento de descargar el reprote lo trae y anterior mente se realizo un la implemtnacion de este caso pero no se implemntado la solucion o no se termino , entonces tengo este error entonces dejo la novedad por parte del usaurio y la imagen y el rprote descargado qeu pasa con ecuador ya que este es un modulo trasvesar entre enpresas es un erro que puede pasar en todas, ya que en el servicio que me mues»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 24 archivos tocados · 0,8 h de sesión real · commits 116e052 · sesión 02ddb5a6
Estimación: 5 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0007-T18' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/gastos_inventario_reporte_estado_existencias_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 8.00, descripcion = 'Plan: fase_de_desarrollo/fix_disponibilidad_aves_venta_engorde_plan.md

── Bitácora de la sesión (2026-08-05) ──
Pedido: «encontre una novedad en seguimiento diario , y ventas en el seguimiento diario en la parte qeu me da las aves disponibles esta sumando las aves del otro lote que esta cerrado que esta en 7 y mas 32 da 40 aves disponibles eso dice en el seguimeinto diario pero en la venta dice que tiene 32 y las 32 deben ser las correctas ya que no se puede sumar aves entre lote de pollo engorde hay un error de la logica que se aplico y esta mostrando datos que no son correcto o no estan disponibles de ambos ya que ventas depende de lo disponible que deja seguimeinto dairio y seguimeinto diario va dejando de»
Solución (1 commit): merge(pollo-engorde): reconciliacion con 75f7980 - correccion de datos + baseline de las bajas
Bugs encontrados: 3 — cada uno queda como subtarea BUG con su causa.
Evidencia: 16 archivos tocados · 1,5 h de sesión real · commits 933b3b1, 3998aa2, 75f7980, b9cab63 · sesión 600a26a2
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0010-T12' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/fix_disponibilidad_aves_venta_engorde_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 10.00, descripcion = 'Plan: fase_de_desarrollo/envio_correo_graph_api_plan.md

── Bitácora de la sesión (2026-08-05) ──
Pedido: «TENGO UN ERROR AL MOMENTO DE ENVIAR CORREO ELETRONICO EN PRODUCCION YA UQE PARA ESTE 2026 EL PROTOCOLO DE ENVIO CAMBIO ENTONCES NECESITO CAMIBNAR ESO EN LOS ENCARGADOS DE ENVIO DE CORREO CONFIGURADOS EN EL PORYECTO»
Solución (3 commits): merge(main): integrar la correccion de la referencia Inicio con la migracion de correo a Graph; refactor(correo): dejar un solo transporte SMTP y revertir el emisor por Graph; docs(gastos-inventario): validacion sobre la BD restaurada de prod + correccion de atribucion
Bugs encontrados: 4 — cada uno queda como subtarea BUG con su causa.
Evidencia: 23 archivos tocados · 1,6 h de sesión real · commits cadd84f, abe3643, 31e3654, d341223, c7b6834, 587d6cc, 2cab258 · sesión 86121869
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0018-T2' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/envio_correo_graph_api_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 6.00, descripcion = 'Plan: fase_de_desarrollo/exportar_stock_inventario_excel_plan.md

── Bitácora de la sesión (2026-08-05) ──
Pedido: «tengo un requerimeinto nuevo , en el modulo de gestion de inventario necesito que descargeu el stock disponible de todas las granjas con su galpon correspondiente si e alimento si es otro item sobre la granja entonces hay me debe traer todas las las granjas al descargar el excel ya que me piden descargar en excel lo que esta en la palicaicon > BUENAS TARDES ESTIMADO MOISES, SOLICITO SU AYUDA EN PODER DESCARGAR EN EXCEL EL STOCK QUE TENEMOS EN CADA BODEGA PARA PODER REALIZAR UN COMPARATIVO,»
Solución (2 commits): feat(gestion-inventario): descargar en Excel el stock de todas las granjas; feat(gestion-inventario): el Excel de stock sale en dos hojas por concepto
Bugs encontrados: 0.
Evidencia: 16 archivos tocados · 1,0 h de sesión real · commits 6b1f635, 19adf57 · sesión bac7ce9f
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0007-T17' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/exportar_stock_inventario_excel_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 12.00, descripcion = 'Plan: fase_de_desarrollo/traslado_aves_destino_cross_granja_y_fecha_registro_plan.md

── Bitácora de la sesión (2026-08-06) ──
Pedido: «al momento de realizar traslado de aves tanto de pollo engorde y postura en levante o produccion debe tenerla forma de realizar traslados a otraws granjas otros galpones pero tamibne tener un campo de fecha de traslado y otro que es la fecha de creacion del registro ya que se tiene que son dos tipos de datos diferentes entonces el usuario en la web modifica la fecha de traslado de aves o de lote entonces pro eso dehamso el created_at como la fecha de creacion en el sistema»
Solución (2 commits): feat(traslado-aves): destino en otra granja/galpon para engorde y fecha de registro visible; feat(cohortes): un lote que recibe aves guarda de donde vienen y con que edad
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 52 archivos tocados · 2,6 h de sesión real · commits 00ff4b5, 881812d, d50cd9c · sesión a7c907b3
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0010-T13' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/traslado_aves_destino_cross_granja_y_fecha_registro_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 10.00, descripcion = 'Plan: fase_de_desarrollo/cohortes_edades_lote_receptor_plan.md

── Bitácora de la sesión (2026-08-06) ──
Pedido: «al momento de realizar traslado de aves tanto de pollo engorde y postura en levante o produccion debe tenerla forma de realizar traslados a otraws granjas otros galpones pero tamibne tener un campo de fecha de traslado y otro que es la fecha de creacion del registro ya que se tiene que son dos tipos de datos diferentes entonces el usuario en la web modifica la fecha de traslado de aves o de lote entonces pro eso dehamso el created_at como la fecha de creacion en el sistema»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión a7c907b3
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0010-T14' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/cohortes_edades_lote_receptor_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 5.00, descripcion = 'Plan: fase_de_desarrollo/tipo_alimento_varchar_desborde_plan.md

── Bitácora de la sesión (2026-08-06) ──
Pedido: «me reprotan el siguitne error cuando realizan un seguimeinto diario a un lote en particular en sanmarino colombia , no lo entiendo mas o menos y valida tu realziando todo el flujo para poder darme detalle del error: Luego de ingresar datos en el lote A374A e intentar guardar sale aviso de falla en guardado y al volver a entrar no aparece la información.»
Solución (2 commits): docs(carga-masiva): E2E del lote S-369 en local — carga validada y 3 defectos de reporte; docs(carga-masiva): ciclo completo del S-369 en local y una venta que el reporte de produccion no ve
Bugs encontrados: 2 — cada uno queda como subtarea BUG con su causa.
Evidencia: 24 archivos tocados · 1,9 h de sesión real · commits b947cf2, ccb372b, 2a35d63, 92e1cb5 · sesión 7132c5db
Estimación: 5 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0012-T33' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/tipo_alimento_varchar_desborde_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 16.00, descripcion = 'Plan: fase_de_desarrollo/carga_masiva_s369ab_postura_plan.md

── Bitácora de la sesión (2026-08-06 → 2026-08-07) ──
Pedido: «estoy realizando el carga masiva de postura de levante y produccion con alimento tamibne , la idea es cargar ek seguimeinto dairio entonces me pasaron estos archivos donde tinen toda la informacion del lote seguimeinto de cada una de las fases entonces en la carpeta estan los archivos entonces tenemos que estudiar estos archivos para sacar el archivo de carga masivo constouirlo con la informacion que esta aqui S369 de la regional Centro es el lote que no esta creado pero seria para crearlo en una granja de granja pruebas hasta galpon de prubas entonces necesito primero con estos archivos contr»
Solución (5 commits): docs(carga-masiva): archivos de migracion del lote S-369AB (levante + produccion + alimento); docs(carga-masiva): validacion exhaustiva del S-369 y el origen de los 5 huevos; feat(postura): unifica el tab Indicadores de levante y produccion y quita el Reporte semana; docs(postura): handoff de los hallazgos de la sesion para continuar en otra ventana; docs(postura): manual de carga masiva para implementacion
Bugs encontrados: 7 — cada uno queda como subtarea BUG con su causa.
Evidencia: 47 archivos tocados · 9,2 h de sesión real · commits c110718, 1398335, 219f05f, 148f061, 4f7b83e, 2ac57a8, 2d26fae, 22f3be2, b34e629, 91533a0 · sesión 4186dd9a, cc4398d8
Estimación: 16 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0006-T15' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/carga_masiva_s369ab_postura_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 10.00, descripcion = 'Plan: fase_de_desarrollo/19_indicadores_levante_produccion_ux_plan.md

── Bitácora de la sesión (2026-08-06 → 2026-08-07) ──
Pedido: «estoy realizando el carga masiva de postura de levante y produccion con alimento tamibne , la idea es cargar ek seguimeinto dairio entonces me pasaron estos archivos donde tinen toda la informacion del lote seguimeinto de cada una de las fases entonces en la carpeta estan los archivos entonces tenemos que estudiar estos archivos para sacar el archivo de carga masivo constouirlo con la informacion que esta aqui S369 de la regional Centro es el lote que no esta creado pero seria para crearlo en una granja de granja pruebas hasta galpon de prubas entonces necesito primero con estos archivos contr»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 4186dd9a
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0013-T7' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/19_indicadores_levante_produccion_ux_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 2.00, descripcion = 'Plan: fase_de_desarrollo/20_handoff_postura_hallazgos_sesion.md

── Bitácora de la sesión (2026-08-06 → 2026-08-07) ──
Pedido: «estoy realizando el carga masiva de postura de levante y produccion con alimento tamibne , la idea es cargar ek seguimeinto dairio entonces me pasaron estos archivos donde tinen toda la informacion del lote seguimeinto de cada una de las fases entonces en la carpeta estan los archivos entonces tenemos que estudiar estos archivos para sacar el archivo de carga masivo constouirlo con la informacion que esta aqui S369 de la regional Centro es el lote que no esta creado pero seria para crearlo en una granja de granja pruebas hasta galpon de prubas entonces necesito primero con estos archivos contr»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: sesión 4186dd9a
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0014-T12' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/20_handoff_postura_hallazgos_sesion.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 24.00, descripcion = 'Plan: fase_de_desarrollo/18_tickets_jira_casos_tareas_tablero_plan.md

── Bitácora de la sesión (2026-08-06 → 2026-08-07) ──
Pedido: «en el modulo ticket en el modulo de mi solicitudes puedo crear solicitudes para mi si soy el mismo usuario , tengo que esta loueado tengo que colocar de que usurio del sistema biene la soliccutu ya que puedo resolver casos que no estan montado en la aplciacion por un usuario entonces cosas que voy incontrando en si , y quiero tener un modulo tipo jira que tome ticket como casos y pueda creaer tareas historicas etc , como en sira y moverlos como en gira y colocar tiempos de solucion y todo lo necsario que conlleven y con fases de desarrollo , analisis documentacion , en revicion , solucionado»
Solución (3 commits): feat(tickets): los tickets pasan a ser casos tipo Jira, con tareas, tablero y tiempos; feat(tickets): panel de control del administrador y reporte detallado a Excel; feat(tickets): una sola barra de filtros para tablero, roadmap y panel
Bugs encontrados: 3 — cada uno queda como subtarea BUG con su causa.
Evidencia: 74 archivos tocados · 4,1 h de sesión real · commits 4bf63d1, 152be88, d536926, 588dc94, 0ce0485, 4f61046 · sesión f4ac9295
Estimación: 24 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0001-T12' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/18_tickets_jira_casos_tareas_tablero_plan.md';
    UPDATE public.ticket_tareas
       SET horas_estimadas = 6.00, descripcion = 'Plan: fase_de_desarrollo/reconciliacion_espejo_fn_indicadores_produccion_plan.md

── Bitácora de la sesión (2026-08-07) ──
Pedido: «# Handoff — hallazgos de la sesión de postura (06-07 ago 2026) Todo lo de acá ya está **commiteado en `main`** y **aplicado en la BD local**. En producción se aplica solo en el próximo deploy (EF corre las migraciones al arrancar). Origen: cargar el lote histórico **S-369** (levante + producción + alimento) desde tres Excel y hacer que los reportes de la app coincidan con ellos. Al hacerlo salieron a la luz una docena de defectos que **no eran de este lote** sino del código, y que estaban vivos en producción para todas las empresas. --- ## 1 · Commits de esta sesión (postura) | Commit | Q»
Solución (3 commits): chore(postura): detector de sobregiro de aves para decidir el bloqueo del seguimiento; feat(postura): la fn emite el %Seleccion de machos (la tabla mostraba %Sel H sin su par); Merge branch ''main'' into claude/mystifying-haslett-917cc0
Bugs encontrados: 2 — cada uno queda como subtarea BUG con su causa.
Evidencia: 4 archivos tocados · 0,7 h de sesión real · commits f8f887a, 9f56da1, 2eb2382, d9d45bb, 6be9031 · sesión 2ec6763f
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = 'HIS-2026-0014-T13' AND horas_estimadas IS NULL AND descripcion = 'Plan: fase_de_desarrollo/reconciliacion_espejo_fn_indicadores_produccion_plan.md';
    -- ═══ 2) Tareas nuevas: sesiones de trabajo que no tenían tarea sembrada ═══
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260709-9cca', 'TAREA', 'LISTO', 'MEDIA',
           'Seguimiento Diario Levante: dos tipos de alimento (hembras y machos) por registro', '── Bitácora de la sesión (2026-07-09 → 2026-07-10) ──
Pedido: «en el modulo de seguimiento diario levante tengo que agregar al momento de realziar un nuevo registro , quiero tener dos tipso de alimento uno para hembras y el otro para machos asi seleciono el tipo de alimento que estoy alimentando para el mocho y para la hembra , y asi serian dos tipos de alimento o el mismo para macho o para ehmbras aqui dejo el pantallso y quiero que cuando selecione el aliemnt ome debe mostrar la cantidad de alimento que tiene , debemos tambian separa el alimento que si es el mismo separo lo que estoy colocando en el consumo asi si no hay alimento solo se el echo a he»
Solución (1 commit): refactor(inventario): migrar modal levante a ItemInventarioDto y eliminar alias TS deprecado (frontend 100% neutro)
Bugs encontrados: 0.
Evidencia: 31 archivos tocados · 4,2 h de sesión real · commits c5ef5f9 · sesión 9cca1c87
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-07-09', DATE '2026-07-10', TIMESTAMPTZ '2026-07-09 13:46:28+00', TIMESTAMPTZ '2026-07-10 02:14:15+00', 10.00, 'postura,levante,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-07-09 13:46:28+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0013'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260709-9cca');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260710-50cd', 'MEJORA', 'LISTO', 'MEDIA',
           'Un solo comando para levantar back y front en .NET 10 (make dev)', '── Bitácora de la sesión (2026-07-10) ──
Pedido: «como levanto el back y el front por que en tro en linea de comando y me da error si no el make lo editamos para acomodar con le .net 10 y el front que tenga un solo comand opara levantar dev»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 6 archivos tocados · 0,6 h de sesión real · commits a1f0af3 · sesión 50cd7cfb
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-07-10', DATE '2026-07-10', TIMESTAMPTZ '2026-07-10 05:39:32+00', TIMESTAMPTZ '2026-07-10 07:12:48+00', 2.00, 'plataforma,devops,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-07-10 05:39:32+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0019'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260710-50cd');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260710-ff01', 'MEJORA', 'LISTO', 'MEDIA',
           'Seguimiento pollo engorde: el tipo de ítem sale del formulario y el alimento queda definido', '── Bitácora de la sesión (2026-07-10) ──
Pedido: «en el modulo de seguimeinto diario pollo engorde necesito al moment ode realizar un registro nuevo por defecto el campo tipo de iten se quite y este el alimento definido sin mostrar solo mostraria alimento donde lecionan el alimento tamibne quiero saber que pasa cuando se agrega dos alimentos para machos yo debo selecionar eso por que hay momento que oueden mesclar el alimento viejo con el nuevo y eso da una cantidad de consumo se puede decir que de alimento A. se comio 50 kg y del B. 20 el macho entonces eso debo tenerlo mapiado en la tabla y en el seguimeinto diario que muestra individual l»
Solución (3 commits): refactor(inventario): naming neutro del catálogo (ItemInventario) compartido EC/PA/CO; procesos de mirgaciiones; docs(inventario-rename): ratificar decisiones 2a sesion (conservar simbolos EC/PA, dejar modal levante, diferir Fase C BD)
Bugs encontrados: 0.
Evidencia: 13 archivos tocados · 1,1 h de sesión real · commits c2dd7a2, 7f077ac, 07a94b7 · sesión ff01dc07
Estimación: 3 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-07-10', DATE '2026-07-10', TIMESTAMPTZ '2026-07-10 06:28:45+00', TIMESTAMPTZ '2026-07-10 07:33:23+00', 3.00, 'engorde,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-07-10 06:28:45+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0012'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260710-ff01');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260710-2cdf', 'DOCUMENTACION', 'LISTO', 'MEDIA',
           'Explicación del CI/CD actual: qué despliega, cómo y con qué credenciales', '── Bitácora de la sesión (2026-07-10) ──
Pedido: «el proyecto actual mente con ci/cd como realiza todo : Hola Moises como vas?, realmente para que nose me pierda tu contacto y no perder la pregunta que me surgio y es tu haces despliegue de infra en AWS a traves de github? o los pipes son solo para el despliegue de codigo?»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: 5 archivos tocados · 0,2 h de sesión real · sesión 2cdf3319
Estimación: 1 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-07-10', DATE '2026-07-10', TIMESTAMPTZ '2026-07-10 17:05:35+00', TIMESTAMPTZ '2026-07-10 17:47:51+00', 1.00, 'plataforma,devops,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-07-10 17:05:35+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0019'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260710-2cdf');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260714-b0af', 'BUG', 'LISTO', 'ALTA',
           'El select de alimentos de Seguimiento Producción listaba ítems sin stock', '── Bitácora de la sesión (2026-07-14) ──
Pedido: «en el modulo de seguimiento diario produccion al momento de abrir el modal de registrar un nuevo segumiento , en el select que me muestra los aliemtnos necesito que me lsite los alimentos que tiene inventario pro que actual mente me los muestra todos a si no tenga invetario este es el servicio que utiliza si algo creemos un nuevo api que identifique que es del modulo de seguimeinto diario produccion y aplciamos la logica que necestiamos ya que no sabemos donde mas necesitemos ese servicio o le agregamos un condicion que cuando enviaseguimiento_produccion , aplcia el flitro de solo los que tn»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 4 archivos tocados · 0,2 h de sesión real · commits 19d2f58 · sesión b0af1fb8
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-07-14', DATE '2026-07-14', TIMESTAMPTZ '2026-07-14 12:16:31+00', TIMESTAMPTZ '2026-07-14 12:26:23+00', 2.00, 'inventario,gastos,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-07-14 12:16:31+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0007'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260714-b0af');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260714-bab8', 'BUG', 'LISTO', 'ALTA',
           'El consumo de Seguimiento Producción no descontaba el inventario (ítems camino-2)', '── Bitácora de la sesión (2026-07-14) ──
Pedido: «realziae un seguimiento daiario porduccion al momento de realizar el descuento del consumo no se aplico el el descuento al inventario de alimento , ya estamos apuntando a un nueva tabala que implemntadmos de inventario entonces peude ser que el cambio no funcione , pero no esta apciando el consumo del inventario y tmaibne si en un consumo que coloco tipo embra coloco 100 y el iten tiene 120 , para el consumo de macho que esta abajo debe mostrar los 20 solamente , ya que si no se controla esto pueden hacer un consumo de 100 en los dos y quedaria en negativo de mas proque no se controla la exi»
Solución (1 commit): Merge pull request #31 from ItalcolColombia/claude/infallible-brahmagupta-90c317
Bugs encontrados: 2 — cada uno queda como subtarea BUG con su causa.
Evidencia: 6 archivos tocados · 0,7 h de sesión real · commits 13ce348, 99c8736, 92087b4 · sesión bab8ee83
Estimación: 5 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-07-14', DATE '2026-07-14', TIMESTAMPTZ '2026-07-14 12:33:18+00', TIMESTAMPTZ '2026-07-14 14:05:39+00', 5.00, 'inventario,gastos,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-07-14 12:33:18+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0007'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260714-bab8');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260714-21a8', 'BUG', 'LISTO', 'ALTA',
           'Vacunación: el cronograma no traía nada por permisos faltantes', '── Bitácora de la sesión (2026-07-14 → 2026-07-15) ──
Pedido: «en el modulo de vacunancion cuando le doy clic en cronograma no trae nada no esta funcional , enotnces validar los otros tres modulos que funciones»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 12 archivos tocados · 0,4 h de sesión real · commits 9c87ec6 · sesión 21a8d370
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-07-14', DATE '2026-07-15', TIMESTAMPTZ '2026-07-14 19:32:39+00', TIMESTAMPTZ '2026-07-15 13:19:04+00', 2.00, 'vacunacion,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-07-14 19:32:39+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0003'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260714-21a8');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260721-1a99', 'DOCUMENTACION', 'LISTO', 'MEDIA',
           'SQL de diagnóstico: por qué el admin de Panamá solo veía dos granjas', '── Bitácora de la sesión (2026-07-21) ──
Pedido: «dame un sql para sacar las ranjas de panama es que el usuario qeu tengo como admin.panmaa solome trae dos granjas enotnces quiero tirar en base de datos de peoduccion si en la migracion desde el modulo migracion panama paso algo o no se asignaron al usuario admin panama la migracion de infomrcion: Request URL Request Method GET Status Code 200 OK Remote Address 18.119.197.100:443 Referrer Policy strict-origin-when-cross-origin cache-control no-store, no-cache, must-revalidate, max-age=0 content-s»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: 6 archivos tocados · 0,3 h de sesión real · sesión 1a993293
Estimación: 1 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-07-21', DATE '2026-07-21', TIMESTAMPTZ '2026-07-21 12:55:00+00', TIMESTAMPTZ '2026-07-21 19:57:01+00', 1.00, 'usuarios,roles,granjas,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-07-21 12:55:00+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0005'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260721-1a99');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260725-ee29', 'MEJORA', 'LISTO', 'MEDIA',
           'Super grafo del proyecto: mejoras de contexto y reducción de tokens', '── Bitácora de la sesión (2026-07-25 → 2026-07-26) ──
Pedido: «como va el cereblo super grafo que esta concetado con claude , se tiene que mejora algo para hacer mas intelignrete , exerto y que este reduccioendo token y apreda»
Solución (1 commit): merge: fix fn_rekey_nucleo copia codigo/descripcion bodega al mover nucleo (migracion 20260725210000)
Bugs encontrados: 0.
Evidencia: 1 archivos tocados · 1,5 h de sesión real · commits 7bdf712 · sesión ee29da40
Estimación: 3 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-07-25', DATE '2026-07-26', TIMESTAMPTZ '2026-07-25 20:57:13+00', TIMESTAMPTZ '2026-07-26 00:49:38+00', 3.00, 'plataforma,devops,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-07-25 20:57:13+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0019'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260725-ee29');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260726-64d2', 'BUG', 'LISTO', 'ALTA',
           'El modal de alcance de granja quedaba colgado en «Cargando…» (Angular 22 = OnPush por defecto)', '── Bitácora de la sesión (2026-07-26) ──
Pedido: «tengo un error que visualizo es que cuando entro a usuario y voy a asignarle dentro de la granja que tiene un alcance a que solo vea galpones un usuario veo que lso servicios retornan todo lo que necesita , pero el modal queda cargado y nunca mustra nada > este es un error que ha venido pasando mucho en el front cuando se utiliza el desarrollo de algo nuevo tenemos que colocar la forma del arreglo en el cerebro y claude para que siemrpe lo tenga precente al desarrollar un modelo o modal nuevo»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 7 archivos tocados · 0,6 h de sesión real · commits 14a8bfa · sesión 64d22f2d
Estimación: 3 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-07-26', DATE '2026-07-26', TIMESTAMPTZ '2026-07-26 22:28:22+00', TIMESTAMPTZ '2026-07-26 23:05:33+00', 3.00, 'usuarios,roles,granjas,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-07-26 22:28:22+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0005'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260726-64d2');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260727-8b92', 'TAREA', 'LISTO', 'MEDIA',
           'PWA Fase 0: higiene de entrega y sesión que sobrevive a la falta de red', '── Bitácora de la sesión (2026-07-27) ──
Pedido: «vamos a desarrollar este plan completo # Plan — PWA offline-first con sincronización diferida **Fecha:** 2026-07-26 **Estado:** ANÁLISIS COMPLETO / DISEÑO PROPUESTO — pendiente de decisiones del usuario antes de implementar **Alcance pedido:** que los módulos operativos funcionen sin red y sincronicen al recuperar conexión; PWA autoactualizable; **no** app móvil nativa; que lo que se construya de ahora en más nazca sirviendo para los dos modos. **Módulos operativos nombrados por el usuario:** gestión de lotes · seguimiento levante · seguimiento producción · pollo engorde · reproductora pollo»
Solución (3 commits): chore(pwa): Fase 0.C - higiene de entrega para poder sostener un Service Worker; feat(pwa): Fase 0.B parcial - la sesion sobrevive a la falta de red (B2, B3, B7); docs(pwa): README de core/auth/funciones con la convencion y el porque de aislar las reglas de sesion
Bugs encontrados: 0.
Evidencia: 39 archivos tocados · 1,6 h de sesión real · commits 76a2903, f139dfd, 73b14d3 · sesión 8b92c475
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-07-27', DATE '2026-07-27', TIMESTAMPTZ '2026-07-27 04:25:53+00', TIMESTAMPTZ '2026-07-27 14:08:05+00', 8.00, 'plataforma,devops,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-07-27 04:25:53+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0019'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260727-8b92');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260807-276f', 'TAREA', 'LISTO', 'MEDIA',
           'ItalJira: la gestión del área de desarrollo sale de Tickets a un módulo propio', '── Bitácora de la sesión (2026-08-07) ──
Pedido: «ahroa en el modulo de ticket donde recibo el ticket y gestiono la apliicacion encesiot que esmo modulo este bien acomodado donde gestione lostiempo y tareas historias de casos etc , cuando se crea por un usuario es una tarea sin historia pero si es una historia un proceso que realize manual desde el area de desarrollo donde implemnteo el desarrollo directamente ya sea el area de requerimeinto o el administrador se asigne o me asigne trabajos , tamibne vamos a organizar que lso tiempo de entrega y finalizacion ay que puedo crea una historia que se llama modulo de ticket y dentro de ella tendr»
Solución (1 commit): feat(italjira): saca la gestion del area de desarrollo de Tickets a un modulo propio
Bugs encontrados: 0.
Evidencia: 61 archivos tocados · 2,0 h de sesión real · commits 5f5eb9a · sesión 276ffba3
Estimación: 16 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-07', DATE '2026-08-07', TIMESTAMPTZ '2026-08-07 07:09:20+00', TIMESTAMPTZ '2026-08-07 09:11:13+00', 16.00, 'italjira,tickets,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-07 07:09:20+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0020'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260807-276f');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260807-bd7a', 'MEJORA', 'LISTO', 'MEDIA',
           'make dev-back cierra la instancia anterior antes de compilar (bin bloqueado)', '── Bitácora de la sesión (2026-08-07) ──
Pedido: «al levantar el back me sale error > PS C:\Users\SAN MARINO\Desktop\App_SanMarino> make dev-back powershell -NoProfile -ExecutionPolicy Bypass -File dev-back.ps1 [dev-back] dotnet: 10.0.301 (esperado 10.x) [dev-back] ASPNETCORE_ENVIRONMENT = Development [dev-back] Backend -> (Swagger: Using launch settings from C:\Users\SAN MARINO\Desktop\App_SanMarino\backend\src\ZooSanMarino.API\Properties\launchSettings.json... Building... C:\Users\SAN MARINO\.dotnet\sdk\10.0.301\Microsoft.Common.CurrentVersion.targets(5096,5): warning MSB3026: Could no»
Solución (3 commits): chore(dev): dev-back baja el backend previo antes de compilar; Revert ""chore(dev): dev-back baja el backend previo antes de compilar""; chore(dev): make dev-back cierra la instancia anterior via dev-kill-back.cmd
Bugs encontrados: 0.
Evidencia: 3 archivos tocados · 0,5 h de sesión real · commits 9f31dec, 3875462, e44ea0d · sesión bd7a9927
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-07', DATE '2026-08-07', TIMESTAMPTZ '2026-08-07 09:15:11+00', TIMESTAMPTZ '2026-08-07 09:43:47+00', 2.00, 'plataforma,devops,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-07 09:15:11+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0019'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260807-bd7a');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260807-3610', 'TAREA', 'LISTO', 'MEDIA',
           'Conciliación del lote K345 (NIZA III): aplicación vs ERP en el traslape levante-producción', '── Bitácora de la sesión (2026-08-07) ──
Pedido: «tengo que realizar un analisis de un lote en particular que es el que esta en niza iii que cumplio su etapa de levante y produccion costos valido la aplciacion y comparar con el erp entonces ahroa necesito valdiar y encontrar donde esta la diferencia tan grandes o proceso humanos para contestarle enotnces ya esta actualzia la base de datos con lo que esta en produccion enotnces aqui pego lo que esta en el correo de lo que realizaron enotnces ahora tengo que contestar con por area de desarrollo de la plataforma > Buen dia A continuación relaciono la conciliación   LOTE K345 LVTE A»
Solución (3 commits): docs(conciliacion): analisis lote K345 NIZA III aplicacion vs ERP; feat(reportes): Seleccion y movimiento de huevo en el informe contable; carga masiva de levante a paridad; feat(postura): bloquea el doble conteo cuando un dia se registra en levante y en produccion
Bugs encontrados: 0.
Evidencia: 23 archivos tocados · 1,3 h de sesión real · commits 69853d3, d299a8a, 3347fbf · sesión 361053cc
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-07', DATE '2026-08-07', TIMESTAMPTZ '2026-08-07 09:23:43+00', TIMESTAMPTZ '2026-08-07 10:43:47+00', 6.00, 'reportes,excel,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-07 09:23:43+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0015'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260807-3610');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260807-8e56', 'TAREA', 'LISTO', 'MEDIA',
           'Reporte Diario Área de Costos para POSTURA (levante + producción) — Sanmarino', '── Bitácora de la sesión (2026-08-07) ──
Pedido: «neceisto crear un reorte area de costos para la empresa de sanmarino colombia , la idea es que los reportes son diarios sobre lote base y los filtros que nos muestra y ese es el dise;o del reporte con sus hojas o tap que nos mostrara entonces validamos informacion con el lote de pruebas que se cargaron masiva mente que son 369B con ese vamos a trabjar ya que lo cargamos desde archivos que son veridicos»
Solución (3 commits): @ feat(reportes): Reporte Diario Area de Costos para POSTURA (levante + produccion); chore(tracker): cierra el bloque del alcance de nombre de lote por galpon; docs(postura): manual de carga masiva en Word (17 pag.) + PDF
Bugs encontrados: 2 — cada uno queda como subtarea BUG con su causa.
Evidencia: 31 archivos tocados · 1,2 h de sesión real · commits 3469004, 9ddbbc8, 3ce5360, 92cd918, 8d5565c · sesión 8e56bd43
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-07', DATE '2026-08-07', TIMESTAMPTZ '2026-08-07 10:56:25+00', TIMESTAMPTZ '2026-08-07 12:10:36+00', 12.00, 'reportes,excel,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-07 10:56:25+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0015'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260807-8e56');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260807-9c89', 'BUG', 'LISTO', 'ALTA',
           'El nombre de lote se validaba único por granja cuando es único por GALPÓN', '── Bitácora de la sesión (2026-08-07) ──
Pedido: «tengo este error en ticket de un seguimeinto diario no le deja y la ubicacion es la siuintes > Falla en fecha registro levante semana 6 lote A374A galpón 4 > Descripción El aplicativo no permitió el registro de información de la fecha 22 de noviembre con sus datos. Quedó una fila inconclusa»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 11 archivos tocados · 0,6 h de sesión real · commits 226a5a4 · sesión 9c898bbe
Estimación: 4 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-07', DATE '2026-08-07', TIMESTAMPTZ '2026-08-07 11:32:15+00', TIMESTAMPTZ '2026-08-07 12:10:22+00', 4.00, 'usuarios,roles,granjas,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-07 11:32:15+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0005'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260807-9c89');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260807-893a', 'MEJORA', 'LISTO', 'MEDIA',
           'Gastos de inventario (Ecuador): rango de fechas al descargar el Excel', '── Bitácora de la sesión (2026-08-07) ──
Pedido: «el modulo de gastos de inventario neceito realziar esta cambio de mejora al moemnto de descargar el excel y ver el resultado en la tabla > ESTIMADO MOISE, SOLICITO SU AYUDA QUE AL MOMENTO DE DESCARGAR PUEDA ELEGIR DE QUE FECHA HASTA QUE FECHA NECESITO EL CONSUMO DE PRODUCTOS, PARA ASI NO TENER QUE BAJAR TODO LOS CONSUMOS REALIZADOS»
Solución (1 commit): ecuador agregar fechas rangos en gastos de invnetario
Bugs encontrados: 0.
Evidencia: 17 archivos tocados · 0,3 h de sesión real · commits 90f97ad · sesión 893addf3
Estimación: 3 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-07', DATE '2026-08-07', TIMESTAMPTZ '2026-08-07 12:16:11+00', TIMESTAMPTZ '2026-08-07 12:35:34+00', 3.00, 'inventario,gastos,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-07 12:16:11+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0007'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260807-893a');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260807-bd43', 'MEJORA', 'LISTO', 'MEDIA',
           'Migración manual: se retiran Ventas y Movimientos, permiso propio y tiles por permiso', '── Bitácora de la sesión (2026-08-07) ──
Pedido: «necesito acomodar mas este modulo de migracion manual ya que este modulo ya tenemos reducido en las cargas masivas los archivos , de ventas y traslados se acen desde el seguimeinto diario entonces no tiene que cargar mas informacion entonces quitamos las cagitas de movimeinto y ventas por que eso se realiza en los seguimeintos >»
Solución (2 commits): feat(migraciones): retira los tipos Ventas/Movimiento de Aves/Movimiento de Huevos; feat(migraciones): permiso de postura, tiles por permiso y modulo solo para Sanmarino
Bugs encontrados: 0.
Evidencia: 21 archivos tocados · 1,2 h de sesión real · commits cbc922c, 07c9c0c · sesión bd434cce
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-07', DATE '2026-08-07', TIMESTAMPTZ '2026-08-07 21:52:14+00', TIMESTAMPTZ '2026-08-07 23:02:47+00', 6.00, 'carga-masiva,excel,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-07 21:52:14+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0006'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260807-bd43');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260807-7ad9', 'BUG', 'LISTO', 'ALTA',
           'El Reporte Diario de Costos de POSTURA nunca mostraba el levante', '── Bitácora de la sesión (2026-08-07) ──
Pedido: «el reprote que se creo para constos de sanmarino Reporte Diario Área de Costos — Postura tiene un error cuando escojo lotes que tiene levante y produccion o levante solamenteo o solo produccion no trae nada como el de la granja niza iii o la que utilizamos de prubas para cargar entonces no esta funcioanndo ya descargue la base de datos de producion actual con los cambios despelgados con ante mano , tmaibne veo un problema que puede ser que un lote base este en barias granjas ya que en niza paso pero no la an movido lo que se ralizo levante en niza iii se pasara a niza i lo que esta en la fase»
Solución (1 commit): chore(tracker): cierra el bloque del reporte de costos de postura
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 18 archivos tocados · 1,1 h de sesión real · commits c6ba60f, 425001e · sesión 7ad9a6f8
Estimación: 4 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-07', DATE '2026-08-07', TIMESTAMPTZ '2026-08-07 22:04:30+00', TIMESTAMPTZ '2026-08-07 23:08:37+00', 4.00, 'reportes,excel,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-07 22:04:30+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0015'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260807-7ad9');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260808-8849', 'BUG', 'LISTO', 'ALTA',
           'Un lote sin liquidar absorbía el ciclo siguiente del galpón (Ecuador)', '── Bitácora de la sesión (2026-08-08) ──
Pedido: «el lote que me reporta ecuador es un lote que ya esta cerrado sin actividad entonces tenemos este problema tamibne me guastaria que pueda validar que solo pueda agregar manual mente los alimento en gestion de einventario del mes actual que se encuentra asi se evita meter meses antes > Buen Dia estimado Moises, me puede ayudar validando el reporte de granja KM 86 lote 01 galpón 1 y 02 tenemos ingreso del mes de julio cuando el lote cerro en abril, adjunto una imagen para su revision»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 28 archivos tocados · 1,1 h de sesión real · commits 7339c61 · sesión 8849a5a1
Estimación: 5 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-08', DATE '2026-08-08', TIMESTAMPTZ '2026-08-08 02:00:49+00', TIMESTAMPTZ '2026-08-08 03:06:37+00', 5.00, 'engorde,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-08 02:00:49+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0012'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260808-8849');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260808-9212', 'TAREA', 'LISTO', 'MEDIA',
           'Alimento previo al encaset: fecha real de llegada e ingreso inicial del ciclo visible', '── Bitácora de la sesión (2026-08-08 → 2026-08-09) ──
Pedido: «en gestion de invetario y encaetameito de un lote es la forma de como nosotros le asingamos el primer alimento a ese lote que tiene ese galpon especificifco que tiene alimento es decir ahroa tengo un problema que es es ro que actuale mente tengo que decirle a cada persona si el aliemnto llego tres o dos dias antes o una semana antes tiene que realizan el ingreso edl primer dia del consumo para que el reprote lo tome en el seguimeinto dairio y cuadren lso valores entonces como podrai realziar esa parte o organizar ya que esto es tanto para postura y pollo engorde pasa eso , tamibne es por que c»
Solución (3 commits): feat(inventario,engorde,postura): fecha real de llegada del alimento + ingreso inicial del ciclo visible; docs(tracker): auditoria de cierre del alimento previo al encaset; docs(tracker): la v16 de engorde se intento 3 veces y se revirtio
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 6 archivos tocados · 4,2 h de sesión real · commits 801b14f, 362155c, d6aeccb, 8424557 · sesión 92124d1d
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-08', DATE '2026-08-09', TIMESTAMPTZ '2026-08-08 02:26:13+00', TIMESTAMPTZ '2026-08-09 07:19:56+00', 12.00, 'inventario,gastos,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-08 02:26:13+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0007'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260808-9212');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260809-a721', 'TAREA', 'LISTO', 'MEDIA',
           'PWA F1-F2 + stock atómico: app instalable, consulta offline y escrituras concurrentes a salvo', '── Bitácora de la sesión (2026-08-09 → 2026-08-10) ──
Pedido: «coloca lo faltande en acomodar todo para que sea pwa la app 100% tomando todos los arreglaso posibles que encontres y lo dejes listo al final de la sesion toma los mejores caminos y realiza pruebas en vivo de cada funcionamiento»
Solución (5 commits): feat(pwa): la app se vuelve una PWA instalable, autoactualizable y con kill switch; feat(pwa): consulta offline - la app deja de quedar vacia sin red; feat(sync): lapidas de borrado + auditoria del estado real de F0.A; docs(f0a): A6 medido y cerrado como no-se-cambia; la colision del plan no existe en los datos; feat(engorde): detector de atribucion de lote — el cuadre es CIEGO a este defecto (A9, paso 1)
Bugs encontrados: 3 — cada uno queda como subtarea BUG con su causa.
Evidencia: 68 archivos tocados · 2,6 h de sesión real · commits 8ecb7c6, c55a8e1, 60d3125, f82874e, f70603d, 44b2400, 502ad98, 813e9f5 · sesión a721c8a5
Estimación: 14 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-09', DATE '2026-08-10', TIMESTAMPTZ '2026-08-09 20:16:40+00', TIMESTAMPTZ '2026-08-10 04:06:41+00', 14.00, 'plataforma,devops,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-09 20:16:40+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0019'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260809-a721');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260811-d0bf', 'TAREA', 'LISTO', 'MEDIA',
           'Programación de lotes de engorde para Ecuador + gasto contra lote PROGRAMADO', '── Bitácora de la sesión (2026-08-11) ──
Pedido: «ahroa necesito crear un un modulo o agregarlo tambine a ecuador la necesidad de lote base que tiene panama donde crean la programacion de lotes y se asignan a una granja para que al momento de crar un lote de pollo engorde aparesca si esta singado a la granja por el que tiene el permiso de este lote progrmacion necesito que valides esa parte para que ecuador tambine lo tenga y la parte del nombre sera ya la que este definidad asi apareceran lotes y dejaran de aparecer tamibne entonces aqui dejo la necesidad de ecuador > Descripción Buenas tardes estimado Moises, solicito su ayuda con un módul»
Solución (14 commits): feat(companies): dos flags tipados para la programacion de lotes de engorde; feat(inventario): el gasto puede colgar de un lote PROGRAMADO; feat(engorde): el nombre del lote deja de asumir el sufijo de Panama; feat(inventario): reglas puras del gasto programado y su re-atribucion; feat(inventario): registrar y listar el gasto contra un lote programado; feat(inventario): fn_inventario_gastos_search devuelve el lote programado; feat(engorde): al encasetar, los gastos de la programacion pasan al lote real; chore(db): migraciones de esquema de la programacion de lotes
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 22 archivos tocados · 2,3 h de sesión real · commits 27f1348, 495d7c4, 252015b, 3682e63, d766a84, 8ebede6, 118ea8d, 067453e, 3232254, ed055fa · sesión d0bf32ae
Estimación: 14 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-11', DATE '2026-08-11', TIMESTAMPTZ '2026-08-11 20:58:39+00', TIMESTAMPTZ '2026-08-11 23:14:39+00', 14.00, 'engorde,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-11 20:58:39+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0012'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260811-d0bf');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260811-cbb2', 'DOCUMENTACION', 'ANALISIS', 'MEDIA',
           'Auditoría de impacto de la columna mixto en los reportes de Panamá', '── Bitácora de la sesión (2026-08-11 → 2026-08-12) ──
Pedido: «realiza los archivos word de estas sin pdf , de aceurdo a cada formato es un trabajo que sera a mano pero me daras todo el disno completo de como esta , dame los word en el escritorio»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: 1,8 h de sesión real · sesión cbb290ec
Estimación: 4 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'ANALISIS'),
           DATE '2026-08-11', DATE '2026-08-12', TIMESTAMPTZ '2026-08-11 22:43:54+00', NULL, 4.00, 'engorde,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-11 22:43:54+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0012'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260811-cbb2');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260811-7a82', 'TAREA', 'LISTO', 'MEDIA',
           'El alimento mixto de engorde deja de contarse como consumo de hembras (app y Power BI)', '── Bitácora de la sesión (2026-08-11 → 2026-08-12) ──
Pedido: «en el modulo de seguimiento diario pollo engorde , en el alimento e los 7 dias si debe aparecer por genero en el seguimiento diario pero cuando ya se cumple los 7 dias que se realiza desde este modulo debemos tener un campo nuevo que se llame alimento mixto asi no se convinan ya que actua mente muestra que esta en hembras entonces por medio de la informacion se confunde visual mente esto es mas un cambio visual y del excel al momento de descargar , te coloco el archivo excel Consumo hembras (kg)»
Solución (4 commits): feat(engorde): el alimento mixto deja de contarse como consumo de hembras; docs(engorde): auditoria de impacto de la columna mixto en los reportes de Panama; feat(powerbi): el consumo mixto de engorde deja de publicarse como consumo de hembras; docs(powerbi): el espejo SQL de la vista de engorde vuelve a ser fiel a lo desplegado
Bugs encontrados: 0.
Evidencia: 12 archivos tocados · 1,0 h de sesión real · commits dd85c51, 694836b, 2f2a00a, 156a0d1 · sesión 7a82bef9
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-11', DATE '2026-08-12', TIMESTAMPTZ '2026-08-11 23:30:05+00', TIMESTAMPTZ '2026-08-12 02:33:45+00', 6.00, 'engorde,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-11 23:30:05+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0012'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260811-7a82');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260812-93b9', 'TAREA', 'LISTO', 'MEDIA',
           'Permisos por empresa: cada empresa define qué permisos existen para ella', '── Bitácora de la sesión (2026-08-12) ──
Pedido: «ahroa que estoy valdiando queiro saber si tengo una migracion que crea los permiso para el modulo de migracion manual para postura por que no lo veo tmaibne me gustaria definir los permisos que podran ver por empresa ya que hay permisos de modulos que no se utilizan digamos en las empresas de ecuador y panama que colombia no lo tiene y viseversas deberia tener ese parametro en el modulo de empresa asi como especifico el menu tamibne los persmiso que debe tener esa empresa en particular , y asi el modulo de permisos al crear el rol depende de lo que selecione que puedan ver»
Solución (5 commits): docs(tracker): commit del gate del borde marcado; feat(permisos): cada empresa define qué permisos existen para ella; feat(permisos): el backend también rechaza el permiso que la empresa no habilita; docs(tracker): el backend queda arriba a proposito para la validacion; docs(tracker): validacion de F3.1 con los dos perfiles de operario reales
Bugs encontrados: 0.
Evidencia: 46 archivos tocados · 1,9 h de sesión real · commits 3407cb2, cf9ed0f, 3e0c2a3, f01a165, 574cfb0 · sesión 93b94a8b
Estimación: 12 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-12', DATE '2026-08-12', TIMESTAMPTZ '2026-08-12 02:35:23+00', TIMESTAMPTZ '2026-08-12 04:26:18+00', 12.00, 'seguridad,auth,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-12 02:35:23+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0004'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260812-93b9');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260812-f5d0', 'BUG', 'LISTO', 'ALTA',
           'El gate del borde del front exigía que la PWA no existiera y tumbaba el deploy', '── Bitácora de la sesión (2026-08-12) ──
Pedido: «valida porque al momento de realizar un despliegue el frot genero error en el git aqui dejo el archivo zip del log del front que genero error , con eso validar el error y solucioanrlo»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 4 archivos tocados · 0,3 h de sesión real · commits 6f410db · sesión f5d064a6
Estimación: 2 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-12', DATE '2026-08-12', TIMESTAMPTZ '2026-08-12 02:36:38+00', TIMESTAMPTZ '2026-08-12 02:53:08+00', 2.00, 'plataforma,devops,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-12 02:36:38+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0019'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260812-f5d0');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260812-0d35', 'TAREA', 'LISTO', 'MEDIA',
           'PWA F3: captura offline con idempotencia real (postura, engorde y reproductora)', '── Bitácora de la sesión (2026-08-12) ──
Pedido: «para lo de pwa que faltaria mas»
Solución (8 commits): feat(pwa): captura offline con idempotencia real (F3.1); feat(pwa): el operario ya sabe donde quedo lo que capturo sin red; feat(pwa): captura offline tambien en produccion (F3.2); feat(pwa): captura offline de engorde, pollo y reproductora (F3.3); docs(tracker): la reproductora de pollo engorde es modulo exclusivo de Panama; docs(pwa): auditoria de acceso offline — menu muerto, primer ingreso y acciones operativas; docs(tracker): punto de retoma de la PWA para continuar en otra sesion; feat(menus): el menu de reproductora de postura queda definido pero sin asignar, con la etiqueta corregida
Bugs encontrados: 0.
Evidencia: 46 archivos tocados · 3,6 h de sesión real · commits c44e0a4, de3ea10, b681a50, 505c13b, b56459c, 30c6865, 88f1d3d, 6980fa3 · sesión 0d35be4e
Estimación: 14 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-12', DATE '2026-08-12', TIMESTAMPTZ '2026-08-12 04:27:20+00', TIMESTAMPTZ '2026-08-12 08:04:08+00', 14.00, 'plataforma,devops,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-12 04:27:20+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0019'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260812-0d35');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260812-2d5d', 'DOCUMENTACION', 'LISTO', 'MEDIA',
           'Estado medido de la PWA y brecha real para salir a producción', '── Bitácora de la sesión (2026-08-12) ──
Pedido: «validemso lo que tenemos en el pwa a y lo que falta por terminar y que pueda salir a funcionar»
Solución (2 commits): docs(pwa): validacion medida del estado y la brecha real para salir a produccion; feat(sql): invariante que prueba que company_permissions no dejo a nadie sin acceso
Bugs encontrados: 0.
Evidencia: 10 archivos tocados · 0,8 h de sesión real · commits 71836ff, 8f1cb56 · sesión 2d5d63ea
Estimación: 4 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-12', DATE '2026-08-12', TIMESTAMPTZ '2026-08-12 09:20:57+00', TIMESTAMPTZ '2026-08-12 10:11:56+00', 4.00, 'plataforma,devops,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-12 09:20:57+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0019'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260812-2d5d');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260812-a3c4', 'BUG', 'LISTO', 'ALTA',
           'La recuperación de contraseña estaba cortada: el correo imprimía el token como contraseña', '── Bitácora de la sesión (2026-08-12) ──
Pedido: «realiza pruebas locales sin modificar nada solo enocntrar que funcione el envio de correos eletronicos de la aplicacion ya que anterior mente se habia realizado algo cuando en pruebas controladas estaba solucioando solo cambiarle el protocolo que tenia a tls para que funcionara el envio de correo es que neceisto la opcio nde recuperacion de contrase;as»
Solución (2 commits): feat(correos): la recuperacion de contraseña estaba cortada, no solo el SMTP; feat(correos): el encabezado pasa a ser el de la pantalla de ingreso
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 38 archivos tocados · 2,5 h de sesión real · commits dcba98f, 29dfdfd, 565164a · sesión a3c49b65
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-12', DATE '2026-08-12', TIMESTAMPTZ '2026-08-12 11:10:40+00', TIMESTAMPTZ '2026-08-12 19:54:44+00', 8.00, 'integraciones,correo,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-12 11:10:40+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0018'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260812-a3c4');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260812-484c', 'TAREA', 'LISTO', 'MEDIA',
           'Santa Reyes — Silos y bodegas como ubicación real del inventario (plan + Fase A)', '── Bitácora de la sesión (2026-08-12) ──
Pedido: «para la empresa santa reyes de colombia necesito plantiar muy bien este plan de trabajo ya que modificara modulos existente para acomodarlos a ellos por ahroa es todo lo que es postura con este cambio desde la gestion de granja creemos el plam bien mirando bien detallado los serviios y lugares que se veran afectados funcione tmaibn de base dedatos y servicios en el back y front para acomodar para esta empresa > Para la empresa Santa Reyes va a haber un cambio lógico. Hay que mirar cómo se estructura para ellos, porque ellos manejan unas cosas que se llaman silos. Y la idea de los silos es que»
Solución (2 commits): docs(santa-reyes): plan de silos y bodegas como ubicacion real del inventario; feat(silos): la granja, el galpon y el lote declaran sus silos (Santa Reyes, Fase A)
Bugs encontrados: 0.
Evidencia: 63 archivos tocados · 2,1 h de sesión real · commits 503d5a3, 7f43581 · sesión 484c317f
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-12', DATE '2026-08-12', TIMESTAMPTZ '2026-08-12 21:51:40+00', TIMESTAMPTZ '2026-08-12 23:59:53+00', 10.00, 'inventario,gastos,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-12 21:51:40+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0007'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260812-484c');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260813-c487', 'TAREA', 'LISTO', 'MEDIA',
           'Silos Fase B: silo_id en el stock y swap del índice único acoplado al ON CONFLICT', '── Bitácora de la sesión (2026-08-13) ──
Pedido: «La Fase B es la del riesgo: ahí va el silo_id en inventario_gestion_stock y el swap del índice único, que va acoplado al ON CONFLICT de SumarStockAtomicoAsync — desalineados, revienta todo ingreso de todas las empresas. Empieza con el smoke de regresión en Sanmarino y Ecuador antes de tocar nada de Santa Reyes. ¿Sigo con la Fase B?»
Solución (2 commits): feat(silos): el saldo aprende a vivir en un silo, sin mover el de nadie (Santa Reyes, Fase B parcial); feat(silos): el movimiento ya sabe en que silo cae (Santa Reyes, Fase B backend)
Bugs encontrados: 0.
Evidencia: 19 archivos tocados · 1,4 h de sesión real · commits a15c7ac, 5f2fa35 · sesión c487cccd
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-13', DATE '2026-08-13', TIMESTAMPTZ '2026-08-13 00:17:15+00', TIMESTAMPTZ '2026-08-13 02:21:04+00', 10.00, 'inventario,gastos,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-13 00:17:15+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0007'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260813-c487');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260813-35a8', 'TAREA', 'LISTO', 'MEDIA',
           'Silos Fase B (front): el operario elige en qué silo cae el alimento', '── Bitácora de la sesión (2026-08-13) ──
Pedido: «Lo que falta La pantalla 5 del front (/gestion-inventario: selector de silo en ingreso y traslado, columna Silo en las grillas, recepción por silo, export) y tres lecturas del backend que todavía no proyectan el silo: GetIngresosAsync, GetTrasladosAsync y GetFilterDataAsync. El tracker lo refleja línea por línea. ¿Sigo con eso?»
Solución (1 commit): feat(silos): el operario ya puede decir en que silo cae el alimento (Santa Reyes, Fase B cerrada)
Bugs encontrados: 0.
Evidencia: 12 archivos tocados · 1,0 h de sesión real · commits 72b4bf2 · sesión 35a89cd4
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-13', DATE '2026-08-13', TIMESTAMPTZ '2026-08-13 04:23:44+00', TIMESTAMPTZ '2026-08-13 05:22:37+00', 8.00, 'inventario,gastos,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-13 04:23:44+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0007'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260813-35a8');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260813-b66e', 'TAREA', 'LISTO', 'MEDIA',
           'Silos Fase C: el consumo diario y los gastos dicen de qué silo salen', '── Bitácora de la sesión (2026-08-13 → 2026-08-14) ──
Pedido: «Queda la Fase C: consumo por silo desde el seguimiento diario (ItemConsumoKey con siloId, ColombiaInventarioConsumoService, pantallas 6-7). ¿Sigo con eso?»
Solución (3 commits): feat(silos): el consumo diario ya dice de que silo sale (Santa Reyes, Fase C); feat(silos): Gastos por silo y los reportes leen el alimento donde la empresa lo tiene; feat(reportes): Sanmarino tambien lee el alimento del inventario unificado
Bugs encontrados: 0.
Evidencia: 27 archivos tocados · 1,6 h de sesión real · commits c5f67fa, 22a0ac3, ab6e97d · sesión b66ee068
Estimación: 10 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-13', DATE '2026-08-14', TIMESTAMPTZ '2026-08-13 06:31:07+00', TIMESTAMPTZ '2026-08-14 00:40:03+00', 10.00, 'inventario,gastos,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-13 06:31:07+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0007'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260813-b66e');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260813-7866', 'BUG', 'LISTO', 'ALTA',
           'Con el flag de silos puesto, el consumo no encontraba su propio ítem (Santa Reyes)', '── Bitácora de la sesión (2026-08-13) ──
Pedido: «realiza la prueba y luego sigue con el de > Lo que quedó afuera: el smoke de Santa Reyes (casos 18-24) y el caso 23. No es un bloqueo de código: la BD local no tiene ningún lote de SR (lotes de granja 109 = 0, lote_silos vacía — el smoke de la Fase B se restauró). Para correrlo hay que fabricar antes núcleo + galpón + lote + lote_postura_levante + lote_silos + un ingreso al silo. ¿Armo ese fixture y corro los casos ON, o seguimos con la Fase D?»
Solución (1 commit): docs(silos): el smoke ahora cubre el ciclo completo de produccion, no solo el alta
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 7 archivos tocados · 0,6 h de sesión real · commits 86111b6, 803f170 · sesión 7866b0a5
Estimación: 4 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-13', DATE '2026-08-13', TIMESTAMPTZ '2026-08-13 11:36:49+00', TIMESTAMPTZ '2026-08-13 16:35:39+00', 4.00, 'inventario,gastos,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-13 11:36:49+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0007'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260813-7866');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260813-de48', 'BUG', 'LISTO', 'ALTA',
           'Silos Fase D: el reporte de existencias repetía el ítem una vez por silo', '── Bitácora de la sesión (2026-08-13) ──
Pedido: «Sigo con la Fase D — empezando por fn_inventario_gastos_existencias, que hoy asume una fila de stock por granja+ítem y con N silos multiplicaría filas.»
Solución (3 commits): docs(silos): la carga masiva y los reportes no necesitan silo, pero los reportes leen la tabla vieja; docs(silos): las dos precondiciones de prod que fallarian en silencio al desplegar; chore(silos): chequeo de go-live de Santa Reyes contra datos reales de produccion
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 5 archivos tocados · 0,7 h de sesión real · commits 6e3b167, b546c06, 0529bec, 584394e · sesión de48b9bb
Estimación: 6 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-13', DATE '2026-08-13', TIMESTAMPTZ '2026-08-13 17:10:48+00', TIMESTAMPTZ '2026-08-13 20:06:28+00', 6.00, 'inventario,gastos,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-13 17:10:48+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0007'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260813-de48');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260813-bb55', 'TAREA', 'LISTO', 'MEDIA',
           'Gerencia — Panel de control de ItalJira en solo lectura (permiso tickets.indicadores)', '── Bitácora de la sesión (2026-08-13) ──
Pedido: «en los modulos que tengo de italjira puedo darle permisos aun rol en especifico para que pueda ver solo ese item delmenu y darle permiso algunos iten internos que solo sea el de Panel de control ya que lo quiero agregar a un modulo particular que seria el de gerencia pero si se uede queiro validar ya que le tenai unas reglas que solo el rol admin podra verlo»
Solución (1 commit): feat(gerencia): el gerente ya puede ver los indicadores sin poder tocar nada
Bugs encontrados: 0.
Evidencia: 23 archivos tocados · 1,1 h de sesión real · commits c1ed6e3 · sesión bb55e00f
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-13', DATE '2026-08-13', TIMESTAMPTZ '2026-08-13 17:14:24+00', TIMESTAMPTZ '2026-08-13 18:22:12+00', 8.00, 'italjira,tickets,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-13 17:14:24+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0020'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260813-bb55');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260813-7218', 'BUG', 'LISTO', 'ALTA',
           'La copia descargable de DB Studio dejaba 4 funciones sin crear al restaurar', '── Bitácora de la sesión (2026-08-13) ──
Pedido: «estoy cargando la bse de datos de produccion a local eliminando la base de atos para cargarla desde limpio y me sale este error que biene de produccion pero en produccion no falla > ERROR: function fn_seguimiento_diario_engorde(integer) does not exist LINE 100794: FROM fn_seguimiento_diario_engorde(p_lote_id) f ^ HINT: No function matches the given name and argument types. You might need to add explicit type casts. SQL state: 42883 Character: 24159793»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 1 — cada uno queda como subtarea BUG con su causa.
Evidencia: 6 archivos tocados · 1,2 h de sesión real · commits 9e9e24a · sesión 72180ffd
Estimación: 4 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'LISTO'),
           DATE '2026-08-13', DATE '2026-08-13', TIMESTAMPTZ '2026-08-13 20:17:44+00', TIMESTAMPTZ '2026-08-13 21:27:34+00', 4.00, 'plataforma,devops,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-13 20:17:44+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0019'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260813-7218');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, 'SES-20260814-880f', 'DOCUMENTACION', 'EN_CURSO', 'MEDIA',
           'Bitácora ItalJira de julio y agosto 2026: horas, solución y bugs por sesión', '── Bitácora de la sesión (2026-08-14) ──
Pedido: «quiero crear una migracion con todas la tareas y historias que se ah venido realizando en los ticket co ntiempos estimados y en el la fase de solcuion por que se ah solucionado y errores bug que se ah encontrado tamibne de acuerdo a todas la sesiones de este mes y el anterior»
Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea (sin commit propio atribuible a esta sesión).
Bugs encontrados: 0.
Evidencia: 7 archivos tocados · 0,3 h de sesión real · sesión 880f7278
Estimación: 8 h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = 'EN_CURSO'),
           DATE '2026-08-14', DATE '2026-08-14', TIMESTAMPTZ '2026-08-14 00:46:29+00', NULL, 8.00, 'italjira,tickets,bitacora',
           v_company, v_cedula, TIMESTAMPTZ '2026-08-14 00:46:29+00'
    FROM public.historias h
    WHERE h.codigo = 'HIS-2026-0020'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'SES-20260814-880f');
    -- ═══ 3) Bugs encontrados: un commit fix(...) = una subtarea BUG de su tarea ═══
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-9b7d75e', 'BUG', 'LISTO', 'ALTA',
           'fix(indicador-ecuador): total kilos despachados a cliente se muestra siempre (sin merma = kg carne pollo)', 'Bug detectado y corregido durante «SoporteBot — Loop de soporte automatizado sobre el módulo de Tickets».
Commit 9b7d75e (2026-07-06).
Causa/detalle registrado en el commit: Liquidación Técnica Pollo Engorde EC: cuando no hay merma en kilos/unidades, ''Total kilos despachados a cliente'' quedaba NULL (''—''). Ahora se refleja = kg carne pollo (no existe merma), y con merma se mantiene kg carne - merma (aritmética previa intacta). - fn_indicadores_pollo_engorde: total_kilos_despachados_cliente = kg_carne - COALESCE(merma_kilos,0) (antes: CASE WHEN merma_registrada THEN', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-06', DATE '2026-07-06', TIMESTAMPTZ '2026-07-06 12:00:00+00', TIMESTAMPTZ '2026-07-06 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-06 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0001-T11'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-9b7d75e');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-cc54beb', 'BUG', 'LISTO', 'ALTA',
           'fix: H3 - NG0103 latente en modales de seguimiento postura Colombia', 'Bug detectado y corregido durante «SoporteBot — Loop de soporte automatizado sobre el módulo de Tickets».
Commit cc54beb (2026-07-02).
Causa/detalle registrado en el commit: Barrido estatico del mismo anti-patron de H1 (*ngFor sobre metodo que aloca) en todo el front: la mayoria de foo() en *ngFor son signals (ref estable, OK), pero getAlimentosFiltradosPorTipo se repetia con implementacion alocadora en dos modales mas de Colombia postura: lote-produccion/modal-seguimiento-diario y lote-levante/modal-create-edit. Aplicada la misma memoizacion por igualdad de contenido', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-02', DATE '2026-07-02', TIMESTAMPTZ '2026-07-02 12:00:00+00', TIMESTAMPTZ '2026-07-02 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-02 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0001-T11'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-cc54beb');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-f505816', 'BUG', 'LISTO', 'ALTA',
           'fix: H4 - NG0103 en gestion-inventario (16 getters + 8 metodos que alocaban en *ngFor)', 'Bug detectado y corregido durante «SoporteBot — Loop de soporte automatizado sobre el módulo de Tickets».
Commit f505816 (2026-07-02).
Causa/detalle registrado en el commit: Reproducido: seleccionar granja en Ingresos disparaba 2-4 NG0103 (Infinite change detection). Causa: getters (farmsDestino/farmsOrigen, nucleosFiltered/galponesFiltered, historico*Options/Filtered) y metodos (*NucleosFiltered/*GalponesFiltered, recepcion*ForFarm) devolvian un array nuevo en cada acceso, usados directamente en *ngFor. Fix: helper listaEstable (memoizacion por firma de contenido: mi', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-02', DATE '2026-07-02', TIMESTAMPTZ '2026-07-02 12:00:00+00', TIMESTAMPTZ '2026-07-02 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-02 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0001-T11'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-f505816');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-c239c90', 'BUG', 'LISTO', 'ALTA',
           'fix(inventario): S1 gate por país del lote — cierra descuento cross-país silencioso', 'Bug detectado y corregido durante «SoporteBot — Loop de soporte automatizado sobre el módulo de Tickets».
Commit c239c90 (2026-07-02).
Causa/detalle registrado en el commit: Bug: el consumo desde seguimientos usa el fallback catalogItemId→item_inventario_ecuador_id (en MetadataEngordeCalculos.ParseMetadataItemsToKg). Para lotes Colombia, si un catalogItemId colisiona con un item_inventario_ecuador.id real con stock, se descontaba del inventario Ecuador (modelo B) en silencio (dejó 1 fila espuria). Fix aguas arriba (no se toca el parser, que tiene test verde fijando e', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-02', DATE '2026-07-02', TIMESTAMPTZ '2026-07-02 12:00:00+00', TIMESTAMPTZ '2026-07-02 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-02 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0001-T11'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-c239c90');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-a1f0af3', 'BUG', 'LISTO', 'ALTA',
           'fix', 'Bug detectado y corregido durante «Un solo comando para levantar back y front en .NET 10 (make dev)».
Commit a1f0af3 (2026-07-10).', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-10', DATE '2026-07-10', TIMESTAMPTZ '2026-07-10 12:00:00+00', TIMESTAMPTZ '2026-07-10 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-10 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'SES-20260710-50cd'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-a1f0af3');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-09df059', 'BUG', 'LISTO', 'ALTA',
           'fix(seguimiento-levante): disponible no descontaba reservas de otras filas del mismo formulario', 'Bug detectado y corregido durante «Plan — Seguimiento diario: catálogo de alimento desde inventario NUEVO + alimento distinto para machos».
Commit 09df059 (2026-07-10).
Causa/detalle registrado en el commit: Bug reportado en prueba: si un item se elegia en Hembras (p.ej. 900 kg, sin guardar) y el mismo item se elegia luego en Machos, el dropdown y el hint de ""Stock disponible"" mostraban el stock COMPLETO en ambas filas en vez del remanente real, ya que el backend suma ambas filas en un solo descuento. Permitia sobre-asignar alimento inexistente sin ningun aviso hasta guardar. sumaReservadaEnOtrasFila', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-10', DATE '2026-07-10', TIMESTAMPTZ '2026-07-10 12:00:00+00', TIMESTAMPTZ '2026-07-10 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-10 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0007-T11'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-09df059');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-f3f9c1d', 'BUG', 'LISTO', 'ALTA',
           'fix(seguimiento-levante): wiring del disponible ajustado en dropdown y hints', 'Bug detectado y corregido durante «Plan — Seguimiento diario: catálogo de alimento desde inventario NUEVO + alimento distinto para machos».
Commit f3f9c1d (2026-07-10).
Causa/detalle registrado en el commit: Pasa la fila actual (excludeControl) a getItemDisplayText/ getCantidadDisponibleAjustada/getMaxPermitidoKg en los tres bloques (Hembras, Machos, Generales) para que el ""Disponible"" mostrado descuente lo que otras filas del mismo formulario ya reservan del mismo item.', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-10', DATE '2026-07-10', TIMESTAMPTZ '2026-07-10 12:00:00+00', TIMESTAMPTZ '2026-07-10 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-10 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0007-T11'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-f3f9c1d');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-97ba976', 'BUG', 'LISTO', 'ALTA',
           'fix(seguimiento-produccion): disponible ajustado + validacion de tope (no existia)', 'Bug detectado y corregido durante «Plan — Seguimiento diario: catálogo de alimento desde inventario NUEVO + alimento distinto para machos».
Commit 97ba976 (2026-07-10).
Causa/detalle registrado en el commit: Mismo bug que en levante: el dropdown mostraba el stock crudo sin descontar lo que otras filas del formulario (sin guardar) ya reservaban del mismo item. Produccion ademas NO tenia ninguna validacion de ""supera stock"" (a diferencia de levante), asi que el riesgo de asignar mas alimento del que existe pasaba completamente desapercibido hasta guardar. Se agrega sumaReservadaEnOtrasFilas + getCantid', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-10', DATE '2026-07-10', TIMESTAMPTZ '2026-07-10 12:00:00+00', TIMESTAMPTZ '2026-07-10 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-10 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0007-T11'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-97ba976');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-32d4450', 'BUG', 'LISTO', 'ALTA',
           'fix(seguimiento-produccion): wiring del disponible ajustado + bloquear guardado si excede', 'Bug detectado y corregido durante «Plan — Seguimiento diario: catálogo de alimento desde inventario NUEVO + alimento distinto para machos».
Commit 32d4450 (2026-07-10).
Causa/detalle registrado en el commit: Pasa la fila actual (excludeControl) a getItemDisplayText/ getCantidadDisponibleAjustada en hembras y machos, agrega el mensaje ""Supera el stock disponible"" y deshabilita el boton Guardar cuando hasCantidadExcedida es true (mismo criterio que levante).', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-10', DATE '2026-07-10', TIMESTAMPTZ '2026-07-10 12:00:00+00', TIMESTAMPTZ '2026-07-10 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-10 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0007-T11'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-32d4450');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-2eab7f8', 'BUG', 'LISTO', 'ALTA',
           'fix(migraciones-masivas): descuento incremental de aves en Seguimiento Levante', 'Bug detectado y corregido durante «Fase 3 — Migraciones Masivas: Ventas + Movimiento Aves + Movimiento Huevos (ESPECIFICACIÓN)».
Commit 2eab7f8 (2026-07-13).
Causa/detalle registrado en el commit: La migración masiva de Seguimiento Levante recalculaba aves_h_actual/aves_m_actual desde cero (inicial - mortalidad - selección - error), pisando los traslados entre lotes y los movimientos del módulo Movimiento de Aves ya aplicados al lote. Además, si una fecha ya tenía una fila ""solo traslado"", la saltaba en silencio en vez de completarla. Ahora el descuento es incremental sobre el valor actual', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-13', DATE '2026-07-13', TIMESTAMPTZ '2026-07-13 12:00:00+00', TIMESTAMPTZ '2026-07-13 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-13 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0006-T2'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-2eab7f8');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-4e49369', 'BUG', 'LISTO', 'ALTA',
           'fix(migraciones-masivas): descuento incremental de aves en Seguimiento Producción', 'Bug detectado y corregido durante «Fase 3 — Migraciones Masivas: Ventas + Movimiento Aves + Movimiento Huevos (ESPECIFICACIÓN)».
Commit 4e49369 (2026-07-13).
Causa/detalle registrado en el commit: Mismo bug que Seguimiento Levante, en fn_migracion_seguimiento_produccion: recalculaba aves_h_actual/aves_m_actual desde cero, pisando los traslados entre lotes de Producción y los movimientos del módulo Movimiento de Aves ya aplicados al lote. Diferencia encontrada respecto a Levante: las filas de traslado de Producción (TrasladoAvesDesdeSegService) no setean lote_postura_produccion_id, así que', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-13', DATE '2026-07-13', TIMESTAMPTZ '2026-07-13 12:00:00+00', TIMESTAMPTZ '2026-07-13 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-13 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0006-T2'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-4e49369');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-19d2f58', 'BUG', 'LISTO', 'ALTA',
           'fix(seguimiento-produccion): filtrar select de items solo a los que tienen stock', 'Bug detectado y corregido durante «El select de alimentos de Seguimiento Producción listaba ítems sin stock».
Commit 19d2f58 (2026-07-14).
Causa/detalle registrado en el commit: El modal de seguimiento diario producción listaba todos los items activos del inventario aunque no tuvieran existencias. Se reutiliza el stock ya cargado (inventarioPorItem) para filtrar el dropdown, sin tocar el endpoint compartido /api/inventario/items ni otros modulos que lo consumen. Se conserva visible un item ya seleccionado en el formulario aunque su stock llegue a 0, para no romper la edic', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-14', DATE '2026-07-14', TIMESTAMPTZ '2026-07-14 12:00:00+00', TIMESTAMPTZ '2026-07-14 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-14 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'SES-20260714-b0af'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-19d2f58');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-99c8736', 'BUG', 'LISTO', 'ALTA',
           'fix(seguimiento-produccion): registrar company_id/created_by_user_id/created_at al crear y editar', 'Bug detectado y corregido durante «El consumo de Seguimiento Producción no descontaba el inventario (ítems camino-2)».
Commit 99c8736 (2026-07-14).
Causa/detalle registrado en el commit: CrearSeguimientoAsync no seteaba estos campos de auditoria (quedaban en 0/default), mismo patron ya usado en SeguimientoProduccionService. ActualizarSeguimientoAsync ahora tambien registra updated_by_user_id/updated_at.', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-14', DATE '2026-07-14', TIMESTAMPTZ '2026-07-14 12:00:00+00', TIMESTAMPTZ '2026-07-14 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-14 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'SES-20260714-bab8'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-99c8736');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-92087b4', 'BUG', 'LISTO', 'ALTA',
           'fix(seguimiento-produccion): descontar inventario de items camino-2 (item_inventario_ecuador sin espejo)', 'Bug detectado y corregido durante «El consumo de Seguimiento Producción no descontaba el inventario (ítems camino-2)».
Commit 92087b4 (2026-07-14).
Causa/detalle registrado en el commit: AcumularItemsRequestPorCatalogItem solo leia CatalogItemId y lo saltaba en 0, que es justo el valor que llega para items nuevos del inventario (sin fila espejo en catalogo_items, ej. ""moises""): esos items llegan con ItemInventarioEcuadorId y CatalogItemId=0, y quedaban fuera del descuento/validacion de stock sin error. Ahora resuelve el id igual que ParseMetadataItemsToKg (prioridad ItemInventario', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-14', DATE '2026-07-14', TIMESTAMPTZ '2026-07-14 12:00:00+00', TIMESTAMPTZ '2026-07-14 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-14 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'SES-20260714-bab8'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-92087b4');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-c6bbd29', 'BUG', 'LISTO', 'ALTA',
           'fix(aves-engorde): restar mortalidad en caja en tabla diaria y liquidacion', 'Bug detectado y corregido durante «Plan — Fix: """"aves vivas"""" (tabla diaria / liquidación) ignora mortalidad en caja (mort_caja_h/m)».
Commit c6bbd29 (2026-07-14).
Causa/detalle registrado en el commit: fn_seguimiento_diario_engorde (tabla diaria Ecuador) y GetLiquidacionResumenAsync (liquidacion, Ecuador y Colombia) no restaban mort_caja_h/mort_caja_m del total inicial, mostrando aves vivas fantasma cuando el widget ""Aves disponibles"" y la validacion de creacion de registros si la restaban del maestro. Caso real: lote 77 ""2603"" (Sacachun 3b, galpon G0049) con mort_caja_h=17 mostraba 17 aves viva', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-14', DATE '2026-07-14', TIMESTAMPTZ '2026-07-14 12:00:00+00', TIMESTAMPTZ '2026-07-14 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-14 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0012-T13'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-c6bbd29');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-d44cb07', 'BUG', 'LISTO', 'ALTA',
           'fix(vacunacion): convierte el seed del menu a migracion EF', 'Bug detectado y corregido durante «Módulo Vacunación — cronogramas por lote/granja/galpón».
Commit d44cb07 (2026-07-14).
Causa/detalle registrado en el commit: El menu (grupo ""Vacunacion"" + 3 hijos) era el unico objeto de BD del modulo aplicado directo (psql -f), sin migracion, por lo que no se creaba solo en cada deploy. Se envuelve el mismo SQL idempotente en la migracion AddVacunacionMenu (aplicada y verificada: 4 migraciones en __EFMigrationsHistory, sin filas de menu duplicadas) y se elimina el .sql suelto para no duplicar la fuente de verdad.', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-14', DATE '2026-07-14', TIMESTAMPTZ '2026-07-14 12:00:00+00', TIMESTAMPTZ '2026-07-14 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-14 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0003-T1'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-d44cb07');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-9c87ec6', 'BUG', 'LISTO', 'ALTA',
           'fix permisos', 'Bug detectado y corregido durante «Vacunación: el cronograma no traía nada por permisos faltantes».
Commit 9c87ec6 (2026-07-15).', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-15', DATE '2026-07-15', TIMESTAMPTZ '2026-07-15 12:00:00+00', TIMESTAMPTZ '2026-07-15 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-15 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'SES-20260714-21a8'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-9c87ec6');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-0f89e59', 'BUG', 'LISTO', 'ALTA',
           'fix(indicadores-levante): consumo/peso/mortalidad/retiro H-M reales y de guia, regional y acumulados', 'Bug detectado y corregido durante «Plan — Matriz Verenice rev 6-jul-26 · Postura Colombia (validación + corrección)».
Commit 0f89e59 (2026-07-17).
Causa/detalle registrado en el commit: fn_indicadores_levante_postura: columnas por sexo (consumo/peso/mortalidad/retiro) reales y de guia sin promediar; acumulados = bajas/aves iniciales; excluye filas de traslado (elimina semana fantasma); fallback de aves y guarda de encaset futuro (0 filas); DROP TEMP TABLE IF EXISTS. - LotePosturaLevanteService: Regional resuelto desde master_list_options cuando la columna esta vacia/NULL. - Ind', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-17', DATE '2026-07-17', TIMESTAMPTZ '2026-07-17 12:00:00+00', TIMESTAMPTZ '2026-07-17 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-17 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0014-T5'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-0f89e59');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-10fd9dd', 'BUG', 'LISTO', 'ALTA',
           'fix(indicadores-levante-ui): columnas corridas, chips de region y sin conversion', 'Bug detectado y corregido durante «Plan — Matriz Verenice rev 6-jul-26 · Postura Colombia (validación + corrección)».
Commit 10fd9dd (2026-07-17).
Causa/detalle registrado en el commit: tabla-lista-indicadores: reordena los td (peso/uniformidad) para calzar con el thead (arregla el bug de ''peso=1.01%''); region/granja/modulo/sublote pasan a chips encima; colspan del empty-state 28->23; quita la ficha FCR de conversion. REQ-002a/m/n, REQ-010f.', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-17', DATE '2026-07-17', TIMESTAMPTZ '2026-07-17 12:00:00+00', TIMESTAMPTZ '2026-07-17 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-17 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0014-T5'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-10fd9dd');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-d7c6b53', 'BUG', 'LISTO', 'ALTA',
           'fix(inventario): scoping de catalogo por empresa/pais efectivos + seed Panama', 'Bug detectado y corregido durante «Plan — Inventario Gestión: scoping multi-empresa / multi-país consistente + ítems de Panamá».
Commit d7c6b53 (2026-07-17).
Causa/detalle registrado en el commit: ItemInventarioService resolvia la empresa por _current.CompanyId directo y, si no resolvia empresa, devolvia TODO el catalogo (fuga: en Panama mostraba los items de Ecuador). Ahora resuelve la empresa efectiva por nombre del header X-Active-Company (mismo criterio que las granjas) y falla cerrado (vacio) si no hay empresa; lecturas/escrituras acotadas a empresa+pais efectivos (fallback company_pai', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-17', DATE '2026-07-17', TIMESTAMPTZ '2026-07-17 12:00:00+00', TIMESTAMPTZ '2026-07-17 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-17 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0007-T13'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-d7c6b53');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-c23d9bc', 'BUG', 'LISTO', 'ALTA',
           'fix(postura): migracion de datos Verenice Fase 0 (bloques 1-3) para auto-aplicar en deploy', 'Bug detectado y corregido durante «Plan — Módulo """"Implementación"""" (cronogramas de entrega por empresa con checklist confirmable)».
Commit c23d9bc (2026-07-20).
Causa/detalle registrado en el commit: Convierte los bloques 1-3 del data-fix fix_datos_postura_verenice_jul26.sql en migracion EF 20260720211748_FixDatosPosturaVereniceBloques1a3 para que la correccion corra sola en el deploy sobre la RDS prod (Database__RunMigrations=true), versionada y auditable, en vez de ejecucion manual del DBA. - Idempotente (guardas WHERE) -> re-aplicar es no-op. Sin cambios de schema (no toca snapshot). - Cor', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-20', DATE '2026-07-20', TIMESTAMPTZ '2026-07-20 12:00:00+00', TIMESTAMPTZ '2026-07-20 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-20 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0002-T1'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-c23d9bc');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-dba28e9', 'BUG', 'LISTO', 'ALTA',
           'fix(seguridad): rate limiting menos agresivo por IP y desbloqueo de cuenta que resetea intentos', 'Bug detectado y corregido durante «Plan — Módulo Implementación (checklists) v2: rediseño + firmas de participantes».
Commit dba28e9 (2026-07-21).
Causa/detalle registrado en el commit: Middleware/appsettings: limite auth 5 -> 15 req/min por IP (oficinas/granjas comparten IP NAT) y bloqueo de IP 10 -> 3 min; config RateLimiting externalizada. - UserService: al desbloquear manualmente (IsLocked=false) se resetea FailedAttempts y LockedAt para que el primer fallo siguiente no re-bloquee. - Logica pura extraida a RateLimitingCalculos + tests.', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-21', DATE '2026-07-21', TIMESTAMPTZ '2026-07-21 12:00:00+00', TIMESTAMPTZ '2026-07-21 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-21 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0002-T2'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-dba28e9');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-c4755b9', 'BUG', 'LISTO', 'ALTA',
           'fix(engorde): fecha mostraba un dia menos en lotes reproductora y seguimientos', 'Bug detectado y corregido durante «Plan — Módulo Implementación (checklists) v2: rediseño + firmas de participantes».
Commit c4755b9 (2026-07-21).
Causa/detalle registrado en el commit: Backend: anclar fechas a mediodia UTC con FechasPuras.AnclarMediodiaUtc al guardar (seguimiento aves engorde CO/EC, lote reproductora, seguimiento diario reproductora) y consultar por rango de dia completo en UTC (desde -12h / hasta +12h exclusivo). - Frontend: envio y display sin corrimiento de zona (mediodia UTC / extraccion literal YMD) en aves-engorde, engorde-comun (fecha.funcion, modales,', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-21', DATE '2026-07-21', TIMESTAMPTZ '2026-07-21 12:00:00+00', TIMESTAMPTZ '2026-07-21 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-21 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0002-T2'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-c4755b9');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-1c172df', 'BUG', 'LISTO', 'ALTA',
           'fix(inventario): consumo Colombia multi-empresa con clave tipada camino-1/2', 'Bug detectado y corregido durante «Fix — Consumo de inventario Colombia multi-empresa (error 400 """"no tiene equivalente"""")».
Commit 1c172df (2026-07-21).
Causa/detalle registrado en el commit: El descuento de inventario de lotes Colombia (levante/produccion/engorde) rechazaba con 400 ""no tiene equivalente"" los items del inventario nuevo de empresas distintas a Sanmarino (p.ej. Demo, item 208 ""Alimneto ERP""): ColombiaInventarioConsumoService validaba el camino 2 hardcodeado a company 1 y ademas el parser aplanaba itemInventarioEcuadorId/catalogItemId en un solo int, adivinando la tabla p', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-21', DATE '2026-07-21', TIMESTAMPTZ '2026-07-21 12:00:00+00', TIMESTAMPTZ '2026-07-21 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-21 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0007-T14'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-1c172df');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-0fcda75', 'BUG', 'LISTO', 'ALTA',
           'fix(engorde): el lote reproductora cierra solo con los 7 días confirmados (no por registro)', 'Bug detectado y corregido durante «Plan — Cierre del lote reproductora engorde por CONFIRMACIÓN (no por registro)».
Commit 0fcda75 (2026-07-22).
Causa/detalle registrado en el commit: El lote se cerraba al completar 7 registros, lo que deshabilitaba el botón Confirmar y dejaba los registros en Pendiente sin cruzar a pollo engorde (incluso el día 7 nunca era confirmable). Ahora el cierre depende exclusivamente de los 7 días CONFIRMADOS; mientras haya pendientes el lote sigue Vigente. El ""restante de aves"" hacia pollo engorde también se libera solo con los días confirmados. Cambi', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-22', DATE '2026-07-22', TIMESTAMPTZ '2026-07-22 12:00:00+00', TIMESTAMPTZ '2026-07-22 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0008-T8'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-0fcda75');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-944c9c6', 'BUG', 'LISTO', 'ALTA',
           'fix granjas y errorres de aplicacion', 'Bug detectado y corregido durante «Plan — Numeración de corrida por lote base + galpón (Panamá) en Lote Pollo Engorde».
Commit 944c9c6 (2026-07-22).', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-22', DATE '2026-07-22', TIMESTAMPTZ '2026-07-22 12:00:00+00', TIMESTAMPTZ '2026-07-22 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0012-T17'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-944c9c6');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-967e490', 'BUG', 'LISTO', 'ALTA',
           'fix(postura): el lote de producción hereda LoteId/LotePadreId al cerrar levante', 'Bug detectado y corregido durante «Plan — Seguimiento Diario Producción: heredar Lote padre al cerrar Levante (Postura)».
Commit 967e490 (2026-07-22).
Causa/detalle registrado en el commit: Al cerrar un lote de levante, CrearLoteProduccion no copiaba LoteId ni LotePadreId, dejando lote_postura_produccion.lote_id en NULL. Al guardar un seguimiento de producción, ProduccionService exige lote_id>0 y devolvía 400 (""no tiene LoteId asociado""). Ahora la producción hereda el Lote base del levante (que siempre lo tiene desde LoteService), igual que hace Levante. Incluye backfill idempotente', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-22', DATE '2026-07-22', TIMESTAMPTZ '2026-07-22 12:00:00+00', TIMESTAMPTZ '2026-07-22 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0014-T6'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-967e490');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-da3bf77', 'BUG', 'LISTO', 'ALTA',
           'fix(engorde): reabrir lote reproductora ahora sí persiste (habilita eliminar)', 'Bug detectado y corregido durante «Plan — """"Reabrir lote"""" reproductora engorde no persiste (confirma sin aplicar)».
Commit da3bf77 (2026-07-22).
Causa/detalle registrado en el commit: ReabrirAsync cargaba la entidad con un join AsNoTracking, que deja la consulta sin rastrear, por lo que SaveChanges no emitía UPDATE y `reabierto` nunca llegaba a la BD. El endpoint devolvía reabierto=true (mutado en memoria) y el front habilitaba eliminar, pero DeleteAsync releía el valor real (false) y bloqueaba: la reapertura ""se confirmaba sin aplicarse"". Ahora se valida la pertenencia con Any', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-22', DATE '2026-07-22', TIMESTAMPTZ '2026-07-22 12:00:00+00', TIMESTAMPTZ '2026-07-22 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0008-T7'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-da3bf77');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-4893032', 'BUG', 'LISTO', 'ALTA',
           'fix(engorde): migracion alinea nombres de lote pollo engorde Panama al lote base asignado', 'Bug detectado y corregido durante «Plan — Alinear nombres de Lote Pollo Engorde (Panamá) al lote base asignado».
Commit 4893032 (2026-07-22).
Causa/detalle registrado en el commit: Los lotes de Panama creados antes de la feature de corrida quedaron con nombre libre y numero_corrida NULL. Backfill idempotente (solo Sql, sin cambios de schema) que, solo para Panama con lote base + galpon y numero_corrida NULL, asigna corrida = MAX(company,base,galpon) + ROW_NUMBER() y reescribe lote_nombre = ''{base} - {n}'' (misma regla que ConstruirNombreCorrida/CreateAsync). No toca Ecuador/C', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-22', DATE '2026-07-22', TIMESTAMPTZ '2026-07-22 12:00:00+00', TIMESTAMPTZ '2026-07-22 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-22 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0005-T4'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-4893032');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-d509c93', 'BUG', 'LISTO', 'ALTA',
           'fix(migraciones): lotes elegibles reales en el paso 2 + selector opcional de reproductora', 'Bug detectado y corregido durante «Plan — Migraciones Masivas: línea Seguimiento Reproductora Engorde + alineación Seguimiento Pollo Engorde».
Commit d509c93 (2026-07-23).
Causa/detalle registrado en el commit: Fix ""0 lotes"": el paso 2 usaba el selector de lotes generico del filtro jerarquico (lotes base/postura, donde no existen los de engorde); ahora el lote se elige de GET /api/Migracion/elegibles del tipo seleccionado (engorde = lote_ave_engorde abiertos), refrescado por la cascada granja/nucleo/galpon - Selector ""Reproductora (opcional)"" para Seguimiento Reproductora Engorde: endpoint nuevo', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-23', DATE '2026-07-23', TIMESTAMPTZ '2026-07-23 12:00:00+00', TIMESTAMPTZ '2026-07-23 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-23 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0006-T10'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-d509c93');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-0cb8eec', 'BUG', 'LISTO', 'ALTA',
           'fix(ubicacion): fn_rekey_nucleo copia codigo/descripcion de bodega ERP al mover nucleo', 'Bug detectado y corregido durante «Plan — Limpieza seguimientos diarios Panamá (reproductora + pollo engorde) para re-carga masiva».
Commit 0cb8eec (2026-07-25).
Causa/detalle registrado en el commit: La migracion AddInfraErpAvicolaSantaReyes agrega codigo_bodega/descripcion_bodega a nucleos y el INSERT de lista explicita de fn_rekey_nucleo las perdia en silencio al mover un nucleo entre granjas. Se suman al INSERT/SELECT y la migracion 20260725210000 re-crea las 3 funciones de fn_mover_ubicacion.sql (antes aplicadas fuera de banda, sin migracion) con columnas defensivas IF NOT EXISTS para ser', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-25', DATE '2026-07-25', TIMESTAMPTZ '2026-07-25 12:00:00+00', TIMESTAMPTZ '2026-07-25 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-25 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0012-T21'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-0cb8eec');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-4691c49', 'BUG', 'LISTO', 'ALTA',
           'fix(front): eliminar artefacto $safeNavigationMigration de 25 templates HTML (93 reemplazos, TypeError en runtime)', 'Bug detectado y corregido durante «Plan — Diseño unificado de filtros «Selección de contexto» en TODOS los módulos».
Commit 4691c49 (2026-07-25).', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-25', DATE '2026-07-25', TIMESTAMPTZ '2026-07-25 12:00:00+00', TIMESTAMPTZ '2026-07-25 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-25 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0017-T4'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-4691c49');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-c5b74a4', 'BUG', 'LISTO', 'ALTA',
           'fix(santa-reyes): lotes del Excel van a lote_postura_base con raza/tipo_linea/fecha_encaset/descripcion_erp + limpieza SR y Demo (migracion 20260726030933)', 'Bug detectado y corregido durante «Fix: Seguimiento diario de producción falla con """"El lote postura producción no tiene LoteId asociado"""" (400)».
Commit c5b74a4 (2026-07-26).', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-26', DATE '2026-07-26', TIMESTAMPTZ '2026-07-26 12:00:00+00', TIMESTAMPTZ '2026-07-26 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-26 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0014-T7'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-c5b74a4');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-645535b', 'BUG', 'LISTO', 'ALTA',
           'fix(demo): volver a clasificacion de huevo clasica (flag off + limpieza de items, migracion 20260726035944)', 'Bug detectado y corregido durante «Fix: Seguimiento diario de producción falla con """"El lote postura producción no tiene LoteId asociado"""" (400)».
Commit 645535b (2026-07-26).', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-26', DATE '2026-07-26', TIMESTAMPTZ '2026-07-26 12:00:00+00', TIMESTAMPTZ '2026-07-26 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-26 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0014-T7'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-645535b');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-f783bf5', 'BUG', 'LISTO', 'ALTA',
           'fix(produccion): seguimiento diario 400 ""no tiene LoteId asociado"" — backfill desde levante (migracion 20260726052546) + self-heal fail-closed en ProduccionService', 'Bug detectado y corregido durante «Fix: Seguimiento diario de producción falla con """"El lote postura producción no tiene LoteId asociado"""" (400)».
Commit f783bf5 (2026-07-26).', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-26', DATE '2026-07-26', TIMESTAMPTZ '2026-07-26 12:00:00+00', TIMESTAMPTZ '2026-07-26 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-26 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0014-T7'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-f783bf5');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-9534528', 'BUG', 'LISTO', 'ALTA',
           'fix(seguridad): cierres del QA del alcance granular (gate admin, movimientos, mutaciones de lote)', 'Bug detectado y corregido durante «Plan — Alcance granular por usuario-granja (núcleo / galpón / lote o global)».
Commit 9534528 (2026-07-26).
Causa/detalle registrado en el commit: Fixes de la revision adversarial (fable) sobre d492eed: - A1: GET/PUT scope y locations-tree ahora exigen Admin de Empresa de la empresa de la granja (rol is_company_admin o admin/administrador) o Super Admin -> un usuario restringido ya no puede quitarse la restriccion por API ni leer arboles de otras empresas (403; fail-closed) - A2: Movimiento de Aves filtrado en TODAS las lecturas (GetAll,', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-26', DATE '2026-07-26', TIMESTAMPTZ '2026-07-26 12:00:00+00', TIMESTAMPTZ '2026-07-26 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-26 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0004-T5'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-9534528');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-14a8bfa', 'BUG', 'LISTO', 'ALTA',
           'fix(front): modal de alcance de granja colgado en ""Cargando..."" (Angular 22 = OnPush por defecto)', 'Bug detectado y corregido durante «El modal de alcance de granja quedaba colgado en «Cargando…» (Angular 22 = OnPush por defecto)».
Commit 14a8bfa (2026-07-26).
Causa/detalle registrado en el commit: En Angular 22 omitir `changeDetection` en @Component ya no equivale al viejo `Default`: el default del framework es OnPush (`Default` quedo deprecado como alias de `Eager`). Con OnPush, asignar campos desde un callback de HttpClient (`this.tree = tree; this.loading = false`) no marca la vista sucia -> Zone.js dispara el tick pero la vista se saltea y la plantilla nunca se repinta. Sintoma: el mod', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-26', DATE '2026-07-26', TIMESTAMPTZ '2026-07-26 12:00:00+00', TIMESTAMPTZ '2026-07-26 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-26 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'SES-20260726-64d2'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-14a8bfa');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-4616dfa', 'BUG', 'LISTO', 'ALTA',
           'fix(produccion): el seguimiento diario deja de cruzar empresas + la fn no cuenta filas borradas', 'Bug detectado y corregido durante «Plan — PWA offline-first con sincronización diferida».
Commit 4616dfa (2026-08-10).
Causa/detalle registrado en el commit: Continuacion de la PWA. Se retomo A5 (2a parte) verificando el plan contra la BD y el codigo de HOY —la regla que dejo la auditoria anterior— y esa verificacion destapo algo que el plan no menciona y pesa mas que los dos items que quedaban. SeguimientoProduccionService no filtraba por empresa en NINGUNO de sus seis metodos: GetAllAsync devolvia los seguimientos de todas, y Update/Delete resolvian', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-10', DATE '2026-08-10', TIMESTAMPTZ '2026-08-10 12:00:00+00', TIMESTAMPTZ '2026-08-10 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-10 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0019-T20'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-4616dfa');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-7639b79', 'BUG', 'LISTO', 'ALTA',
           'fix(engorde): validar la hora de encasetamiento contra los seguimientos ya cargados', 'Bug detectado y corregido durante «Plan — Hora de encasetamiento: define el primer día con registro (engorde y reproductora)».
Commit 7639b79 (2026-07-27).
Causa/detalle registrado en el commit: Los lotes de produccion se crearon SIN hora y muchos ya tienen seguimientos. Al informarles la hora despues, nada miraba esos registros: el PUT guardaba 200 OK y dejaba el lote en un estado que la propia regla nueva considera invalido. - EncasetamientoRetroactivoCalculos (puro, 9 tests): diagnostica si la hora es compatible con las fechas ya cargadas. Solo una hora tardia puede dejar registros', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-27', DATE '2026-07-27', TIMESTAMPTZ '2026-07-27 12:00:00+00', TIMESTAMPTZ '2026-07-27 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-27 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0012-T22'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-7639b79');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-528b283', 'BUG', 'LISTO', 'ALTA',
           'fix(migracion): los encabezados MIXTOS se validaban pero se leian en CERO', 'Bug detectado y corregido durante «Plan — Hora de encasetamiento: define el primer día con registro (engorde y reproductora)».
Commit 528b283 (2026-07-27).
Causa/detalle registrado en el commit: Bug propio, encontrado en el smoke contra el backend real: un Excel con los titulos mixtos (""Mort Mixta"", ""Consumo Mixto (kg)""...) daba ""Validado"" y al importar insertaba el dia con mortalidad 0 y consumo 0. Sin error y sin advertencia. Causa: habia DOS capas de matcheo de encabezados que podian desincronizarse. - La validacion de encabezados usa el ESQUEMA (titulo + alias) -> el titulo mixto se', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-27', DATE '2026-07-27', TIMESTAMPTZ '2026-07-27 12:00:00+00', TIMESTAMPTZ '2026-07-27 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-27 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0012-T22'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-528b283');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-769a48c', 'BUG', 'LISTO', 'ALTA',
           'fix(engorde): el seguimiento numeraba desde Edad 0 y pedia el peso un dia tarde', 'Bug detectado y corregido durante «Plan — Hora de encasetamiento: define el primer día con registro (engorde y reproductora)».
Commit 769a48c (2026-07-27).
Causa/detalle registrado en el commit: La tabla de seguimiento de pollo engorde pintaba crudo el edad_dia de fn_seguimiento_diario_engorde (fecha - fecha_encaset), asi que el dia del encasetamiento salia como ""Edad 0"". La regla de la hora de llegada, que ya estaba en reproductora desde 56edf3a, nunca se cableo en engorde. Ademas el pesaje obligatorio estaba escrito sobre esa edad 0-based (edad 1..7 o multiplo de 7). Como la semana de', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-27', DATE '2026-07-27', TIMESTAMPTZ '2026-07-27 12:00:00+00', TIMESTAMPTZ '2026-07-27 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-27 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0012-T22'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-769a48c');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-c30272c', 'BUG', 'LISTO', 'ALTA',
           'fix(ci): el frontend no compilaba en CI porque .dockerignore ocultaba build-version.js', 'Bug detectado y corregido durante «Fix — el deploy del frontend muere en el build de Docker (`MODULE_NOT_FOUND`)».
Commit c30272c (2026-07-27).
Causa/detalle registrado en el commit: El run 82085199647 murio en el paso 7 del job del frontend con ""Cannot find module ''/app/scripts/build-version.js''"". Causa: frontend/.dockerignore excluye scripts/* y solo dejaba pasar scripts/inject-version.js, archivo que 76a2903 borro al renombrarlo a build-version.js. El contexto de build llegaba con scripts/ vacio. El fallo era silencioso por tres motivos que vale la pena dejar escritos: CO', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-27', DATE '2026-07-27', TIMESTAMPTZ '2026-07-27 12:00:00+00', TIMESTAMPTZ '2026-07-27 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-27 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0019-T21'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-c30272c');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-2f46837', 'BUG', 'LISTO', 'ALTA',
           'fix(seguridad): la CSP bloqueaba el reCAPTCHA del login en produccion', 'Bug detectado y corregido durante «Fix — el reCAPTCHA de Google desapareció del login en producción».
Commit 2f46837 (2026-07-27).
Causa/detalle registrado en el commit: La CSP centralizada de la Fase 0.C empezo a aplicarse de verdad y no permitia los origenes de Google: script-src sin google/gstatic y frame-src heredando default-src ''self''. El widget no se renderizaba y el login se veia como en desarrollo, aunque el bundle desplegado SI era de produccion. - nginx-security-headers.conf: script-src + www.google.com/recaptcha/ y www.gstatic.com/recaptcha/; frame', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-27', DATE '2026-07-27', TIMESTAMPTZ '2026-07-27 12:00:00+00', TIMESTAMPTZ '2026-07-27 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-27 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0004-T6'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-2f46837');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-eb8c38f', 'BUG', 'LISTO', 'ALTA',
           'fix(engorde): las bajas de la primera semana descuentan aves y el saldo de alimento deja de inflarse', 'Bug detectado y corregido durante «Plan — Alimento (ingresos/traslados) en el MISMO archivo de carga masiva de seguimiento engorde».
Commit eb8c38f (2026-07-27).
Causa/detalle registrado en el commit: Validacion del lote 13-1 (galpon 6) sobre numeracion de dia, descuento de aves y cuadre de alimento. La numeracion esta bien: engorde va de edad 0 a 40 y pantalla muestra dia 1 a 41; reproductora, dia 1 a 7 en ambas. Los otros dos puntos tenian bugs. BAJAS DE LOS DIAS 1-7 QUE NO DESCONTABAN AVES Esos dias los inserta el trigger SQL del cruce de reproductora, sin pasar por el service, asi que su', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-27', DATE '2026-07-27', TIMESTAMPTZ '2026-07-27 12:00:00+00', TIMESTAMPTZ '2026-07-27 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-27 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0006-T12'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-eb8c38f');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-36a8bab', 'BUG', 'LISTO', 'ALTA',
           'fix(engorde): ventana de alimento previo al encaset - el reporte cierra contra el inventario', 'Bug detectado y corregido durante «Plan — Alimento (ingresos/traslados) en el MISMO archivo de carga masiva de seguimiento engorde».
Commit 36a8bab (2026-07-28).
Causa/detalle registrado en el commit: Tercer y ultimo factor del descuadre de alimento del lote 13-1. El saldo descartaba todos los movimientos anteriores a la fecha de encasetamiento -- puesto en may-2026 para que la apertura no heredara el sobrante del ciclo anterior del galpon -- pero en engorde el PREINICIADOR llega antes que los pollitos, asi que ese corte se comia alimento propio del lote. En el galpon 6 eran los 12.129,638 kg d', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-28', DATE '2026-07-28', TIMESTAMPTZ '2026-07-28 12:00:00+00', TIMESTAMPTZ '2026-07-28 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-28 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0006-T12'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-36a8bab');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-54ce0e1', 'BUG', 'LISTO', 'ALTA',
           'fix(engorde): el saldo proyectado contaba movimientos que la carga iba a omitir', 'Bug detectado y corregido durante «Plan — Alimento (ingresos/traslados) en el MISMO archivo de carga masiva de seguimiento engorde».
Commit 54ce0e1 (2026-07-28).
Causa/detalle registrado en el commit: Al validar el archivo con el lote ya cargado, el reporte anunciaba 4.470,664 kg de saldo cuando en el galpon hay 2.235,332. Importar no habria hecho eso -- la idempotencia omite los movimientos ya aplicados -- pero el numero era falso y asustaba. La simulacion sumaba todo lo del archivo sin mirar que iba a pasar de verdad con cada cosa. Ahora: - Los movimientos de la hoja Alimento que ya estan e', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-28', DATE '2026-07-28', TIMESTAMPTZ '2026-07-28 12:00:00+00', TIMESTAMPTZ '2026-07-28 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-28 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0006-T12'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-54ce0e1');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-bd472f0', 'BUG', 'LISTO', 'ALTA',
           'fix(postura): el Informe RA Pesadas ya no pierde la seleccion al cambiar de modo', 'Bug detectado y corregido durante «Plan — Informe RA Pesadas (Parámetros + Gráficos)».
Commit bd472f0 (2026-07-28).
Causa/detalle registrado en el commit: Los dos modos se OCULTAN en vez de destruirse. Con @if el componente se remontaba en cada cambio: el Resumen perdia el ano y la semana y el Detalle el lote base, asi que para volver a mirar el otro modo habia que reelegir todo. Las graficas siguen creandose solo cuando su propia vista esta activa -estan dentro de su propio @if-, asi que ocultar el contenedor no monta canvas invisibles. Verificad', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-28', DATE '2026-07-28', TIMESTAMPTZ '2026-07-28 12:00:00+00', TIMESTAMPTZ '2026-07-28 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-28 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0015-T8'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-bd472f0');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-a0cd08d', 'BUG', 'LISTO', 'ALTA',
           'fix(postura): el Detalle tomaba la fila de guia equivocada en la semana 25', 'Bug detectado y corregido durante «Plan — Informe RA Pesadas (Parámetros + Gráficos)».
Commit a0cd08d (2026-07-28).
Causa/detalle registrado en el commit: Encontrado por la validacion independiente sobre la granja real NIZA III. CargarGuiaPorSemanaAsync consultaba sin ORDER BY y se quedaba con la primera fila que llegara. ParseEdadSemana(''25P'') devuelve 25, asi que en la semana 25 ganaba ''25P''. Las dos etapas comparten ese loader pero necesitan la preferencia INVERTIDA: - levante debe usar ''25'' (cierre de levante, retiro acumulado 4,03) - producc', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-28', DATE '2026-07-28', TIMESTAMPTZ '2026-07-28 12:00:00+00', TIMESTAMPTZ '2026-07-28 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-28 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0015-T8'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-a0cd08d');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-1661e2d', 'BUG', 'LISTO', 'ALTA',
           'fix(postura): los %% semanales de levante usan el denominador del archivo oficial', 'Bug detectado y corregido durante «Plan — Informe RA Pesadas (Parámetros + Gráficos)».
Commit 1661e2d (2026-07-28).
Causa/detalle registrado en el commit: El Detalle dividia mortalidad, descarte y error de sexaje por la base FIJA de aves iniciales. Antes de cambiarlo se despejo la regla real contrastando fila a fila el archivo fuente sobre los 73 lotes: %Mort H/M -> saldo al INICIO de la semana (1401 H + 1311 M lo confirman; ninguna fila cuadra con el final ni con la base fija) %Sel H/M -> saldo al FINAL de la se', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-28', DATE '2026-07-28', TIMESTAMPTZ '2026-07-28 12:00:00+00', TIMESTAMPTZ '2026-07-28 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-28 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0015-T8'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-1661e2d');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-f359290', 'BUG', 'LISTO', 'ALTA',
           'fix(postura): la carga masiva de produccion fusiona el dia del cierre del levante', 'Bug detectado y corregido durante «Plan — Carga masiva de Postura (Levante + Producción): alimento con inventario real, huevos completos y validaciones a…».
Commit f359290 (2026-07-28).
Causa/detalle registrado en el commit: Encontrado al ejercitar el ciclo completo (crear lote -> carga masiva de levante -> liquidar y cerrar -> carga masiva de produccion) sobre un lote de prueba real. El cierre del levante crea una fila de produccion con los huevos arrastrados y nada mas. Cuando el Excel de produccion traia ESE MISMO dia -- que es el caso normal, porque es el primer dia de produccion -- la carga lo contaba como ""ya c', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-28', DATE '2026-07-28', TIMESTAMPTZ '2026-07-28 12:00:00+00', TIMESTAMPTZ '2026-07-28 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-28 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0006-T13'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-f359290');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-21e53ab', 'BUG', 'LISTO', 'ALTA',
           'fix(engorde): cuadre de aves y alimento en pollo engorde (Panama)', 'Bug detectado y corregido durante «Plan — Cuadre de aves y alimento en pollo engorde (Panamá)».
Commit 21e53ab (2026-07-29).
Causa/detalle registrado en el commit: AVES — «Aves disponibles» descontaba dos veces las bajas del seguimiento. GetAvesDisponiblesAsync partia de lote_ave_engorde.hembras_l/machos_l —el maestro, que RetiroAvesEngordeAplicador YA descuenta— y le volvia a restar la mortalidad, seleccion y error de sexaje acumulados. La formula era correcta cuando el maestro solo bajaba por ventas; con el descuento automatico quedo contando doble. El m', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-29', DATE '2026-07-29', TIMESTAMPTZ '2026-07-29 12:00:00+00', TIMESTAMPTZ '2026-07-29 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-29 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0012-T26'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-21e53ab');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-2cc4855', 'BUG', 'LISTO', 'ALTA',
           'fix(engorde): cuadre de datos de alimento en Panama (G0486 y DAYLAND)', 'Bug detectado y corregido durante «Plan — Cuadre de aves y alimento en pollo engorde (Panamá)».
Commit 2cc4855 (2026-07-29).
Causa/detalle registrado en el commit: Segunda parte del cuadre, con las decisiones tomadas por el usuario sobre los 17 galpones que seguian descuadrados despues del fix de scope. G0486 (MENDOZA) — ingresos de G0485 cargados encima. La carga masiva del 28/07 corrio dos veces (20:53 y 20:56). Solo G0486 recibio filas de las DOS pasadas, y la segunda —18 filas, 128.302,2 kg— es identica en cantidad de filas y en kilos al total de G0485', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-29', DATE '2026-07-29', TIMESTAMPTZ '2026-07-29 12:00:00+00', TIMESTAMPTZ '2026-07-29 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-29 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0012-T26'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-2cc4855');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-2f58e22', 'BUG', 'LISTO', 'ALTA',
           'fix(engorde): el saldo correcto es el logico, y el inventario era el que estaba mal', 'Bug detectado y corregido durante «Plan — Cuadre de aves y alimento en pollo engorde (Panamá)».
Commit 2f58e22 (2026-07-29).
Causa/detalle registrado en el commit: Reescritura de la Fase 3 del cuadre de Panama tras encontrar la causa raiz real al analizar los 12 galpones que quedaban con diferencia chica. CAUSA RAIZ: el inventario nunca descontó el consumo de los 7 dias del CRUCE de reproductora. Esos dias los escribe fn_cruce_reproductora_a_engorde por SQL directo, sin pasar por el service — el mismo bug que ya se habia corregido para las aves. Verificado', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-29', DATE '2026-07-29', TIMESTAMPTZ '2026-07-29 12:00:00+00', TIMESTAMPTZ '2026-07-29 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-29 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0012-T26'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-2f58e22');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-a050ec7', 'BUG', 'LISTO', 'ALTA',
           'fix(engorde): el Reporte de Costos mostraba una fraccion del stock de alimento', 'Bug detectado y corregido durante «Plan — Cuadre de aves y alimento en pollo engorde (Panamá)».
Commit a050ec7 (2026-07-29).
Causa/detalle registrado en el commit: fn_reporte_diario_costos_engorde v2: el stock_kg por alimento se DERIVA de ingresos - consumo, en vez de leer el saldo_final del snapshot jsonb historico_consumo_alimento. El snapshot solo existe para los alimentos que se consumieron ESE dia, asi que el reporte mostraba una fraccion del stock real y ademas no se movia cuando el saldo se recalculaba. Caso testigo G0464 (DAYLAND) al 22/07: el repor', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-29', DATE '2026-07-29', TIMESTAMPTZ '2026-07-29 12:00:00+00', TIMESTAMPTZ '2026-07-29 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-29 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0012-T26'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-a050ec7');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-9a753ea', 'BUG', 'LISTO', 'ALTA',
           'fix(engorde): el Reporte de Costos cruza inventario y seguimiento, no el jsonb', 'Bug detectado y corregido durante «Plan — Cuadre de aves y alimento en pollo engorde (Panamá)».
Commit 9a753ea (2026-07-29).
Causa/detalle registrado en el commit: El stock y el consumo por tipo de alimento salian del snapshot jsonb historico_consumo_alimento. Ese snapshot esta INCOMPLETO —suma 1.554.181,4 kg contra los 1.706.089,8 kg de consumo real del seguimiento— y su saldo_final solo existe para los alimentos consumidos ESE dia, asi que el stock mostrado era una fraccion del real y no se movia al recalcularse el saldo. Caso testigo G0464 (DAYLAND) al 22', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-29', DATE '2026-07-29', TIMESTAMPTZ '2026-07-29 12:00:00+00', TIMESTAMPTZ '2026-07-29 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-29 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0012-T26'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-9a753ea');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-e2a8a3d', 'BUG', 'LISTO', 'ALTA',
           'fix(engorde): la apertura de alimento deja de heredar el ciclo anterior del galpon', 'Bug detectado y corregido durante «Plan — Congelar la liquidación de un lote de pollo engorde».
Commit e2a8a3d (2026-07-30).
Causa/detalle registrado en el commit: La ventana de alimento previo al encaset (v9, 36a8bab) retrocede 10 dias y en Ecuador, donde cada galpon encadena 3-4 ciclos sucesivos, cae dentro del ciclo anterior justo cuando se vacia su bodega. Como el filtro de devoluciones descarta las entradas pero conserva los traslados de salida, la apertura salia negativa y corria todas las filas. Kilometro 22 / G0036 / lote 2603: apertura -7.960 kg, l', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-30', DATE '2026-07-30', TIMESTAMPTZ '2026-07-30 12:00:00+00', TIMESTAMPTZ '2026-07-30 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-30 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0008-T10'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-e2a8a3d');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-a396d1f', 'BUG', 'LISTO', 'ALTA',
           'fix(engorde): el saldo de alimento se refresca al mover el inventario', 'Bug detectado y corregido durante «Plan — Congelar la liquidación de un lote de pollo engorde».
Commit a396d1f (2026-07-30).
Causa/detalle registrado en el commit: Hasta ahora RecalcularSaldoAlimentoPorLoteAsync solo corria al crear o editar un seguimiento diario, asi que un ingreso o traslado registrado despues nunca actualizaba seguimiento_diario_aves_engorde.saldo_alimento_kg. La grilla no se veia afectada porque recalcula en vivo, pero la columna alimenta la liquidacion y Cuadrar Saldos: Kilometro 61 G0037 mostraba 2.360 kg contra 12.360 de stock real.', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-30', DATE '2026-07-30', TIMESTAMPTZ '2026-07-30 12:00:00+00', TIMESTAMPTZ '2026-07-30 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-30 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0008-T10'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-a396d1f');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-e749ed0', 'BUG', 'LISTO', 'ALTA',
           'fix(engorde): la apertura tampoco retrocede mas alla del fin del ciclo anterior (v12)', 'Bug detectado y corregido durante «Plan — Congelar la liquidación de un lote de pollo engorde».
Commit e749ed0 (2026-07-30).
Causa/detalle registrado en el commit: Ticket de operacion: hay seguimientos diarios de pollo engorde en Ecuador con saldo de alimento en negativo. Medido sobre el dump: hoy en produccion son 330 filas en 27 lotes (-1.175.479 kg), con 95 de esas filas en las corridas ACTIVAS 2603 y 2604. No hay aves ni consumos negativos: es solo el saldo de alimento. La v11 tapaba solo la mitad del agujero. lote_ave_engorde_id lo pone el trigger con', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-30', DATE '2026-07-30', TIMESTAMPTZ '2026-07-30 12:00:00+00', TIMESTAMPTZ '2026-07-30 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-30 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0008-T10'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-e749ed0');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-3c4d3d0', 'BUG', 'LISTO', 'ALTA',
           'fix(inventario): anular un movimiento tambien anula su fila del historico', 'Bug detectado y corregido durante «Plan — Congelar la liquidación de un lote de pollo engorde».
Commit 3c4d3d0 (2026-07-30).
Causa/detalle registrado en el commit: El trigger que llena lote_registro_historico_unificado es solo AFTER INSERT, asi que ningun UPDATE ni DELETE del movimiento se propaga. EliminarIngresoAsync y EliminarTrasladoAsync ya marcaban anulado=true a mano; estos dos caminos no lo hacian: - AnularMovimientoHistoricoAsync borraba el movimiento y dejaba la fila del historico huerfana, de modo que el saldo de alimento seguia contando un ing', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-30', DATE '2026-07-30', TIMESTAMPTZ '2026-07-30 12:00:00+00', TIMESTAMPTZ '2026-07-30 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-30 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0008-T10'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-3c4d3d0');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-b64898f', 'BUG', 'LISTO', 'ALTA',
           'fix(traslados): fechas puras a mediodia y match de fila diaria por dia calendario', 'Bug detectado y corregido durante «Plan — Carga masiva Seguimiento Diario Levante: movimientos de aves + tab huevos fijo + ocultar estructura».
Commit b64898f (2026-07-31).
Causa/detalle registrado en el commit: TrasladoAvesDesdeSegService escribia Fecha/FechaMovimiento a MEDIANOCHE: con Npgsql legacy el instante se relee corrido al dia ANTERIOR (Bogota) y rompia toda comparacion por dia calendario (la idempotencia de la carga masiva duplicaba movimientos; mismo bug ya corregido en 3453b09 para MigracionService). - Fecha de filas nuevas de seguimiento (levante/produccion), FechaTraslado y FechaMovimien', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-07-31', DATE '2026-07-31', TIMESTAMPTZ '2026-07-31 12:00:00+00', TIMESTAMPTZ '2026-07-31 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-07-31 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0006-T14'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-b64898f');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-f6ac8c7', 'BUG', 'LISTO', 'ALTA',
           'fix(reporte-contable): seccion Movimientos de Huevos lee seguimiento_diario_produccion ademas de la legacy', 'Bug detectado y corregido durante «Plan — Seguimiento Diario de PRODUCCIÓN: fn SQL canónica + reducción de services + invariantes».
Commit f6ac8c7 (2026-08-01).
Causa/detalle registrado en el commit: La seccion leia solo seguimiento_diario (tipo produccion), que tiene 0 filas de produccion en prod desde la Fase 3 => salia siempre vacia o con 400. Ahora une ambas fuentes con el criterio canonico de fn_indicadores_produccion_postura (por lote+dia calendario Bogota gana el timestamp mas temprano; empate exacto => legacy) via calculo puro ReporteContableHuevosCalculos con 12 tests. Alcance ampliad', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-01', DATE '2026-08-01', TIMESTAMPTZ '2026-08-01 12:00:00+00', TIMESTAMPTZ '2026-08-01 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-01 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0014-T11'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-f6ac8c7');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-116e052', 'BUG', 'LISTO', 'ALTA',
           'fix(gastos-inventario): el reporte deja de exportar consumos eliminados + hoja de existencias completa', 'Bug detectado y corregido durante «Plan — Gastos de inventario: reporte sin eliminados + hoja de existencias completas».
Commit 116e052 (2026-08-05).
Causa/detalle registrado en el commit: Novedad del usuario final (Ecuador, modulo transversal): el Excel del modulo traia tambien los consumos eliminados y solo listaba las referencias que tuvieron consumo. Auditoria previa (BD local, dump tipo-prod): - El retorno a inventario al eliminar SI funciona: 38/38 gastos eliminados con su devolucion, 0 sin devolucion, 0 lineas y 0 cantidades descuadradas. - ExportAsync no filtraba estado (', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-05', DATE '2026-08-05', TIMESTAMPTZ '2026-08-05 12:00:00+00', TIMESTAMPTZ '2026-08-05 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-05 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0007-T18'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-116e052');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-3998aa2', 'BUG', 'LISTO', 'ALTA',
           'fix(pollo-engorde): la venta contaba dos veces las bajas ya aplicadas al maestro', 'Bug detectado y corregido durante «Fix — «Aves disponibles» difiere entre Seguimiento diario y Venta (pollo engorde)».
Commit 3998aa2 (2026-08-05).
Causa/detalle registrado en el commit: Ticket de operación: en CAROLINA G4 lote 2603 el seguimiento diario reportaba 40 aves disponibles y la pantalla de venta 33, impidiendo despachar. La hipótesis del reporte —que el seguimiento sumaba las 7 aves del lote 2601 cerrado del mismo galpon— es falsa: ningún cálculo cruza lotes y los dos «7» son una coincidencia numérica. El 7 del lote 2601 es su saldo real; el que explica la diferencia so', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-05', DATE '2026-08-05', TIMESTAMPTZ '2026-08-05 12:00:00+00', TIMESTAMPTZ '2026-08-05 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-05 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0010-T12'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-3998aa2');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-75f7980', 'BUG', 'LISTO', 'ALTA',
           'fix(pollo-engorde): corregir el maestro de aves desalineado + detector del invariante', 'Bug detectado y corregido durante «Fix — «Aves disponibles» difiere entre Seguimiento diario y Venta (pollo engorde)».
Commit 75f7980 (2026-08-05).
Causa/detalle registrado en el commit: Segunda parte del ticket 05-ago-2026: además de la fórmula (commit 3998aa2), corregir los DATOS y evitar que vuelva a pasar en otros lotes. 🔴 La identidad obvia habria roto 8 lotes. Auditar con `maestro = encaset - ventas - bajas_aplicadas` marcaba 9 lotes de Ecuador, pero 8 de ellos tienen hembras_l == bajas_h y machos_l == bajas_m a PROPOSITO: son los lotes «2601» de correccion_aves_disponibles', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-05', DATE '2026-08-05', TIMESTAMPTZ '2026-08-05 12:00:00+00', TIMESTAMPTZ '2026-08-05 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-05 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0010-T12'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-75f7980');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-b9cab63', 'BUG', 'LISTO', 'ALTA',
           'fix(pollo-engorde): el baseline de las bajas sale de la fila del historico, no del registro', 'Bug detectado y corregido durante «Fix — «Aves disponibles» difiere entre Seguimiento diario y Venta (pollo engorde)».
Commit b9cab63 (2026-08-05).
Causa/detalle registrado en el commit: Borrar o editar un seguimiento de la cohorte anterior al aplicador (< 2026-07-27 17:58) acreditaba al maestro aves que nunca se habian debitado, y lo hacia en silencio: sin fila anulada, sin updated_at y sin auditoria. Es el origen de las 17 aves de mas del lote 107 (Kilometro 61 / G0037 / 2604), cuyo dia 2026-07-24 se borro y recreo tres veces. La unica prueba de que un dia descontó es su fila v', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-05', DATE '2026-08-05', TIMESTAMPTZ '2026-08-05 12:00:00+00', TIMESTAMPTZ '2026-08-05 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-05 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0010-T12'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-b9cab63');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-d341223', 'BUG', 'LISTO', 'ALTA',
           'fix(pollo-engorde): migracion que corrige la referencia Inicio del historial y el encaset', 'Bug detectado y corregido durante «Plan — Migrar el envío de correo a Microsoft Graph API (retiro de auth básica SMTP)».
Commit d341223 (2026-08-05).
Causa/detalle registrado en el commit: Los 4 lotes cuyo historial Inicio no coincide con aves_encasetadas quedaban FUERA de toda auditoria: fn_cuadre_aves_engorde los marca referencia_confiable=false y no opina sobre ellos. Son dos causas opuestas y se corrigen en sentidos contrarios, cada una con su evidencia registrada. Bloque 1 - el Inicio es plantilla de la carga inicial (lotes 5 y 7): seis lotes recibieron el mismo 25.000 H / 25.', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-05', DATE '2026-08-05', TIMESTAMPTZ '2026-08-05 12:00:00+00', TIMESTAMPTZ '2026-08-05 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-05 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0018-T2'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-d341223');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-c7b6834', 'BUG', 'LISTO', 'ALTA',
           'fix(correo): migrar el envio a Microsoft Graph API (Office 365 retiro la auth basica de SMTP)', 'Bug detectado y corregido durante «Plan — Migrar el envío de correo a Microsoft Graph API (retiro de auth básica SMTP)».
Commit c7b6834 (2026-08-05).
Causa/detalle registrado en el commit: Produccion no enviaba correos: Exchange Online retiro la autenticacion basica para SMTP Client Submission (rechazo desde el 01-mar-2026, refuerzo total el 30-abr-2026), que responde ""550 5.7.30 Basic authentication is not supported for Client Submission"". No se arregla cambiando la contrasena ni con una App Password: el mecanismo fue eliminado. System.Net.Mail.SmtpClient no soporta XOAUTH2, asi q', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-05', DATE '2026-08-05', TIMESTAMPTZ '2026-08-05 12:00:00+00', TIMESTAMPTZ '2026-08-05 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-05 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0018-T2'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-c7b6834');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-587d6cc', 'BUG', 'LISTO', 'ALTA',
           'fix(correo): corregir el diagnostico — el rechazo es una politica del tenant, no el codigo', 'Bug detectado y corregido durante «Plan — Migrar el envío de correo a Microsoft Graph API (retiro de auth básica SMTP)».
Commit 587d6cc (2026-08-05).
Causa/detalle registrado en el commit: Con la BD local ya sincronizada con produccion aparecio el error real que guarda email_queue (id 112, 05-ago-2026): ""530 5.7.57 Client not authenticated"" + ""535 5.7.139 ... did not meet the criteria to be authenticated successfully. Contact your administrator"". NO es ""550 5.7.30 Basic authentication is not supported"": el retiro global de la auth basica que motivo el commit anterior NO era la causa', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-05', DATE '2026-08-05', TIMESTAMPTZ '2026-08-05 12:00:00+00', TIMESTAMPTZ '2026-08-05 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-05 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0018-T2'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-587d6cc');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-2cab258', 'BUG', 'LISTO', 'ALTA',
           'fix(gastos-inventario): 10 lineas de gasto guardaron un tipo_item en la columna concepto', 'Bug detectado y corregido durante «Plan — Migrar el envío de correo a Microsoft Graph API (retiro de auth básica SMTP)».
Commit 2cab258 (2026-08-05).
Causa/detalle registrado en el commit: 10 filas de inventario_gasto_detalle quedaron con concepto ''insumo'' sobre el item 57 (AV0351 · AV. LIV 52 PROTEC 5 LTR, empresa 3), cuyo catalogo dice ''Otros insumos''. El desplegable de Concepto se arma desde el catalogo pero el filtro compara con igualdad EXACTA sobre el snapshot: como ninguna opcion ofrece ''insumo'', esas lineas eran infiltrables y salian con una etiqueta distinta a la de su item', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-05', DATE '2026-08-05', TIMESTAMPTZ '2026-08-05 12:00:00+00', TIMESTAMPTZ '2026-08-05 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-05 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0018-T2'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-2cab258');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-d50cd9c', 'BUG', 'LISTO', 'ALTA',
           'fix(inventario): alinear lote_id a integer — el traslado MOV-* decia ""Completado"" sin mover aves', 'Bug detectado y corregido durante «Plan — Traslado de aves: destino cross-granja/galpón en Engorde + fecha de registro visible».
Commit d50cd9c (2026-08-06).
Causa/detalle registrado en el commit: `inventario_aves.lote_id` e `historial_inventario.lote_id` eran `character varying` en la base mientras `InventarioAves.LoteId` e `HistorialInventario.LoteId` son `int`. Toda consulta que los comparara moria con `42883: operator does not exist: character varying = integer`. El efecto era peor que un error visible: `ProcesarMovimientoAsync` guarda `Estado = ""Completado""` y hace SaveChanges ANTES d', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-06', DATE '2026-08-06', TIMESTAMPTZ '2026-08-06 12:00:00+00', TIMESTAMPTZ '2026-08-06 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-06 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0010-T13'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-d50cd9c');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-2a35d63', 'BUG', 'LISTO', 'ALTA',
           'fix(seguimiento-levante): el tercer alimento tumbaba el guardado del dia entero', 'Bug detectado y corregido durante «Plan — `tipo_alimento` desborda `varchar(100)` y tumba el guardado del seguimiento diario».
Commit 2a35d63 (2026-08-06).
Causa/detalle registrado en el commit: Reportado como ""falla al guardar el lote A374A"" en Sanmarino Colombia: el toast mostraba ""An error occurred while saving the entity changes"" y al reabrir la pantalla no habia nada. No era el lote. El formulario arma tipo_alimento concatenando los nombres de los alimentos del dia (""H: ... / M: ... / G: ..."") y no limita cuantos se agregan, pero la columna era varchar(100). Con los nombres de repro', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-06', DATE '2026-08-06', TIMESTAMPTZ '2026-08-06 12:00:00+00', TIMESTAMPTZ '2026-08-06 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-06 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0012-T33'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-2a35d63');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-92e1cb5', 'BUG', 'LISTO', 'ALTA',
           'fix(seguimiento): engorde tambien acepta la lista larga de alimentos', 'Bug detectado y corregido durante «Plan — `tipo_alimento` desborda `varchar(100)` y tumba el guardado del seguimiento diario».
Commit 92e1cb5 (2026-08-06).
Causa/detalle registrado en el commit: Completa el fix del tercer alimento: en la 1a ronda engorde quedo en varchar(100) porque Postgres rechaza el ALTER cuando una vista depende de la columna (0A000), y de seguimiento_diario_aves_engorde.tipo_alimento cuelgan las 3 vistas de Power BI. Ahora las cuatro tablas de seguimiento comparten un unico tope. - migracion AmpliarTipoAlimentoEngorde: amplia las 3 tablas de engorde recreando las', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-06', DATE '2026-08-06', TIMESTAMPTZ '2026-08-06 12:00:00+00', TIMESTAMPTZ '2026-08-06 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-06 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0012-T33'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-92e1cb5');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-2ac57a8', 'BUG', 'LISTO', 'ALTA',
           'fix(postura): el reporte de levante contaba 614 aves de mas y editar el lote borraba las bajas', 'Bug detectado y corregido durante «Plan — Archivos de carga masiva del lote S-369AB (postura: levante + producción + alimento)».
Commit 2ac57a8 (2026-08-06).
Causa/detalle registrado en el commit: Tres defectos que salieron al cargar el historico del lote S-369 y que no son de ese lote: le pasan a cualquier empresa. 1. El reporte tecnico de levante no descontaba el error de sexaje del saldo, ni los traslados de aves. En un lote de 20.458 hembras cerraba en 19.632 contra las 19.018 reales del maestro y del informe, y como el gr/ave/dia divide por ese saldo, la semana 24 daba 109,81 en', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-06', DATE '2026-08-06', TIMESTAMPTZ '2026-08-06 12:00:00+00', TIMESTAMPTZ '2026-08-06 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-06 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0006-T15'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-2ac57a8');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-2d26fae', 'BUG', 'LISTO', 'ALTA',
           'fix(produccion): el saldo de aves no veia las ventas, ni los traslados, ni la seleccion de machos', 'Bug detectado y corregido durante «Plan — Archivos de carga masiva del lote S-369AB (postura: levante + producción + alimento)».
Commit 2d26fae (2026-08-06).
Causa/detalle registrado en el commit: Una venta de produccion descontaba las aves y dejaba la auditoria, pero en la fila diaria solo escribia una nota de texto. El reporte reconstruye el saldo desde esas filas, asi que la venta quedaba fuera y cerraba por encima del real en exactamente el total vendido: +114 hembras en un sublote del S-369 y +224 en el otro. Se arregla por los dos lados, que son complementarios: La venta ahora deja', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-06', DATE '2026-08-06', TIMESTAMPTZ '2026-08-06 12:00:00+00', TIMESTAMPTZ '2026-08-06 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-06 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0006-T15'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-2d26fae');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-22f3be2', 'BUG', 'LISTO', 'ALTA',
           'fix(produccion): el gr/ave/dia divide por las aves al cierre, como el informe', 'Bug detectado y corregido durante «Plan — Archivos de carga masiva del lote S-369AB (postura: levante + producción + alimento)».
Commit 22f3be2 (2026-08-06).
Causa/detalle registrado en el commit: Produccion dividia por un censo de inicio reconstruido como fin + mortalidad + seleccion, asi que daba de menos justo en las semanas de mas bajas: hasta 1,08 g en el lote S-369. El informe tecnico divide por la columna ""No. Final de aves"" —el saldo de cierre— y el reporte de levante ya lo hacia asi, que es por lo que levante cuadraba y produccion no. Los cuatro sitios que calculaban el numero (le', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-06', DATE '2026-08-06', TIMESTAMPTZ '2026-08-06 12:00:00+00', TIMESTAMPTZ '2026-08-06 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-06 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0006-T15'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-22f3be2');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-b34e629', 'BUG', 'LISTO', 'ALTA',
           'fix(produccion): cuatro reportes caidos que solo se veian con un lote cargado de verdad', 'Bug detectado y corregido durante «Plan — Archivos de carga masiva del lote S-369AB (postura: levante + producción + alimento)».
Commit b34e629 (2026-08-06).
Causa/detalle registrado en el commit: El consolidado de un lote padre con sublotes de fechas distintas cuadra: 480 celdas comparadas contra la suma de sus tabs sin una diferencia, y contra las hojas ""general"" del informe 24 de 24 semanas en levante y 22 de 23 en produccion. Al probar reporte por reporte aparecieron cuatro caidos, todos en produccion: El reporte principal daba 500 con ""Column ''PesoHuevo'' is null"". La entidad declarab', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-06', DATE '2026-08-06', TIMESTAMPTZ '2026-08-06 12:00:00+00', TIMESTAMPTZ '2026-08-06 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-06 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0006-T15'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-b34e629');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-91533a0', 'BUG', 'LISTO', 'ALTA',
           'fix(postura): la curva de levante devolvia 0 puntos y el traslado entre sublotes movia un solo lado', 'Bug detectado y corregido durante «Plan — Archivos de carga masiva del lote S-369AB (postura: levante + producción + alimento)».
Commit 91533a0 (2026-08-06).
Causa/detalle registrado en el commit: Cierra los tres pendientes que quedaban del ciclo S-369 y un cuarto bug que aparecio al arreglarlos. Los cuatro estaban vivos en produccion y ninguno era exclusivo de esta empresa. CURVA DE LEVANTE — 0 puntos en todas las empresas El commit de la curva (145348b) agrego el guard `p_sem_anio IS NULL OR (...)` a los DOS espejos .sql pero no genero migracion. La fn de produccion se redesplego despue', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-06', DATE '2026-08-06', TIMESTAMPTZ '2026-08-06 12:00:00+00', TIMESTAMPTZ '2026-08-06 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-06 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0006-T15'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-91533a0');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-5054da3', 'BUG', 'LISTO', 'ALTA',
           'fix(postura): un lote poblado por traslado reportaba saldo negativo o al doble', 'Bug detectado y corregido durante «Plan — Archivos de carga masiva del lote S-369AB (postura: levante + producción + alimento)».
Commit 5054da3 (2026-08-06).
Causa/detalle registrado en el commit: Un lote sin aves encasetadas es legitimo: hay lotes que reciben aves de otros lotes y nunca tuvieron encaset propio. El bug no era permitir esos lotes sino como el reporte les resuelve la base. Las tres fns de levante la resolvian con el MISMO fallback defectuoso: leian la fila de traslado mas antigua con LIMIT 1 y sacaban de ahi LOS DOS SEXOS. De ahi salian dos defectos independientes. SALDO NE', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-06', DATE '2026-08-06', TIMESTAMPTZ '2026-08-06 12:00:00+00', TIMESTAMPTZ '2026-08-06 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-06 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0006-T15'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-5054da3');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-b315612', 'BUG', 'LISTO', 'ALTA',
           'fix(postura): el saldo de levante no descontaba las ventas de aves', 'Bug detectado y corregido durante «Plan — Archivos de carga masiva del lote S-369AB (postura: levante + producción + alimento)».
Commit b315612 (2026-08-06).
Causa/detalle registrado en el commit: El mismo numero tenia dos implementaciones que discrepaban. El camino C# (ReporteTecnicoService sobre SaldoAvesLevanteCalculos) SI descuenta la venta y coincide con el informe; las dos fns SQL no la miraban. Para S-369B daban 1.281 machos donde el maestro y el informe dicen 991: los 290 de dos ventas de machos durante la crianza. La causa de fondo es que la fila diaria de levante solo guardaba ve', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-06', DATE '2026-08-06', TIMESTAMPTZ '2026-08-06 12:00:00+00', TIMESTAMPTZ '2026-08-06 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-06 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0006-T15'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-b315612');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-588dc94', 'BUG', 'LISTO', 'ALTA',
           'fix(tickets): el alta de una tarea aparecia dos veces en la linea de tiempo', 'Bug detectado y corregido durante «Plan — Tickets como CASOS tipo Jira: tareas, tablero, tiempos y solicitante delegado».
Commit 588dc94 (2026-08-06).
Causa/detalle registrado en el commit: Al verificar el modulo en pantalla, cada tarea creada generaba dos entradas seguidas: ""Cambio en las tareas · Tarea creada: TK-...-T1 · X"" y ""Tarea creada · TK-...-T1 · X"". Son la misma cosa contada por dos caminos: la nota de sistema que escribia el service y el evento que TicketTimelineCalculos ya deriva de la propia fila de ticket_tareas. Se quita la nota al crear y queda solo el evento deriva', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-06', DATE '2026-08-06', TIMESTAMPTZ '2026-08-06 12:00:00+00', TIMESTAMPTZ '2026-08-06 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-06 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0001-T12'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-588dc94');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-0ce0485', 'BUG', 'LISTO', 'ALTA',
           'fix(tickets): el tablero y el roadmap no le iban a aparecer a nadie en produccion', 'Bug detectado y corregido durante «Plan — Tickets como CASOS tipo Jira: tareas, tablero, tiempos y solicitante delegado».
Commit 0ce0485 (2026-08-06).
Causa/detalle registrado en el commit: Crear la fila en `menus` + `menu_permissions` no alcanza para que un menu se vea: `RoleCompositeService.Menus_GetForUserAsync` arma el arbol desde `role_menus` y solo cae al filtro por permisos cuando el rol no tiene NINGUN menu asignado — que no es el caso en prod. En local los dos menus nuevos figuraban con 0 roles: existian, respondian por URL directa, y no los veia nadie en el sidebar. Ademas', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-06', DATE '2026-08-06', TIMESTAMPTZ '2026-08-06 12:00:00+00', TIMESTAMPTZ '2026-08-06 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-06 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0001-T12'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-0ce0485');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-4f61046', 'BUG', 'LISTO', 'ALTA',
           'fix(tickets): el detalle sacaba scroll horizontal y desperdiciaba el ancho del monitor', 'Bug detectado y corregido durante «Plan — Tickets como CASOS tipo Jira: tareas, tablero, tiempos y solicitante delegado».
Commit 4f61046 (2026-08-07).
Causa/detalle registrado en el commit: Feedback sobre las pantallas: sobra espacio a los lados, todo va hacia abajo, y el chat queda debajo del caso cuando podria estar al lado. Nuevo ticket: el formulario pasa a dos columnas en pantallas grandes — el caso a la izquierda (titulo, tipo, resolutor, descripcion, prioridad) y a la derecha lo que lo acompaña (notificados, imagenes, adjuntos). ""A nombre de"" queda a lo ancho arriba porque su', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-07', DATE '2026-08-07', TIMESTAMPTZ '2026-08-07 12:00:00+00', TIMESTAMPTZ '2026-08-07 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-07 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0001-T12'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-4f61046');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-d9d45bb', 'BUG', 'LISTO', 'ALTA',
           'fix(postura): el espejo .sql de indicadores de produccion iba a tumbar una columna', 'Bug detectado y corregido durante «Plan — Reconciliar el espejo `.sql` de `fn_indicadores_produccion_postura` + `uniformidad_guia` NULL».
Commit d9d45bb (2026-08-07).
Causa/detalle registrado en el commit: El archivo backend/sql/fn_indicadores_produccion_postura.sql no coincidia con la funcion desplegada: le faltaba todo lo que agrego 20260806093256 (columna seleccion_machos, y ventas/retiros/traslados en el saldo). Correrlo dejaba la fn en 68 columnas en vez de 69 y rompia IndicadorProduccionSemanalBdRow.SeleccionMachos en runtime. Como ese archivo esta justamente para correrse, era cuestion de tie', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-07', DATE '2026-08-07', TIMESTAMPTZ '2026-08-07 12:00:00+00', TIMESTAMPTZ '2026-08-07 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-07 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0014-T13'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-d9d45bb');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-6be9031', 'BUG', 'LISTO', 'ALTA',
           'fix(postura): la seleccion de machos se calculaba y se descartaba antes del front', 'Bug detectado y corregido durante «Plan — Reconciliar el espejo `.sql` de `fn_indicadores_produccion_postura` + `uniformidad_guia` NULL».
Commit 6be9031 (2026-08-07).
Causa/detalle registrado en el commit: La fn emite `seleccion_machos` desde 20260806093256 y el BdRow la materializa, pero `IndicadorProduccionSemanalDto` no tenia el campo y `MapRow` no lo mapeaba: el valor moria en el backend y la respuesta JSON ni siquiera traia la clave. Solo se expone la columna; la aritmetica ya estaba bien y no se toca. Verificado contra la fn desplegada (pg_get_functiondef, no el espejo .sql): el saldo de mach', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-07', DATE '2026-08-07', TIMESTAMPTZ '2026-08-07 12:00:00+00', TIMESTAMPTZ '2026-08-07 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-07 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'HIS-2026-0014-T13'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-6be9031');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-92cd918', 'BUG', 'LISTO', 'ALTA',
           'fix(reportes): el Reporte Contable de postura solo veia 20 movimientos de bultos', 'Bug detectado y corregido durante «Reporte Diario Área de Costos para POSTURA (levante + producción) — Sanmarino».
Commit 92cd918 (2026-08-08).
Causa/detalle registrado en el commit: ObtenerDatosBultosAsync pedia PageSize=10000 a IFarmInventoryMovementService .GetPagedAsync, que clampa a 20 cualquier pedido mayor a 200 y ordena por created_at DESC. Peor: el filtro por type_item=''alimento'' corria en memoria DESPUES de paginar, asi que un movimiento de vacunas consumia cupo del kardex de alimento. Medido en la granja 5 (lote 13 K345A): el reporte veia 5 de los 58 movimientos de', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-08', DATE '2026-08-08', TIMESTAMPTZ '2026-08-08 12:00:00+00', TIMESTAMPTZ '2026-08-08 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-08 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'SES-20260807-8e56'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-92cd918');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-8d5565c', 'BUG', 'LISTO', 'ALTA',
           'fix(inventario,reportes): el tipo de item sale de la columna y paginar de mas ya no devuelve 20', 'Bug detectado y corregido durante «Reporte Diario Área de Costos para POSTURA (levante + producción) — Sanmarino».
Commit 8d5565c (2026-08-08).
Causa/detalle registrado en el commit: Dos defectos que se tapaban entre si. El segundo es el FACTOR que produjo al primero y seguia vivo en otro modulo. 1) El criterio ""esto es alimento"" El Reporte Contable decidia mirando catalogo_items.metadata->>''type_item'', el modelo VIEJO. La columna item_type nacio para reemplazarlo (add_item_type_catalogo.sql la creo, copio los valores y la puso NOT NULL): hoy la columna esta poblada al 100%', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-08', DATE '2026-08-08', TIMESTAMPTZ '2026-08-08 12:00:00+00', TIMESTAMPTZ '2026-08-08 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-08 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'SES-20260807-8e56'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-8d5565c');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-226a5a4', 'BUG', 'LISTO', 'ALTA',
           'fix(lotes): el nombre de lote es unico por galpon, no por granja', 'Bug detectado y corregido durante «El nombre de lote se validaba único por granja cuando es único por GALPÓN».
Commit 226a5a4 (2026-08-07).
Causa/detalle registrado en el commit: La guarda REQ-009c (b917ad9, 17-jul-2026) rechazaba el alta/edicion cuando ya existia un lote activo con el mismo nombre en la compania+granja. La regla real es mas fina: un mismo nombre de sublote SI puede repetirse en galpones distintos de la misma granja, que es el patron vivo en produccion (A374A en G0326 y G0324 de LA ESMERALDA, A374B en G0325 y G0323, LOTE 235A en dos galpones de la empresa', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-07', DATE '2026-08-07', TIMESTAMPTZ '2026-08-07 12:00:00+00', TIMESTAMPTZ '2026-08-07 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-07 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'SES-20260807-9c89'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-226a5a4');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-425001e', 'BUG', 'LISTO', 'ALTA',
           'fix(reportes): el Reporte Diario de Costos de POSTURA nunca mostraba el levante', 'Bug detectado y corregido durante «El Reporte Diario de Costos de POSTURA nunca mostraba el levante».
Commit 425001e (2026-08-07).
Causa/detalle registrado en el commit: La fn keyeaba seguimiento_diario_levante por lote_id_int, y esa columna esta NULL en el 100% de las filas de produccion (588/588). Ninguna linea de C# la escribe: solo la setea fn_migracion_seguimiento_levante en sus INSERT, que es exactamente por que el lote de pruebas S-369 (cargado por carga masiva en local) validaba y produccion no. Resultado en prod: K345 salia mutilado (solo produccion) y A3', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-07', DATE '2026-08-07', TIMESTAMPTZ '2026-08-07 12:00:00+00', TIMESTAMPTZ '2026-08-07 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-07 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'SES-20260807-7ad9'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-425001e');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-7339c61', 'BUG', 'LISTO', 'ALTA',
           'fix(engorde,inventario): un lote sin cerrar absorbia el ciclo siguiente + ventana de mes actual', 'Bug detectado y corregido durante «Un lote sin liquidar absorbía el ciclo siguiente del galpón (Ecuador)».
Commit 7339c61 (2026-08-07).
Causa/detalle registrado en el commit: Ticket de operacion (Ecuador): ""granja KM 86 lote 01 galpon 1 y 02: tenemos ingreso del mes de julio cuando el lote cerro en abril"". fn_seguimiento_diario_engorde v14 — corte por ciclo siguiente El lote 2601 de Kilometro 86 / Galpon-1 tiene su ultimo seguimiento el 2026-04-20 y la grilla llegaba al 2026-08-03, con el saldo inflado de 1.600 kg a 206.450 kg. Los ingresos de julio son CORRECTOS: son', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-07', DATE '2026-08-07', TIMESTAMPTZ '2026-08-07 12:00:00+00', TIMESTAMPTZ '2026-08-07 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-07 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'SES-20260808-8849'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-7339c61');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-8424557', 'BUG', 'LISTO', 'ALTA',
           'fix(inventario): deshabilita marcar alimento ""para el proximo ciclo"" hasta su rediseno', 'Bug detectado y corregido durante «Alimento previo al encaset: fecha real de llegada e ingreso inicial del ciclo visible».
Commit 8424557 (2026-08-09).
Causa/detalle registrado en el commit: La auditoria de la marca encontro que el defecto no estaba solo en los intentos de mejorarla: esta en lo que ya se entrego en 801b14f. Bajo la fn vigente, marcar un movimiento ROMPE LA CONSERVACION de kilos en 729 de 2.210 casos reales (hasta 37.467 kg que desaparecen de toda tabla diaria) y la grilla llega a 208 filas negativas. Los cuatro guards de la fn le quitan el movimiento a TODO lote con', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-09', DATE '2026-08-09', TIMESTAMPTZ '2026-08-09 12:00:00+00', TIMESTAMPTZ '2026-08-09 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-09 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'SES-20260808-9212'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-8424557');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-44b2400', 'BUG', 'LISTO', 'ALTA',
           'fix(inventario): el stock deja de perder escrituras concurrentes (A1 + A2)', 'Bug detectado y corregido durante «PWA F1-F2 + stock atómico: app instalable, consulta offline y escrituras concurrentes a salvo».
Commit 44b2400 (2026-08-09).
Causa/detalle registrado en el commit: Dos bugs de produccion de HOY, reproducibles con dos pestanas del navegador. Son ademas los items A1 y A2 de la Fase 0.A del plan de PWA, o sea prerrequisito de F2/F3, pero su valor no depende del offline: el offline solo los multiplicaria por N dispositivos. A1 - el stock duplicado era INVISIBLE El indice de la clave natural (farm, item, nucleo, galpon) no era unico y todos los caminos de escrit', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-09', DATE '2026-08-09', TIMESTAMPTZ '2026-08-09 12:00:00+00', TIMESTAMPTZ '2026-08-09 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-09 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'SES-20260809-a721'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-44b2400');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-502ad98', 'BUG', 'LISTO', 'ALTA',
           'fix(levante): editar o borrar un seguimiento mueve el saldo de aves por CUALQUIER camino (A7)', 'Bug detectado y corregido durante «PWA F1-F2 + stock atómico: app instalable, consulta offline y escrituras concurrentes a salvo».
Commit 502ad98 (2026-08-09).
Causa/detalle registrado en el commit: SeguimientoDiarioService escribia la fila del seguimiento pero NO movia el saldo de aves de levante al editar ni al borrar; eso lo hacia el modulo de levante DESPUES de llamarlo. Consecuencia: el mismo comando producia dos estados distintos segun el endpoint. modulo de levante -> fila corregida y saldo movido PUT/DELETE /api/SeguimientoDiario -> fila corregida y SALDO INTACTO mod', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-09', DATE '2026-08-09', TIMESTAMPTZ '2026-08-09 12:00:00+00', TIMESTAMPTZ '2026-08-09 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-09 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'SES-20260809-a721'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-502ad98');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-813e9f5', 'BUG', 'LISTO', 'ALTA',
           'fix(engorde): la atribucion de movimientos deja de caer en lotes liquidados (A9)', 'Bug detectado y corregido durante «PWA F1-F2 + stock atómico: app instalable, consulta offline y escrituras concurrentes a salvo».
Commit 813e9f5 (2026-08-09).
Causa/detalle registrado en el commit: Regla del usuario: un lote liquidado esta CONGELADO y no recibe atribucion nueva. La liquidacion guarda una copia congelada de sus numeros; si despues le siguen entrando movimientos, la copia y el dato vivo dejan de coincidir y no hay forma de saber cual es el bueno. fn_lote_ave_engorde_id_desde_ubicacion resolvia con ORDER BY lote_ave_engorde_id DESC LIMIT 1 -el id mas alto del galpon, sin mirar', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-09', DATE '2026-08-09', TIMESTAMPTZ '2026-08-09 12:00:00+00', TIMESTAMPTZ '2026-08-09 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-09 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'SES-20260809-a721'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-813e9f5');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-4b0f040', 'BUG', 'LISTO', 'ALTA',
           'fix(db): sin permisos de lote base, Ecuador no puede administrar su programacion', 'Bug detectado y corregido durante «Programación de lotes de engorde para Ecuador + gasto contra lote PROGRAMADO».
Commit 4b0f040 (2026-08-11).
Causa/detalle registrado en el commit: Lo encontro el smoke end-to-end: NINGUN rol de ItalcolEcuador tenia `lote_base_pollo_engorde.*` (solo Panama, Demo, Santa Reyes y Sanmarino). Con el flag encendido eso deja el lote base obligatorio en el formulario y ninguna pantalla para crearlo o asignarlo. ver/crear/editar a «Ecuador Administrador» y «Lider implementacion - Regional Ecuador». `.eliminar` NO se otorga, igual que en Panama.', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-11', DATE '2026-08-11', TIMESTAMPTZ '2026-08-11 12:00:00+00', TIMESTAMPTZ '2026-08-11 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-11 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'SES-20260811-d0bf'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-4b0f040');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-6f410db', 'BUG', 'LISTO', 'ALTA',
           'fix(ci): el gate del borde ya no exige que la PWA no exista', 'Bug detectado y corregido durante «El gate del borde del front exigía que la PWA no existiera y tumbaba el deploy».
Commit 6f410db (2026-08-11).
Causa/detalle registrado en el commit: El paso ""Validar nginx y politica de cache del borde"" corre antes del push a ECR y pedia 404 en /ngsw.json y /manifest.webmanifest. Ese criterio se escribio el 27-jul (76a2903), cuando el build no emitia esos archivos: probaba que un recurso no navegable inexistente devuelve 404 y no el index.html. Desde 8ecb7c6 (09-ago) la app es PWA y los emite a proposito, asi que responden 200 y el gate corta', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-11', DATE '2026-08-11', TIMESTAMPTZ '2026-08-11 12:00:00+00', TIMESTAMPTZ '2026-08-11 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-11 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'SES-20260812-f5d0'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-6f410db');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-565164a', 'BUG', 'LISTO', 'ALTA',
           'fix(correos): el respaldo del logo se lee cuando el cliente no baja la imagen', 'Bug detectado y corregido durante «La recuperación de contraseña estaba cortada: el correo imprimía el token como contraseña».
Commit 565164a (2026-08-12).
Causa/detalle registrado en el commit: Visto en Gmail movil: donde va el logo aparecia un icono roto con una leyenda diminuta al lado. En la captura era el localhost:4200 de la configuracion de desarrollo (la URL de produccion existe y responde 200), pero el hueco es real igual: Outlook de escritorio NUNCA descarga imagenes remotas y Gmail tampoco ante un remitente desconocido, asi que buena parte de los lectores ve el texto alternativ', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-12', DATE '2026-08-12', TIMESTAMPTZ '2026-08-12 12:00:00+00', TIMESTAMPTZ '2026-08-12 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-12 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'SES-20260812-a3c4'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-565164a');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-803f170', 'BUG', 'LISTO', 'ALTA',
           'fix(silos): con el flag puesto, el consumo no encontraba su propio item (Santa Reyes, Fase C)', 'Bug detectado y corregido durante «Con el flag de silos puesto, el consumo no encontraba su propio ítem (Santa Reyes)».
Commit 803f170 (2026-08-13).
Causa/detalle registrado en el commit: El smoke de Santa Reyes destapo que con maneja_inventario_por_silo NINGUN consumo podia descontar: el mapeo item -> item_inventario_ecuador se devuelve indexado por claves sin silo y el service lo consulta con la clave real, que si trae el silo. Resultado, un 400 mintiendo (""El item de inventario (id=363) no existe o no pertenece a la empresa de la granja""). Ningun test unitario podia verlo porqu', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-13', DATE '2026-08-13', TIMESTAMPTZ '2026-08-13 12:00:00+00', TIMESTAMPTZ '2026-08-13 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-13 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'SES-20260813-7866'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-803f170');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-584394e', 'BUG', 'LISTO', 'ALTA',
           'fix(silos): el reporte de existencias repetia el item una vez por silo (Santa Reyes, Fase D)', 'Bug detectado y corregido durante «Silos Fase D: el reporte de existencias repetía el ítem una vez por silo».
Commit 584394e (2026-08-13).
Causa/detalle registrado en el commit: fn_inventario_gastos_existencias sacaba el saldo de un LEFT JOIN directo contra inventario_gestion_stock, que asumia UNA fila por (granja, item). Desde la Fase B una empresa con maneja_inventario_por_silo guarda una fila POR SILO, todas con nucleo_id y galpon_id en NULL: con 3 ubicaciones el mismo insumo salia 3 veces en la hoja ""Existencias"", cada una con un saldo parcial. Ahora el saldo lo arma', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-13', DATE '2026-08-13', TIMESTAMPTZ '2026-08-13 12:00:00+00', TIMESTAMPTZ '2026-08-13 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-13 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'SES-20260813-de48'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-584394e');
    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, 'BUG-9e9e24a', 'BUG', 'LISTO', 'ALTA',
           'fix(db-studio): la copia descargable dejaba 4 funciones sin crear al restaurar', 'Bug detectado y corregido durante «La copia descargable de DB Studio dejaba 4 funciones sin crear al restaurar».
Commit 9e9e24a (2026-08-13).
Causa/detalle registrado en el commit: El backup emitia las funciones ordenadas por OID (orden de creacion). Recrear una funcion con DROP+CREATE --obligatorio para cambiarle el RETURNS TABLE-- le asigna un OID nuevo, mas alto que el de sus llamadores, y la manda al final del archivo. Le paso a fn_seguimiento_diario_engorde: al restaurar, sus 4 llamadores LANGUAGE sql fallaban con 42883 y quedaban sin crear (cuadre de alimento engorde,', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '2026-08-13', DATE '2026-08-13', TIMESTAMPTZ '2026-08-13 12:00:00+00', TIMESTAMPTZ '2026-08-13 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '2026-08-13 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = 'SES-20260813-7218'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = 'BUG-9e9e24a');
    -- ═══ 4) La historia agrega las horas de sus tareas (evita el doble conteo) ═══
    UPDATE public.historias h
       SET horas_estimadas = s.total, updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
      FROM (SELECT t.historia_id, SUM(t.horas_estimadas) AS total
              FROM public.ticket_tareas t
             WHERE t.historia_id IS NOT NULL AND t.deleted_at IS NULL
             GROUP BY t.historia_id) s
     WHERE h.id = s.historia_id
       AND h.codigo ~ '^HIS-2026-[0-9]{4}$'
       AND h.horas_estimadas IS DISTINCT FROM s.total;

    RAISE NOTICE 'ItalJira bitácora jul-ago 2026: sembrada.';
END $$;
";

        private const string DOWN_SQL = @"-- Revierte SOLO lo de esta migración.
DO $$
BEGIN
    DELETE FROM public.ticket_tareas WHERE codigo ~ '^BUG-[0-9a-f]{7}$';
    DELETE FROM public.ticket_tareas WHERE codigo ~ '^SES-2026[0-9]{4}-';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/tickets_notificados_flujos_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0001-T10';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/postura_colombia_alineacion_guia_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0011-T2';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/soporte_bot_loop_tickets_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0001-T11';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/fn_metadata_items_kg_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0007-T6';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/unificacion_inventario_colombia_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0007-T7';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/gastos_inventario_ux_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0007-T8';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/alimento_nivel_galpon_configurable_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0012-T11';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/tracker_fase3_paso3_colombia_ARCHIVE.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0016-T5';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/refactor_ux_pro_front_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0017-T2';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/design_system_shared_ui_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0017-T3';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/upgrade_angular_20_a_22_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0019-T15';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/upgrade_dotnet_9_a_10_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0019-T16';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/inventario_nuevo_y_alimento_macho_seguimiento_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0007-T11';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/migraciones_masivas_fase3_spec.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0006-T2';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/migraciones_masivas_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0006-T5';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/migraciones_masivas_engorde_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0006-T4';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/venta_granja_bloqueo_lotes_cerrados_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0010-T10';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/db_studio_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0015-T5';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/db_studio_backup_descargable_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0015-T7';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/fix_aves_vivas_mort_caja_engorde_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0012-T13';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/CONTEXTO_TRASPASO_VACUNACION.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0003-T2';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/CONTEXTO_TRASPASO_SESION.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0019-T10';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/vacunacion_cronograma_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0003-T1';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/puente_panama_engorde_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0012-T14';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/vacunacion_mejora_integral_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0003-T3';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/postura_verenice_rev_6jul26_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0014-T5';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/inventario_multiempresa_scoping_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0007-T13';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/liquidacion_panama_por_corrida_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0008-T6';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/modulo_implementacion_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0002-T1';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/reporte_diario_costos_engorde_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0012-T16';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/implementacion_checklist_v2_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0002-T2';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/fix_consumo_inventario_colombia_multiempresa_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0007-T14';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/fix_fecha_menos_un_dia_engorde_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0012-T15';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/rate_limiting_ajuste_bloqueo_ip_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0004-T3';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/admin_empresa_granjas_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0005-T3';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/sesion_deslizante_inactividad_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0004-T4';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/confirmacion_seguimiento_reproductora_engorde_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0009-T8';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/lote_base_engorde_por_granja_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0005-T7';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/qq_a_kg_alimento_seguimiento_engorde_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0012-T18';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/cierre_lote_reproductora_por_confirmacion_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0008-T8';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/gestion_granjas_cascada_refresh_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0005-T6';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/lote_engorde_corrida_panama_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0012-T17';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/seguimiento_produccion_hereda_lote_padre_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0014-T6';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/reabrir_lote_reproductora_no_persiste_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0008-T7';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/22_mixto_agua_reapertura_cruce_reproductora_engorde_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0009-T4';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/lote_reproductora_engorde_ajustes_creacion_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0009-T7';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/gestion_ubicacion_nucleo_galpon_lote_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0005-T5';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/seguimiento_reproductora_engorde_fechas_edicion_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0009-T9';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/fix_nombres_lote_engorde_panama_por_lote_base_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0005-T4';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/guia_genetica_panama_ross308ap_2022_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0011-T3';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/lote_reproductora_engorde_ux_cascada_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0009-T10';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/seguimiento_pollo_engorde_ux_cascada_scroll_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0012-T20';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/codigo_erp_granja_engorde_panama_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0012-T19';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/migracion_masiva_seguimiento_engorde_reproductora_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0006-T10';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/seguimiento_reproductora_engorde_edad_cero_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0009-T11';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/limpieza_seguimientos_engorde_panama_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0012-T21';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/santa_reyes_implementacion_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0002-T3';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/diseno_filtros_unificado_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0017-T4';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/lote_base_santa_reyes_correccion_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0005-T8';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/demo_huevos_clasico_sanmarino_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0014-T8';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/fix_seguimiento_produccion_lote_id_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0014-T7';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/scope_ubicacion_usuario_granja_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0004-T5';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/reporte_tecnico_semanal_postura_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0014-T9';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/huevos_levante_semana14_arrastre_produccion_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0013-T5';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/CONTEXTO_TRASPASO_HUEVOS_LEVANTE.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0013-T6';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/venta_engorde_panama_peso_diferido_y_carga_masiva_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0006-T11';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/recepcion_transito_distribucion_galpones_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0007-T16';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/cierre_levante_reapertura_validada_y_cierre_produccion_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0008-T9';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/pwa_offline_first_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0019-T20';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/hora_encasetamiento_primer_registro_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0012-T22';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/seguimiento_engorde_mixto_panama_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0012-T24';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/fix_deploy_frontend_dockerignore_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0019-T21';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/csp_recaptcha_login_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0004-T6';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/dia_negocio_engorde_pesaje_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0012-T23';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/carga_masiva_alimento_engorde_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0006-T12';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/informe_ra_pesadas_parametros_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0015-T8';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/migracion_masiva_postura_alimento_inventario_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0006-T13';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/cuadre_engorde_panama_aves_alimento_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0012-T26';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/cuadre_engorde_ecuador_requerimiento.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0012-T27';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/congelar_liquidacion_lote_engorde_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0008-T10';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/cuadre_engorde_ecuador_diagnostico_saldo_alimento.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0012-T25';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/fix_apertura_alimento_ciclo_anterior_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0012-T28';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/prevencion_descuadres_alimento_engorde_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0012-T29';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/INSTRUCTIVO_OPERACION_saldos_alimento_engorde.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0012-T30';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/carga_masiva_levante_movimientos_aves_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0006-T14';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/seguimiento_produccion_fn_canonica_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0014-T11';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/gastos_inventario_reporte_estado_existencias_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0007-T18';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/fix_disponibilidad_aves_venta_engorde_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0010-T12';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/envio_correo_graph_api_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0018-T2';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/exportar_stock_inventario_excel_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0007-T17';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/traslado_aves_destino_cross_granja_y_fecha_registro_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0010-T13';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/cohortes_edades_lote_receptor_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0010-T14';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/tipo_alimento_varchar_desborde_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0012-T33';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/carga_masiva_s369ab_postura_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0006-T15';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/19_indicadores_levante_produccion_ux_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0013-T7';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/20_handoff_postura_hallazgos_sesion.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0014-T12';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/18_tickets_jira_casos_tareas_tablero_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0001-T12';
    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = 'Plan: fase_de_desarrollo/reconciliacion_espejo_fn_indicadores_produccion_plan.md', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = 'HIS-2026-0014-T13';
    UPDATE public.historias SET horas_estimadas = NULL, updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo ~ '^HIS-2026-[0-9]{4}$';
END $$;
";
    }
}
