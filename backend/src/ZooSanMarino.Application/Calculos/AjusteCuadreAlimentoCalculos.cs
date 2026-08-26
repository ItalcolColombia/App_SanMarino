namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Qué hay que escribir de cada lado para que un galpón de engorde vuelva a cuadrar
/// (`fase_de_desarrollo/ecuador_cuadre_alimento_y_permisos_plan.md` §1, F2).
///
/// <para>
/// <b>El pedido era «editar el saldo desde la pestaña de Cuadre». El saldo no es un campo: es un
/// derivado.</b> <c>fn_seguimiento_diario_engorde</c> lo calcula como
/// <c>apertura + Σ(ingresos y traslados del histórico) − Σ(consumo del seguimiento)</c>. No hay
/// dónde escribirlo; lo que se corrige es el insumo que está mal.
/// </para>
///
/// <para>
/// Y hay <b>dos lados</b> que pueden estarlo, con arreglos opuestos:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <b>Sobra stock</b> (G0044: tabla 7.720, stock 12.720). La tabla tiene razón — el ingreso
///     duplicado se borró bien— y lo que hay que bajar es el <b>stock</b>.
///   </description></item>
///   <item><description>
///     <b>Sobra tabla</b> (Panamá G0475: tabla 21.216, stock 2.566). Alguien ya corrigió el
///     inventario a mano y la tabla diaria nunca se enteró, porque los ajustes se espejan como
///     <c>INV_OTRO</c> y la fn no lee ese tipo. Lo que hay que bajar es la <b>tabla</b>.
///   </description></item>
/// </list>
///
/// <para>
/// Por eso la operación no es «editar un número» sino <b>declarar los kilos que realmente hay en el
/// galpón</b>: de ahí salen los dos deltas, y el invariante queda en cero por construcción. Quien
/// cuadra no tiene que saber de qué lado está el error — solo cuánto alimento hay.
/// </para>
///
/// <para>
/// Este cálculo es puro a propósito: decide <b>cuánto</b> se mueve de cada lado, que es donde un
/// signo invertido pasa desapercibido y deja el galpón peor que antes.
/// </para>
/// </summary>
public static class AjusteCuadreAlimentoCalculos
{
    /// <summary>
    /// Misma tolerancia que <see cref="CuadreAlimentoEngordeCalculos.ToleranciaKg"/>, y tiene que
    /// seguir siéndolo: si el plan escribiera ajustes por debajo de lo que el cuadre considera
    /// cuadrado, la pantalla generaría movimientos que no cambian ningún veredicto.
    /// </summary>
    public const decimal ToleranciaKg = CuadreAlimentoEngordeCalculos.ToleranciaKg;

    /// <summary>Largo mínimo del motivo. Un ajuste sin explicación es un descuadre con permiso.</summary>
    public const int MotivoMinimo = 10;

    public const string MensajeMotivoRequerido =
        "Explique por qué se corrige el galpón (mínimo 10 caracteres). El motivo queda en la auditoría " +
        "del movimiento y es lo único que le dice al próximo que mire por qué estos kilos cambiaron.";

    public const string MensajeKilosNegativos =
        "Los kilos reales del galpón no pueden ser negativos.";

    public const string MensajeSinCambio =
        "El galpón ya cuadra con esos kilos: no hay nada que corregir.";

    /// <summary>
    /// Lo que se va a escribir. Los dos deltas son independientes: lo normal es que uno sea 0.
    /// </summary>
    /// <param name="DeltaStockKg">
    /// Kilos a sumar (o restar, si es negativo) en <c>inventario_gestion_stock</c>. Se escribe como
    /// <c>AjusteStock</c>, que la tabla diaria NO ve — y está bien, porque en este caso la tabla ya
    /// tenía razón.
    /// </param>
    /// <param name="DeltaTablaKg">
    /// Kilos a sumar (o restar) en la tabla diaria. Se escribe como <c>AjusteCuadreTablaEntrada</c> /
    /// <c>AjusteCuadreTablaSalida</c>, que NO tocan el stock — y está bien, porque en este caso el
    /// stock ya tenía razón.
    /// </param>
    public sealed record PlanCuadre(
        decimal SaldoTablaKg,
        decimal MovPostKg,
        decimal StockKg,
        decimal ReservadoActivoKg,
        decimal KilosRealesKg,
        decimal DeltaStockKg,
        decimal DeltaTablaKg,
        decimal DescuadreAntesKg,
        decimal DescuadreDespuesKg)
    {
        /// <summary>¿Hay algo que escribir? Un plan sin movimientos se rechaza.</summary>
        public bool MueveAlgo => Math.Abs(DeltaStockKg) > ToleranciaKg || Math.Abs(DeltaTablaKg) > ToleranciaKg;

        public bool TocaStock => Math.Abs(DeltaStockKg) > ToleranciaKg;
        public bool TocaTabla => Math.Abs(DeltaTablaKg) > ToleranciaKg;
    }

    /// <summary>
    /// Arma el plan a partir de la fila del cuadre y de los kilos que el operador declara.
    ///
    /// <para>
    /// El invariante es <c>saldo == stock − movimientos_posteriores</c>. Declarando el stock real
    /// como <paramref name="kilosRealesKg"/>, el saldo que la tabla DEBE mostrar es
    /// <c>kilosReales − movPost</c>; de ahí salen los dos deltas y el descuadre posterior es cero por
    /// construcción, no por aproximación.
    /// </para>
    ///
    /// <para>
    /// <b>Los movimientos posteriores no se tocan.</b> Son alimento que entró después del último
    /// seguimiento: existe, está bien registrado, y lo único que pasa es que todavía no tiene día
    /// donde reflejarse en la tabla. Restarlos acá los borraría dos veces.
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>Lo RESERVADO tampoco.</b> Con la doble validación encendida, el consumo de un registro
    /// pendiente <b>ya está descontado en el saldo</b> —ninguna fn mira <c>validado</c>— pero todavía
    /// no salió del inventario. El stock comparable es <c>stock − reservado</c>, que es exactamente
    /// la corrección que <c>CuadreAlimentoEngordeCalculos.DescuadreAjustadoPorReservas</c> ya aplica
    /// al descuadre que se publica. Ignorarlo acá dejaría el galpón descuadrado <b>por el monto
    /// reservado</b> después de un ajuste que la pantalla iba a mostrar como exitoso — el peor
    /// resultado posible. Medido el 25-ago-2026: ItalcolPanama tiene 12.609,7 kg activos en 3
    /// reservas; ItalcolEcuador, cero.
    /// </para>
    /// </summary>
    /// <param name="kilosRealesKg">
    /// Los kilos FÍSICOS del galpón, tal como los cuenta la operación: incluyen lo reservado, porque
    /// esos kilos todavía están ahí.
    /// </param>
    public static PlanCuadre Planificar(
        decimal saldoTablaKg, decimal movPostKg, decimal stockKg, decimal kilosRealesKg,
        decimal reservadoActivoKg = 0m)
    {
        // Comparable = lo que hay − lo que ya se consumió sin salir del inventario − lo que entró
        // después del último seguimiento.
        var saldoObjetivo = kilosRealesKg - reservadoActivoKg - movPostKg;

        return new PlanCuadre(
            SaldoTablaKg: saldoTablaKg,
            MovPostKg: movPostKg,
            StockKg: stockKg,
            ReservadoActivoKg: reservadoActivoKg,
            KilosRealesKg: kilosRealesKg,
            DeltaStockKg: kilosRealesKg - stockKg,
            DeltaTablaKg: saldoObjetivo - saldoTablaKg,
            // Mismo número que publica la fila del cuadre: descuadre crudo + reservado.
            DescuadreAntesKg: CuadreAlimentoEngordeCalculos.DescuadreAjustadoPorReservas(
                saldoTablaKg - (stockKg - movPostKg), reservadoActivoKg),
            DescuadreDespuesKg: CuadreAlimentoEngordeCalculos.DescuadreAjustadoPorReservas(
                saldoObjetivo - (kilosRealesKg - movPostKg), reservadoActivoKg));
    }

    /// <summary>
    /// Motivo del rechazo, o <c>null</c> si el ajuste se puede aplicar. Devuelve el texto y no un
    /// booleano porque el usuario tiene que saber CUÁL de las tres cosas está mal.
    /// </summary>
    public static string? Rechazo(PlanCuadre plan, string? motivo)
    {
        if (plan.KilosRealesKg < 0) return MensajeKilosNegativos;
        if ((motivo ?? string.Empty).Trim().Length < MotivoMinimo) return MensajeMotivoRequerido;
        if (!plan.MueveAlgo) return MensajeSinCambio;
        return null;
    }

    /// <summary>
    /// Resumen legible de lo que el ajuste va a hacer. Es el texto de la previsualización del modal
    /// <b>y</b> el que queda en la <c>reason</c> del movimiento: la pantalla y la auditoría dicen lo
    /// mismo porque salen de la misma función.
    /// </summary>
    public static string Describir(PlanCuadre plan)
    {
        var partes = new List<string>();

        if (plan.TocaStock)
            partes.Add(plan.DeltaStockKg > 0
                ? $"sumar {Kg(plan.DeltaStockKg)} kg al inventario"
                : $"descontar {Kg(-plan.DeltaStockKg)} kg del inventario");

        if (plan.TocaTabla)
            partes.Add(plan.DeltaTablaKg > 0
                ? $"sumar {Kg(plan.DeltaTablaKg)} kg a la tabla diaria"
                : $"descontar {Kg(-plan.DeltaTablaKg)} kg de la tabla diaria");

        if (partes.Count == 0) return MensajeSinCambio;

        return $"Cuadre a {Kg(plan.KilosRealesKg)} kg reales: " + string.Join(" y ", partes) + ".";
    }

    private static string Kg(decimal v) => v.ToString("N1", System.Globalization.CultureInfo.InvariantCulture);
}
