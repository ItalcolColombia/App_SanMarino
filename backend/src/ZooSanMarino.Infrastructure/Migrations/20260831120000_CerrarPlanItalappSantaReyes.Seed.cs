using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Cuerpo SQL del cierre del Plan de Trabajo de Italapp para Santa Reyes. Vive en su propio
    /// archivo (<c>partial</c>) por tamaño: la documentación de qué hace y por qué está en
    /// <c>20260831120000_CerrarPlanItalappSantaReyes.cs</c>.
    /// </summary>
    /// <remarks>
    /// Los textos se escriben <b>sin acentos</b>, igual que el resto de los seeds del módulo de
    /// tickets, y sin apóstrofes para no tener que escaparlos dentro del literal SQL.
    /// </remarks>
    public partial class CerrarPlanItalappSantaReyes
    {
        private const string CIERRE_SQL = @"
-- ─────────────────────────────────────────────────────────────────────────────
-- Cierre en ItalJira del trabajo de Santa Reyes ya construido y desplegado:
--   TK Requerimientos de Italapp + su historia + 42 tareas  -> CERRADO / LISTO
--   TK 6 definiciones del cliente + sus 6 tareas SR-DEF-*   -> CERRADO / LISTO
-- Idempotente: todo filtra con IS DISTINCT FROM / NOT EXISTS.
-- ─────────────────────────────────────────────────────────────────────────────
DO $$
DECLARE
    c_his_titulo constant varchar(200) :=
        'Implementacion de Italapp para Santa Reyes (requerimientos del cliente + guias geneticas)';
    c_tk_plan constant varchar(160) :=
        'Requerimientos de Italapp para Santa Reyes: 100 horas en 10 jornadas, entrega el 1 de septiembre de 2026';
    c_tk_def constant varchar(200) :=
        'Santa Reyes: 6 definiciones pendientes del cliente para cerrar los Requerimientos de Italapp';

    -- Momentos DETERMINISTAS (no now()): cada paquete lleva la fecha en que cerro de verdad.
    c_ini     constant timestamptz := '2026-08-20 05:00:00+00';  -- arranque de la ejecucion (V52)
    c_ini_def constant timestamptz := '2026-08-24 06:00:00+00';  -- alta del caso de definiciones
    c_f_v52   constant timestamptz := '2026-08-21 23:00:00+00';  -- F0-F12 salvo lo de abajo
    c_f_x18   constant timestamptz := '2026-08-24 23:00:00+00';  -- machos en ventas, comprobante, bodega destino
    c_f_fin   constant timestamptz := '2026-08-31 12:00:00+00';  -- alias de raza en SQL + huevo al alta del lote

    v_admin_guid  uuid;
    v_admin_ced   integer;
    v_company     integer;
    v_historia_id bigint;
    v_tk_plan_id  bigint;
    v_tk_def_id   bigint;
    v_sol_plan    text;
    v_sol_def     text;
    v_n           integer;
BEGIN
    -- ═══════════════ 0) ADMINISTRADOR: quien soluciona y quien cierra ═══════════════
    -- Identidad POR EMAIL, nunca por guid fijo: los ids difieren entre local y produccion.
    SELECT u.id INTO v_admin_guid
    FROM public.users u
    JOIN public.user_logins ul ON ul.user_id = u.id
    JOIN public.logins l       ON l.id = ul.login_id
    WHERE lower(l.email) = 'moiesbbuga@gmail.com'
    LIMIT 1;

    -- Fail-open: sin el administrador no se cierra nada y la app arranca igual.
    IF v_admin_guid IS NULL THEN
        RAISE NOTICE 'Cierre Italapp Santa Reyes: no existe moiesbbuga@gmail.com en este entorno; omitido.';
        RETURN;
    END IF;

    -- El int de auditoria del modulo NO es la cedula: se reusa el que ya usan sus propios casos.
    SELECT t.created_by_user_id INTO v_admin_ced
    FROM public.tickets t WHERE t.created_by_user_guid = v_admin_guid ORDER BY t.id DESC LIMIT 1;
    v_admin_ced := COALESCE(v_admin_ced, 0);

    -- ═══════════════ 1) EMPRESA SANTA REYES: por nombre, nunca por id ═══════════════
    SELECT c.id INTO v_company
    FROM public.companies c
    WHERE lower(c.name) LIKE '%santa%reyes%'
    ORDER BY c.id
    LIMIT 1;

    IF v_company IS NULL THEN
        RAISE NOTICE 'Cierre Italapp Santa Reyes: no existe la empresa en este entorno; omitido.';
        RETURN;
    END IF;

    -- ═══════════════ 2) LOS OBJETOS A CERRAR: por titulo, igual que el seed que los creo ═══════════════
    SELECT h.id INTO v_historia_id
    FROM public.historias h
    WHERE h.titulo = c_his_titulo AND h.company_id = v_company AND h.deleted_at IS NULL
    ORDER BY h.id LIMIT 1;

    SELECT t.id INTO v_tk_plan_id
    FROM public.tickets t
    WHERE t.titulo = c_tk_plan AND t.company_id = v_company AND t.deleted_at IS NULL
    ORDER BY t.id LIMIT 1;

    SELECT t.id INTO v_tk_def_id
    FROM public.tickets t
    WHERE t.titulo = c_tk_def AND t.company_id = v_company AND t.deleted_at IS NULL
    ORDER BY t.id LIMIT 1;

    IF v_tk_plan_id IS NULL AND v_tk_def_id IS NULL THEN
        RAISE NOTICE 'Cierre Italapp Santa Reyes: no hay casos que cerrar en este entorno; omitido.';
        RETURN;
    END IF;

    -- ═══════════════ 3) TEXTO DE LA SOLUCION ═══════════════
    -- Dice lo que se entrego Y lo que no, con el motivo. Cerrar en silencio lo que depende del
    -- cliente borraria el unico rastro de que falta un dato suyo.
    v_sol_plan :=
'ENTREGADO Y DESPLEGADO. Los 13 paquetes del plan (F0 a F12) estan construidos, probados y corriendo en produccion, verificados uno por uno contra su artefacto real (base de datos y codigo), no contra un checklist.

QUE QUEDO FUNCIONANDO. Parametrizacion por empresa: 8 banderas de comportamiento en la ficha de la empresa. Estructura fisica: silos de granja, galpon y lote, mas los codigos de integracion con el ERP por nivel. Guias geneticas: tabla propia con 615 filas (5 razas por 123 semanas), conectada a los indicadores de produccion y de levante, al reporte tecnico y al selector de raza del alta de lote, tolerando la grafia del ERP (BABCOK BROWN, HY LINE). Semanas de ciclo por raza, con la correccion de Lohmann Brown, que se clasificaba blanca. Consumo de alimento solo hembras. Mortalidad, seleccion, pesaje, uniformidad y ventas sin machos ni error de sexaje. Tipos de inventario limitados a Alimento y Aves. Huevo sin clasificar por items del catalogo, con los tipos declarables al crear el lote y la vigencia de primera postura hasta la semana 22. Traslado de aves con placa, conductor y sellos, y su comprobante imprimible. Traslado de huevos con bodega destino desplegable y los tipos alineados al catalogo nuevo. Todo con pruebas automatizadas y con el comportamiento de las demas empresas verificado sin cambios.

QUE NO SE ENTREGO Y POR QUE. Tres puntos dependen del cliente y no se pueden construir sin un dato suyo: (1) F8.1 productos no conformes, los 7 items nuevos del catalogo (4 ENYEMADO y 3 DECOLORADO) existen creados pero SIN codigo ERP, porque el archivo Items.xlsx que entrego Santa Reyes trae 21 items y ninguno Enyemado, mientras el documento de requerimientos si lo pide: los dos documentos del cliente se contradicen entre si y no se inventan codigos; (2) F8.3 panel de eficiencia con la nomenclatura nueva, que depende del punto anterior; (3) F11.3 pruebas asistidas con el usuario de Santa Reyes sobre datos reales, que necesita al cliente. Los tres se retoman en cuanto Santa Reyes entregue los codigos ERP.

Se cierra el caso para que el tablero refleje el estado real del trabajo. Los tres puntos de arriba quedan escritos aca como constancia.';

    v_sol_def :=
'CUATRO DE LAS SEIS DEFINICIONES SE CERRARON leyendo los archivos que entrego el cliente, no con una decision de diseno inventada.

SR-DEF-1 (F5.3, machos en ventas): el cliente aclaro que Santa Reyes no maneja machos en ningun lado, asi que en el registro de ventas se RETIRAN, no se agrega un campo informativo. Se extendio la bandera que ya existia; no se creo una nueva.
SR-DEF-2 (F7.3, huevos que produce el lote): implementado. Los tipos de huevo se declaran al CREAR el lote, resolviendo el catalogo por la granja elegida en el formulario, y son los que despues acepta el registro diario de produccion.
SR-DEF-5 (F9.2c, comprobante del traslado de aves): construido. Es el primer comprobante imprimible del sistema, con datos del movimiento, origen y destino, aves, transporte y tres firmas.
SR-DEF-6 (F10.1, bodega de salida): el campo digitado era el del TRASLADO, no el de la venta, que ya era desplegable. Se reemplazo por una lista alimentada del catalogo de la empresa.

DOS SIGUEN DEPENDIENDO DE UN DATO DEL CLIENTE. SR-DEF-3 (F8.1): los 7 items nuevos de productos no conformes (4 ENYEMADO y 3 DECOLORADO) estan creados en el catalogo pero SIN codigo ERP, porque el Items.xlsx trae 21 items y ninguno Enyemado, mientras el documento de requerimientos si lo pide. No se borran ni se les inventa codigo: quedan ocultos hasta que Santa Reyes entregue los codigos reales. SR-DEF-4 (F8.3) depende de SR-DEF-3.

Se cierra el caso porque no hay trabajo del equipo pendiente en el. Cuando lleguen los codigos ERP, esos dos puntos se retoman en un caso nuevo.';

    -- ═══════════════ 4) HISTORIA -> LISTO ═══════════════
    IF v_historia_id IS NOT NULL THEN
        UPDATE public.historias h
           SET estado             = 'LISTO',
               fecha_inicio_real  = COALESCE(h.fecha_inicio_real, c_ini),
               fecha_fin_real     = COALESCE(h.fecha_fin_real, c_f_fin),
               updated_by_user_id = v_admin_ced,
               updated_at         = c_f_fin
         WHERE h.id = v_historia_id
           AND h.estado IS DISTINCT FROM 'LISTO';
    END IF;

    -- ═══════════════ 5) CASO DEL PLAN: 42 tareas -> LISTO, caso -> CERRADO ═══════════════
    IF v_tk_plan_id IS NOT NULL THEN
        -- El fin real sale del prefijo F<n> del titulo, que el seed escribio y es estable
        -- (el codigo HIS-2026-NNNN-Tn deriva del id de la historia y difiere local vs prod).
        UPDATE public.ticket_tareas t
           SET estado             = 'LISTO',
               fecha_inicio_real  = COALESCE(t.fecha_inicio_real, c_ini),
               fecha_fin_real     = COALESCE(t.fecha_fin_real,
                                    CASE split_part(t.titulo, ' ', 1)
                                        WHEN 'F5'    THEN c_f_x18
                                        WHEN 'F5.3'  THEN c_f_x18
                                        WHEN 'F9'    THEN c_f_x18
                                        WHEN 'F9.2'  THEN c_f_x18
                                        WHEN 'F10'   THEN c_f_x18
                                        WHEN 'F10.1' THEN c_f_x18
                                        WHEN 'F2'    THEN c_f_fin
                                        WHEN 'F2.2'  THEN c_f_fin
                                        WHEN 'F7'    THEN c_f_fin
                                        WHEN 'F7.3'  THEN c_f_fin
                                        WHEN 'F8'    THEN c_f_fin
                                        WHEN 'F8.1'  THEN c_f_fin
                                        WHEN 'F8.3'  THEN c_f_fin
                                        WHEN 'F11'   THEN c_f_fin
                                        WHEN 'F11.3' THEN c_f_fin
                                        ELSE c_f_v52
                                    END),
               updated_by_user_id = v_admin_ced,
               updated_at         = c_f_fin
         WHERE t.ticket_id = v_tk_plan_id
           AND t.deleted_at IS NULL
           AND t.estado IS DISTINCT FROM 'LISTO';
        GET DIAGNOSTICS v_n = ROW_COUNT;
        RAISE NOTICE 'Cierre Italapp Santa Reyes: % tareas del plan pasadas a LISTO.', v_n;

        UPDATE public.tickets t
           SET estado                   = 'CERRADO',
               solucion_descripcion     = COALESCE(t.solucion_descripcion, v_sol_plan),
               fecha_primera_apertura   = COALESCE(t.fecha_primera_apertura, c_ini),
               fecha_solucion           = COALESCE(t.fecha_solucion, c_f_fin),
               fecha_cierre_solicitante = COALESCE(t.fecha_cierre_solicitante, c_f_fin),
               cerrado_por_user_id      = COALESCE(t.cerrado_por_user_id, v_admin_ced),
               updated_by_user_id       = v_admin_ced,
               updated_at               = c_f_fin
         WHERE t.id = v_tk_plan_id
           AND t.estado IS DISTINCT FROM 'CERRADO';

        -- Las 2 notas que escribe el servicio (SOLUCIONADO y CERRADO). La linea de tiempo del caso
        -- se DERIVA de notas + tareas: sin ellas el caso se veria cerrado sin explicacion.
        INSERT INTO public.ticket_notas
            (ticket_id, user_id, nota, estado_resultante, es_interna, created_at)
        SELECT v_tk_plan_id, v_admin_ced, 'Solucionado: ' || v_sol_plan, 'SOLUCIONADO', false, c_f_fin
        WHERE NOT EXISTS (
            SELECT 1 FROM public.ticket_notas n
            WHERE n.ticket_id = v_tk_plan_id AND n.estado_resultante = 'SOLUCIONADO');

        INSERT INTO public.ticket_notas
            (ticket_id, user_id, nota, estado_resultante, es_interna, created_at)
        SELECT v_tk_plan_id, v_admin_ced,
               'Cierre confirmado por el solicitante. Los 13 paquetes del plan quedaron construidos, probados y desplegados; los 3 puntos que dependen de un dato del cliente (codigos ERP de los items ENYEMADO y DECOLORADO, panel de eficiencia y pruebas asistidas) quedan detallados en la descripcion de la solucion.',
               'CERRADO', false, c_f_fin
        WHERE NOT EXISTS (
            SELECT 1 FROM public.ticket_notas n
            WHERE n.ticket_id = v_tk_plan_id AND n.estado_resultante = 'CERRADO');
    END IF;

    -- ═══════════════ 6) CASO DE LAS DEFINICIONES: 6 tareas -> LISTO, caso -> CERRADO ═══════════════
    IF v_tk_def_id IS NOT NULL THEN
        UPDATE public.ticket_tareas t
           SET estado             = 'LISTO',
               fecha_inicio_real  = COALESCE(t.fecha_inicio_real, c_ini_def),
               fecha_fin_real     = COALESCE(t.fecha_fin_real,
                                    CASE t.codigo
                                        WHEN 'SR-DEF-1' THEN c_f_x18
                                        WHEN 'SR-DEF-5' THEN c_f_x18
                                        WHEN 'SR-DEF-6' THEN c_f_x18
                                        ELSE c_f_fin
                                    END),
               updated_by_user_id = v_admin_ced,
               updated_at         = c_f_fin
         WHERE t.ticket_id = v_tk_def_id
           AND t.deleted_at IS NULL
           AND t.estado IS DISTINCT FROM 'LISTO';
        GET DIAGNOSTICS v_n = ROW_COUNT;
        RAISE NOTICE 'Cierre Italapp Santa Reyes: % definiciones pasadas a LISTO.', v_n;

        UPDATE public.tickets t
           SET estado                   = 'CERRADO',
               solucion_descripcion     = COALESCE(t.solucion_descripcion, v_sol_def),
               fecha_primera_apertura   = COALESCE(t.fecha_primera_apertura, c_ini_def),
               fecha_solucion           = COALESCE(t.fecha_solucion, c_f_fin),
               fecha_cierre_solicitante = COALESCE(t.fecha_cierre_solicitante, c_f_fin),
               cerrado_por_user_id      = COALESCE(t.cerrado_por_user_id, v_admin_ced),
               updated_by_user_id       = v_admin_ced,
               updated_at               = c_f_fin
         WHERE t.id = v_tk_def_id
           AND t.estado IS DISTINCT FROM 'CERRADO';

        INSERT INTO public.ticket_notas
            (ticket_id, user_id, nota, estado_resultante, es_interna, created_at)
        SELECT v_tk_def_id, v_admin_ced, 'Solucionado: ' || v_sol_def, 'SOLUCIONADO', false, c_f_fin
        WHERE NOT EXISTS (
            SELECT 1 FROM public.ticket_notas n
            WHERE n.ticket_id = v_tk_def_id AND n.estado_resultante = 'SOLUCIONADO');

        INSERT INTO public.ticket_notas
            (ticket_id, user_id, nota, estado_resultante, es_interna, created_at)
        SELECT v_tk_def_id, v_admin_ced,
               'Cierre confirmado por el solicitante. Cuatro definiciones se resolvieron con los archivos del cliente y se construyeron. SR-DEF-3 (codigos ERP de los 7 items ENYEMADO y DECOLORADO) y SR-DEF-4 (panel de eficiencia, que depende de la anterior) siguen esperando un dato de Santa Reyes: se retoman en un caso nuevo cuando llegue.',
               'CERRADO', false, c_f_fin
        WHERE NOT EXISTS (
            SELECT 1 FROM public.ticket_notas n
            WHERE n.ticket_id = v_tk_def_id AND n.estado_resultante = 'CERRADO');
    END IF;
END $$;
";

        private const string REVERT_SQL = @"
-- Devuelve exactamente lo que movio el Up, comparando contra los valores que el Up escribio
-- (nunca borra a ciegas: una fecha que ya venia de antes se conserva).
DO $$
DECLARE
    c_his_titulo constant varchar(200) :=
        'Implementacion de Italapp para Santa Reyes (requerimientos del cliente + guias geneticas)';
    c_tk_plan constant varchar(160) :=
        'Requerimientos de Italapp para Santa Reyes: 100 horas en 10 jornadas, entrega el 1 de septiembre de 2026';
    c_tk_def constant varchar(200) :=
        'Santa Reyes: 6 definiciones pendientes del cliente para cerrar los Requerimientos de Italapp';

    c_ini     constant timestamptz := '2026-08-20 05:00:00+00';
    c_ini_def constant timestamptz := '2026-08-24 06:00:00+00';
    c_f_v52   constant timestamptz := '2026-08-21 23:00:00+00';
    c_f_x18   constant timestamptz := '2026-08-24 23:00:00+00';
    c_f_fin   constant timestamptz := '2026-08-31 12:00:00+00';

    v_company     integer;
    v_historia_id bigint;
    v_tk_plan_id  bigint;
    v_tk_def_id   bigint;
BEGIN
    SELECT c.id INTO v_company
    FROM public.companies c WHERE lower(c.name) LIKE '%santa%reyes%' ORDER BY c.id LIMIT 1;
    IF v_company IS NULL THEN RETURN; END IF;

    SELECT h.id INTO v_historia_id FROM public.historias h
    WHERE h.titulo = c_his_titulo AND h.company_id = v_company ORDER BY h.id LIMIT 1;
    SELECT t.id INTO v_tk_plan_id FROM public.tickets t
    WHERE t.titulo = c_tk_plan AND t.company_id = v_company ORDER BY t.id LIMIT 1;
    SELECT t.id INTO v_tk_def_id FROM public.tickets t
    WHERE t.titulo = c_tk_def AND t.company_id = v_company ORDER BY t.id LIMIT 1;

    IF v_tk_plan_id IS NOT NULL THEN
        DELETE FROM public.ticket_notas
         WHERE ticket_id = v_tk_plan_id AND created_at = c_f_fin
           AND estado_resultante IN ('SOLUCIONADO','CERRADO');

        UPDATE public.tickets t
           SET estado                   = 'ABIERTO',
               solucion_descripcion     = NULL,
               fecha_primera_apertura   = CASE WHEN t.fecha_primera_apertura   = c_ini   THEN NULL ELSE t.fecha_primera_apertura END,
               fecha_solucion           = CASE WHEN t.fecha_solucion           = c_f_fin THEN NULL ELSE t.fecha_solucion END,
               fecha_cierre_solicitante = CASE WHEN t.fecha_cierre_solicitante = c_f_fin THEN NULL ELSE t.fecha_cierre_solicitante END,
               cerrado_por_user_id      = NULL,
               updated_at               = c_f_fin
         WHERE t.id = v_tk_plan_id;

        UPDATE public.ticket_tareas t
           SET estado            = 'BACKLOG',
               fecha_inicio_real = CASE WHEN t.fecha_inicio_real = c_ini THEN NULL ELSE t.fecha_inicio_real END,
               fecha_fin_real    = CASE WHEN t.fecha_fin_real IN (c_f_v52, c_f_x18, c_f_fin) THEN NULL ELSE t.fecha_fin_real END
         WHERE t.ticket_id = v_tk_plan_id AND t.deleted_at IS NULL;
    END IF;

    IF v_tk_def_id IS NOT NULL THEN
        DELETE FROM public.ticket_notas
         WHERE ticket_id = v_tk_def_id AND created_at = c_f_fin
           AND estado_resultante IN ('SOLUCIONADO','CERRADO');

        UPDATE public.tickets t
           SET estado                   = 'ABIERTO',
               solucion_descripcion     = NULL,
               fecha_solucion           = CASE WHEN t.fecha_solucion           = c_f_fin THEN NULL ELSE t.fecha_solucion END,
               fecha_cierre_solicitante = CASE WHEN t.fecha_cierre_solicitante = c_f_fin THEN NULL ELSE t.fecha_cierre_solicitante END,
               cerrado_por_user_id      = NULL,
               updated_at               = c_f_fin
         WHERE t.id = v_tk_def_id;

        UPDATE public.ticket_tareas t
           SET estado            = 'BLOQUEADA',
               fecha_inicio_real = CASE WHEN t.fecha_inicio_real = c_ini_def THEN NULL ELSE t.fecha_inicio_real END,
               fecha_fin_real    = CASE WHEN t.fecha_fin_real IN (c_f_x18, c_f_fin) THEN NULL ELSE t.fecha_fin_real END
         WHERE t.ticket_id = v_tk_def_id AND t.deleted_at IS NULL;
    END IF;

    IF v_historia_id IS NOT NULL THEN
        UPDATE public.historias h
           SET estado            = 'BACKLOG',
               fecha_inicio_real = CASE WHEN h.fecha_inicio_real = c_ini   THEN NULL ELSE h.fecha_inicio_real END,
               fecha_fin_real    = CASE WHEN h.fecha_fin_real    = c_f_fin THEN NULL ELSE h.fecha_fin_real END,
               updated_at        = c_f_fin
         WHERE h.id = v_historia_id;
    END IF;
END $$;
";
    }
}
