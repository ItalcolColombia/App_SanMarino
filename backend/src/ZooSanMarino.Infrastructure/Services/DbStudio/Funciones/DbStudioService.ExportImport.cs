using System.Globalization;
using System.Text;
using System.Text.Json;
using Npgsql;
using static ZooSanMarino.Application.Calculos.DbStudioSqlCalculos;

namespace ZooSanMarino.Infrastructure.Services;

public sealed partial class DbStudioService
{
    // ===================== EXPORT =====================
    public async Task<byte[]> ExportTableAsync(string schema, string table, string format = "sql")
    {
        var qtable = QuoteQualified(schema, table);
        var ct = CancellationToken.None;
        var max = _opts.MaxExportRows <= 0 ? 100_000 : _opts.MaxExportRows;

        await using var conn = await _rt.OpenReadAsync(ct);
        await using var cmd = new NpgsqlCommand($"select * from {qtable} limit {max}", conn);
        var (rows, cols) = await ReadAllAsync(cmd, max, ct);

        var content = format.ToLowerInvariant() switch
        {
            "csv" => BuildCsv(cols, rows),
            "json" => JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }),
            _ => BuildInsertSql(schema, table, cols, rows)
        };
        return Encoding.UTF8.GetBytes(content);
    }

    public async Task<byte[]> ExportSchemaAsync(string schema)
    {
        EnsureValidIdentifier(schema, "Schema");
        var tables = await GetTablesAsync(schema);
        var sb = new StringBuilder();
        sb.AppendLine($"-- Esquema {schema} — generado por DB Studio el {DateTime.UtcNow:u}");
        foreach (var t in tables.Where(t => t.Kind is "BASE TABLE" or "PARTITIONED TABLE"))
        {
            var cols = (await GetTableColumnsAsync(schema, t.Name)).ToList();
            sb.AppendLine().AppendLine($"CREATE TABLE {QuoteQualified(schema, t.Name)} (");
            var defs = new List<string>();
            foreach (var c in cols)
            {
                var def = $"  {QuoteIdent(c.Name)} {c.DataType}";
                if (!c.IsNullable) def += " NOT NULL";
                if (!string.IsNullOrWhiteSpace(c.Default)) def += $" DEFAULT {c.Default}";
                defs.Add(def);
            }
            var pk = cols.Where(c => c.IsPrimaryKey).Select(c => QuoteIdent(c.Name)).ToList();
            if (pk.Count > 0) defs.Add($"  PRIMARY KEY ({string.Join(", ", pk)})");
            sb.AppendLine(string.Join(",\n", defs)).AppendLine(");");
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string BuildCsv(List<string> cols, List<Dictionary<string, object?>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", cols.Select(CsvCell)));
        foreach (var r in rows)
            sb.AppendLine(string.Join(",", cols.Select(c => CsvCell(Stringify(r.GetValueOrDefault(c))))));
        return sb.ToString();
    }

    private static string CsvCell(string? v)
    {
        v ??= string.Empty;
        if (v.Contains(',') || v.Contains('"') || v.Contains('\n') || v.Contains('\r'))
            return $"\"{v.Replace("\"", "\"\"")}\"";
        return v;
    }

    private string BuildInsertSql(string schema, string table, List<string> cols, List<Dictionary<string, object?>> rows)
    {
        var qtable = QuoteQualified(schema, table);
        var colSql = string.Join(", ", cols.Select(QuoteIdent));
        var sb = new StringBuilder();
        foreach (var r in rows)
        {
            var vals = string.Join(", ", cols.Select(c => SqlLiteral(r.GetValueOrDefault(c))));
            sb.AppendLine($"INSERT INTO {qtable} ({colSql}) VALUES ({vals});");
        }
        return sb.ToString();
    }

    private static string? Stringify(object? v) => v switch
    {
        null => null,
        DateTime dt => dt.ToString("o", CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        _ => Convert.ToString(v, CultureInfo.InvariantCulture)
    };

    private static string SqlLiteral(object? v) => v switch
    {
        null => "NULL",
        bool b => b ? "true" : "false",
        short or int or long or float or double or decimal => Convert.ToString(v, CultureInfo.InvariantCulture)!,
        DateTime dt => $"'{dt:o}'",
        _ => "'" + (Convert.ToString(v, CultureInfo.InvariantCulture) ?? "").Replace("'", "''") + "'"
    };

    // ===================== IMPORT =====================
    public async Task ImportTableAsync(string schema, string table, byte[] fileContent, string format = "csv")
    {
        var text = Encoding.UTF8.GetString(fileContent);
        List<Dictionary<string, object>> rows;

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var parsed = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(text) ?? new();
            rows = parsed.Select(d => d.ToDictionary(
                kv => kv.Key, kv => (object)(JsonToClr(kv.Value) ?? DBNull.Value))).ToList();
        }
        else
        {
            rows = ParseCsv(text);
        }

        if (rows.Count == 0) throw new InvalidOperationException("El archivo no contiene filas.");
        await InsertDataAsync(schema, table, rows);
    }

    private static object? JsonToClr(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => e.TryGetInt64(out var l) ? l : e.GetDouble(),
        JsonValueKind.String => e.GetString(),
        _ => e.GetRawText()
    };

    private static List<Dictionary<string, object>> ParseCsv(string text)
    {
        var result = new List<Dictionary<string, object>>();
        var lines = SplitCsvLines(text);
        if (lines.Count < 2) return result;
        var headers = SplitCsvLine(lines[0]);
        for (var i = 1; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var cells = SplitCsvLine(lines[i]);
            var row = new Dictionary<string, object>();
            for (var c = 0; c < headers.Count && c < cells.Count; c++)
                row[headers[c]] = cells[c].Length == 0 ? DBNull.Value : cells[c];
            result.Add(row);
        }
        return result;
    }

    private static List<string> SplitCsvLines(string text)
        => text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Where(l => l.Length > 0).ToList();

    private static List<string> SplitCsvLine(string line)
    {
        var cells = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(ch);
            }
            else if (ch == '"') inQuotes = true;
            else if (ch == ',') { cells.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(ch);
        }
        cells.Add(sb.ToString());
        return cells;
    }
}
