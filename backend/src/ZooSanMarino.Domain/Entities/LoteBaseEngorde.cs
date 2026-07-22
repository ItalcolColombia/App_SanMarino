// src/ZooSanMarino.Domain/Entities/LoteBaseEngorde.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Lote base global de pollo engorde. Catálogo a nivel empresa que agrupa varios
/// lote_ave_engorde (distintos galpones/granjas) bajo un mismo nombre (ej. "95").
/// La asignación en el lote es OPCIONAL. Tabla: lote_base_engorde.
/// </summary>
public class LoteBaseEngorde : AuditableEntity
{
    public int Id { get; set; }

    /// <summary>Nombre corto del lote base (ej. "95"). Único por empresa entre vivos.</summary>
    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    /// <summary>Código ERP del lote base (opcional).</summary>
    public string? CodigoErp { get; set; }

    /// <summary>Línea genética del lote base (opcional, ej. "ROSS 308 AP").</summary>
    public string? LineaGenetica { get; set; }

    /// <summary>
    /// Fecha de activación (columna date). Vigencia por AÑO: el lote base aparece en el
    /// selector de crear-lote solo durante el año de esta fecha (NULL = siempre vigente).
    /// </summary>
    public DateTime? FechaActivacion { get; set; }

    /// <summary>Desactivación MANUAL: inactivo deja de aparecer en el selector de crear-lote.</summary>
    public bool Activo { get; set; } = true;
}
