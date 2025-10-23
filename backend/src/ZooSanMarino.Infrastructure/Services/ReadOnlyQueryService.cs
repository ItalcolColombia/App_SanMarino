// src/ZooSanMarino.Infrastructure/Services/ReadOnlyQueryService.cs
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
using ZooSanMarino.Application.DTOs.DbStudio;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.DbStudio;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class ReadOnlyQueryService : IReadOnlyQueryService
{
    private readonly string _connStr;
    private readonly IOptionsMonitor<DbStudioOptions> _opts;

    // Solo SELECT/WITH y sin palabras peligrosas
    private static readonly Regex OnlySelect = new(@"^\s*(select|with)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly string[] Forbidden = {
        "insert","update","delete","drop","alter","create","grant","revoke",
        "truncate","execute","call","\\copy","copy","comment"
    };

    public ReadOnlyQueryService(
        IConfiguration cfg,
        IOptionsMonitor<DbStudioOptions> opts,
        ZooSanMarinoContext? ctx = null)
    {
        _connStr = ConnectionStringResolver.Resolve(cfg, ctx);
        _opts = opts;
    }

    public async Task<QueryPageDto> RunSafeSelectAsync(SelectQueryDto dto, string actorUserId, CancellationToken ct)
    {
        if (!_opts.CurrentValue.Enabled)
            throw new InvalidOperationException("DB Studio deshabilitado por configuración.");

        var maxLimit = Math.Max(1, _opts.CurrentValue.SelectMaxLimit <= 0 ? 500 : _opts.CurrentValue.SelectMaxLimit);
        var limit = Math.Max(1, Math.Min(dto.Limit <= 0 ? 100 : dto.Limit, maxLimit));
        var offset = Math.Max(0, dto.Offset);

        var sql = dto.Sql?.Trim() ?? throw new InvalidOperationException("SQL requerido.");
        if (sql.Contains(';')) throw new InvalidOperationException("Una sola sentencia permitida.");
        if (!OnlySelect.IsMatch(sql)) throw new InvalidOperationException("Solo se permite SELECT o WITH.");
        var lower = sql.ToLowerInvariant();
        foreach (var f in Forbidden)
            if (Regex.IsMatch(lower, $@"\b{f}\b"))
                throw new InvalidOperationException($"Operación no permitida en consulta: {f}");

        // Forzar paginación
        string runSql = $"with _q as ({sql}) select * from _q offset $1 limit $2;";

        try
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            var rows = new List<Dictionary<string, object?>>();

            await using (var cmd = new NpgsqlCommand(runSql, conn))
            {
                // $1 y $2 siempre son offset/limit
                cmd.Parameters.AddWithValue(offset);
                cmd.Parameters.AddWithValue(limit);

                // Parámetros adicionales: soporta @name (nativo) o :name (los convierte a $n)
                if (dto.Params is not null && dto.Params.Count > 0)
                {
                    // Orden determinístico de parámetros adicionales
                    var kvs = dto.Params.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList();
                    var nextIndex = 3;

                    foreach (var (key, value) in kvs)
                    {
                        // Si el SQL ya usa @key, agregamos named parameter
                        if (runSql.Contains('@' + key))
                        {
                            // Nada que reemplazar; Npgsql entiende @key
                            cmd.Parameters.AddWithValue('@' + key, value ?? DBNull.Value);
                        }
                        else if (runSql.Contains(":" + key))
                        {
                            // Convertimos :key -> $n
                            runSql = runSql.Replace(":" + key, $"${nextIndex}");
                            cmd.Parameters.AddWithValue(value ?? DBNull.Value);
                            nextIndex++;
                        }
                        // Si el parámetro no está en el SQL, lo ignoramos silenciosamente
                    }

                    cmd.CommandText = runSql;
                }

                await using var rd = await cmd.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct))
                {
                    var row = new Dictionary<string, object?>(rd.FieldCount, StringComparer.Ordinal);
                    for (var i = 0; i < rd.FieldCount; i++)
                        row[rd.GetName(i)] = await rd.IsDBNullAsync(i, ct) ? null : rd.GetValue(i);
                    rows.Add(row);
                }
            }

            // No contamos total real (costoso). Devolvemos el tamaño de la página.
            return new QueryPageDto(rows, rows.Count, limit, offset);
        }
        catch (PostgresException pgx)
        {
            // Error controlado de Postgres (p. ej., columna inexistente)
            throw new InvalidOperationException($"Error de SQL: {pgx.MessageText}", pgx);
        }
        catch (Exception ex)
        {
            // Otros errores (conexión, timeout, etc.)
            throw new InvalidOperationException($"No se pudo ejecutar la consulta: {ex.Message}", ex);
        }
    }
}
