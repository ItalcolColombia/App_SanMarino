using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// <b>Fase C</b> - le saca el pais a los nombres FISICOS: renombra 3 tablas, 6 columnas y los
    /// indices/constraints que los nombraban, y <b>recrea las 13 funciones</b> que quedarian
    /// apuntando a un nombre inexistente.
    /// </summary>
    /// <remarks>
    /// <b>Que se renombra.</b> <c>item_inventario_ecuador</c> -> <c>item_inventario</c>;
    /// <c>guia_genetica_ecuador_header/_detalle</c> -> <c>guia_genetica_header/_detalle</c>;
    /// <c>item_inventario_ecuador_id</c> -> <c>item_inventario_id</c> en las 5 tablas vivas que la
    /// tienen; <c>guia_genetica_ecuador_header_id</c> -> <c>guia_genetica_header_id</c>. Los tres
    /// modulos son transversales: el catalogo lo comparten Ecuador, Panama y Colombia, y el header
    /// de la guia tiene <c>pais_id</c> - la Ross 308 AP de Panama vive ahi.
    ///
    /// <b>Por que las funciones van en la MISMA migracion.</b> Postgres guarda el <i>texto</i> de
    /// una funcion, no una referencia: renombrar la tabla las deja apuntando a un nombre que ya no
    /// existe y revientan en la primera llamada. Son 13 - 10 <c>sql</c> y 3 <c>plpgsql</c>, dos de
    /// ellas los triggers que llenan <c>lote_registro_historico_unificado</c>. Se reescriben desde
    /// <c>pg_get_functiondef</c>, o sea desde <b>la version realmente desplegada</b>, no desde el
    /// espejo del repo: se midio que algunos espejos estan atrasados, y recrear desde el archivo
    /// habria revertido funciones en silencio.
    ///
    /// <b>Las vistas no hacen falta.</b> Postgres las liga por OID, asi que sobreviven solas al
    /// rename y su definicion se reescribe sola. En particular
    /// <c>vw_indicadores_diarios_engorde</c> - que lee <b>Power BI</b> - conserva su columna de
    /// salida <c>guia_genetica_ecuador_header_id</c>, porque sale de un alias explicito
    /// (<c>gh.id AS ...</c>) y no de la columna renombrada. Ese nombre viejo queda <b>a proposito</b>:
    /// cambiarlo romperia un consumidor externo que no pidio nada.
    ///
    /// <b>Que NO toca.</b> La tabla <c>_backup_consumos_duplicados_validacion_20260831</c>
    /// - renombrarle una columna al respaldo lo falsifica -; la clave de wire
    /// <c>itemInventarioEcuadorId</c> y las claves jsonb ya persistidas, que son contrato con el
    /// front y con la cola offline de la PWA.
    ///
    /// <b>Idempotente.</b> Cada paso mira el estado antes de tocar (<c>to_regclass</c>,
    /// <c>information_schema</c>, <c>pg_constraint</c>), asi que una segunda corrida no hace nada y
    /// no falla. Verificado corriendola dos veces seguidas en la misma transaccion.
    ///
    /// <b>Validacion hecha antes de commitear</b> (todo en transaccion con <c>ROLLBACK</c>, sin
    /// tocar la BD local): 3 tablas renombradas, 0 funciones con el nombre viejo, 0
    /// indices/constraints sobrantes, 2a corrida limpia, y <c>fn_inventario_gastos_existencias</c>
    /// - la unica que cambia su firma y hay que dropear - comparada <b>fila a fila antes y
    /// despues</b>: 1.188 filas, <b>0 diferencias</b>.
    ///
    /// <b>Riesgo de despliegue.</b> Va en un deploy PROPIO. Si ECS hace su rollback silencioso, la
    /// TaskDef anterior queda corriendo contra la base ya renombrada y el inventario deja de
    /// funcionar entero. El <c>Down()</c> es simetrico y esta escrito para poder volver.
    /// </remarks>
    public partial class RenombraTablasYColumnasSinPais : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DO $fasec$
DECLARE
    r    record;
    def  text;
    v_args text;
BEGIN
    -- 1) TABLAS -----------------------------------------------------------------
    IF to_regclass('public.item_inventario_ecuador') IS NOT NULL
       AND to_regclass('public.item_inventario') IS NULL THEN
        ALTER TABLE public.item_inventario_ecuador RENAME TO item_inventario;
    END IF;

    IF to_regclass('public.guia_genetica_ecuador_header') IS NOT NULL
       AND to_regclass('public.guia_genetica_header') IS NULL THEN
        ALTER TABLE public.guia_genetica_ecuador_header RENAME TO guia_genetica_header;
    END IF;

    IF to_regclass('public.guia_genetica_ecuador_detalle') IS NOT NULL
       AND to_regclass('public.guia_genetica_detalle') IS NULL THEN
        ALTER TABLE public.guia_genetica_ecuador_detalle RENAME TO guia_genetica_detalle;
    END IF;

    -- 2) COLUMNAS ---------------------------------------------------------------
    -- `item_inventario_ecuador_id` vive en 5 tablas vivas. El backup congelado
    -- `_backup_consumos_duplicados_validacion_20260831` queda AFUERA a proposito: renombrarle una
    -- columna al respaldo lo falsifica.
    FOR r IN
        SELECT c.table_name
          FROM information_schema.columns c
         WHERE c.table_schema = 'public'
           AND c.column_name  = 'item_inventario_ecuador_id'
           AND c.table_name NOT LIKE '\_backup%'
           AND EXISTS (SELECT 1 FROM information_schema.tables t
                        WHERE t.table_schema='public' AND t.table_name=c.table_name
                          AND t.table_type='BASE TABLE')
    LOOP
        EXECUTE format('ALTER TABLE public.%I RENAME COLUMN item_inventario_ecuador_id TO item_inventario_id', r.table_name);
    END LOOP;

    IF EXISTS (SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='guia_genetica_detalle'
                  AND column_name='guia_genetica_ecuador_header_id') THEN
        ALTER TABLE public.guia_genetica_detalle
            RENAME COLUMN guia_genetica_ecuador_header_id TO guia_genetica_header_id;
    END IF;

    -- 3) INDICES y CONSTRAINTS --------------------------------------------------
    FOR r IN
        SELECT i.indexname AS nombre,
               replace(replace(replace(i.indexname,'item_inventario_ecuador','item_inventario'),
                       'guia_genetica_ecuador','guia_genetica'),'item_inv_ecuador','item_inv') AS nuevo
          FROM pg_indexes i
         WHERE i.schemaname='public'
           AND i.tablename NOT LIKE '\_backup%'
           AND (i.indexname LIKE '%item_inventario_ecuador%' OR i.indexname LIKE '%guia_genetica_ecuador%'
                OR i.indexname LIKE '%item_inv_ecuador%')
    LOOP
        IF r.nombre <> r.nuevo AND to_regclass('public.'||r.nuevo) IS NULL THEN
            EXECUTE format('ALTER INDEX public.%I RENAME TO %I', r.nombre, r.nuevo);
        END IF;
    END LOOP;

    FOR r IN
        SELECT c.conname AS nombre, t.relname AS tabla,
               replace(replace(replace(c.conname,'item_inventario_ecuador','item_inventario'),
                       'guia_genetica_ecuador','guia_genetica'),'item_inv_ecuador','item_inv') AS nuevo
          FROM pg_constraint c
          JOIN pg_class t ON t.oid = c.conrelid
          JOIN pg_namespace n ON n.oid = t.relnamespace
         WHERE n.nspname='public'
           AND t.relname NOT LIKE '\_backup%'
           AND (c.conname LIKE '%item_inventario_ecuador%' OR c.conname LIKE '%guia_genetica_ecuador%'
                OR c.conname LIKE '%item_inv_ecuador%')
    LOOP
        IF r.nombre <> r.nuevo
           AND NOT EXISTS (SELECT 1 FROM pg_constraint c2 JOIN pg_class t2 ON t2.oid=c2.conrelid
                            WHERE t2.relname=r.tabla AND c2.conname=r.nuevo) THEN
            EXECUTE format('ALTER TABLE public.%I RENAME CONSTRAINT %I TO %I', r.tabla, r.nombre, r.nuevo);
        END IF;
    END LOOP;

    -- 4) FUNCIONES --------------------------------------------------------------
    -- Postgres guarda el TEXTO de la funcion, asi que el rename las deja apuntando a un nombre que
    -- ya no existe. Se reescriben desde `pg_get_functiondef`, o sea desde la version que esta
    -- REALMENTE desplegada, no desde el espejo del repo (que puede estar atrasado).
    -- El reemplazo textual cubre los tres usos: la tabla, la columna `..._id` y el unico caso donde
    -- el nombre viaja como string literal (`trg_sync_tombstone`, que arma la clave de negocio del
    -- tombstone a partir de nombres de columna).
    -- Copia de seguridad de las definiciones ORIGINALES, para que el Down pueda restaurarlas
    -- literales en vez de intentar deshacer el texto con otro reemplazo. Hace falta: se midio que
    -- las 3 funciones de vacunacion YA usaban la columna neutra `item_inventario_id` (sus tablas
    -- nunca llevaron el sufijo), asi que un reemplazo inverso las corromperia renombrando columnas
    -- que nunca fueron `_ecuador`.
    CREATE TABLE IF NOT EXISTS public._rename_sin_pais_fn_backup (
        proname    text NOT NULL,
        args       text NOT NULL,
        definicion text NOT NULL,
        PRIMARY KEY (proname, args)
    );

    INSERT INTO public._rename_sin_pais_fn_backup (proname, args, definicion)
    SELECT p.proname, pg_get_function_identity_arguments(p.oid), pg_get_functiondef(p.oid)
      FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
     WHERE n.nspname='public'
       AND (p.prosrc LIKE '%item_inventario_ecuador%' OR p.prosrc LIKE '%guia_genetica_ecuador%')
    ON CONFLICT (proname, args) DO NOTHING;

    FOR r IN
        SELECT p.oid, p.proname
          FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
         WHERE n.nspname='public'
           AND (p.prosrc LIKE '%item_inventario_ecuador%' OR p.prosrc LIKE '%guia_genetica_ecuador%')
    LOOP
        def  := pg_get_functiondef(r.oid);
        v_args := pg_get_function_identity_arguments(r.oid);
        def  := replace(def, 'item_inventario_ecuador', 'item_inventario');
        def  := replace(def, 'guia_genetica_ecuador',   'guia_genetica');
        BEGIN
            EXECUTE def;
        EXCEPTION WHEN invalid_function_definition THEN
            -- La funcion declara una columna de SALIDA con el nombre viejo (hoy: solo
            -- `fn_inventario_gastos_existencias`), asi que cambia su tipo de retorno y
            -- `CREATE OR REPLACE` no alcanza. Se dropea y se recrea dentro de la MISMA
            -- transaccion: no hay ventana donde la funcion no exista. El `DROP` va sin CASCADE
            -- a proposito — si algo dependiera de ella, queremos que falle acá y no descubrirlo
            -- en produccion.
            EXECUTE format('DROP FUNCTION public.%I(%s)', r.proname, v_args);
            EXECUTE def;
        END;
    END LOOP;
END $fasec$;");
        }

        /// <summary>
        /// Vuelta atras simetrica: deshace los tres renames y reescribe las funciones al nombre
        /// viejo con el mismo mecanismo. Esta escrito ANTES de desplegar, no despues, porque el
        /// rollback silencioso de ECS no avisa.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DO $fasec$
DECLARE
    r record;
BEGIN
    -- 1) TABLAS de vuelta
    IF to_regclass('public.item_inventario') IS NOT NULL
       AND to_regclass('public.item_inventario_ecuador') IS NULL THEN
        ALTER TABLE public.item_inventario RENAME TO item_inventario_ecuador;
    END IF;
    IF to_regclass('public.guia_genetica_header') IS NOT NULL
       AND to_regclass('public.guia_genetica_ecuador_header') IS NULL THEN
        ALTER TABLE public.guia_genetica_header RENAME TO guia_genetica_ecuador_header;
    END IF;
    IF to_regclass('public.guia_genetica_detalle') IS NOT NULL
       AND to_regclass('public.guia_genetica_ecuador_detalle') IS NULL THEN
        ALTER TABLE public.guia_genetica_detalle RENAME TO guia_genetica_ecuador_detalle;
    END IF;

    -- 2) COLUMNAS de vuelta. Solo en las 5 tablas del inventario: las de vacunacion tienen su
    -- propia `item_inventario_id` que NUNCA llevo el sufijo y no se toca.
    FOR r IN
        SELECT unnest(ARRAY['inventario_gasto_detalle','inventario_gestion_movimiento',
                            'inventario_gestion_stock','lote_registro_historico_unificado',
                            'seguimiento_reserva_alimento']) AS t
    LOOP
        IF EXISTS (SELECT 1 FROM information_schema.columns
                    WHERE table_schema='public' AND table_name=r.t AND column_name='item_inventario_id') THEN
            EXECUTE format('ALTER TABLE public.%I RENAME COLUMN item_inventario_id TO item_inventario_ecuador_id', r.t);
        END IF;
    END LOOP;

    IF EXISTS (SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='guia_genetica_ecuador_detalle'
                  AND column_name='guia_genetica_header_id') THEN
        ALTER TABLE public.guia_genetica_ecuador_detalle
            RENAME COLUMN guia_genetica_header_id TO guia_genetica_ecuador_header_id;
    END IF;

    -- 3) INDICES y CONSTRAINTS de vuelta
    FOR r IN
        SELECT i.indexname AS nombre,
               replace(replace(replace(i.indexname,'ix_item_inventario_','ix_item_inventario_ecuador_'),
                       'item_inventario_pkey','item_inventario_ecuador_pkey'),
                       'uq_item_inv_company','uq_item_inv_ecuador_company') AS nuevo
          FROM pg_indexes i
         WHERE i.schemaname='public' AND i.tablename='item_inventario_ecuador'
    LOOP
        IF r.nombre <> r.nuevo AND to_regclass('public.'||r.nuevo) IS NULL THEN
            EXECUTE format('ALTER INDEX public.%I RENAME TO %I', r.nombre, r.nuevo);
        END IF;
    END LOOP;

    FOR r IN
        SELECT i.indexname AS nombre,
               replace(replace(i.indexname,'guia_genetica_header','guia_genetica_ecuador_header'),
                       'guia_genetica_detalle','guia_genetica_ecuador_detalle') AS nuevo
          FROM pg_indexes i
         WHERE i.schemaname='public'
           AND i.tablename IN ('guia_genetica_ecuador_header','guia_genetica_ecuador_detalle')
    LOOP
        IF r.nombre <> r.nuevo AND to_regclass('public.'||r.nuevo) IS NULL THEN
            EXECUTE format('ALTER INDEX public.%I RENAME TO %I', r.nombre, r.nuevo);
        END IF;
    END LOOP;

    FOR r IN
        SELECT c.conname AS nombre, t.relname AS tabla,
               replace(replace(replace(replace(c.conname,
                       'fk_igm_item_inventario','fk_igm_item_inventario_ecuador'),
                       'fk_igs_item_inventario','fk_igs_item_inventario_ecuador'),
                       'fk_item_inv_company','fk_item_inv_ecuador_company'),
                       'fk_item_inv_pais','fk_item_inv_ecuador_pais') AS nuevo
          FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid
          JOIN pg_namespace n ON n.oid=t.relnamespace
         WHERE n.nspname='public'
           AND c.conname IN ('fk_igm_item_inventario','fk_igs_item_inventario',
                             'fk_item_inv_company','fk_item_inv_pais')
    LOOP
        IF r.nombre <> r.nuevo THEN
            EXECUTE format('ALTER TABLE public.%I RENAME CONSTRAINT %I TO %I', r.tabla, r.nombre, r.nuevo);
        END IF;
    END LOOP;

    -- 4) FUNCIONES: se restauran LITERALES desde la copia que dejo el Up. No se reconstruyen con
    -- otro reemplazo de texto: seria irreversible en las funciones que ya usaban el nombre neutro.
    IF to_regclass('public._rename_sin_pais_fn_backup') IS NOT NULL THEN
        FOR r IN SELECT proname, args, definicion FROM public._rename_sin_pais_fn_backup
        LOOP
            BEGIN
                EXECUTE r.definicion;
            EXCEPTION WHEN invalid_function_definition THEN
                EXECUTE format('DROP FUNCTION public.%I(%s)', r.proname, r.args);
                EXECUTE r.definicion;
            END;
        END LOOP;
        DROP TABLE public._rename_sin_pais_fn_backup;
    ELSE
        RAISE NOTICE 'Sin copia de definiciones: el Up no llego a correr, no hay funciones que restaurar.';
    END IF;
END $fasec$;");
        }
    }
}
