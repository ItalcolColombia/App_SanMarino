using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.RoleAdminCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Verifica la lógica pura del flag "Es Administrador de Empresa/País" (roles.is_company_admin):
///  - solo un Super Admin puede activar/cambiar el flag,
///  - la visibilidad global de granjas = Admin de Empresa OR Super Admin.
/// </summary>
public class RoleAdminCalculosTests
{
    // ── Creación: solo Super Admin puede activar el flag ─────────────────────
    [Theory]
    [InlineData(true,  true,  true)]   // super admin lo solicita → true
    [InlineData(true,  false, false)]  // super admin no lo solicita → false
    [InlineData(false, true,  false)]  // NO super admin lo solicita → se ignora → false
    [InlineData(false, false, false)]  // NO super admin no lo solicita → false
    public void ResolverIsCompanyAdminEnCreacion_SoloSuperAdminPuedeActivar(bool esSuperAdmin, bool solicitado, bool esperado)
    {
        Assert.Equal(esperado, ResolverIsCompanyAdminEnCreacion(esSuperAdmin, solicitado));
    }

    // ── Edición: null = no tocar; solo Super Admin con valor explícito cambia ─
    [Theory]
    // super admin envía valor explícito → gana el solicitado
    [InlineData(true,  true,  false, true)]
    [InlineData(true,  false, true,  false)]
    // super admin sin valor (null) → conserva el actual
    [InlineData(true,  null,  true,  true)]
    [InlineData(true,  null,  false, false)]
    // NO super admin → nunca cambia el actual, aunque envíe valor
    [InlineData(false, true,  false, false)]
    [InlineData(false, false, true,  true)]
    [InlineData(false, null,  true,  true)]
    public void ResolverIsCompanyAdminEnEdicion_SoloSuperAdminConValorCambia(bool esSuperAdmin, bool? solicitado, bool actual, bool esperado)
    {
        Assert.Equal(esperado, ResolverIsCompanyAdminEnEdicion(esSuperAdmin, solicitado, actual));
    }

    // ── Visibilidad global de granjas = Admin de Empresa OR Super Admin ──────
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true,  false, true)]   // admin de empresa
    [InlineData(false, true,  true)]   // super admin
    [InlineData(true,  true,  true)]
    public void PuedeVerTodasLasGranjas_AdminEmpresaOSuperAdmin(bool esAdminEmpresa, bool esSuperAdmin, bool esperado)
    {
        Assert.Equal(esperado, PuedeVerTodasLasGranjas(esAdminEmpresa, esSuperAdmin));
    }
}
