// src/ZooSanMarino.Application/DTOs/InventarioGestionDtos.cs
using ZooSanMarino.Application.DTOs.Shared;

namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Datos para filtros del módulo Gestión de Inventario (Granja → Núcleo → Galpón).
/// <list type="bullet">
/// <item><term>FarmsOrigen</term> Granjas asignadas al usuario (user_farms) dentro de la empresa activa.</item>
/// <item><term>FarmsDestino</term> Todas las granjas de la empresa (p. ej. destino de traslado inter-granja o granja de procedencia en ingreso).</item>
/// </list>
/// </summary>
public sealed record InventarioGestionFilterDataDto(
    IEnumerable<FarmDto> FarmsOrigen,
    IEnumerable<FarmDto> FarmsDestino,
    IEnumerable<NucleoDto> NucleosOrigen,
    IEnumerable<NucleoDto> NucleosDestino,
    IEnumerable<GalponLiteDto> GalponesOrigen,
    IEnumerable<GalponLiteDto> GalponesDestino,
    // Default GLOBAL de la empresa activa: ¿alimento a nivel galpón? El front resuelve el
    // efectivo por granja: farm.ManejaAlimentoPorGalpon ?? CompanyManejaAlimentoPorGalpon.
    bool CompanyManejaAlimentoPorGalpon = false
);

/// <summary>Lote para filtrar el histórico por ubicación (tabla lotes, granjas asignadas).</summary>
public sealed record InventarioGestionLoteFiltroDto(
    int LoteId,
    string LoteNombre,
    string? Fase,
    int GranjaId,
    string? NucleoId,
    string? GalponId);

/// <summary>
/// Valores distintos en el histórico + lotes en granjas asignadas + catálogo de ubicación (misma jerarquía que filter-data).
/// Las granjas/núcleos/galpones permiten armar el filtro aunque <c>filter-data</c> falle o cargue tarde.
/// </summary>
public sealed record InventarioGestionHistoricoFiltrosDto(
    IReadOnlyList<InventarioGestionLoteFiltroDto> Lotes,
    IReadOnlyList<string> ConceptosEnHistorico,
    IReadOnlyList<string> TiposItemEnHistorico,
    IReadOnlyList<string> EstadosEnHistorico,
    IReadOnlyList<string> MovementTypesEnHistorico,
    IReadOnlyList<string> UnidadesEnHistorico,
    IReadOnlyList<string> TiposOperacionEnHistorico,
    IReadOnlyList<FarmDto> FarmsOrigen,
    IReadOnlyList<NucleoDto> NucleosOrigen,
    IReadOnlyList<GalponLiteDto> GalponesOrigen);

/// <summary>Stock de un ítem en una ubicación (granja o granja+núcleo+galpón). Ítem desde item_inventario_ecuador.</summary>
public sealed record InventarioGestionStockDto(
    int Id,
    int FarmId,
    string? NucleoId,
    string? GalponId,
    int ItemInventarioEcuadorId,
    string ItemCodigo,
    string ItemNombre,
    string ItemType,
    decimal Quantity,
    string Unit,
    string? GranjaNombre = null,
    string? NucleoNombre = null,
    string? GalponNombre = null,
    /// <summary>Fecha en que se creó el registro de stock en esta ubicación (primera vez que hubo existencia).</summary>
    DateTimeOffset? FechaIngreso = null,
    /// <summary>
    /// Aviso —no error— cuando el movimiento recién registrado quedó fechado FUERA del ciclo vigente
    /// del galpón, así que ningún lote de engorde lo va a reflejar en su tabla diaria.
    /// <para>
    /// Nace del caso Kilometro 86 / G0040 (jul-2026): recibió 182.630 kg fechados después de que su
    /// ciclo cerró y el sistema los aceptó en silencio. El lote se había comido ese alimento, así que
    /// la tabla diaria mostró 9.020 kg de déficit que parecían un error del aplicativo. Fue la única
    /// causa de saldos negativos que NO se pudo arreglar con código: el dato ya estaba mal.
    /// </para>
    /// <para><c>null</c> cuando la fecha es normal. Ver <c>AvisoFechaFueraDeCicloCalculos</c>.</para>
    /// </summary>
    string? AvisoFechaFueraDeCiclo = null
);

/// <summary>Request para registrar un ingreso. ItemInventarioEcuadorId referencia a config/item-inventario-ecuador.</summary>
public sealed record InventarioGestionIngresoRequest(
    int FarmId,
    string? NucleoId,
    string? GalponId,
    int ItemInventarioEcuadorId,
    decimal Quantity,
    string Unit,
    string? Reference,
    string? Reason,
    /// <summary>Origen para estado en histórico: planta | granja | bodega.</summary>
    string? OrigenTipo = null,
    /// <summary>Si OrigenTipo es "granja", granja de procedencia (debe ser distinta a FarmId).</summary>
    int? OrigenFarmId = null,
    /// <summary>Si OrigenTipo es "bodega", texto opcional (nombre/referencia de la bodega de procedencia).</summary>
    string? OrigenBodegaDescripcion = null,
    /// <summary>Fecha del movimiento en histórico (solo día). Si es null, se usa la fecha/hora actual del servidor.</summary>
    DateTime? FechaMovimiento = null
);

/// <summary>Request para registrar un traslado.</summary>
public sealed record InventarioGestionTrasladoRequest(
    int FromFarmId,
    string? FromNucleoId,
    string? FromGalponId,
    int ToFarmId,
    string? ToNucleoId,
    string? ToGalponId,
    int ItemInventarioEcuadorId,
    decimal Quantity,
    string Unit,
    string? Reference,
    string? Reason,
    /// <summary>Destino para estado en histórico: "granja" → Transferencia a granja, "planta" → Transferencia a planta.</summary>
    string? DestinoTipo = null,
    /// <summary>Fecha en que se realizó el traslado (solo día). Si es null, se usa la fecha/hora actual del servidor.</summary>
    DateTime? FechaMovimiento = null
);

/// <summary>Registro del histórico de movimientos (entradas, salidas, traslados).</summary>
public sealed record InventarioGestionMovimientoDto(
    int Id,
    int FarmId,
    string? NucleoId,
    string? GalponId,
    int ItemInventarioEcuadorId,
    string ItemCodigo,
    string ItemNombre,
    string ItemType,
    decimal Quantity,
    string Unit,
    string MovementType,
    string? Estado,
    int? FromFarmId,
    string? FromNucleoId,
    string? FromGalponId,
    string? Reference,
    string? Reason,
    DateTimeOffset CreatedAt,
    string? GranjaNombre,
    string? NucleoNombre,
    string? GalponNombre,
    /// <summary>Agrupa salida/entrada de un mismo traslado (incl. inter-granja en tránsito).</summary>
    Guid? TransferGroupId = null,
    /// <summary>Nombre granja en el otro extremo (origen de traslado hacia esta fila, o destino según tipo).</summary>
    string? FromGranjaNombre = null,
    string? FromNucleoNombre = null,
    string? FromGalponNombre = null,
    /// <summary>Etiqueta de operación para reportes (ingreso, consumo, traslado, etc.).</summary>
    string? TipoOperacion = null,
    string? ItemConcepto = null,
    string? ItemTipoItem = null
);

/// <summary>Una ubicación de la granja destino y cuánto de lo recibido entra en ella (alimento por galpón).</summary>
public sealed record InventarioGestionRecepcionDestinoDto(
    string? NucleoId,
    string? GalponId,
    decimal Quantity
);

/// <summary>Recepción en granja destino de un traslado inter-granja que quedó en tránsito.</summary>
public sealed record InventarioGestionRecepcionTransitoRequest(
    Guid TransferGroupId,
    int ToFarmId,
    string? ToNucleoId,
    string? ToGalponId,
    /// <summary>
    /// Reparto de lo recibido entre varios galpones de la granja destino (solo alimento manejado por galpón).
    /// Si trae filas, reemplaza a <c>ToNucleoId</c>/<c>ToGalponId</c> y la suma debe igualar la cantidad en tránsito.
    /// Null o vacío = recepción en una sola ubicación (comportamiento clásico).
    /// </summary>
    IReadOnlyList<InventarioGestionRecepcionDestinoDto>? Distribucion = null
);

/// <summary>
/// Resultado de una recepción de tránsito: una entrada por ubicación de destino
/// (una sola cuando no hay distribución).
/// </summary>
public sealed record InventarioGestionRecepcionTransitoResultDto(
    IReadOnlyList<InventarioGestionStockDto> Destinos,
    IReadOnlyList<InventarioGestionMovimientoDto> Movimientos
);

/// <summary>Rechazo en destino de una solicitud inter-granja pendiente (no descuenta origen).</summary>
public sealed record InventarioGestionRechazoTransitoRequest(
    Guid TransferGroupId,
    string? Reason
);

/// <summary>Envío inter-granja pendiente de recepción en destino (origen ya descontado en envíos nuevos).</summary>
public sealed record InventarioGestionTransitoPendienteDto(
    Guid TransferGroupId,
    int SalidaMovimientoId,
    int FromFarmId,
    string? FromGranjaNombre,
    int ToFarmId,
    string? ToGranjaNombre,
    string? FromNucleoId,
    string? FromGalponId,
    string? DestinoNucleoIdHint,
    string? DestinoGalponIdHint,
    int ItemInventarioEcuadorId,
    string ItemCodigo,
    string ItemNombre,
    decimal Quantity,
    string Unit,
    DateTimeOffset CreatedAt,
    /// <summary>True: movimiento antiguo TrasladoInterGranjaPendiente (origen se descuenta al recibir). False: TrasladoInterGranjaSalida (origen ya descontado al registrar el traslado).</summary>
    bool PendienteDespachoOrigen = true
);

/// <summary>Request para registrar consumo (reduce stock). Usado desde Seguimiento Diario.</summary>
public sealed record InventarioGestionConsumoRequest(
    int FarmId,
    string? NucleoId,
    string? GalponId,
    int ItemInventarioEcuadorId,
    decimal Quantity,
    string Unit,
    string? Reference,
    string? Reason,
    /// <summary>
    /// Fecha del consumo en el histórico (solo día). Null = fecha/hora actual del servidor, que es el
    /// comportamiento de siempre. Se usa en cargas históricas: sin esto, el kardex fecha TODOS los
    /// consumos de un lote de 41 días el día en que se importó el archivo.
    /// </summary>
    DateTime? FechaMovimiento = null
);

/// <summary>Ajuste directo de cantidad/unidad en un registro de stock (misma ubicación e ítem).</summary>
public sealed record InventarioGestionStockUpdateRequest(
    decimal Quantity,
    string? Unit,
    string? Reason,
    /// <summary>Fecha de primer ingreso en ubicación (solo día). Si se indica, actualiza <c>CreatedAt</c> del stock.</summary>
    DateTime? FechaIngreso = null
);

// ─── TRASLADOS: LISTADO Y EDICIÓN ────────────────────────────────────────────

/// <summary>
/// Vista agrupada de un traslado (salida + entrada opcional bajo el mismo TransferGroupId).
/// <list type="bullet">
///   <item>Misma granja: TrasladoSalida + TrasladoEntrada, ambos presentes.</item>
///   <item>Inter-granja: TrasladoInterGranjaSalida + TrasladoInterGranjaEntrada (o solo salida si está en tránsito).</item>
/// </list>
/// </summary>
public sealed record InventarioGestionTrasladoListDto(
    Guid TransferGroupId,
    int SalidaMovimientoId,
    int? EntradaMovimientoId,
    int FromFarmId,
    string? FromGranjaNombre,
    string? FromNucleoId,
    string? FromNucleoNombre,
    string? FromGalponId,
    string? FromGalponNombre,
    int ToFarmId,
    string? ToGranjaNombre,
    string? ToNucleoId,
    string? ToNucleoNombre,
    string? ToGalponId,
    string? ToGalponNombre,
    int ItemInventarioEcuadorId,
    string ItemCodigo,
    string ItemNombre,
    string ItemConcepto,
    string ItemTipoItem,
    decimal Quantity,
    string Unit,
    string? Reference,
    string? Reason,
    /// <summary>Estado del traslado: "Completado", "En tránsito", "Rechazado".</summary>
    string Estado,
    DateTimeOffset FechaMovimiento,
    DateTimeOffset CreatedAt
);

/// <summary>Edita solo la fecha de movimiento de un traslado (aplica a todos los registros del TransferGroupId).</summary>
public sealed record InventarioGestionActualizarFechaTrasladoRequest(
    DateTime FechaMovimiento
);

// ─── INGRESOS: LISTADO Y EDICIÓN ─────────────────────────────────────────────

/// <summary>Vista de un ingreso individual del histórico.</summary>
public sealed record InventarioGestionIngresoListDto(
    int MovimientoId,
    int FarmId,
    string? GranjaNombre,
    string? NucleoId,
    string? NucleoNombre,
    string? GalponId,
    string? GalponNombre,
    int ItemInventarioEcuadorId,
    string ItemCodigo,
    string ItemNombre,
    string ItemConcepto,
    string ItemTipoItem,
    decimal Quantity,
    string Unit,
    string? Reference,
    string? Reason,
    string? Estado,
    DateTimeOffset FechaMovimiento,
    DateTimeOffset CreatedAt
);

/// <summary>Edita solo la fecha de movimiento de un ingreso.</summary>
public sealed record InventarioGestionActualizarFechaIngresoRequest(
    DateTime FechaMovimiento
);
