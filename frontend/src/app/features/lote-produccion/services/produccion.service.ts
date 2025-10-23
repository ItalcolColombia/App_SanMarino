// src/app/features/lote-produccion/services/produccion.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface ExisteProduccionLoteResponse {
  exists: boolean;
  produccionLoteId?: number;
}

export interface CrearProduccionLoteRequest {
  loteId: number;
  fechaInicio: string; // ISO date
  avesInicialesH: number;
  avesInicialesM: number;
  observaciones?: string;
}

export interface ProduccionLoteDetalleDto {
  id: number;
  loteId: number;
  fechaInicio: string; // ISO date
  avesInicialesH: number;
  avesInicialesM: number;
  observaciones?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface CrearSeguimientoRequest {
  produccionLoteId: number;
  fechaRegistro: string; // ISO date
  mortalidadH: number;
  mortalidadM: number;
  consumoKg: number;
  huevosTotales: number;
  huevosIncubables: number;
  pesoHuevo: number;
  observaciones?: string;
}

export interface SeguimientoItemDto {
  id: number;
  produccionLoteId: number;
  fechaRegistro: string; // ISO date
  mortalidadH: number;
  mortalidadM: number;
  consumoKg: number;
  huevosTotales: number;
  huevosIncubables: number;
  pesoHuevo: number;
  observaciones?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface ListaSeguimientoResponse {
  items: SeguimientoItemDto[];
  total: number;
}

export interface ListaSeguimientoQuery {
  loteId: number;
  desde?: string;
  hasta?: string;
  page?: number;
  size?: number;
}

@Injectable({
  providedIn: 'root'
})
export class ProduccionService {
  private readonly baseUrl = `${environment.apiUrl}/api/Produccion`;

  constructor(private http: HttpClient) {}

  /**
   * Verifica si existe un registro inicial de producción para un lote
   */
  existsProduccionLote(loteId: number): Observable<ExisteProduccionLoteResponse> {
    return this.http.get<ExisteProduccionLoteResponse>(`${this.baseUrl}/lotes/${loteId}/exists`);
  }

  /**
   * Crea un nuevo registro inicial de producción para un lote
   */
  crearProduccionLote(payload: CrearProduccionLoteRequest): Observable<number> {
    return this.http.post<number>(`${this.baseUrl}/lotes`, payload);
  }

  /**
   * Obtiene el detalle del registro inicial de producción de un lote
   */
  getProduccionLote(loteId: number): Observable<ProduccionLoteDetalleDto> {
    return this.http.get<ProduccionLoteDetalleDto>(`${this.baseUrl}/lotes/${loteId}`);
  }

  /**
   * Crea un nuevo seguimiento diario de producción
   */
  crearSeguimiento(payload: CrearSeguimientoRequest): Observable<number> {
    return this.http.post<number>(`${this.baseUrl}/seguimiento`, payload);
  }

  /**
   * Lista los seguimientos diarios de producción de un lote
   */
  listarSeguimiento(query: ListaSeguimientoQuery): Observable<ListaSeguimientoResponse> {
    let params = new HttpParams();
    
    params = params.set('loteId', query.loteId.toString());
    
    if (query.desde) {
      params = params.set('desde', query.desde);
    }
    
    if (query.hasta) {
      params = params.set('hasta', query.hasta);
    }
    
    if (query.page) {
      params = params.set('page', query.page.toString());
    }
    
    if (query.size) {
      params = params.set('size', query.size.toString());
    }

    return this.http.get<ListaSeguimientoResponse>(`${this.baseUrl}/seguimiento`, { params });
  }
}
