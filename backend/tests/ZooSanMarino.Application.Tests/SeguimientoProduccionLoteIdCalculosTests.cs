using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Resolución del <c>lote_id</c> efectivo para el seguimiento diario de producción
/// (fix "El lote postura producción no tiene LoteId asociado", plan
/// <c>fase_de_desarrollo/fix_seguimiento_produccion_lote_id_plan.md</c>).
/// Regla: el <c>LoteId</c> propio del LPP manda si es válido (&gt; 0); si no, se hereda el del
/// levante de origen (filas creadas antes del fix del cierre de levante); si ninguno es válido,
/// <c>null</c> → el service lanza el error claro.
/// </summary>
public class SeguimientoProduccionLoteIdCalculosTests
{
    [Theory]
    // El propio manda: no se pisa con el del levante.
    [InlineData(124, null, 124)]
    [InlineData(124, 99, 124)]
    // Herencia del levante cuando el propio es null/0/negativo (inválido).
    [InlineData(null, 124, 124)]
    [InlineData(0, 124, 124)]
    [InlineData(-1, 124, 124)]
    // Irresoluble: ninguno de los dos tiene un lote válido → null (error claro en el service).
    [InlineData(null, null, null)]
    [InlineData(null, 0, null)]
    [InlineData(0, -5, null)]
    public void ResolverLoteIdEfectivo_PrimerValorValido_ONullSiNoHay(
        int? loteIdProduccion, int? loteIdLevante, int? esperado)
    {
        Assert.Equal(esperado,
            SeguimientoProduccionLoteIdCalculos.ResolverLoteIdEfectivo(loteIdProduccion, loteIdLevante));
    }
}
