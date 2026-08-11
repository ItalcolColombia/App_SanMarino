using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// A7 — unificación de la regla de saldo de aves de levante.
///
/// El módulo de levante aplicaba el <b>delta neto</b> (<c>viejo − nuevo</c>) y
/// <c>SeguimientoDiarioService</c> <b>revierte y reaplica</b>. Antes de mover la regla de un lado al
/// otro había que demostrar que las dos formas dan exactamente el mismo saldo — clamp incluido.
/// Eso es <c>RevertirYAplicarEsIgualAlDeltaNeto</c>, y es lo que convierte el cambio en un refactor
/// en vez de en una modificación de comportamiento.
/// </summary>
public class DescuentoAvesSeguimientoCalculosTests
{
    // ─── El total descontado ────────────────────────────────────────────────────

    [Fact]
    public void El_descuento_suma_mortalidad_seleccion_y_error_de_sexaje()
    {
        Assert.Equal(15, DescuentoAvesSeguimientoCalculos.TotalDescuento(10, 3, 2));
    }

    [Fact]
    public void Sin_ninguno_de_los_tres_no_hay_descuento()
    {
        Assert.Equal(0, DescuentoAvesSeguimientoCalculos.TotalDescuento(0, 0, 0));
    }

    [Fact]
    public void El_error_de_sexaje_cuenta_igual_que_la_mortalidad()
    {
        // Es el componente que una de las dos copias podía olvidarse; queda fijado.
        Assert.Equal(
            DescuentoAvesSeguimientoCalculos.TotalDescuento(5, 0, 0),
            DescuentoAvesSeguimientoCalculos.TotalDescuento(0, 0, 5));
    }

    // ─── El clamp ───────────────────────────────────────────────────────────────

    [Fact]
    public void El_saldo_nunca_queda_negativo()
    {
        Assert.Equal(0, DescuentoAvesSeguimientoCalculos.AplicarDelta(3, -10));
    }

    [Fact]
    public void Un_delta_positivo_suma_normalmente()
    {
        Assert.Equal(13, DescuentoAvesSeguimientoCalculos.AplicarDelta(3, 10));
    }

    [Fact]
    public void El_clamp_hace_la_operacion_NO_reversible()
    {
        // Documenta por qué la escritura offline está bloqueada: descontar 10 sobre un saldo de 3
        // deja 0, y revertir esos mismos 10 deja 10 — no vuelve a 3. Con reintentos de una cola de
        // sincronización, esto multiplica aves de la nada.
        var tras = DescuentoAvesSeguimientoCalculos.AplicarDelta(3, -10);   // 0
        var revertido = DescuentoAvesSeguimientoCalculos.AplicarDelta(tras, 10); // 10

        Assert.Equal(0, tras);
        Assert.Equal(10, revertido);
        Assert.NotEqual(3, revertido);
    }

    // ─── La equivalencia que habilita A7 ────────────────────────────────────────

    [Theory]
    // saldo, descuento viejo, descuento nuevo
    [InlineData(100, 10, 5)]     // se corrige la mortalidad hacia abajo
    [InlineData(100, 5, 10)]     // hacia arriba
    [InlineData(100, 10, 10)]    // sin cambio
    [InlineData(0, 10, 5)]       // saldo en cero
    [InlineData(0, 0, 10)]       // saldo en cero y se agrega mortalidad
    [InlineData(3, 10, 0)]       // el viejo era mayor que el saldo
    [InlineData(3, 0, 10)]       // el nuevo es mayor que el saldo
    [InlineData(5, 0, 0)]        // nada que mover
    [InlineData(50, 50, 50)]     // el descuento consume el saldo entero
    public void RevertirYAplicarEsIgualAlDeltaNeto(int saldo, int viejo, int nuevo)
    {
        var revertirYAplicar = DescuentoAvesSeguimientoCalculos.SaldoTrasEdicion(saldo, viejo, nuevo);
        var deltaNeto = DescuentoAvesSeguimientoCalculos.SaldoTrasEdicionPorDeltaNeto(saldo, viejo, nuevo);

        Assert.Equal(deltaNeto, revertirYAplicar);
    }

    [Fact]
    public void Editar_bajando_la_mortalidad_devuelve_aves_al_saldo()
    {
        // 100 aves, se había registrado 10 de mortalidad (saldo 90) y se corrige a 4.
        Assert.Equal(96, DescuentoAvesSeguimientoCalculos.SaldoTrasEdicion(90, 10, 4));
    }

    [Fact]
    public void Editar_subiendo_la_mortalidad_descuenta_la_diferencia()
    {
        Assert.Equal(85, DescuentoAvesSeguimientoCalculos.SaldoTrasEdicion(90, 10, 15));
    }

    [Fact]
    public void Borrar_un_registro_devuelve_exactamente_lo_que_habia_descontado()
    {
        // Es lo que hace `RestaurarAvesLevanteAsync`: revertir sin aplicar nada nuevo.
        Assert.Equal(100, DescuentoAvesSeguimientoCalculos.AplicarDelta(90, 10));
    }
}
