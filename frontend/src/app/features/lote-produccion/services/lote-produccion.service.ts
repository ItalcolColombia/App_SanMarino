// src/app/features/lote-produccion/services/lote-produccion.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';

export interface LoteProduccionDto {
  id: number;
  fecha: string;
  loteId: number;
  mortalidadH: number;
  mortalidadM: number;
  selH: number;
  consKgH: number;
  consKgM: number;
  huevoTot: number;
  huevoInc: number;
  tipoAlimento: string;
  observaciones?: string;
  pesoHuevo: number;
  etapa: 1|2|3;
}

export interface CreateLoteProduccionDto extends Omit<LoteProduccionDto,'id'> {}
export interface UpdateLoteProduccionDto extends LoteProduccionDto {}

@Injectable({ providedIn: 'root' })
export class LoteProduccionService {
  private readonly base = `${environment.apiUrl}/ProduccionDiaria`;
  private readonly http = inject(HttpClient);

  constructor() {}

  /** Obtener todos los registros */
  getAll(): Observable<LoteProduccionDto[]> {
    return this.http.get<LoteProduccionDto[]>(this.base)
      .pipe(catchError(this.handleError));
  }

  /** Obtener registros por lote */
  getByLote(loteId: number): Observable<LoteProduccionDto[]> {
    return this.http.get<LoteProduccionDto[]>(`${this.base}/${loteId}`)
      .pipe(catchError(this.handleError));
  }

  /** Crear nuevo registro */
  create(dto: CreateLoteProduccionDto): Observable<LoteProduccionDto> {
    return this.http.post<LoteProduccionDto>(this.base, dto)
      .pipe(catchError(this.handleError));
  }

  /** Actualizar registro */
  update(dto: UpdateLoteProduccionDto): Observable<LoteProduccionDto> {
    return this.http.put<LoteProduccionDto>(`${this.base}/${dto.id}`, dto)
      .pipe(catchError(this.handleError));
  }

  /** Eliminar registro */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`)
      .pipe(catchError(this.handleError));
  }

  /** Filtrar registros */
  filter(loteId?: number, desde?: string, hasta?: string): Observable<LoteProduccionDto[]> {
    let params = '';
    if (loteId) params += `loteId=${loteId}`;
    if (desde) params += (params ? '&' : '') + `desde=${desde}`;
    if (hasta) params += (params ? '&' : '') + `hasta=${hasta}`;
    
    const url = params ? `${this.base}/filter?${params}` : `${this.base}/filter`;
    return this.http.get<LoteProduccionDto[]>(url)
      .pipe(catchError(this.handleError));
  }

  /** Verificar si un lote tiene configuración de ProduccionLote */
  checkProduccionLoteConfig(loteId: string): Observable<{ hasProduccionLoteConfig: boolean }> {
    return this.http.get<{ hasProduccionLoteConfig: boolean }>(`${this.base}/check-config/${loteId}`)
      .pipe(catchError(this.handleError));
  }

  private handleError(err: HttpErrorResponse) {
    const message = 
      (err.error && (err.error.detail || err.error.title)) ||
      (typeof err.error === 'string' ? err.error : 'Error inesperado en el servidor.');
    return throwError(() => ({ status: err.status, message }));
  }
}
