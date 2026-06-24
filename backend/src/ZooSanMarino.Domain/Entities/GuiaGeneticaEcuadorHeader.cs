using System.Collections.Generic;

namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Encabezado de Guía Genética Ecuador (por empresa + raza + año).
/// Estado: "active" / "inactive".
/// </summary>
public class GuiaGeneticaEcuadorHeader : AuditableEntity
{
    public int Id { get; set; }

    /// <summary>País al que pertenece la guía (0 = sin país / legado). Filtra por x-active-pais.</summary>
    public int PaisId { get; set; }

    public string Raza { get; set; } = null!;
    public int AnioGuia { get; set; }

    /// <summary>Estado: "active" / "inactive".</summary>
    public string Estado { get; set; } = "active";

    public virtual ICollection<GuiaGeneticaEcuadorDetalle> Detalles { get; set; } = new List<GuiaGeneticaEcuadorDetalle>();
}

