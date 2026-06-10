using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ZooSanMarino.Infrastructure.DbStudio;

/// <summary>
/// Data source dedicado y con pool propio para DB Studio (singleton). Evita pedir prestada
/// la conexión del DbContext de EF. Expone una conexión principal (escritura/DDL) y una de
/// solo lectura (opcionalmente apuntando a otra cadena). Cada conexión abierta fija
/// <c>statement_timeout</c> y, en lectura, una transacción READ ONLY.
/// </summary>
public sealed class DbStudioRuntime : IAsyncDisposable
{
    private readonly NpgsqlDataSource _primary;
    private readonly NpgsqlDataSource _readOnly;
    private readonly bool _readOnlyIsSeparate;
    private readonly int _statementTimeoutMs;

    public DbStudioRuntime(IConfiguration cfg, IOptions<DbStudioOptions> options)
    {
        var opt = options.Value;
        _statementTimeoutMs = Math.Max(1, opt.StatementTimeoutSeconds) * 1000;

        var baseConn = ConnectionStringResolver.Resolve(cfg);
        _primary = Build(baseConn, opt, opt.ApplicationName);

        if (!string.IsNullOrWhiteSpace(opt.ReadOnlyConnectionString))
        {
            _readOnly = Build(opt.ReadOnlyConnectionString!, opt, opt.ApplicationName + "-ro");
            _readOnlyIsSeparate = true;
        }
        else
        {
            _readOnly = _primary;
            _readOnlyIsSeparate = false;
        }
    }

    private static NpgsqlDataSource Build(string connStr, DbStudioOptions opt, string appName)
    {
        var csb = new NpgsqlConnectionStringBuilder(connStr)
        {
            ApplicationName = appName,
            MinPoolSize = Math.Max(0, opt.PoolMinSize),
            MaxPoolSize = Math.Max(1, opt.PoolMaxSize),
            Pooling = true
        };
        return new NpgsqlDataSourceBuilder(csb.ConnectionString).Build();
    }

    /// <summary>Abre una conexión de escritura/DDL con statement_timeout aplicado.</summary>
    public async Task<NpgsqlConnection> OpenWriteAsync(CancellationToken ct)
    {
        var conn = await _primary.OpenConnectionAsync(ct);
        await SetTimeoutAsync(conn, ct);
        return conn;
    }

    /// <summary>Abre una conexión de solo lectura: statement_timeout + sesión READ ONLY por defecto.</summary>
    public async Task<NpgsqlConnection> OpenReadAsync(CancellationToken ct)
    {
        var conn = await _readOnly.OpenConnectionAsync(ct);
        await SetTimeoutAsync(conn, ct);
        // Garantiza que cualquier intento de escritura por este canal falle.
        await using (var cmd = new NpgsqlCommand("SET default_transaction_read_only = on;", conn))
            await cmd.ExecuteNonQueryAsync(ct);
        return conn;
    }

    private async Task SetTimeoutAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand($"SET statement_timeout = {_statementTimeoutMs};", conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_readOnlyIsSeparate) await _readOnly.DisposeAsync();
        await _primary.DisposeAsync();
    }
}
