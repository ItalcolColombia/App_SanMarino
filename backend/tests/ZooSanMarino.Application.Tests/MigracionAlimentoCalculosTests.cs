using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Migracion;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Hoja "Alimento" de la carga masiva de seguimiento pollo engorde: normalización de movimiento/origen
/// y la SIMULACIÓN de balance que permite rechazar en el dry-run un archivo que dejaría el galpón en
/// negativo (antes el descuento fallaba dentro de un catch y el inventario quedaba mal en silencio).
/// <para>
/// Caso testigo de los números: galpón 6 de DAYLAND — 155.188,243 kg de entradas, 7.166,829 kg de
/// consumo en la primera semana y 145.786,084 kg entre los días 8 y 41 ⇒ saldo 2.235,330 kg.
/// </para>
/// </summary>
public class MigracionAlimentoCalculosTests
{
    private static readonly UbicacionAlimento Galpon6 = new(107, "353105", "G0471");
    private static readonly UbicacionAlimento Galpon5 = new(107, "353105", "G0460");
    private const int Preiniciador = 223;
    private const int Iniciacion = 214;
    private const int Engorde = 213;

    private static PosicionAlimento Pos(UbicacionAlimento u, int item) => new(u, item);

    // ── Movimiento ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Ingreso")]
    [InlineData("ingreso")]
    [InlineData("INGRESO")]
    [InlineData("Entrada")]
    [InlineData("compra")]
    public void TryMovimiento_Ingreso(string texto)
    {
        Assert.True(MigracionAlimentoCalculos.TryMovimiento(texto, out var m));
        Assert.Equal(MovimientoAlimento.Ingreso, m);
    }

    [Theory]
    [InlineData("Traslado")]
    [InlineData("transferencia")]
    [InlineData("Salida")]
    public void TryMovimiento_Traslado(string texto)
    {
        Assert.True(MigracionAlimentoCalculos.TryMovimiento(texto, out var m));
        Assert.Equal(MovimientoAlimento.Traslado, m);
    }

    [Theory]
    [InlineData("Recepción")]
    [InlineData("Recepcion")]
    [InlineData("RECEPCIÓN")]
    [InlineData("ingreso por traslado")]
    public void TryMovimiento_Recepcion_ToleraAcentoYMayusculas(string texto)
    {
        Assert.True(MigracionAlimentoCalculos.TryMovimiento(texto, out var m));
        Assert.Equal(MovimientoAlimento.Recepcion, m);
    }

    [Theory]
    [InlineData("Consumo")]
    [InlineData("consumo")]
    [InlineData("CONSUMIDO")]
    public void TryMovimiento_Consumo(string texto)
    {
        // Salida de alimento que ningún día de seguimiento descontó: la primera semana del lote (que
        // se digita en reproductora y queda confirmada) o un histórico cargado antes del descuento.
        Assert.True(MigracionAlimentoCalculos.TryMovimiento(texto, out var m));
        Assert.Equal(MovimientoAlimento.Consumo, m);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryMovimiento_Vacio_EsIngreso(string? texto)
    {
        Assert.True(MigracionAlimentoCalculos.TryMovimiento(texto, out var m));
        Assert.Equal(MovimientoAlimento.Ingreso, m);
    }

    [Theory]
    [InlineData("devolucion")]
    [InlineData("ajuste")]
    [InlineData("xyz")]
    public void TryMovimiento_Desconocido_Falla(string texto) =>
        Assert.False(MigracionAlimentoCalculos.TryMovimiento(texto, out _));

    // ── Origen del ingreso ───────────────────────────────────────────────────

    [Theory]
    [InlineData(null, "planta")]
    [InlineData("planta", "planta")]
    [InlineData("Bodega", "bodega")]
    [InlineData("GRANJA", "granja")]
    [InlineData("otra granja", "granja")]
    public void TryOrigenIngreso_Normaliza(string? texto, string esperado)
    {
        Assert.True(MigracionAlimentoCalculos.TryOrigenIngreso(texto, out var o));
        Assert.Equal(esperado, o);
    }

    [Fact]
    public void TryOrigenIngreso_Desconocido_Falla() =>
        Assert.False(MigracionAlimentoCalculos.TryOrigenIngreso("proveedor", out _));

    // ── Ubicación ────────────────────────────────────────────────────────────

    [Fact]
    public void Ubicacion_Normalizada_ConvierteVaciosANull()
    {
        var u = new UbicacionAlimento(107, "  ", "").Normalizada();
        Assert.Null(u.NucleoId);
        Assert.Null(u.GalponId);
    }

    [Fact]
    public void Ubicacion_Normalizada_RecortaEspacios()
    {
        var u = new UbicacionAlimento(107, " 353105 ", " G0471 ").Normalizada();
        Assert.Equal("353105", u.NucleoId);
        Assert.Equal("G0471", u.GalponId);
    }

    [Fact]
    public void Posicion_MismaUbicacionEItem_SonIguales() =>
        Assert.Equal(Pos(new UbicacionAlimento(107, "353105", "G0471"), Engorde), Pos(Galpon6, Engorde));

    [Fact]
    public void Posicion_DistintoGalpon_NoSonIguales() =>
        Assert.NotEqual(Pos(Galpon6, Engorde), Pos(Galpon5, Engorde));

    // ── Simulación de balance ────────────────────────────────────────────────

    [Fact]
    public void Simular_SinSalidas_NoHayFaltantes()
    {
        var entradas = new Dictionary<PosicionAlimento, decimal> { [Pos(Galpon6, Engorde)] = 1000m };
        var faltantes = MigracionAlimentoCalculos.Simular(
            new Dictionary<PosicionAlimento, decimal>(), entradas, new Dictionary<PosicionAlimento, decimal>());
        Assert.Empty(faltantes);
    }

    [Fact]
    public void Simular_EntradaCubreExactamenteElConsumo_NoHayFaltante()
    {
        var entradas = new Dictionary<PosicionAlimento, decimal> { [Pos(Galpon6, Engorde)] = 1000m };
        var salidas = new Dictionary<PosicionAlimento, decimal> { [Pos(Galpon6, Engorde)] = 1000m };
        Assert.Empty(MigracionAlimentoCalculos.Simular(new Dictionary<PosicionAlimento, decimal>(), entradas, salidas));
    }

    [Fact]
    public void Simular_ConsumoSinStockNiEntrada_ReportaElTotalComoFaltante()
    {
        var salidas = new Dictionary<PosicionAlimento, decimal> { [Pos(Galpon6, Engorde)] = 500m };
        var faltantes = MigracionAlimentoCalculos.Simular(
            new Dictionary<PosicionAlimento, decimal>(), new Dictionary<PosicionAlimento, decimal>(), salidas);

        var f = Assert.Single(faltantes);
        Assert.Equal(Pos(Galpon6, Engorde), f.Posicion);
        Assert.Equal(0m, f.Disponible);
        Assert.Equal(500m, f.Requerido);
        Assert.Equal(500m, f.Faltante);
    }

    [Fact]
    public void Simular_StockPrevioSeSumaALaEntrada()
    {
        var stock = new Dictionary<PosicionAlimento, decimal> { [Pos(Galpon6, Engorde)] = 300m };
        var entradas = new Dictionary<PosicionAlimento, decimal> { [Pos(Galpon6, Engorde)] = 200m };
        var salidas = new Dictionary<PosicionAlimento, decimal> { [Pos(Galpon6, Engorde)] = 500m };
        Assert.Empty(MigracionAlimentoCalculos.Simular(stock, entradas, salidas));
    }

    [Fact]
    public void Simular_FaltanteEsLaDiferenciaExacta()
    {
        var stock = new Dictionary<PosicionAlimento, decimal> { [Pos(Galpon6, Engorde)] = 100m };
        var entradas = new Dictionary<PosicionAlimento, decimal> { [Pos(Galpon6, Engorde)] = 50.5m };
        var salidas = new Dictionary<PosicionAlimento, decimal> { [Pos(Galpon6, Engorde)] = 200.75m };

        var f = Assert.Single(MigracionAlimentoCalculos.Simular(stock, entradas, salidas));
        Assert.Equal(150.5m, f.Disponible);
        Assert.Equal(50.25m, f.Faltante);
    }

    [Fact]
    public void Simular_ElStockDeOtroGalponNoTapaElFaltante()
    {
        // Fail-closed por ubicación: en Panamá el alimento vive a nivel galpón; tener stock en el
        // galpón 5 no habilita a consumir en el 6.
        var stock = new Dictionary<PosicionAlimento, decimal> { [Pos(Galpon5, Engorde)] = 10_000m };
        var salidas = new Dictionary<PosicionAlimento, decimal> { [Pos(Galpon6, Engorde)] = 100m };

        var f = Assert.Single(MigracionAlimentoCalculos.Simular(stock, new Dictionary<PosicionAlimento, decimal>(), salidas));
        Assert.Equal(Pos(Galpon6, Engorde), f.Posicion);
        Assert.Equal(0m, f.Disponible);
    }

    [Fact]
    public void Simular_ElStockDeOtroAlimentoNoTapaElFaltante()
    {
        var stock = new Dictionary<PosicionAlimento, decimal> { [Pos(Galpon6, Preiniciador)] = 10_000m };
        var salidas = new Dictionary<PosicionAlimento, decimal> { [Pos(Galpon6, Engorde)] = 100m };
        Assert.Single(MigracionAlimentoCalculos.Simular(stock, new Dictionary<PosicionAlimento, decimal>(), salidas));
    }

    [Fact]
    public void Simular_VariasPosiciones_ReportaSoloLasNegativas()
    {
        var entradas = new Dictionary<PosicionAlimento, decimal>
        {
            [Pos(Galpon6, Preiniciador)] = 12_129.638m,
            [Pos(Galpon6, Iniciacion)] = 20_135.172m,
            [Pos(Galpon6, Engorde)] = 100m,
        };
        var salidas = new Dictionary<PosicionAlimento, decimal>
        {
            [Pos(Galpon6, Preiniciador)] = 12_129.638m,
            [Pos(Galpon6, Iniciacion)] = 20_135.172m,
            [Pos(Galpon6, Engorde)] = 120_688.103m,
        };

        var f = Assert.Single(MigracionAlimentoCalculos.Simular(new Dictionary<PosicionAlimento, decimal>(), entradas, salidas));
        Assert.Equal(Pos(Galpon6, Engorde), f.Posicion);
        Assert.Equal(120_588.103m, f.Faltante);
    }

    [Fact]
    public void Simular_SalidaEnCero_NoEsFaltante()
    {
        var salidas = new Dictionary<PosicionAlimento, decimal> { [Pos(Galpon6, Engorde)] = 0m };
        Assert.Empty(MigracionAlimentoCalculos.Simular(
            new Dictionary<PosicionAlimento, decimal>(), new Dictionary<PosicionAlimento, decimal>(), salidas));
    }

    [Fact]
    public void Simular_LaCronologiaNoImporta_ElArchivoSeAplicaEntradasPrimero()
    {
        // Caso real del galpón 6: las llegadas están fechadas DESPUÉS del consumo que las gasta (el
        // saldo día a día llega a −10.634 kg), pero como las entradas del archivo se aplican antes que
        // los consumos, el balance total alcanza y no hay faltante.
        var entradas = new Dictionary<PosicionAlimento, decimal> { [Pos(Galpon6, Engorde)] = 122_923.433m };
        var salidas = new Dictionary<PosicionAlimento, decimal> { [Pos(Galpon6, Engorde)] = 120_688.103m };
        Assert.Empty(MigracionAlimentoCalculos.Simular(new Dictionary<PosicionAlimento, decimal>(), entradas, salidas));
    }

    // ── Proyección de saldos ─────────────────────────────────────────────────

    [Fact]
    public void Proyectar_CasoGalpon6_DejaElSaldoEsperado()
    {
        var entradas = new Dictionary<PosicionAlimento, decimal>
        {
            [Pos(Galpon6, Preiniciador)] = 12_129.638m,
            [Pos(Galpon6, Iniciacion)] = 20_135.172m,
            [Pos(Galpon6, Engorde)] = 122_923.433m,
        };
        var salidas = new Dictionary<PosicionAlimento, decimal>
        {
            [Pos(Galpon6, Preiniciador)] = 12_129.638m, // 7.166,829 semana 1 + 4.962,809 días 8+
            [Pos(Galpon6, Iniciacion)] = 20_135.172m,
            [Pos(Galpon6, Engorde)] = 120_688.103m,
        };

        var saldos = MigracionAlimentoCalculos.Proyectar(new Dictionary<PosicionAlimento, decimal>(), entradas, salidas);

        Assert.Equal(3, saldos.Count);
        Assert.Equal(0m, saldos.Single(s => s.Posicion.ItemId == Preiniciador).SaldoFinal);
        Assert.Equal(0m, saldos.Single(s => s.Posicion.ItemId == Iniciacion).SaldoFinal);
        Assert.Equal(2_235.330m, saldos.Single(s => s.Posicion.ItemId == Engorde).SaldoFinal);
        Assert.Equal(2_235.330m, saldos.Sum(s => s.SaldoFinal));
    }

    [Fact]
    public void Proyectar_CasoGalpon6Completo_CierraEn2235()
    {
        // El galpón 6 real: los 7 días de la primera semana entran como movimiento "Consumo" de la
        // hoja (los seguimientos de reproductora ya están CONFIRMADOS y no se pueden corregir), y los
        // días 8-41 como consumo del seguimiento. El PREINICIADOR tiene que cerrar en cero.
        var entradas = new Dictionary<PosicionAlimento, decimal>
        {
            [Pos(Galpon6, Preiniciador)] = 12_129.638m,
            [Pos(Galpon6, Iniciacion)] = 20_135.171m,
            [Pos(Galpon6, Engorde)] = 122_923.435m,
        };
        var salidas = new Dictionary<PosicionAlimento, decimal>
        {
            [Pos(Galpon6, Preiniciador)] = 7_166.832m + 4_962.805m, // semana 1 (Consumo) + días 8-41
            [Pos(Galpon6, Iniciacion)] = 20_135.171m,
            [Pos(Galpon6, Engorde)] = 120_688.104m,
        };

        var saldos = MigracionAlimentoCalculos.Proyectar(new Dictionary<PosicionAlimento, decimal>(), entradas, salidas);

        Assert.Equal(0.001m, saldos.Single(s => s.Posicion.ItemId == Preiniciador).SaldoFinal);
        Assert.Equal(0m, saldos.Single(s => s.Posicion.ItemId == Iniciacion).SaldoFinal);
        Assert.Equal(2_235.331m, saldos.Single(s => s.Posicion.ItemId == Engorde).SaldoFinal);
        Assert.Empty(MigracionAlimentoCalculos.Simular(new Dictionary<PosicionAlimento, decimal>(), entradas, salidas));
    }

    [Fact]
    public void ClaveIdempotencia_ConsumoYIngresoIgualesSonDistintos() =>
        // Un ingreso y un consumo del mismo alimento, día y cantidad son movimientos opuestos: si
        // compartieran clave, cargar uno haría omitir el otro.
        Assert.NotEqual(
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Ingreso, Galpon6, Preiniciador, new DateTime(2026, 6, 8), 635.036m, "R1"),
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Consumo, Galpon6, Preiniciador, new DateTime(2026, 6, 8), 635.036m, "R1"));

    [Fact]
    public void Proyectar_IncluyeElSaldoInicialDeLaBD()
    {
        var stock = new Dictionary<PosicionAlimento, decimal> { [Pos(Galpon6, Engorde)] = 500m };
        var entradas = new Dictionary<PosicionAlimento, decimal> { [Pos(Galpon6, Engorde)] = 1_000m };
        var salidas = new Dictionary<PosicionAlimento, decimal> { [Pos(Galpon6, Engorde)] = 200m };

        var s = Assert.Single(MigracionAlimentoCalculos.Proyectar(stock, entradas, salidas));
        Assert.Equal(500m, s.SaldoInicial);
        Assert.Equal(1_000m, s.Entradas);
        Assert.Equal(200m, s.Salidas);
        Assert.Equal(1_300m, s.SaldoFinal);
    }

    [Fact]
    public void Proyectar_SinMovimientos_NoDevuelveNada() =>
        Assert.Empty(MigracionAlimentoCalculos.Proyectar(
            new Dictionary<PosicionAlimento, decimal> { [Pos(Galpon6, Engorde)] = 999m },
            new Dictionary<PosicionAlimento, decimal>(),
            new Dictionary<PosicionAlimento, decimal>()));

    // ── Acumular ─────────────────────────────────────────────────────────────

    [Fact]
    public void Acumular_SumaSobreLaMismaPosicion()
    {
        var d = new Dictionary<PosicionAlimento, decimal>();
        MigracionAlimentoCalculos.Acumular(d, Pos(Galpon6, Engorde), 100m);
        MigracionAlimentoCalculos.Acumular(d, Pos(Galpon6, Engorde), 50.5m);
        Assert.Equal(150.5m, d[Pos(Galpon6, Engorde)]);
    }

    [Fact]
    public void Acumular_Cero_NoCreaLaPosicion()
    {
        var d = new Dictionary<PosicionAlimento, decimal>();
        MigracionAlimentoCalculos.Acumular(d, Pos(Galpon6, Engorde), 0m);
        Assert.Empty(d);
    }

    // ── Idempotencia ─────────────────────────────────────────────────────────

    [Fact]
    public void ClaveIdempotencia_MismosDatos_MismaClave()
    {
        var f = new DateTime(2026, 6, 29);
        Assert.Equal(
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Ingreso, Galpon6, Engorde, f, 6_392.542864m, "REM-1"),
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Ingreso, Galpon6, Engorde, f, 6_392.542864m, "REM-1"));
    }

    [Fact]
    public void ClaveIdempotencia_RedondeaATresDecimales()
    {
        var f = new DateTime(2026, 6, 29);
        Assert.Equal(
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Ingreso, Galpon6, Engorde, f, 6_392.5428m, null),
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Ingreso, Galpon6, Engorde, f, 6_392.5431m, null));
    }

    [Fact]
    public void ClaveIdempotencia_LaEscalaDelDecimalNoCambiaLaClave()
    {
        // Regresión: el Excel entrega 2717.5 y la columna numeric devuelve 2717.500. Son el mismo
        // número, pero decimal conserva la escala y con ToString() sin formato daban claves distintas
        // ⇒ el reintento volvía a aplicar el ingreso y duplicaba el stock del galpón.
        var f = new DateTime(2026, 7, 14);
        Assert.Equal(
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Ingreso, Galpon6, Engorde, f, 2717.5m, "LLEG-22"),
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Ingreso, Galpon6, Engorde, f, 2717.500m, "LLEG-22"));
    }

    [Theory]
    [InlineData(2402.25, 2402.250)]
    [InlineData(100, 100.000)]
    [InlineData(0.1, 0.100)]
    public void ClaveIdempotencia_EscalaDistinta_MismoValor_MismaClave(decimal a, decimal b)
    {
        var f = new DateTime(2026, 7, 16);
        Assert.Equal(
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Ingreso, Galpon6, Engorde, f, a, null),
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Ingreso, Galpon6, Engorde, f, b, null));
    }

    [Fact]
    public void ClaveIdempotencia_LaHoraNoCuenta_SoloElDia()
    {
        Assert.Equal(
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Ingreso, Galpon6, Engorde, new DateTime(2026, 6, 29, 0, 0, 0), 100m, null),
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Ingreso, Galpon6, Engorde, new DateTime(2026, 6, 29, 23, 59, 0), 100m, null));
    }

    [Fact]
    public void ClaveIdempotencia_LaReferenciaDistingueDosEntradasDelMismoDia()
    {
        // El archivo real trae DOS ingresos el 29/06 y dos el 04/07: la referencia es lo que evita que
        // el segundo se tome por repetido.
        var f = new DateTime(2026, 6, 29);
        Assert.NotEqual(
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Ingreso, Galpon6, Engorde, f, 100m, "REM-1"),
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Ingreso, Galpon6, Engorde, f, 100m, "REM-2"));
    }

    [Theory]
    [InlineData("REM-1", "rem-1")]
    [InlineData("Remisión 5", "remision 5")]
    [InlineData("  REM 7  ", "rem 7")]
    public void ClaveIdempotencia_ReferenciaSinMayusculasNiAcentos(string a, string b)
    {
        var f = new DateTime(2026, 6, 29);
        Assert.Equal(
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Ingreso, Galpon6, Engorde, f, 100m, a),
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Ingreso, Galpon6, Engorde, f, 100m, b));
    }

    [Fact]
    public void ClaveIdempotencia_LaPuntuacionDeLaReferenciaSiCuenta() =>
        // "REM-1" y "REM 1" son remisiones distintas: normalizar solo mayúsculas/acentos, no la
        // puntuación, evita fusionar dos entradas reales en una.
        Assert.NotEqual(
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Ingreso, Galpon6, Engorde, new DateTime(2026, 6, 29), 100m, "REM-1"),
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Ingreso, Galpon6, Engorde, new DateTime(2026, 6, 29), 100m, "REM 1"));

    [Fact]
    public void ClaveIdempotencia_DistintoMovimiento_DistintaClave()
    {
        var f = new DateTime(2026, 6, 29);
        Assert.NotEqual(
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Ingreso, Galpon6, Engorde, f, 100m, null),
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Traslado, Galpon6, Engorde, f, 100m, null));
    }

    [Fact]
    public void ClaveIdempotencia_DistintoGalpon_DistintaClave()
    {
        var f = new DateTime(2026, 6, 29);
        Assert.NotEqual(
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Ingreso, Galpon6, Engorde, f, 100m, null),
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Ingreso, Galpon5, Engorde, f, 100m, null));
    }

    [Fact]
    public void ClaveIdempotencia_UbicacionSinNormalizarEsEquivalente()
    {
        var f = new DateTime(2026, 6, 29);
        Assert.Equal(
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Ingreso, Galpon6, Engorde, f, 100m, null),
            MigracionAlimentoCalculos.ClaveIdempotencia(MovimientoAlimento.Ingreso, new UbicacionAlimento(107, " 353105 ", " G0471 "), Engorde, f, 100m, null));
    }

    // ── Esquema de la hoja ───────────────────────────────────────────────────

    [Fact]
    public void EsquemaAlimento_UsaLaHojaAlimento() =>
        Assert.Equal("Alimento", MigracionEsquemas.AlimentoEngorde.Hoja);

    [Fact]
    public void EsquemaAlimento_SoloFechaAlimentoYCantidadSonObligatorias()
    {
        var requeridas = MigracionEsquemas.AlimentoEngorde.Columnas.Where(c => c.Requerida).Select(c => c.Titulo).ToList();
        Assert.Equal(new[] { "Fecha", "Alimento", "Cantidad" }, requeridas);
    }

    [Fact]
    public void EsquemaAlimento_NoTieneTitulosDuplicados()
    {
        var claves = MigracionEsquemas.AlimentoEngorde.Columnas
            .Select(c => MigracionCalculos.NormalizarClave(c.Titulo)).ToList();
        Assert.Equal(claves.Count, claves.Distinct().Count());
    }

    [Theory]
    [InlineData("Movimiento", "tipo movimiento")]
    [InlineData("Cantidad", "cantidad kg")]
    [InlineData("Granja", "granja destino")]
    [InlineData("Granja Origen", "desde granja")]
    public void EsquemaAlimento_AceptaLosAlias(string titulo, string alias) =>
        Assert.Contains(MigracionCalculos.NormalizarClave(alias),
            MigracionEsquemaCalculos.ClavesDeColumna(MigracionEsquemas.AlimentoEngorde, titulo));

    [Fact]
    public void EsquemaAlimento_NoEsUnTipoDeMigracion() =>
        // Es una hoja auxiliar del archivo de seguimiento engorde, no una línea propia del catálogo.
        Assert.DoesNotContain(MigracionEsquemas.TiposConEsquema,
            t => MigracionEsquemas.Para(t).Hoja == MigracionEsquemas.AlimentoEngorde.Hoja);

    // ── Archivo unificado: una hoja por módulo, identificada por NOMBRE ──────

    [Fact]
    public void HojasDelArchivoUnificado_TienenNombresDistintos()
    {
        // "Datos" (días 8+), "Alimento" (inventario) y "Reproductora" (primera semana) conviven en el
        // mismo .xlsx; si dos compartieran nombre, el lector tomaría una por la otra.
        var hojas = new[]
        {
            MigracionEsquemas.SeguimientoPolloEngorde.Hoja,
            MigracionEsquemas.AlimentoEngorde.Hoja,
            MigracionEsquemas.ReproductoraEnHoja.Hoja,
        };
        Assert.Equal(hojas.Length, hojas.Select(MigracionCalculos.NormalizarClave).Distinct().Count());
    }

    [Fact]
    public void ReproductoraEnHoja_SoloCambiaElNombreDeLaHoja()
    {
        // Mismas columnas, mismos alias, mismo orden: cargar la primera semana desde el archivo
        // unificado tiene que validar EXACTAMENTE igual que la línea de migración dedicada.
        var dedicada = MigracionEsquemas.SeguimientoReproductoraEngorde;
        var enHoja = MigracionEsquemas.ReproductoraEnHoja;

        Assert.Equal("Reproductora", enHoja.Hoja);
        Assert.Equal("Datos", dedicada.Hoja);
        Assert.Equal(dedicada.MaxFilas, enHoja.MaxFilas);
        Assert.Equal(dedicada.Columnas.Count, enHoja.Columnas.Count);
        Assert.Equal(
            dedicada.Columnas.Select(c => c.Titulo),
            enHoja.Columnas.Select(c => c.Titulo));
        Assert.Equal(
            dedicada.Columnas.Select(c => (c.Requerida, string.Join('|', c.Alias ?? Array.Empty<string>()))),
            enHoja.Columnas.Select(c => (c.Requerida, string.Join('|', c.Alias ?? Array.Empty<string>()))));
    }

    [Theory]
    [InlineData("Alimento 1 H")]
    [InlineData("Consumo Alimento 1 H")]
    [InlineData("Reproductora")]
    [InlineData("Fecha")]
    public void ReproductoraEnHoja_ResuelveLasMismasClaves(string titulo) =>
        Assert.Equal(
            MigracionEsquemaCalculos.ClavesDeColumna(MigracionEsquemas.SeguimientoReproductoraEngorde, titulo),
            MigracionEsquemaCalculos.ClavesDeColumna(MigracionEsquemas.ReproductoraEnHoja, titulo));

    [Fact]
    public void ReproductoraEnHoja_NoEsUnTipoDeMigracionPropio() =>
        // Es una hoja del archivo de engorde; la línea dedicada sigue existiendo y usando "Datos".
        Assert.DoesNotContain(MigracionEsquemas.TiposConEsquema,
            t => MigracionEsquemas.Para(t).Hoja == MigracionEsquemas.ReproductoraEnHoja.Hoja);

    // ── Reproductora: columnas de alimento (primera semana descuenta) ────────

    [Theory]
    [InlineData("Alimento 1 H")]
    [InlineData("Consumo Alimento 1 H")]
    [InlineData("Alimento 2 H")]
    [InlineData("Consumo Alimento 2 H")]
    [InlineData("Alimento 1 M")]
    [InlineData("Consumo Alimento 1 M")]
    [InlineData("Alimento 2 M")]
    [InlineData("Consumo Alimento 2 M")]
    public void EsquemaReproductora_TieneLosSlotsDeAlimento(string titulo) =>
        Assert.Contains(MigracionEsquemas.SeguimientoReproductoraEngorde.Columnas, c => c.Titulo == titulo);

    [Fact]
    public void EsquemaReproductora_LosSlotsDeAlimentoSonOpcionales() =>
        // Aditivo: un archivo de reproductora anterior, sin estas columnas, se sigue procesando igual.
        Assert.All(
            MigracionEsquemas.SeguimientoReproductoraEngorde.Columnas.Where(c => c.Titulo.Contains("Alimento") && c.Titulo != "Tipo Alimento"),
            c => Assert.False(c.Requerida));

    [Fact]
    public void EsquemaReproductora_SoloLaFechaEsObligatoria() =>
        Assert.Equal(new[] { "Fecha" },
            MigracionEsquemas.SeguimientoReproductoraEngorde.Columnas.Where(c => c.Requerida).Select(c => c.Titulo));
}
