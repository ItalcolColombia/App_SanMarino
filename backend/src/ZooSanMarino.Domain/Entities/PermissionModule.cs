namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Módulo de permisos: agrupa keys del catálogo global (<see cref="Permission"/>) por área funcional
/// — Postura, Pollo Engorde, Inventario, Vacunación… — para que una empresa se configure prendiendo
/// módulos en vez de 50 permisos sueltos.
/// <para>
/// La relación con los permisos es <b>M:N</b> (<see cref="PermissionModulePermission"/>): un permiso
/// compartido como <c>lote.corregir_aves</c> pertenece a Postura Y a Pollo Engorde, y la empresa lo
/// recibe si tiene cualquiera de los dos.
/// </para>
/// <para>
/// Los módulos NO reemplazan a <see cref="CompanyPermission"/>: son la forma de ESCRIBIRLA. El login y
/// los gates siguen leyendo <c>company_permissions</c>. Reglas en
/// <c>Application/Calculos/PermisoModuloCalculos</c>.
/// </para>
/// </summary>
public class PermissionModule
{
    public int Id { get; set; }

    /// <summary>Identificador estable (<c>postura</c>, <c>pollo_engorde</c>…). Los seeds localizan por acá, nunca por id.</summary>
    public string Key { get; set; } = null!;

    /// <summary>Nombre visible («Pollo Engorde»).</summary>
    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    /// <summary>Orden de presentación en las pantallas.</summary>
    public int Orden { get; set; }

    public ICollection<PermissionModulePermission> Permisos { get; set; } = new List<PermissionModulePermission>();
    public ICollection<CompanyPermissionModule> Empresas { get; set; } = new List<CompanyPermissionModule>();
}
