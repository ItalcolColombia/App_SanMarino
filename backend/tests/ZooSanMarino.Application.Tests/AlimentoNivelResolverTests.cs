using Xunit;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

public class AlimentoNivelResolverTests
{
    [Theory]
    // Granja sin override (null) → hereda el default de la empresa
    [InlineData(null, true, true)]
    [InlineData(null, false, false)]
    // Granja con override → gana la granja (aunque la empresa diga lo contrario)
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    public void ManejaPorGalpon_resuelve_farm_sobre_company(bool? farmOverride, bool companyDefault, bool esperado)
    {
        Assert.Equal(esperado, AlimentoNivelResolver.ManejaPorGalpon(farmOverride, companyDefault));
    }
}
