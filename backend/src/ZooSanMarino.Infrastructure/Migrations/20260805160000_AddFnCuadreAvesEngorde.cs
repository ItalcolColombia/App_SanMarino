using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// <c>fn_cuadre_aves_engorde</c>: detector permanente del invariante del maestro de aves de pollo
    /// engorde, hermana de <c>fn_cuadre_alimento_engorde</c> y con el mismo criterio del repo —
    /// «el cuadre se mira, no se espera».
    /// <code>
    /// maestro = inicio(historial) − ventas Completado − bajas BAJA_SEGUIMIENTO − ajustes fantasma
    /// </code>
    /// <para>
    /// <b>Por qué:</b> el descuadre del lote 107 (Km 61 · G1 · 2604) se descubrió por un ticket de
    /// operación semanas después de producirse, y su síntoma visible fue que «Aves disponibles» dejó de
    /// coincidir con el saldo de la tabla diaria. Con esta función el mismo caso se ve el día que pasa:
    /// <c>SELECT * FROM fn_cuadre_aves_engorde(NULL) WHERE NOT cuadra;</c>
    /// </para>
    /// <para>
    /// <c>referencia_confiable = false</c> aísla los lotes cuyo historial <c>Inicio</c> no cuadra con
    /// <c>aves_encasetadas</c>: ahí el desfase no es concluyente y el lote pide revisión manual, no
    /// corrección automática (4 lotes de Ecuador al 05-ago-2026, todos con la pantalla ya correcta).
    /// </para>
    /// <para>
    /// Idempotente (<c>DROP ... IF EXISTS</c> + <c>CREATE OR REPLACE</c>); es una función de solo
    /// lectura, no toca datos. Copia en <c>backend/sql/fn_cuadre_aves_engorde.sql</c>.
    /// </para>
    /// </summary>
    public partial class AddFnCuadreAvesEngorde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS fn_cuadre_aves_engorde(integer);

                CREATE OR REPLACE FUNCTION fn_cuadre_aves_engorde(p_company_id integer DEFAULT NULL)
                RETURNS TABLE (
                    lote_ave_engorde_id  integer,
                    company_id           integer,
                    granja               text,
                    galpon               text,
                    lote_nombre          text,
                    estado_operativo     text,
                    maestro_h            integer,
                    maestro_m            integer,
                    esperado_h           integer,
                    esperado_m           integer,
                    desfase_h            integer,
                    desfase_m            integer,
                    referencia_confiable boolean,
                    cuadra               boolean
                )
                LANGUAGE sql
                STABLE
                AS $fn$
                    WITH ini AS (
                        SELECT DISTINCT ON (h.lote_ave_engorde_id) h.lote_ave_engorde_id AS id,
                               COALESCE(h.aves_hembras, 0) AS ih, COALESCE(h.aves_machos, 0) AS im, COALESCE(h.aves_mixtas, 0) AS ix
                        FROM historial_lote_pollo_engorde h
                        WHERE h.tipo_lote = 'LoteAveEngorde' AND h.tipo_registro = 'Inicio' AND h.lote_ave_engorde_id IS NOT NULL
                        ORDER BY h.lote_ave_engorde_id, h.fecha_registro, h.id
                    ), aj AS (
                        SELECT h.lote_ave_engorde_id AS id,
                               SUM(COALESCE(h.aves_hembras, 0)) AS ah, SUM(COALESCE(h.aves_machos, 0)) AS am
                        FROM historial_lote_pollo_engorde h
                        WHERE h.tipo_lote = 'LoteAveEngorde' AND h.tipo_registro = 'Ajuste' AND h.lote_ave_engorde_id IS NOT NULL
                        GROUP BY h.lote_ave_engorde_id
                    ), v AS (
                        SELECT m.lote_ave_engorde_origen_id AS id,
                               SUM(m.cantidad_hembras) AS vh, SUM(m.cantidad_machos) AS vm
                        FROM movimiento_pollo_engorde m
                        WHERE m.estado = 'Completado' AND m.deleted_at IS NULL AND m.lote_ave_engorde_origen_id IS NOT NULL
                        GROUP BY m.lote_ave_engorde_origen_id
                    ), ap AS (
                        SELECT u.lote_ave_engorde_id AS id,
                               SUM(COALESCE(u.cantidad_hembras, 0)) AS ph, SUM(COALESCE(u.cantidad_machos, 0)) AS pm
                        FROM lote_registro_historico_unificado u
                        WHERE u.tipo_evento = 'BAJA_SEGUIMIENTO' AND NOT u.anulado AND u.lote_ave_engorde_id IS NOT NULL
                        GROUP BY u.lote_ave_engorde_id
                    )
                    SELECT
                        l.lote_ave_engorde_id,
                        l.company_id,
                        f.name::text,
                        g.galpon_nombre::text,
                        l.lote_nombre::text,
                        l.estado_operativo_lote::text,
                        COALESCE(l.hembras_l, 0)::integer,
                        COALESCE(l.machos_l, 0)::integer,
                        esp.eh::integer,
                        esp.em::integer,
                        (COALESCE(l.hembras_l, 0) - esp.eh)::integer,
                        (COALESCE(l.machos_l, 0) - esp.em)::integer,
                        esp.confiable,
                        (esp.confiable AND COALESCE(l.hembras_l, 0) = esp.eh AND COALESCE(l.machos_l, 0) = esp.em)
                    FROM lote_ave_engorde l
                    JOIN farms f ON f.id = l.granja_id
                    LEFT JOIN galpones g ON g.galpon_id = l.galpon_id
                    LEFT JOIN ini i ON i.id = l.lote_ave_engorde_id
                    LEFT JOIN aj  ON aj.id = l.lote_ave_engorde_id
                    LEFT JOIN v   ON v.id  = l.lote_ave_engorde_id
                    LEFT JOIN ap  ON ap.id = l.lote_ave_engorde_id
                    CROSS JOIN LATERAL (
                        SELECT COALESCE(i.ih, 0) - COALESCE(v.vh, 0) - COALESCE(ap.ph, 0) - COALESCE(aj.ah, 0) AS eh,
                               COALESCE(i.im, 0) - COALESCE(v.vm, 0) - COALESCE(ap.pm, 0) - COALESCE(aj.am, 0) AS em,
                               (i.id IS NOT NULL
                                AND COALESCE(l.aves_encasetadas, 0) > 0
                                AND COALESCE(i.ih, 0) + COALESCE(i.im, 0) + COALESCE(i.ix, 0) = l.aves_encasetadas) AS confiable
                    ) esp
                    WHERE l.deleted_at IS NULL
                      AND l.lote_ave_engorde_id IS NOT NULL
                      AND (p_company_id IS NULL OR l.company_id = p_company_id);
                $fn$;

                COMMENT ON FUNCTION fn_cuadre_aves_engorde(integer) IS
                'Invariante del maestro de aves de pollo engorde: hembras_l/machos_l == inicio - ventas Completado - BAJA_SEGUIMIENTO - ajustes fantasma. cuadra=false señala un maestro que dejó de reflejar lo registrado; referencia_confiable=false indica que el historial Inicio no cuadra con aves_encasetadas y el lote necesita revisión manual.';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_cuadre_aves_engorde(integer);");
        }
    }
}
