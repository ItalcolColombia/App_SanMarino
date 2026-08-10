using Xunit;
using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.SeguimientoProduccionScopeCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato del acotamiento por empresa del seguimiento diario de producción.
///
/// Fija las dos propiedades que hacen que el fix sea un fix y no una intención: que la ausencia de
/// identidad NUNCA degrade a "todas las empresas", y que el <c>0</c> —el valor que dejaba
/// <c>_current?.CompanyId ?? 0</c>, y que quedó grabado en filas reales— no funcione como una
/// empresa alcanzable.
/// </summary>
public class SeguimientoProduccionScopeCalculosTests
{
    // ── EmpresaEfectiva ──────────────────────────────────────────────────────────────

    [Fact]
    public void SinSesion_NoHayEmpresaEfectiva()
    {
        Assert.Null(EmpresaEfectiva(null));
    }

    [Fact]
    public void CompanyIdCero_CuentaComoAusencia_NoComoEmpresaCero()
    {
        // Es el valor que deja `_current?.CompanyId ?? 0` sin identidad, y el que quedó grabado en
        // filas reales de `seguimiento_diario_produccion`. Si valiera como empresa, esas filas
        // serían alcanzables desde cualquier sesión sin empresa.
        Assert.Null(EmpresaEfectiva(0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void CompanyIdNegativo_TampocoEsEmpresa(int companyId)
    {
        Assert.Null(EmpresaEfectiva(companyId));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(int.MaxValue)]
    public void CompanyIdValido_SeConserva(int companyId)
    {
        Assert.Equal(companyId, EmpresaEfectiva(companyId));
    }

    // ── Decidir ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-3)]
    public void SinEmpresaResoluble_FailClosed(int? empresa)
    {
        // La propiedad que importa: la ausencia de identidad NO habilita un modo "sin filtro".
        // Este service no tiene llamadores internos, así que no existe la excepción de "uso interno"
        // que sí tiene el scoping del catálogo de inventario.
        Assert.Equal(ScopeDecision.FailClosed, Decidir(empresa));
    }

    [Fact]
    public void ConEmpresaValida_FiltraPorEsaEmpresa()
    {
        Assert.Equal(ScopeDecision.FilterByCompany, Decidir(7));
    }

    // ── FilaAlcanzable ───────────────────────────────────────────────────────────────

    [Fact]
    public void FilaDeLaMismaEmpresa_EsAlcanzable()
    {
        Assert.True(FilaAlcanzable(companyIdDelLote: 3, empresaEfectiva: 3));
    }

    [Fact]
    public void FilaDeOtraEmpresa_NoEsAlcanzable()
    {
        // El caso que se podía explotar: `DELETE /api/SeguimientoProduccion/{id}` con el id de otra
        // empresa resolvía la fila por `FindAsync` y la borraba.
        Assert.False(FilaAlcanzable(companyIdDelLote: 4, empresaEfectiva: 1));
    }

    [Fact]
    public void SinSesion_NingunaFilaEsAlcanzable()
    {
        Assert.False(FilaAlcanzable(companyIdDelLote: 1, empresaEfectiva: null));
        Assert.False(FilaAlcanzable(companyIdDelLote: 1, empresaEfectiva: 0));
    }

    [Fact]
    public void LoteSinEmpresa_NoEsAlcanzableNiPorUnaSesionSinEmpresa()
    {
        // Sin esta cláusula, `null == null` haría alcanzable la fila huérfana justo para la sesión
        // que no pudo resolver empresa: dos ausencias no forman una coincidencia.
        Assert.False(FilaAlcanzable(companyIdDelLote: null, empresaEfectiva: null));
        Assert.False(FilaAlcanzable(companyIdDelLote: 0, empresaEfectiva: 0));
    }

    [Fact]
    public void EmpresaCeroDeLaFila_NoQuedaExpuestaAUnaSesionValida()
    {
        // La fila con `company_id = 0` que existe en la BD no debe volverse alcanzable por accidente.
        Assert.False(FilaAlcanzable(companyIdDelLote: 0, empresaEfectiva: 1));
    }
}
