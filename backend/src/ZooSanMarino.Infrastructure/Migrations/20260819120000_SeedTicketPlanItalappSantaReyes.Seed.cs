using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Cuerpo SQL del seed del Plan de Trabajo de Italapp para Santa Reyes. Vive en su propio
    /// archivo (<c>partial</c>) por tamaño: la documentación de qué hace y por qué está en
    /// <c>20260819120000_SeedTicketPlanItalappSantaReyes.cs</c>.
    /// </summary>
    /// <remarks>
    /// Los textos son los del entregable comercial <c>Plan_de_Trabajo_Santa_Reyes.xlsx</c> v2.0
    /// (hojas <i>Alcance</i>, <i>Plan diario</i> y <i>Cronograma</i>). Se escriben <b>sin acentos</b>,
    /// igual que el resto de los seeds del módulo de tickets.
    /// </remarks>
    public partial class SeedTicketPlanItalappSantaReyes
    {
        private const string SEED_SQL = @"
-- ─────────────────────────────────────────────────────────────────────────────
-- Plan de Trabajo de Italapp para Santa Reyes en ItalJira.
--   1 historia · 1 caso (creado por el administrador A NOMBRE de Lenin) ·
--   13 tareas (paquetes F0..F12) · 29 subtareas (actividades) · 100,00 h.
-- Fuente: ~/Desktop/Plan_de_Trabajo_Santa_Reyes.xlsx v2.0 (18ago26).
-- ─────────────────────────────────────────────────────────────────────────────
DO $$
DECLARE
    c_his_titulo constant varchar(200) :=
        'Implementacion de Italapp para Santa Reyes (requerimientos del cliente + guias geneticas)';
    c_tk_titulo  constant varchar(160) :=
        'Requerimientos de Italapp para Santa Reyes: 100 horas en 10 jornadas, entrega el 1 de septiembre de 2026';

    v_admin_guid  uuid;
    v_admin_ced   integer;
    v_admin_nom   text;
    v_lenin_guid  uuid;
    v_lenin_ced   integer;
    v_lenin_nom   text;
    v_sr_company  integer;
    v_company     integer;
    v_pais        integer;
    v_historia_id bigint;
    v_his_codigo  varchar(40);
    v_ticket_id   bigint;
    v_orden       integer;
    v_nota        text;
BEGIN
    -- ═══════════════ 0) ADMINISTRADOR: creador, responsable y asignado ═══════════════
    -- Identidad POR EMAIL, nunca por guid fijo: los ids difieren entre local y produccion.
    SELECT u.id, btrim(coalesce(u.first_name,'') || ' ' || coalesce(u.sur_name,''))
      INTO v_admin_guid, v_admin_nom
    FROM public.users u
    JOIN public.user_logins ul ON ul.user_id = u.id
    JOIN public.logins l       ON l.id = ul.login_id
    WHERE lower(l.email) = 'moiesbbuga@gmail.com'
    LIMIT 1;

    -- Fail-open silencioso: sin el administrador no se siembra nada y la app arranca igual.
    IF v_admin_guid IS NULL THEN
        RAISE NOTICE 'Plan Italapp Santa Reyes: no existe moiesbbuga@gmail.com en este entorno; omitido.';
        RETURN;
    END IF;

    -- El int de auditoria del modulo NO es la cedula (3177120174 no entra en integer):
    -- se reusa el que ya usan sus propios casos.
    SELECT t.created_by_user_id INTO v_admin_ced
    FROM public.tickets t WHERE t.created_by_user_guid = v_admin_guid ORDER BY t.id DESC LIMIT 1;
    IF v_admin_ced IS NULL THEN
        SELECT CASE WHEN u.cedula ~ '^[0-9]{1,9}$' THEN u.cedula::integer ELSE 0 END
          INTO v_admin_ced FROM public.users u WHERE u.id = v_admin_guid;
    END IF;
    v_admin_ced := COALESCE(v_admin_ced, 0);
    v_admin_nom := NULLIF(btrim(COALESCE(v_admin_nom, '')), '');

    -- ═══════════════ 1) EMPRESA SANTA REYES: por nombre, nunca por id ═══════════════
    SELECT c.id INTO v_sr_company
    FROM public.companies c
    WHERE lower(c.name) LIKE '%santa%reyes%'
    ORDER BY c.id
    LIMIT 1;

    -- ═══════════════ 2) LENIN: el solicitante a cuyo nombre va el caso ═══════════════
    -- Se busca por nombre, apellido o correo; prefiriendo a quien pertenezca a Santa Reyes.
    SELECT u.id,
           CASE WHEN u.cedula ~ '^[0-9]{1,9}$' THEN u.cedula::integer ELSE NULL END,
           btrim(coalesce(u.first_name,'') || ' ' || coalesce(u.sur_name,''))
      INTO v_lenin_guid, v_lenin_ced, v_lenin_nom
    FROM public.users u
    LEFT JOIN public.user_companies uc ON uc.user_id = u.id
    LEFT JOIN public.user_logins ul    ON ul.user_id = u.id
    LEFT JOIN public.logins l          ON l.id = ul.login_id
    WHERE lower(coalesce(u.first_name, '')) LIKE '%lenin%'
       OR lower(coalesce(u.sur_name,   '')) LIKE '%lenin%'
       OR lower(coalesce(l.email,      '')) LIKE '%lenin%'
    ORDER BY (uc.company_id IS NOT DISTINCT FROM v_sr_company) DESC, u.created_at, u.id
    LIMIT 1;

    -- Delegar en uno mismo es redundante: el modulo lo ignora y el caso queda como propio.
    IF v_lenin_guid = v_admin_guid THEN
        v_lenin_guid := NULL;
        v_lenin_ced  := NULL;
        v_lenin_nom  := NULL;
    END IF;

    -- ═══════════════ 3) EMPRESA Y PAIS DEL CASO ═══════════════
    -- El caso pertenece a la empresa del SOLICITANTE: si quedara en la del administrador,
    -- Mis solicitudes de Lenin (que filtra por su empresa efectiva) no lo mostraria nunca.
    IF v_lenin_guid IS NOT NULL THEN
        SELECT uc.company_id, uc.pais_id INTO v_company, v_pais
        FROM public.user_companies uc
        WHERE uc.user_id = v_lenin_guid
        ORDER BY (uc.company_id IS NOT DISTINCT FROM v_sr_company) DESC,
                 uc.is_default DESC, uc.company_id
        LIMIT 1;
    END IF;

    v_company := COALESCE(v_company, v_sr_company);

    IF v_pais IS NULL AND v_company IS NOT NULL THEN
        SELECT uc.pais_id INTO v_pais
        FROM public.user_companies uc
        WHERE uc.company_id = v_company AND uc.pais_id IS NOT NULL
        LIMIT 1;
    END IF;

    -- Ultimo recurso: la empresa y el pais de los propios casos del administrador.
    IF v_company IS NULL THEN
        SELECT t.company_id, t.pais_id INTO v_company, v_pais
        FROM public.tickets t WHERE t.created_by_user_guid = v_admin_guid ORDER BY t.id DESC LIMIT 1;
    END IF;
    v_company := COALESCE(v_company, 1);
    v_pais    := COALESCE(v_pais, 1);

    IF v_lenin_guid IS NULL THEN
        RAISE NOTICE 'Plan Italapp Santa Reyes: no se encontro al usuario Lenin; el caso se siembra en la empresa % SIN solicitante delegado (asignarlo desde la pantalla).', v_company;
    END IF;

    -- ═══════════════ 4) HISTORIA (epica del proyecto) ═══════════════
    SELECT h.id, h.codigo INTO v_historia_id, v_his_codigo
    FROM public.historias h
    WHERE h.titulo = c_his_titulo AND h.deleted_at IS NULL
    LIMIT 1;

    IF v_historia_id IS NULL THEN
        SELECT COALESCE(MAX(h.orden) + 1, 0) INTO v_orden
        FROM public.historias h WHERE h.estado = 'BACKLOG' AND h.deleted_at IS NULL;

        INSERT INTO public.historias
            (pais_id, titulo, descripcion, estado, prioridad, responsable_user_guid, orden,
             horas_estimadas, fecha_inicio_plan, fecha_fin_plan, etiquetas,
             company_id, created_by_user_id, created_at)
        VALUES
            (v_pais, c_his_titulo,
             'Proyecto de implementacion de los requerimientos de Italapp entregados por Santa Reyes el 18 de agosto de 2026: el documento Requerimientos de Italapp (7 modulos, 10 pantallas anotadas) y el archivo Guias Geneticas (5 lineas por 108 semanas).

PLAN VIGENTE (v2.0). 100 horas en 10 jornadas habiles de 10 horas, del miercoles 19 de agosto al martes 1 de septiembre de 2026, con un desarrollador full-stack dedicado. 4 hitos y 29 actividades agrupadas en 13 paquetes de trabajo (F0 a F12). Cada jornada resuelve varias actividades y suma exactamente 10 horas.

LOS 4 HITOS.
H1 - Fundaciones, estructura de granja, guias geneticas y ciclo por raza (dias 1 a 3, 30 h).
H2 - Registro diario ajustado a Santa Reyes (dias 4 a 6, 30 h).
H3 - Produccion de huevos con la nomenclatura del cliente (dias 7 y 8, 20 h).
H4 - Traslados, pruebas y salida a produccion (dias 9 y 10, 20 h).

PRINCIPIO RECTOR. Todo el comportamiento nuevo viaja detras de banderas por empresa, apagadas por defecto: con la bandera apagada, las empresas ya productivas (Sanmarino, Panama y Ecuador) quedan identicas a como estan hoy.

Fuente: Plan_de_Trabajo_Santa_Reyes.xlsx v2.0 (hojas Plan diario, Cronograma, Carga por jornada, Alcance y Guias geneticas).',
             'BACKLOG', 'ALTA', v_admin_guid, v_orden,
             100.00, DATE '2026-08-19', DATE '2026-09-01',
             'santa-reyes,italapp,postura,multiempresa',
             v_company, v_admin_ced, timezone('utc', now()))
        RETURNING id INTO v_historia_id;

        v_his_codigo := 'HIS-2026-' || lpad(v_historia_id::text, 4, '0');
        UPDATE public.historias SET codigo = v_his_codigo WHERE id = v_historia_id;
    END IF;

    -- El codigo de la historia es la clave de idempotencia de las tareas. Si la historia existiera
    -- con codigo NULL, el WHERE NOT EXISTS de abajo comparara contra NULL, no encontrara nada y
    -- volvera a insertar las 42 filas en cada corrida. Se completa antes de seguir.
    IF v_his_codigo IS NULL THEN
        v_his_codigo := 'HIS-2026-' || lpad(v_historia_id::text, 4, '0');
        UPDATE public.historias SET codigo = v_his_codigo WHERE id = v_historia_id AND codigo IS NULL;
    END IF;

    -- ═══════════════ 5) CASO (ticket) — creado por el admin A NOMBRE de Lenin ═══════════════
    SELECT t.id INTO v_ticket_id
    FROM public.tickets t
    WHERE t.titulo = c_tk_titulo AND t.deleted_at IS NULL
    LIMIT 1;

    IF v_ticket_id IS NULL THEN
        SELECT COALESCE(MAX(t.orden_tablero) + 1, 0) INTO v_orden
        FROM public.tickets t WHERE t.estado = 'ABIERTO' AND t.deleted_at IS NULL;

        INSERT INTO public.tickets
            (pais_id, tipo, estado, titulo, descripcion,
             assigned_to_user_guid, created_by_user_guid,
             solicitante_user_guid, solicitante_user_id,
             prioridad, orden_tablero, horas_estimadas,
             fecha_inicio_plan, fecha_fin_plan, fecha_limite,
             historia_id, status, notificado_correo,
             company_id, created_by_user_id, created_at)
        VALUES
            (v_pais, 'REQUERIMIENTO', 'ABIERTO', c_tk_titulo,
             'PEDIDO DEL CLIENTE (Santa Reyes, 18 de agosto de 2026). Requerimientos de Italapp entregados en dos archivos: el documento Requerimientos de Italapp, con 7 modulos y 10 pantallas anotadas, y el archivo Guias Geneticas, con 5 lineas de 108 semanas cada una.

ALCANCE ACORDADO. 100 horas en 10 jornadas habiles de 10 horas, del miercoles 19 de agosto al martes 1 de septiembre de 2026, con un desarrollador full-stack dedicado. 4 hitos y 29 actividades. Cada jornada resuelve varias actividades y suma exactamente 10 horas.

LOS 4 HITOS.
H1 - Fundaciones, estructura de granja, guias geneticas y ciclo por raza (dias 1 a 3, 30 h).
H2 - Registro diario ajustado a Santa Reyes (dias 4 a 6, 30 h).
H3 - Produccion de huevos con la nomenclatura del cliente (dias 7 y 8, 20 h).
H4 - Traslados, pruebas y salida a produccion (dias 9 y 10, 20 h).

QUE ENTRA. Parametrizacion por empresa y catalogo de items propio; silo como estructura fisica de la granja y codigos de integracion con el ERP por nivel (granja CO, nucleo bodega, silo y bodega ubicacion, lote centro de costo); carga de las 5 lineas geneticas (540 registros) y comparativo del real contra la guia; semanas de ciclo por raza; consumo de alimento solo hembras; retiro del error de sexaje y de la columna de machos en mortalidad, seleccion, peso y uniformidad; campo de machos sobre el total de las aves en ventas; tipos de inventario acotados a Alimento y Aves; huevo sin clasificar con sus 7 items; huevo de primera postura por raza con vigencia hasta la semana 22; renombre de los productos no conformes; panel de eficiencia cuadrado contra el total de produccion de la granja; traslado de aves sin machos y con placa, precinto y conductor; traslado de huevos con bodega de salida desplegable y tipos alineados al catalogo nuevo; pruebas y despliegue.

QUE NO ENTRA. El acompañamiento posterior a la entrega (semana del 2 al 8 de septiembre) queda fuera de las 10 jornadas y se presta bajo demanda.

SUPUESTOS Y RIESGOS.
1) Riesgo alto numero 1: la actividad F1.2 (codigos de integracion con el ERP por nivel) corre el DIA 1. Santa Reyes tiene que entregar antes del inicio la estructura fisica real (nucleos, galpones, silos y bodegas) y los codigos ERP (CO, bodegas, ubicaciones y centros de costo). El plan no tiene holgura: cada dia de demora corre la entrega del 1 de septiembre en la misma medida.
2) El cronograma arranca con la aprobacion del cliente del alcance, del cronograma y de los supuestos.
3) Ninguna empresa ya productiva cambia: todo el comportamiento nuevo viaja detras de banderas por empresa, apagadas por defecto.

DETALLE. Cada paquete de trabajo es una tarea de este caso y cada actividad una subtarea, con sus horas, su hito y su dia. El detalle completo esta en las hojas Plan diario, Cronograma, Carga por jornada, Alcance y Guias geneticas del archivo Plan_de_Trabajo_Santa_Reyes.xlsx (v2.0, 18 de agosto de 2026).',
             v_admin_guid, v_admin_guid,
             v_lenin_guid, v_lenin_ced,
             'ALTA', v_orden, 100.00,
             -- Compromiso de entrega = fin del dia 1 de septiembre en Colombia (UTC-5), no UTC:
             -- con +00 el semaforo lo mostraria venciendo a las 18:59 del mismo dia.
             DATE '2026-08-19', DATE '2026-09-01', timestamptz '2026-09-01 23:59:59-05',
             v_historia_id, 'A', false,
             v_company, v_admin_ced, timezone('utc', now()))
        RETURNING id INTO v_ticket_id;

        UPDATE public.tickets
           SET codigo = 'TK-2026-' || lpad(v_ticket_id::text, 6, '0')
         WHERE id = v_ticket_id;
    END IF;

    -- ═══════════════ 6) NOTA DE SOLICITANTE DELEGADO ═══════════════
    -- Sin esta nota, Lenin veria un caso suyo que nunca creo. Es la misma que escribe
    -- TicketService.CreateAsync al delegar (tipo_evento SISTEMA_SOLICITANTE).
    IF v_lenin_guid IS NOT NULL THEN
        v_nota := 'Caso registrado por ' || COALESCE(v_admin_nom, 'el administrador') ||
                  ' a nombre de ' || COALESCE(NULLIF(v_lenin_nom, ''), 'otro usuario') || '.';

        INSERT INTO public.ticket_notas (ticket_id, user_id, nota, es_interna, tipo_evento, created_at)
        SELECT v_ticket_id, v_admin_ced, v_nota, false, 'SISTEMA_SOLICITANTE', timezone('utc', now())
        WHERE NOT EXISTS (
            SELECT 1 FROM public.ticket_notas n
            WHERE n.ticket_id = v_ticket_id AND n.tipo_evento = 'SISTEMA_SOLICITANTE');
    END IF;

    -- ═══════════════ 7) TAREAS: los 13 paquetes de trabajo (F0..F12) ═══════════════
    -- orden 0..12 dentro del caso; las 29 subtareas siguen en 13..41 (contiguo, sin huecos:
    -- el tablero de tareas esta scopeado por ticket_id y un orden con huecos baraja las tarjetas).
    INSERT INTO public.ticket_tareas
        (ticket_id, historia_id, codigo, tipo, estado, prioridad, titulo, descripcion,
         asignado_user_guid, orden, horas_estimadas, fecha_inicio_plan, fecha_fin_plan, etiquetas,
         company_id, created_by_user_id, created_at)
    SELECT v_ticket_id, v_historia_id,
           v_his_codigo || '-T' || t.n,
           'TAREA', 'BACKLOG', t.prioridad, t.titulo, t.descripcion,
           v_admin_guid, t.orden, t.horas::numeric(8,2), t.f_ini, t.f_fin, t.etiquetas,
           v_company, v_admin_ced, timezone('utc', now())
    FROM (VALUES
        (1, 0, 'CRITICA',
         'F0 - Parametrizacion por empresa',
         'Paquete F0 (hito H1, dia 1, 6 h en 2 actividades).

Es la base de todo lo demas: habilita que los cambios de Santa Reyes apliquen SOLO a Santa Reyes, sin tocar a ninguna otra empresa, y deja creado el catalogo de items propio de la compania.

ACTIVIDADES. F0.1 banderas de comportamiento en la ficha de empresa (3 h). F0.2 catalogo de items de huevo y alimento (3 h).

POR QUE VA PRIMERO. Las 27 actividades restantes se apoyan en las banderas y en el catalogo: sin ellas cada cambio seria global y afectaria a Sanmarino, Panama y Ecuador.',
         6, DATE '2026-08-19', DATE '2026-08-19', 'santa-reyes,italapp,H1,backend,frontend,multiempresa'),

        (2, 1, 'CRITICA',
         'F1 - Estructura fisica de granja y codigos ERP',
         'Paquete F1 (hito H1, dias 1 y 2, 10 h en 2 actividades).

Lleva el silo a la estructura fisica de la granja y homologa los codigos de integracion con el ERP en los cuatro niveles.

ACTIVIDADES. F1.2 codigos de integracion con el ERP por nivel (4 h, dia 1). F1.1 campo Silo como estructura fisica de la granja (6 h, dia 2).

RIESGO ALTO NUMERO 1. F1.2 corre el DIA 1 y necesita que Santa Reyes haya entregado antes del inicio la estructura fisica real y los codigos ERP. El plan no tiene holgura: cada dia de demora corre la entrega del 1 de septiembre en la misma medida.',
         10, DATE '2026-08-19', DATE '2026-08-20', 'santa-reyes,italapp,H1,backend,frontend,erp'),

        (3, 2, 'ALTA',
         'F2 - Guias geneticas',
         'Paquete F2 (hito H1, dias 2 y 3, 10 h en 2 actividades).

Carga las 5 lineas geneticas del archivo del cliente y las pone a trabajar: el lote queda asociado a su linea y los indicadores y reportes comparan el real contra esa guia.

ACTIVIDADES. F2.1 carga de las 5 lineas, semanas 18 a 125 (4 h, dia 2). F2.2 asociacion de la linea al lote y uso en indicadores y reportes (6 h, dia 3).

VOLUMEN. 5 lineas por 108 semanas = 540 registros, con porcentaje de produccion de tabla, porcentaje de mortalidad acumulada y gramos por ave por dia.',
         10, DATE '2026-08-20', DATE '2026-08-21', 'santa-reyes,italapp,H1,backend,frontend,guia-genetica'),

        (4, 3, 'ALTA',
         'F3 - Semanas de produccion por raza',
         'Paquete F3 (hitos H1 y H2, dias 3 y 4, 10 h en 2 actividades).

Saca de la aplicacion las semanas del ciclo, que hoy son fijas, y las convierte en parametros por raza. De esos numeros salen la etapa del lote, el fin de ciclo y las alertas de edad.

ACTIVIDADES. F3.1 parametros de levante por raza: 8 semanas de alistamiento y 16 de levante (4 h, dia 3). F3.2 parametros de produccion: 4 semanas de levante en granja de produccion mas 74 semanas de postura para rojas y criollas, u 84 para blancas y Azur (6 h, dia 4).',
         10, DATE '2026-08-21', DATE '2026-08-24', 'santa-reyes,italapp,H1,H2,backend,frontend'),

        (5, 4, 'ALTA',
         'F4 - Consumo de alimento solo hembras',
         'Paquete F4 (hito H2, dias 4 y 5, 8 h en 2 actividades).

Retira el consumo de machos de los dos seguimientos diarios: el registro queda expresado solo sobre hembras.

ACTIVIDADES. F4.1 retirar el consumo de machos del seguimiento diario de produccion (4 h, dia 4). F4.2 lo mismo en el seguimiento diario de levante (4 h, dia 5).

REGLA. La decision la toma el backend a partir de la bandera de empresa, no la pantalla.',
         8, DATE '2026-08-24', DATE '2026-08-25', 'santa-reyes,italapp,H2,backend,frontend'),

        (6, 5, 'MEDIA',
         'F5 - Mortalidad, pesaje y ventas',
         'Paquete F5 (hito H2, dias 5 y 6, 9 h en 3 actividades).

Ajusta el registro diario a como trabaja Santa Reyes: sin error de sexaje, sin columna de machos, y con el dato de machos que la empresa si necesita, que es el de las ventas.

ACTIVIDADES. F5.1 retirar el concepto de error de sexaje del registro diario (3 h, dia 5). F5.2 ocultar la columna de machos en mortalidad, seleccion, peso y uniformidad (3 h, dia 5). F5.3 campo machos sobre el total de las aves en el registro de ventas (3 h, dia 6).',
         9, DATE '2026-08-25', DATE '2026-08-26', 'santa-reyes,italapp,H2,backend,frontend'),

        (7, 6, 'MEDIA',
         'F6 - Tipos de inventario',
         'Paquete F6 (hito H2, dia 6, 3 h en 1 actividad).

Acota el catalogo de inventario a los dos tipos que maneja la compania.

ACTIVIDAD. F6.1 limitar los tipos de item de inventario a Alimento y Aves (3 h, dia 6).',
         3, DATE '2026-08-26', DATE '2026-08-26', 'santa-reyes,italapp,H2,backend,frontend,inventario'),

        (8, 7, 'ALTA',
         'F7 - Huevo sin clasificar y primera postura',
         'Paquete F7 (hitos H2 y H3, dias 6 a 8, 17 h en 4 actividades). Es el paquete mas grande del plan.

Reexpresa la produccion de huevos con la nomenclatura del cliente y le da al huevo sin clasificar y a la primera postura el desglose por item y por raza que hoy no tienen.

ACTIVIDADES. F7.1 renombrar huevos incubables a huevos sin clasificar (4 h, dia 6). F7.2 los 7 items de huevo sin clasificar (6 h, dia 7). F7.3 huevo de primera postura con seleccion de raza y definicion de los huevos del lote al crearlo (4 h, dia 7). F7.4 regla de vigencia de la primera postura hasta la semana 22 (3 h, dia 8).',
         17, DATE '2026-08-26', DATE '2026-08-28', 'santa-reyes,italapp,H2,H3,backend,frontend,huevos'),

        (9, 8, 'MEDIA',
         'F8 - Productos no conformes y panel de eficiencia',
         'Paquete F8 (hito H3, dia 8, 7 h en 3 actividades).

Cierra la nomenclatura de la produccion de huevos y deja el panel de eficiencia cuadrado contra el total de la granja.

ACTIVIDADES. F8.1 renombrar los productos no conformes a Manchado, Decolorado, Enyemado, Picado y Farfara (2 h). F8.2 retirar huevo tratado, peso promedio y tipo de alimento del registro de produccion (2 h). F8.3 panel de eficiencia con la nueva nomenclatura y cuadre de la suma de huevos contra el total de produccion de la granja (3 h).',
         7, DATE '2026-08-28', DATE '2026-08-28', 'santa-reyes,italapp,H3,backend,frontend,huevos'),

        (10, 9, 'MEDIA',
         'F9 - Traslado de aves',
         'Paquete F9 (hito H4, dia 9, 5 h en 2 actividades).

Deja el traslado de aves como lo necesita Santa Reyes: sin machos y con la informacion del transporte.

ACTIVIDADES. F9.1 ocultar machos en el traslado de aves (2 h). F9.2 campos de transporte placa, precinto y conductor, con reflejo en el listado y en el comprobante (3 h).',
         5, DATE '2026-08-31', DATE '2026-08-31', 'santa-reyes,italapp,H4,backend,frontend,traslados'),

        (11, 10, 'MEDIA',
         'F10 - Traslado de huevos',
         'Paquete F10 (hito H4, dia 9, 5 h en 2 actividades).

Homologa el traslado de huevos con la estructura de la granja y con el catalogo nuevo: se elimina la digitacion libre.

ACTIVIDADES. F10.1 bodega de salida como lista desplegable con las opciones de traslado de la granja (3 h). F10.2 tipos de huevo del traslado alineados al nuevo catalogo (2 h).',
         5, DATE '2026-08-31', DATE '2026-08-31', 'santa-reyes,italapp,H4,backend,frontend,traslados,huevos'),

        (12, 11, 'CRITICA',
         'F11 - Pruebas',
         'Paquete F11 (hito H4, dia 10, 8 h en 3 actividades).

Es la compuerta de calidad: sin pruebas verdes no hay despliegue.

ACTIVIDADES. F11.1 pruebas automatizadas de los calculos y reglas nuevas (3 h). F11.2 pruebas de no regresion sobre las empresas ya productivas: Sanmarino, Panama y Ecuador (3 h). F11.3 pruebas asistidas con el usuario de Santa Reyes sobre datos reales (2 h).

CRITERIO. Cero diferencias en las empresas que no son Santa Reyes.',
         8, DATE '2026-09-01', DATE '2026-09-01', 'santa-reyes,italapp,H4,tests,calidad'),

        (13, 12, 'CRITICA',
         'F12 - Despliegue',
         'Paquete F12 (hito H4, dia 10, 2 h en 1 actividad). Cierra la entrega.

ACTIVIDAD. F12.1 despliegue a produccion y verificacion posterior (2 h).

FUERA DEL PLAN. El acompañamiento de la semana del 2 al 8 de septiembre quedo fuera de las 10 jornadas y se presta bajo demanda.',
         2, DATE '2026-09-01', DATE '2026-09-01', 'santa-reyes,italapp,H4,devops,despliegue')
    ) AS t(n, orden, prioridad, titulo, descripcion, horas, f_ini, f_fin, etiquetas)
    WHERE NOT EXISTS (
        SELECT 1 FROM public.ticket_tareas x WHERE x.codigo = v_his_codigo || '-T' || t.n
    );

    -- ═══════════════ 8) SUBTAREAS: las 29 actividades ═══════════════
    -- El codigo es <historia>-T<paquete>-S<actividad>: la S refleja el numero real de la actividad
    -- (F1.1 -> T2-S1, F1.2 -> T2-S2), no el orden en que se ejecutan. El padre se resuelve por
    -- codigo contra la tarea del paquete recien insertada, nunca por id.
    INSERT INTO public.ticket_tareas
        (ticket_id, historia_id, codigo, tipo, estado, prioridad, titulo, descripcion,
         asignado_user_guid, parent_tarea_id, orden, horas_estimadas,
         fecha_inicio_plan, fecha_fin_plan, etiquetas,
         company_id, created_by_user_id, created_at)
    SELECT v_ticket_id, v_historia_id,
           v_his_codigo || '-T' || s.p || '-S' || s.m,
           'SUBTAREA', 'BACKLOG', s.prioridad, s.titulo, s.descripcion,
           v_admin_guid, padre.id, s.orden, s.horas::numeric(8,2),
           s.fecha, s.fecha, s.etiquetas,
           v_company, v_admin_ced, timezone('utc', now())
    FROM (VALUES
        -- ── Dia 1 · miercoles 19 de agosto de 2026 ──
        (1, 1, 13, 'CRITICA',
         'F0.1 Banderas de comportamiento de Santa Reyes en la ficha de empresa (base de datos, servicio y pantalla de administracion)',
         'Actividad F0.1 · hito H1 · dia 1, miercoles 19 de agosto de 2026 · 3 h.

OBSERVACION DEL CLIENTE. Transversal: habilita que los cambios apliquen solo a Santa Reyes sin afectar a las demas empresas.

QUE SE ENTREGA. La ficha de empresa gana una bandera por cada comportamiento nuevo (consumo de alimento solo hembras, ocultar machos en postura, semana limite del huevo de primera postura, silo dentro de la estructura de granja, tipos de inventario acotados). Cada bandera nace apagada, viaja en el servicio de empresa y en las cuatro proyecciones que arman la ficha, y se puede encender o apagar desde la pantalla de administracion de empresas sin necesidad de un despliegue.

COMO SE VERIFICA. Con las banderas apagadas, Sanmarino, Panama y Ecuador se comportan exactamente igual que hoy. Encendidas en Santa Reyes, solo esa empresa cambia.',
         3, DATE '2026-08-19', 'santa-reyes,italapp,H1,backend,frontend,multiempresa'),

        (1, 2, 14, 'ALTA',
         'F0.2 Catalogo de items de huevo y alimento de Santa Reyes (creacion y carga inicial)',
         'Actividad F0.2 · hito H1 · dia 1, miercoles 19 de agosto de 2026 · 3 h.

OBSERVACION DEL CLIENTE. Produccion de huevos y Levantes: nuevos items y tipos de inventario.

QUE SE ENTREGA. Catalogo propio de Santa Reyes con los items de huevo que usa la empresa (los 7 de huevo sin clasificar, el de primera postura por raza y los 5 de producto no conforme) y los de alimento. La carga inicial va por migracion idempotente y el alcance del catalogo queda acotado a la empresa, de modo que no se mezcla con el de las demas.

COMO SE VERIFICA. Los desplegables de items en produccion, levante e inventario muestran solo lo de Santa Reyes, y ninguna otra empresa ve items nuevos.',
         3, DATE '2026-08-19', 'santa-reyes,italapp,H1,backend,catalogo'),

        (2, 2, 15, 'CRITICA',
         'F1.2 Codigos de integracion con el ERP por nivel: granja = CO, nucleo = bodega, silo y bodega = ubicacion, lote = centro de costo',
         'Actividad F1.2 · hito H1 · dia 1, miercoles 19 de agosto de 2026 · 4 h.

OBSERVACION DEL CLIENTE. Ingreso a granja: observaciones de homologacion con el ERP.

QUE SE ENTREGA. Cada nivel de la estructura lleva su codigo del ERP: la granja el CO, el nucleo la bodega, el silo y la bodega la ubicacion, y el lote el centro de costo. Los campos quedan en el alta y en la edicion de cada nivel, y viajan en los listados y en los reportes que alimentan la integracion.

DEPENDENCIA CRITICA. Esta actividad corre el DIA 1 y necesita que Santa Reyes haya entregado la estructura fisica real (nucleos, galpones, silos y bodegas) y los codigos ERP antes del inicio. Es el riesgo alto numero 1 del plan y el cronograma no tiene holgura: cada dia de demora corre la entrega del 1 de septiembre en la misma medida.

COMO SE VERIFICA. Los cuatro niveles muestran y guardan su codigo, y el reporte que alimenta el ERP los trae.',
         4, DATE '2026-08-19', 'santa-reyes,italapp,H1,backend,frontend,erp'),

        -- ── Dia 2 · jueves 20 de agosto de 2026 ──
        (2, 1, 16, 'ALTA',
         'F1.1 Campo Silo como estructura fisica de la granja: alta, edicion, listado y asociacion a galpon y lote',
         'Actividad F1.1 · hito H1 · dia 2, jueves 20 de agosto de 2026 · 6 h.

OBSERVACION DEL CLIENTE. Ingreso a granja: se requiere campo para Silo como estructura fisica de la granja.

QUE SE ENTREGA. El silo pasa a ser parte de la estructura de la granja, al mismo nivel que el nucleo y el galpon: alta, edicion, listado y baja desde el formulario de ingreso a granja, con su asociacion a galpon y a lote para poder imputar el alimento por silo.

COMO SE VERIFICA. Crear una granja con sus silos, asociarlos a galpon y a lote, y ver el silo disponible como ubicacion al mover alimento.',
         6, DATE '2026-08-20', 'santa-reyes,italapp,H1,backend,frontend,silos'),

        (3, 1, 17, 'ALTA',
         'F2.1 Carga de las 5 lineas geneticas (Babcock Brown, Hy Line Brown, Lohmann LSL, Criolla y Azur), semanas 18 a 125',
         'Actividad F2.1 · hito H1 · dia 2, jueves 20 de agosto de 2026 · 4 h.

OBSERVACION DEL CLIENTE. Archivo Guias Geneticas.xlsx: porcentaje de produccion de tabla, porcentaje de mortalidad acumulada y gramos por ave por dia.

QUE SE ENTREGA. Carga de las 5 lineas, 108 semanas cada una (de la 18 a la 125), es decir 540 registros, con los tres indicadores del archivo. La carga va por migracion idempotente y se valida fila a fila contra el archivo entregado por el cliente.

DATO DE CADA LINEA. Babcock Brown y Hy Line Brown son aves rojas de 74 semanas de postura; Lohmann LSL es blanca de 84; Criolla, 74; Azur, 84.

COMO SE VERIFICA. Las 540 filas cargadas coinciden una a una con el Excel del cliente.',
         4, DATE '2026-08-20', 'santa-reyes,italapp,H1,backend,guia-genetica'),

        -- ── Dia 3 · viernes 21 de agosto de 2026 ──
        (3, 2, 18, 'ALTA',
         'F2.2 Asociacion de la linea genetica al lote y uso de la guia en indicadores y reportes',
         'Actividad F2.2 · hito H1 · dia 3, viernes 21 de agosto de 2026 · 6 h.

OBSERVACION DEL CLIENTE. Archivo Guias Geneticas.xlsx: comparativo del real contra la guia.

QUE SE ENTREGA. Al crear o editar un lote se elige su linea genetica. A partir de ahi, los indicadores y los reportes muestran el comparativo del real contra la guia (produccion, mortalidad acumulada y consumo por ave) usando la curva de la linea de ese lote y no una generica.

COMO SE VERIFICA. Dos lotes con lineas distintas comparan contra curvas distintas en la misma pantalla.',
         6, DATE '2026-08-21', 'santa-reyes,italapp,H1,backend,frontend,guia-genetica,reportes'),

        (4, 1, 19, 'ALTA',
         'F3.1 Parametros de levante por raza: 8 semanas de alistamiento y 16 semanas de levante',
         'Actividad F3.1 · hito H1 · dia 3, viernes 21 de agosto de 2026 · 4 h.

OBSERVACION DEL CLIENTE. Consumo de alimento: se tienen que configurar las semanas de produccion.

QUE SE ENTREGA. Las semanas del ciclo dejan de estar fijas dentro de la aplicacion y pasan a ser parametros por raza: 8 semanas de alistamiento y 16 de levante. La etapa del lote se calcula a partir de ese parametro.

COMO SE VERIFICA. Cambiar el parametro de una raza mueve la etapa calculada del lote sin tocar codigo ni desplegar.',
         4, DATE '2026-08-21', 'santa-reyes,italapp,H1,backend,frontend,ciclo'),

        -- ── Dia 4 · lunes 24 de agosto de 2026 ──
        (4, 2, 20, 'ALTA',
         'F3.2 Parametros de produccion: 4 semanas de levante en granja de produccion mas 74 semanas de postura (rojas y criollas) u 84 (blancas y Azur)',
         'Actividad F3.2 · hito H2 · dia 4, lunes 24 de agosto de 2026 · 6 h.

OBSERVACION DEL CLIENTE. Consumo de alimento: configuracion diferenciada por color de ave.

QUE SE ENTREGA. El tramo de produccion queda parametrizado por tipo de ave: 4 semanas de levante en granja de produccion mas 74 semanas de postura para rojas y criollas, u 84 para blancas y Azur. De esos numeros salen el fin de ciclo y las alertas de edad del lote.

COMO SE VERIFICA. Un lote de ave blanca en la semana 88 sigue dentro del ciclo y en la 89 queda fuera; uno de ave roja sigue dentro en la 78 y queda fuera en la 79.',
         6, DATE '2026-08-24', 'santa-reyes,italapp,H2,backend,frontend,ciclo'),

        (5, 1, 21, 'ALTA',
         'F4.1 Retirar el consumo de machos del seguimiento diario de produccion',
         'Actividad F4.1 · hito H2 · dia 4, lunes 24 de agosto de 2026 · 4 h.

OBSERVACION DEL CLIENTE. Consumo de alimento: se deben eliminar u ocultar las opciones de consumos para machos, solo dejar hembras.

QUE SE ENTREGA. En el seguimiento diario de produccion, el bloque de consumo de machos deja de mostrarse y de pedirse: el registro se guarda solo con el consumo de hembras. La regla la decide el backend a partir de la bandera de empresa, no la pantalla.

COMO SE VERIFICA. En Santa Reyes el formulario no ofrece consumo de machos y el registro guarda sin reclamarlo; en las demas empresas el bloque sigue igual que hoy.',
         4, DATE '2026-08-24', 'santa-reyes,italapp,H2,backend,frontend,seguimiento'),

        -- ── Dia 5 · martes 25 de agosto de 2026 ──
        (5, 2, 22, 'ALTA',
         'F4.2 Retirar el consumo de machos del seguimiento diario de levante',
         'Actividad F4.2 · hito H2 · dia 5, martes 25 de agosto de 2026 · 4 h.

OBSERVACION DEL CLIENTE. Levantes: se debe eliminar la parte de machos.

QUE SE ENTREGA. Lo mismo que F4.1, ahora en el seguimiento diario de levante: el bloque de consumo de machos deja de mostrarse y de pedirse, y el registro queda expresado solo sobre hembras.

COMO SE VERIFICA. En Santa Reyes el formulario de levante no ofrece consumo de machos y el registro guarda; en las demas empresas nada cambia.',
         4, DATE '2026-08-25', 'santa-reyes,italapp,H2,backend,frontend,seguimiento'),

        (6, 1, 23, 'MEDIA',
         'F5.1 Retirar el concepto de error de sexaje del registro diario',
         'Actividad F5.1 · hito H2 · dia 5, martes 25 de agosto de 2026 · 3 h.

OBSERVACION DEL CLIENTE. Mortalidades: desaparece el concepto de error de sexaje.

QUE SE ENTREGA. El concepto sale del registro diario de Santa Reyes: no se muestra, no se pide y no participa de los totales que ve la empresa. Se retira de la captura sin alterar los saldos ya registrados ni el comportamiento de las demas empresas, que lo siguen usando.

COMO SE VERIFICA. El formulario de Santa Reyes no lo ofrece y los saldos de aves del lote siguen cuadrando antes y despues del cambio.',
         3, DATE '2026-08-25', 'santa-reyes,italapp,H2,frontend,backend,seguimiento'),

        (6, 2, 24, 'MEDIA',
         'F5.2 Ocultar la columna de machos en mortalidad, seleccion, peso y uniformidad',
         'Actividad F5.2 · hito H2 · dia 5, martes 25 de agosto de 2026 · 3 h.

OBSERVACION DEL CLIENTE. Mortalidades: ocultar o eliminar la parte de machos.

QUE SE ENTREGA. Las cuatro capturas dejan de mostrar la columna de machos, y las tablas y totales asociados quedan expresados solo sobre hembras.

COMO SE VERIFICA. Las cuatro pantallas y sus exportaciones a Excel salen sin la columna, y los totales de hembras no cambian.',
         3, DATE '2026-08-25', 'santa-reyes,italapp,H2,frontend,seguimiento'),

        -- ── Dia 6 · miercoles 26 de agosto de 2026 ──
        (6, 3, 25, 'MEDIA',
         'F5.3 Campo machos sobre el total de las aves en el registro de ventas',
         'Actividad F5.3 · hito H2 · dia 6, miercoles 26 de agosto de 2026 · 3 h.

OBSERVACION DEL CLIENTE. Mortalidades: que en ventas aparezca campo machos sobre el total de las aves.

QUE SE ENTREGA. El registro de ventas gana el campo que informa cuantos machos van sobre el total de aves vendidas, con su reflejo en el listado de ventas y en el comprobante.

COMO SE VERIFICA. Registrar una venta con machos y ver el dato en el listado y en el comprobante impreso.',
         3, DATE '2026-08-26', 'santa-reyes,italapp,H2,backend,frontend,ventas'),

        (7, 1, 26, 'MEDIA',
         'F6.1 Limitar los tipos de item de inventario a Alimento y Aves',
         'Actividad F6.1 · hito H2 · dia 6, miercoles 26 de agosto de 2026 · 3 h.

OBSERVACION DEL CLIENTE. Levantes: se deben actualizar los tipos de inventarios que maneja la compania asi: Alimento, Aves.

QUE SE ENTREGA. El desplegable de tipo de item del catalogo de inventario queda acotado a Alimento y Aves para Santa Reyes. Las demas empresas conservan todos sus tipos.

COMO SE VERIFICA. Crear un item de inventario en Santa Reyes ofrece exactamente dos tipos; en otra empresa, los de siempre.',
         3, DATE '2026-08-26', 'santa-reyes,italapp,H2,backend,frontend,inventario'),

        (8, 1, 27, 'ALTA',
         'F7.1 Renombrar huevos incubables a huevos sin clasificar en formularios, tablas y reportes',
         'Actividad F7.1 · hito H2 · dia 6, miercoles 26 de agosto de 2026 · 4 h.

OBSERVACION DEL CLIENTE. Produccion de huevos: lo que se llama huevos incubables, cambiarlo por huevos sin clasificar.

QUE SE ENTREGA. El rotulo cambia en todos los puntos donde se ve: formulario de produccion, grillas de seguimiento, panel de eficiencia, indicadores y exportaciones a Excel. Es un cambio de nomenclatura: ningun dato ya registrado se mueve ni se recalcula.

COMO SE VERIFICA. Barrido de pantallas y de los Excel exportados sin rastro del rotulo anterior en Santa Reyes, y con los totales historicos intactos.',
         4, DATE '2026-08-26', 'santa-reyes,italapp,H2,frontend,reportes,huevos'),

        -- ── Dia 7 · jueves 27 de agosto de 2026 ──
        (8, 2, 28, 'ALTA',
         'F7.2 Los 7 items de huevo sin clasificar: rojo, blanco, criollo, gallina feliz, Azur, Boneg y libre de jaula',
         'Actividad F7.2 · hito H3 · dia 7, jueves 27 de agosto de 2026 · 6 h.

OBSERVACION DEL CLIENTE. Produccion de huevos: lista desplegable de huevo sin clasificar.

QUE SE ENTREGA. El registro de produccion captura el huevo sin clasificar por item, con los 7 del cliente en una lista desplegable: rojo, blanco, criollo, gallina feliz, Azur, Boneg y libre de jaula. Cada item queda trazado por separado en el seguimiento y en el reporte, y todos suman al total del dia.

COMO SE VERIFICA. Cargar un dia con varios items y ver tanto el desglose por item como el total, que tiene que coincidir con la suma.',
         6, DATE '2026-08-27', 'santa-reyes,italapp,H3,backend,frontend,huevos'),

        (8, 3, 29, 'ALTA',
         'F7.3 Huevo de primera postura con seleccion de raza, y definicion de los huevos que producira el lote al crearlo',
         'Actividad F7.3 · hito H3 · dia 7, jueves 27 de agosto de 2026 · 4 h.

OBSERVACION DEL CLIENTE. Produccion de huevos: huevo de primera postura, este si debe tener lista desplegable por raza.

QUE SE ENTREGA. El huevo de primera postura se registra eligiendo la raza en una lista desplegable. Ademas, al crear el lote se define que huevos va a producir, de modo que el formulario diario ofrezca solo esos y no todo el catalogo.

COMO SE VERIFICA. Un lote configurado con dos huevos ofrece exactamente esos dos en la captura diaria; la primera postura pide la raza antes de guardar.',
         4, DATE '2026-08-27', 'santa-reyes,italapp,H3,backend,frontend,huevos'),

        -- ── Dia 8 · viernes 28 de agosto de 2026 ──
        (8, 4, 30, 'ALTA',
         'F7.4 Regla de vigencia: primera postura habilitada hasta el ultimo dia de la semana 22 y deshabilitada desde el primer dia de la semana 23',
         'Actividad F7.4 · hito H3 · dia 8, viernes 28 de agosto de 2026 · 3 h.

OBSERVACION DEL CLIENTE. Produccion de huevos: mostrar primera postura hasta el ultimo dia de la semana 22.

QUE SE ENTREGA. El huevo de primera postura queda disponible hasta el ultimo dia de la semana 22 del lote y deja de estarlo desde el primer dia de la semana 23. El corte lo decide el backend a partir de la edad del lote, no la pantalla, de modo que la carga masiva y la aplicacion movil respetan la misma regla.

COMO SE VERIFICA. Un lote en el ultimo dia de la semana 22 lo ofrece y al dia siguiente ya no. Queda cubierto por prueba automatizada en F11.1.',
         3, DATE '2026-08-28', 'santa-reyes,italapp,H3,backend,frontend,huevos'),

        (9, 1, 31, 'MEDIA',
         'F8.1 Renombrar los productos no conformes: Manchado, Decolorado, Enyemado, Picado y Farfara',
         'Actividad F8.1 · hito H3 · dia 8, viernes 28 de agosto de 2026 · 2 h.

OBSERVACION DEL CLIENTE. Produccion de huevos: se deben renombrar los codigos para los productos no conformes.

QUE SE ENTREGA. Los productos no conformes pasan a llamarse Manchado, Decolorado, Enyemado, Picado y Farfara en la captura, en las tablas, en el panel de eficiencia y en los reportes. El renombre va por catalogo, de forma que la informacion historica se sigue leyendo y las demas empresas conservan su nomenclatura.

COMO SE VERIFICA. Las cinco etiquetas nuevas en pantalla y en los Excel, con el historico previo intacto.',
         2, DATE '2026-08-28', 'santa-reyes,italapp,H3,backend,frontend,huevos'),

        (9, 2, 32, 'MEDIA',
         'F8.2 Retirar huevo tratado, peso promedio y tipo de alimento del registro de produccion',
         'Actividad F8.2 · hito H3 · dia 8, viernes 28 de agosto de 2026 · 2 h.

OBSERVACION DEL CLIENTE. Produccion de huevos: no se usa el campo de huevo tratado, y tampoco se necesita peso promedio ni tipo de alimento.

QUE SE ENTREGA. Los tres campos salen del formulario diario de produccion de Santa Reyes, junto con sus columnas en las grillas y exportaciones de la empresa.

COMO SE VERIFICA. El formulario queda sin los tres campos y el registro diario guarda igual.',
         2, DATE '2026-08-28', 'santa-reyes,italapp,H3,frontend,seguimiento'),

        (9, 3, 33, 'ALTA',
         'F8.3 Panel de eficiencia con la nueva nomenclatura y cuadre de la suma de huevos contra el total de produccion de la granja',
         'Actividad F8.3 · hito H3 · dia 8, viernes 28 de agosto de 2026 · 3 h.

OBSERVACION DEL CLIENTE. Produccion de huevos: la suma de todos los huevos debe ser el total de produccion de la granja.

QUE SE ENTREGA. El panel de eficiencia se reexpresa con la nomenclatura nueva (sin clasificar, primera postura y los cinco no conformes) y se agrega el cuadre: la suma de todos los huevos registrados tiene que dar exactamente el total de produccion de la granja, mostrando el descuadre si aparece en lugar de esconderlo.

COMO SE VERIFICA. En un dia cargado con varios items y varios no conformes, la suma de los items da el total de la granja. Queda cubierto por prueba automatizada en F11.1.',
         3, DATE '2026-08-28', 'santa-reyes,italapp,H3,backend,frontend,huevos,reportes'),

        -- ── Dia 9 · lunes 31 de agosto de 2026 ──
        (10, 1, 34, 'MEDIA',
         'F9.1 Ocultar machos en el traslado de aves',
         'Actividad F9.1 · hito H4 · dia 9, lunes 31 de agosto de 2026 · 2 h.

OBSERVACION DEL CLIENTE. Traslado de aves: ocultar la parte de machos.

QUE SE ENTREGA. El modal de traslado de aves deja de mostrar el bloque de machos: el traslado se registra solo sobre hembras.

COMO SE VERIFICA. Trasladar aves entre dos lotes sin ver el bloque, con el saldo del origen y del destino cuadrando despues del movimiento.',
         2, DATE '2026-08-31', 'santa-reyes,italapp,H4,frontend,traslados'),

        (10, 2, 35, 'MEDIA',
         'F9.2 Campos de transporte: placa, precinto y conductor, con reflejo en el listado y en el comprobante',
         'Actividad F9.2 · hito H4 · dia 9, lunes 31 de agosto de 2026 · 3 h.

OBSERVACION DEL CLIENTE. Traslado de aves: colocar campos para la placa, el precinto y el conductor.

QUE SE ENTREGA. El traslado de aves captura la placa del vehiculo, el precinto y el nombre del conductor. Los tres datos viajan al listado de traslados y al comprobante que se imprime y se entrega con el despacho.

COMO SE VERIFICA. Registrar un traslado con los tres datos y verlos tanto en el listado como en el comprobante.',
         3, DATE '2026-08-31', 'santa-reyes,italapp,H4,backend,frontend,traslados'),

        (11, 1, 36, 'MEDIA',
         'F10.1 Bodega de salida como lista desplegable con las opciones de traslado de la granja, sin digitacion libre',
         'Actividad F10.1 · hito H4 · dia 9, lunes 31 de agosto de 2026 · 3 h.

OBSERVACION DEL CLIENTE. Traslado de huevos: debe ser una lista desplegable, de solo las opciones de traslado que tenga la granja.

QUE SE ENTREGA. La bodega de salida deja de digitarse a mano y pasa a ser una lista con los destinos de traslado que tiene la granja. Se elimina la digitacion libre para que el dato quede homologado con la estructura fisica y con los codigos del ERP definidos en F1.2.

COMO SE VERIFICA. La lista trae solo los destinos de esa granja y el formulario no acepta texto libre.',
         3, DATE '2026-08-31', 'santa-reyes,italapp,H4,backend,frontend,traslados'),

        (11, 2, 37, 'MEDIA',
         'F10.2 Tipos de huevo del traslado alineados al nuevo catalogo: sin clasificar, primera postura y no conformes',
         'Actividad F10.2 · hito H4 · dia 9, lunes 31 de agosto de 2026 · 2 h.

OBSERVACION DEL CLIENTE. Traslado de huevos: los tipos de huevo deben actualizarse con estos ya mencionados.

QUE SE ENTREGA. El traslado de huevos ofrece los mismos tipos que quedaron definidos en produccion: los 7 items de huevo sin clasificar, la primera postura por raza y los 5 productos no conformes. Un solo catalogo para captura y para traslado.

COMO SE VERIFICA. Los tipos disponibles en el traslado coinciden uno a uno con los de la captura diaria.',
         2, DATE '2026-08-31', 'santa-reyes,italapp,H4,backend,frontend,traslados,huevos'),

        -- ── Dia 10 · martes 1 de septiembre de 2026 ──
        (12, 1, 38, 'CRITICA',
         'F11.1 Pruebas automatizadas de los calculos y reglas nuevas',
         'Actividad F11.1 · hito H4 · dia 10, martes 1 de septiembre de 2026 · 3 h.

OBSERVACION DEL CLIENTE. Control de calidad.

QUE SE ENTREGA. Bateria de pruebas automatizadas de las reglas nuevas: semanas de ciclo por raza (F3.1 y F3.2), vigencia del huevo de primera postura hasta la semana 22 (F7.4), cuadre de la suma de huevos contra el total de la granja (F8.3), consumo solo hembras (F4.1 y F4.2) y resolucion de la empresa por dato. Las pruebas corren en el pipeline y bloquean el despliegue si alguna falla.

COMO SE VERIFICA. La suite pasa en el pipeline y la compuerta queda activa: sin pruebas verdes no hay despliegue.',
         3, DATE '2026-09-01', 'santa-reyes,italapp,H4,tests,backend'),

        (12, 2, 39, 'CRITICA',
         'F11.2 Pruebas de no regresion sobre las empresas ya productivas (Sanmarino, Panama y Ecuador)',
         'Actividad F11.2 · hito H4 · dia 10, martes 1 de septiembre de 2026 · 3 h.

OBSERVACION DEL CLIENTE. Control de calidad: garantizar cero impacto en las empresas actuales.

QUE SE ENTREGA. Recorrido de no regresion con las banderas apagadas: seguimientos, saldos, indicadores y reportes de Sanmarino, Panama y Ecuador tienen que dar exactamente lo mismo que antes del cambio, comparado fila a fila.

CRITERIO DE ACEPTACION. Cero diferencias en las tres empresas. Cualquier diferencia se justifica por escrito antes de desplegar.',
         3, DATE '2026-09-01', 'santa-reyes,italapp,H4,tests,multiempresa'),

        (12, 3, 40, 'ALTA',
         'F11.3 Pruebas asistidas con el usuario de Santa Reyes sobre datos reales',
         'Actividad F11.3 · hito H4 · dia 10, martes 1 de septiembre de 2026 · 2 h.

OBSERVACION DEL CLIENTE. Aceptacion del cliente.

QUE SE ENTREGA. Sesion acompanada con el usuario de Santa Reyes recorriendo el circuito completo sobre datos reales: ingreso a granja con silos y codigos ERP, creacion de lote con linea genetica, seguimiento diario de levante y de produccion, traslado de aves y de huevos, y reportes.

COMO SE VERIFICA. El usuario confirma pantalla por pantalla; lo que quede observado se corrige antes del despliegue de F12.1.',
         2, DATE '2026-09-01', 'santa-reyes,italapp,H4,tests,aceptacion'),

        (13, 1, 41, 'CRITICA',
         'F12.1 Despliegue a produccion y verificacion posterior',
         'Actividad F12.1 · hito H4 · dia 10, martes 1 de septiembre de 2026 · 2 h. Cierra la entrega.

OBSERVACION DEL CLIENTE. Puesta en marcha.

QUE SE ENTREGA. Despliegue a produccion y verificacion posterior: que la version desplegada sea efectivamente la que se pretendia, que las migraciones hayan corrido y que las banderas de comportamiento queden encendidas en Santa Reyes y apagadas en las demas empresas.

COMO SE VERIFICA. La verificacion posterior confirma los tres puntos: imagen desplegada, migraciones aplicadas y banderas por empresa.',
         2, DATE '2026-09-01', 'santa-reyes,italapp,H4,devops,despliegue')
    ) AS s(p, m, orden, prioridad, titulo, descripcion, horas, fecha, etiquetas)
    JOIN public.ticket_tareas padre
      ON padre.codigo = v_his_codigo || '-T' || s.p
     AND padre.deleted_at IS NULL
    WHERE NOT EXISTS (
        SELECT 1 FROM public.ticket_tareas x
        WHERE x.codigo = v_his_codigo || '-T' || s.p || '-S' || s.m
    );

    RAISE NOTICE 'Plan Italapp Santa Reyes sembrado: historia % / caso % / empresa % / solicitante %',
                 v_his_codigo, v_ticket_id, v_company, COALESCE(v_lenin_nom, '(sin delegar)');
END $$;
";

        private const string DOWN_SQL = @"
DO $$
DECLARE
    c_his_titulo constant varchar(200) :=
        'Implementacion de Italapp para Santa Reyes (requerimientos del cliente + guias geneticas)';
    c_tk_titulo  constant varchar(160) :=
        'Requerimientos de Italapp para Santa Reyes: 100 horas en 10 jornadas, entrega el 1 de septiembre de 2026';
    v_historia_id bigint;
    v_his_codigo  varchar(40);
    v_ticket_id   bigint;
BEGIN
    SELECT h.id, h.codigo INTO v_historia_id, v_his_codigo
    FROM public.historias h WHERE h.titulo = c_his_titulo LIMIT 1;

    SELECT t.id INTO v_ticket_id
    FROM public.tickets t WHERE t.titulo = c_tk_titulo LIMIT 1;

    -- Subtareas primero: parent_tarea_id es ON DELETE RESTRICT.
    IF v_historia_id IS NOT NULL AND v_his_codigo IS NOT NULL THEN
        DELETE FROM public.ticket_tareas
         WHERE historia_id = v_historia_id AND codigo LIKE v_his_codigo || '-T%-S%';
        DELETE FROM public.ticket_tareas
         WHERE historia_id = v_historia_id AND codigo LIKE v_his_codigo || '-T%';
    END IF;

    IF v_ticket_id IS NOT NULL THEN
        DELETE FROM public.ticket_notas
         WHERE ticket_id = v_ticket_id AND tipo_evento = 'SISTEMA_SOLICITANTE';
        DELETE FROM public.tickets WHERE id = v_ticket_id;
    END IF;

    IF v_historia_id IS NOT NULL THEN
        DELETE FROM public.historias WHERE id = v_historia_id;
    END IF;
END $$;
";
    }
}
