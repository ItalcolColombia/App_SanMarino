namespace ZooSanMarino.Application.DTOs.Common;

public sealed class PagedResult<T>
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long Total { get; init; }
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
}
