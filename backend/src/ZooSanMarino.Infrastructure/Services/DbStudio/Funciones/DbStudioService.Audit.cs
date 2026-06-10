using System.Text.Json;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace ZooSanMarino.Infrastructure.Services;

public sealed partial class DbStudioService
{
    /// <summary>
    /// Registra una acción en <c>public.dbstudio_audit</c>. Nunca lanza: un fallo de auditoría
    /// no debe tumbar la operación principal (se loguea y sigue).
    /// </summary>
    private async Task AuditAsync(
        string action, string? schema, string? objectName, string sqlText,
        bool success, object? resultSummary, CancellationToken ct)
    {
        try
        {
            const string sql = @"
                insert into public.dbstudio_audit
                  (action, schema_name, object_name, sql_text, result_summary, success,
                   actor_user_id, actor_email, company_id, ip_address, created_at_utc)
                values
                  (@action, @schema, @object, @sql, @summary::jsonb, @success,
                   @actor, @email, @company, @ip, now() at time zone 'utc');";

            await using var conn = await _rt.OpenWriteAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("action", action);
            cmd.Parameters.AddWithValue("schema", (object?)schema ?? DBNull.Value);
            cmd.Parameters.AddWithValue("object", (object?)objectName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("sql", sqlText ?? string.Empty);
            cmd.Parameters.Add(new NpgsqlParameter("summary", NpgsqlDbType.Jsonb)
            {
                Value = JsonSerializer.Serialize(resultSummary ?? new { })
            });
            cmd.Parameters.AddWithValue("success", success);
            cmd.Parameters.AddWithValue("actor", (object?)_current.UserGuid ?? DBNull.Value);
            cmd.Parameters.AddWithValue("email", (object?)GetActorEmail() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("company", _current.CompanyId);
            cmd.Parameters.AddWithValue("ip", (object?)GetClientIp() ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo registrar auditoría DB Studio para acción {Action}", action);
        }
    }
}
