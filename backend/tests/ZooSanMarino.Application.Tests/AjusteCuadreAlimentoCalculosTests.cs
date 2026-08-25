using ZooSanMarino.Application.Calculos;

using static ZooSanMarino.Application.Calculos.AjusteCuadreAlimentoCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests del plan de «Cuadrar galpón»
/// (`fase_de_desarrollo/ecuador_cuadre_alimento_y_permisos_plan.md` §1, F2).
///
/// <para>
/// Los dos casos que abren esta clase son reales y opuestos, y por eso existe: si el plan moviera
/// siempre el mismo lado, uno de los dos quedaría peor que antes.
/// </para>
/// </summary>
public class AjusteCuadreAlimentoCalculosTests
{
    private const string MotivoValido = "remision 63705 duplicada, eliminada el 19-ago";

    // ─── Los dos casos reales, en direcciones opuestas ─────────────────────────

    /// <summary>
    /// Sacachún 3A / G0044, medido el 25-ago-2026 sobre la copia de producción: tabla 7.720,00,
    /// stock 12.720,00, sin movimientos posteriores. La TABLA tiene razón (el ingreso duplicado se
    /// borró bien), así que lo que hay que bajar es el STOCK, y la tabla no se toca.
    /// </summary>
    [Fact]
    public void G0044_sobra_stock_y_solo_se_mueve_el_inventario()
    {
        var plan = Planificar(saldoTablaKg: 7_720m, movPostKg: 0m, stockKg: 12_720m, kilosRealesKg: 7_720m);

        Assert.Equal(-5_000m, plan.DeltaStockKg);
        Assert.Equal(0m, plan.DeltaTablaKg);
        Assert.True(plan.TocaStock);
        Assert.False(plan.TocaTabla);
        Assert.Equal(-5_000m, plan.DescuadreAntesKg);
        Assert.Equal(0m, plan.DescuadreDespuesKg);
    }

    /// <summary>
    /// DOÑA MARIA / G0475 (ItalcolPanama), misma medición: tabla 21.216,40, stock 2.566,00. Acá el
    /// que tiene razón es el STOCK —alguien ya lo corrigió a mano y la tabla nunca se enteró—, así
    /// que se mueve la TABLA y el inventario no se toca. Es el caso que un arreglo pensado solo para
    /// Ecuador dejaría sin resolver, o peor: empeorado.
    /// </summary>
    [Fact]
    public void G0475_sobra_tabla_y_solo_se_mueve_la_tabla_diaria()
    {
        var plan = Planificar(saldoTablaKg: 21_216.4m, movPostKg: 0m, stockKg: 2_566m, kilosRealesKg: 2_566m);

        Assert.Equal(0m, plan.DeltaStockKg);
        Assert.Equal(-18_650.4m, plan.DeltaTablaKg);
        Assert.False(plan.TocaStock);
        Assert.True(plan.TocaTabla);
        Assert.Equal(0m, plan.DescuadreDespuesKg);
    }

    // ─── El invariante ─────────────────────────────────────────────────────────

    /// <summary>
    /// Después del ajuste el descuadre es CERO, cualquiera sea el punto de partida. No es una
    /// aproximación: los dos deltas se derivan del mismo objetivo.
    /// </summary>
    [Theory]
    [InlineData(7720, 0, 12720, 7720)]
    [InlineData(21216.4, 0, 2566, 2566)]
    [InlineData(2379.5, 2786, 178.3, 178.3)]
    [InlineData(-175.4, 4799.5, 3710.9, 3710.9)]
    [InlineData(0, 0, 0, 1500)]
    public void Despues_del_ajuste_el_descuadre_es_cero(
        decimal saldo, decimal movPost, decimal stock, decimal reales)
    {
        var plan = Planificar(saldo, movPost, stock, reales);
        Assert.Equal(0m, plan.DescuadreDespuesKg);
    }

    /// <summary>
    /// 🔴 Los movimientos POSTERIORES al último seguimiento no se tocan. Son alimento real, bien
    /// registrado, que todavía no tiene día donde reflejarse en la tabla. Restarlos acá los borraría
    /// dos veces — el mismo error que hizo estrenar la fn del cuadre con 24/35 falsos positivos.
    /// </summary>
    [Fact]
    public void Los_movimientos_posteriores_sobreviven_al_ajuste()
    {
        // TROFARELLO G0495: 2.786,0 kg entraron después del último seguimiento.
        var plan = Planificar(saldoTablaKg: 2_379.5m, movPostKg: 2_786m, stockKg: 178.3m, kilosRealesKg: 178.3m);

        Assert.Equal(0m, plan.DeltaStockKg);
        // La tabla tiene que quedar en (stock real − movimientos posteriores), no en el stock a secas.
        Assert.Equal(178.3m - 2_786m - 2_379.5m, plan.DeltaTablaKg);
        Assert.Equal(0m, plan.DescuadreDespuesKg);
    }

    // ─── Lo reservado por la doble validación ──────────────────────────────────

    /// <summary>
    /// 🔴 El caso que casi se escapa. Con doble validación, el consumo de un seguimiento pendiente
    /// YA está descontado en el saldo pero todavía no salió del inventario, y el descuadre que la
    /// pantalla publica viene corregido por eso. Si el plan lo ignorara, el ajuste dejaría el galpón
    /// descuadrado <b>por el monto reservado</b> — después de una pantalla que dijo «cuadrado», que
    /// es el peor resultado posible. Medido el 25-ago-2026: ItalcolPanama tiene 12.609,7 kg activos.
    /// </summary>
    [Fact]
    public void Lo_reservado_se_descuenta_igual_que_los_movimientos_posteriores()
    {
        var plan = Planificar(
            saldoTablaKg: 5_000m, movPostKg: 0m, stockKg: 8_000m,
            kilosRealesKg: 8_000m, reservadoActivoKg: 3_000m);

        // El inventario ya tenía razón: 8.000 kg físicos.
        Assert.Equal(0m, plan.DeltaStockKg);
        // Y la tabla también: 8.000 − 3.000 reservados = 5.000, que es lo que muestra.
        Assert.Equal(0m, plan.DeltaTablaKg);
        Assert.False(plan.MueveAlgo);
        // Por eso el galpón YA cuadra, aunque saldo (5.000) y stock (8.000) no coincidan.
        Assert.Equal(0m, plan.DescuadreAntesKg);
    }

    /// <summary>
    /// El descuadre que calcula el plan es el MISMO que publica la fila del cuadre
    /// (<c>DescuadreAjustadoPorReservas</c>). Si divergieran, el operador vería un número en la tabla
    /// y otro en el modal, para el mismo galpón.
    /// </summary>
    [Theory]
    [InlineData(5000, 0, 8000, 3000)]
    [InlineData(21216.4, 0, 2566, 0)]
    [InlineData(7720, 0, 12720, 0)]
    [InlineData(2403.1, 0, 9070.3, 1200)]
    public void El_descuadre_del_plan_coincide_con_el_que_publica_la_fila(
        decimal saldo, decimal movPost, decimal stock, decimal reservado)
    {
        var plan = Planificar(saldo, movPost, stock, kilosRealesKg: stock, reservadoActivoKg: reservado);

        var comoLaFila = CuadreAlimentoEngordeCalculos.DescuadreAjustadoPorReservas(
            saldo - (stock - movPost), reservado);

        Assert.Equal(comoLaFila, plan.DescuadreAntesKg);
    }

    /// <summary>Con reservas, después del ajuste el descuadre sigue quedando en cero.</summary>
    [Fact]
    public void Con_reservas_el_descuadre_posterior_tambien_es_cero()
    {
        var plan = Planificar(
            saldoTablaKg: 9_000m, movPostKg: 1_500m, stockKg: 4_000m,
            kilosRealesKg: 4_000m, reservadoActivoKg: 2_000m);

        Assert.Equal(0m, plan.DescuadreDespuesKg);
        Assert.Equal(0m, plan.DeltaStockKg);
        // La tabla tiene que bajar a 4.000 − 2.000 − 1.500 = 500.
        Assert.Equal(500m - 9_000m, plan.DeltaTablaKg);
    }

    /// <summary>Sin el flag de doble validación, `reservado` es 0 y nada cambia.</summary>
    [Fact]
    public void Sin_reservas_el_resultado_es_el_de_siempre()
    {
        var conCero = Planificar(7_720m, 0m, 12_720m, 7_720m, 0m);
        var sinParametro = Planificar(7_720m, 0m, 12_720m, 7_720m);

        Assert.Equal(sinParametro, conCero);
    }

    /// <summary>Un galpón que ya cuadra no genera ningún movimiento.</summary>
    [Fact]
    public void Un_galpon_que_ya_cuadra_no_mueve_nada()
    {
        var plan = Planificar(saldoTablaKg: 5_000m, movPostKg: 0m, stockKg: 5_000m, kilosRealesKg: 5_000m);

        Assert.False(plan.MueveAlgo);
        Assert.Equal(MensajeSinCambio, Rechazo(plan, MotivoValido));
    }

    /// <summary>
    /// Por debajo de la tolerancia del cuadre no se escribe nada: generar un movimiento que no cambia
    /// el veredicto de la pantalla es ruido en la auditoría.
    /// </summary>
    [Fact]
    public void Una_diferencia_bajo_la_tolerancia_no_genera_movimiento()
    {
        var plan = Planificar(saldoTablaKg: 5_000m, movPostKg: 0m, stockKg: 5_000m, kilosRealesKg: 5_000.5m);

        Assert.False(plan.MueveAlgo);
        Assert.Equal(ToleranciaKg, CuadreAlimentoEngordeCalculos.ToleranciaKg);
    }

    // ─── Rechazos ──────────────────────────────────────────────────────────────

    [Fact]
    public void Sin_motivo_se_rechaza()
    {
        var plan = Planificar(7_720m, 0m, 12_720m, 7_720m);

        Assert.Equal(MensajeMotivoRequerido, Rechazo(plan, null));
        Assert.Equal(MensajeMotivoRequerido, Rechazo(plan, "   "));
        Assert.Equal(MensajeMotivoRequerido, Rechazo(plan, "duplicado"));   // 9 caracteres
    }

    [Fact]
    public void Con_motivo_valido_y_delta_real_se_acepta()
    {
        var plan = Planificar(7_720m, 0m, 12_720m, 7_720m);
        Assert.Null(Rechazo(plan, MotivoValido));
    }

    [Fact]
    public void Kilos_reales_negativos_se_rechazan()
    {
        var plan = Planificar(7_720m, 0m, 12_720m, -1m);
        Assert.Equal(MensajeKilosNegativos, Rechazo(plan, MotivoValido));
    }

    /// <summary>Cuadrar a CERO es legítimo: un galpón vacío existe.</summary>
    [Fact]
    public void Cuadrar_a_cero_es_valido()
    {
        var plan = Planificar(saldoTablaKg: 500m, movPostKg: 0m, stockKg: 500m, kilosRealesKg: 0m);

        Assert.Null(Rechazo(plan, MotivoValido));
        Assert.Equal(-500m, plan.DeltaStockKg);
        Assert.Equal(-500m, plan.DeltaTablaKg);
    }

    // ─── El texto que ve el usuario y queda en la auditoría ────────────────────

    [Fact]
    public void El_resumen_nombra_solo_el_lado_que_se_mueve()
    {
        var soloStock = Describir(Planificar(7_720m, 0m, 12_720m, 7_720m));
        Assert.Contains("descontar 5,000.0 kg del inventario", soloStock);
        Assert.DoesNotContain("tabla diaria", soloStock);

        var soloTabla = Describir(Planificar(21_216.4m, 0m, 2_566m, 2_566m));
        Assert.Contains("tabla diaria", soloTabla);
        Assert.DoesNotContain("del inventario", soloTabla);
    }

    /// <summary>Cuando los dos lados están mal, el resumen los nombra a los dos.</summary>
    [Fact]
    public void Si_los_dos_lados_estan_mal_el_resumen_nombra_los_dos()
    {
        var texto = Describir(Planificar(saldoTablaKg: 1_000m, movPostKg: 0m, stockKg: 3_000m, kilosRealesKg: 2_000m));

        Assert.Contains("inventario", texto);
        Assert.Contains("tabla diaria", texto);
    }
}
