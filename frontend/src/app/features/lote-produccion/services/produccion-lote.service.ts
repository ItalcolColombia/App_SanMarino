// src/app/features/lote-produccion/services/produccion-lote.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';

export interface ProduccionLoteDto {
  id: number;
  loteId: number;
  fechaInicioProduccion: string;
  hembrasIniciales: number;
  machosIniciales: number;
  huevosIniciales: number;
  tipoNido: string;
  nucleoProduccionId: string;
  granjaId: number;
  ciclo: string;
}

export interface CreateProduccionLoteDto extends Omit<ProduccionLoteDto,'id'> {}
export interface UpdateProduccionLoteDto extends ProduccionLoteDto {}

@Injectable({ providedIn: 'root' })
export class ProduccionLoteService {
  private readonly base = `${environment.apiUrl}/ProduccionLote`;
  private readonly http = inject(HttpClient);

  constructor() {}

  /** Obtener todos los registros */
  getAll(): Observable<ProduccionLoteDto[]> {
    return this.http.get<ProduccionLoteDto[]>(this.base)
      .pipe(catchError(this.handleError));
  }

  /** Obtener registro por lote */
  getByLote(loteId: number): Observable<ProduccionLoteDto | null> {
    return this.http.get<ProduccionLoteDto | null>(`${this.base}/lote/${loteId}`)
      .pipe(catchError(this.handleError));
  }

  /** Crear nuevo registro */
  create(dto: CreateProduccionLoteDto): Observable<ProduccionLoteDto> {
    return this.http.post<ProduccionLoteDto>(this.base, dto)
      .pipe(catchError(this.handleError));
  }

  /** Actualizar registro */
  update(dto: UpdateProduccionLoteDto): Observable<ProduccionLoteDto> {
    return this.http.put<ProduccionLoteDto>(`${this.base}/${dto.id}`, dto)
      .pipe(catchError(this.handleError));
  }

  /** Eliminar registro */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`)
      .pipe(catchError(this.handleError));
  }

  private handleError(err: HttpErrorResponse) {
    const message = 
      (err.error && (err.error.detail || err.error.title)) ||
      (typeof err.error === 'string' ? err.error : 'Error inesperado en el servidor.');
    return throwError(() => ({ status: err.status, message }));
  }
}



