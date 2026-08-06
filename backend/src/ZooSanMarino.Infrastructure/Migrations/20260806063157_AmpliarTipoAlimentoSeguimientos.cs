using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Amplía <c>seguimiento_diario_levante.tipo_alimento</c> de <c>varchar(100)</c> a <c>varchar(500)</c>.
    ///
    /// <para><b>Incidente 2026-08-06 (lote A374A, Agroavicola Sanmarino).</b> El cliente arma
    /// <c>tipo_alimento</c> concatenando los nombres de los alimentos del día
    /// (<c>"H: … / M: … / G: …"</c>) y la pantalla no limita cuántos se agregan. Con los nombres de
    /// reproductora (30–35 caracteres) el TERCER alimento pasaba de 100 y Postgres abortaba el INSERT con
    /// <c>22001 value too long</c>. Como el alta de Colombia corre en una transacción atómica, se perdía
    /// el guardado entero y el usuario veía «An error occurred while saving the entity changes».
    /// Confirmado en datos: el <c>length(tipo_alimento)</c> máximo de toda la tabla era <b>79</b> — nunca
    /// llegó a entrar un registro con tres alimentos, en ningún lote.</para>
    ///
    /// <para>500 es el mismo largo que ya tenía <c>seguimiento_diario_lote_reproductora_aves_engorde</c>,
    /// ampliada en su momento por este mismo motivo. <c>seguimiento_diario_produccion.tipo_alimento</c>
    /// ya es <c>text</c> y por eso producción nunca estuvo afectada.</para>
    ///
    /// <para><b>Por qué NO se amplían las tablas de engorde</b> (que también están en 100): la vista de
    /// Power BI <c>vw_seguimiento_pollo_engorde</c> depende de
    /// <c>seguimiento_diario_aves_engorde.tipo_alimento</c> y Postgres rechaza el ALTER con
    /// <c>0A000 cannot alter type of a column used by a view or rule</c> — verificado al aplicar la
    /// primera versión de esta migración en local. Ampliarlas exigiría dropear y recrear esa vista dentro
    /// de una migración que se aplica sola en cada deploy, con riesgo de perder sus permisos sin que nadie
    /// lo note. Engorde no es el módulo del incidente y ya quedó cubierto por el recorte de
    /// <c>TipoAlimentoCalculos.MaxLongitudEngorde</c>: el texto se acorta, pero el guardado no se cae.</para>
    ///
    /// <para><b>DDL escrito a mano</b> porque el <c>AlterColumn</c> de EF no es idempotente. El bloque de
    /// abajo omite la columna si no existe o si ya está ampliada, y <b>omite con WARNING</b> —en vez de
    /// fallar— si en ese entorno hubiera una vista dependiente que local no tiene. Un deploy que no aplica
    /// el ancho es recuperable; uno que no arranca, no (ver CLAUDE.md §🚀: el ALTER fallido mata la tarea
    /// ECS con SIGSEGV antes del primer log).</para>
    ///
    /// <para>Ampliar un <c>varchar</c> en PostgreSQL no reescribe la tabla (≥ 9.2): es un cambio de
    /// catálogo, instantáneo y sin riesgo sobre los datos existentes.</para>
    /// </summary>
    public partial class AmpliarTipoAlimentoSeguimientos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    v_largo  integer;
    v_vistas text;
BEGIN
    -- NULL si la tabla no existe, si no tiene la columna, o si la columna es 'text' (sin tope).
    SELECT c.character_maximum_length INTO v_largo
    FROM information_schema.columns c
    WHERE c.table_schema = 'public'
      AND c.table_name   = 'seguimiento_diario_levante'
      AND c.column_name  = 'tipo_alimento';

    IF v_largo IS NULL THEN
        RAISE NOTICE 'AmpliarTipoAlimentoSeguimientos: public.seguimiento_diario_levante.tipo_alimento no existe o no tiene tope - se omite';
        RETURN;
    END IF;

    IF v_largo >= 500 THEN
        RAISE NOTICE 'AmpliarTipoAlimentoSeguimientos: public.seguimiento_diario_levante.tipo_alimento ya es varchar(%) - se omite', v_largo;
        RETURN;
    END IF;

    -- Guarda: si algun entorno tiene una vista/regla colgada de la columna, el ALTER fallaria con 0A000
    -- y tumbaria el arranque de la app. Se omite con aviso para que el deploy siga en pie.
    SELECT string_agg(DISTINCT dep.relname, ', ') INTO v_vistas
    FROM pg_depend d
    JOIN pg_rewrite   r   ON r.oid   = d.objid
    JOIN pg_class     dep ON dep.oid = r.ev_class
    JOIN pg_class     src ON src.oid = d.refobjid
    JOIN pg_namespace ns  ON ns.oid  = src.relnamespace
    JOIN pg_attribute a   ON a.attrelid = src.oid AND a.attnum = d.refobjsubid
    WHERE ns.nspname   = 'public'
      AND src.relname  = 'seguimiento_diario_levante'
      AND a.attname    = 'tipo_alimento'
      AND dep.relname <> src.relname;

    IF v_vistas IS NOT NULL THEN
        RAISE WARNING 'AmpliarTipoAlimentoSeguimientos: la columna la usan las vistas (%) - se omite el ALTER para no romper el arranque. Ampliar a mano recreando esas vistas.', v_vistas;
        RETURN;
    END IF;

    ALTER TABLE public.seguimiento_diario_levante ALTER COLUMN tipo_alimento TYPE character varying(500);
    RAISE NOTICE 'AmpliarTipoAlimentoSeguimientos: tipo_alimento ampliada de varchar(%) a varchar(500)', v_largo;
END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Inverso e igualmente idempotente, pero NO achica en silencio: si hay filas que no
            // entrarian en 100 aborta con un mensaje explicito en vez de truncar datos del usuario.
            migrationBuilder.Sql(@"
DO $$
DECLARE
    v_largo      integer;
    v_excedentes bigint;
BEGIN
    SELECT c.character_maximum_length INTO v_largo
    FROM information_schema.columns c
    WHERE c.table_schema = 'public'
      AND c.table_name   = 'seguimiento_diario_levante'
      AND c.column_name  = 'tipo_alimento';

    IF v_largo IS NULL OR v_largo <= 100 THEN
        RETURN;
    END IF;

    SELECT count(*) INTO v_excedentes
    FROM public.seguimiento_diario_levante
    WHERE length(tipo_alimento) > 100;

    IF v_excedentes > 0 THEN
        RAISE EXCEPTION 'No se puede volver seguimiento_diario_levante.tipo_alimento a varchar(100): % fila(s) superan los 100 caracteres y se truncarian. Revisalas antes de revertir.', v_excedentes;
    END IF;

    ALTER TABLE public.seguimiento_diario_levante ALTER COLUMN tipo_alimento TYPE character varying(100);
END $$;");
        }
    }
}
