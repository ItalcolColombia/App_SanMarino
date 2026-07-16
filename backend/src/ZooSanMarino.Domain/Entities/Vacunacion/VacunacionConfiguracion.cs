// src/ZooSanMarino.Domain/Entities/Vacunacion/VacunacionConfiguracion.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Configuración de cumplimiento de vacunación por empresa/país: umbral de días de desviación a
/// partir del cual un registro se marca <c>Incumplido</c> (rojo) en reportes y en la tabla de
/// cronograma. Una fila por (CompanyId, PaisId); si no existe, el default aplicado es 14 días.
/// </summary>
public class VacunacionConfiguracion
{
    public int CompanyId { get; set; }
    public int PaisId { get; set; }

    public int DiasUmbralIncumplido { get; set; } = 14;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
