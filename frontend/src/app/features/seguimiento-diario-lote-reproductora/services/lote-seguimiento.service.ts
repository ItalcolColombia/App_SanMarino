// app/features/lote-reproductora/services/lote-seguimiento.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';

/* ===========================
   DTOs alineados al componente
   =========================== */
export interface LoteSeguimientoDto {
  id: number;
  fecha: string;             // ISO (p.ej. '2025-03-10T00:00:00.000Z')
  loteId: string;
  reproductoraId: string;
  mortalidadH: number;
  mortalidadM: number;
  selH: number;
  selM: number;
  errorH: number;
  errorM: number;
  tipoAlimento: string;
  consumoAlimento: number;   // ← en UI: consumoKgHembras
  observaciones?: string | null;
  pesoInicial?: number | null; // opcionales para extender
  pesoFinal?: number | null;   // opcionales para extender
}

export type CreateLoteSeguimientoDto = Omit<LoteSeguimientoDto, 'id'>;
export type UpdateLoteSeguimientoDto = LoteSeguimientoDto;

@Injectable({ providedIn: 'root' })
export class LoteSeguimientoService {
  private readonly base = `${environment.apiUrl}/LoteSeguimiento`;

  constructor(private http: HttpClient) {}

  // Helpers
  private toIsoIfDateLike(v: string): string {
    // Acepta 'yyyy-MM-dd' o ISO y devuelve ISO
    const d = new Date(v);
    return isNaN(d.getTime()) ? v : d.toISOString();
  }
  private handleError = (err: HttpErrorResponse) =>
    throwError(() => ({
      status: err.status,
      message:
        (err.error?.detail ?? err.error?.title) ||
        (typeof err.error === 'string' ? err.error : 'Error inesperado en el servidor.')
    }));

  // ====== Queries ======
  /** Trae seguimientos por lote y reproductora (como usa el componente). */
  getByLoteYRepro(loteId: string, reproductoraId: string): Observable<LoteSeguimientoDto[]> {
    const params = new HttpParams().set('loteId', loteId).set('reproductoraId', reproductoraId);
    return this.http.get<LoteSeguimientoDto[]>(this.base, { params }).pipe(catchError(this.handleError));
  }

  get(id: number): Observable<LoteSeguimientoDto> {
    return this.http.get<LoteSeguimientoDto>(`${this.base}/${id}`).pipe(catchError(this.handleError));
  }

  // ====== CRUD ======
  create(dto: CreateLoteSeguimientoDto): Observable<LoteSeguimientoDto> {
    const payload: CreateLoteSeguimientoDto = {
      ...dto,
      fecha: this.toIsoIfDateLike(dto.fecha)
    };
    return this.http.post<LoteSeguimientoDto>(this.base, payload).pipe(catchError(this.handleError));
  }

  update(dto: UpdateLoteSeguimientoDto): Observable<LoteSeguimientoDto> {
    const payload: UpdateLoteSeguimientoDto = {
      ...dto,
      fecha: this.toIsoIfDateLike(dto.fecha)
    };
    return this.http.put<LoteSeguimientoDto>(`${this.base}/${dto.id}`, payload).pipe(catchError(this.handleError));
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`).pipe(catchError(this.handleError));
  }
}
