using System.Collections.Generic;

namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Encabezado de Guía Genética Ecuador (por empresa + raza + año).
/// Estado: "active" / "inactive".
/// </summary>
public class GuiaGeneticaEcuadorHeader : AuditableEntity
{
    public int Id { get; set; }

    public string Raza { get; set; } = null!;
    public int AnioGuia { get; set; }

    /// <summary>Estado: "active" / "inactive".</summary>
    public string Estado { get; set; } = "active";

    public virtual ICollection<GuiaGeneticaEcuadorDetalle> Detalles { get; set; } = new List<GuiaGeneticaEcuadorDetalle>();
}

