// src/app/features/reporte-contable/services/reporte-contable.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface ConsumoDiarioContableDto {
  fecha: string;
  loteId: number;
  loteNombre: string;
  consumoAlimento: number;
  consumoAgua: number;
  consumoMedicamento: number;
  consumoVacuna: number;
  otrosConsumos: number;
  totalConsumo: number;
}

export interface ReporteContableSemanalDto {
  semanaContable: number;
  fechaInicio: string;
  fechaFin: string;
  lotePadreId: number;
  lotePadreNombre: string;
  sublotes: string[];
  consumoTotalAlimento: number;
  consumoTotalAgua: number;
  consumoTotalMedicamento: number;
  consumoTotalVacuna: number;
  otrosConsumos: number;
  totalGeneral: number;
  consumosDiarios: ConsumoDiarioContableDto[];
}

export interface ReporteContableCompletoDto {
  lotePadreId: number;
  lotePadreNombre: string;
  granjaId: number;
  granjaNombre: string;
  nucleoId: string;
  nucleoNombre: string;
  fechaPrimeraLlegada: string;
  semanaContableActual: number;
  fechaInicioSemanaActual: string;
  fechaFinSemanaActual: string;
  reportesSemanales: ReporteContableSemanalDto[];
}

export interface GenerarReporteContableRequestDto {
  lotePadreId: number;
  semanaContable?: number;
  fechaInicio?: string;
  fechaFin?: string;
}

@Injectable({
  providedIn: 'root'
})
export class ReporteContableService {
  private readonly apiUrl = `${environment.apiUrl}/ReporteContable`;

  constructor(private http: HttpClient) {}

  /**
   * Genera el reporte contable para un lote padre
   */
  generarReporte(request: GenerarReporteContableRequestDto): Observable<ReporteContableCompletoDto> {
    let params = new HttpParams()
      .set('lotePadreId', request.lotePadreId.toString());

    if (request.semanaContable) {
      params = params.set('semanaContable', request.semanaContable.toString());
    }
    if (request.fechaInicio) {
      params = params.set('fechaInicio', request.fechaInicio);
    }
    if (request.fechaFin) {
      params = params.set('fechaFin', request.fechaFin);
    }

    return this.http.get<ReporteContableCompletoDto>(`${this.apiUrl}/generar`, { params });
  }

  /**
   * Obtiene las semanas contables disponibles para un lote padre
   */
  obtenerSemanasContables(lotePadreId: number): Observable<number[]> {
    return this.http.get<number[]>(`${this.apiUrl}/semanas-contables/${lotePadreId}`);
  }

  /**
   * Exporta el reporte contable a Excel
   */
  exportarExcel(request: GenerarReporteContableRequestDto): Observable<Blob> {
    let params = new HttpParams()
      .set('lotePadreId', request.lotePadreId.toString());

    if (request.semanaContable) {
      params = params.set('semanaContable', request.semanaContable.toString());
    }
    if (request.fechaInicio) {
      params = params.set('fechaInicio', request.fechaInicio);
    }
    if (request.fechaFin) {
      params = params.set('fechaFin', request.fechaFin);
    }

    return this.http.get(`${this.apiUrl}/exportar/excel`, {
      params,
      responseType: 'blob'
    });
  }
}

