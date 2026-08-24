namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO que representa una combinación empresa-país
/// </summary>
public class CompanyPaisDto
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = null!;
    public string? CompanyLogoDataUrl { get; set; }
    public int PaisId { get; set; }
    public string PaisNombre { get; set; } = null!;
    public bool IsDefault { get; set; }

    /// <summary>
    /// Kill switch de F5 (descuento_inventario_movil_plan.md): con <c>true</c>, la app móvil manda
    /// ítems de inventario reales en el seguimiento diario en vez del escalar de hoy. Viaja acá (y no
    /// solo en <c>CompanyDto</c>) porque este es el DTO que de verdad llega al login de la app — la
    /// app lee este flag fail-closed: ausente o <c>false</c> ⇒ sigue mandando el escalar.
    /// </summary>
    public bool DescuentaInventarioDesdeMovil { get; set; }
}





