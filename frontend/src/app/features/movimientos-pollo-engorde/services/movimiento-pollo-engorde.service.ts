import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';

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

  search(params: {
    numeroMovimiento?: string;
    tipoMovimiento?: string;
    estado?: string;
    loteAveEngordeOrigenId?: number;
    loteReproductoraAveEngordeOrigenId?: number;
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

  private handleError(error: HttpErrorResponse): Observable<never> {
    const msg =
      error.error?.error ?? error.error?.message ?? (error.status === 0 ? 'Error de red' : `Error ${error.status}`);
    return throwError(() => new Error(msg));
  }
}
