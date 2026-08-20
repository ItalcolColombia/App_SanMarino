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
    bool CompanyManejaAlimentoPorGalpon = false,
    /// <summary>
    /// Silos y bodegas ACTIVOS de las granjas visibles (origen + destino), ya agrupables por
    /// <c>GranjaId</c>. Va <b>vacío</b> cuando la empresa no maneja inventario por silo: en ese caso
    /// no se consulta nada y el front no pinta la columna.
    /// </summary>
    IEnumerable<InventarioGestionSiloDto>? Silos = null,
    /// <summary>¿La empresa activa ubica el inventario en SILOS en vez de galpones?</summary>
    bool CompanyManejaInventarioPorSilo = false
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
    string? AvisoFechaFueraDeCiclo = null,
    /// <summary>
    /// Silo o bodega donde vive este saldo (empresas con <c>maneja_inventario_por_silo</c>).
    /// <c>null</c> en todas las demás, donde la ubicación sigue siendo núcleo/galpón.
    /// </summary>
    int? SiloId = null,
    string? SiloNombre = null,
    /// <summary>
    /// Kilos <b>separados</b> (reservados) por seguimientos diarios que todavía no se validaron.
    /// <para>
    /// No se descontaron del stock: están comprometidos. Existe porque el mismo galpón alimenta a dos
    /// lotes y, sin esto, los dos ven el saldo completo y los dos creen tenerlo. Siempre 0 en las
    /// empresas sin doble validación.
    /// </para>
    /// </summary>
    decimal ReservadoKg = 0
)
{
    /// <summary>
    /// Lo que realmente se puede comprometer: <c>Quantity − ReservadoKg</c>. Puede quedar NEGATIVO si
    /// se separó de más; no se recorta a cero porque ese número es la señal de que dos lotes se
    /// pisaron sobre el mismo galpón.
    ///
    /// <para>
    /// <b>Es DERIVADA, no un parámetro más.</b> Hay nueve sitios en <c>InventarioGestionService</c>
    /// que arman este DTO a mano para las respuestas de ingreso, traslado y consumo; ninguno llegaba
    /// hasta el último parámetro, así que como parámetro posicional todos habrían devuelto
    /// <c>disponible = 0</c> y el front habría leído «no hay nada» sobre un galpón lleno. Derivada, es
    /// imposible de olvidar y la fórmula tiene un solo dueño.
    /// </para>
    /// </summary>
    public decimal DisponibleKg => Quantity - ReservadoKg;
}

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
    DateTime? FechaMovimiento = null,
    /// <summary>
    /// «Este alimento es para el PRÓXIMO encasetamiento de este galpón». Atribución EXPLÍCITA al ciclo
    /// siguiente, para los galpones encadenados donde la fecha sola no alcanza (el ingreso real cae
    /// dentro del ciclo anterior y el corte por fecha lo descartaría).
    /// <para>
    /// Con la marca puesta tampoco se emite <c>AvisoFechaFueraDeCiclo</c>: el usuario ya dijo a qué
    /// ciclo pertenece. Default <c>false</c> ⇒ comportamiento previo intacto.
    /// </para>
    /// </summary>
    bool ParaProximoCiclo = false,
    /// <summary>
    /// Silo o bodega destino. <b>Obligatorio</b> si la empresa maneja el inventario por silo, y
    /// rechazado si no lo maneja (no se mezclan los dos modelos en la misma tabla). Cuando viene,
    /// <c>NucleoId</c>/<c>GalponId</c> se persisten en <c>null</c>: el galpón viaja solo para filtrar
    /// qué silos ofrecer.
    /// </summary>
    int? SiloId = null
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
    DateTime? FechaMovimiento = null,
    /// <summary>Silo/bodega ORIGEN (empresas con inventario por silo). Admite bodega→silo y silo→silo.</summary>
    int? FromSiloId = null,
    /// <summary>Silo/bodega DESTINO (empresas con inventario por silo).</summary>
    int? ToSiloId = null
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
    string? ItemTipoItem = null,
    /// <summary>Movimiento atribuido explícitamente al PRÓXIMO ciclo del galpón (marca del alta o del historial).</summary>
    bool ParaProximoCiclo = false,
    /// <summary>Instante real de captura. <c>null</c> en las filas anteriores a la columna.</summary>
    DateTimeOffset? RegistradoAt = null,
    /// <summary>Silo/bodega donde ocurrió el movimiento (empresas con inventario por silo).</summary>
    int? SiloId = null,
    string? SiloNombre = null,
    /// <summary>Silo/bodega de ORIGEN del traslado (espejo de <c>FromGalponNombre</c>).</summary>
    int? FromSiloId = null,
    string? FromSiloNombre = null
);

/// <summary>Una ubicación de la granja destino y cuánto de lo recibido entra en ella (alimento por galpón).</summary>
public sealed record InventarioGestionRecepcionDestinoDto(
    string? NucleoId,
    string? GalponId,
    decimal Quantity,
    /// <summary>Silo/bodega destino de esta fila del reparto (empresas con inventario por silo).</summary>
    int? SiloId = null
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
    IReadOnlyList<InventarioGestionRecepcionDestinoDto>? Distribucion = null,
    /// <summary>Silo/bodega destino de la recepción en una sola ubicación (empresas con inventario por silo).</summary>
    int? ToSiloId = null
);

/// <summary>
/// Silo o bodega ofrecido como ubicación de un movimiento. Es la lista que llena el selector de
/// <c>/gestion-inventario</c>: solo silos ACTIVOS de la granja, con la bodega al final.
/// </summary>
public sealed record InventarioGestionSiloDto(
    int Id,
    int GranjaId,
    string Nombre,
    /// <summary><c>Silo</c> o <c>Bodega</c>.</summary>
    string Tipo,
    string? CodigoErpUbicacion,
    string? CodigoBodega
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
    DateTime? FechaMovimiento = null,
    /// <summary>Silo o bodega del que sale el consumo (empresas con inventario por silo).</summary>
    int? SiloId = null
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
    DateTimeOffset CreatedAt,
    /// <summary>Silo/bodega de ORIGEN (empresas con inventario por silo). Espejo de <c>FromGalponId</c>.</summary>
    int? FromSiloId = null,
    string? FromSiloNombre = null,
    /// <summary>Silo/bodega de DESTINO. <c>null</c> mientras el inter-granja sigue en tránsito.</summary>
    int? ToSiloId = null,
    string? ToSiloNombre = null
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
    DateTimeOffset CreatedAt,
    /// <summary>Ingreso atribuido explícitamente al PRÓXIMO ciclo del galpón (editable desde el historial).</summary>
    bool ParaProximoCiclo = false,
    /// <summary>Instante real de captura. <c>null</c> en las filas anteriores a la columna.</summary>
    DateTimeOffset? RegistradoAt = null,
    /// <summary>
    /// Silo/bodega donde entró el alimento (empresas con inventario por silo). Las filas huérfanas
    /// —el movimiento se borró y solo sobrevive el espejo— llegan sin silo: el dato vive en
    /// <c>inventario_gestion_movimiento</c>, que ya no existe.
    /// </summary>
    int? SiloId = null,
    string? SiloNombre = null
);

/// <summary>Edita solo la fecha de movimiento de un ingreso.</summary>
public sealed record InventarioGestionActualizarFechaIngresoRequest(
    DateTime FechaMovimiento
);

/// <summary>
/// Edita solo la atribución de ciclo de un ingreso ya registrado (el «desde acá podamos modificar
/// los datos» del pedido). Sincroniza el espejo <c>lote_registro_historico_unificado</c>.
/// </summary>
public sealed record InventarioGestionActualizarDestinoCicloRequest(
    bool ParaProximoCiclo
);

/// <summary>
/// Ventana de alimento previo al encasetamiento resuelta para una ubicación (D4). La usa el
/// controller para decidir si una fecha del mes anterior es admisible.
/// </summary>
/// <param name="ProximoEncaset">
/// Encasetamiento más cercano del galpón (engorde o postura) con <c>fecha_encaset &gt;= fecha</c> del
/// movimiento. <c>null</c> = el galpón no tiene ninguno ⇒ no hay excepción que aplicar.
/// </param>
/// <param name="DiasVentanaEmpresa"><c>companies.dias_alimento_previo_encaset</c> de la empresa de la granja.</param>
public sealed record InventarioGestionVentanaAlimentoPrevioDto(
    DateTime? ProximoEncaset,
    int DiasVentanaEmpresa
);

/// <summary>
/// Ventana de fechas que la PANTALLA puede ofrecer para un ingreso (D4). Es informativa: la que
/// decide sigue siendo la del controller —el conjunto admitido no es contiguo y esto es su rango
/// envolvente—, pero permite que el datepicker no recorte la fecha real del alimento previo al
/// encasetamiento y que el hint nombre el encaset concreto en vez de una promesa vaga.
/// </summary>
/// <param name="Min">
/// Primera fecha ofrecible (<c>yyyy-MM-dd</c>), o <c>null</c> cuando NO hay piso: el usuario tiene el
/// permiso de fecha retroactiva y la pantalla no debe poner atributo <c>min</c> en el datepicker.
/// </param>
/// <param name="Max">Última fecha ofrecible: siempre hoy, el futuro no lo abre ninguna vía.</param>
/// <param name="ProximoEncaset">Encasetamiento que justifica la apertura, o <c>null</c> si no hay.</param>
/// <param name="DiasVentanaEmpresa"><c>companies.dias_alimento_previo_encaset</c> de la empresa de la granja.</param>
/// <param name="Ayuda">Texto ya armado para el hint, para que backend y front digan lo mismo.</param>
public sealed record InventarioGestionVentanaFechaIngresoDto(
    DateOnly? Min,
    DateOnly Max,
    DateOnly? ProximoEncaset,
    int DiasVentanaEmpresa,
    string Ayuda
);
