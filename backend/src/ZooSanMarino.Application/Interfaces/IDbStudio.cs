using ZooSanMarino.Application.DTOs.DbStudio;

namespace ZooSanMarino.Application.Interfaces;

public interface IDbSchemaService
{
    Task CreateTableAsync(CreateTableDto dto, string actorUserId, CancellationToken ct);
    Task AddColumnAsync(string schema, string table, AddColumnDto dto, string actorUserId, CancellationToken ct);
    Task AlterColumnAsync(string schema, string table, string column, AlterColumnDto dto, string actorUserId, CancellationToken ct);

}

public interface IReadOnlyQueryService
{
    Task<QueryPageDto> RunSafeSelectAsync(SelectQueryDto dto, string actorUserId, CancellationToken ct);
}
