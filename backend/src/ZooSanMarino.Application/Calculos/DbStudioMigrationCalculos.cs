using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Calculos;

/// <summary>Reglas puras para el resumen seguro de migraciones de DB Studio.</summary>
public static class DbStudioMigrationCalculos
{
    public const string EmailConAccesoCompleto = "moiesbbuga@gmail.com";

    public static bool TieneAccesoCompleto(IEnumerable<string> roles, string? email)
        => roles.Any(role => string.Equals(role?.Trim(), "admin", StringComparison.OrdinalIgnoreCase))
           || string.Equals(email?.Trim(), EmailConAccesoCompleto, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<DbStudioMigrationSummaryItemDto> Resumir(
        IEnumerable<string> migracionesConocidas,
        IEnumerable<string> migracionesAplicadas)
    {
        var aplicadas = migracionesAplicadas.ToHashSet(StringComparer.Ordinal);
        return migracionesConocidas
            .Distinct(StringComparer.Ordinal)
            .Select(id => new DbStudioMigrationSummaryItemDto
            {
                MigrationId = id,
                // __EFMigrationsHistory estándar no registra la fecha de aplicación.
                AppliedAtUtc = null,
                Status = aplicadas.Contains(id) ? "aplicada" : "pendiente"
            })
            .ToList();
    }
}
