// src/ZooSanMarino.Application/DTOs/LoteHuevoItemDtos.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Un tipo de huevo que el lote declara producir (F7.3). Trae el detalle del ítem del catálogo para
/// que el front pinte la fila fija del seguimiento diario sin recargar el catálogo entero.
/// </summary>
/// <param name="Id">Id de la fila de <c>lote_huevo_items</c>.</param>
/// <param name="LoteId">Lote MAESTRO (<c>lotes.lote_id</c>).</param>
/// <param name="CatalogItemId"><c>catalogo_items.id</c> del ítem de huevo.</param>
/// <param name="Codigo">Código ERP del ítem. Puede venir vacío: el código es opcional en el catálogo.</param>
/// <param name="Nombre">Nombre del ítem.</param>
/// <param name="TipoHuevo">Categoría comercial (<c>Primera</c> / <c>Pnc</c>) leída del metadata del catálogo.</param>
/// <param name="Um">Unidad de medida (<c>UND</c> / <c>KIL</c>). Decide si la cantidad admite decimales.</param>
/// <param name="PrimeraPostura">El ítem representa «huevo de primera postura» y está sujeto a la vigencia por semana (F7.4).</param>
/// <param name="ItemActivo">El ítem sigue activo en el catálogo. Un ítem dado de baja se conserva declarado pero se marca.</param>
/// <param name="Activo">La declaración sigue vigente para el lote.</param>
public sealed record LoteHuevoItemDto(
    int Id,
    int LoteId,
    int CatalogItemId,
    string? Codigo,
    string Nombre,
    string? TipoHuevo,
    string? Um,
    bool PrimeraPostura,
    bool ItemActivo,
    bool Activo
);

/// <summary>
/// Reemplaza el conjunto de tipos de huevo del lote. Es un SET completo: lo que no venga se
/// desactiva. Lista vacía = el lote no declara ninguno y —fail-closed— no podrá clasificar huevos.
/// </summary>
public sealed record AsignarHuevoItemsDto(IReadOnlyList<int> CatalogItemIds);
