using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.InventarioCatalogoScopeCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Verifica el acotamiento del catálogo de ítems por empresa (fix multi-empresa/país):
/// la empresa efectiva prioriza el header por nombre, y con sesión sin empresa se falla cerrado
/// (vacío) en vez de devolver todo el catálogo (la fuga que mostraba Ecuador en Panamá).
/// </summary>
public class InventarioCatalogoScopeCalculosTests
{
    // ── EmpresaEfectiva: nombre del header manda; cae al token ────────────────
    [Fact]
    public void EmpresaEfectiva_PrioridadNombreSobreToken()
    {
        // Panamá por header (5) aunque el token diga Ecuador (3)
        Assert.Equal(5, EmpresaEfectiva(companyIdPorNombre: 5, companyIdToken: 3));
    }

    [Fact]
    public void EmpresaEfectiva_CaeAlTokenSiNoHayNombre()
    {
        Assert.Equal(3, EmpresaEfectiva(companyIdPorNombre: null, companyIdToken: 3));
        Assert.Equal(3, EmpresaEfectiva(companyIdPorNombre: 0, companyIdToken: 3));
    }

    [Fact]
    public void EmpresaEfectiva_NullSiNingunaValida()
    {
        Assert.Null(EmpresaEfectiva(companyIdPorNombre: null, companyIdToken: 0));
        Assert.Null(EmpresaEfectiva(companyIdPorNombre: 0, companyIdToken: 0));
    }

    // ── Decidir: NoSession / FailClosed / FilterByCompany ─────────────────────
    [Fact]
    public void Decidir_SinSesion_NoAplicaFiltro()
    {
        Assert.Equal(ScopeDecision.NoSession, Decidir(currentUserPresent: false, empresaEfectiva: null));
        // aunque venga una empresa, sin sesión no hay scoping (uso interno)
        Assert.Equal(ScopeDecision.NoSession, Decidir(currentUserPresent: false, empresaEfectiva: 5));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Decidir_ConSesionSinEmpresa_FailClosed(int? empresa)
    {
        // Esta rama es el corazón del fix: en vez de devolver TODO (Ecuador), devuelve vacío.
        Assert.Equal(ScopeDecision.FailClosed, Decidir(currentUserPresent: true, empresaEfectiva: empresa));
    }

    [Theory]
    [InlineData(1)] // Colombia
    [InlineData(3)] // Ecuador
    [InlineData(5)] // Panamá
    public void Decidir_ConSesionYEmpresa_FiltraPorEmpresa(int empresa)
    {
        Assert.Equal(ScopeDecision.FilterByCompany, Decidir(currentUserPresent: true, empresaEfectiva: empresa));
    }

    // ── Escenario del bug reportado (Panamá) end-to-end de la decisión ────────
    [Fact]
    public void Escenario_Panama_ResuelvePanamaYNoEcuador()
    {
        // header = ItalcolPanama (5) resuelto por nombre; token del usuario = Ecuador (3)
        var empresa = EmpresaEfectiva(companyIdPorNombre: 5, companyIdToken: 3);
        Assert.Equal(5, empresa);
        Assert.Equal(ScopeDecision.FilterByCompany, Decidir(currentUserPresent: true, empresaEfectiva: empresa));
    }

    [Fact]
    public void Escenario_Panama_EmpresaNoResuelta_NoFiltraCatalogoEcuador()
    {
        // Si ni header ni token resuelven empresa, antes caía a "sin filtro" (todo=Ecuador). Ahora: vacío.
        var empresa = EmpresaEfectiva(companyIdPorNombre: null, companyIdToken: 0);
        Assert.Null(empresa);
        Assert.Equal(ScopeDecision.FailClosed, Decidir(currentUserPresent: true, empresaEfectiva: empresa));
    }
}
