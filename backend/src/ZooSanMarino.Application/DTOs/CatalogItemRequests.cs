using System.Text.Json;

namespace ZooSanMarino.Application.DTOs;

public class CatalogItemCreateRequest
{
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public JsonDocument? Metadata { get; set; }  // si viene null, guardamos {}
    public bool Activo { get; set; } = true;
}

public class CatalogItemUpdateRequest
{
    public string Nombre { get; set; } = null!;
    public JsonDocument? Metadata { get; set; }  // si viene null, conservamos la actual
    public bool Activo { get; set; } = true;
}

/// <summary>Wrapper para respuestas paginadas.</summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
