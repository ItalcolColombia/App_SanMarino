// src/ZooSanMarino.Application/DTOs/Produccion/HuevoItemSeguimientoDto.cs
namespace ZooSanMarino.Application.DTOs.Produccion;

/// <summary>
/// Una fila de la clasificación de huevos POR ÍTEM del catálogo de inventario
/// (empresas con <c>companies.clasificacion_huevo_por_items = true</c>, ej. Santa Reyes).
/// Reemplaza a las 11 columnas fijas (<c>huevo_limpio</c>…<c>huevo_otro</c>): el desglose se
/// persiste en <c>seguimiento_diario_produccion.metadata → huevoItems</c> y la SUMA de
/// <see cref="Cantidad"/> va a <c>huevo_tot</c> (lo que mantiene vivos espejo, trigger,
/// saldos e indicadores, que solo consumen el total).
/// </summary>
/// <param name="CatalogItemId">Id del ítem en <c>catalogo_items</c> (debe ser de la empresa de la granja del lote y tener <c>item_type = 'huevo'</c>).</param>
/// <param name="Codigo">Código ERP del ítem. Solo informativo (se persiste para pintar el desglose sin recargar el catálogo).</param>
/// <param name="Nombre">Nombre del ítem resuelto por el front. Solo informativo.</param>
/// <param name="TipoHuevo">Categoría comercial del ítem: <c>Primera</c> | <c>Pnc</c>. Solo informativo (la fuente de verdad es el metadata del catálogo).</param>
/// <param name="Cantidad">Cantidad de huevos del ítem (unidades o kg según <paramref name="Um"/>). No puede ser negativa.</param>
/// <param name="Um">Unidad de medida del ítem (<c>UND</c>, <c>KIL</c>…). Solo informativo.</param>
public record HuevoItemSeguimientoDto(
    int CatalogItemId,
    string? Codigo = null,
    string? Nombre = null,
    string? TipoHuevo = null,
    int Cantidad = 0,
    string? Um = null
);
