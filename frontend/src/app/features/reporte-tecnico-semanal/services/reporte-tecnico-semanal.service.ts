// Servicio HTTP del Reporte Técnico Semanal (Sanmarino postura).
// Interfaces espejo del backend en ../models. Filtros: se reutiliza
// ReporteTecnicoLevanteFilterService (mismo filter-data que Reporte Técnico).
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  ReporteTecnicoSemanalLevanteResponse,
  ReporteTecnicoSemanalProduccionResponse,
  ReporteTecnicoSemanalRequest
} from '../models/reporte-tecnico-semanal.model';
import {
  ResumenSemanalRaPesadasLevanteResponse,
  ResumenSemanalRaPesadasProduccionResponse,
  ResumenSemanalRaPesadasRequest
} from '../models/resumen-semanal-ra-pesadas.model';

@Injectable({ providedIn: 'root' })
export class ReporteTecnicoSemanalService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/ReporteTecnicoSemanal`;

  generarLevante(request: ReporteTecnicoSemanalRequest): Observable<ReporteTecnicoSemanalLevanteResponse> {
    return this.http.post<ReporteTecnicoSemanalLevanteResponse>(`${this.baseUrl}/levante`, request);
  }

  generarProduccion(request: ReporteTecnicoSemanalRequest): Observable<ReporteTecnicoSemanalProduccionResponse> {
    return this.http.post<ReporteTecnicoSemanalProduccionResponse>(`${this.baseUrl}/produccion`, request);
  }

  /**
   * Hoja «RESUMEN SEMANAL»: N lotes de UNA semana calendario. El mismo endpoint
   * sirve las dos etapas; el tipo de respuesta lo fija `request.etapa`.
   */
  generarResumenLevante(
    request: ResumenSemanalRaPesadasRequest
  ): Observable<ResumenSemanalRaPesadasLevanteResponse> {
    return this.http.post<ResumenSemanalRaPesadasLevanteResponse>(
      `${this.baseUrl}/resumen`, { ...request, etapa: 'levante' });
  }

  generarResumenProduccion(
    request: ResumenSemanalRaPesadasRequest
  ): Observable<ResumenSemanalRaPesadasProduccionResponse> {
    return this.http.post<ResumenSemanalRaPesadasProduccionResponse>(
      `${this.baseUrl}/resumen`, { ...request, etapa: 'produccion' });
  }
}
