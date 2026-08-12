namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Relación empresa-permiso: qué permisos del catálogo global están habilitados para cada empresa.
/// <para>
/// Gemela de <see cref="CompanyMenu"/>, pero a diferencia de aquélla ESTA SÍ MANDA: filtra lo que se
/// puede asignar a un rol y se intersecta con los permisos efectivos del usuario en el login. Un
/// permiso que la empresa no tiene habilitado no se ofrece y no viaja en la sesión.
/// </para>
/// <para>
/// La señal vive en datos, no en código: nada de <c>if (empresa == "X")</c>. Los seeds la localizan
/// por <c>companies.name</c> / <c>permissions.key</c>, nunca por id (difieren local↔prod).
/// </para>
/// </summary>
public class CompanyPermission
{
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;

    /// <summary>
    /// Si el permiso está habilitado para esta empresa. Se conserva la fila con <c>false</c> en vez
    /// de borrarla cuando el admin la desmarca, para dejar rastro de que se decidió apagarla.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
