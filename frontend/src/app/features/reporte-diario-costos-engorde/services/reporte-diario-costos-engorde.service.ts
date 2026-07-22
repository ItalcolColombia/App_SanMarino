// frontend/src/app/features/reporte-diario-costos-engorde/services/reporte-diario-costos-engorde.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  ReporteDiarioCostosReporte,
  ReporteDiarioCostosRequest
} from '../models/reporte-diario-costos.model';

@Injectable({ providedIn: 'root' })
export class ReporteDiarioCostosEngordeService {
  private readonly baseUrl = `${environment.apiUrl}/ReporteDiarioCostosEngorde`;
  private readonly http = inject(HttpClient);

  generar(request: ReporteDiarioCostosRequest): Observable<ReporteDiarioCostosReporte> {
    return this.http.post<ReporteDiarioCostosReporte>(`${this.baseUrl}/generar`, request);
  }
}
