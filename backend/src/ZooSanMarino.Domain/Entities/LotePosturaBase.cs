// src/ZooSanMarino.Domain/Entities/LotePosturaBase.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Registro "base" para lotes de postura (creación rápida).
/// Se usa como nodo principal para asociar lotes de levante/producción/seguimientos.
/// </summary>
public class LotePosturaBase : AuditableEntity
{
    public int LotePosturaBaseId { get; set; }

    public string LoteNombre { get; set; } = null!;

    public string? CodigoErp { get; set; }

    public int CantidadHembras { get; set; }
    public int CantidadMachos { get; set; }

    /// <summary>
    /// Cantidad mixtas (aves mixtas) asociadas al lote base.
    /// </summary>
    public int CantidadMixtas { get; set; }

    public int? PaisId { get; set; }
}

