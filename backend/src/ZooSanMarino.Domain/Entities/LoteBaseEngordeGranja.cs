// src/ZooSanMarino.Domain/Entities/LoteBaseEngordeGranja.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Asignación de un lote base de engorde a una granja. Parametriza en qué granjas es
/// visible el lote base al crear un lote de engorde: solo aparece en el selector de la
/// granja seleccionada si existe una fila que lo amarre a esa granja. Tabla:
/// lote_base_engorde_granja (M:N lote_base_engorde ↔ farms).
/// </summary>
public class LoteBaseEngordeGranja
{
    public int Id { get; set; }

    public int LoteBaseEngordeId { get; set; }

    public int FarmId { get; set; }

    /// <summary>Empresa dueña de la asignación (scoping multi-empresa).</summary>
    public int CompanyId { get; set; }

    /// <summary>Usuario que creó la asignación.</summary>
    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
