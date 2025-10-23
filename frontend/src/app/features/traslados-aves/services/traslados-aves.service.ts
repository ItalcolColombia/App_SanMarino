import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';

// =====================================================
// INTERFACES PRINCIPALES
// =====================================================

// Inventario de Aves
export interface InventarioAvesDto {
  id: number;
  companyId: number;
  loteId: string;
  granjaId: number;
  nucleoId: string;
  galponId?: string;
  cantidadHembras: number;
  cantidadMachos: number;
  fechaUltimoConteo: Date;
  createdAt: Date;
  updatedAt?: Date;
}

export interface CreateInventarioAvesDto {
  loteId: string;
  granjaId: number;
  nucleoId: string;
  galponId?: string;
  cantidadHembras: number;
  cantidadMachos: number;
  fechaUltimoConteo: Date;
}

export interface UpdateInventarioAvesDto extends CreateInventarioAvesDto {
  id: number;
}

export interface InventarioAvesSearchRequest {
  loteId?: string;
  granjaId?: number;
  nucleoId?: string;
  galponId?: string;
  estado?: string;
  fechaDesde?: Date;
  fechaHasta?: Date;
  soloActivos?: boolean;
  sortBy?: string;
  sortDesc?: boolean;
  page: number;
  pageSize: number;
}

// Movimiento de Aves
export interface MovimientoAvesDto {
  id: number;
  companyId: number;
  loteOrigenId: string;
  loteDestinoId: string;
  cantidadHembras: number;
  cantidadMachos: number;
  tipoMovimiento: string;
  observaciones?: string;
  fechaMovimiento: Date;
  createdAt: Date;
  updatedAt?: Date;
}

export interface CreateMovimientoAvesDto {
  loteOrigenId: string;
  loteDestinoId: string;
  cantidadHembras: number;
  cantidadMachos: number;
  tipoMovimiento: string;
  observaciones?: string;
  fechaMovimiento: Date;
}

export interface MovimientoAvesSearchRequest {
  numeroMovimiento?: string;
  tipoMovimiento?: string;
  estado?: string;
  loteOrigenId?: string;
  loteDestinoId?: string;
  granjaOrigenId?: number;
  granjaDestinoId?: number;
  fechaDesde?: Date;
  fechaHasta?: Date;
  usuarioMovimientoId?: number;
  sortBy?: string;
  sortDesc?: boolean;
  page: number;
  pageSize: number;
}

// Historial de Inventario
export interface HistorialInventarioDto {
  id: number;
  companyId: number;
  loteId: string;
  cantidadHembrasAntes: number;
  cantidadMachosAntes: number;
  cantidadHembrasDespues: number;
  cantidadMachosDespues: number;
  tipoEvento: string;
  referenciaMovimientoId?: string;
  fechaRegistro: Date;
  createdAt: Date;
  updatedAt?: Date;
}

export interface HistorialInventarioSearchRequest {
  inventarioId?: number;
  loteId?: string;
  tipoCambio?: string;
  movimientoId?: number;
  granjaId?: number;
  nucleoId?: string;
  galponId?: string;
  fechaDesde?: Date;
  fechaHasta?: Date;
  usuarioCambioId?: number;
  sortBy?: string;
  sortDesc?: boolean;
  page: number;
  pageSize: number;
}

// Interfaces Auxiliares
export interface ResumenInventarioDto {
  totalLotes: number;
  totalHembras: number;
  totalMachos: number;
  totalAves: number;
  resumenPorGranja: ResumenPorGranjaDto[];
}

export interface ResumenPorGranjaDto {
  granjaId: number;
  granjaNombre: string;
  nucleoId: string;
  nucleoNombre?: string;
  galponId?: string;
  galponNombre?: string;
  cantidadLotes: number;
  totalHembras: number;
  totalMachos: number;
  totalAves: number;
  fechaUltimaActualizacion: Date;
}

export interface TrasladoRapidoRequest {
  loteOrigenId: string;
  loteDestinoId: string;
  cantidadHembras: number;
  cantidadMachos: number;
  observaciones?: string;
}

export interface TrasladoRapidoResponse {
  success: boolean;
  message: string;
  movimientoId?: number;
  inventarioOrigenActualizado?: {
    loteId: string;
    cantidadHembras: number;
    cantidadMachos: number;
  };
  inventarioDestinoActualizado?: {
    loteId: string;
    cantidadHembras: number;
    cantidadMachos: number;
  };
}

export interface EventoTrazabilidadDto {
  fecha: Date;
  tipoEvento: string;
  descripcion: string;
  cantidadHembrasAntes: number;
  cantidadMachosAntes: number;
  cantidadHembrasDespues: number;
  cantidadMachosDespues: number;
  usuario?: string;
  referenciaMovimiento?: string;
}

export interface TrazabilidadLoteDto {
  loteId: string;
  eventos: EventoTrazabilidadDto[];
}

export interface AjusteInventarioRequest {
  cantidadHembras: number;
  cantidadMachos: number;
  tipoEvento: string;
  observaciones?: string;
}

// Resultado paginado
export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

@Injectable({
  providedIn: 'root'
})
export class TrasladosAvesService {
  private inventarioUrl = `${environment.apiUrl}/InventarioAves`;
  private movimientoUrl = `${environment.apiUrl}/MovimientoAves`;
  private historialUrl = `${environment.apiUrl}/HistorialInventario`;

  constructor(private http: HttpClient) {}

  // =====================================================
  // INVENTARIO DE AVES
  // =====================================================

  // Obtener inventario por ID
  getInventarioById(id: number): Observable<InventarioAvesDto> {
    return this.http.get<InventarioAvesDto>(`${this.inventarioUrl}/${id}`)
      .pipe(catchError(this.handleError));
  }

  // Obtener inventario por lote
  getInventarioByLote(loteId: string): Observable<InventarioAvesDto> {
    return this.http.get<InventarioAvesDto>(`${this.inventarioUrl}/lote/${loteId}`)
      .pipe(catchError(this.handleError));
  }

  // Búsqueda de inventarios
  searchInventarios(request: InventarioAvesSearchRequest): Observable<PagedResult<InventarioAvesDto>> {
    return this.http.post<PagedResult<InventarioAvesDto>>(`${this.inventarioUrl}/search`, request)
      .pipe(catchError(this.handleError));
  }

  // Crear inventario
  createInventario(dto: CreateInventarioAvesDto): Observable<InventarioAvesDto> {
    return this.http.post<InventarioAvesDto>(this.inventarioUrl, dto)
      .pipe(catchError(this.handleError));
  }

  // Actualizar inventario
  updateInventario(id: number, dto: UpdateInventarioAvesDto): Observable<InventarioAvesDto> {
    return this.http.put<InventarioAvesDto>(`${this.inventarioUrl}/${id}`, dto)
      .pipe(catchError(this.handleError));
  }

  // Eliminar inventario
  deleteInventario(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.inventarioUrl}/${id}`)
      .pipe(catchError(this.handleError));
  }

  // Ajustar cantidades
  ajustarInventario(loteId: string, ajuste: AjusteInventarioRequest): Observable<InventarioAvesDto> {
    return this.http.post<InventarioAvesDto>(`${this.inventarioUrl}/ajustar/${loteId}`, ajuste)
      .pipe(catchError(this.handleError));
  }

  // Obtener resumen
  getResumenInventario(): Observable<ResumenInventarioDto> {
    return this.http.get<ResumenInventarioDto>(`${this.inventarioUrl}/resumen`)
      .pipe(catchError(this.handleError));
  }

  // =====================================================
  // MOVIMIENTOS DE AVES
  // =====================================================

  // Crear movimiento
  createMovimiento(dto: CreateMovimientoAvesDto): Observable<MovimientoAvesDto> {
    return this.http.post<MovimientoAvesDto>(this.movimientoUrl, dto)
      .pipe(catchError(this.handleError));
  }

  // Obtener movimiento por ID
  getMovimientoById(id: number): Observable<MovimientoAvesDto> {
    return this.http.get<MovimientoAvesDto>(`${this.movimientoUrl}/${id}`)
      .pipe(catchError(this.handleError));
  }

  // Búsqueda de movimientos
  searchMovimientos(request: MovimientoAvesSearchRequest): Observable<PagedResult<MovimientoAvesDto>> {
    return this.http.post<PagedResult<MovimientoAvesDto>>(`${this.movimientoUrl}/search`, request)
      .pipe(catchError(this.handleError));
  }

  // Traslado rápido
  trasladoRapido(request: TrasladoRapidoRequest): Observable<TrasladoRapidoResponse> {
    return this.http.post<TrasladoRapidoResponse>(`${this.movimientoUrl}/traslado-rapido`, request)
      .pipe(catchError(this.handleError));
  }

  // Procesar movimiento
  procesarMovimiento(id: number): Observable<MovimientoAvesDto> {
    return this.http.post<MovimientoAvesDto>(`${this.movimientoUrl}/${id}/procesar`, {})
      .pipe(catchError(this.handleError));
  }

  // Cancelar movimiento
  cancelarMovimiento(id: number, motivo: string): Observable<MovimientoAvesDto> {
    return this.http.post<MovimientoAvesDto>(`${this.movimientoUrl}/${id}/cancelar`, { motivoCancelacion: motivo })
      .pipe(catchError(this.handleError));
  }

  // =====================================================
  // HISTORIAL DE INVENTARIO
  // =====================================================

  // Obtener historial por ID
  getHistorialById(id: number): Observable<HistorialInventarioDto> {
    return this.http.get<HistorialInventarioDto>(`${this.historialUrl}/${id}`)
      .pipe(catchError(this.handleError));
  }

  // Búsqueda en historial
  searchHistorial(request: HistorialInventarioSearchRequest): Observable<PagedResult<HistorialInventarioDto>> {
    return this.http.post<PagedResult<HistorialInventarioDto>>(`${this.historialUrl}/search`, request)
      .pipe(catchError(this.handleError));
  }

  // Obtener trazabilidad de lote
  getTrazabilidadLote(loteId: string): Observable<TrazabilidadLoteDto> {
    return this.http.get<TrazabilidadLoteDto>(`${this.historialUrl}/trazabilidad/${loteId}`)
      .pipe(catchError(this.handleError));
  }

  // =====================================================
  // MÉTODOS AUXILIARES
  // =====================================================

  // Validar traslado
  validarTraslado(loteOrigenId: string, loteDestinoId: string, cantidadHembras: number, cantidadMachos: number): Observable<boolean> {
    const request = {
      loteOrigenId,
      loteDestinoId,
      cantidadHembras,
      cantidadMachos
    };
    return this.http.post<boolean>(`${this.movimientoUrl}/validar`, request)
      .pipe(catchError(this.handleError));
  }

  // Obtener lotes disponibles
  getLotesDisponibles(): Observable<string[]> {
    return this.http.get<string[]>(`${this.inventarioUrl}/lotes-disponibles`)
      .pipe(catchError(this.handleError));
  }

  // Manejo de errores
  private handleError(error: HttpErrorResponse): Observable<never> {
    let errorMessage = 'Error desconocido';
    
    if (error.error instanceof ErrorEvent) {
      errorMessage = `Error: ${error.error.message}`;
    } else {
      switch (error.status) {
        case 400:
          errorMessage = 'Datos inválidos en la solicitud';
          break;
        case 401:
          errorMessage = 'No autorizado. Inicie sesión nuevamente';
          break;
        case 404:
          errorMessage = 'Recurso no encontrado';
          break;
        case 409:
          errorMessage = 'Conflicto: No hay suficientes aves para el traslado';
          break;
        case 500:
          errorMessage = 'Error interno del servidor';
          break;
        default:
          errorMessage = `Error ${error.status}: ${error.message}`;
      }
    }
    
    console.error('Error en TrasladosAvesService:', error);
    return throwError(() => new Error(errorMessage));
  }
}
