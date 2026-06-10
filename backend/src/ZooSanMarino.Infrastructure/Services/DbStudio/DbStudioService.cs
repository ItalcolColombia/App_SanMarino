using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.DbStudio;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Servicio principal de DB Studio (orquestador). La implementación está repartida en
/// archivos <c>partial</c> por responsabilidad dentro de <c>Funciones/</c>:
/// Introspection, Query, Ddl, Data, ExportImport, Audit. Este archivo ancla guarda campos,
/// constructor y helpers compartidos, y declara la interfaz.
/// </summary>
public sealed partial class DbStudioService : IDbStudioService
{
    private readonly DbStudioRuntime _rt;
    private readonly DbStudioOptions _opts;
    private readonly ICurrentUser _current;
    private readonly IDbStudioAuthorization _authz;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<DbStudioService> _logger;

    public DbStudioService(
        DbStudioRuntime rt,
        IOptions<DbStudioOptions> opts,
        ICurrentUser current,
        IDbStudioAuthorization authz,
        IHttpContextAccessor http,
        ILogger<DbStudioService> logger)
    {
        _rt = rt;
        _opts = opts.Value;
        _current = current;
        _authz = authz;
        _http = http;
        _logger = logger;
    }

    // ===================== Helpers compartidos =====================

    private static void AddParams(NpgsqlCommand cmd, IDictionary<string, object?>? prms)
    {
        if (prms is null) return;
        foreach (var (key, value) in prms)
        {
            var name = key.StartsWith('@') ? key[1..] : key;
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
    }

    /// <summary>Lee todas las filas del reader (hasta <paramref name="max"/> si se indica).</summary>
    private static async Task<(List<Dictionary<string, object?>> Rows, List<string> Columns)> ReadAllAsync(
        NpgsqlCommand cmd, int? max, CancellationToken ct)
    {
        var rows = new List<Dictionary<string, object?>>();
        var cols = new List<string>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        for (var i = 0; i < rd.FieldCount; i++) cols.Add(rd.GetName(i));
        while (await rd.ReadAsync(ct))
        {
            if (max.HasValue && rows.Count >= max.Value) break;
            var row = new Dictionary<string, object?>(rd.FieldCount, StringComparer.Ordinal);
            for (var i = 0; i < rd.FieldCount; i++)
                row[rd.GetName(i)] = await rd.IsDBNullAsync(i, ct) ? null : rd.GetValue(i);
            rows.Add(row);
        }
        return (rows, cols);
    }

    private int ClampLimit(int requested)
    {
        var max = _opts.SelectMaxLimit <= 0 ? 500 : _opts.SelectMaxLimit;
        return Math.Max(1, Math.Min(requested <= 0 ? 100 : requested, max));
    }

    private string? GetActorEmail()
        => _http.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
           ?? _http.HttpContext?.User?.FindFirst("email")?.Value;

    private string? GetClientIp()
        => _http.HttpContext?.Connection?.RemoteIpAddress?.ToString();
}
