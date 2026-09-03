// src/app/features/traslados-aves/services/inventario-aves.service.ts
// Inventario de aves (`/api/InventarioAves`).
//
// Extraido de `TrasladosAvesService` (3-sep-2026), que mezclaba 4 dominios en 747 lineas.
// Las rutas, los payloads y los mensajes de error son los mismos: solo cambio de archivo.
import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import { manejarErrorHttp } from '../funciones/manejar-error-http.funcion';
import {
  AjusteInventarioRequest,
  InventarioAvesDto,
  InventarioAvesSearchRequest,
  PagedResult,
  ResumenInventarioDto
} from '../models';

@Injectable({ providedIn: 'root' })
export class InventarioAvesService {
  private inventarioUrl = `${environment.apiUrl}/InventarioAves`;

  constructor(private http: HttpClient) {}

  // Búsqueda de inventarios
  searchInventarios(request: InventarioAvesSearchRequest): Observable<PagedResult<InventarioAvesDto>> {
    return this.http.post<PagedResult<InventarioAvesDto>>(`${this.inventarioUrl}/search`, request)
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

  private handleError(error: HttpErrorResponse): Observable<never> {
    return manejarErrorHttp(error, 'InventarioAvesService');
  }
}
