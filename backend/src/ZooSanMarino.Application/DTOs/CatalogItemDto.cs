using System.Text.Json;

namespace ZooSanMarino.Application.DTOs;

public class CatalogItemDto
{
    public int? Id { get; set; }
    /// <summary>Código ERP. <c>null</c> = todavía sin asignar (ver <see cref="CatalogItemUpdateRequest.Codigo"/>).</summary>
    public string? Codigo { get; set; }
    public string Nombre { get; set; } = null!;
    public string ItemType { get; set; } = "alimento"; // Tipo de item: alimento, vacuna, medicamento, etc.
    public JsonDocument? Metadata { get; set; }  // opcional al crear/editar
    public bool Activo { get; set; } = true;
}
