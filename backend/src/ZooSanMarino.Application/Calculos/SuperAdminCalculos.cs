// src/ZooSanMarino.Application/Calculos/SuperAdminCalculos.cs
// Regla PURA de quién es Super Admin. Sin EF, sin estado, sin I/O.
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Quién es <b>Super Admin</b> — el que atraviesa el aislamiento multiempresa (puede operar sobre
/// empresas a las que no pertenece), ve el catálogo global y administra roles, alcances y DB Studio.
///
/// <para>
/// <b>Por qué existe esta clase.</b> Hasta ago-2026 la regla estaba escrita a mano en <b>14 sitios</b>
/// de autorización, cada uno comparando un correo hardcodeado (<c>ActiveCompanyMiddleware</c> ×2,
/// <c>AuthController</c>, <c>AuthService</c>, <c>CompanyService</c>, <c>DbStudioAuthorization</c>,
/// <c>FarmService</c>, <c>GalponService</c>, <c>NucleoService</c>, <c>LotePosturaLevanteService</c>,
/// <c>LotePosturaProduccionService</c>, <c>RoleCompositeService</c>, <c>UserFarmScopeService</c>,
/// <c>UserPermissionService</c>) y encima con <b>cuatro</b> formas distintas de comparar. Conceder o
/// revocar el privilegio más grande del sistema exigía <b>editar código y desplegar</b>, y no había
/// forma de auditarlo desde la base. Es la regla del repo —<i>una sola fórmula por número</i>—
/// aplicada a la autorización.
/// </para>
///
/// <para>
/// <b>La señal es un dato</b>: <c>users.is_super_admin</c>, tipada y con default neutro, igual que
/// <c>roles.is_company_admin</c>. Va en el USUARIO y no en el rol a propósito: el rol <c>Admin</c> lo
/// tiene más de una persona, así que ponerla ahí <b>ampliaría</b> el privilegio en vez de moverlo.
/// </para>
///
/// <para>
/// ⛔ <b>Nunca se infiere.</b> Ni de un correo, ni de un nombre de rol, ni de un nombre de empresa. Si
/// el dato no está, la respuesta es <c>false</c>.
/// </para>
/// </summary>
public static class SuperAdminCalculos
{
    /// <summary>
    /// ¿Es Super Admin? <b>Fail-closed</b>: <c>null</c> —usuario inexistente, sesión sin Guid, fila
    /// no encontrada— responde <c>false</c>. Ante la duda, no se concede.
    /// </summary>
    /// <param name="marcaDelUsuario">
    /// Valor de <c>users.is_super_admin</c> del usuario en cuestión, o <c>null</c> si no se pudo leer.
    /// </param>
    public static bool EsSuperAdmin(bool? marcaDelUsuario) => marcaDelUsuario == true;
}
