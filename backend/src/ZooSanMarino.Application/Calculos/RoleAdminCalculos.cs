namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Lógica PURA del flag "Es Administrador de Empresa/País" (<c>roles.is_company_admin</c>) y de la
/// visibilidad global de granjas al asignar usuarios. Extraída para poder verificarla con xUnit sin EF/HTTP.
///
/// Reglas de negocio:
///  - Solo un Super Admin puede activar/desactivar el flag <c>is_company_admin</c>.
///  - Un usuario ve TODAS las granjas activas de su empresa (al asignar granjas) si su rol es
///    Administrador de Empresa para esa empresa, o si es Super Admin.
/// </summary>
public static class RoleAdminCalculos
{
    /// <summary>
    /// Valor de <c>is_company_admin</c> al CREAR un rol: solo se activa si quien crea es Super Admin
    /// y lo solicitó explícitamente; en cualquier otro caso queda en false.
    /// </summary>
    public static bool ResolverIsCompanyAdminEnCreacion(bool esSuperAdmin, bool solicitado) =>
        esSuperAdmin && solicitado;

    /// <summary>
    /// Valor de <c>is_company_admin</c> al EDITAR un rol: solo un Super Admin que envía un valor
    /// explícito (no null) puede cambiarlo; de lo contrario se conserva el valor actual.
    /// </summary>
    public static bool ResolverIsCompanyAdminEnEdicion(bool esSuperAdmin, bool? solicitado, bool actual) =>
        (esSuperAdmin && solicitado.HasValue) ? solicitado.Value : actual;

    /// <summary>
    /// ¿El usuario actual puede ver TODAS las granjas de la empresa (visibilidad global)?
    /// True si es Administrador de Empresa (flag) o Super Admin.
    /// </summary>
    public static bool PuedeVerTodasLasGranjas(bool esAdminEmpresa, bool esSuperAdmin) =>
        esAdminEmpresa || esSuperAdmin;
}
