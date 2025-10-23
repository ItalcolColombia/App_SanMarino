using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;
using System.Text;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services
{
    public class DbStudioService : IDbStudioService
    {
        private readonly ZooSanMarinoContext _context;
        private readonly ILogger<DbStudioService> _logger;

        public DbStudioService(ZooSanMarinoContext context, ILogger<DbStudioService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ===================== ESQUEMAS =====================
        public async Task<IEnumerable<SchemaDto>> GetSchemasAsync()
        {
            var sql = @"
                SELECT 
                    schema_name as name,
                    COALESCE(table_count, 0) as tables
                FROM information_schema.schemata s
                LEFT JOIN (
                    SELECT 
                        table_schema,
                        COUNT(*) as table_count
                    FROM information_schema.tables 
                    WHERE table_type = 'BASE TABLE'
                    GROUP BY table_schema
                ) t ON s.schema_name = t.table_schema
                WHERE s.schema_name NOT IN ('information_schema', 'pg_catalog', 'pg_toast')
                ORDER BY s.schema_name";

            var schemas = new List<SchemaDto>();
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            
            await _context.Database.OpenConnectionAsync();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                schemas.Add(new SchemaDto
                {
                    Name = reader.GetString("name"),
                    Tables = reader.GetInt32("tables")
                });
            }

            return schemas;
        }

        // ===================== TABLAS =====================
        public async Task<IEnumerable<TableDto>> GetTablesAsync(string? schema = null)
        {
            try
            {
                // Consulta mejorada que incluye información real de filas y tamaño
                var sql = @"
                    SELECT 
                        t.table_schema as schema,
                        t.table_name as name,
                        t.table_type as kind,
                        COALESCE(s.n_tup_ins - s.n_tup_del, 0) as rows,
                        pg_size_pretty(pg_total_relation_size(c.oid)) as size
                    FROM information_schema.tables t
                    LEFT JOIN pg_class c ON c.relname = t.table_name
                    LEFT JOIN pg_namespace n ON n.oid = c.relnamespace AND n.nspname = t.table_schema
                    LEFT JOIN pg_stat_user_tables s ON s.schemaname = t.table_schema AND s.relname = t.table_name
                    WHERE t.table_type IN ('BASE TABLE', 'VIEW')
                    AND t.table_schema = @schema
                    ORDER BY t.table_schema, t.table_name";

                var tables = new List<TableDto>();
                using var command = _context.Database.GetDbConnection().CreateCommand();
                command.CommandText = sql;
                
                var param = command.CreateParameter();
                param.ParameterName = "@schema";
                param.Value = schema ?? "public";
                command.Parameters.Add(param);

                await _context.Database.OpenConnectionAsync();
                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    tables.Add(new TableDto
                    {
                        Schema = reader.GetString("schema"),
                        Name = reader.GetString("name"),
                        Kind = reader.GetString("kind"),
                        Rows = reader.IsDBNull("rows") ? 0 : reader.GetInt64("rows"),
                        Size = reader.IsDBNull("size") ? "N/A" : reader.GetString("size")
                    });
                }

                return tables;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GetTablesAsync para schema: {Schema}", schema);
                throw;
            }
        }

        public async Task<TableDetailsDto> GetTableDetailsAsync(string schema, string table)
        {
            var tableInfo = await GetTablesAsync(schema);
            var tableDto = tableInfo.FirstOrDefault(t => t.Name == table) ?? new TableDto { Schema = schema, Name = table };
            
            var columns = await GetTableColumnsAsync(schema, table);
            var indexes = await GetTableIndexesAsync(schema, table);
            var foreignKeys = await GetTableForeignKeysAsync(schema, table);
            var stats = await GetTableStatsAsync(schema, table);

            return new TableDetailsDto
            {
                Table = tableDto,
                Columns = columns.ToList(),
                Indexes = indexes.ToList(),
                ForeignKeys = foreignKeys.ToList(),
                Stats = stats
            };
        }

        public async Task<IEnumerable<ColumnDto>> GetTableColumnsAsync(string schema, string table)
        {
            var sql = @"
                SELECT 
                    c.column_name as name,
                    c.data_type as data_type,
                    c.is_nullable = 'YES' as is_nullable,
                    c.column_default as default_value,
                    CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END as is_primary_key,
                    c.character_maximum_length as max_length,
                    c.numeric_precision as precision,
                    c.numeric_scale as scale,
                    CASE WHEN c.is_identity = 'YES' THEN true ELSE false END as is_identity,
                    col_description(pgc.oid, c.ordinal_position) as comment
                FROM information_schema.columns c
                LEFT JOIN pg_class pgc ON pgc.relname = c.table_name
                LEFT JOIN pg_namespace pgn ON pgn.oid = pgc.relnamespace AND pgn.nspname = c.table_schema
                LEFT JOIN (
                    SELECT ku.table_schema, ku.table_name, ku.column_name
                    FROM information_schema.table_constraints tc
                    JOIN information_schema.key_column_usage ku ON tc.constraint_name = ku.constraint_name
                    WHERE tc.constraint_type = 'PRIMARY KEY'
                ) pk ON pk.table_schema = c.table_schema AND pk.table_name = c.table_name AND pk.column_name = c.column_name
                WHERE c.table_schema = @schema AND c.table_name = @table
                ORDER BY c.ordinal_position";

            var columns = new List<ColumnDto>();
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            
            var schemaParam = command.CreateParameter();
            schemaParam.ParameterName = "@schema";
            schemaParam.Value = schema;
            command.Parameters.Add(schemaParam);
            
            var tableParam = command.CreateParameter();
            tableParam.ParameterName = "@table";
            tableParam.Value = table;
            command.Parameters.Add(tableParam);

            await _context.Database.OpenConnectionAsync();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                columns.Add(new ColumnDto
                {
                    Name = reader.GetString("name"),
                    DataType = reader.GetString("data_type"),
                    IsNullable = reader.GetBoolean("is_nullable"),
                    Default = reader.IsDBNull("default_value") ? null : reader.GetString("default_value"),
                    IsPrimaryKey = reader.GetBoolean("is_primary_key"),
                    MaxLength = reader.IsDBNull("max_length") ? null : reader.GetInt32("max_length"),
                    Precision = reader.IsDBNull("precision") ? null : reader.GetInt32("precision"),
                    Scale = reader.IsDBNull("scale") ? null : reader.GetInt32("scale"),
                    IsIdentity = reader.GetBoolean("is_identity"),
                    Comment = reader.IsDBNull("comment") ? null : reader.GetString("comment")
                });
            }

            return columns;
        }

        public async Task<IEnumerable<IndexDto>> GetTableIndexesAsync(string schema, string table)
        {
            var sql = @"
                SELECT 
                    i.indexname as name,
                    i.indexdef as definition,
                    i.indexdef LIKE '%UNIQUE%' as is_unique,
                    i.indexdef LIKE '%PRIMARY%' as is_primary,
                    array_agg(a.attname ORDER BY a.attnum) as columns
                FROM pg_indexes i
                JOIN pg_class c ON c.relname = i.tablename
                JOIN pg_namespace n ON n.oid = c.relnamespace AND n.nspname = i.schemaname
                JOIN pg_index idx ON idx.indexrelid = (i.schemaname||'.'||i.indexname)::regclass
                JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum = ANY(idx.indkey)
                WHERE i.schemaname = @schema AND i.tablename = @table
                GROUP BY i.indexname, i.indexdef
                ORDER BY i.indexname";

            var indexes = new List<IndexDto>();
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            
            var schemaParam = command.CreateParameter();
            schemaParam.ParameterName = "@schema";
            schemaParam.Value = schema;
            command.Parameters.Add(schemaParam);
            
            var tableParam = command.CreateParameter();
            tableParam.ParameterName = "@table";
            tableParam.Value = table;
            command.Parameters.Add(tableParam);

            await _context.Database.OpenConnectionAsync();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var columns = new List<string>();
                if (!reader.IsDBNull("columns"))
                {
                    var columnArray = (string[])reader.GetValue("columns");
                    columns.AddRange(columnArray);
                }

                indexes.Add(new IndexDto
                {
                    Name = reader.GetString("name"),
                    Type = "btree", // PostgreSQL default
                    Columns = columns,
                    IsUnique = reader.GetBoolean("is_unique"),
                    IsPrimary = reader.GetBoolean("is_primary")
                });
            }

            return indexes;
        }

        public async Task<IEnumerable<ForeignKeyDto>> GetTableForeignKeysAsync(string schema, string table)
        {
            var sql = @"
                SELECT 
                    tc.constraint_name as name,
                    kcu.column_name as column_name,
                    ccu.table_schema as foreign_table_schema,
                    ccu.table_name as foreign_table_name,
                    ccu.column_name as foreign_column_name,
                    rc.delete_rule as on_delete,
                    rc.update_rule as on_update
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name
                JOIN information_schema.constraint_column_usage ccu ON ccu.constraint_name = tc.constraint_name
                JOIN information_schema.referential_constraints rc ON tc.constraint_name = rc.constraint_name
                WHERE tc.constraint_type = 'FOREIGN KEY' 
                AND tc.table_schema = @schema 
                AND tc.table_name = @table";

            var foreignKeys = new List<ForeignKeyDto>();
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            
            var schemaParam = command.CreateParameter();
            schemaParam.ParameterName = "@schema";
            schemaParam.Value = schema;
            command.Parameters.Add(schemaParam);
            
            var tableParam = command.CreateParameter();
            tableParam.ParameterName = "@table";
            tableParam.Value = table;
            command.Parameters.Add(tableParam);

            await _context.Database.OpenConnectionAsync();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                foreignKeys.Add(new ForeignKeyDto
                {
                    Name = reader.GetString("name"),
                    Column = reader.GetString("column_name"),
                    ReferencedTable = reader.GetString("foreign_table_name"),
                    ReferencedColumn = reader.GetString("foreign_column_name"),
                    OnDelete = reader.GetString("on_delete"),
                    OnUpdate = reader.GetString("on_update")
                });
            }

            return foreignKeys;
        }

        public async Task<TableStatsDto> GetTableStatsAsync(string schema, string table)
        {
            var sql = @"
                SELECT 
                    @table as table_name,
                    @schema as schema_name,
                    COALESCE(s.n_tup_ins + s.n_tup_upd + s.n_tup_del, 0) as row_count,
                    pg_size_pretty(pg_relation_size(c.oid)) as table_size,
                    pg_size_pretty(pg_indexes_size(c.oid)) as index_size,
                    pg_size_pretty(pg_total_relation_size(c.oid)) as total_size,
                    s.last_analyze as last_analyzed
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                LEFT JOIN pg_stat_user_tables s ON s.relname = c.relname AND s.schemaname = n.nspname
                WHERE n.nspname = @schema AND c.relname = @table";

            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            
            var schemaParam = command.CreateParameter();
            schemaParam.ParameterName = "@schema";
            schemaParam.Value = schema;
            command.Parameters.Add(schemaParam);
            
            var tableParam = command.CreateParameter();
            tableParam.ParameterName = "@table";
            tableParam.Value = table;
            command.Parameters.Add(tableParam);

            await _context.Database.OpenConnectionAsync();
            using var reader = await command.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                return new TableStatsDto
                {
                    TableName = reader.GetString("table_name"),
                    SchemaName = reader.GetString("schema_name"),
                    RowCount = reader.GetInt64("row_count"),
                    TableSize = reader.IsDBNull("table_size") ? "0 B" : reader.GetString("table_size"),
                    IndexSize = reader.IsDBNull("index_size") ? "0 B" : reader.GetString("index_size"),
                    TotalSize = reader.IsDBNull("total_size") ? "0 B" : reader.GetString("total_size"),
                    LastAnalyzed = reader.IsDBNull("last_analyzed") ? null : reader.GetDateTime("last_analyzed")
                };
            }

            return new TableStatsDto { TableName = table, SchemaName = schema };
        }

        public async Task<QueryPageDto> PreviewTableAsync(string schema, string table, int limit = 50, int offset = 0)
        {
            var sql = $"SELECT * FROM \"{schema}\".\"{table}\" LIMIT @limit OFFSET @offset";
            return await ExecuteSelectQueryAsync(new SelectQueryRequest
            {
                Sql = sql,
                Params = new Dictionary<string, object>
                {
                    ["limit"] = limit,
                    ["offset"] = offset
                },
                Limit = limit,
                Offset = offset
            });
        }

        // ===================== CONSULTAS SQL =====================
        public async Task<QueryPageDto> ExecuteSelectQueryAsync(SelectQueryRequest request)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                var rows = new List<Dictionary<string, object?>>();
                var columns = new List<string>();
                
                using var command = _context.Database.GetDbConnection().CreateCommand();
                command.CommandText = request.Sql;
                
                if (request.Params != null)
                {
                    foreach (var param in request.Params)
                    {
                        var dbParam = command.CreateParameter();
                        dbParam.ParameterName = param.Key.StartsWith("@") ? param.Key : $"@{param.Key}";
                        dbParam.Value = param.Value ?? DBNull.Value;
                        command.Parameters.Add(dbParam);
                    }
                }

                await _context.Database.OpenConnectionAsync();
                using var reader = await command.ExecuteReaderAsync();
                
                // Obtener nombres de columnas
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columns.Add(reader.GetName(i));
                }

                // Leer datos
                while (await reader.ReadAsync() && rows.Count < request.Limit)
                {
                    var row = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var columnName = reader.GetName(i);
                        row[columnName] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }
                    rows.Add(row);
                }

                stopwatch.Stop();

                return new QueryPageDto
                {
                    Rows = rows,
                    Columns = columns,
                    Count = rows.Count,
                    Limit = request.Limit,
                    Offset = request.Offset,
                    ExecutionTime = stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ejecutando consulta SELECT: {Sql}", request.Sql);
                throw;
            }
        }

        public async Task<QueryResultDto> ExecuteQueryAsync(ExecuteQueryRequest request)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                using var command = _context.Database.GetDbConnection().CreateCommand();
                command.CommandText = request.Sql;
                
                if (request.Params != null)
                {
                    foreach (var param in request.Params)
                    {
                        var dbParam = command.CreateParameter();
                        dbParam.ParameterName = param.Key.StartsWith("@") ? param.Key : $"@{param.Key}";
                        dbParam.Value = param.Value ?? DBNull.Value;
                        command.Parameters.Add(dbParam);
                    }
                }

                await _context.Database.OpenConnectionAsync();
                
                var affectedRows = await command.ExecuteNonQueryAsync();
                stopwatch.Stop();

                return new QueryResultDto
                {
                    Success = true,
                    AffectedRows = affectedRows,
                    ExecutionTime = stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ejecutando consulta: {Sql}", request.Sql);
                stopwatch.Stop();
                
                return new QueryResultDto
                {
                    Success = false,
                    Error = ex.Message,
                    ExecutionTime = stopwatch.ElapsedMilliseconds
                };
            }
        }

        // ===================== CREACIÓN Y MODIFICACIÓN =====================
        public async Task CreateTableAsync(CreateTableRequest request)
        {
            var sql = new StringBuilder();
            sql.AppendLine($"CREATE TABLE \"{request.Schema}\".\"{request.Table}\" (");
            
            var columnDefinitions = new List<string>();
            foreach (var column in request.Columns)
            {
                var columnDef = $"\"{column.Name}\" {column.Type}";
                
                if (column.MaxLength.HasValue)
                    columnDef += $"({column.MaxLength})";
                else if (column.Precision.HasValue)
                {
                    columnDef += column.Scale.HasValue ? $"({column.Precision},{column.Scale})" : $"({column.Precision})";
                }
                
                if (!column.Nullable)
                    columnDef += " NOT NULL";
                
                if (!string.IsNullOrEmpty(column.Default))
                    columnDef += $" DEFAULT {column.Default}";
                
                if (column.Identity == "always")
                    columnDef += " GENERATED ALWAYS AS IDENTITY";
                else if (column.Identity == "by_default")
                    columnDef += " GENERATED BY DEFAULT AS IDENTITY";
                
                columnDefinitions.Add(columnDef);
            }
            
            sql.AppendLine(string.Join(",\n", columnDefinitions));
            
            // Primary Key
            if (request.PrimaryKey != null && request.PrimaryKey.Any())
            {
                sql.AppendLine($",\nPRIMARY KEY (\"{string.Join("\", \"", request.PrimaryKey)}\")");
            }
            
            // Unique constraints
            if (request.Uniques != null)
            {
                foreach (var unique in request.Uniques)
                {
                    sql.AppendLine($",\nUNIQUE (\"{string.Join("\", \"", unique)}\")");
                }
            }
            
            sql.AppendLine(");");
            
            // Indexes
            if (request.Indexes != null)
            {
                foreach (var index in request.Indexes)
                {
                    var uniqueKeyword = index.Unique ? "UNIQUE " : "";
                    sql.AppendLine($"CREATE {uniqueKeyword}INDEX \"{index.Name}\" ON \"{request.Schema}\".\"{request.Table}\" (\"{string.Join("\", \"", index.Columns)}\");");
                }
            }
            
            // Foreign Keys
            if (request.ForeignKeys != null)
            {
                foreach (var fk in request.ForeignKeys)
                {
                    var onDelete = string.IsNullOrEmpty(fk.OnDelete) ? "" : $" ON DELETE {fk.OnDelete}";
                    var onUpdate = string.IsNullOrEmpty(fk.OnUpdate) ? "" : $" ON UPDATE {fk.OnUpdate}";
                    sql.AppendLine($"ALTER TABLE \"{request.Schema}\".\"{request.Table}\" ADD CONSTRAINT \"{fk.Name}\" FOREIGN KEY (\"{fk.Column}\") REFERENCES \"{fk.ReferencedTable}\" (\"{fk.ReferencedColumn}\"){onDelete}{onUpdate};");
                }
            }

            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql.ToString();
            
            await _context.Database.OpenConnectionAsync();
            await command.ExecuteNonQueryAsync();
        }

        public async Task DropTableAsync(string schema, string table, bool cascade = false)
        {
            var cascadeKeyword = cascade ? " CASCADE" : "";
            var sql = $"DROP TABLE \"{schema}\".\"{table}\"{cascadeKeyword};";
            
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            
            await _context.Database.OpenConnectionAsync();
            await command.ExecuteNonQueryAsync();
        }

        public async Task AddColumnAsync(string schema, string table, AddColumnRequest request)
        {
            var columnDef = $"\"{request.Name}\" {request.Type}";
            
            if (request.MaxLength.HasValue)
                columnDef += $"({request.MaxLength})";
            else if (request.Precision.HasValue)
            {
                columnDef += request.Scale.HasValue ? $"({request.Precision},{request.Scale})" : $"({request.Precision})";
            }
            
            if (!request.Nullable)
                columnDef += " NOT NULL";
            
            if (!string.IsNullOrEmpty(request.Default))
                columnDef += $" DEFAULT {request.Default}";
            
            var sql = $"ALTER TABLE \"{schema}\".\"{table}\" ADD COLUMN {columnDef};";
            
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            
            await _context.Database.OpenConnectionAsync();
            await command.ExecuteNonQueryAsync();
        }

        public async Task AlterColumnAsync(string schema, string table, string column, AlterColumnRequest request)
        {
            var sql = new StringBuilder();
            
            if (!string.IsNullOrEmpty(request.NewType))
            {
                sql.AppendLine($"ALTER TABLE \"{schema}\".\"{table}\" ALTER COLUMN \"{column}\" TYPE {request.NewType};");
            }
            
            if (request.SetNotNull == true)
            {
                sql.AppendLine($"ALTER TABLE \"{schema}\".\"{table}\" ALTER COLUMN \"{column}\" SET NOT NULL;");
            }
            
            if (request.DropNotNull == true)
            {
                sql.AppendLine($"ALTER TABLE \"{schema}\".\"{table}\" ALTER COLUMN \"{column}\" DROP NOT NULL;");
            }
            
            if (!string.IsNullOrEmpty(request.SetDefault))
            {
                sql.AppendLine($"ALTER TABLE \"{schema}\".\"{table}\" ALTER COLUMN \"{column}\" SET DEFAULT {request.SetDefault};");
            }
            
            if (request.DropDefault == true)
            {
                sql.AppendLine($"ALTER TABLE \"{schema}\".\"{table}\" ALTER COLUMN \"{column}\" DROP DEFAULT;");
            }
            
            if (sql.Length > 0)
            {
                using var command = _context.Database.GetDbConnection().CreateCommand();
                command.CommandText = sql.ToString();
                
                await _context.Database.OpenConnectionAsync();
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task DropColumnAsync(string schema, string table, string column)
        {
            var sql = $"ALTER TABLE \"{schema}\".\"{table}\" DROP COLUMN \"{column}\";";
            
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            
            await _context.Database.OpenConnectionAsync();
            await command.ExecuteNonQueryAsync();
        }

        // ===================== MÉTODOS STUB (para implementar más tarde) =====================
        public Task CreateIndexAsync(string schema, string table, CreateIndexRequest request) => throw new NotImplementedException();
        public Task DropIndexAsync(string schema, string table, string indexName) => throw new NotImplementedException();
        public Task CreateForeignKeyAsync(string schema, string table, CreateForeignKeyRequest request) => throw new NotImplementedException();
        public Task DropForeignKeyAsync(string schema, string table, string fkName) => throw new NotImplementedException();
        public Task InsertDataAsync(string schema, string table, List<Dictionary<string, object>> data) => throw new NotImplementedException();
        public Task UpdateDataAsync(string schema, string table, Dictionary<string, object> data, Dictionary<string, object> where) => throw new NotImplementedException();
        public Task DeleteDataAsync(string schema, string table, Dictionary<string, object> where) => throw new NotImplementedException();
        public Task<IEnumerable<string>> GetDataTypesAsync() => throw new NotImplementedException();
        public Task<SqlValidationResult> ValidateSqlAsync(string sql) => throw new NotImplementedException();
        public Task<byte[]> ExportTableAsync(string schema, string table, string format = "sql") => throw new NotImplementedException();
        public Task ImportTableAsync(string schema, string table, byte[] fileContent, string format = "csv") => throw new NotImplementedException();
        
        // ===================== MÉTODOS ADICIONALES =====================
        public Task<TableDependenciesDto> GetTableDependenciesAsync(string schema, string table) => throw new NotImplementedException();
        public async Task<DatabaseAnalysisDto> AnalyzeDatabaseAsync()
        {
            var sql = @"
                SELECT 
                    COUNT(DISTINCT table_schema) as total_schemas,
                    COUNT(*) as total_tables,
                    SUM(COALESCE(s.n_tup_ins - s.n_tup_del, 0)) as total_rows,
                    pg_size_pretty(SUM(pg_total_relation_size(c.oid))) as total_size
                FROM information_schema.tables t
                LEFT JOIN pg_class c ON c.relname = t.table_name
                LEFT JOIN pg_namespace n ON n.oid = c.relnamespace AND n.nspname = t.table_schema
                LEFT JOIN pg_stat_user_tables s ON s.schemaname = t.table_schema AND s.relname = t.table_name
                WHERE t.table_type = 'BASE TABLE'
                AND t.table_schema NOT IN ('information_schema', 'pg_catalog', 'pg_toast')";

            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;

            await _context.Database.OpenConnectionAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new DatabaseAnalysisDto
                {
                    TotalSchemas = reader.GetInt32("total_schemas"),
                    TotalTables = reader.GetInt32("total_tables"),
                    TotalRows = reader.GetInt64("total_rows"),
                    TotalSize = reader.GetString("total_size"),
                    SchemaAnalysis = new List<SchemaAnalysisDto>(), // Implementar más tarde
                    LargestTables = new List<TableAnalysisDto>(), // Implementar más tarde
                    MostIndexedTables = new List<TableAnalysisDto>() // Implementar más tarde
                };
            }

            return new DatabaseAnalysisDto();
        }
        public Task<byte[]> ExportSchemaAsync(string schema) => throw new NotImplementedException();
    }
}
