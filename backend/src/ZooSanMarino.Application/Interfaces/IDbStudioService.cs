using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces
{
    public interface IDbStudioService
    {
        // ===================== ESQUEMAS =====================
        Task<IEnumerable<SchemaDto>> GetSchemasAsync();
        
        // ===================== TABLAS =====================
        Task<IEnumerable<TableDto>> GetTablesAsync(string? schema = null);
        Task<TableDetailsDto> GetTableDetailsAsync(string schema, string table);
        Task<IEnumerable<ColumnDto>> GetTableColumnsAsync(string schema, string table);
        Task<IEnumerable<IndexDto>> GetTableIndexesAsync(string schema, string table);
        Task<IEnumerable<ForeignKeyDto>> GetTableForeignKeysAsync(string schema, string table);
        Task<TableStatsDto> GetTableStatsAsync(string schema, string table);
        Task<QueryPageDto> PreviewTableAsync(string schema, string table, int limit = 50, int offset = 0);
        
        // ===================== CONSULTAS SQL =====================
        Task<QueryPageDto> ExecuteSelectQueryAsync(SelectQueryRequest request);
        Task<QueryResultDto> ExecuteQueryAsync(ExecuteQueryRequest request);
        
        // ===================== CREACIÓN Y MODIFICACIÓN =====================
        Task CreateTableAsync(CreateTableRequest request);
        Task DropTableAsync(string schema, string table, bool cascade = false);
        Task AddColumnAsync(string schema, string table, AddColumnRequest request);
        Task AlterColumnAsync(string schema, string table, string column, AlterColumnRequest request);
        Task DropColumnAsync(string schema, string table, string column);
        
        // ===================== ÍNDICES =====================
        Task CreateIndexAsync(string schema, string table, CreateIndexRequest request);
        Task DropIndexAsync(string schema, string table, string indexName);
        
        // ===================== CLAVES FORÁNEAS =====================
        Task CreateForeignKeyAsync(string schema, string table, CreateForeignKeyRequest request);
        Task DropForeignKeyAsync(string schema, string table, string fkName);
        
        // ===================== DATOS =====================
        Task InsertDataAsync(string schema, string table, List<Dictionary<string, object>> data);
        Task UpdateDataAsync(string schema, string table, Dictionary<string, object> data, Dictionary<string, object> where);
        Task DeleteDataAsync(string schema, string table, Dictionary<string, object> where);
        
        // ===================== UTILIDADES =====================
        Task<IEnumerable<string>> GetDataTypesAsync();
        Task<SqlValidationResult> ValidateSqlAsync(string sql);
        Task<byte[]> ExportTableAsync(string schema, string table, string format = "sql");
        Task ImportTableAsync(string schema, string table, byte[] fileContent, string format = "csv");
        
        // ===================== ANÁLISIS Y DEPENDENCIAS =====================
        Task<TableDependenciesDto> GetTableDependenciesAsync(string schema, string table);
        Task<DatabaseAnalysisDto> AnalyzeDatabaseAsync();
        Task<byte[]> ExportSchemaAsync(string schema);
    }
}

