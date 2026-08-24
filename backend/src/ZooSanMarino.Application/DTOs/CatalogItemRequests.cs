using System.Text.Json;

namespace ZooSanMarino.Application.DTOs;

public class CatalogItemCreateRequest
{
    /// <summary>Código ERP. Opcional: se puede crear el ítem sin código y completarlo después.</summary>
    public string? Codigo { get; set; }
    public string Nombre { get; set; } = null!;
    public string ItemType { get; set; } = "alimento"; // Tipo de item: alimento, vacuna, medicamento, etc.
    public JsonDocument? Metadata { get; set; }  // si viene null, guardamos {}
    public bool Activo { get; set; } = true;
}

public class CatalogItemUpdateRequest
{
    public string Nombre { get; set; } = null!;
    public string? ItemType { get; set; } // Opcional, si no se envía se mantiene el actual
    public JsonDocument? Metadata { get; set; }  // si viene null, conservamos la actual
    public bool Activo { get; set; } = true;
    /// <summary>
    /// Código ERP a asignar SOLO si el ítem todavía no tiene uno. Una vez que el ítem tiene código,
    /// es clave natural: <c>CatalogItemService.UpdateAsync</c> ignora cualquier valor que llegue acá
    /// y no lo pisa. Sirve para completar después un ítem sembrado sin código (p. ej. mientras se
    /// espera que el cliente confirme el código real).
    /// </summary>
    public string? Codigo { get; set; }
}

/// <summary>Wrapper para respuestas paginadas.</summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
