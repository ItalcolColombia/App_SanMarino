using Npgsql;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

public sealed partial class DbStudioService
{
    private static string? Str(object v) => v is DBNull ? null : Convert.ToString(v);
    private static long Lng(object v) => v is DBNull ? 0L : Convert.ToInt64(v);
    private static int Int(object v) => v is DBNull ? 0 : Convert.ToInt32(v);

    // ===================== ESQUEMAS =====================
    public async Task<IEnumerable<SchemaDto>> GetSchemasAsync()
    {
        const string sql = @"
            select n.nspname as name,
                   count(c.*) filter (where c.relkind in ('r','p')) as tables
            from pg_namespace n
            left join pg_class c on c.relnamespace = n.oid
            where n.nspname not like 'pg_%' and n.nspname <> 'information_schema'
            group by n.nspname
            order by n.nspname;";

        await using var conn = await _rt.OpenReadAsync(CancellationToken.None);
        await using var cmd = new NpgsqlCommand(sql, conn);
        var (rows, _) = await ReadAllAsync(cmd, null, CancellationToken.None);
        return rows.Select(r => new SchemaDto { Name = Str(r["name"]!)!, Tables = Int(r["tables"]!) }).ToList();
    }

    // ===================== TABLAS =====================
    public async Task<IEnumerable<TableDto>> GetTablesAsync(string? schema = null)
    {
        var sch = string.IsNullOrWhiteSpace(schema) ? "public" : schema!;
        const string sql = @"
            select c.relname as name,
                   case c.relkind when 'r' then 'BASE TABLE' when 'p' then 'PARTITIONED TABLE'
                                  when 'v' then 'VIEW' when 'm' then 'MATERIALIZED VIEW' else 'TABLE' end as kind,
                   coalesce(s.n_live_tup, c.reltuples::bigint, 0) as rows,
                   pg_size_pretty(pg_total_relation_size(c.oid)) as size
            from pg_class c
            join pg_namespace n on n.oid = c.relnamespace
            left join pg_stat_user_tables s on s.relid = c.oid
            where n.nspname = @schema and c.relkind in ('r','p','v','m')
            order by c.relname;";

        await using var conn = await _rt.OpenReadAsync(CancellationToken.None);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("schema", sch);
        var (rows, _) = await ReadAllAsync(cmd, null, CancellationToken.None);

        var readable = await _authz.GetReadableObjectKeysAsync();
        var list = rows.Select(r => new TableDto
        {
            Schema = sch,
            Name = Str(r["name"]!)!,
            Kind = Str(r["kind"]!) ?? "TABLE",
            Rows = Lng(r["rows"]!),
            Size = Str(r["size"]!) ?? "N/A"
        });

        if (readable is not null)
            list = list.Where(t => readable.Contains($"{t.Schema}.{t.Name}".ToLowerInvariant()));

        return list.ToList();
    }

    public async Task<TableDetailsDto> GetTableDetailsAsync(string schema, string table)
    {
        var tables = await GetTablesAsync(schema);
        var tableDto = tables.FirstOrDefault(t => t.Name == table) ?? new TableDto { Schema = schema, Name = table };
        return new TableDetailsDto
        {
            Table = tableDto,
            Columns = (await GetTableColumnsAsync(schema, table)).ToList(),
            Indexes = (await GetTableIndexesAsync(schema, table)).ToList(),
            ForeignKeys = (await GetTableForeignKeysAsync(schema, table)).ToList(),
            Stats = await GetTableStatsAsync(schema, table)
        };
    }

    public async Task<IEnumerable<ColumnDto>> GetTableColumnsAsync(string schema, string table)
    {
        const string sql = @"
            select a.attname as name,
                   pg_catalog.format_type(a.atttypid, a.atttypmod) as data_type,
                   not a.attnotnull as is_nullable,
                   pg_get_expr(ad.adbin, ad.adrelid) as default_value,
                   exists(select 1 from pg_index i where i.indrelid = c.oid and i.indisprimary and a.attnum = any(i.indkey)) as is_primary_key,
                   information_schema._pg_char_max_length(a.atttypid, a.atttypmod) as max_length,
                   information_schema._pg_numeric_precision(a.atttypid, a.atttypmod) as precision,
                   information_schema._pg_numeric_scale(a.atttypid, a.atttypmod) as scale,
                   (a.attidentity <> '') as is_identity,
                   col_description(c.oid, a.attnum) as comment
            from pg_attribute a
            join pg_class c on c.oid = a.attrelid
            join pg_namespace n on n.oid = c.relnamespace
            left join pg_attrdef ad on ad.adrelid = c.oid and ad.adnum = a.attnum
            where n.nspname = @schema and c.relname = @table and a.attnum > 0 and not a.attisdropped
            order by a.attnum;";

        await using var conn = await _rt.OpenReadAsync(CancellationToken.None);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);
        var (rows, _) = await ReadAllAsync(cmd, null, CancellationToken.None);

        return rows.Select(r => new ColumnDto
        {
            Name = Str(r["name"]!)!,
            DataType = Str(r["data_type"]!) ?? "",
            IsNullable = r["is_nullable"] is bool b && b,
            Default = Str(r["default_value"]!),
            IsPrimaryKey = r["is_primary_key"] is bool pk && pk,
            MaxLength = r["max_length"] is DBNull ? null : Int(r["max_length"]!),
            Precision = r["precision"] is DBNull ? null : Int(r["precision"]!),
            Scale = r["scale"] is DBNull ? null : Int(r["scale"]!),
            IsIdentity = r["is_identity"] is bool id && id,
            Comment = Str(r["comment"]!)
        }).ToList();
    }

    public async Task<IEnumerable<IndexDto>> GetTableIndexesAsync(string schema, string table)
    {
        const string sql = @"
            select i.relname as name,
                   ix.indisunique as is_unique,
                   ix.indisprimary as is_primary,
                   am.amname as type,
                   array_agg(a.attname order by k.ord) as columns
            from pg_index ix
            join pg_class i on i.oid = ix.indexrelid
            join pg_class t on t.oid = ix.indrelid
            join pg_namespace n on n.oid = t.relnamespace
            join pg_am am on am.oid = i.relam
            join lateral unnest(ix.indkey) with ordinality as k(attnum, ord) on true
            join pg_attribute a on a.attrelid = t.oid and a.attnum = k.attnum
            where n.nspname = @schema and t.relname = @table
            group by i.relname, ix.indisunique, ix.indisprimary, am.amname
            order by i.relname;";

        await using var conn = await _rt.OpenReadAsync(CancellationToken.None);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);
        var (rows, _) = await ReadAllAsync(cmd, null, CancellationToken.None);

        return rows.Select(r => new IndexDto
        {
            Name = Str(r["name"]!)!,
            Type = Str(r["type"]!) ?? "btree",
            IsUnique = r["is_unique"] is bool u && u,
            IsPrimary = r["is_primary"] is bool p && p,
            Columns = r["columns"] is string[] arr ? arr.ToList() : new List<string>()
        }).ToList();
    }

    public async Task<IEnumerable<ForeignKeyDto>> GetTableForeignKeysAsync(string schema, string table)
    {
        const string sql = @"
            select tc.constraint_name as name,
                   kcu.column_name as column_name,
                   ccu.table_name as foreign_table_name,
                   ccu.column_name as foreign_column_name,
                   rc.delete_rule as on_delete,
                   rc.update_rule as on_update
            from information_schema.table_constraints tc
            join information_schema.key_column_usage kcu on tc.constraint_name = kcu.constraint_name and tc.table_schema = kcu.table_schema
            join information_schema.constraint_column_usage ccu on ccu.constraint_name = tc.constraint_name
            join information_schema.referential_constraints rc on tc.constraint_name = rc.constraint_name
            where tc.constraint_type = 'FOREIGN KEY' and tc.table_schema = @schema and tc.table_name = @table;";

        await using var conn = await _rt.OpenReadAsync(CancellationToken.None);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);
        var (rows, _) = await ReadAllAsync(cmd, null, CancellationToken.None);

        return rows.Select(r => new ForeignKeyDto
        {
            Name = Str(r["name"]!)!,
            Column = Str(r["column_name"]!) ?? "",
            ReferencedTable = Str(r["foreign_table_name"]!) ?? "",
            ReferencedColumn = Str(r["foreign_column_name"]!) ?? "",
            OnDelete = Str(r["on_delete"]!) ?? "",
            OnUpdate = Str(r["on_update"]!) ?? ""
        }).ToList();
    }

    public async Task<TableStatsDto> GetTableStatsAsync(string schema, string table)
    {
        const string sql = @"
            select coalesce(s.n_live_tup, c.reltuples::bigint, 0) as row_count,
                   pg_size_pretty(pg_relation_size(c.oid)) as table_size,
                   pg_size_pretty(pg_indexes_size(c.oid)) as index_size,
                   pg_size_pretty(pg_total_relation_size(c.oid)) as total_size,
                   greatest(s.last_analyze, s.last_autoanalyze) as last_analyzed
            from pg_class c
            join pg_namespace n on n.oid = c.relnamespace
            left join pg_stat_user_tables s on s.relid = c.oid
            where n.nspname = @schema and c.relname = @table;";

        await using var conn = await _rt.OpenReadAsync(CancellationToken.None);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);
        var (rows, _) = await ReadAllAsync(cmd, 1, CancellationToken.None);
        if (rows.Count == 0) return new TableStatsDto { TableName = table, SchemaName = schema };
        var r = rows[0];
        return new TableStatsDto
        {
            TableName = table,
            SchemaName = schema,
            RowCount = Lng(r["row_count"]!),
            TableSize = Str(r["table_size"]!) ?? "0 B",
            IndexSize = Str(r["index_size"]!) ?? "0 B",
            TotalSize = Str(r["total_size"]!) ?? "0 B",
            LastAnalyzed = r["last_analyzed"] is DBNull ? null : Convert.ToDateTime(r["last_analyzed"])
        };
    }

    public async Task<TableDependenciesDto> GetTableDependenciesAsync(string schema, string table)
    {
        // Dependientes: tablas cuyas FKs apuntan a (schema, table). Dependencias: a quién apunta esta tabla.
        const string sqlDependents = @"
            select distinct tc.table_schema as schema, tc.table_name as table
            from information_schema.table_constraints tc
            join information_schema.constraint_column_usage ccu on ccu.constraint_name = tc.constraint_name
            where tc.constraint_type = 'FOREIGN KEY' and ccu.table_schema = @schema and ccu.table_name = @table;";
        const string sqlDependencies = @"
            select distinct ccu.table_schema as schema, ccu.table_name as table
            from information_schema.table_constraints tc
            join information_schema.constraint_column_usage ccu on ccu.constraint_name = tc.constraint_name
            where tc.constraint_type = 'FOREIGN KEY' and tc.table_schema = @schema and tc.table_name = @table;";

        var result = new TableDependenciesDto();
        await using var conn = await _rt.OpenReadAsync(CancellationToken.None);

        await using (var cmd = new NpgsqlCommand(sqlDependencies, conn))
        {
            cmd.Parameters.AddWithValue("schema", schema);
            cmd.Parameters.AddWithValue("table", table);
            var (rows, _) = await ReadAllAsync(cmd, null, CancellationToken.None);
            result.Dependencies = rows.Select(r => new TableReferenceDto
            { Schema = Str(r["schema"]!) ?? "", Table = Str(r["table"]!) ?? "", Type = "table" }).ToList();
        }
        await using (var cmd = new NpgsqlCommand(sqlDependents, conn))
        {
            cmd.Parameters.AddWithValue("schema", schema);
            cmd.Parameters.AddWithValue("table", table);
            var (rows, _) = await ReadAllAsync(cmd, null, CancellationToken.None);
            result.Dependents = rows.Select(r => new TableReferenceDto
            { Schema = Str(r["schema"]!) ?? "", Table = Str(r["table"]!) ?? "", Type = "table" }).ToList();
        }
        return result;
    }

    public async Task<DatabaseAnalysisDto> AnalyzeDatabaseAsync()
    {
        const string sql = @"
            select count(distinct n.nspname) as total_schemas,
                   count(*) as total_tables,
                   coalesce(sum(coalesce(s.n_live_tup, c.reltuples::bigint, 0)),0) as total_rows,
                   pg_size_pretty(coalesce(sum(pg_total_relation_size(c.oid)),0)) as total_size
            from pg_class c
            join pg_namespace n on n.oid = c.relnamespace
            left join pg_stat_user_tables s on s.relid = c.oid
            where c.relkind in ('r','p') and n.nspname not like 'pg_%' and n.nspname <> 'information_schema';";

        await using var conn = await _rt.OpenReadAsync(CancellationToken.None);
        await using var cmd = new NpgsqlCommand(sql, conn);
        var (rows, _) = await ReadAllAsync(cmd, 1, CancellationToken.None);
        if (rows.Count == 0) return new DatabaseAnalysisDto();
        var r = rows[0];
        return new DatabaseAnalysisDto
        {
            TotalSchemas = Int(r["total_schemas"]!),
            TotalTables = Int(r["total_tables"]!),
            TotalRows = Lng(r["total_rows"]!),
            TotalSize = Str(r["total_size"]!) ?? "0 B"
        };
    }

    public Task<IEnumerable<string>> GetDataTypesAsync()
    {
        // Tipos más usados + escalares comunes de PostgreSQL.
        var types = new[]
        {
            "integer","bigint","smallint","serial","bigserial","numeric","real","double precision",
            "boolean","text","varchar","char","uuid","json","jsonb","date","time","timestamp",
            "timestamptz","interval","bytea","inet","cidr","macaddr","money","xml"
        };
        return Task.FromResult<IEnumerable<string>>(types);
    }

    // ===================== VISTAS =====================
    public async Task<IEnumerable<ViewDto>> GetViewsAsync(string? schema = null)
    {
        var sch = string.IsNullOrWhiteSpace(schema) ? "public" : schema!;
        const string sql = @"
            select c.relname as name, (c.relkind = 'm') as materialized
            from pg_class c join pg_namespace n on n.oid = c.relnamespace
            where n.nspname = @schema and c.relkind in ('v','m')
            order by c.relname;";

        await using var conn = await _rt.OpenReadAsync(CancellationToken.None);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("schema", sch);
        var (rows, _) = await ReadAllAsync(cmd, null, CancellationToken.None);

        var readable = await _authz.GetReadableObjectKeysAsync();
        var list = rows.Select(r => new ViewDto
        { Schema = sch, Name = Str(r["name"]!)!, Materialized = r["materialized"] is bool m && m });
        if (readable is not null)
            list = list.Where(v => readable.Contains($"{v.Schema}.{v.Name}".ToLowerInvariant()));
        return list.ToList();
    }

    public async Task<string> GetViewDefinitionAsync(string schema, string name)
    {
        const string sql = "select pg_get_viewdef((@schema || '.' || @name)::regclass, true) as def;";
        await using var conn = await _rt.OpenReadAsync(CancellationToken.None);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("name", name);
        var def = await cmd.ExecuteScalarAsync();
        return def as string ?? string.Empty;
    }

    // ===================== FUNCIONES =====================
    public async Task<IEnumerable<FunctionDto>> GetFunctionsAsync(string? schema = null)
    {
        var sch = string.IsNullOrWhiteSpace(schema) ? "public" : schema!;
        const string sql = @"
            select p.proname as name,
                   pg_get_function_arguments(p.oid) as arguments,
                   pg_get_function_result(p.oid) as return_type,
                   case p.prokind when 'f' then 'function' when 'p' then 'procedure'
                                  when 'a' then 'aggregate' when 'w' then 'window' else 'function' end as kind,
                   l.lanname as language
            from pg_proc p
            join pg_namespace n on n.oid = p.pronamespace
            join pg_language l on l.oid = p.prolang
            where n.nspname = @schema
            order by p.proname;";

        await using var conn = await _rt.OpenReadAsync(CancellationToken.None);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("schema", sch);
        var (rows, _) = await ReadAllAsync(cmd, null, CancellationToken.None);
        return rows.Select(r => new FunctionDto
        {
            Schema = sch,
            Name = Str(r["name"]!)!,
            Arguments = Str(r["arguments"]!) ?? "",
            ReturnType = Str(r["return_type"]!) ?? "",
            Kind = Str(r["kind"]!) ?? "function",
            Language = Str(r["language"]!) ?? ""
        }).ToList();
    }

    public async Task<RoutineSourceDto> GetFunctionSourceAsync(string schema, string name)
    {
        // Toma la primera coincidencia por nombre (puede haber overloads).
        const string sql = @"
            select pg_get_functiondef(p.oid) as def
            from pg_proc p join pg_namespace n on n.oid = p.pronamespace
            where n.nspname = @schema and p.proname = @name
            limit 1;";
        await using var conn = await _rt.OpenReadAsync(CancellationToken.None);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("name", name);
        var def = await cmd.ExecuteScalarAsync();
        return new RoutineSourceDto { Schema = schema, Name = name, Definition = def as string ?? string.Empty };
    }
}
