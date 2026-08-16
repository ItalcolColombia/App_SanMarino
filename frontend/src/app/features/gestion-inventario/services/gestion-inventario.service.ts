// src/app/features/gestion-inventario/services/gestion-inventario.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface FarmDto {
  id: number;
  name: string;
  companyId: number;
  /** Override manejo alimento: null/undefined = hereda empresa; true = galpón; false = granja. */
  manejaAlimentoPorGalpon?: boolean | null;
}
export interface NucleoDto {
  nucleoId: string;
  granjaId: number;
  nucleoNombre: string;
  granjaNombre?: string;
}
export interface GalponLiteDto {
  galponId: string;
  galponNombre: string;
  nucleoId: string;
  granjaId: number;
}

/**
 * Silo o bodega ofrecido como ubicación de un movimiento. Es lo que llena el selector de
 * ingreso/traslado y la columna «Silo» de las grillas (empresas con inventario por silo).
 */
export interface InventarioGestionSiloDto {
  id: number;
  granjaId: number;
  nombre: string;
  /** `Silo` | `Bodega`. */
  tipo: string;
  codigoErpUbicacion?: string | null;
  codigoBodega?: string | null;
}

export interface InventarioGestionFilterDataDto {
  /** Granjas asignadas al usuario (origen / donde gestiona). */
  farmsOrigen: FarmDto[];
  /** Todas las granjas de la empresa (destino inter-granja, procedencia en ingreso). */
  farmsDestino: FarmDto[];
  nucleosOrigen: NucleoDto[];
  nucleosDestino: NucleoDto[];
  galponesOrigen: GalponLiteDto[];
  galponesDestino: GalponLiteDto[];
  /** Default GLOBAL de la empresa: ¿alimento a nivel galpón? (el front resuelve efectivo por granja). */
  companyManejaAlimentoPorGalpon?: boolean;
  /** Silos y bodegas activos de las granjas visibles. Vacío si la empresa no maneja silos. */
  silos?: InventarioGestionSiloDto[];
  /** ¿La empresa ubica el inventario en SILOS en vez de galpones? Fail-closed: ausente = false. */
  companyManejaInventarioPorSilo?: boolean;
}

/** Lote para filtrar histórico (granjas asignadas). */
export interface InventarioGestionLoteFiltroDto {
  loteId: number;
  loteNombre: string;
  fase: string | null;
  granjaId: number;
  nucleoId: string | null;
  galponId: string | null;
}

/** Valores distintos ya presentes en movimientos + lotes en tus granjas. */
export interface InventarioGestionHistoricoFiltrosDto {
  lotes: InventarioGestionLoteFiltroDto[];
  conceptosEnHistorico: string[];
  tiposItemEnHistorico: string[];
  estadosEnHistorico: string[];
  /** movementType tal como en BD (Ingreso, Consumo, TrasladoSalida, …). */
  movementTypesEnHistorico?: string[];
  unidadesEnHistorico?: string[];
  /** Etiquetas legibles (mapeadas en backend a movementType). */
  tiposOperacionEnHistorico?: string[];
  /** Misma jerarquía que filter-data: granjas asignadas → núcleos → galpones. */
  farmsOrigen?: FarmDto[];
  nucleosOrigen?: NucleoDto[];
  galponesOrigen?: GalponLiteDto[];
}

export interface InventarioGestionStockDto {
  id: number;
  farmId: number;
  nucleoId: string | null;
  galponId: string | null;
  itemInventarioEcuadorId: number;
  itemCodigo: string;
  itemNombre: string;
  itemType: string;
  quantity: number;
  unit: string;
  // El API los manda como `string?` de C#: llegan en null cuando el ítem no es alimento
  // (stock a nivel granja) o cuando no se resolvió el nombre. Siempre leer con `??`.
  granjaNombre?: string | null;
  nucleoNombre?: string | null;
  galponNombre?: string | null;
  /** Fecha de creación del registro de stock en esta ubicación (primera existencia). */
  fechaIngreso?: string | null;
  /** Silo/bodega donde vive el saldo (empresas con inventario por silo). Núcleo y galpón van null. */
  siloId?: number | null;
  siloNombre?: string | null;
  /**
   * Kilos SEPARADOS por seguimientos diarios que todavía no se validaron (doble validación).
   * No salieron del stock: están comprometidos. Siempre 0 en empresas sin el flag.
   */
  reservadoKg?: number | null;
  /**
   * Lo que realmente se puede comprometer: `quantity - reservadoKg`. **Este es el número que hay que
   * mostrar y validar en los formularios**, no `quantity` — que es la existencia física del galpón.
   *
   * Puede venir NEGATIVO si se separó de más; no se recorta, porque ese número es la señal de que dos
   * lotes se pisaron sobre el mismo galpón.
   */
  disponibleKg?: number | null;
}

/**
 * Saldo que un formulario puede comprometer sobre una fila de stock.
 *
 * Fail-safe hacia el comportamiento previo: si el backend no manda `disponibleKg` (respuesta vieja,
 * o uno de los DTO que arman ingreso/traslado/consumo), cae en `quantity`. Nunca inventa saldo.
 */
export function saldoComprometible(row: Pick<InventarioGestionStockDto, 'quantity' | 'disponibleKg'>): number {
  return row.disponibleKg == null ? Number(row.quantity ?? 0) : Number(row.disponibleKg);
}

export interface InventarioGestionIngresoRequest {
  farmId: number;
  nucleoId: string | null;
  galponId: string | null;
  itemInventarioEcuadorId: number;
  quantity: number;
  unit: string;
  reference?: string | null;
  reason?: string | null;
  /** Origen para estado en histórico: "planta" | "granja" | "bodega" */
  origenTipo?: string | null;
  /** Si origen es granja: granja de procedencia (distinta a farmId). Si es bodega: granja a la que pertenece la bodega. */
  origenFarmId?: number | null;
  /** Si origen es bodega: nombre o referencia de la bodega (opcional). */
  origenBodegaDescripcion?: string | null;
  /** Fecha del movimiento (solo día, yyyy-MM-dd). Si se omite, el backend usa fecha/hora actual. */
  fechaMovimiento?: string | null;
  /**
   * «Este alimento es para el PRÓXIMO encasetamiento de este galpón». Atribución explícita al ciclo
   * siguiente (galpones encadenados donde la fecha real no alcanza para distinguir el ciclo). Solo
   * aplica a ingresos con galpón (alimento); default false = comportamiento previo intacto.
   */
  paraProximoCiclo?: boolean;
  /**
   * Silo/bodega destino. **Obligatorio** con inventario por silo (todo concepto, alimento e insumos);
   * el backend rechaza el movimiento sin él. Núcleo/galpón viajan solo para filtrar la lista.
   */
  siloId?: number | null;
}

export interface InventarioGestionTrasladoRequest {
  fromFarmId: number;
  fromNucleoId: string | null;
  fromGalponId: string | null;
  toFarmId: number;
  toNucleoId: string | null;
  toGalponId: string | null;
  itemInventarioEcuadorId: number;
  quantity: number;
  unit: string;
  reference?: string | null;
  reason?: string | null;
  /** Destino para estado en histórico: "granja" | "planta" */
  destinoTipo?: string | null;
  /** Fecha en que se realizó el traslado (solo día, yyyy-MM-dd). Si se omite, el backend usa fecha/hora actual. */
  fechaMovimiento?: string | null;
  /** Silo/bodega ORIGEN (inventario por silo). Habilita bodega→silo y silo→silo. */
  fromSiloId?: number | null;
  /** Silo/bodega DESTINO (inventario por silo). En inter-granja lo elige quien recibe. */
  toSiloId?: number | null;
}

/** Registro del histórico de movimientos. */
export interface InventarioGestionMovimientoDto {
  id: number;
  farmId: number;
  nucleoId: string | null;
  galponId: string | null;
  itemInventarioEcuadorId: number;
  itemCodigo: string;
  itemNombre: string;
  itemType: string;
  quantity: number;
  unit: string;
  movementType: string;
  estado: string | null;
  fromFarmId: number | null;
  fromNucleoId: string | null;
  fromGalponId: string | null;
  reference: string | null;
  reason: string | null;
  createdAt: string;
  granjaNombre?: string | null;
  nucleoNombre?: string | null;
  galponNombre?: string | null;
  /** Agrupa salida/entrada del mismo traslado (inter-granja en tránsito). */
  transferGroupId?: string | null;
  /** Nombre granja en el otro extremo (origen/destino según tipo de movimiento). */
  fromGranjaNombre?: string | null;
  fromNucleoNombre?: string | null;
  fromGalponNombre?: string | null;
  /** Etiqueta legible: Ingreso, Consumo, Traslado entre granjas, etc. */
  tipoOperacion?: string | null;
  /** Catálogo: concepto del ítem (puede coincidir con itemType si no hay concepto). */
  itemConcepto?: string | null;
  /** Catálogo: tipo de ítem (alimento, etc.). */
  itemTipoItem?: string | null;
  /** Silo/bodega donde ocurrió el movimiento (empresas con inventario por silo). */
  siloId?: number | null;
  siloNombre?: string | null;
  /** Silo/bodega del otro extremo del traslado (espejo de `fromGalponNombre`). */
  fromSiloId?: number | null;
  fromSiloNombre?: string | null;
}

/** Salida inter-granja pendiente de recepción en destino. */
export interface InventarioGestionTransitoPendienteDto {
  transferGroupId: string;
  salidaMovimientoId: number;
  fromFarmId: number;
  fromGranjaNombre: string | null;
  toFarmId: number;
  toGranjaNombre: string | null;
  fromNucleoId: string | null;
  fromGalponId: string | null;
  destinoNucleoIdHint: string | null;
  destinoGalponIdHint: string | null;
  itemInventarioEcuadorId: number;
  itemCodigo: string;
  itemNombre: string;
  quantity: number;
  unit: string;
  createdAt: string;
  /** true: solicitud antigua (TrasladoInterGranjaPendiente); al recibir se descuenta origen. false: envío actual (origen ya descontado al traslado). */
  pendienteDespachoOrigen?: boolean;
}

/** Una ubicación de la granja destino y cuánto de lo recibido entra en ella. */
export interface InventarioGestionRecepcionDestino {
  nucleoId: string | null;
  galponId: string | null;
  quantity: number;
  /** Silo/bodega de esta fila del reparto (inventario por silo). */
  siloId?: number | null;
}

export interface InventarioGestionRecepcionTransitoRequest {
  transferGroupId: string;
  toFarmId: number;
  toNucleoId: string | null;
  toGalponId: string | null;
  /** Silo/bodega destino cuando se recibe todo en una sola ubicación (inventario por silo). */
  toSiloId?: number | null;
  /**
   * Reparto de lo recibido entre varios galpones (solo alimento manejado por galpón).
   * Si trae filas, reemplaza a toNucleoId/toGalponId y la suma debe igualar la cantidad en tránsito.
   */
  distribucion?: InventarioGestionRecepcionDestino[] | null;
}

/** Respuesta de la recepción: destino/movimiento = primera ubicación; destinos/movimientos = todas. */
export interface InventarioGestionRecepcionTransitoResponse {
  destino: InventarioGestionStockDto;
  movimiento: InventarioGestionMovimientoDto;
  destinos?: InventarioGestionStockDto[];
  movimientos?: InventarioGestionMovimientoDto[];
}

// ─── TRASLADOS: LISTADO Y EDICIÓN ────────────────────────────────────────────

/** Vista agrupada de un traslado (salida + entrada bajo el mismo TransferGroupId). */
export interface InventarioGestionTrasladoListDto {
  transferGroupId: string;
  salidaMovimientoId: number;
  entradaMovimientoId: number | null;
  fromFarmId: number;
  fromGranjaNombre: string | null;
  fromNucleoId: string | null;
  fromNucleoNombre: string | null;
  fromGalponId: string | null;
  fromGalponNombre: string | null;
  toFarmId: number;
  toGranjaNombre: string | null;
  toNucleoId: string | null;
  toNucleoNombre: string | null;
  toGalponId: string | null;
  toGalponNombre: string | null;
  itemInventarioEcuadorId: number;
  itemCodigo: string;
  itemNombre: string;
  itemConcepto: string;
  itemTipoItem: string;
  quantity: number;
  unit: string;
  reference: string | null;
  reason: string | null;
  /** "Completado" | "En tránsito" | "Pendiente despacho" | "Rechazado" */
  estado: string;
  fechaMovimiento: string;
  createdAt: string;
  /** Silo/bodega de ORIGEN (inventario por silo). */
  fromSiloId?: number | null;
  fromSiloNombre?: string | null;
  /** Silo/bodega de DESTINO. Null mientras el inter-granja sigue en tránsito. */
  toSiloId?: number | null;
  toSiloNombre?: string | null;
}

/** Edita solo la fecha de movimiento de un traslado (aplica a todos los registros del TransferGroupId). */
export interface InventarioGestionActualizarFechaTrasladoRequest {
  /** yyyy-MM-dd */
  fechaMovimiento: string;
}

// ─── INGRESOS: LISTADO Y EDICIÓN ─────────────────────────────────────────────

/** Vista de un ingreso (Ingreso directo, TrasladoEntrada o TrasladoInterGranjaEntrada). */
export interface InventarioGestionIngresoListDto {
  movimientoId: number;
  farmId: number;
  granjaNombre: string | null;
  nucleoId: string | null;
  nucleoNombre: string | null;
  galponId: string | null;
  galponNombre: string | null;
  itemInventarioEcuadorId: number;
  itemCodigo: string;
  itemNombre: string;
  itemConcepto: string;
  itemTipoItem: string;
  quantity: number;
  unit: string;
  reference: string | null;
  reason: string | null;
  estado: string | null;
  fechaMovimiento: string;
  createdAt: string;
  /** Ingreso atribuido explícitamente al PRÓXIMO ciclo del galpón (editable desde el historial). */
  paraProximoCiclo: boolean;
  /** Instante real de captura del movimiento. Null en filas anteriores a la columna. */
  registradoAt?: string | null;
  /** Silo/bodega donde entró (inventario por silo). Null en filas huérfanas del espejo. */
  siloId?: number | null;
  siloNombre?: string | null;
}

/** Edita solo la fecha de movimiento de un ingreso. */
export interface InventarioGestionActualizarFechaIngresoRequest {
  /** yyyy-MM-dd */
  fechaMovimiento: string;
}

/** Edita solo la atribución de ciclo (próximo ciclo) de un ingreso ya registrado. */
export interface InventarioGestionActualizarDestinoCicloRequest {
  paraProximoCiclo: boolean;
}

/** Ajuste manual de cantidad/unidad en un registro de stock. */
export interface InventarioGestionStockUpdateRequest {
  quantity: number;
  unit?: string | null;
  reason?: string | null;
  /** Fecha de primer ingreso (solo día, yyyy-MM-dd). Actualiza la fecha mostrada en stock. */
  fechaIngreso?: string | null;
}

/** Ítem del catálogo de inventario (Config > Ítems de inventario). Compartido EC/PA/CO. */
export interface ItemInventarioDto {
  id: number;
  codigo: string;
  nombre: string;
  tipoItem: string;
  concepto?: string | null;
  unidad: string;
  descripcion?: string | null;
  activo: boolean;
}

@Injectable({ providedIn: 'root' })
export class GestionInventarioService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getFilterData(): Observable<InventarioGestionFilterDataDto> {
    return this.http.get<InventarioGestionFilterDataDto>(`${this.api}/inventario-gestion/filter-data`);
  }

  /** Lotes en tus granjas + conceptos/tipos/estados ya vistos en el histórico. */
  getHistoricoFiltros(): Observable<InventarioGestionHistoricoFiltrosDto> {
    return this.http.get<InventarioGestionHistoricoFiltrosDto>(`${this.api}/inventario-gestion/historico-filtros`);
  }

  getStock(params: {
    farmId?: number;
    nucleoId?: string;
    galponId?: string;
    itemType?: string;
    search?: string;
  } = {}): Observable<InventarioGestionStockDto[]> {
    let httpParams = new HttpParams();
    if (params.farmId != null) httpParams = httpParams.set('farmId', params.farmId);
    if (params.nucleoId) httpParams = httpParams.set('nucleoId', params.nucleoId);
    if (params.galponId) httpParams = httpParams.set('galponId', params.galponId);
    if (params.itemType) httpParams = httpParams.set('itemType', params.itemType);
    if (params.search) httpParams = httpParams.set('search', params.search);
    return this.http.get<InventarioGestionStockDto[]>(`${this.api}/inventario-gestion/stock`, { params: httpParams });
  }

  /**
   * Silos y bodega ACTIVOS que pueden recibir o entregar un movimiento en esa granja.
   * Con `galponId` el backend acota a los silos que alimentan ese galpón (el galpón filtra, no
   * ubica) más la bodega, que se ofrece siempre. Devuelve `[]` si la empresa no maneja silos.
   */
  getSilos(farmId: number, nucleoId?: string | null, galponId?: string | null): Observable<InventarioGestionSiloDto[]> {
    let httpParams = new HttpParams().set('farmId', farmId);
    if (nucleoId) httpParams = httpParams.set('nucleoId', nucleoId);
    if (galponId) httpParams = httpParams.set('galponId', galponId);
    return this.http.get<InventarioGestionSiloDto[]>(`${this.api}/inventario-gestion/silos`, { params: httpParams });
  }

  actualizarStock(
    stockId: number,
    payload: InventarioGestionStockUpdateRequest
  ): Observable<InventarioGestionStockDto> {
    return this.http.put<InventarioGestionStockDto>(`${this.api}/inventario-gestion/stock/${stockId}`, payload);
  }

  eliminarStock(stockId: number): Observable<void> {
    return this.http.delete<void>(`${this.api}/inventario-gestion/stock/${stockId}`);
  }

  registrarIngreso(payload: InventarioGestionIngresoRequest): Observable<InventarioGestionStockDto> {
    return this.http.post<InventarioGestionStockDto>(`${this.api}/inventario-gestion/ingreso`, payload);
  }

  registrarTraslado(payload: InventarioGestionTrasladoRequest): Observable<{ origen: InventarioGestionStockDto; destino: InventarioGestionStockDto }> {
    return this.http.post<{ origen: InventarioGestionStockDto; destino: InventarioGestionStockDto }>(`${this.api}/inventario-gestion/traslado`, payload);
  }

  /** Histórico de movimientos (entradas, salidas, traslados). */
  getMovimientos(params: {
    farmId?: number;
    fechaDesde?: string;
    fechaHasta?: string;
    estado?: string;
    movementType?: string;
    nucleoId?: string;
    galponId?: string;
    loteId?: number;
    /** Código o nombre de ítem (item_inventario_ecuador). */
    search?: string;
    /** Filtro exacto por concepto del ítem (columna concepto en catálogo). */
    concepto?: string;
    /** Filtro exacto por tipo de ítem (columna tipo_item en catálogo). */
    tipoItem?: string;
    /** Etiqueta de operación (backend la traduce a movementType). */
    tipoOperacion?: string;
    unit?: string;
    referenceContains?: string;
    reasonContains?: string;
    transferGroupId?: string;
    itemInventarioEcuadorId?: number;
    fromFarmId?: number;
    fromNucleoId?: string;
    fromGalponId?: string;
  } = {}): Observable<InventarioGestionMovimientoDto[]> {
    let httpParams = new HttpParams();
    if (params.farmId != null) httpParams = httpParams.set('farmId', params.farmId);
    if (params.fechaDesde) httpParams = httpParams.set('fechaDesde', params.fechaDesde);
    if (params.fechaHasta) httpParams = httpParams.set('fechaHasta', params.fechaHasta);
    if (params.estado) httpParams = httpParams.set('estado', params.estado);
    if (params.movementType) httpParams = httpParams.set('movementType', params.movementType);
    if (params.nucleoId) httpParams = httpParams.set('nucleoId', params.nucleoId);
    if (params.galponId) httpParams = httpParams.set('galponId', params.galponId);
    if (params.loteId != null && params.loteId > 0) httpParams = httpParams.set('loteId', String(params.loteId));
    if (params.search?.trim()) httpParams = httpParams.set('search', params.search.trim());
    if (params.concepto?.trim()) httpParams = httpParams.set('concepto', params.concepto.trim());
    if (params.tipoItem?.trim()) httpParams = httpParams.set('tipoItem', params.tipoItem.trim());
    if (params.tipoOperacion?.trim()) httpParams = httpParams.set('tipoOperacion', params.tipoOperacion.trim());
    if (params.unit?.trim()) httpParams = httpParams.set('unit', params.unit.trim());
    if (params.referenceContains?.trim()) httpParams = httpParams.set('referenceContains', params.referenceContains.trim());
    if (params.reasonContains?.trim()) httpParams = httpParams.set('reasonContains', params.reasonContains.trim());
    if (params.transferGroupId?.trim()) httpParams = httpParams.set('transferGroupId', params.transferGroupId.trim());
    if (params.itemInventarioEcuadorId != null && params.itemInventarioEcuadorId > 0) {
      httpParams = httpParams.set('itemInventarioEcuadorId', String(params.itemInventarioEcuadorId));
    }
    if (params.fromFarmId != null) httpParams = httpParams.set('fromFarmId', String(params.fromFarmId));
    if (params.fromNucleoId) httpParams = httpParams.set('fromNucleoId', params.fromNucleoId);
    if (params.fromGalponId) httpParams = httpParams.set('fromGalponId', params.fromGalponId);
    return this.http.get<InventarioGestionMovimientoDto[]>(`${this.api}/inventario-gestion/movimientos`, { params: httpParams });
  }

  /**
   * Anula un registro del histórico (solo Consumo o Ingreso): revierte stock y elimina la fila.
   */
  anularMovimientoHistorico(movimientoId: number, motivo?: string | null): Observable<void> {
    let params = new HttpParams();
    if (motivo?.trim()) params = params.set('motivo', motivo.trim());
    return this.http.delete<void>(`${this.api}/inventario-gestion/movimientos/${movimientoId}`, { params });
  }

  /** Traslados inter-granja en tránsito (pendientes de recepción en destino). */
  getTransitosPendientes(farmIdDestino?: number | null): Observable<InventarioGestionTransitoPendienteDto[]> {
    let httpParams = new HttpParams();
    if (farmIdDestino != null) httpParams = httpParams.set('farmIdDestino', farmIdDestino);
    return this.http.get<InventarioGestionTransitoPendienteDto[]>(`${this.api}/inventario-gestion/transito/pendientes`, {
      params: httpParams
    });
  }

  /** Completa el ingreso en granja destino (cierra el tránsito). */
  registrarRecepcionTransito(
    payload: InventarioGestionRecepcionTransitoRequest
  ): Observable<InventarioGestionRecepcionTransitoResponse> {
    return this.http.post<InventarioGestionRecepcionTransitoResponse>(
      `${this.api}/inventario-gestion/transito/recepcion`,
      payload
    );
  }

  // ─── TRASLADOS ──────────────────────────────────────────────────────────────

  /** Lista de traslados agrupados por TransferGroupId. Filtros: granja, núcleo, galpón, fechas, búsqueda, tipoItem. */
  getTraslados(params: {
    farmId?: number;
    fechaDesde?: string;
    fechaHasta?: string;
    search?: string;
    itemTipoItem?: string;
    nucleoId?: string;
    galponId?: string;
  } = {}): Observable<InventarioGestionTrasladoListDto[]> {
    let httpParams = new HttpParams();
    if (params.farmId != null) httpParams = httpParams.set('farmId', params.farmId);
    if (params.fechaDesde) httpParams = httpParams.set('fechaDesde', params.fechaDesde);
    if (params.fechaHasta) httpParams = httpParams.set('fechaHasta', params.fechaHasta);
    if (params.search?.trim()) httpParams = httpParams.set('search', params.search.trim());
    if (params.itemTipoItem?.trim()) httpParams = httpParams.set('itemTipoItem', params.itemTipoItem.trim());
    if (params.nucleoId) httpParams = httpParams.set('nucleoId', params.nucleoId);
    if (params.galponId) httpParams = httpParams.set('galponId', params.galponId);
    return this.http.get<InventarioGestionTrasladoListDto[]>(`${this.api}/inventario-gestion/traslados`, { params: httpParams });
  }

  /** Actualiza la fecha de movimiento de un traslado (aplica a todos los registros del grupo). */
  actualizarFechaTraslado(
    transferGroupId: string,
    req: InventarioGestionActualizarFechaTrasladoRequest
  ): Observable<InventarioGestionTrasladoListDto> {
    return this.http.put<InventarioGestionTrasladoListDto>(`${this.api}/inventario-gestion/traslados/${transferGroupId}/fecha`, req);
  }

  /** Elimina un traslado completo: revierte stock y marca anulado en el histórico unificado. */
  eliminarTraslado(transferGroupId: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/inventario-gestion/traslados/${transferGroupId}`);
  }

  // ─── INGRESOS ───────────────────────────────────────────────────────────────

  /** Lista de ingresos (directos y de traslados). Filtros: granja, núcleo, galpón, fechas, búsqueda, tipoItem. */
  getIngresos(params: {
    farmId?: number;
    fechaDesde?: string;
    fechaHasta?: string;
    search?: string;
    itemTipoItem?: string;
    nucleoId?: string;
    galponId?: string;
  } = {}): Observable<InventarioGestionIngresoListDto[]> {
    let httpParams = new HttpParams();
    if (params.farmId != null) httpParams = httpParams.set('farmId', params.farmId);
    if (params.fechaDesde) httpParams = httpParams.set('fechaDesde', params.fechaDesde);
    if (params.fechaHasta) httpParams = httpParams.set('fechaHasta', params.fechaHasta);
    if (params.search?.trim()) httpParams = httpParams.set('search', params.search.trim());
    if (params.itemTipoItem?.trim()) httpParams = httpParams.set('itemTipoItem', params.itemTipoItem.trim());
    if (params.nucleoId) httpParams = httpParams.set('nucleoId', params.nucleoId);
    if (params.galponId) httpParams = httpParams.set('galponId', params.galponId);
    return this.http.get<InventarioGestionIngresoListDto[]>(`${this.api}/inventario-gestion/ingresos`, { params: httpParams });
  }

  /** Actualiza la fecha de movimiento de un ingreso (Ingreso directo o entrada de traslado). */
  actualizarFechaIngreso(
    movimientoId: number,
    req: InventarioGestionActualizarFechaIngresoRequest
  ): Observable<InventarioGestionIngresoListDto> {
    return this.http.put<InventarioGestionIngresoListDto>(`${this.api}/inventario-gestion/ingresos/${movimientoId}/fecha`, req);
  }

  /** Marca o quita la atribución «para el próximo ciclo» de un ingreso ya registrado (solo ingresos con galpón). */
  actualizarDestinoCicloIngreso(
    movimientoId: number,
    req: InventarioGestionActualizarDestinoCicloRequest
  ): Observable<InventarioGestionIngresoListDto> {
    return this.http.put<InventarioGestionIngresoListDto>(`${this.api}/inventario-gestion/ingresos/${movimientoId}/destino-ciclo`, req);
  }

  /** Elimina un ingreso: revierte stock y marca anulado en el histórico unificado. */
  eliminarIngreso(movimientoId: number): Observable<void> {
    return this.http.delete<void>(`${this.api}/inventario-gestion/ingresos/${movimientoId}`);
  }

  /** Ítems desde Config > Ítems inventario Ecuador (item_inventario_ecuador). */
  getItemsByType(tipoItem: string | null = null, search: string | null = null, activo = true): Observable<ItemInventarioDto[]> {
    let httpParams = new HttpParams();
    if (tipoItem) httpParams = httpParams.set('tipoItem', tipoItem);
    if (search) httpParams = httpParams.set('q', search);
    if (activo !== undefined) httpParams = httpParams.set('activo', String(activo));
    return this.http.get<ItemInventarioDto[]>(`${this.api}/inventario/items`, { params: httpParams });
  }

  /** Obtiene un ítem de inventario Ecuador por su ID. */
  getItemById(id: number): Observable<ItemInventarioDto> {
    return this.http.get<ItemInventarioDto>(`${this.api}/inventario/items/${id}`);
  }
}
