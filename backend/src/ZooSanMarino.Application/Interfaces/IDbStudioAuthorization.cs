using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Reglas de autorización de DB Studio. La protección real vive acá: las policies de ASP.NET sólo
/// exigen usuario autenticado (deny-by-default en Program.cs), no distinguen admin de no-admin.
/// Lanza <see cref="UnauthorizedAccessException"/> cuando no hay permiso (el controller la mapea a 403).
/// </summary>
public interface IDbStudioAuthorization
{
    /// <summary>
    /// True solo con doble validación: el correo autorizado Y además ser admin (rol admin/administrador,
    /// superadmin o permiso `db_studio.admin`). Un admin sin ese correo devuelve false.
    /// </summary>
    Task<bool> IsAdminAsync(CancellationToken ct = default);

    /// <summary>Exige acceso completo al estudio (correo autorizado + admin); si no, 403.</summary>
    Task EnsureFullAccessAsync(CancellationToken ct = default);

    /// <summary>Exige acceso al resumen de migraciones (cualquier sesión autenticada), sin revelar objetos ni scripts.</summary>
    Task EnsureMigrationSummaryAccessAsync(CancellationToken ct = default);

    /// <summary>Exige rol admin (DDL, SQL arbitrario, concurrencia, grants).</summary>
    Task EnsureAdminAsync(CancellationToken ct = default);

    /// <summary>Exige acceso completo: la lectura de objetos ya no se abre por grant, solo a admin.</summary>
    Task EnsureCanReadAsync(string schema, string objectName, CancellationToken ct = default);

    /// <summary>Exige acceso completo: la escritura de datos ya no se abre por grant, solo a admin.</summary>
    Task EnsureCanWriteDataAsync(string schema, string objectName, CancellationToken ct = default);

    /// <summary>Exige acceso completo y devuelve null: el admin ve todos los objetos, sin filtro por grant.</summary>
    Task<HashSet<string>?> GetReadableObjectKeysAsync(CancellationToken ct = default);

    /// <summary>Resumen de acceso del usuario actual; hoy exige acceso completo y siempre reporta IsAdmin.</summary>
    Task<MyAccessDto> GetMyAccessAsync(CancellationToken ct = default);

    /// <summary>Clave canónica schema.objeto en minúsculas para comparaciones.</summary>
    static string Key(string schema, string objectName) =>
        $"{schema}.{objectName}".ToLowerInvariant();
}
