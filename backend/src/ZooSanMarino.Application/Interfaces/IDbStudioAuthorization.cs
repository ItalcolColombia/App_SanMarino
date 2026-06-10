using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Reglas de autorización de DB Studio. La protección real vive acá (no en las policies de ASP.NET,
/// que en este proyecto están neutralizadas por el AllowAllPolicyProvider).
/// Lanza <see cref="UnauthorizedAccessException"/> cuando no hay permiso (el controller la mapea a 403).
/// </summary>
public interface IDbStudioAuthorization
{
    /// <summary>True si el usuario actual es admin/administrador, superadmin o tiene `db_studio.admin`.</summary>
    Task<bool> IsAdminAsync(CancellationToken ct = default);

    /// <summary>Exige poder abrir el módulo (admin o permiso `db_studio.access`).</summary>
    Task EnsureModuleAccessAsync(CancellationToken ct = default);

    /// <summary>Exige rol admin (DDL, SQL arbitrario, concurrencia, grants).</summary>
    Task EnsureAdminAsync(CancellationToken ct = default);

    /// <summary>Exige lectura sobre el objeto (admin o grant read/write).</summary>
    Task EnsureCanReadAsync(string schema, string objectName, CancellationToken ct = default);

    /// <summary>Exige escritura de datos sobre el objeto (admin o grant write).</summary>
    Task EnsureCanWriteDataAsync(string schema, string objectName, CancellationToken ct = default);

    /// <summary>
    /// Conjunto de objetos legibles por el usuario actual. Devuelve null para admin (= todos).
    /// </summary>
    Task<HashSet<string>?> GetReadableObjectKeysAsync(CancellationToken ct = default);

    /// <summary>Resumen de acceso del usuario actual (para el frontend).</summary>
    Task<MyAccessDto> GetMyAccessAsync(CancellationToken ct = default);

    /// <summary>Clave canónica schema.objeto en minúsculas para comparaciones.</summary>
    static string Key(string schema, string objectName) =>
        $"{schema}.{objectName}".ToLowerInvariant();
}
