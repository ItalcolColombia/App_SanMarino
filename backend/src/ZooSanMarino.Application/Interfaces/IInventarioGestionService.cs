// src/ZooSanMarino.Application/Interfaces/IInventarioGestionService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>Servicio de Gestión de Inventario (Panama/Ecuador): ingresos y traslados. Alimento → Granja/Núcleo/Galpón; otros → solo Granja.</summary>
public interface IInventarioGestionService
{
    Task<InventarioGestionFilterDataDto> GetFilterDataAsync(CancellationToken ct = default);

    /// <summary>Lotes en granjas asignadas y valores distintos de concepto/tipo/estado ya presentes en movimientos (histórico).</summary>
    Task<InventarioGestionHistoricoFiltrosDto> GetHistoricoFiltrosAsync(CancellationToken ct = default);

    Task<List<InventarioGestionStockDto>> GetStockAsync(
        int? farmId = null,
        string? nucleoId = null,
        string? galponId = null,
        string? itemType = null,
        string? search = null,
        CancellationToken ct = default);

    /// <summary>
    /// Silos y bodega ACTIVOS de una granja que pueden recibir o entregar un movimiento.
    /// <para>
    /// Con <paramref name="galponId"/> se acota a los silos que alimentan a ese galpón —el galpón
    /// filtra, no ubica— más la bodega de la granja, que se ofrece siempre. Devuelve vacío si la
    /// granja no es de la empresa activa (fail-closed).
    /// </para>
    /// </summary>
    Task<IEnumerable<InventarioGestionSiloDto>> GetSilosElegiblesAsync(
        int farmId,
        string? nucleoId = null,
        string? galponId = null,
        CancellationToken ct = default);

    Task<InventarioGestionStockDto> RegistrarIngresoAsync(InventarioGestionIngresoRequest req, CancellationToken ct = default);

    /// <summary>
    /// Id de un ingreso ya registrado con la <b>misma remisión</b>, ítem, ubicación y cantidad que el
    /// pedido, o <c>null</c> si no hay ninguno. Solo lee.
    ///
    /// <para>
    /// Lo consulta el controller para avisar antes de duplicar el alimento de un galpón. La consulta
    /// filtra en la BD, no en memoria: en una granja con miles de movimientos, traerlos para comparar
    /// en C# sería justamente lo que este repo tiene prohibido.
    /// </para>
    /// </summary>
    Task<int?> BuscarIngresoConMismaRemisionAsync(InventarioGestionIngresoRequest req, CancellationToken ct = default);

    /// <summary>
    /// Peor día de la tabla diaria del galpón <b>origen</b> desde la fecha del traslado en adelante, o
    /// <c>null</c> si ese galpón no tiene ningún día cargado. Solo lee.
    ///
    /// <para>
    /// Lo consulta el controller para avisar cuando una salida dejaría ese día en rojo. El saldo lo
    /// devuelve <c>fn_seguimiento_diario_engorde</c> —la dueña del número—: recalcularlo en C# sería
    /// una segunda fórmula para el mismo dato. Se pide el <b>mínimo</b> y no el saldo del día del
    /// movimiento porque la salida baja por igual todos los días siguientes.
    /// </para>
    /// </summary>
    Task<InventarioGestionSaldoMinimoDto?> BuscarPeorDiaDelGalponAsync(
        InventarioGestionTrasladoRequest req, CancellationToken ct = default);

    Task<(InventarioGestionStockDto Origen, InventarioGestionStockDto Destino)> RegistrarTrasladoAsync(InventarioGestionTrasladoRequest req, CancellationToken ct = default);

    /// <summary>Registra consumo (reduce stock). Para devolución usar RegistrarIngresoAsync.</summary>
    Task<InventarioGestionStockDto> RegistrarConsumoAsync(InventarioGestionConsumoRequest req, CancellationToken ct = default);

    /// <summary>
    /// Comprueba que haya stock suficiente para TODOS los ítems ANTES de persistir, y lanza con un
    /// mensaje que nombra el ítem y el faltante. No modifica nada.
    ///
    /// <para>
    /// Es el tercer validador de stock del sistema: ya existían el de nivel granja
    /// (<see cref="IColombiaInventarioConsumoService.ValidarStockConsumoAsync"/>) y el de modelo A
    /// (<see cref="IFarmInventoryConsumoService.ValidarStockConsumoAsync"/>); éste cubre el modelo B
    /// <b>con ubicación</b> (núcleo + galpón o silo), que es Ecuador y Panamá.
    /// </para>
    ///
    /// <para>
    /// <b>Se llama antes de guardar, no después.</b> Los seguimientos diarios de esos países
    /// persistían el registro primero y aplicaban el consumo dentro de un <c>catch</c> que se comía el
    /// rechazo: quedaba un día cargado con sus kilos y el inventario sin tocar. Validar acá es lo que
    /// permite rechazar dejando la base intacta.
    /// </para>
    ///
    /// <para>
    /// No reemplaza al descuento atómico de <c>RegistrarConsumoAsync</c>: la carrera entre dos
    /// consumos concurrentes la sigue cerrando el <c>UPDATE … WHERE quantity &gt;= …</c>.
    /// </para>
    /// </summary>
    Task ValidarStockConsumoAsync(
        int farmId,
        string? nucleoId,
        string? galponId,
        IReadOnlyDictionary<int, decimal> byItem,
        int? siloId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Fase 3 — consumo a NIVEL GRANJA (Colombia): descuenta stock por (farm, item, nucleo=NULL,
    /// galpon=NULL) sin exigir galpón; NO abre transacción propia (participa de la externa); lanza si
    /// no hay stock suficiente (bloqueo). Aditivo: NO cambia RegistrarConsumoAsync (EC/PA con galpón).
    /// </summary>
    Task RegistrarConsumoNivelGranjaAsync(InventarioGestionConsumoRequest req, CancellationToken ct = default);

    /// <summary>
    /// Fase 3 — devolución/ingreso a NIVEL GRANJA (Colombia): repone stock por (farm, item, nucleo=NULL,
    /// galpon=NULL); crea el stock si no existe; NO abre transacción propia. Aditivo (no toca EC/PA).
    /// </summary>
    Task RegistrarIngresoNivelGranjaAsync(InventarioGestionIngresoRequest req, CancellationToken ct = default);

    /// <summary>Histórico de movimientos (entradas, salidas, traslados) con filtros.</summary>
    Task<List<InventarioGestionMovimientoDto>> GetMovimientosAsync(
        int? farmId = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        string? estado = null,
        string? movementType = null,
        string? nucleoId = null,
        string? galponId = null,
        int? loteId = null,
        string? search = null,
        string? concepto = null,
        string? tipoItem = null,
        string? tipoOperacion = null,
        string? unit = null,
        string? referenceContains = null,
        string? reasonContains = null,
        string? transferGroupId = null,
        int? itemInventarioEcuadorId = null,
        int? fromFarmId = null,
        string? fromNucleoId = null,
        string? fromGalponId = null,
        CancellationToken ct = default);

    /// <summary>Traslados inter-granja en tránsito pendientes de recepción en la granja destino (opcional).</summary>
    Task<List<InventarioGestionTransitoPendienteDto>> GetTransitosPendientesAsync(int? farmIdDestino = null, CancellationToken ct = default);

    /// <summary>
    /// Completa el ingreso en destino de un traslado inter-granja (cierra el tránsito). Si la solicitud aún no
    /// descontó origen, descuenta aquí. Si el request trae <c>Distribucion</c> (alimento por galpón), genera una
    /// entrada por galpón; si no, una sola entrada como siempre.
    /// </summary>
    Task<InventarioGestionRecepcionTransitoResultDto> RegistrarRecepcionTransitoAsync(InventarioGestionRecepcionTransitoRequest req, CancellationToken ct = default);

    /// <summary>Rechaza una solicitud inter-granja pendiente; no modifica stock.</summary>
    Task RechazarTransitoPendienteAsync(InventarioGestionRechazoTransitoRequest req, CancellationToken ct = default);

    /// <summary>Ajusta cantidad (y opcionalmente unidad) de un registro de inventario_gestion_stock. Registra movimiento tipo AjusteStock.</summary>
    Task<InventarioGestionStockDto> ActualizarStockAsync(int stockId, InventarioGestionStockUpdateRequest req, CancellationToken ct = default);

    /// <summary>Elimina el registro de stock. Si había cantidad &gt; 0, registra salida antes de borrar.</summary>
    Task EliminarStockAsync(int stockId, CancellationToken ct = default);

    /// <summary>
    /// Anula un registro del histórico (solo <c>Consumo</c> o <c>Ingreso</c>): revierte el efecto en <c>inventario_gestion_stock</c> y elimina la fila del movimiento.
    /// No aplica a traslados ni tránsito inter-granja.
    /// </summary>
    Task AnularMovimientoHistoricoAsync(int movimientoId, string? motivo, CancellationToken ct = default);

    // ─── TRASLADOS ───────────────────────────────────────────────────────────

    /// <summary>Lista de traslados agrupados por TransferGroupId en granjas asignadas al usuario.</summary>
    Task<List<InventarioGestionTrasladoListDto>> GetTrasladosAsync(
        int? farmId = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        string? search = null,
        string? itemTipoItem = null,
        string? nucleoId = null,
        string? galponId = null,
        CancellationToken ct = default);

    /// <summary>Actualiza la fecha de movimiento de un traslado (aplica a todos los registros del TransferGroupId).</summary>
    Task<InventarioGestionTrasladoListDto> ActualizarFechaTrasladoAsync(
        Guid transferGroupId,
        InventarioGestionActualizarFechaTrasladoRequest req,
        CancellationToken ct = default);

    /// <summary>
    /// Elimina todos los movimientos de un traslado sin modificar stock.
    /// Marca anulado=true en lote_registro_historico_unificado (auditoría) y borra los registros.
    /// </summary>
    Task EliminarTrasladoAsync(Guid transferGroupId, CancellationToken ct = default);

    // ─── INGRESOS ────────────────────────────────────────────────────────────

    /// <summary>Lista de ingresos (Ingreso, TrasladoEntrada, TrasladoInterGranjaEntrada) en granjas asignadas al usuario.</summary>
    Task<List<InventarioGestionIngresoListDto>> GetIngresosAsync(
        int? farmId = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        string? search = null,
        string? itemTipoItem = null,
        string? nucleoId = null,
        string? galponId = null,
        CancellationToken ct = default);

    /// <summary>Actualiza la fecha de movimiento de un ingreso.</summary>
    Task<InventarioGestionIngresoListDto> ActualizarFechaIngresoAsync(
        int movimientoId,
        InventarioGestionActualizarFechaIngresoRequest req,
        CancellationToken ct = default);

    /// <summary>
    /// Actualiza la marca «para el próximo ciclo» de un ingreso y la sincroniza con el espejo
    /// <c>lote_registro_historico_unificado</c>.
    /// </summary>
    Task<InventarioGestionIngresoListDto> ActualizarDestinoCicloIngresoAsync(
        int movimientoId,
        InventarioGestionActualizarDestinoCicloRequest req,
        CancellationToken ct = default);

    /// <summary>
    /// D4 — datos para la excepción a la ventana de mes en curso: encasetamiento más cercano del
    /// galpón a partir de <paramref name="fechaMovimiento"/> y ventana de alimento previo de la
    /// empresa dueña de la granja. Sin galpón devuelve <c>ProximoEncaset = null</c>.
    /// </summary>
    Task<InventarioGestionVentanaAlimentoPrevioDto> ResolverVentanaAlimentoPrevioEncasetAsync(
        int farmId,
        string? nucleoId,
        string? galponId,
        DateTime fechaMovimiento,
        CancellationToken ct = default);

    /// <summary>
    /// Igual que <see cref="ResolverVentanaAlimentoPrevioEncasetAsync"/> pero tomando la ubicación de
    /// un ingreso ya registrado (la edición de fecha no la trae en el request).
    /// </summary>
    Task<InventarioGestionVentanaAlimentoPrevioDto> ResolverVentanaAlimentoPrevioEncasetDeIngresoAsync(
        int movimientoId,
        DateTime fechaMovimiento,
        CancellationToken ct = default);

    /// <summary>
    /// Elimina un ingreso (Ingreso / TrasladoEntrada / TrasladoInterGranjaEntrada) sin modificar stock.
    /// Marca anulado=true en lote_registro_historico_unificado (auditoría) y borra el registro.
    /// </summary>
    Task EliminarIngresoAsync(int movimientoId, CancellationToken ct = default);
}
