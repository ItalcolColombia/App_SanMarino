using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Remedia las filas de cruce que quedaron fechadas <b>el día del encasetamiento</b> en los lotes
    /// de engorde cuyas aves llegaron a las 13:00 o después.
    ///
    /// <para>
    /// <b>De dónde viene.</b> <c>20260828170000_FnCruceReproductoraEngordeHoraLlegada</c> arregló al
    /// ESCRITOR —<c>fn_cruce_reproductora_a_engorde</c> ya corre la serie cuando el lote tiene
    /// <c>hora_encasetamiento &gt;= 13:00</c>— pero <b>no recalculó nada</b>, y lo dejó dicho: las
    /// filas viejas quedaban para «una operación de datos aparte, con su propia verificación y su
    /// propio OK». Esta migración es esa operación.
    /// </para>
    ///
    /// <para>
    /// <b>Alcance medido</b> (copia de producción, 28-ago-2026): <b>3</b> filas violan la regla, todas
    /// <c>origen_cruce</c>/<c>SYSTEM_CRUCE</c> y todas de ItalcolPanamá — lotes <b>215</b> (10-ago,
    /// hora 23:30) y <b>216</b> (13-ago, 22:40), vivos, y <b>238</b>, que es el lote del ticket y está
    /// <b>borrado</b>, así que queda fuera. Ecuador no entra: sus lotes con hora tardía no usan el
    /// cruce (0 filas <c>origen_cruce</c>) y no tienen una sola violación.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>Se mide en UTC, no en la zona de la sesión.</b> <c>fecha</c> es <c>timestamptz</c>
    /// guardada a <c>00:00Z</c>; con <c>America/Bogotá</c> un <c>fecha::date</c> a secas resta un día y
    /// el mismo diagnóstico reporta 6 violaciones donde hay 3. Todo el SQL usa
    /// <c>(fecha AT TIME ZONE 'UTC')::date</c>.
    /// </para>
    ///
    /// <para>
    /// <b>Un segundo defecto, independiente, que hay que corregir ANTES de recalcular.</b> El lote
    /// reproductora hijo del 215 tenía <c>fecha_encasetamiento</c> = 09-ago, un día antes que su padre,
    /// y fue editado el 25-ago — cuatro días <i>después</i> de que corriera el cruce (21-ago). Como el
    /// cruce mapea por <b>EDAD</b>, ese desfase corre la serie otra vez: recalcular sin alinear movía
    /// las filas del 215 dos días y le costaba <b>2.086,560 kg</b> en vez de 1.088,640. Alinearlo es
    /// además la norma: <b>128 de 138</b> lotes reproductora ya coinciden con su padre. Sólo se tocan
    /// los lotes de la cohorte; los otros desalineados del universo (deltas de 7, 18 y 29 días) son
    /// otra historia y no se tocan.
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>Por qué no alcanza con mover las filas.</b> El descuento de aves al maestro
    /// <c>lote_ave_engorde</c> y la fila <c>BAJA_SEGUIMIENTO</c> de
    /// <c>lote_registro_historico_unificado</c> los escribe <b>C#</b>
    /// (<c>RetiroAvesEngordeAplicador.SincronizarCruceAsync</c>), no el SQL. Borrar filas de cruce a
    /// secas dejaría el histórico <b>huérfano y sin anular</b> —«se ANULA, nunca se abandona»— y
    /// <b>62 aves</b> descontadas de más en el maestro. Por eso los pasos 1 y 4 replican los dos pasos
    /// del aplicador, con su mismo reparto (<c>RetiroAvesEngordeCalculos.EsLoteMixto</c>: el bucket lo
    /// decide el DATO del lote), su mismo <b>clamp a 0</b> y su misma guarda anti doble descuento
    /// (<c>aves_encasetadas &gt; 0</c>, porque con 0 <c>fn_seguimiento_diario_engorde</c> deriva las
    /// iniciales del propio maestro y moverlo restaría dos veces). Mismo patrón que
    /// <c>20260729100000_AplicarBajasCruceReproductoraAlMaestroEngorde</c>.
    /// </para>
    ///
    /// <para>
    /// <b>Resultado medido</b> (ensayo en transacción revertida contra la copia de producción):
    /// lote 215 pasa de <b>7 filas / 5.080,320 kg / 10-ago..16-ago</b> a <b>6 / 3.991,680 /
    /// 11-ago..16-ago</b>; lote 216, de <b>7 / 1.542,240 / 13-ago..19-ago</b> a <b>6 / 1.360,800 /
    /// 14-ago..19-ago</b>. Violaciones <b>2 → 0</b>. Cuadre de alimento del galpón <b>G0471</b>
    /// (compartido por los dos lotes): <b>−634,64 → +635,44 kg</b>. Cuadre de aves: desfase
    /// <b>0/0</b>, <c>cuadra = true</c> en ambos. <b>Ninguna fila manual</b> se mueve, se borra ni se
    /// crea, y los otros 64 galpones del cuadre quedan con la <b>misma huella</b>.
    /// </para>
    ///
    /// <para>
    /// <b>Lo que se pierde, dicho de frente.</b> Al correr la serie, el último día del cruce cae sobre
    /// un <b>registro manual</b> que ya ocupa esa fecha, y la fn lo saltea con
    /// <c>ON CONFLICT DO NOTHING</c> + <c>RAISE WARNING</c> (el guarda que agregó la migración
    /// anterior; sin él esto <b>reventaba</b> con <c>duplicate key</c>). Son <b>1.088,640 kg</b> del
    /// 215 y <b>181,440 kg</b> del 216 que dejan de contarse. No hay salida sin pérdida: las dos
    /// fuentes reclaman el mismo día. Se eligió esta con el número a la vista.
    /// </para>
    ///
    /// <para>
    /// <b>De UNA sola vez, no convergente.</b> Todo el <c>Up</c> va dentro de un bloque guardado por la
    /// tabla de respaldo: si se re-corriera, el paso 3 volvería a borrar y recrear las filas de cruce
    /// con ids <b>nuevos</b> y las filas del histórico de la corrida anterior quedarían huérfanas y sin
    /// anular — justo el invariante que esto viene a cuidar. Verificado: la segunda corrida deja
    /// <c>RAISE NOTICE</c> y no mueve un solo número.
    /// </para>
    ///
    /// <para>
    /// <b>El <c>Down</c> revierte de verdad</b>, guiado por los tres respaldos (cohorte + maestro
    /// previo, filas de cruce completas, e ids del histórico anulados/insertados). Sin ellos no habría
    /// forma de distinguir lo que movió esta migración de lo que ya estaba: las filas que el aplicador
    /// anuló en regeneraciones anteriores tienen la misma pinta. Verificado: vuelve al estado inicial
    /// línea por línea, incluido el encaset del reproductora y el descuadre de −634,64.
    /// </para>
    ///
    /// Data-only: Designer clonado, ModelSnapshot intacto.
    /// Diagnóstico repetible: <c>backend/sql/verificar_cruce_engorde_hora_llegada.sql</c>.
    /// Plan: <c>fase_de_desarrollo/remediacion_cruce_engorde_hora_llegada_plan.md</c>.
    /// </summary>
    public partial class RemediarCruceEngordeHoraLlegadaPanama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RemediacionUp);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RemediacionDown);
        }
    }
}
