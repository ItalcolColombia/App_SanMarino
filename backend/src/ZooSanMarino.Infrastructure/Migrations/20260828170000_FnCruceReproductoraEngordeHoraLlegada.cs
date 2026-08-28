using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// El cruce reproductora → pollo engorde pasa a respetar la <b>hora de llegada</b> del lote
    /// engorde: desde las <b>13:00 inclusive</b> las aves no consumen el día del encasetamiento, así
    /// que toda la serie del cruce se corre un día.
    ///
    /// <para>
    /// <b>El defecto.</b> La regla de la hora de llegada existe desde jul-2026 y los cuatro caminos
    /// C# de captura la respetan (<c>SeguimientoAvesEngordeEcuadorService</c> y
    /// <c>SeguimientoAvesEngordeService</c>, alta y edición, más los dos PUT de lote y las dos cargas
    /// masivas). <c>fn_cruce_reproductora_a_engorde</c> era el <b>único escritor</b> de
    /// <c>seguimiento_diario_aves_engorde</c> que no la miraba: fechaba el destino en
    /// <c>fecha_encaset + d</c>, con <c>d</c> = edad del registro reproductora.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué el guarda de reproductora no lo tapaba.</b> Medido sobre la copia de producción:
    /// <b>ningún lote reproductora tiene hora informada</b> (0 de 138). La hora se pide en el
    /// formulario del lote POLLO ENGORDE, que es donde el operario la escribe; el lote reproductora
    /// hijo la deja nula, su propio guarda nunca dispara, el operario captura ahí la edad 0 y el
    /// cruce la re-fecha al día del encaset del lote engorde.
    /// </para>
    ///
    /// <para>
    /// <b>Lo que ve el usuario</b> (ticket de operación, Panamá): una fila el día del encaset con
    /// <b>Día 0 / Semana 0</b> —el front ya aplica bien el desplazamiento— y con el <b>saldo de
    /// alimento en negativo</b>, porque hay consumo real y el alimento entra al día siguiente. La
    /// fila es <c>origen_cruce</c> ⇒ <b>solo lectura en la UI</b>: por eso abren ticket en vez de
    /// borrarla.
    /// </para>
    ///
    /// <para>
    /// <b>Se corre la serie, no se descarta el día.</b> El consumo del reproductora es real y tiene
    /// que llegar al lote engorde; saltear la edad 0 desalinearía las aves que sincroniza
    /// <c>RetiroAvesEngordeAplicador</c>.
    /// </para>
    ///
    /// <para>
    /// <b>Alcance medido:</b> 18 lotes engorde tienen hora informada — 16 de ItalcolEcuador y 2 de
    /// ItalcolPanama, todas ≥ 13:00. Los de Ecuador no usan el cruce (0 filas <c>origen_cruce</c>).
    /// Con hora <c>NULL</c> o anterior a 13:00 el desplazamiento es 0 y el SQL es <b>idéntico</b> al
    /// previo, así que los demás lotes no cambian.
    /// </para>
    ///
    /// <para>
    /// <b>Lo que esta migración NO hace: no recalcula nada.</b> Las 3 filas ya torcidas (lotes 215 y
    /// 216 de ItalcolPanama) se quedan donde están. Recalcularlas hoy <b>choca</b> con registros
    /// manuales que ya ocupan las fechas destino — al 215 le editaron la <c>fecha_encaset</c> después
    /// del cruce, así que cualquier recálculo mueve sus siete filas — y el índice único por día UTC
    /// haría fallar la confirmación de reproductora entera. Es una operación de datos aparte, con su
    /// propia verificación y su propio OK.
    /// </para>
    ///
    /// Plan: <c>fase_de_desarrollo/hora_llegada_manda_primer_dia_engorde_plan.md</c>.
    /// Espejo: <c>backend/sql/fn_cruce_reproductora_a_engorde.sql</c> — esta migración es el
    /// <b>vehículo</b>: nada de <c>backend/sql/</c> llega a producción por sí solo.
    /// Idempotente: <c>CREATE OR REPLACE</c> + <c>DROP/CREATE TRIGGER</c> + <c>INDEX IF NOT EXISTS</c>.
    /// Sin cambios de modelo (ModelSnapshot intacto).
    /// </summary>
    public partial class FnCruceReproductoraEngordeHoraLlegada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnCruceReproductoraAEngordeHoraLlegada);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin reversión deliberada: la versión anterior es la que fecha el cruce en el día del
            // encaset aunque las aves hayan llegado a las 23:58. Un Down que reintroduce el defecto
            // no es un Down.
            migrationBuilder.Sql(
                "-- Sin reversion deliberada: ver el comentario del Down en la migracion.");
        }
    }
}
