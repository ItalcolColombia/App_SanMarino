using System.Diagnostics;
using Npgsql;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public sealed partial class DbStudioService
{
    // ===================== SELECT SEGURO =====================
    public async Task<QueryPageDto> ExecuteSelectQueryAsync(SelectQueryRequest request)
    {
        var ct = CancellationToken.None;
        var sql = request.Sql?.Trim() ?? throw new InvalidOperationException("SQL requerido.");
        if (!DbStudioSqlCalculos.IsPureSelect(sql))
            throw new InvalidOperationException("Solo se permite una sentencia SELECT/WITH de solo lectura.");

        var limit = ClampLimit(request.Limit);
        var offset = Math.Max(0, request.Offset);
        var inner = sql.TrimEnd(';');
        var runSql = $"with _q as ({inner}) select * from _q offset @__offset limit @__limit";

        var sw = Stopwatch.StartNew();
        await using var conn = await _rt.OpenReadAsync(ct);
        await using var cmd = new NpgsqlCommand(runSql, conn);
        AddParams(cmd, request.Params?.ToDictionary(k => k.Key, v => (object?)v.Value));
        cmd.Parameters.AddWithValue("__offset", offset);
        cmd.Parameters.AddWithValue("__limit", limit);

        var (rows, cols) = await ReadAllAsync(cmd, limit, ct);
        sw.Stop();

        return new QueryPageDto
        {
            Rows = rows,
            Columns = cols,
            Count = rows.Count,
            Limit = limit,
            Offset = offset,
            ExecutionTime = sw.ElapsedMilliseconds
        };
    }

    // ===================== PREVIEW DE TABLA =====================
    public async Task<QueryPageDto> PreviewTableAsync(string schema, string table, int limit = 50, int offset = 0)
    {
        DbStudioSqlCalculos.EnsureValidIdentifier(schema, "Schema");
        DbStudioSqlCalculos.EnsureValidIdentifier(table, "Tabla");
        var ct = CancellationToken.None;
        limit = ClampLimit(limit);
        offset = Math.Max(0, offset);

        var qname = DbStudioSqlCalculos.QuoteQualified(schema, table);
        var sql = $"select * from {qname} offset @__offset limit @__limit";

        var sw = Stopwatch.StartNew();
        await using var conn = await _rt.OpenReadAsync(ct);

        long approx = 0;
        await using (var cmd2 = new NpgsqlCommand(
            "select reltuples::bigint from pg_class where oid = (@q)::regclass", conn))
        {
            cmd2.Parameters.AddWithValue("q", $"{schema}.{table}");
            approx = Convert.ToInt64(await cmd2.ExecuteScalarAsync(ct) ?? 0L);
        }

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("__offset", offset);
        cmd.Parameters.AddWithValue("__limit", limit);
        var (rows, cols) = await ReadAllAsync(cmd, limit, ct);
        sw.Stop();

        return new QueryPageDto
        {
            Rows = rows,
            Columns = cols,
            Count = Math.Max(approx, rows.Count),
            Limit = limit,
            Offset = offset,
            ExecutionTime = sw.ElapsedMilliseconds
        };
    }

    // ===================== CLASIFICACIÓN / VALIDACIÓN =====================
    public SqlClassificationDto ClassifySql(string sql)
    {
        var c = DbStudioSqlCalculos.Classify(sql);
        return new SqlClassificationDto
        {
            Kind = c.Kind.ToString().ToLowerInvariant(),
            IsReadOnly = c.IsReadOnly,
            RequiresConfirmation = c.RequiresConfirmation,
            Reason = c.Reason
        };
    }

    public Task<SqlValidationResult> ValidateSqlAsync(string sql)
    {
        var c = DbStudioSqlCalculos.Classify(sql);
        var valid = c.Kind is not (DbStudioSqlCalculos.SqlKind.Empty or DbStudioSqlCalculos.SqlKind.Multiple);
        return Task.FromResult(new SqlValidationResult { Valid = valid, Error = valid ? null : c.Reason });
    }

    // ===================== SQL ARBITRARIO (ADMIN) =====================
    public Task<QueryResultDto> ExecuteQueryAsync(ExecuteQueryRequest request) =>
        ExecuteSqlAsync(new ExecuteSqlRequest
        {
            Sql = request.Sql,
            Params = request.Params?.ToDictionary(k => k.Key, v => (object?)v.Value),
            Confirm = false
        });

    public async Task<QueryResultDto> ExecuteSqlAsync(ExecuteSqlRequest request)
    {
        var ct = CancellationToken.None;
        var sql = request.Sql?.Trim() ?? throw new InvalidOperationException("SQL requerido.");
        var c = DbStudioSqlCalculos.Classify(sql);

        if (c.Kind == DbStudioSqlCalculos.SqlKind.Empty)
            throw new InvalidOperationException("Sentencia vacía.");
        if (c.Kind == DbStudioSqlCalculos.SqlKind.Multiple)
            throw new InvalidOperationException("Solo se permite una sentencia por ejecución.");
        if (c.Kind == DbStudioSqlCalculos.SqlKind.Dangerous)
            throw new InvalidOperationException(c.Reason ?? "Operación bloqueada.");
        if (c.RequiresConfirmation && !request.Confirm)
            throw new InvalidOperationException(
                (c.Reason ?? "Operación destructiva.") + " Reenviá con confirm=true para ejecutar.");

        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = await _rt.OpenWriteAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            AddParams(cmd, request.Params);

            if (c.IsReadOnly)
            {
                var (rows, cols) = await ReadAllAsync(cmd, _opts.SelectMaxLimit, ct);
                sw.Stop();
                await AuditAsync("EXECUTE_SELECT", null, null, sql, true, new { rows = rows.Count }, ct);
                return new QueryResultDto
                {
                    Success = true,
                    Data = new QueryPageDto { Rows = rows, Columns = cols, Count = rows.Count, ExecutionTime = sw.ElapsedMilliseconds },
                    ExecutionTime = sw.ElapsedMilliseconds
                };
            }

            var affected = await cmd.ExecuteNonQueryAsync(ct);
            sw.Stop();
            await AuditAsync("EXECUTE", null, null, sql, true, new { affected }, ct);
            return new QueryResultDto { Success = true, AffectedRows = affected, ExecutionTime = sw.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            sw.Stop();
            await AuditAsync("EXECUTE", null, null, sql, false, new { error = ex.Message }, ct);
            return new QueryResultDto { Success = false, Error = ex.Message, ExecutionTime = sw.ElapsedMilliseconds };
        }
    }
}
