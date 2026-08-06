using Xunit;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests de las reglas puras de la hoja "Movimientos Aves" de la carga masiva de levante:
/// interpretación del tipo (con sinónimos), proyección del saldo de aves y clave de duplicado.
/// </summary>
public class MigracionMovimientosAvesCalculosTests
{
    // ── TryMovimiento ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Salida")]
    [InlineData("SALIDA")]
    [InlineData("  salidas ")]
    [InlineData("Traslado Salida")]
    [InlineData("salida traslado")]
    [InlineData("Salida de aves")]
    [InlineData("Envío")]   // acento: NormalizarClave lo aplana
    public void TryMovimiento_reconoce_salida_con_sinonimos(string texto)
    {
        Assert.True(MigracionMovimientosAvesCalculos.TryMovimiento(texto, out var tipo));
        Assert.Equal(MovimientoAvesMigracion.Salida, tipo);
    }

    [Theory]
    [InlineData("Ingreso")]
    [InlineData("ingresos")]
    [InlineData("ENTRADA")]
    [InlineData("entradas")]
    [InlineData("Traslado Ingreso")]
    [InlineData("ingreso traslado")]
    [InlineData("Ingreso de aves")]
    [InlineData("Ingreso en tránsito")]
    [InlineData("En tránsito")]
    [InlineData("transito")]
    public void TryMovimiento_reconoce_ingreso_con_sinonimos(string texto)
    {
        Assert.True(MigracionMovimientosAvesCalculos.TryMovimiento(texto, out var tipo));
        Assert.Equal(MovimientoAvesMigracion.Ingreso, tipo);
    }

    [Theory]
    [InlineData("Venta")]
    [InlineData("VENTAS")]
    [InlineData("Venta de aves")]
    [InlineData("venta aves")]
    public void TryMovimiento_reconoce_venta_con_sinonimos(string texto)
    {
        Assert.True(MigracionMovimientosAvesCalculos.TryMovimiento(texto, out var tipo));
        Assert.Equal(MovimientoAvesMigracion.Venta, tipo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Retiro")]
    [InlineData("Traslado")]  // ambiguo a propósito: no dice el sentido
    [InlineData("cualquier cosa")]
    public void TryMovimiento_rechaza_vacio_y_texto_no_reconocido(string? texto)
    {
        // Sin default (a diferencia de la hoja Alimento): el tipo cambia el SIGNO del movimiento.
        Assert.False(MigracionMovimientosAvesCalculos.TryMovimiento(texto, out _));
    }

    // ── ProyectarSaldoAves ────────────────────────────────────────────────────

    [Fact]
    public void ProyectarSaldoAves_resta_bajas_y_salidas_y_suma_ingresos()
    {
        var p = MigracionMovimientosAvesCalculos.ProyectarSaldoAves(
            avesHActual: 1000, avesMActual: 100,
            bajasArchivoH: 50, bajasArchivoM: 5,
            salidasH: 300, salidasM: 20,
            ingresosH: 40, ingresosM: 10);

        Assert.Equal(690, p.Hembras);   // 1000 − 50 − 300 + 40
        Assert.Equal(85, p.Machos);     // 100 − 5 − 20 + 10
        Assert.False(p.AlgunoNegativo);
    }

    [Fact]
    public void ProyectarSaldoAves_detecta_negativo_por_sexo()
    {
        var p = MigracionMovimientosAvesCalculos.ProyectarSaldoAves(
            avesHActual: 100, avesMActual: 100,
            bajasArchivoH: 0, bajasArchivoM: 0,
            salidasH: 150, salidasM: 0,
            ingresosH: 0, ingresosM: 0);

        Assert.Equal(-50, p.Hembras);
        Assert.Equal(100, p.Machos);
        Assert.True(p.AlgunoNegativo);
    }

    // ── ClaveArchivo ──────────────────────────────────────────────────────────

    [Fact]
    public void ClaveArchivo_distingue_fecha_tipo_y_cantidades()
    {
        var fecha = new DateTime(2026, 7, 15);
        var clave = MigracionMovimientosAvesCalculos.ClaveArchivo(fecha, MovimientoAvesMigracion.Salida, 100, 20);

        Assert.Equal(clave, MigracionMovimientosAvesCalculos.ClaveArchivo(fecha, MovimientoAvesMigracion.Salida, 100, 20));
        Assert.NotEqual(clave, MigracionMovimientosAvesCalculos.ClaveArchivo(fecha.AddDays(1), MovimientoAvesMigracion.Salida, 100, 20));
        Assert.NotEqual(clave, MigracionMovimientosAvesCalculos.ClaveArchivo(fecha, MovimientoAvesMigracion.Ingreso, 100, 20));
        Assert.NotEqual(clave, MigracionMovimientosAvesCalculos.ClaveArchivo(fecha, MovimientoAvesMigracion.Salida, 101, 20));
        Assert.NotEqual(clave, MigracionMovimientosAvesCalculos.ClaveArchivo(fecha, MovimientoAvesMigracion.Salida, 100, 21));
    }
}

/// <summary>
/// La Salida de un lote y el Ingreso del otro escriben filas IDÉNTICAS en movimiento_aves
/// (Traslado, mismo origen, mismo destino). Sin desempate, la idempotencia del Ingreso encontraba
/// la Salida del vecino y lo omitía sin acreditar las aves.
/// </summary>
public class LadoDelMovimientoTests
{
    const int A = 135, B = 136;
    static MovimientoAvesMigracion? Lado(string tipo, int? origen, int? destino, string? desc, int lote) =>
        MigracionMovimientosAvesCalculos.LadoDelMovimiento(tipo, origen, destino, desc, lote);

    [Fact]
    public void La_salida_de_A_no_bloquea_el_ingreso_de_B()
    {
        // La fila la creó la SALIDA de A. Para B no es su Ingreso: no debe omitir nada.
        Assert.Null(Lado("Traslado", A, B, MigracionMovimientosAvesCalculos.MarcaCargaSalida, B));
        // Y para A sí es su Salida.
        Assert.Equal(MovimientoAvesMigracion.Salida,
            Lado("Traslado", A, B, MigracionMovimientosAvesCalculos.MarcaCargaSalida, A));
    }

    [Fact]
    public void El_ingreso_de_B_es_suyo_y_no_de_A()
    {
        Assert.Equal(MovimientoAvesMigracion.Ingreso,
            Lado("Traslado", A, B, MigracionMovimientosAvesCalculos.MarcaCargaIngreso, B));
        Assert.Null(Lado("Traslado", A, B, MigracionMovimientosAvesCalculos.MarcaCargaIngreso, A));
    }

    [Fact]
    public void Reimportar_el_mismo_archivo_sigue_omitiendo()
    {
        // Idempotencia: B reimporta su Ingreso y encuentra el suyo.
        Assert.Equal(MovimientoAvesMigracion.Ingreso,
            Lado("Traslado", A, B, MigracionMovimientosAvesCalculos.MarcaCargaIngreso, B));
        // A reimporta su Salida y encuentra la suya.
        Assert.Equal(MovimientoAvesMigracion.Salida,
            Lado("Traslado", A, B, MigracionMovimientosAvesCalculos.MarcaCargaSalida, A));
    }

    [Fact]
    public void La_venta_solo_cuenta_para_el_lote_que_la_hizo()
    {
        Assert.Equal(MovimientoAvesMigracion.Venta,
            Lado("Venta", A, null, MigracionMovimientosAvesCalculos.MarcaCargaVenta, A));
        Assert.Null(Lado("Venta", A, null, MigracionMovimientosAvesCalculos.MarcaCargaVenta, B));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Traslado manual desde la pantalla")]
    public void Sin_marca_se_conserva_la_heuristica_historica(string? desc)
    {
        Assert.Equal(MovimientoAvesMigracion.Salida, Lado("Traslado", A, B, desc, A));
        Assert.Equal(MovimientoAvesMigracion.Ingreso, Lado("Traslado", A, B, desc, B));
    }

    [Fact]
    public void Un_movimiento_de_otros_lotes_no_es_de_este()
    {
        Assert.Null(Lado("Traslado", 1, 2, null, A));
        Assert.Null(Lado("Venta", 1, null, null, A));
    }

    [Fact]
    public void Un_tipo_desconocido_no_clasifica()
    {
        Assert.Null(Lado("Ajuste", A, B, null, A));
        Assert.Null(Lado(null, A, B, null, A));
    }

    [Fact]
    public void Un_ingreso_en_transito_sin_contraparte_es_del_receptor()
    {
        Assert.Equal(MovimientoAvesMigracion.Ingreso,
            Lado("Traslado", null, B, MigracionMovimientosAvesCalculos.MarcaCargaIngreso, B));
    }
}
