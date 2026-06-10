namespace ZooSanMarino.Application.DTOs
{
    // =====================================================
    // DTOs PRINCIPALES
    // =====================================================

    public class SchemaDto
    {
        public string Name { get; set; } = string.Empty;
        public int Tables { get; set; }
        public string? Description { get; set; }
    }

    public class TableDto
    {
        public string Schema { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public long Rows { get; set; }
        public string? Size { get; set; }
        public string? Description { get; set; }
    }

    public class ColumnDto
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public string? Default { get; set; }
        public bool IsPrimaryKey { get; set; }
        public int? MaxLength { get; set; }
        public int? Precision { get; set; }
        public int? Scale { get; set; }
        public bool IsIdentity { get; set; }
        public string? Comment { get; set; }
    }

    public class IndexDto
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public List<string> Columns { get; set; } = new();
        public bool IsUnique { get; set; }
        public bool IsPrimary { get; set; }
    }

    public class ForeignKeyDto
    {
        public string Name { get; set; } = string.Empty;
        public string Column { get; set; } = string.Empty;
        public string ReferencedTable { get; set; } = string.Empty;
        public string ReferencedColumn { get; set; } = string.Empty;
        public string OnDelete { get; set; } = string.Empty;
        public string OnUpdate { get; set; } = string.Empty;
    }

    public class TableStatsDto
    {
        public string TableName { get; set; } = string.Empty;
        public string SchemaName { get; set; } = string.Empty;
        public long RowCount { get; set; }
        public string TableSize { get; set; } = string.Empty;
        public string IndexSize { get; set; } = string.Empty;
        public string TotalSize { get; set; } = string.Empty;
        public DateTime? LastAnalyzed { get; set; }
    }

    public class QueryPageDto
    {
        public List<Dictionary<string, object?>> Rows { get; set; } = new();
        public long Count { get; set; }
        public int Limit { get; set; }
        public int Offset { get; set; }
        public List<string>? Columns { get; set; }
        public long? ExecutionTime { get; set; }
    }

    public class QueryResultDto
    {
        public bool Success { get; set; }
        public QueryPageDto? Data { get; set; }
        public string? Error { get; set; }
        public int? AffectedRows { get; set; }
        public long? ExecutionTime { get; set; }
    }

    public class TableDetailsDto
    {
        public TableDto Table { get; set; } = new();
        public List<ColumnDto> Columns { get; set; } = new();
        public List<IndexDto> Indexes { get; set; } = new();
        public List<ForeignKeyDto> ForeignKeys { get; set; } = new();
        public TableStatsDto Stats { get; set; } = new();
    }

    // =====================================================
    // DTOs PARA REQUESTS
    // =====================================================

    public class CreateTableRequest
    {
        public string Schema { get; set; } = string.Empty;
        public string Table { get; set; } = string.Empty;
        public List<CreateColumnRequest> Columns { get; set; } = new();
        public List<string>? PrimaryKey { get; set; }
        public List<List<string>>? Uniques { get; set; }
        public List<CreateIndexRequest>? Indexes { get; set; }
        public List<CreateForeignKeyRequest>? ForeignKeys { get; set; }
    }

    public class CreateColumnRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool Nullable { get; set; }
        public string? Default { get; set; }
        public string? Identity { get; set; } // "always", "by_default", null
        public int? MaxLength { get; set; }
        public int? Precision { get; set; }
        public int? Scale { get; set; }
        public string? Comment { get; set; }
    }

    public class CreateIndexRequest
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Columns { get; set; } = new();
        public bool Unique { get; set; }
    }

    public class CreateForeignKeyRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Column { get; set; } = string.Empty;
        public string ReferencedTable { get; set; } = string.Empty;
        public string ReferencedColumn { get; set; } = string.Empty;
        public string? OnDelete { get; set; }
        public string? OnUpdate { get; set; }
    }

    public class AddColumnRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool Nullable { get; set; }
        public string? Default { get; set; }
        public int? MaxLength { get; set; }
        public int? Precision { get; set; }
        public int? Scale { get; set; }
        public string? Comment { get; set; }
    }

    public class AlterColumnRequest
    {
        public string? NewType { get; set; }
        public bool? SetNotNull { get; set; }
        public bool? DropNotNull { get; set; }
        public string? SetDefault { get; set; }
        public bool? DropDefault { get; set; }
        public int? NewMaxLength { get; set; }
        public int? NewPrecision { get; set; }
        public int? NewScale { get; set; }
        public string? NewComment { get; set; }
    }

    public class SelectQueryRequest
    {
        public string Sql { get; set; } = string.Empty;
        public Dictionary<string, object>? Params { get; set; }
        public int Limit { get; set; } = 100;
        public int Offset { get; set; } = 0;
    }

    public class ExecuteQueryRequest
    {
        public string Sql { get; set; } = string.Empty;
        public Dictionary<string, object>? Params { get; set; }
    }

    public class SqlValidationRequest
    {
        public string Sql { get; set; } = string.Empty;
    }

    public class SqlValidationResult
    {
        public bool Valid { get; set; }
        public string? Error { get; set; }
    }

    // =====================================================
    // DTOs ADICIONALES PARA FUNCIONALIDADES COMPLETAS
    // =====================================================

    public class InsertDataRequest
    {
        public List<Dictionary<string, object?>> Rows { get; set; } = new();
    }

    public class UpdateDataRequest
    {
        public Dictionary<string, object?> Data { get; set; } = new();
        public Dictionary<string, object?> Where { get; set; } = new();
    }

    public class DeleteDataRequest
    {
        public Dictionary<string, object?> Where { get; set; } = new();
    }

    public class TableDependenciesDto
    {
        public List<TableReferenceDto> Dependencies { get; set; } = new();
        public List<TableReferenceDto> Dependents { get; set; } = new();
    }

    public class TableReferenceDto
    {
        public string Table { get; set; } = string.Empty;
        public string Schema { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    public class DatabaseAnalysisDto
    {
        public int TotalSchemas { get; set; }
        public int TotalTables { get; set; }
        public long TotalRows { get; set; }
        public string TotalSize { get; set; } = string.Empty;
        public List<SchemaAnalysisDto> SchemaAnalysis { get; set; } = new();
        public List<TableAnalysisDto> LargestTables { get; set; } = new();
        public List<TableAnalysisDto> MostIndexedTables { get; set; } = new();
    }

    public class SchemaAnalysisDto
    {
        public string SchemaName { get; set; } = string.Empty;
        public int TableCount { get; set; }
        public long TotalRows { get; set; }
        public string TotalSize { get; set; } = string.Empty;
    }

    public class TableAnalysisDto
    {
        public string SchemaName { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public long RowCount { get; set; }
        public string Size { get; set; } = string.Empty;
        public int IndexCount { get; set; }
        public int ForeignKeyCount { get; set; }
    }

    // =====================================================
    // VISTAS Y FUNCIONES
    // =====================================================

    public class ViewDto
    {
        public string Schema { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool Materialized { get; set; }
    }

    public class FunctionDto
    {
        public string Schema { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string ReturnType { get; set; } = string.Empty;
        public string Kind { get; set; } = "function"; // function | procedure | aggregate | window
        public string Language { get; set; } = string.Empty;
    }

    public class RoutineSourceDto
    {
        public string Schema { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Definition { get; set; } = string.Empty;
    }

    public class CreateViewRequest
    {
        public string Schema { get; set; } = "public";
        public string Name { get; set; } = string.Empty;
        public string SelectSql { get; set; } = string.Empty;
        public bool OrReplace { get; set; } = true;
        public bool Materialized { get; set; }
    }

    /// <summary>Cuerpo SQL completo de CREATE OR REPLACE FUNCTION/PROCEDURE (admin).</summary>
    public class CreateRoutineRequest
    {
        public string Definition { get; set; } = string.Empty;
        public bool Confirm { get; set; }
    }

    public class ExecuteSqlRequest
    {
        public string Sql { get; set; } = string.Empty;
        public Dictionary<string, object?>? Params { get; set; }
        /// <summary>Confirmación explícita para operaciones marcadas como destructivas.</summary>
        public bool Confirm { get; set; }
    }

    public class SqlClassificationDto
    {
        public string Kind { get; set; } = string.Empty;
        public bool IsReadOnly { get; set; }
        public bool RequiresConfirmation { get; set; }
        public string? Reason { get; set; }
    }

    // =====================================================
    // PERMISOS POR OBJETO (GRANTS)
    // =====================================================

    public class ObjectGrantDto
    {
        public long Id { get; set; }
        public Guid UserId { get; set; }
        public int CompanyId { get; set; }
        public string Schema { get; set; } = "public";
        public string Object { get; set; } = string.Empty;
        public string AccessLevel { get; set; } = "read"; // read | write
        public Guid GrantedByUserId { get; set; }
        public DateTime GrantedAtUtc { get; set; }
    }

    public class GrantRequest
    {
        public Guid UserId { get; set; }
        public string Schema { get; set; } = "public";
        public string Object { get; set; } = string.Empty;
        public string AccessLevel { get; set; } = "read"; // read | write
    }

    public class MyAccessItemDto
    {
        public string Schema { get; set; } = "public";
        public string Object { get; set; } = string.Empty;
        public string AccessLevel { get; set; } = "read";
    }

    public class MyAccessDto
    {
        public bool IsAdmin { get; set; }
        public List<MyAccessItemDto> Objects { get; set; } = new();
    }

    // =====================================================
    // CONCURRENCIA / HILOS
    // =====================================================

    public class ActivitySessionDto
    {
        public int Pid { get; set; }
        public string? UserName { get; set; }
        public string? ApplicationName { get; set; }
        public string? ClientAddr { get; set; }
        public string? State { get; set; }
        public string? WaitEventType { get; set; }
        public string? WaitEvent { get; set; }
        public string? Query { get; set; }
        public DateTime? QueryStart { get; set; }
        public DateTime? XactStart { get; set; }
        public DateTime? BackendStart { get; set; }
        public List<int> BlockedBy { get; set; } = new();
        public bool IsCurrentSession { get; set; }
    }

    public class ActivitySnapshotDto
    {
        public int MaxConnections { get; set; }
        public int TotalConnections { get; set; }
        public int ActiveConnections { get; set; }
        public int IdleConnections { get; set; }
        public int IdleInTransaction { get; set; }
        public int BlockedConnections { get; set; }
        public List<ActivitySessionDto> Sessions { get; set; } = new();
    }

    public class PoolStatsDto
    {
        public int PoolMinSize { get; set; }
        public int PoolMaxSize { get; set; }
        public int DbStudioConnections { get; set; }
        public string ApplicationName { get; set; } = string.Empty;
    }

    public class LockDto
    {
        public int Pid { get; set; }
        public string? LockType { get; set; }
        public string? Mode { get; set; }
        public bool Granted { get; set; }
        public string? Relation { get; set; }
        public string? Query { get; set; }
    }

    public class ConcurrencyActionRequest
    {
        public int Pid { get; set; }
    }
}

