using Xunit;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Sentinel de borrado (0) para parámetros opcionales de empresa (`HuevoPrimeraPosturaHastaSemana`,
/// `HuevosLevanteDesdeSemana`): sin él, vaciar el campo en Configuración → Empresas no distinguía
/// «no lo mandé» de «quiero borrarlo», porque las dos cosas llegaban como `null`.
/// </summary>
public class ParametroEmpresaOpcionalCalculosTests
{
    [Fact]
    public void Null_conserva_el_valor_actual_sin_tocarlo()
    {
        Assert.Equal(22, ParametroEmpresaOpcionalCalculos.ResolverEnteroOpcional(null, 22));
        Assert.Null(ParametroEmpresaOpcionalCalculos.ResolverEnteroOpcional(null, null));
    }

    [Fact]
    public void Cero_es_el_sentinel_de_borrado_explicito_vuelve_a_null()
    {
        Assert.Null(ParametroEmpresaOpcionalCalculos.ResolverEnteroOpcional(0, 22));
    }

    [Fact]
    public void Cero_sobre_un_valor_ya_null_sigue_siendo_null_sin_error()
    {
        Assert.Null(ParametroEmpresaOpcionalCalculos.ResolverEnteroOpcional(0, null));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(18)]
    [InlineData(140)]
    public void Un_entero_positivo_reemplaza_el_valor_actual(int nuevo)
    {
        Assert.Equal(nuevo, ParametroEmpresaOpcionalCalculos.ResolverEnteroOpcional(nuevo, 5));
    }
}
