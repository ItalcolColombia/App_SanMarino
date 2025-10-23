/// file: backend/src/ZooSanMarino.Application/Interfaces/IGramajeProvider.cs
namespace ZooSanMarino.Application.Interfaces;

public interface IGramajeProvider
{
    /// <summary>
    /// Gramaje en gramos/ave/día para el galpón y semana indicados.
    /// </summary>
    Task<double?> GetGramajeGrPorAveAsync(int galponId, int semana, string? tipoAlimento = null, CancellationToken ct = default);
}
