using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests del gate S1 (bugfix descuento cross-país). Contrato: solo Ecuador (2) y Panamá (3)
/// operan sobre el inventario modelo B; un lote Colombia (o país desconocido) NO debe disparar
/// consumo/devolución en modelo B — así se cierra el descuento cross-país silencioso que el
/// fallback catalogItemId→item_inventario_ecuador_id producía para lotes Colombia.
///
/// Los 3 servicios (SeguimientoLoteLevanteService, SeguimientoAvesEngordeEcuadorService,
/// SeguimientoAvesEngordeService) invocan RegistrarConsumoAsync/RegistrarIngresoAsync SOLO cuando
/// este gate devuelve true, con el país resuelto por lote.PaisId ?? (farm→departamento→pais).
/// </summary>
public class InventarioConsumoGateTests
{
    [Fact]
    public void Colombia_NoDescuentaModeloB()
        => Assert.False(InventarioConsumoGate.DebeDescontarModeloB(1));

    [Fact]
    public void Ecuador_DescuentaModeloB()
        => Assert.True(InventarioConsumoGate.DebeDescontarModeloB(2));

    [Fact]
    public void Panama_DescuentaModeloB()
        => Assert.True(InventarioConsumoGate.DebeDescontarModeloB(3));

    [Fact]
    public void PaisNull_NoDescuentaModeloB()
        => Assert.False(InventarioConsumoGate.DebeDescontarModeloB(null));

    [Theory]
    [InlineData(0)]     // no resuelto / defecto
    [InlineData(4)]     // otro país (p.ej. Demo/Colombia empresa 4)
    [InlineData(-1)]
    [InlineData(99)]
    public void PaisNoEcuadorNiPanama_NoDescuentaModeloB(int paisId)
        => Assert.False(InventarioConsumoGate.DebeDescontarModeloB(paisId));

    [Fact]
    public void Constantes_PaisIds_CoincidenConTablaPaises()
    {
        // paises: 1=Colombia, 2=Ecuador, 3=Panama (BD local y prod).
        Assert.Equal(2, InventarioConsumoGate.PaisEcuador);
        Assert.Equal(3, InventarioConsumoGate.PaisPanama);
    }
}
