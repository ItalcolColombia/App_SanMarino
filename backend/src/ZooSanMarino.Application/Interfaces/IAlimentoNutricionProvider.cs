/// file: backend/src/ZooSanMarino.Application/Interfaces/IAlimentoNutricionProvider.cs
namespace ZooSanMarino.Application.Interfaces;

public interface IAlimentoNutricionProvider
{
    /// <summary>
    /// Retorna (kcal, prot) por kg del alimento indicado. Null si no se encuentra.
    /// </summary>
    Task<(double kcal, double prot)?> GetNutrientesAsync(string tipoAlimento, CancellationToken ct = default);
}
