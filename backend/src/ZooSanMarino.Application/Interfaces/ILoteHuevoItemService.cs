// src/ZooSanMarino.Application/Interfaces/ILoteHuevoItemService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// F7.3 — qué tipos de huevo produce un lote. El seguimiento diario de PRODUCCIÓN muestra una fila
/// fija por cada uno y rechaza cualquier ítem que no esté acá.
///
/// <para>
/// <b>Fail-closed:</b> un lote sin ítems declarados no puede clasificar huevos. No hay caída al
/// catálogo completo — es la decisión explícita del cliente.
/// </para>
/// </summary>
public interface ILoteHuevoItemService
{
    /// <summary>Tipos de huevo declarados por el lote, ordenados Primera → Pnc → resto.</summary>
    Task<IEnumerable<LoteHuevoItemDto>> GetByLoteAsync(int loteId, CancellationToken ct = default);

    /// <summary>
    /// Ítems de huevo elegibles: los del catálogo ACTIVO de la empresa dueña de la GRANJA del lote.
    /// Se resuelve por la granja y no por la empresa activa del token, igual que el gate de guardado
    /// (<c>ProduccionService.ValidarHuevoItemsAsync</c>) — si difirieran, el usuario podría declarar
    /// un ítem que después el guardado rechaza.
    /// </summary>
    Task<IEnumerable<LoteHuevoItemDto>> GetDisponiblesAsync(int loteId, CancellationToken ct = default);

    /// <summary>
    /// Los mismos ítems elegibles, pero para un lote que TODAVÍA NO EXISTE: el alta necesita
    /// ofrecer los tipos de huevo antes del POST, y ahí no hay <c>loteId</c> con el que resolver la
    /// empresa. Se resuelve por la granja que el usuario acaba de elegir en el formulario, que es
    /// el mismo dato del que después colgará el lote — y el mismo con el que valida el guardado del
    /// diario. Ninguno viene marcado (<c>Activo = false</c>): no hay declaración previa que marcar.
    /// </summary>
    Task<IEnumerable<LoteHuevoItemDto>> GetDisponiblesPorGranjaAsync(int granjaId, CancellationToken ct = default);

    /// <summary>Reemplaza el conjunto de tipos de huevo del lote. Lista vacía = ninguno.</summary>
    Task<IEnumerable<LoteHuevoItemDto>> AsignarAsync(int loteId, AsignarHuevoItemsDto dto, CancellationToken ct = default);
}
