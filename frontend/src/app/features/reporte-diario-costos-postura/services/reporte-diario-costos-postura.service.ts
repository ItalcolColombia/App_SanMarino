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
  private readonly lotesBaseUrl = `${environment.apiUrl}/LotePosturaBase`;
  private readonly http = inject(HttpClient);

  generar(request: ReporteDiarioCostosPosturaRequest): Observable<ReporteDiarioCostosPosturaReporte> {
    return this.http.post<ReporteDiarioCostosPosturaReporte>(`${this.baseUrl}/generar`, request);
  }

  /** Catálogo de lotes base de postura para el filtro. */
  lotesBase(): Observable<LotePosturaBaseOpcion[]> {
    return this.http.get<LotePosturaBaseOpcion[]>(this.lotesBaseUrl);
  }
}
