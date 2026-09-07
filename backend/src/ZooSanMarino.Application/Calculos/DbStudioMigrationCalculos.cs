using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Calculos;

/// <summary>Reglas puras para el resumen seguro de migraciones de DB Studio.</summary>
public static class DbStudioMigrationCalculos
{
    public const string EmailConAccesoCompleto = "moiesbbuga@gmail.com";

    /// <summary>
    /// Acceso completo a DB Studio = <b>doble validación</b>: hay que ser el correo autorizado
    /// <see cref="EmailConAccesoCompleto"/> <b>y además</b> ser admin (rol <c>admin</c>/<c>administrador</c>,
    /// superadmin o permiso <c>db_studio.admin</c>). Si falta cualquiera de los dos, la sesión solo ve
    /// el resumen de migraciones. El correo se compara ordinal, sin depender del casing del claim.
    /// </summary>
    public static bool TieneAccesoCompleto(
        IEnumerable<string> roles,
        string? email,
        bool esSuperAdmin = false,
        bool tienePermisoDbStudioAdmin = false)
        => EsCorreoAutorizado(email)
           && (EsAdminPorRol(roles) || esSuperAdmin || tienePermisoDbStudioAdmin);

    /// <summary>El claim de correo es exactamente el autorizado (trim + comparación ordinal case-insensitive).</summary>
    public static bool EsCorreoAutorizado(string? email)
        => string.Equals(email?.Trim(), EmailConAccesoCompleto, StringComparison.OrdinalIgnoreCase);

    /// <summary>Alguno de los roles es <c>admin</c> o <c>administrador</c> (trim + case-insensitive).</summary>
    public static bool EsAdminPorRol(IEnumerable<string> roles)
        => roles.Any(r => !string.IsNullOrWhiteSpace(r) &&
            (r.Trim().Equals("admin", StringComparison.OrdinalIgnoreCase) ||
             r.Trim().Equals("administrador", StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// Lista de migraciones conocidas por EF y si ya se ejecutaron. Sin fechas ni metadatos de base:
    /// solo el nombre y el estado (<c>aplicada</c> / <c>pendiente</c>).
    /// </summary>
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
                Status = aplicadas.Contains(id) ? "aplicada" : "pendiente"
            })
            .ToList();
    }
}
