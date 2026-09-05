using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Los índices únicos "un registro por lote+día" de producción y de levante pasan a excluir a
    /// las empresas con <c>companies.permite_multiples_seguimientos_diarios = true</c>.
    ///
    /// <para>
    /// <b>Por qué el predicado no hace una subconsulta a <c>companies</c>.</b> Postgres prohíbe
    /// subconsultas en el predicado de un índice parcial (solo admite columnas de la propia
    /// tabla). Por eso este <c>DO</c> resuelve la lista de <c>company_id</c> con el flag ON al
    /// momento de correr la migración y la hornea como literales en el predicado — igual patrón
    /// que <c>ux_sdlr_tipo_lote_rep_dia_utc</c> ya usa con <c>id NOT IN (1090)</c>.
    /// </para>
    ///
    /// <para>
    /// <b>Consecuencia a futuro:</b> si otra empresa enciende el flag más adelante, este índice NO
    /// se actualiza solo — hace falta una migración nueva que lo recree con la lista vigente
    /// (mismo <c>DO</c> de acá, ejecutado de nuevo). Documentado también en el plan.
    /// </para>
    ///
    /// <para>
    /// <b>Producción</b> (<c>seguimiento_diario_produccion.company_id</c> siempre poblada) excluye
    /// por <c>company_id</c> directo. <b>Levante</b> (<c>seguimiento_diario_levante.company_id</c>
    /// nullable, sin backfill histórico — ver <c>AddCompanyIdSeguimientoDiarioLevante</c>) trata
    /// NULL como PROTEGIDO (se sigue enforzando unicidad) y solo excluye filas
    /// <c>tipo_seguimiento='levante'</c> de una empresa con el flag — reproductora no cambia.
    /// </para>
    ///
    /// Plan: <c>fase_de_desarrollo/seguimiento_produccion_multiples_registros_dia_plan.md</c>.
    /// </summary>
    public partial class IndicesUnicosDiaExcluyenFlagMultiplesRegistros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $mig$
                DECLARE
                    v_ids text;
                BEGIN
                    SELECT string_agg(id::text, ',') INTO v_ids
                      FROM companies
                     WHERE permite_multiples_seguimientos_diarios = true;

                    -- ── PRODUCCIÓN ──────────────────────────────────────────────────────────
                    EXECUTE 'DROP INDEX IF EXISTS ux_seguimiento_diario_produccion_lote_dia_utc';
                    IF v_ids IS NULL THEN
                        EXECUTE '
                            CREATE UNIQUE INDEX IF NOT EXISTS ux_seguimiento_diario_produccion_lote_dia_utc
                                ON seguimiento_diario_produccion (lote_id, ((fecha_registro AT TIME ZONE ''UTC'')::date))';
                    ELSE
                        EXECUTE format('
                            CREATE UNIQUE INDEX IF NOT EXISTS ux_seguimiento_diario_produccion_lote_dia_utc
                                ON seguimiento_diario_produccion (lote_id, ((fecha_registro AT TIME ZONE ''UTC'')::date))
                             WHERE company_id IS NULL OR company_id NOT IN (%s)', v_ids);
                    END IF;

                    -- ── LEVANTE (clave tipo+lote+reproductora+dia; reproductora NO cambia) ────
                    EXECUTE 'DROP INDEX IF EXISTS ux_sdlr_tipo_lote_rep_dia_utc';
                    IF v_ids IS NULL THEN
                        EXECUTE '
                            CREATE UNIQUE INDEX IF NOT EXISTS ux_sdlr_tipo_lote_rep_dia_utc
                                ON seguimiento_diario_levante (tipo_seguimiento, lote_id, COALESCE(reproductora_id, ''''), ((fecha AT TIME ZONE ''UTC'')::date))
                             WHERE id NOT IN (1090)';
                    ELSE
                        EXECUTE format('
                            CREATE UNIQUE INDEX IF NOT EXISTS ux_sdlr_tipo_lote_rep_dia_utc
                                ON seguimiento_diario_levante (tipo_seguimiento, lote_id, COALESCE(reproductora_id, ''''), ((fecha AT TIME ZONE ''UTC'')::date))
                             WHERE id NOT IN (1090)
                               AND (tipo_seguimiento <> ''levante'' OR company_id IS NULL OR company_id NOT IN (%s))', v_ids);
                    END IF;
                END
                $mig$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ux_seguimiento_diario_produccion_lote_dia_utc;
                CREATE UNIQUE INDEX IF NOT EXISTS ux_seguimiento_diario_produccion_lote_dia_utc
                    ON seguimiento_diario_produccion (lote_id, ((fecha_registro AT TIME ZONE 'UTC')::date));

                DROP INDEX IF EXISTS ux_sdlr_tipo_lote_rep_dia_utc;
                CREATE UNIQUE INDEX IF NOT EXISTS ux_sdlr_tipo_lote_rep_dia_utc
                    ON seguimiento_diario_levante (tipo_seguimiento, lote_id, COALESCE(reproductora_id, ''), ((fecha AT TIME ZONE 'UTC')::date))
                 WHERE id NOT IN (1090);
                """);
        }
    }
}
