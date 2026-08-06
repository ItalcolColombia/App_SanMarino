using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Alinea <c>inventario_aves.lote_id</c> e <c>historial_inventario.lote_id</c> a <c>integer</c>.
    /// <para>
    /// <b>Por qué:</b> las entidades declaran <c>int LoteId</c> pero en la base las dos columnas eran
    /// <c>character varying</c>. Toda consulta que las comparara moría con
    /// <c>42883: operator does not exist: character varying = integer</c>, y como
    /// <c>MovimientoAvesService.ProcesarMovimientoAsync</c> guarda <c>Estado = "Completado"</c> ANTES de
    /// tocar el inventario y <c>CreateAsync</c> sólo loguea el fallo, los traslados del módulo
    /// «Movimientos de Aves» quedaban marcados como completados <b>sin mover una sola ave</b>.
    /// </para>
    /// <para>
    /// Por la regla «el código manda» de CLAUDE.md gana el código: <c>lotes.lote_id</c> ya es
    /// <c>integer</c>, así que estas dos columnas se alinean a integer (no al revés).
    /// </para>
    /// <para>
    /// <b>Idempotente y defensiva.</b> Cada tabla se migra dentro de un bloque que:
    /// (1) sale sin hacer nada si la columna no existe o ya es <c>integer</c> — se puede re-ejecutar;
    /// (2) antes de convertir cuenta las filas NO convertibles (nulas, vacías o no numéricas) y, si hay
    /// alguna, aborta con un mensaje explícito en vez de dejar que el cast falle con un error críptico o
    /// —peor— de descartar datos en silencio. Al escribirse esta migración ambas tablas estaban VACÍAS en
    /// el dump de producción, sin FK y sin vistas dependientes; el único índice sobre la columna lo
    /// reconstruye Postgres solo.
    /// </para>
    /// </summary>
    public partial class AlinearLoteIdInventarioAvesAInteger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlAlinearAInteger("inventario_aves"));
            migrationBuilder.Sql(SqlAlinearAInteger("historial_inventario"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlVolverAVarchar("inventario_aves"));
            migrationBuilder.Sql(SqlVolverAVarchar("historial_inventario"));
        }

        /// <summary>Convierte <c>&lt;tabla&gt;.lote_id</c> a integer si todavía es texto y todos los valores son numéricos.</summary>
        private static string SqlAlinearAInteger(string tabla) => $@"
            DO $migracion$
            DECLARE
                v_tipo   text;
                v_malos  bigint;
            BEGIN
                SELECT data_type INTO v_tipo
                  FROM information_schema.columns
                 WHERE table_schema = 'public' AND table_name = '{tabla}' AND column_name = 'lote_id';

                -- La tabla o la columna no existen en esta base: nada que hacer.
                IF v_tipo IS NULL THEN
                    RETURN;
                END IF;

                -- Ya está alineada (re-ejecución de la migración): nada que hacer.
                IF v_tipo = 'integer' THEN
                    RETURN;
                END IF;

                EXECUTE format(
                    'SELECT count(*) FROM public.%I WHERE lote_id IS NULL OR btrim(lote_id) = '''' OR lote_id !~ ''^-?[0-9]+$''',
                    '{tabla}')
                  INTO v_malos;

                IF v_malos > 0 THEN
                    RAISE EXCEPTION
                        'No se puede alinear public.{tabla}.lote_id a integer: % fila(s) con valores nulos, vacíos o no numéricos. Corrija esas filas y vuelva a desplegar.',
                        v_malos;
                END IF;

                ALTER TABLE public.{tabla}
                    ALTER COLUMN lote_id TYPE integer USING btrim(lote_id)::integer;
            END
            $migracion$;
        ";

        /// <summary>Inverso de <see cref="SqlAlinearAInteger"/>: devuelve la columna a texto.</summary>
        private static string SqlVolverAVarchar(string tabla) => $@"
            DO $migracion$
            DECLARE
                v_tipo text;
            BEGIN
                SELECT data_type INTO v_tipo
                  FROM information_schema.columns
                 WHERE table_schema = 'public' AND table_name = '{tabla}' AND column_name = 'lote_id';

                IF v_tipo IS NULL OR v_tipo <> 'integer' THEN
                    RETURN;
                END IF;

                ALTER TABLE public.{tabla}
                    ALTER COLUMN lote_id TYPE character varying(100) USING lote_id::text;
            END
            $migracion$;
        ";
    }
}
