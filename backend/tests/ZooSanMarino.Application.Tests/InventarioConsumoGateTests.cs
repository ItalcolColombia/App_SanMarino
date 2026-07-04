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
        Assert.Equal(1, InventarioConsumoGate.PaisColombia);
        Assert.Equal(2, InventarioConsumoGate.PaisEcuador);
        Assert.Equal(3, InventarioConsumoGate.PaisPanama);
    }

    // Fase 3 (paso 2): despacho por país al modelo de inventario correcto.
    // Colombia migró de modelo A → modelo B a NIVEL GRANJA (unificación); EC/PA sin cambios.
    [Theory]
    [InlineData(1, ModeloInventarioConsumo.ModeloBNivelGranja)]  // Colombia → modelo B nivel granja (Fase 3)
    [InlineData(2, ModeloInventarioConsumo.ModeloB)]             // Ecuador  → modelo B con galpón (sin cambios)
    [InlineData(3, ModeloInventarioConsumo.ModeloB)]             // Panamá   → modelo B con galpón (sin cambios)
    [InlineData(4, ModeloInventarioConsumo.Ninguno)]             // otro país → ninguno
    [InlineData(0, ModeloInventarioConsumo.Ninguno)]
    public void ResolverModelo_DespachaPorPais(int paisId, ModeloInventarioConsumo esperado)
        => Assert.Equal(esperado, InventarioConsumoGate.ResolverModelo(paisId));

    [Fact]
    public void ResolverModelo_PaisNull_Ninguno()
        => Assert.Equal(ModeloInventarioConsumo.Ninguno, InventarioConsumoGate.ResolverModelo(null));

    [Fact]
    public void ResolverModelo_Colombia_YaNoUsaModeloA()
    {
        // Fase 3: Colombia dejó de consumir del modelo A. El path A queda sin uso.
        Assert.NotEqual(ModeloInventarioConsumo.ModeloA, InventarioConsumoGate.ResolverModelo(1));
        Assert.Equal(ModeloInventarioConsumo.ModeloBNivelGranja, InventarioConsumoGate.ResolverModelo(1));
    }

    [Fact]
    public void ResolverModelo_Coherente_ConDebeDescontarModeloB()
    {
        // El dispatch al modelo B "con galpón" (EC/PA) no puede contradecir el gate booleano.
        // Colombia (ModeloBNivelGranja) NO es ModeloB literal → DebeDescontarModeloB(1) sigue false.
        foreach (var pais in new int?[] { 1, 2, 3, 4, null })
        {
            var esModeloBConGalpon = InventarioConsumoGate.ResolverModelo(pais) == ModeloInventarioConsumo.ModeloB;
            Assert.Equal(InventarioConsumoGate.DebeDescontarModeloB(pais), esModeloBConGalpon);
        }
        // Colombia unifica en B pero por nivel granja, no por el gate booleano de EC/PA.
        Assert.False(InventarioConsumoGate.DebeDescontarModeloB(1));
        Assert.Equal(ModeloInventarioConsumo.ModeloBNivelGranja, InventarioConsumoGate.ResolverModelo(1));
    }
}
