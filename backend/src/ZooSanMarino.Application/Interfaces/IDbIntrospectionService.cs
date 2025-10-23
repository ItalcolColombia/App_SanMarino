using ZooSanMarino.Application.DTOs.DbStudio;

namespace ZooSanMarino.Application.Interfaces;

public interface IDbIntrospectionService
{
    Task<IReadOnlyList<SchemaDto>> GetSchemasAsync(CancellationToken ct);
    Task<IReadOnlyList<TableDto>>  GetTablesAsync(string schema, CancellationToken ct);
    Task<IReadOnlyList<ColumnDto>> GetColumnsAsync(string schema, string table, CancellationToken ct);
    Task<QueryPageDto>             PreviewAsync(string schema, string table, int limit, int offset, CancellationToken ct);
}
