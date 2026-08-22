using System.Text.Json;

namespace ZooSanMarino.Domain.Entities;

public class CatalogItem
{
    public int Id { get; set; }
    /// <summary>
    /// Código ERP del ítem. <b>Opcional</b>: un ítem puede nacer sin código (p. ej. sembrado antes
    /// de que el cliente confirme el código real) y se completa después, una sola vez — ver
    /// <c>CatalogItemService.UpdateAsync</c>, que lo bloquea apenas deja de estar vacío (clave
    /// natural). Varios ítems con código <c>null</c> conviven sin problema: el índice único
    /// <c>ux_catalogo_items_codigo_company_pais</c> no considera duplicados dos <c>NULL</c>.
    /// </summary>
    public string? Codigo { get; set; }
    public string Nombre { get; set; } = null!;
    public string ItemType { get; set; } = "alimento"; // Tipo de item: alimento, vacuna, medicamento, etc.
    public JsonDocument Metadata { get; set; } = JsonDocument.Parse("{}");
    public bool Activo { get; set; } = true;
    
    // Campos para filtrado por empresa y país
    public int CompanyId { get; set; }
    public int PaisId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
