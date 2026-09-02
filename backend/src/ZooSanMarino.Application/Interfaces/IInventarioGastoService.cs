// src/ZooSanMarino.Application/Interfaces/IInventarioGastoService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IInventarioGastoService
{
    Task<LoteReproductoraFilterDataDto> GetFilterDataAsync(CancellationToken ct = default);

    Task<List<InventarioGastoListItemDto>> SearchAsync(InventarioGastoSearchRequest req, CancellationToken ct = default);

    /// <summary>Filas del reporte (una por línea de consumo). NUNCA incluye gastos eliminados.</summary>
    Task<List<InventarioGastoExportRowDto>> ExportAsync(InventarioGastoSearchRequest req, CancellationToken ct = default);

    /// <summary>
    /// Existencias de TODOS los ítems no-alimento del catálogo (tengan o no consumo) con su saldo
    /// actual y lo consumido en el rango. Es la hoja de control de inventario del reporte.
    /// </summary>
    Task<List<InventarioGastoExistenciaDto>> GetExistenciasAsync(InventarioGastoExistenciasRequest req, CancellationToken ct = default);

    Task<InventarioGastoDto> GetByIdAsync(int id, CancellationToken ct = default);

    /// <param name="sinDescontarStock">
    /// H4 / F7 — <b>sólo para el push offline</b> (<c>SyncPushService</c>). Registra el gasto y sus
    /// líneas <b>sin mover el inventario</b>, y sin validar que alcance.
    ///
    /// <para>
    /// Existe porque una captura hecha sin red llega horas después, cuando el ítem ya puede no estar
    /// en la granja. El consumo <b>ocurrió físicamente</b>; lo que está atrasado es el número del
    /// sistema. Rechazar la captura mandaría a la bandeja un dato de campo real y lo dejaría varado,
    /// que es lo que §5.5 del plan madre prohíbe. La fila queda marcada <c>requiere_cuadre</c> y
    /// aparece en la bandeja para que una persona cargue el ingreso que falta.
    /// </para>
    ///
    /// <para>
    /// ⛔ <b>Nunca lo pase el controller.</b> Con red hay que ver el error: no existe el caso de un
    /// gasto que el usuario acepte guardar sabiendo que el stock no da. Y no descuenta "hasta donde
    /// alcance": un consumo parcial inventa un número que nadie capturó.
    /// </para>
    /// </param>
    Task<InventarioGastoDto> CreateAsync(CreateInventarioGastoRequest req, CancellationToken ct = default, bool sinDescontarStock = false);

    Task DeleteAsync(int id, string? motivo, CancellationToken ct = default);

    Task<List<string>> GetConceptosAsync(int? farmId = null, CancellationToken ct = default);

    /// <param name="siloId">
    /// Silo o bodega del que se va a consumir (empresas con inventario por silo): acota el saldo
    /// ofrecido al de ESE silo. Sin él, el saldo es el total de la granja —ya agregado, una fila por
    /// ítem—, que es lo que ven las empresas sin el flag.
    /// </param>
    Task<List<InventarioGastoItemStockDto>> GetItemsWithStockAsync(int farmId, string concepto, int? siloId = null, CancellationToken ct = default);
}

