using Npgsql;
using static ZooSanMarino.Application.Calculos.DbStudioSqlCalculos;

namespace ZooSanMarino.Infrastructure.Services;

public sealed partial class DbStudioService
{
    public async Task InsertDataAsync(string schema, string table, List<Dictionary<string, object>> data)
    {
        if (data is null || data.Count == 0) throw new InvalidOperationException("Sin filas para insertar.");
        var qtable = QuoteQualified(schema, table);
        var ct = CancellationToken.None;
        var inserted = 0;

        await using var conn = await _rt.OpenWriteAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            foreach (var row in data)
            {
                if (row.Count == 0) continue;
                foreach (var k in row.Keys) EnsureValidIdentifier(k, "Columna");
                var cols = row.Keys.ToList();
                var colSql = string.Join(", ", cols.Select(QuoteIdent));
                var valSql = string.Join(", ", cols.Select((_, i) => $"@p{i}"));
                await using var cmd = new NpgsqlCommand($"INSERT INTO {qtable} ({colSql}) VALUES ({valSql});", conn, tx);
                for (var i = 0; i < cols.Count; i++)
                    cmd.Parameters.AddWithValue($"p{i}", row[cols[i]] ?? (object)DBNull.Value);
                inserted += await cmd.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
            await AuditAsync("INSERT_DATA", schema, table, $"insert {data.Count} fila(s)", true, new { inserted }, ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            await AuditAsync("INSERT_DATA", schema, table, "insert", false, new { error = ex.Message }, ct);
            throw new InvalidOperationException($"Error insertando datos: {ex.Message}", ex);
        }
    }

    public async Task UpdateDataAsync(string schema, string table, Dictionary<string, object> data, Dictionary<string, object> where)
    {
        if (data is null || data.Count == 0) throw new InvalidOperationException("Nada para actualizar.");
        if (where is null || where.Count == 0) throw new InvalidOperationException("WHERE requerido para actualizar (seguridad).");
        var qtable = QuoteQualified(schema, table);
        var ct = CancellationToken.None;

        foreach (var k in data.Keys) EnsureValidIdentifier(k, "Columna");
        foreach (var k in where.Keys) EnsureValidIdentifier(k, "Columna WHERE");

        var setKeys = data.Keys.ToList();
        var whereKeys = where.Keys.ToList();
        var setSql = string.Join(", ", setKeys.Select((k, i) => $"{QuoteIdent(k)} = @s{i}"));
        var whereSql = string.Join(" AND ", whereKeys.Select((k, i) => $"{QuoteIdent(k)} = @w{i}"));

        await using var conn = await _rt.OpenWriteAsync(ct);
        await using var cmd = new NpgsqlCommand($"UPDATE {qtable} SET {setSql} WHERE {whereSql};", conn);
        for (var i = 0; i < setKeys.Count; i++) cmd.Parameters.AddWithValue($"s{i}", data[setKeys[i]] ?? (object)DBNull.Value);
        for (var i = 0; i < whereKeys.Count; i++) cmd.Parameters.AddWithValue($"w{i}", where[whereKeys[i]] ?? (object)DBNull.Value);

        try
        {
            var affected = await cmd.ExecuteNonQueryAsync(ct);
            await AuditAsync("UPDATE_DATA", schema, table, "update", true, new { affected }, ct);
        }
        catch (Exception ex)
        {
            await AuditAsync("UPDATE_DATA", schema, table, "update", false, new { error = ex.Message }, ct);
            throw new InvalidOperationException($"Error actualizando datos: {ex.Message}", ex);
        }
    }

    public async Task DeleteDataAsync(string schema, string table, Dictionary<string, object> where)
    {
        if (where is null || where.Count == 0) throw new InvalidOperationException("WHERE requerido para borrar (seguridad).");
        var qtable = QuoteQualified(schema, table);
        var ct = CancellationToken.None;

        foreach (var k in where.Keys) EnsureValidIdentifier(k, "Columna WHERE");
        var whereKeys = where.Keys.ToList();
        var whereSql = string.Join(" AND ", whereKeys.Select((k, i) => $"{QuoteIdent(k)} = @w{i}"));

        await using var conn = await _rt.OpenWriteAsync(ct);
        await using var cmd = new NpgsqlCommand($"DELETE FROM {qtable} WHERE {whereSql};", conn);
        for (var i = 0; i < whereKeys.Count; i++) cmd.Parameters.AddWithValue($"w{i}", where[whereKeys[i]] ?? (object)DBNull.Value);

        try
        {
            var affected = await cmd.ExecuteNonQueryAsync(ct);
            await AuditAsync("DELETE_DATA", schema, table, "delete", true, new { affected }, ct);
        }
        catch (Exception ex)
        {
            await AuditAsync("DELETE_DATA", schema, table, "delete", false, new { error = ex.Message }, ct);
            throw new InvalidOperationException($"Error borrando datos: {ex.Message}", ex);
        }
    }
}
