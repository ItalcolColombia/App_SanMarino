namespace ZooSanMarino.Application.Interfaces;

public interface ICurrentUser
{
    int CompanyId { get; }
    int UserId { get; }
    int? PaisId { get; }
    string? ActiveCompanyName { get; }
    Guid? UserGuid { get; }
    IReadOnlyList<string> Permissions { get; }

    /// <summary>
    /// ¿Administra TODAS las empresas? Super admin (claim <c>is_super_admin</c>) o rol de
    /// administrador de la aplicación con nombre EXACTO (<c>Admin</c>/<c>Administrador</c>). Es la
    /// misma regla que la policy <c>AdminEmpresas</c>
    /// (<c>AdministracionEmpresasAutorizacionCalculos.PuedeAdministrarEmpresas</c>). Fail-closed:
    /// sin sesión ⇒ <c>false</c>.
    /// </summary>
    bool EsAdminEmpresas { get; }
}
