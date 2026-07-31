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
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Venta")]
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
