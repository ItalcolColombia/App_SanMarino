using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Amplía <c>tipo_alimento</c> a <c>varchar(500)</c> en las tablas de seguimiento de ENGORDE,
    /// recreando alrededor del cambio las vistas que dependen de la columna.
    ///
    /// <para>Completa <c>AmpliarTipoAlimentoSeguimientos</c>, que dejó levante en 500 pero no pudo tocar
    /// engorde: de <c>seguimiento_diario_aves_engorde.tipo_alimento</c> cuelgan las 3 vistas de Power BI
    /// (<c>vw_seguimiento_pollo_engorde</c>, <c>vw_indicadores_diarios_engorde</c>,
    /// <c>vw_liquidacion_ecuador_pollo_engorde</c>) y PostgreSQL rechaza el ALTER con
    /// <c>0A000 cannot alter type of a column used by a view or rule</c>. Con esta migración las cuatro
    /// tablas de seguimiento quedan alineadas en <c>TipoAlimentoCalculos.MaxLongitud</c>.</para>
    ///
    /// <para><b>Cómo se recrean las vistas sin perder nada.</b> Antes de dropear se captura de cada una:
    /// definición (<c>pg_get_viewdef</c>), dueño, GRANTs (regenerados desde <c>aclexplode</c>) y comments
    /// —de la vista y de sus columnas—. Se dropean de la más dependiente a la más base, se amplían las
    /// columnas y se recrean en orden inverso restaurando todo. Las vistas <b>no se renombran</b>: Power
    /// BI apunta a esos nombres.</para>
    ///
    /// <para><b>No puede tumbar el deploy.</b> Todo el bloque va dentro de un
    /// <c>BEGIN … EXCEPTION WHEN OTHERS</c>, que en plpgsql abre una subtransacción: si algo falla
    /// (una vista que no se puede recrear, un dependiente que no es una vista simple, un permiso), se
    /// revierte SOLO ese bloque —las vistas quedan como estaban— y la migración sigue con un WARNING en
    /// lugar de abortar. Un deploy que no aplica el ancho es recuperable; uno que no arranca, no
    /// (CLAUDE.md §🚀: el ALTER fallido mata la tarea ECS con SIGSEGV antes del primer log).
    /// El recorte de <c>TipoAlimentoCalculos</c> cubre ese caso: el texto se acorta pero nada se cae.</para>
    ///
    /// <para><b>Verificación post-deploy</b> (las 4 tablas deben dar 500, y las 3 vistas seguir vivas):
    /// <code>
    /// select table_name, character_maximum_length from information_schema.columns
    ///  where table_schema='public' and column_name='tipo_alimento' and table_name like 'seguimiento%';
    /// select viewname from pg_views where schemaname='public' and viewname like 'vw_%engorde%';
    /// </code></para>
    /// </summary>
    public partial class AmpliarTipoAlimentoEngorde : Migration
    {
        private const string TablasEngorde = @"ARRAY[
            'seguimiento_diario_aves_engorde',
            'seguimiento_diario_aves_engorde_ecuador',
            'seguimiento_diario_aves_engorde_panama'
        ]";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
DO $$
DECLARE
    v_tabla     text;
    v_largo     integer;
    v_pendiente text[] := ARRAY[]::text[];
    v_raro      text;
    v_vista     record;
    v_recreadas integer := 0;
BEGIN
    -- 1) Que columnas siguen cortas. Si ninguna, no hay nada que hacer (idempotencia).
    FOREACH v_tabla IN ARRAY {TablasEngorde} LOOP
        SELECT c.character_maximum_length INTO v_largo
        FROM information_schema.columns c
        WHERE c.table_schema = 'public'
          AND c.table_name   = v_tabla
          AND c.column_name  = 'tipo_alimento';

        IF v_largo IS NOT NULL AND v_largo < 500 THEN
            v_pendiente := array_append(v_pendiente, v_tabla);
        END IF;
    END LOOP;

    IF array_length(v_pendiente, 1) IS NULL THEN
        RAISE NOTICE 'AmpliarTipoAlimentoEngorde: todas las columnas ya estan en varchar(500) (o no existen) - se omite';
        RETURN;
    END IF;

    -- 2) Subtransaccion: si algo falla, se revierte SOLO esto y el deploy sigue en pie.
    BEGIN
        -- 2a) Cierre recursivo de objetos que dependen de esas columnas, con todo lo necesario
        --     para reconstruirlos identicos.
        CREATE TEMP TABLE _tipo_alimento_vistas ON COMMIT DROP AS
        WITH RECURSIVE dependientes AS (
            SELECT DISTINCT dep.oid, dep.relname, dep.relkind, 0 AS nivel
            FROM pg_depend d
            JOIN pg_rewrite   r   ON r.oid   = d.objid
            JOIN pg_class     dep ON dep.oid = r.ev_class
            JOIN pg_class     src ON src.oid = d.refobjid
            JOIN pg_namespace ns  ON ns.oid  = src.relnamespace
            JOIN pg_attribute a   ON a.attrelid = src.oid AND a.attnum = d.refobjsubid
            WHERE ns.nspname  = 'public'
              AND src.relname = ANY(v_pendiente)
              AND a.attname   = 'tipo_alimento'
              AND dep.relname <> src.relname
            UNION
            SELECT DISTINCT dep.oid, dep.relname, dep.relkind, x.nivel + 1
            FROM dependientes x
            JOIN pg_depend  d   ON d.refobjid = x.oid
            JOIN pg_rewrite r   ON r.oid   = d.objid
            JOIN pg_class   dep ON dep.oid = r.ev_class
            WHERE dep.oid <> x.oid
        )
        SELECT
            x.relname,
            x.relkind,
            max(x.nivel) AS nivel,
            pg_get_viewdef(x.oid, true) AS definicion,
            pg_get_userbyid(c.relowner)  AS duenio,
            (SELECT string_agg(
                        format('GRANT %s ON public.%I TO %s%s;',
                               ac.privilege_type,
                               x.relname,
                               CASE WHEN ac.grantee = 0 THEN 'PUBLIC' ELSE quote_ident(pg_get_userbyid(ac.grantee)) END,
                               CASE WHEN ac.is_grantable THEN ' WITH GRANT OPTION' ELSE '' END),
                        ' ')
             FROM aclexplode(c.relacl) ac)  AS grants,
            obj_description(x.oid, 'pg_class') AS comentario,
            (SELECT string_agg(
                        format('COMMENT ON COLUMN public.%I.%I IS %L;', x.relname, at.attname, dsc.description),
                        ' ')
             FROM pg_description dsc
             JOIN pg_attribute at ON at.attrelid = dsc.objoid AND at.attnum = dsc.objsubid
             WHERE dsc.objoid = x.oid AND dsc.objsubid > 0) AS comentarios_columna
        FROM dependientes x
        JOIN pg_class c ON c.oid = x.oid
        GROUP BY x.oid, x.relname, x.relkind, c.relowner, c.relacl;

        -- 2b) Si hay algo que no sea una vista simple (matview, regla sobre tabla), no arriesgamos:
        --     se aborta el intento y el recorte del backend sigue cubriendo el caso.
        -- relkind es de tipo ""char"" (interno de Postgres): sin el cast explicito, 'text || ""char""'
        -- queda ambiguo y aborta el bloque entero.
        SELECT string_agg(relname || ' (relkind=' || relkind::text || ')', ', ')
        INTO v_raro
        FROM _tipo_alimento_vistas
        WHERE relkind <> 'v';

        IF v_raro IS NOT NULL THEN
            RAISE EXCEPTION 'hay dependientes que no son vistas simples: %', v_raro;
        END IF;

        -- 2c) Dropear de la mas dependiente a la mas base.
        FOR v_vista IN SELECT * FROM _tipo_alimento_vistas ORDER BY nivel DESC, relname LOOP
            EXECUTE format('DROP VIEW IF EXISTS public.%I', v_vista.relname);
        END LOOP;

        -- 2d) Ampliar.
        FOREACH v_tabla IN ARRAY v_pendiente LOOP
            EXECUTE format('ALTER TABLE public.%I ALTER COLUMN tipo_alimento TYPE character varying(500)', v_tabla);
            RAISE NOTICE 'AmpliarTipoAlimentoEngorde: public.%.tipo_alimento ampliada a varchar(500)', v_tabla;
        END LOOP;

        -- 2e) Recrear en orden inverso, restaurando duenio, grants y comments.
        FOR v_vista IN SELECT * FROM _tipo_alimento_vistas ORDER BY nivel ASC, relname LOOP
            EXECUTE format('CREATE VIEW public.%I AS %s', v_vista.relname, v_vista.definicion);
            EXECUTE format('ALTER VIEW public.%I OWNER TO %I', v_vista.relname, v_vista.duenio);
            IF v_vista.grants IS NOT NULL THEN
                EXECUTE v_vista.grants;
            END IF;
            IF v_vista.comentario IS NOT NULL THEN
                EXECUTE format('COMMENT ON VIEW public.%I IS %L', v_vista.relname, v_vista.comentario);
            END IF;
            IF v_vista.comentarios_columna IS NOT NULL THEN
                EXECUTE v_vista.comentarios_columna;
            END IF;
            v_recreadas := v_recreadas + 1;
        END LOOP;

        RAISE NOTICE 'AmpliarTipoAlimentoEngorde: % tabla(s) ampliada(s), % vista(s) recreada(s)',
            array_length(v_pendiente, 1), v_recreadas;
    EXCEPTION WHEN OTHERS THEN
        RAISE WARNING 'AmpliarTipoAlimentoEngorde: se omite el ancho de engorde (queda en varchar(100); el recorte de TipoAlimentoCalculos lo cubre). Motivo: %', SQLERRM;
    END;
END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Inverso: mismo baile de vistas, pero NO achica en silencio — si hay filas que no entrarian
            // en 100 aborta con un mensaje explicito en vez de truncar datos del usuario.
            migrationBuilder.Sql($@"
DO $$
DECLARE
    v_tabla      text;
    v_largo      integer;
    v_excedentes bigint;
    v_pendiente  text[] := ARRAY[]::text[];
    v_vista      record;
BEGIN
    FOREACH v_tabla IN ARRAY {TablasEngorde} LOOP
        SELECT c.character_maximum_length INTO v_largo
        FROM information_schema.columns c
        WHERE c.table_schema = 'public'
          AND c.table_name   = v_tabla
          AND c.column_name  = 'tipo_alimento';

        IF v_largo IS NULL OR v_largo <= 100 THEN
            CONTINUE;
        END IF;

        EXECUTE format('SELECT count(*) FROM public.%I WHERE length(tipo_alimento) > 100', v_tabla)
        INTO v_excedentes;

        IF v_excedentes > 0 THEN
            RAISE EXCEPTION 'No se puede volver public.%.tipo_alimento a varchar(100): % fila(s) superan los 100 caracteres y se truncarian. Revisalas antes de revertir.',
                v_tabla, v_excedentes;
        END IF;

        v_pendiente := array_append(v_pendiente, v_tabla);
    END LOOP;

    IF array_length(v_pendiente, 1) IS NULL THEN
        RETURN;
    END IF;

    CREATE TEMP TABLE _tipo_alimento_vistas_down ON COMMIT DROP AS
    WITH RECURSIVE dependientes AS (
        SELECT DISTINCT dep.oid, dep.relname, 0 AS nivel
        FROM pg_depend d
        JOIN pg_rewrite   r   ON r.oid   = d.objid
        JOIN pg_class     dep ON dep.oid = r.ev_class
        JOIN pg_class     src ON src.oid = d.refobjid
        JOIN pg_namespace ns  ON ns.oid  = src.relnamespace
        JOIN pg_attribute a   ON a.attrelid = src.oid AND a.attnum = d.refobjsubid
        WHERE ns.nspname  = 'public'
          AND src.relname = ANY(v_pendiente)
          AND a.attname   = 'tipo_alimento'
          AND dep.relname <> src.relname
        UNION
        SELECT DISTINCT dep.oid, dep.relname, x.nivel + 1
        FROM dependientes x
        JOIN pg_depend  d   ON d.refobjid = x.oid
        JOIN pg_rewrite r   ON r.oid   = d.objid
        JOIN pg_class   dep ON dep.oid = r.ev_class
        WHERE dep.oid <> x.oid
    )
    SELECT x.relname, max(x.nivel) AS nivel,
           pg_get_viewdef(x.oid, true) AS definicion,
           pg_get_userbyid(c.relowner) AS duenio
    FROM dependientes x
    JOIN pg_class c ON c.oid = x.oid
    GROUP BY x.oid, x.relname, c.relowner;

    FOR v_vista IN SELECT * FROM _tipo_alimento_vistas_down ORDER BY nivel DESC, relname LOOP
        EXECUTE format('DROP VIEW IF EXISTS public.%I', v_vista.relname);
    END LOOP;

    FOREACH v_tabla IN ARRAY v_pendiente LOOP
        EXECUTE format('ALTER TABLE public.%I ALTER COLUMN tipo_alimento TYPE character varying(100)', v_tabla);
    END LOOP;

    FOR v_vista IN SELECT * FROM _tipo_alimento_vistas_down ORDER BY nivel ASC, relname LOOP
        EXECUTE format('CREATE VIEW public.%I AS %s', v_vista.relname, v_vista.definicion);
        EXECUTE format('ALTER VIEW public.%I OWNER TO %I', v_vista.relname, v_vista.duenio);
    END LOOP;
END $$;");
        }
    }
}
