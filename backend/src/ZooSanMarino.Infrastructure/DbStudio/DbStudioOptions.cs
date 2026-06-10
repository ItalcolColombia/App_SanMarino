namespace ZooSanMarino.Infrastructure.DbStudio;

/// <summary>
/// Opciones de DB Studio (bind desde la sección "DbStudio" de appsettings).
/// <see cref="Enabled"/> es el kill-switch global del módulo.
/// </summary>
public class DbStudioOptions
{
    /// <summary>Kill-switch: si es false, todos los endpoints de DB Studio devuelven error.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Esquemas donde se permite escritura/DDL (además del rol admin).</summary>
    public string[]? WritableSchemas { get; set; }

    /// <summary>Tope de filas para SELECT/preview.</summary>
    public int SelectMaxLimit { get; set; } = 500;

    /// <summary>Tope de filas para exportaciones.</summary>
    public int MaxExportRows { get; set; } = 100_000;

    /// <summary>Timeout de sentencia (server-side `statement_timeout`) en segundos.</summary>
    public int StatementTimeoutSeconds { get; set; } = 30;

    /// <summary>Tamaño mínimo del pool del data source dedicado de DB Studio.</summary>
    public int PoolMinSize { get; set; } = 0;

    /// <summary>Tamaño máximo del pool del data source dedicado de DB Studio.</summary>
    public int PoolMaxSize { get; set; } = 10;

    /// <summary>
    /// Cadena de conexión opcional de SOLO LECTURA para el path de SELECT/preview.
    /// Si es nula, se usa la conexión principal (con transacción READ ONLY).
    /// </summary>
    public string? ReadOnlyConnectionString { get; set; }

    /// <summary>Nombre de aplicación reportado en pg_stat_activity para identificar sesiones de DB Studio.</summary>
    public string ApplicationName { get; set; } = "DbStudio";
}
