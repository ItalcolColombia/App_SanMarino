/// file: backend/src/ZooSanMarino.Infrastructure/Providers/NullAlimentoNutricionProvider.cs
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Providers;

public class NullAlimentoNutricionProvider : IAlimentoNutricionProvider
{
    public Task<(double kcal, double prot)?> GetNutrientesAsync(string tipoAlimento, CancellationToken ct = default)
        => Task.FromResult<(double kcal, double prot)?>(null);
}
