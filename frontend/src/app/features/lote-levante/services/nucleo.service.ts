// src/app/features/lote-levante/services/nucleo.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';

export interface NucleoDto {
  nucleoId: string;
  granjaId: number;
  nucleoNombre: string;
}

@Injectable({ providedIn: 'root' })
export class NucleoService {
  /**
   * Nota: environment.apiUrl debe apuntar a la raíz del API (p.ej. http://localhost:5002/api)
   */
  private readonly base = `${environment.apiUrl}`;

  constructor(private http: HttpClient) {}

  /** Lista todos los núcleos (si tu backend lo expone).
   *  paraDestino=true: catálogo para elegir DESTINO de traslados (omite el alcance granular del usuario). */
  getAll(paraDestino = false): Observable<NucleoDto[]> {
    const qs = paraDestino ? '?paraDestino=true' : '';
    return this.http
      .get<NucleoDto[]>(`${this.base}/Nucleo${qs}`)
      .pipe(catchError(this.handleError));
  }

  /** Núcleos pertenecientes a una granja (endpoint real del backend).
   *  paraDestino=true: catálogo para elegir DESTINO de traslados (omite el alcance granular del usuario). */
  getByGranja(granjaId: number, paraDestino = false): Observable<NucleoDto[]> {
    const qs = paraDestino ? '?paraDestino=true' : '';
    return this.http
      .get<NucleoDto[]>(`${this.base}/Nucleo/granja/${encodeURIComponent(String(granjaId))}${qs}`)
      .pipe(catchError(this.handleError));
  }

  // ───────── helpers ─────────
  private handleError(err: HttpErrorResponse) {
    const message =
      (err.error && (err.error.detail || err.error.title)) ||
      (typeof err.error === 'string' ? err.error : 'Error inesperado en el servidor.');
    return throwError(() => ({ status: err.status, message }));
  }
}
