/// file: backend/src/ZooSanMarino.Infrastructure/Providers/EfAlimentoNutricionProvider.cs
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Providers;

public class EfAlimentoNutricionProvider : IAlimentoNutricionProvider
{
    private readonly ZooSanMarinoContext _ctx;
    public EfAlimentoNutricionProvider(ZooSanMarinoContext ctx) => _ctx = ctx;

    public async Task<(double kcal, double prot)?> GetNutrientesAsync(string tipoAlimento, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tipoAlimento)) return null;

        // Coincidencia por Código o Nombre (case-insensitive), y que esté Activo
        var item = await _ctx.Set<CatalogItem>()
            .AsNoTracking()
            .Where(i => i.Activo &&
                   (EF.Functions.ILike(i.Codigo, tipoAlimento) || EF.Functions.ILike(i.Nombre, tipoAlimento))) // ILike = Postgres; si usas SQL Server, cambia por ToLower()
            .OrderByDescending(i => i.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        if (item is null) return null;

        if (TryGetDouble(item.Metadata, out var kcal, "kcal_alh", "kcal", "kcal_kg") &&
            TryGetDouble(item.Metadata, out var prot, "prot_alh", "prot", "prot_kg"))
        {
            return (kcal, prot);
        }

        // Si no hay ambos valores, intentamos al menos kcal o prot; si falta uno, retornamos null
        return null;
    }

    private static bool TryGetDouble(JsonDocument? doc, out double value, params string[] keys)
    {
        value = default;
        if (doc is null) return false;

        foreach (var key in keys)
        {
            if (!doc.RootElement.TryGetProperty(key, out var el)) continue;

            switch (el.ValueKind)
            {
                case JsonValueKind.Number:
                    if (el.TryGetDouble(out var num))
                    {
                        value = num;
                        return true;
                    }
                    break;

                case JsonValueKind.String:
                    var s = el.GetString();
                    if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                    {
                        value = d;
                        return true;
                    }
                    break;
            }
        }
        return false;
    }
}
