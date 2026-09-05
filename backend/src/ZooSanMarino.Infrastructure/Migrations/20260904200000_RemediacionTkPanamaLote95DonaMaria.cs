using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Remediación del ticket de Panamá — DOÑA MARIA / núcleo A / galpón 4 (G0475), lote 95: los
    /// <b>32 kg separados</b> y los <b>508 kg que faltaban</b> en el stock. Migración DATA-ONLY
    /// (Designer clonado, ModelSnapshot intacto).
    ///
    /// <para>
    /// <b>De dónde salen los dos números.</b> El 28-ago-2026, 08:54–09:17, se corrió una prueba
    /// end-to-end sobre ese galpón real: un ingreso de 2.440 kg, un lote de reproductora (145) con
    /// 7 días de seguimiento <b>precargados con fechas futuras</b> (29-ago → 04-sep) que se
    /// validaron, un 8º seguimiento de engorde sin validar, y finalmente el borrado del lote de
    /// engorde «PRUEBA - 1» (238). Lo que quedó vivo:
    /// </para>
    /// <list type="number">
    ///   <item><description><b>32 kg separados.</b> La reserva del seguimiento 12944 quedó
    ///     <c>ACTIVA</c> porque borrar el lote no libera nada, y el disponible del galpón suma
    ///     reservas por <b>ubicación</b>, no por lote ⇒ le restaba disponible al ciclo siguiente sin
    ///     que nadie pudiera liberarla desde la pantalla (el lote ya no se abre).</description></item>
    ///   <item><description><b>508 kg de menos.</b> Los 7 días validados descontaron stock de verdad
    ///     (150+100+125+34+56+31+12), así que al editar el ingreso a 11.740 kg el stock quedó en
    ///     11.232.</description></item>
    /// </list>
    ///
    /// <para>
    /// <b>Qué hace esta migración.</b> (1) Libera las reservas <c>ACTIVA</c> de <b>cualquier</b> lote
    /// de engorde borrado —criterio general, no una lista de ids: medido, hoy es exactamente 1 fila
    /// de 32 kg—. (2) Devuelve los 508 kg con un <c>AjusteStock</c>, <b>fail-closed</b>: solo si los
    /// 7 movimientos de consumo siguen ahí y suman exactamente 508.
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>Por qué <c>AjusteStock</c> y no un <c>Ingreso</c>.</b> La tabla diaria del ciclo nuevo
    /// <b>ya está bien</b> (11.740 kg: el consumo pertenece a un lote borrado y la fn no lo cuenta);
    /// el que quedó corto es el STOCK. Un <c>Ingreso</c> movería los dos y dejaría la grilla en
    /// 12.248 — el mismo descuadre, del otro lado. <c>AjusteStock</c> se espeja como <c>INV_OTRO</c>,
    /// que <c>fn_seguimiento_diario_engorde</c> no lee, así que mueve el stock y solo el stock.
    /// Verificado en transacción revertida sobre la copia de producción del 04-sep: stock
    /// 11.232 → 11.740, disponible 11.200 → 11.740, grilla <b>11.740 antes y después</b>, y el
    /// <c>esperado_kg</c> de <c>fn_cuadre_alimento_engorde</c> pasa de <b>−508 a 0</b>.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>No corregir los 508 kg a mano antes del deploy.</b> El lápiz de «editar stock» hace
    /// exactamente este mismo <c>AjusteStock</c>; si alguien lo aplica manualmente, esta migración
    /// —que se guarda por sus propios movimientos de origen, no por el saldo— volvería a sumarlos.
    /// </para>
    ///
    /// <para>
    /// El defecto de código que dejaba la reserva huérfana ya está cerrado
    /// (<c>LoteAveEngordeService.DeleteAsync</c> → <c>LiberarDelLoteEngordeAsync</c>), y el saldo
    /// heredado que motivó el ticket, en la migración hermana
    /// <c>20260904190000_FnSeguimientoEngordeV18SaldoSinSeguimiento</c>. Ésta solo repara el dato
    /// que quedó de antes. Precedente del criterio y del <c>Down</c>:
    /// <c>20260831150000_DevolverAlimentoDeReproductorasBorradas</c>.
    /// Plan: <c>fase_de_desarrollo/tk_panama_saldo_alimento_lote_sin_seguimiento_plan.md</c>.
    /// </para>
    /// </summary>
    public partial class RemediacionTkPanamaLote95DonaMaria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(LiberarReservasDeLotesBorrados);
            migrationBuilder.Sql(DevolverConsumoDeLaPrueba);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DeshacerDevolucion);
            migrationBuilder.Sql(RestaurarReservasDeLotesBorrados);
        }
    }
}
