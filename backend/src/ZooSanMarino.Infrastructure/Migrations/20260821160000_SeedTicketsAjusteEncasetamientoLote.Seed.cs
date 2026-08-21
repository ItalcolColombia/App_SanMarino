using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Cuerpo SQL del seed de los dos casos del encasetamiento de lote (21 de agosto de 2026). Vive
    /// en su propio archivo (<c>partial</c>) por tamaño: la documentación de qué hace y por qué está
    /// en <c>20260821160000_SeedTicketsAjusteEncasetamientoLote.cs</c>.
    /// </summary>
    /// <remarks>
    /// Los textos se escriben sin acentos, igual que el resto de los seeds del módulo de tickets.
    /// </remarks>
    public partial class SeedTicketsAjusteEncasetamientoLote
    {
        private const string SEED_SQL = @"
-- ─────────────────────────────────────────────────────────────────────────────
-- Dos casos de soporte, ya SOLUCIONADOS y CERRADOS, sobre el encasetamiento de un
-- lote (21 de agosto de 2026). Creados por el propio administrador
-- (moiesbbuga@gmail.com), a nombre de la empresa ItalcolEcuador, que es donde se
-- reportaron y se midieron.
-- ─────────────────────────────────────────────────────────────────────────────
DO $$
DECLARE
    v_admin_guid uuid;
    v_admin_ced  integer;
    v_company    integer;
    v_pais       integer;
    v_ahora      timestamptz := timezone('utc', now());

    c_tk1_titulo constant varchar(200) :=
        'Lote engorde: editar las aves de un lote con seguimiento reescribia el encasetamiento con el saldo';
    c_tk2_titulo constant varchar(200) :=
        'Gestion de lotes: las columnas de hembras y machos mostraban el saldo, no las aves encasetadas';

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
        RAISE NOTICE 'Tickets encasetamiento de lote: no existe moiesbbuga@gmail.com en este entorno; omitido.';
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

    -- ═══════════════ 1) EMPRESA: ItalcolEcuador, resuelta por nombre ═══════════════
    -- Los dos casos se reportaron y se midieron sobre pollo engorde de Ecuador.
    SELECT c.id INTO v_company
    FROM public.companies c
    WHERE lower(c.name) LIKE '%ecuador%'
    ORDER BY c.id
    LIMIT 1;

    IF v_company IS NULL THEN
        RAISE NOTICE 'Tickets encasetamiento de lote: no existe la empresa ItalcolEcuador en este entorno; omitido.';
        RETURN;
    END IF;

    SELECT uc.pais_id INTO v_pais
    FROM public.user_companies uc
    WHERE uc.company_id = v_company AND uc.pais_id IS NOT NULL
    LIMIT 1;
    v_pais := COALESCE(v_pais, 1);

    -- ═══════════════ 2) CASO 1 — no habia forma correcta de corregir las aves de un lote ═══════════════
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
             'NOVEDAD REPORTADA POR OPERACION (21 de agosto de 2026). Crearon un lote de pollo engorde con la cantidad de aves equivocada y siguieron cargando el seguimiento diario. Al querer corregirlo despues, no querian borrar el lote ni rehacer el seguimiento: necesitaban editar el lote para sumarle las aves faltantes (o restarlas) y que la correccion bajara en cascada a seguimientos, reportes, consumo, disponibilidad y ventas.

CAUSA. lote_ave_engorde guarda el numero de aves DOS VECES con significados opuestos: aves_encasetadas y el registro Inicio de historial_lote_pollo_engorde son la BASE (historico del encasetamiento, no bajan nunca), mientras hembras_l / machos_l / mixtas son el SALDO VIVO que RetiroAvesEngordeAplicador y las ventas van descontando. El formulario de edicion cargaba los campos editables desde el SALDO y tenia actualizarEncasetadas() -> avesEncasetadas = hembrasL + machosL, asi que tocar el campo Hembras reescribia la base con un numero ya consumido.

MEDIDO. En el lote 5: aves_encasetadas 25.542 contra un maestro de 1.840. Con esa base pisada, fn_seguimiento_diario_engorde volvia a restar las mismas bajas y ventas, y toda la serie diaria, la conversion, el porcentaje de mortalidad, los informes y la liquidacion quedaban mal. En POSTURA la columna si es la base y la edicion era correcta, pero la correccion se quedaba a mitad de camino: lote_etapa_levante.aves_inicio_* (que gana sobre lotes.hembras_l en el resumen de mortalidad) y todo lote_postura_produccion no se tocaban nunca.',
             v_admin_guid, v_admin_ced,
             v_admin_guid,
             v_ahora, v_ahora,
             'Se introdujo el concepto de AJUSTE DE ENCASETAMIENTO: el inicial se reemplaza y el saldo vivo se corre por el DELTA, nunca se pisa. Es el mismo criterio con el que el trigger trg_lotes_sync_lote_postura_levante resolvio el caso de postura en agosto de 2026 y con el que RetiroAvesEngordeCalculos mueve las bajas.

QUE SE HIZO. (1) AjusteEncasetamientoCalculos, calculo puro con 24 pruebas: delta por sexo con bucket mixto de Panama, aplicacion con clamp, y diagnostico dia a dia que nombra el primero que quedaria negativo. (2) ENGORDE: LoteAveEngordeService escribe aves_encasetadas, el registro Inicio y el maestro en la misma unidad de trabajo, preservando el invariante de fn_cuadre_aves_engorde, y audita cada correccion con tipo_registro=AjusteEncaset. El formulario pasa a editar el encasetamiento y muestra el saldo aparte. (3) POSTURA: se propaga el delta a lote_etapa_levante y a lote_postura_produccion preservando los NULL; lote_postura_levante se deja al trigger para no duplicar la formula. (4) Restar por debajo de lo ya consumido se rechaza entero, diciendo el dia y las aves que faltan.

VALIDACION (smoke sobre un clon de la base, con backend propio). Engorde lote 107: editar sin tocar aves no movio ningun numero; sumar 500 dejo el saldo en 11.275 conservando las bajas y subio toda la serie diaria en 500; restar 200 bajo las tres copias en 200. Postura lote 13: las 6 copias corregidas, incluido aves_h_actual de produccion (5.315 a 5.815, conservando las bajas), y la funcion diaria de produccion subio 500 en sus 301 dias sin negativos. El gate al restar devolvio 400 con detalle y no escribio nada. fn_cuadre_aves_engorde quedo en 191 lotes, 0 descuadrados y 0 sin referencia. Gate multipais: cero diferencia en alimento, ingreso, consumo y documento en las dos empresas; la unica diferencia de saldo de aves (42 filas) se atribuyo lote por lote al lote ajustado. dotnet build 0 errores, dotnet test 2999/2999, yarn build 0 errores.

COMMIT. a9fd721. Plan: fase_de_desarrollo/ajuste_encasetamiento_lote_plan.md',
             v_ahora, v_admin_ced,
             'CRITICA', v_orden, 'A', false,
             v_company, v_admin_ced, v_ahora)
        RETURNING id INTO v_ticket_id;

        UPDATE public.tickets
           SET codigo = 'TK-2026-' || lpad(v_ticket_id::text, 6, '0')
         WHERE id = v_ticket_id;
    END IF;

    RAISE NOTICE 'Ticket ajuste de encasetamiento sembrado: caso % / empresa %', v_ticket_id, v_company;

    -- ═══════════════ 3) CASO 2 — las grillas mostraban el saldo bajo el rotulo de encasetadas ═══════════════
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
             'NOVEDAD REPORTADA POR OPERACION (21 de agosto de 2026). En Gestion de lotes y en el detalle del lote, las columnas de hembras y machos se movian solas a medida que se cargaba el seguimiento diario, y ya no sumaban las aves encasetadas de la columna de al lado: hay lotes que dicen 19.100 y algo pero al sumar las dos columnas da menos. El encasetamiento es historico del lote y no se puede tocar.

CAUSA. Es la misma trampa del caso anterior, pero en la vista: la grilla y el panel de detalle pintaban hembras_l / machos_l (el SALDO VIVO) justo al lado de aves_encasetadas (la BASE). Como el saldo baja con cada baja del seguimiento y con cada venta, las dos primeras columnas se alejaban de la tercera.

MEDIDO. 123 de los 124 lotes de Ecuador se veian mal. El caso que nombro operacion es el lote 24: la columna decia 19.120 y las de al lado mostraban 1.103 + 2.552 = 3.655, cuando el encasetamiento real es 9.061 + 10.059 = 19.120. Peor caso, lote 19: encasetamiento 51.438 contra 2.832 mostrados.

TAMBIEN EN POSTURA, pero sin efecto visible: los tabs Lotes en Levante y Lotes en Produccion tenian el mismo defecto (mostraban aves_h_actual bajo un rotulo de encasetadas) y ademas, en produccion, el total tampoco cuadraba porque su fallback partia del saldo. Esos dos tabs estan COMENTADOS en el HTML desde el commit cd9b1a7 (25 de mayo de 2026) y no se puede llegar a ellos desde la pantalla. El tab vivo (Lotes Seguimientos) usa hembras_l, que en postura si es el encasetamiento, y ya estaba correcto.',
             v_admin_guid, v_admin_ced,
             v_admin_guid,
             v_ahora, v_ahora,
             'La grilla y el panel de detalle de lote engorde pasan a mostrar el ENCASETAMIENTO (inicialHembras / inicialMachos / inicialMixtas, que el caso anterior ya expone en el contrato de la API), con rotulos explicitos Hembras encaset. y Machos encaset. El saldo NO se pierde: el detalle gana la fila Aves vivas hoy (saldo) con su desglose por sexo. Los accesores devuelven numeros y no objetos, para no romper la deteccion de cambios de Angular.

En postura se corrigieron los dos tabs comentados para dejarlos alineados por si se reactivan. OJO AL ORDEN DEL FALLBACK: en produccion aves_h_inicial NO es el encasetamiento sino las aves que sobrevivieron al levante — medido en P-K345B, encasetamiento 12.587 (10.991 + 1.596) contra un inicio de produccion de 11.526 —, asi que la columna debe salir de hembras_l. En levante los dos coinciden por construccion (lo mantiene el trigger; verificado en 21 de 21 lotes).

PENDIENTE, NO INCLUIDO EN ESTE CASO. Reactivar los tabs Lotes en Levante y Lotes en Produccion es una decision de producto aparte: fueron comentados deliberadamente en mayo de 2026 y este caso no los descomenta.

VALIDACION. Verificado en pantalla, con el build de produccion servido contra un backend propio sobre un clon de la base: la grilla de engorde suma exacto en las filas visibles (lote 24 -> 9.061 + 10.059 = 19.120) y el detalle del lote 24 muestra el encasetamiento y, aparte, Aves vivas hoy (saldo) 3.655 con su desglose. Contraste por HTTP sobre los mismos endpoints que alimentan las grillas: engorde 124 lotes y 0 columnas que no suman, levante 16 y 0, produccion 2 y 0 (antes 2 de 2 no cuadraban). Barrido del resto del front: los otros 3 sitios que suman hembras mas machos son de postura, donde esa columna es la base, y quedan intactos. yarn build 0 errores.

COMMIT. 299c816.',
             v_ahora, v_admin_ced,
             'ALTA', v_orden, 'A', false,
             v_company, v_admin_ced, v_ahora)
        RETURNING id INTO v_ticket_id;

        UPDATE public.tickets
           SET codigo = 'TK-2026-' || lpad(v_ticket_id::text, 6, '0')
         WHERE id = v_ticket_id;
    END IF;

    RAISE NOTICE 'Ticket grillas con el saldo sembrado: caso % / empresa %', v_ticket_id, v_company;
END $$;
";

        private const string DOWN_SQL = @"
DO $$
DECLARE
    c_tk1_titulo constant varchar(200) :=
        'Lote engorde: editar las aves de un lote con seguimiento reescribia el encasetamiento con el saldo';
    c_tk2_titulo constant varchar(200) :=
        'Gestion de lotes: las columnas de hembras y machos mostraban el saldo, no las aves encasetadas';
BEGIN
    DELETE FROM public.tickets WHERE titulo IN (c_tk1_titulo, c_tk2_titulo);
END $$;
";
    }
}
