import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';

// =====================================================
// INTERFACES PARA TRASLADOS DE HUEVOS
// =====================================================

export interface CrearTrasladoHuevosDto {
  loteId: string;
  fechaTraslado: Date;
  tipoOperacion: string; // "Venta" o "Traslado"
  cantidadLimpio: number;
  cantidadTratado: number;
  cantidadSucio: number;
  cantidadDeforme: number;
  cantidadBlanco: number;
  cantidadDobleYema: number;
  cantidadPiso: number;
  cantidadPequeno: number;
  cantidadRoto: number;
  cantidadDesecho: number;
  cantidadOtro: number;
  granjaDestinoId?: number;
  loteDestinoId?: string;
  tipoDestino?: string; // "Granja" o "Planta"
  motivo?: string;
  descripcion?: string;
  observaciones?: string;
}

export interface ActualizarTrasladoHuevosDto {
  fechaTraslado?: Date;
  tipoOperacion?: string;
  cantidadLimpio?: number;
  cantidadTratado?: number;
  cantidadSucio?: number;
  cantidadDeforme?: number;
  cantidadBlanco?: number;
  cantidadDobleYema?: number;
  cantidadPiso?: number;
  cantidadPequeno?: number;
  cantidadRoto?: number;
  cantidadDesecho?: number;
  cantidadOtro?: number;
  granjaDestinoId?: number;
  loteDestinoId?: string;
  tipoDestino?: string;
  motivo?: string;
  descripcion?: string;
  observaciones?: string;
}

export interface TrasladoHuevosDto {
  id: number;
  numeroTraslado: string;
  fechaTraslado: Date;
  tipoOperacion: string;
  loteId: string;
  loteNombre: string;
  granjaOrigenId: number;
  granjaOrigenNombre: string;
  granjaDestinoId?: number;
  granjaDestinoNombre?: string;
  loteDestinoId?: string;
  tipoDestino?: string;
  motivo?: string;
  descripcion?: string;
  cantidadLimpio: number;
  cantidadTratado: number;
  cantidadSucio: number;
  cantidadDeforme: number;
  cantidadBlanco: number;
  cantidadDobleYema: number;
  cantidadPiso: number;
  cantidadPequeno: number;
  cantidadRoto: number;
  cantidadDesecho: number;
  cantidadOtro: number;
  totalHuevos: number;
  estado: string;
  usuarioTrasladoId: number;
  usuarioNombre?: string;
  fechaProcesamiento?: Date;
  fechaCancelacion?: Date;
  observaciones?: string;
  createdAt: Date;
  updatedAt?: Date;
}

export interface DisponibilidadLoteDto {
  loteId: number;
  loteNombre: string;
  tipoLote: string; // "Levante" o "Produccion"
  huevos?: HuevosDisponiblesDto;
  granjaId: number;
  granjaNombre: string;
  nucleoId?: string;
  nucleoNombre?: string;
  galponId?: string;
  galponNombre?: string;
}

export interface HuevosDisponiblesDto {
  totalHuevos: number;
  totalHuevosIncubables: number;
  limpio: number;
  tratado: number;
  sucio: number;
  deforme: number;
  blanco: number;
  dobleYema: number;
  piso: number;
  pequeno: number;
  roto: number;
  desecho: number;
  otro: number;
  fechaUltimoRegistro?: Date;
  diasEnProduccion: number;
}

@Injectable({
  providedIn: 'root'
})
export class TrasladosHuevosService {
  private trasladosUrl = `${environment.apiUrl}/traslados`;

  constructor(private http: HttpClient) {}

  // =====================================================
  // DISPONIBILIDAD DE LOTES
  // =====================================================

  // Obtener disponibilidad de un lote
  getDisponibilidadLote(loteId: string): Observable<DisponibilidadLoteDto> {
    return this.http.get<DisponibilidadLoteDto>(`${this.trasladosUrl}/lote/${loteId}/disponibilidad`)
      .pipe(catchError(this.handleError));
  }

  // =====================================================
  // TRASLADOS DE HUEVOS
  // =====================================================

  // Crear traslado de huevos
  crearTrasladoHuevos(dto: CrearTrasladoHuevosDto): Observable<TrasladoHuevosDto> {
    return this.http.post<TrasladoHuevosDto>(`${this.trasladosUrl}/huevos`, dto)
      .pipe(catchError(this.handleError));
  }

  // Obtener traslado de huevos por ID
  getTrasladoHuevos(id: number): Observable<TrasladoHuevosDto> {
    return this.http.get<TrasladoHuevosDto>(`${this.trasladosUrl}/huevos/${id}`)
      .pipe(catchError(this.handleError));
  }

  // Obtener traslados de huevos por lote
  getTrasladosHuevosPorLote(loteId: string): Observable<TrasladoHuevosDto[]> {
    return this.http.get<TrasladoHuevosDto[]>(`${this.trasladosUrl}/huevos/lote/${loteId}`)
      .pipe(catchError(this.handleError));
  }

  // Obtener traslados de huevos por granja
  getTrasladosHuevosPorGranja(granjaId: number): Observable<TrasladoHuevosDto[]> {
    return this.http.get<TrasladoHuevosDto[]>(`${this.trasladosUrl}/huevos/granja/${granjaId}`)
      .pipe(catchError(this.handleError));
  }

  // Cancelar traslado de huevos
  cancelarTrasladoHuevos(trasladoId: number, motivo: string): Observable<boolean> {
    return this.http.post<boolean>(`${this.trasladosUrl}/huevos/${trasladoId}/cancelar`, { motivo })
      .pipe(catchError(this.handleError));
  }

  // Actualizar traslado de huevos
  actualizarTrasladoHuevos(id: number, dto: ActualizarTrasladoHuevosDto): Observable<TrasladoHuevosDto> {
    return this.http.put<TrasladoHuevosDto>(`${this.trasladosUrl}/huevos/${id}`, dto)
      .pipe(catchError(this.handleError));
  }

  // Procesar traslado de huevos
  procesarTrasladoHuevos(trasladoId: number): Observable<boolean> {
    return this.http.post<boolean>(`${this.trasladosUrl}/huevos/${trasladoId}/procesar`, {})
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
          errorMessage = 'Conflicto: No hay suficientes huevos para el traslado';
          break;
        case 500:
          errorMessage = 'Error interno del servidor';
          break;
        default:
          errorMessage = `Error ${error.status}: ${error.message}`;
      }
    }

    console.error('Error en TrasladosHuevosService:', error);
    return throwError(() => new Error(errorMessage));
  }
}
