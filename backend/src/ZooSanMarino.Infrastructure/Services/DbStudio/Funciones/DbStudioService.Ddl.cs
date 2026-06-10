using System.Text;
using Npgsql;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using static ZooSanMarino.Application.Calculos.DbStudioSqlCalculos;

namespace ZooSanMarino.Infrastructure.Services;

public sealed partial class DbStudioService
{
    /// <summary>Ejecuta un script DDL en una transacción y registra auditoría (éxito o error).</summary>
    private async Task ExecuteDdlAsync(string action, string? schema, string? obj, string sql, CancellationToken ct)
    {
        await using var conn = await _rt.OpenWriteAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            await using (var cmd = new NpgsqlCommand(sql, conn, tx))
                await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            await AuditAsync(action, schema, obj, sql, true, new { }, ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            await AuditAsync(action, schema, obj, sql, false, new { error = ex.Message }, ct);
            throw new InvalidOperationException($"Error ejecutando {action}: {ex.Message}", ex);
        }
    }

    private static string QualifyReferenced(string referenced)
    {
        // Permite 'tabla' o 'schema.tabla'.
        var parts = referenced.Split('.', 2);
        if (parts.Length == 2) return QuoteQualified(parts[0], parts[1]);
        EnsureValidIdentifier(referenced, "Tabla referenciada");
        return QuoteIdent(referenced);
    }

    // ===================== TABLAS =====================
    public async Task CreateTableAsync(CreateTableRequest request)
    {
        EnsureValidIdentifier(request.Schema, "Schema");
        EnsureValidIdentifier(request.Table, "Tabla");
        if (request.Columns is null || request.Columns.Count == 0)
            throw new InvalidOperationException("Debe definir al menos una columna.");

        var qtable = QuoteQualified(request.Schema, request.Table);
        var sb = new StringBuilder();
        var defs = new List<string>();
        foreach (var col in request.Columns)
            defs.Add(BuildColumnDefinition(col.Name, col.Type, col.Nullable, col.Default, col.Identity,
                col.MaxLength, col.Precision, col.Scale));

        if (request.PrimaryKey is { Count: > 0 })
        {
            foreach (var pk in request.PrimaryKey) EnsureValidIdentifier(pk, "Columna PK");
            defs.Add($"PRIMARY KEY ({string.Join(", ", request.PrimaryKey.Select(QuoteIdent))})");
        }
        if (request.Uniques is not null)
        {
            foreach (var ux in request.Uniques.Where(u => u is { Count: > 0 }))
            {
                foreach (var c in ux) EnsureValidIdentifier(c, "Columna UNIQUE");
                defs.Add($"UNIQUE ({string.Join(", ", ux.Select(QuoteIdent))})");
            }
        }

        sb.Append($"CREATE TABLE {qtable} (").Append(string.Join(", ", defs)).Append(");");

        if (request.Indexes is not null)
            foreach (var ix in request.Indexes)
            {
                EnsureValidIdentifier(ix.Name, "Índice");
                foreach (var c in ix.Columns) EnsureValidIdentifier(c, "Columna índice");
                var uniq = ix.Unique ? "UNIQUE " : "";
                sb.Append($" CREATE {uniq}INDEX {QuoteIdent(ix.Name)} ON {qtable} ({string.Join(", ", ix.Columns.Select(QuoteIdent))});");
            }

        if (request.ForeignKeys is not null)
            foreach (var fk in request.ForeignKeys)
            {
                EnsureValidIdentifier(fk.Name, "FK");
                EnsureValidIdentifier(fk.Column, "Columna FK");
                EnsureValidIdentifier(fk.ReferencedColumn, "Columna referenciada");
                var onDel = string.IsNullOrWhiteSpace(fk.OnDelete) ? "" : $" ON DELETE {fk.OnDelete}";
                var onUpd = string.IsNullOrWhiteSpace(fk.OnUpdate) ? "" : $" ON UPDATE {fk.OnUpdate}";
                sb.Append($" ALTER TABLE {qtable} ADD CONSTRAINT {QuoteIdent(fk.Name)} FOREIGN KEY ({QuoteIdent(fk.Column)}) REFERENCES {QualifyReferenced(fk.ReferencedTable)} ({QuoteIdent(fk.ReferencedColumn)}){onDel}{onUpd};");
            }

        await ExecuteDdlAsync("CREATE_TABLE", request.Schema, request.Table, sb.ToString(), CancellationToken.None);
    }

    public async Task DropTableAsync(string schema, string table, bool cascade = false)
    {
        var sql = $"DROP TABLE {QuoteQualified(schema, table)}{(cascade ? " CASCADE" : "")};";
        await ExecuteDdlAsync("DROP_TABLE", schema, table, sql, CancellationToken.None);
    }

    // ===================== COLUMNAS =====================
    public async Task AddColumnAsync(string schema, string table, AddColumnRequest request)
    {
        var def = BuildColumnDefinition(request.Name, request.Type, request.Nullable, request.Default,
            null, request.MaxLength, request.Precision, request.Scale);
        var sql = $"ALTER TABLE {QuoteQualified(schema, table)} ADD COLUMN {def};";
        await ExecuteDdlAsync("ADD_COLUMN", schema, table, sql, CancellationToken.None);
    }

    public async Task AlterColumnAsync(string schema, string table, string column, AlterColumnRequest request)
    {
        EnsureValidIdentifier(column, "Columna");
        var qtable = QuoteQualified(schema, table);
        var qcol = QuoteIdent(column);
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(request.NewType))
        {
            var newType = RenderColumnType(request.NewType, request.NewMaxLength, request.NewPrecision, request.NewScale);
            sb.Append($"ALTER TABLE {qtable} ALTER COLUMN {qcol} TYPE {newType} USING {qcol}::{newType};");
        }
        if (request.SetNotNull == true) sb.Append($"ALTER TABLE {qtable} ALTER COLUMN {qcol} SET NOT NULL;");
        if (request.DropNotNull == true) sb.Append($"ALTER TABLE {qtable} ALTER COLUMN {qcol} DROP NOT NULL;");
        if (!string.IsNullOrWhiteSpace(request.SetDefault)) sb.Append($"ALTER TABLE {qtable} ALTER COLUMN {qcol} SET DEFAULT {request.SetDefault};");
        if (request.DropDefault == true) sb.Append($"ALTER TABLE {qtable} ALTER COLUMN {qcol} DROP DEFAULT;");

        if (sb.Length == 0) throw new InvalidOperationException("Nada para alterar.");
        await ExecuteDdlAsync("ALTER_COLUMN", schema, table, sb.ToString(), CancellationToken.None);
    }

    public async Task DropColumnAsync(string schema, string table, string column)
    {
        EnsureValidIdentifier(column, "Columna");
        var sql = $"ALTER TABLE {QuoteQualified(schema, table)} DROP COLUMN {QuoteIdent(column)};";
        await ExecuteDdlAsync("DROP_COLUMN", schema, table, sql, CancellationToken.None);
    }

    // ===================== ÍNDICES =====================
    public async Task CreateIndexAsync(string schema, string table, CreateIndexRequest request)
    {
        EnsureValidIdentifier(request.Name, "Índice");
        foreach (var c in request.Columns) EnsureValidIdentifier(c, "Columna índice");
        var uniq = request.Unique ? "UNIQUE " : "";
        var sql = $"CREATE {uniq}INDEX {QuoteIdent(request.Name)} ON {QuoteQualified(schema, table)} ({string.Join(", ", request.Columns.Select(QuoteIdent))});";
        await ExecuteDdlAsync("CREATE_INDEX", schema, table, sql, CancellationToken.None);
    }

    public async Task DropIndexAsync(string schema, string table, string indexName)
    {
        var sql = $"DROP INDEX {QuoteQualified(schema, indexName)};";
        await ExecuteDdlAsync("DROP_INDEX", schema, indexName, sql, CancellationToken.None);
    }

    // ===================== CLAVES FORÁNEAS =====================
    public async Task CreateForeignKeyAsync(string schema, string table, CreateForeignKeyRequest request)
    {
        EnsureValidIdentifier(request.Name, "FK");
        EnsureValidIdentifier(request.Column, "Columna FK");
        EnsureValidIdentifier(request.ReferencedColumn, "Columna referenciada");
        var onDel = string.IsNullOrWhiteSpace(request.OnDelete) ? "" : $" ON DELETE {request.OnDelete}";
        var onUpd = string.IsNullOrWhiteSpace(request.OnUpdate) ? "" : $" ON UPDATE {request.OnUpdate}";
        var sql = $"ALTER TABLE {QuoteQualified(schema, table)} ADD CONSTRAINT {QuoteIdent(request.Name)} FOREIGN KEY ({QuoteIdent(request.Column)}) REFERENCES {QualifyReferenced(request.ReferencedTable)} ({QuoteIdent(request.ReferencedColumn)}){onDel}{onUpd};";
        await ExecuteDdlAsync("CREATE_FK", schema, table, sql, CancellationToken.None);
    }

    public async Task DropForeignKeyAsync(string schema, string table, string fkName)
    {
        EnsureValidIdentifier(fkName, "FK");
        var sql = $"ALTER TABLE {QuoteQualified(schema, table)} DROP CONSTRAINT {QuoteIdent(fkName)};";
        await ExecuteDdlAsync("DROP_FK", schema, table, sql, CancellationToken.None);
    }

    // ===================== VISTAS =====================
    public async Task CreateOrReplaceViewAsync(CreateViewRequest request)
    {
        EnsureValidIdentifier(request.Schema, "Schema");
        EnsureValidIdentifier(request.Name, "Vista");
        if (!DbStudioSqlCalculos.IsPureSelect(request.SelectSql))
            throw new InvalidOperationException("La definición de la vista debe ser un único SELECT de solo lectura.");

        var qname = QuoteQualified(request.Schema, request.Name);
        var body = request.SelectSql.Trim().TrimEnd(';');
        var sql = request.Materialized
            ? $"CREATE MATERIALIZED VIEW {qname} AS {body};"
            : $"CREATE OR REPLACE VIEW {qname} AS {body};";
        await ExecuteDdlAsync("CREATE_VIEW", request.Schema, request.Name, sql, CancellationToken.None);
    }

    public async Task DropViewAsync(string schema, string name, bool materialized)
    {
        var sql = $"DROP {(materialized ? "MATERIALIZED VIEW" : "VIEW")} {QuoteQualified(schema, name)};";
        await ExecuteDdlAsync("DROP_VIEW", schema, name, sql, CancellationToken.None);
    }

    // ===================== FUNCIONES / PROCEDIMIENTOS =====================
    public async Task CreateOrReplaceRoutineAsync(CreateRoutineRequest request)
    {
        var def = request.Definition?.Trim() ?? throw new InvalidOperationException("Definición requerida.");
        // El cuerpo de una función contiene ';' internos (dentro de $$...$$); por eso NO se valida
        // multi-statement. Es admin-only y queda auditado.
        await ExecuteDdlAsync("CREATE_ROUTINE", null, null, def, CancellationToken.None);
    }
}
