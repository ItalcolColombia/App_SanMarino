using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Cuerpo SQL del seed del caso de dudas con las 6 definiciones pendientes de Santa Reyes
    /// (21 de agosto de 2026). Vive en su propio archivo (<c>partial</c>) por tamaño: la
    /// documentación de qué hace y por qué está en
    /// <c>20260821190000_SeedTicketDefinicionesPendientesSantaReyes.cs</c>.
    /// </summary>
    /// <remarks>
    /// Los textos se escriben sin acentos, igual que el resto de los seeds del módulo de tickets.
    /// </remarks>
    public partial class SeedTicketDefinicionesPendientesSantaReyes
    {
        private const string TITULO_CASO =
            "Santa Reyes: 6 definiciones pendientes del cliente para cerrar los Requerimientos de Italapp";

        private const string SEED_SQL = @"
-- ─────────────────────────────────────────────────────────────────────────────
-- Un caso de DUDAS (ABIERTO) para Santa Reyes con las 6 definiciones que faltan
-- del cliente, cada una como subtarea BLOQUEADA. 21 de agosto de 2026.
-- ─────────────────────────────────────────────────────────────────────────────
DO $$
DECLARE
    v_admin_guid uuid;
    v_admin_ced  integer;
    v_company    integer;
    v_pais       integer;
    v_ahora      timestamptz := timezone('utc', now());

    c_titulo constant varchar(200) :=
        'Santa Reyes: 6 definiciones pendientes del cliente para cerrar los Requerimientos de Italapp';

    v_ticket_id bigint;
    v_orden     integer;
BEGIN
    -- ═══════════════ 0) ADMINISTRADOR: creador y solicitante del caso ═══════════════
    -- Identidad POR EMAIL, nunca por guid fijo: los ids difieren entre local y produccion.
    SELECT u.id INTO v_admin_guid
    FROM public.users u
    JOIN public.user_logins ul ON ul.user_id = u.id
    JOIN public.logins l       ON l.id = ul.login_id
    WHERE lower(l.email) = 'moiesbbuga@gmail.com'
    LIMIT 1;

    IF v_admin_guid IS NULL THEN
        RAISE NOTICE 'Definiciones pendientes Santa Reyes: no existe moiesbbuga@gmail.com en este entorno; omitido.';
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

    -- ═══════════════ 1) EMPRESA: Santa Reyes, resuelta por nombre ═══════════════
    SELECT c.id INTO v_company
    FROM public.companies c
    WHERE lower(c.name) LIKE '%santa%reyes%'
    ORDER BY c.id
    LIMIT 1;

    IF v_company IS NULL THEN
        RAISE NOTICE 'Definiciones pendientes Santa Reyes: no existe la empresa en este entorno; omitido.';
        RETURN;
    END IF;

    SELECT uc.pais_id INTO v_pais
    FROM public.user_companies uc
    WHERE uc.company_id = v_company AND uc.pais_id IS NOT NULL
    LIMIT 1;
    v_pais := COALESCE(v_pais, 1);

    -- ═══════════════ 2) EL CASO ═══════════════
    SELECT t.id INTO v_ticket_id
    FROM public.tickets t
    WHERE t.titulo = c_titulo AND t.deleted_at IS NULL
    LIMIT 1;

    IF v_ticket_id IS NULL THEN
        SELECT COALESCE(MAX(t.orden_tablero) + 1, 0) INTO v_orden
        FROM public.tickets t WHERE t.estado = 'ABIERTO' AND t.deleted_at IS NULL;

        INSERT INTO public.tickets
            (pais_id, tipo, estado, titulo, descripcion,
             assigned_to_user_guid, assigned_to_user_id,
             created_by_user_guid, solicitante_user_guid, solicitante_user_id,
             fecha_primera_apertura,
             prioridad, orden_tablero, status, notificado_correo,
             company_id, created_by_user_id, created_at)
        VALUES
            (v_pais, 'DUDAS', 'ABIERTO', c_titulo,
             'QUE ES ESTO. El requerimiento TK-2026-000172 (Requerimientos de Italapp para Santa Reyes) esta construido y validado en casi todo su alcance. Quedan 6 puntos que NO se pueden construir porque el texto del cliente admite dos lecturas que producen software distinto. Este caso los junta para poder resolverlos en una sola conversacion con Santa Reyes.

POR QUE NO SE ADIVINARON. Tres de los seis viven en modulos COMPARTIDOS con Sanmarino, Panama y Ecuador: el registro de ventas de aves (movimientos-aves, con historial propio de bugs de doble descuento), el traslado de huevos y los reportes financieros. Elegir mal ahi no deja una funcionalidad a medias: deja una regresion en produccion para las otras tres empresas, a cambio de algo que probablemente tampoco era lo que el cliente queria. La regla del proyecto es explicita: no se adivina UX en modulo compartido.

QUE SIGUE CONSTRUIDO Y ENTREGADO. Parametrizacion por empresa, estructura fisica y codigos ERP, las 5 guias geneticas (615 filas), las etapas del ciclo por raza, consumo de alimento solo hembras, mortalidad/pesaje sin machos, tipos de inventario limitados, huevo sin clasificar con sus 7 items y la vigencia de primera postura hasta la semana 22, los campos de transporte del traslado de aves y la clasificacion por items en el traslado de huevos. Con sus pruebas automatizadas.

QUE DESBLOQUEA CADA RESPUESTA. Ver las 6 subtareas: cada una trae el texto literal del cliente, las dos lecturas posibles, que hay hoy en el sistema y que dato exacto hace falta para poder construir. La subtarea de los productos no conformes (F8.1) ademas necesita un DATO que solo tiene el cliente: los codigos ERP y los nombres de los items nuevos del catalogo.',
             v_admin_guid, v_admin_ced,
             v_admin_guid, v_admin_guid, v_admin_ced,
             v_ahora,
             'ALTA', v_orden, 'A', false,
             v_company, v_admin_ced, v_ahora)
        RETURNING id INTO v_ticket_id;

        UPDATE public.tickets
           SET codigo = 'TK-2026-' || lpad(v_ticket_id::text, 6, '0')
         WHERE id = v_ticket_id;
    END IF;

    -- ═══════════════ 3) LAS 6 SUBTAREAS, una por definicion pendiente ═══════════════
    -- Estado BLOQUEADA (no BACKLOG): el tablero tiene que mostrar que no depende del equipo.
    INSERT INTO public.ticket_tareas
        (ticket_id, codigo, tipo, estado, prioridad, titulo, descripcion,
         asignado_user_guid, orden, etiquetas,
         company_id, created_by_user_id, created_at)
    SELECT v_ticket_id,
           'SR-DEF-' || t.n,
           'TAREA', 'BLOQUEADA', t.prioridad, t.titulo, t.descripcion,
           v_admin_guid, t.orden, t.etiquetas,
           v_company, v_admin_ced, v_ahora
    FROM (VALUES
        (1, 0, 'ALTA',
         'F5.3 - Campo machos sobre el total de aves en el registro de ventas',
         'TEXTO LITERAL DEL CLIENTE (Requerimientos de Italapp, seccion de mortalidad y ventas): ""Desaparece el concepto de error de sexaje, y que en ventas aparezca campo machos sobre el total de las aves"".

QUE HAY HOY. El registro de venta de aves (modal de movimientos de aves, tipo Venta) captura Cantidad Hembras y Cantidad Machos como DOS campos independientes, cada uno con su propio chequeo de disponibilidad contra el saldo del lote. No existe un campo Total.

LAS DOS LECTURAS. (a) Se reemplazan los dos campos por UN campo Total de aves, y Machos pasa a ser un campo informativo al lado (cuantos de ese total son machos). (b) Se dejan los dos campos como estan y solo se agrega una linea que muestre la proporcion machos sobre total.

QUE HACE FALTA DEFINIR. 1) Cual de las dos. 2) Si es la (a): el numero de machos, descuenta saldo de machos o es solo informativo. 3) Si es solo informativo, que pasa cuando el operario escribe un numero de machos mayor al total.

POR QUE NO SE ADIVINO. El modulo de movimientos de aves es COMPARTIDO con Sanmarino, Panama y Ecuador y ya tuvo bugs de doble descuento de aves (la venta llego a restar dos veces). Cambiar como se captura la cantidad sin saber si afecta el descuento es la forma exacta de reintroducir ese bug.',
         'santa-reyes,italapp,H2,bloqueada-por-cliente,ventas'),

        (2, 1, 'MEDIA',
         'F7.3 - Huevo de primera postura: seleccion de raza al crear el lote',
         'TEXTO LITERAL DEL CLIENTE (seccion de produccion de huevos): ""Se necesita que cuando se cree un lote poder especificar los huevos que va a producir en la etapa de produccion"".

QUE HAY HOY. Con la clasificacion por items encendida, el seguimiento diario de produccion ya deja elegir CUALQUIER item de huevo del catalogo de la empresa, agrupado en Primera y Pnc, incluidos los tres items de primeras posturas por raza (rojo, blanco, criollo). La vigencia hasta la semana 22 ya esta implementada y probada (F7.4).

LAS DOS LECTURAS. (a) Se pide un campo NUEVO en el alta del lote que restrinja que items de huevo puede producir ese lote (una lista blanca por lote), y el seguimiento diario solo ofrece esos. (b) Lo que ya existe alcanza: el lote produce lo que su raza determina y el operario elige el item en el dia a dia.

QUE HACE FALTA DEFINIR. 1) Cual de las dos. 2) Si es la (a): la lista blanca la elige el usuario item por item, o se deriva automaticamente de la raza del lote. 3) Que pasa con los lotes ya creados (se les carga la lista despues, o quedan sin restriccion).

POR QUE NO SE ADIVINO. La lectura (a) agrega una tabla nueva y una regla de validacion en el guardado del seguimiento diario; la (b) no cambia una sola linea. Construir la (a) sin necesidad es deuda permanente.',
         'santa-reyes,italapp,H3,bloqueada-por-cliente,huevos'),

        (3, 2, 'ALTA',
         'F8.1 - Productos no conformes: faltan items del catalogo (necesita codigos ERP del cliente)',
         'TEXTO DEL CLIENTE. Los productos no conformes se clasifican en 5 categorias: Manchado, Decolorado, Enyemado, Picado y Farfara.

QUE HAY HOY EN EL CATALOGO DE SANTA REYES (11 items Pnc, verificado en base el 21 de agosto de 2026). Manchado: criollo, blanco, azur y rojo (4 items). Picado: criollo, blanco, azur y rojo (4). Decolorado: SOLO rojo (1). Farfara: un unico item generico, sin raza (1). Enyemado: NINGUNO. Ademas hay un item HUEVO RECUPERACION BOLSA KIL que no pertenece a las 5 categorias.

QUE HACE FALTA. Para cada una de las 5 categorias, para cuales de las 7 lineas de primera (rojo, blanco, criollo, gallina feliz, bonegg, azur, libre de jaula certificado) debe existir el item, y por cada item nuevo: su CODIGO ERP y su NOMBRE exactos.

POR QUE NO SE ADIVINO. Los codigos del catalogo son codigos del ERP del cliente (los existentes son 537, 538, 539, 1944, 2124, 2125, 2521, 2522, 2523, 2697, 2698). Inventar un codigo crea un item que el ERP no reconoce, y la conciliacion contra el ERP falla en silencio recien cuando se cargue produccion real. Este punto no se destraba con una decision: se destraba con un dato que solo tiene Santa Reyes.',
         'santa-reyes,italapp,H3,bloqueada-por-cliente,catalogo,erp'),

        (4, 3, 'MEDIA',
         'F8.3 - Panel de eficiencia con la nueva nomenclatura y cuadre de huevos',
         'TEXTO DEL CLIENTE (parrafo 68 del documento). Pide un panel de eficiencia con la nueva nomenclatura y que la suma de los huevos cuadre con el total de la granja.

PROBLEMA 1. El texto usa la nomenclatura VIEJA (huevos incubables) en el mismo documento que pide eliminarla (F7.1, ya implementado: los items ahora se llaman HUEVO SIN CLASIFICAR). Tal como esta escrito, las dos peticiones se contradicen.

PROBLEMA 2. No existe ninguna pantalla llamada Panel de eficiencia en el sistema. Hay reportes de indicadores de produccion y reportes contables, que son reportes FINANCIEROS y estan en uso por las otras tres empresas.

QUE HACE FALTA DEFINIR. 1) Es una pantalla nueva o es un ajuste de nomenclatura sobre un reporte que ya existe. 2) Si es sobre uno que existe: cual, con nombre y ruta. 3) Que significa exactamente ""la suma de los huevos cuadra con el total de la granja"": que dos numeros tienen que dar igual, y de que pantalla sale cada uno.

POR QUE NO SE ADIVINO. Tocar un reporte financiero en uso por Sanmarino, Panama y Ecuador para adivinar una nomenclatura es riesgo puro sin beneficio.',
         'santa-reyes,italapp,H3,bloqueada-por-cliente,reportes'),

        (5, 4, 'MEDIA',
         'F9.2c - Comprobante del traslado de aves',
         'TEXTO DEL CLIENTE. El traslado de aves debe registrar placa, precinto y conductor, y emitir un comprobante.

QUE HAY HOY (implementado en F9.2 y F9.2b). El traslado de aves desde el seguimiento diario ya captura Placa, Conductor y Precinto, los guarda en el movimiento y los muestra en el listado de movimientos, en una columna Transporte.

QUE NO HAY. Ninguna pantalla, ruta ni componente de comprobante de traslado en todo el sistema.

LAS TRES LECTURAS. (a) Un PDF descargable, como el que ya se genera para otros documentos. (b) Una vista de detalle imprimible desde el navegador. (c) El listado con la columna Transporte ya alcanza y no hay nada que construir.

QUE HACE FALTA DEFINIR. 1) Cual de las tres. 2) Si es (a) o (b): que campos lleva el comprobante, si necesita numeracion propia, y si lleva firmas (de quien despacha y de quien recibe).

POR QUE NO SE ADIVINO. Un comprobante de traslado suele ser un documento con valor operativo o legal. Elegir sus campos por nuestra cuenta produce un papel que despues no sirve.',
         'santa-reyes,italapp,H4,bloqueada-por-cliente,traslados'),

        (6, 5, 'ALTA',
         'F10.1 - Bodega de salida como desplegable en el traslado de huevos',
         'TEXTO DEL CLIENTE. La bodega de salida del traslado de huevos debe ser un desplegable, sin digitacion libre.

QUE HAY HOY. Hay dos operaciones distintas en la misma pantalla. En VENTA el destino se elige de la lista maestra traslado_de_huevos_planta_destino, que es una lista de la EMPRESA, no de la granja. En TRASLADO no se captura destino en absoluto: el campo granja destino viaja siempre vacio.

LAS DOS LECTURAS. (a) Lo que falta es agregar un destino a la operacion TRASLADO (que hoy no tiene ninguno). En ese caso hace falta saber de que lista sale: granjas de la empresa, nucleos, bodegas de la granja de origen. (b) Lo que falta es cambiar el ALCANCE de la lista de plantas de Venta, para que cada granja vea solo sus destinos en vez de los de toda la empresa.

QUE HACE FALTA DEFINIR. 1) Cual de las dos, o las dos. 2) Si es la (a): de que lista salen los destinos y si el destino es obligatorio. 3) Si es la (b): quien mantiene la relacion granja-destino y donde se administra.

POR QUE NO SE ADIVINO. La (a) y la (b) son cambios en operaciones diferentes con modelos de datos diferentes. Construir la equivocada no acerca a la correcta.',
         'santa-reyes,italapp,H4,bloqueada-por-cliente,traslados')
    ) AS t(n, orden, prioridad, titulo, descripcion, etiquetas)
    WHERE NOT EXISTS (
        SELECT 1 FROM public.ticket_tareas x
        WHERE x.ticket_id = v_ticket_id AND x.titulo = t.titulo AND x.deleted_at IS NULL);

    RAISE NOTICE 'Definiciones pendientes Santa Reyes sembradas: caso % / empresa %', v_ticket_id, v_company;
END $$;
";

        private const string DOWN_SQL = @"
DO $$
DECLARE
    v_ticket_id bigint;
BEGIN
    SELECT t.id INTO v_ticket_id
    FROM public.tickets t
    WHERE t.titulo = '" + TITULO_CASO + @"'
    LIMIT 1;

    IF v_ticket_id IS NULL THEN
        RETURN;
    END IF;

    DELETE FROM public.ticket_tareas WHERE ticket_id = v_ticket_id;
    DELETE FROM public.ticket_notas  WHERE ticket_id = v_ticket_id;
    DELETE FROM public.tickets       WHERE id = v_ticket_id;
END $$;
";
    }
}
