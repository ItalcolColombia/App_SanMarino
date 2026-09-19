using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato: una fila de seguimiento que nadie marcó como «separada» <b>nace validada</b>.
///
/// <para>
/// <b>Por qué existe.</b> <c>validado</c> significa «su efecto ya se aplicó». Un registro solo está sin
/// validar mientras tiene alimento o aves SEPARADOS esperando su aplicación, y eso lo dicen de forma
/// explícita los caminos que separan (<c>Validado = !separa</c>). Con el valor inicial en <c>false</c>
/// cada creador de filas tenía que acordarse de marcarlas, y el defecto se repitió cuatro veces (los
/// Crud, los renglones de traslado, el cruce de reproductora y la rama Colombia de Producción,
/// 18-sep-2026: 4 registros de Santa Reyes con su consumo ya aplicado quedaron «pendientes»). Cada
/// vez, el día que una empresa encendía la doble validación esas filas aparecían pendientes, a las 24 h
/// pasaban a EN RETRASO y bloqueaban el alta de días nuevos del lote sin tener nada que validar.
/// </para>
///
/// <para>
/// Estos tests fijan el valor inicial <c>true</c> de las tres entidades para que nadie «limpie» el
/// <c>= true</c> sin ver por qué está. El default de la COLUMNA en la BD sigue en <c>false</c> a
/// propósito (EF omite del INSERT un <c>false</c> con default y deja que lo ponga la BD): ver el
/// doc-comment de <c>SeguimientoProduccion.Validado</c>.
/// </para>
/// </summary>
public class SeguimientoNaceValidadoTests
{
    private const string PorQue =
        "Una fila que nadie marcó como separada nace validada: sin reserva no hay nada que aplicar. " +
        "Con `false` cada creador (traslado, arrastre de huevos, movimientos, cargas) tenía que acordarse " +
        "de marcarla, y los que no lo hacían dejaban «pendientes» que a las 24 h bloqueaban el alta de días nuevos.";

    [Fact]
    public void Produccion_SinAsignarNada_NaceValidado() =>
        Assert.True(new SeguimientoProduccion().Validado, PorQue);

    [Fact]
    public void Levante_SinAsignarNada_NaceValidado() =>
        Assert.True(new SeguimientoDiario().Validado, PorQue);

    [Fact]
    public void Engorde_SinAsignarNada_NaceValidado() =>
        Assert.True(new SeguimientoDiarioAvesEngorde().Validado, PorQue);

    /// <summary>
    /// El camino que SEPARA dice `false` de forma explícita y esa asignación se respeta: el valor
    /// inicial `true` no puede pisarla (con la doble validación encendida el registro queda pendiente).
    /// </summary>
    [Fact]
    public void QuienSepara_LoDejaPendienteDeFormaExplicita()
    {
        var separa = ValidacionSeguimientoCalculos.SeparaAlGuardar(empresaRequiereValidacion: true);

        Assert.False(new SeguimientoProduccion { Validado = !separa }.Validado);
        Assert.False(new SeguimientoDiario { Validado = !separa }.Validado);
        Assert.False(new SeguimientoDiarioAvesEngorde { Validado = !separa }.Validado);
    }

    /// <summary>
    /// Con la doble validación APAGADA la regla `Validado = !separa` da lo mismo que el valor inicial:
    /// el registro descuenta al guardar y nace validado (el comportamiento de siempre).
    /// </summary>
    [Fact]
    public void FlagApagado_LaReglaCoincideConElValorInicial()
    {
        var separa = ValidacionSeguimientoCalculos.SeparaAlGuardar(empresaRequiereValidacion: false);

        Assert.False(separa);
        Assert.Equal(new SeguimientoProduccion().Validado, !separa);
        Assert.Equal(new SeguimientoDiario().Validado, !separa);
        Assert.Equal(new SeguimientoDiarioAvesEngorde().Validado, !separa);
    }
}
