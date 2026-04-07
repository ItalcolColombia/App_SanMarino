import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import { FarmDto } from '../../farm/services/farm.service';
import { GalponDetailDto } from '../../galpon/models/galpon.models';
import { LoteAveEngordeDto } from '../../lote-engorde/services/lote-engorde.service';

/** Catálogo único para filtros (granja → núcleo → galpón → lote Ave Engorde), alineado con GET /api/MovimientoPolloEngorde/filter-data */
export interface MovimientoPolloEngordeFilterDataDto {
  farms: FarmDto[];
  nucleos: Array<{ nucleoId: string; granjaId: number; nucleoNombre: string }>;
  galpones: GalponDetailDto[];
  lotesAveEngorde: LoteAveEngordeDto[];
}

export interface MovimientoPolloEngordeDto {
  id: number;
  numeroMovimiento: string;
  fechaMovimiento: string;
  tipoMovimiento: string;
  tipoLoteOrigen: string | null;
  loteOrigenId: number | null;
  loteOrigenNombre: string | null;
  tipoLoteDestino: string | null;
  loteDestinoId: number | null;
  loteDestinoNombre: string | null;
  granjaOrigenId: number | null;
  granjaOrigenNombre: string | null;
  granjaDestinoId: number | null;
  granjaDestinoNombre: string | null;
  cantidadHembras: number;
  cantidadMachos: number;
  cantidadMixtas: number;
  totalAves: number;
  estado: string;
  motivoMovimiento: string | null;
  observaciones: string | null;
  usuarioMovimientoId: number;
  usuarioNombre: string | null;
  fechaProcesamiento: string | null;
  fechaCancelacion: string | null;
  createdAt: string;
  numeroDespacho?: string | null;
  edadAves?: number | null;
  totalPollosGalpon?: number | null;
  raza?: string | null;
  placa?: string | null;
  horaSalida?: string | null;
  guiaAgrocalidad?: string | null;
  sellos?: string | null;
  ayuno?: string | null;
  conductor?: string | null;
  pesoBruto?: number | null;
  pesoTara?: number | null;
  pesoNeto?: number | null;
  promedioPesoAve?: number | null;
}

export interface ResumenAvesLoteDto {
  tipoLote: string;
  loteId: number;
  nombreLote: string | null;
  avesInicioHembras: number;
  avesInicioMachos: number;
  avesInicioMixtas: number;
  avesInicioTotal: number;
  avesSalidasTotal: number;
  avesVendidasTotal: number;
  avesActualesHembras: number;
  avesActualesMachos: number;
  avesActualesMixtas: number;
  avesActualesTotal: number;
}

/** Respuesta de POST /resumen-aves-lotes (una entrada por id solicitado). */
export interface ResumenAvesLotePorIdDto {
  loteId: number;
  resumen: ResumenAvesLoteDto | null;
}

export interface ResumenAvesLotesResponse {
  items: ResumenAvesLotePorIdDto[];
}

export interface AvesDisponiblesVentaLoteDto {
  loteId: number;
  tipoLote: string;
  nombreLote: string | null;
  hembrasDisponibles: number;
  machosDisponibles: number;
  mixtasDisponibles: number;
  totalDisponibles: number;
  hembrasReservadasPendiente: number;
  machosReservadasPendiente: number;
  mixtasReservadasPendiente: number;
  totalReservadasPendiente: number;
}

export interface AvesDisponiblesLotePorIdDto {
  loteId: number;
  disponibles: AvesDisponiblesVentaLoteDto | null;
}

export interface AvesDisponiblesLotesResponse {
  items: AvesDisponiblesLotePorIdDto[];
}

export interface AuditoriaVentasEngordeRequest {
  granjaId?: number | null;
  loteAveEngordeIds?: number[] | null;
  aplicarCorreccion?: boolean;
  dryRun?: boolean;
}

export interface AuditoriaVentasLoteDetalle {
  loteAveEngordeId: number;
  loteNombre: string | null;
  encasetadasH: number;
  encasetadasM: number;
  encasetadasX: number;
  mortCajaH: number;
  mortCajaM: number;
  mortSegH: number;
  mortSegM: number;
  selH: number;
  selM: number;
  errSexH: number;
  errSexM: number;
  asignadasH: number;
  asignadasM: number;
  maxVendibleH: number;
  maxVendibleM: number;
  maxVendibleX: number;
  vendidasCompletadoH: number;
  vendidasCompletadoM: number;
  vendidasCompletadoX: number;
  vendidasPendienteH: number;
  vendidasPendienteM: number;
  vendidasPendienteX: number;
  excesoH: number;
  excesoM: number;
  excesoX: number;
  autoCorregible: boolean;
  estado: string;
}

export interface AuditoriaCorreccionAccion {
  movimientoId: number;
  numeroMovimiento: string;
  loteAveEngordeOrigenId: number;
  antesH: number;
  antesM: number;
  antesX: number;
  despuesH: number;
  despuesM: number;
  despuesX: number;
  nota: string;
}

export interface AuditoriaVentasEngordeResponse {
  ok: boolean;
  dryRun: boolean;
  aplicarCorreccion: boolean;
  mensaje: string | null;
  lotes: AuditoriaVentasLoteDetalle[];
  acciones: AuditoriaCorreccionAccion[];
}

export interface CorregirVentasCompletadasRequest {
  granjaId?: number | null;
  loteAveEngordeIds?: number[] | null;
  dryRun?: boolean;
}

export interface CorreccionCompletadoAccionDto {
  movimientoId: number;
  numeroMovimiento: string;
  loteAveEngordeId: number;
  antesH: number;
  antesM: number;
  antesX: number;
  despuesH: number;
  despuesM: number;
  despuesX: number;
  devueltoAlLoteH: number;
  devueltoAlLoteM: number;
  devueltoAlLoteX: number;
  nota: string;
}

export interface CorregirVentasCompletadasResponse {
  ok: boolean;
  dryRun: boolean;
  mensaje: string | null;
  acciones: CorreccionCompletadoAccionDto[];
}

export interface VentaGranjaDespachoLineaDto {
  loteAveEngordeOrigenId: number;
  granjaOrigenId?: number | null;
  nucleoOrigenId?: string | null;
  galponOrigenId?: string | null;
  cantidadHembras: number;
  cantidadMachos: number;
  cantidadMixtas: number;
}

export interface CreateVentaGranjaDespachoDto {
  fechaMovimiento: string;
  tipoMovimiento: string;
  granjaOrigenId?: number | null;
  usuarioMovimientoId: number;
  motivoMovimiento?: string | null;
  descripcion?: string | null;
  observaciones?: string | null;
  numeroDespacho?: string | null;
  edadAves?: number | null;
  totalPollosGalpon?: number | null;
  raza?: string | null;
  placa?: string | null;
  horaSalida?: string | null;
  guiaAgrocalidad?: string | null;
  sellos?: string | null;
  ayuno?: string | null;
  conductor?: string | null;
  pesoBruto?: number | null;
  pesoTara?: number | null;
  lineas: VentaGranjaDespachoLineaDto[];
}

export interface VentaGranjaDespachoResultDto {
  movimientos: MovimientoPolloEngordeDto[];
}

export interface CreateMovimientoPolloEngordeDto {
  fechaMovimiento: string;
  tipoMovimiento: string;
  loteAveEngordeOrigenId?: number | null;
  loteReproductoraAveEngordeOrigenId?: number | null;
  granjaOrigenId?: number | null;
  nucleoOrigenId?: string | null;
  galponOrigenId?: string | null;
  loteAveEngordeDestinoId?: number | null;
  loteReproductoraAveEngordeDestinoId?: number | null;
  granjaDestinoId?: number | null;
  nucleoDestinoId?: string | null;
  galponDestinoId?: string | null;
  plantaDestino?: string | null;
  cantidadHembras: number;
  cantidadMachos: number;
  cantidadMixtas: number;
  motivoMovimiento?: string | null;
  descripcion?: string | null;
  observaciones?: string | null;
  usuarioMovimientoId: number;
  numeroDespacho?: string | null;
  edadAves?: number | null;
  totalPollosGalpon?: number | null;
  raza?: string | null;
  placa?: string | null;
  horaSalida?: string | null;
  guiaAgrocalidad?: string | null;
  sellos?: string | null;
  ayuno?: string | null;
  conductor?: string | null;
  pesoBruto?: number | null;
  pesoTara?: number | null;
}

export interface UpdateMovimientoPolloEngordeDto {
  fechaMovimiento?: string | null;
  tipoMovimiento?: string | null;
  granjaOrigenId?: number | null;
  nucleoOrigenId?: string | null;
  galponOrigenId?: string | null;
  granjaDestinoId?: number | null;
  nucleoDestinoId?: string | null;
  galponDestinoId?: string | null;
  plantaDestino?: string | null;
  cantidadHembras?: number | null;
  cantidadMachos?: number | null;
  cantidadMixtas?: number | null;
  motivoMovimiento?: string | null;
  observaciones?: string | null;
  numeroDespacho?: string | null;
  edadAves?: number | null;
  totalPollosGalpon?: number | null;
  raza?: string | null;
  placa?: string | null;
  horaSalida?: string | null;
  guiaAgrocalidad?: string | null;
  sellos?: string | null;
  ayuno?: string | null;
  conductor?: string | null;
  pesoBruto?: number | null;
  pesoTara?: number | null;
}

export interface PagedResult<T> {
  page: number;
  pageSize: number;
  total: number;
  items: T[];
}

@Injectable({ providedIn: 'root' })
export class MovimientoPolloEngordeService {
  private readonly base = `${environment.apiUrl}/MovimientoPolloEngorde`;

  constructor(private http: HttpClient) {}

  getAll(): Observable<MovimientoPolloEngordeDto[]> {
    return this.http.get<MovimientoPolloEngordeDto[]>(this.base).pipe(catchError(this.handleError));
  }

  /** Granjas asignadas, núcleos, galpones y lotes Ave Engorde en una sola petición. */
  getFilterData(): Observable<MovimientoPolloEngordeFilterDataDto> {
    return this.http.get<MovimientoPolloEngordeFilterDataDto>(`${this.base}/filter-data`).pipe(catchError(this.handleError));
  }

  search(params: {
    numeroMovimiento?: string;
    tipoMovimiento?: string;
    estado?: string;
    loteAveEngordeOrigenId?: number;
    loteReproductoraAveEngordeOrigenId?: number;
    /** Filtro por granja de origen (todos los galpones/lotes de esa granja). */
    granjaOrigenId?: number;
    nucleoOrigenId?: string;
    galponOrigenId?: string;
    galponOrigenSinAsignar?: boolean;
    fechaDesde?: string;
    fechaHasta?: string;
    page?: number;
    pageSize?: number;
    sortBy?: string;
    sortDesc?: boolean;
  }): Observable<PagedResult<MovimientoPolloEngordeDto>> {
    return this.http
      .post<PagedResult<MovimientoPolloEngordeDto>>(`${this.base}/search`, params)
      .pipe(catchError(this.handleError));
  }

  /** Obtiene movimientos por lote de origen (Ave Engorde o Reproductora). */
  getByLoteOrigen(
    loteAveEngordeOrigenId?: number | null,
    loteReproductoraAveEngordeOrigenId?: number | null
  ): Observable<MovimientoPolloEngordeDto[]> {
    const params: Record<string, unknown> = { page: 1, pageSize: 500 };
    if (loteAveEngordeOrigenId != null) params['loteAveEngordeOrigenId'] = loteAveEngordeOrigenId;
    if (loteReproductoraAveEngordeOrigenId != null) params['loteReproductoraAveEngordeOrigenId'] = loteReproductoraAveEngordeOrigenId;
    return this.search(params).pipe(
      catchError(this.handleError),
      map((res) => res.items ?? [])
    );
  }

  getById(id: number): Observable<MovimientoPolloEngordeDto> {
    return this.http.get<MovimientoPolloEngordeDto>(`${this.base}/${id}`).pipe(catchError(this.handleError));
  }

  create(dto: CreateMovimientoPolloEngordeDto): Observable<MovimientoPolloEngordeDto> {
    return this.http.post<MovimientoPolloEngordeDto>(this.base, dto).pipe(catchError(this.handleError));
  }

  update(id: number, dto: UpdateMovimientoPolloEngordeDto): Observable<MovimientoPolloEngordeDto> {
    return this.http.put<MovimientoPolloEngordeDto>(`${this.base}/${id}`, dto).pipe(catchError(this.handleError));
  }

  cancel(id: number, motivo: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/cancelar`, { motivo }).pipe(catchError(this.handleError));
  }

  /**
   * Elimina el registro. Si el movimiento estaba completado (p. ej. venta), las aves vuelven al inventario del lote de origen.
   */
  eliminar(id: number, motivo?: string): Observable<void> {
    let params = new HttpParams();
    if (motivo?.trim()) params = params.set('motivo', motivo.trim());
    return this.http.delete<void>(`${this.base}/${id}`, { params }).pipe(catchError(this.handleError));
  }

  /** Completa el movimiento: descuenta aves del lote origen y suma al destino. El movimiento pasa a Completado. */
  complete(id: number): Observable<MovimientoPolloEngordeDto> {
    return this.http.post<MovimientoPolloEngordeDto>(`${this.base}/${id}/completar`, {}).pipe(catchError(this.handleError));
  }

  /** Resumen para reportes: aves con que inició el lote, salidas, vendidas, actuales. */
  getResumenAvesLote(tipoLote: 'LoteAveEngorde' | 'LoteReproductoraAveEngorde', loteId: number): Observable<ResumenAvesLoteDto> {
    return this.http
      .get<ResumenAvesLoteDto>(`${this.base}/resumen-aves-lote`, { params: { tipoLote, loteId: String(loteId) } })
      .pipe(catchError(this.handleError));
  }

  /** Varios resúmenes en una sola petición HTTP. */
  postResumenAvesLotes(body: { tipoLote: string; loteIds: number[] }): Observable<ResumenAvesLotesResponse> {
    return this.http
      .post<ResumenAvesLotesResponse>(`${this.base}/resumen-aves-lotes`, body)
      .pipe(catchError(this.handleError));
  }

  /** Disponibilidad para venta por lote (incluye reservas Pendiente). */
  postAvesDisponiblesLotes(body: { tipoLote: string; loteIds: number[] }): Observable<AvesDisponiblesLotesResponse> {
    return this.http
      .post<AvesDisponiblesLotesResponse>(`${this.base}/aves-disponibles-lotes`, body)
      .pipe(catchError(this.handleError));
  }

  /** Auditoría / corrección de ventas (por granja o por lotes). */
  postAuditarVentas(body: AuditoriaVentasEngordeRequest): Observable<AuditoriaVentasEngordeResponse> {
    return this.http
      .post<AuditoriaVentasEngordeResponse>(`${this.base}/auditar-ventas`, body)
      .pipe(catchError(this.handleError));
  }

  /** Corrección de incoherencias en ventas Completadas (ajusta cantidades y devuelve al lote solo lo necesario). */
  postCorregirVentasCompletadas(body: CorregirVentasCompletadasRequest): Observable<CorregirVentasCompletadasResponse> {
    return this.http
      .post<CorregirVentasCompletadasResponse>(`${this.base}/corregir-ventas-completadas`, body)
      .pipe(catchError(this.handleError));
  }

  /** Venta por granja: cabecera de despacho + líneas; crea todos los movimientos Pendiente en una transacción. */
  createVentaGranjaDespacho(dto: CreateVentaGranjaDespachoDto): Observable<VentaGranjaDespachoResultDto> {
    return this.http
      .post<VentaGranjaDespachoResultDto>(`${this.base}/venta-granja-despacho`, dto)
      .pipe(catchError(this.handleError));
  }

  /** Completa varios movimientos Pendiente en una sola transacción en servidor. */
  completarBatch(movimientoIds: number[]): Observable<MovimientoPolloEngordeDto[]> {
    return this.http
      .post<MovimientoPolloEngordeDto[]>(`${this.base}/completar-batch`, { movimientoIds })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: HttpErrorResponse): Observable<never> {
    const msg =
      error.error?.error ?? error.error?.message ?? (error.status === 0 ? 'Error de red' : `Error ${error.status}`);
    return throwError(() => new Error(msg));
  }
}
