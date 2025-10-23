/// file: backend/src/ZooSanMarino.Infrastructure/Providers/NullGramajeProvider.cs
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Providers;

public class NullGramajeProvider : IGramajeProvider
{
    public Task<double?> GetGramajeGrPorAveAsync(int galponId, int semana, string? tipoAlimento = null, CancellationToken ct = default)
        => Task.FromResult<double?>(null);
}
