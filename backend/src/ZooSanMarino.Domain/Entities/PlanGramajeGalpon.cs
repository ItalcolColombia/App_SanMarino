/// file: backend/src/ZooSanMarino.Domain/Entities/PlanGramajeGalpon.cs
namespace ZooSanMarino.Domain.Entities;

public class PlanGramajeGalpon
{
    public int Id { get; set; }
    public String GalponId { get; set; }
    public int SemanaDesde { get; set; }   // inclusive
    public int SemanaHasta { get; set; }   // inclusive
    public string? TipoAlimento { get; set; } // opcional (null = aplica a cualquier tipo)
    public double GramajeGrPorAve { get; set; } // gramos por ave por día
    public bool Vigente { get; set; } = true;
    public string? Observaciones { get; set; }
}
