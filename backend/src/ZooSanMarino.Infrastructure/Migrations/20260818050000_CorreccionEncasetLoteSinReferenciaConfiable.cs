using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Corrección de DATOS (data-only): alinea <c>aves_encasetadas</c> y el maestro al registro
    /// <c>Inicio</c> del historial, en los lotes de engorde donde el gap entre ambos es
    /// EXACTAMENTE el desfase del maestro.
    ///
    /// <para>
    /// <b>Caso que la motiva (18-ago-2026).</b> El lote 132 (ItalcolEcuador · Sacachun 3b · Galpon-3
    /// · «2604») era el <b>único de los 186</b> de la base con <c>referencia_confiable = false</c>, y
    /// el único que no cuadraba. Su <c>Inicio</c> (id 180, 21-jul-2026) dice 8.414 H + 10.773 M =
    /// <b>19.187</b>, mientras <c>aves_encasetadas</c> decía <b>19.387</b>. Esas 200 hembras de más
    /// son las mismas 200 del <c>desfase_h</c>: el lote se creó inflado (8.614 H, que menos las 285
    /// bajas dan el maestro de hoy, 8.329) y el <c>Inicio</c> guardó el número real.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué hacia el Inicio y no al revés.</b> El <c>Inicio</c> es el registro del acto de
    /// encasetamiento; <c>aves_encasetadas</c> es un campo editable del maestro cuyo inflado ya fue
    /// la causa del lote 30 (<c>20260805170000_CorreccionInicioHistorialYEncasetEngorde</c>).
    /// Reescribir el <c>Inicio</c> para que empatara con un maestro inflado sería mover la evidencia
    /// para que coincida con el error. El lote está activo y sin ventas, así que la conservación no
    /// discrimina sola: la elección es del usuario (decisión del 18-ago-2026).
    /// </para>
    ///
    /// <para>
    /// <b>Guardas — la regla no nombra ids.</b> Exige que <c>aves_encasetadas − total(Inicio)</c> sea
    /// exactamente <c>desfase_h + desfase_m</c>, con ambos desfases &gt;= 0. Un lote descuadrado por
    /// otra causa no entra; si en producción los datos difieren, la fila simplemente no aplica.
    /// Verificado el 18-ago-2026 contra los 186 lotes de la base: alcanza <b>exactamente 1</b>.
    /// </para>
    ///
    /// <para><b>Idempotente</b> por el <c>IS DISTINCT FROM</c>: la 2ª corrida da <c>UPDATE 0</c>.</para>
    ///
    /// <para>
    /// Resultado esperado: <c>fn_cuadre_aves_engorde(NULL)</c> pasa de 1 sin referencia confiable y 1
    /// que no cuadra, a <b>0 y 0</b>. Simulado en transacción con ROLLBACK antes de aplicar.
    /// SQL trazable en <c>backend/sql/correccion_encaset_lote_sin_referencia_confiable.sql</c>.
    /// </para>
    /// </summary>
    public partial class CorreccionEncasetLoteSinReferenciaConfiable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH ini AS (
                    SELECT DISTINCT ON (h.lote_ave_engorde_id) h.lote_ave_engorde_id AS id,
                           COALESCE(h.aves_hembras,0) + COALESCE(h.aves_machos,0) + COALESCE(h.aves_mixtas,0) AS objetivo
                    FROM historial_lote_pollo_engorde h
                    WHERE h.tipo_lote = 'LoteAveEngorde'
                      AND h.tipo_registro = 'Inicio'
                      AND h.lote_ave_engorde_id IS NOT NULL
                    ORDER BY h.lote_ave_engorde_id, h.fecha_registro, h.id
                ),
                objetivo AS (
                    SELECT c.lote_ave_engorde_id AS id, i.objetivo, c.desfase_h, c.desfase_m
                    FROM fn_cuadre_aves_engorde(NULL) c
                    JOIN ini i ON i.id = c.lote_ave_engorde_id
                    JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = c.lote_ave_engorde_id
                    WHERE NOT c.referencia_confiable
                      AND c.desfase_h >= 0 AND c.desfase_m >= 0
                      AND i.objetivo > 0
                      AND l.aves_encasetadas - i.objetivo = c.desfase_h + c.desfase_m
                )
                UPDATE lote_ave_engorde l
                SET aves_encasetadas = o.objetivo,
                    hembras_l        = COALESCE(l.hembras_l,0) - o.desfase_h,
                    machos_l         = COALESCE(l.machos_l,0)  - o.desfase_m
                FROM objetivo o
                WHERE l.lote_ave_engorde_id = o.id
                  AND (l.aves_encasetadas IS DISTINCT FROM o.objetivo OR o.desfase_h <> 0 OR o.desfase_m <> 0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op deliberado: revertir volvería a poner el encaset inflado y a sacar el lote de
            // toda auditoría de conservación. Es una corrección de datos hacia la evidencia
            // registrada, no un cambio de esquema reversible.
        }
    }
}
