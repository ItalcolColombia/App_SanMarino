// src/app/features/traslados-aves/services/historial-inventario.service.ts
// Historial y trazabilidad del inventario de aves (`/api/HistorialInventario`).
//
// Extraido de `TrasladosAvesService` (3-sep-2026). Mismas rutas y mismos mensajes de error.
import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import { manejarErrorHttp } from '../funciones/manejar-error-http.funcion';
import {
  HistorialInventarioDto,
  HistorialInventarioSearchRequest,
  PagedResult,
  TrazabilidadLoteDto
} from '../models';

@Injectable({ providedIn: 'root' })
export class HistorialInventarioService {
  private historialUrl = `${environment.apiUrl}/HistorialInventario`;

  constructor(private http: HttpClient) {}

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

  private handleError(error: HttpErrorResponse): Observable<never> {
    return manejarErrorHttp(error, 'HistorialInventarioService');
  }
}
