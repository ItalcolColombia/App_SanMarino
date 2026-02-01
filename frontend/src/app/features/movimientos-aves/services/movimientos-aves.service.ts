import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';

// =====================================================
// INTERFACES PARA MOVIMIENTOS DE AVES
// =====================================================

export interface UbicacionMovimientoDto {
  loteId?: number;
  loteNombre?: string;
  granjaId?: number;
  granjaNombre?: string;
  nucleoId?: string;
  nucleoNombre?: string;
  galponId?: string;
  galponNombre?: string;
}

export interface CrearMovimientoAvesDto {
  fechaMovimiento: Date;
  tipoMovimiento: string; // "Traslado"
  loteOrigenId?: number;
  granjaOrigenId?: number;
  nucleoOrigenId?: string;
  galponOrigenId?: string;
  loteDestinoId?: number;
  granjaDestinoId?: number;
  nucleoDestinoId?: string;
  galponDestinoId?: string;
  cantidadHembras: number;
  cantidadMachos: number;
  cantidadMixtas: number;
  motivoMovimiento?: string;
  descripcion?: string;
  plantaDestino?: string;
  observaciones?: string;
}

export interface ActualizarMovimientoAvesDto {
  fechaMovimiento?: Date;
  tipoMovimiento?: string;
  loteOrigenId?: number;
  granjaOrigenId?: number;
  nucleoOrigenId?: string;
  galponOrigenId?: string;
  loteDestinoId?: number;
  granjaDestinoId?: number;
  nucleoDestinoId?: string;
  galponDestinoId?: string;
  cantidadHembras?: number;
  cantidadMachos?: number;
  cantidadMixtas?: number;
  motivoMovimiento?: string;
  descripcion?: string;
  plantaDestino?: string;
  observaciones?: string;
}

export interface MovimientoAvesDto {
  id: number;
  numeroMovimiento: string;
  fechaMovimiento: Date;
  tipoMovimiento: string;
  origen?: UbicacionMovimientoDto;
  destino?: UbicacionMovimientoDto;
  cantidadHembras: number;
  cantidadMachos: number;
  cantidadMixtas: number;
  totalAves: number;
  estado: string;
  motivoMovimiento?: string;
  observaciones?: string;
  usuarioMovimientoId: number;
  usuarioNombre?: string;
  fechaProcesamiento?: Date;
  fechaCancelacion?: Date;
  createdAt: Date;
}

export interface InformacionLoteDto {
  loteId: number;
  loteNombre: string;
  granjaId: number;
  granjaNombre: string;
  nucleoId?: string;
  nucleoNombre?: string;
  galponId?: string;
  galponNombre?: string;
  etapa: number; // Semana actual
  tipoLote: string; // "Levante" o "Produccion"
  // Aves iniciales (desde ProduccionLotes para Producción, desde Lotes para Levante)
  hembrasIniciales: number;
  machosIniciales: number;
  // Aves actuales calculadas desde registros diarios
  cantidadHembras: number;
  cantidadMachos: number;
  cantidadMixtas: number;
  totalAves: number;
  fechaEncasetamiento?: Date;
  diasDesdeEncasetamiento?: number;
}

@Injectable({
  providedIn: 'root'
})
export class MovimientosAvesService {
  private movimientosUrl = `${environment.apiUrl}/MovimientoAves`;

  constructor(private http: HttpClient) {}

  // =====================================================
  // INFORMACIÓN DE LOTES
  // =====================================================

  // Obtener información de un lote
  getInformacionLote(loteId: number): Observable<InformacionLoteDto> {
    return this.http.get<InformacionLoteDto>(`${this.movimientosUrl}/lote/${loteId}/informacion`)
      .pipe(catchError(this.handleError));
  }

  // Obtener estado del lote (inventario)
  getEstadoLote(loteId: number): Observable<any> {
    return this.http.get<any>(`${environment.apiUrl}/InventarioAves/lote/${loteId}/estado`)
      .pipe(catchError(this.handleError));
  }

  // =====================================================
  // MOVIMIENTOS DE AVES
  // =====================================================

  // Crear movimiento de aves
  crearMovimientoAves(dto: CrearMovimientoAvesDto): Observable<MovimientoAvesDto> {
    return this.http.post<MovimientoAvesDto>(`${this.movimientosUrl}`, dto)
      .pipe(catchError(this.handleError));
  }

  // Obtener movimiento de aves por ID
  getMovimientoAves(id: number): Observable<MovimientoAvesDto> {
    return this.http.get<MovimientoAvesDto>(`${this.movimientosUrl}/${id}`)
      .pipe(catchError(this.handleError));
  }

  // Obtener movimientos de aves por lote
  getMovimientosAvesPorLote(loteId: number): Observable<MovimientoAvesDto[]> {
    return this.http.get<MovimientoAvesDto[]>(`${this.movimientosUrl}/lote/${loteId}`)
      .pipe(catchError(this.handleError));
  }

  // Obtener movimientos de aves por granja
  getMovimientosAvesPorGranja(granjaId: number): Observable<MovimientoAvesDto[]> {
    return this.http.get<MovimientoAvesDto[]>(`${this.movimientosUrl}/granja/${granjaId}`)
      .pipe(catchError(this.handleError));
  }

  // Actualizar movimiento de aves
  actualizarMovimientoAves(id: number, dto: ActualizarMovimientoAvesDto): Observable<MovimientoAvesDto> {
    return this.http.put<MovimientoAvesDto>(`${this.movimientosUrl}/${id}`, dto)
      .pipe(catchError(this.handleError));
  }

  // Cancelar movimiento de aves
  cancelarMovimientoAves(movimientoId: number, motivo: string): Observable<boolean> {
    return this.http.post<boolean>(`${this.movimientosUrl}/${movimientoId}/cancelar`, { motivoCancelacion: motivo })
      .pipe(catchError(this.handleError));
  }

  // Procesar movimiento de aves
  procesarMovimientoAves(movimientoId: number): Observable<boolean> {
    return this.http.post<boolean>(`${this.movimientosUrl}/${movimientoId}/procesar`, {})
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
          errorMessage = 'Conflicto: No hay suficientes aves para el movimiento';
          break;
        case 500:
          errorMessage = 'Error interno del servidor';
          break;
        default:
          errorMessage = `Error ${error.status}: ${error.message}`;
      }
    }

    console.error('Error en MovimientosAvesService:', error);
    return throwError(() => new Error(errorMessage));
  }
}
