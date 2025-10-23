using System.Text.Json;

namespace ZooSanMarino.Application.DTOs;

public class CatalogItemDto
{
    public int? Id { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public JsonDocument? Metadata { get; set; }  // opcional al crear/editar
    public bool Activo { get; set; } = true;
}
