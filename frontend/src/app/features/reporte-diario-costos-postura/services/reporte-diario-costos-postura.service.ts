// frontend/src/app/features/reporte-diario-costos-postura/services/reporte-diario-costos-postura.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  LotePosturaBaseOpcion,
  ReporteDiarioCostosPosturaReporte,
  ReporteDiarioCostosPosturaRequest
} from '../models/reporte-diario-costos-postura.model';

@Injectable({ providedIn: 'root' })
export class ReporteDiarioCostosPosturaService {
  private readonly baseUrl = `${environment.apiUrl}/ReporteDiarioCostosPostura`;
  private readonly http = inject(HttpClient);

  generar(request: ReporteDiarioCostosPosturaRequest): Observable<ReporteDiarioCostosPosturaReporte> {
    return this.http.post<ReporteDiarioCostosPosturaReporte>(`${this.baseUrl}/generar`, request);
  }

  /**
   * Catálogo de lotes base para el filtro. Endpoint propio del reporte (no `GET /api/LotePosturaBase`):
   * lista cada base con las granjas donde REALMENTE tiene lotes, así una base cuyo levante se hizo en
   * una granja y su producción en otra sigue apareciendo bajo las dos.
   */
  lotesBase(): Observable<LotePosturaBaseOpcion[]> {
    return this.http.get<LotePosturaBaseOpcion[]>(`${this.baseUrl}/lotes-base`);
  }
}
