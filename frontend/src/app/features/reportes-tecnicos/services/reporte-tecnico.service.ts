// src/app/features/reportes-tecnicos/services/reporte-tecnico.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

// DTOs
export interface ReporteTecnicoDiarioDto {
  fecha: string;
  edadDias: number;
  edadSemanas: number;
  numeroAves: number;
  mortalidadTotal: number;
  mortalidadPorcentajeDiario: number;
  mortalidadPorcentajeAcumulado: number;
  errorSexajeNumero: number;
  errorSexajePorcentaje: number;
  errorSexajePorcentajeAcumulado: number;
  descarteNumero: number;
  descartePorcentajeDiario: number;
  descartePorcentajeAcumulado: number;
  consumoBultos: number;
  consumoKilos: number;
  consumoKilosAcumulado: number;
  consumoGramosPorAve: number;
  ingresosAlimentoKilos: number;
  trasladosAlimentoKilos: number;
  pesoActual?: number | null;
  uniformidad?: number | null;
  gananciaPeso?: number | null;
  coeficienteVariacion?: number | null;
  seleccionVentasNumero: number;
  seleccionVentasPorcentaje: number;
}

export interface ReporteTecnicoSemanalDto {
  semana: number;
  fechaInicio: string;
  fechaFin: string;
  edadInicioSemanas: number;
  edadFinSemanas: number;
  avesInicioSemana: number;
  avesFinSemana: number;
  mortalidadTotalSemana: number;
  mortalidadPorcentajeSemana: number;
  consumoKilosSemana: number;
  consumoGramosPorAveSemana: number;
  pesoPromedioSemana?: number | null;
  uniformidadPromedioSemana?: number | null;
  seleccionVentasSemana: number;
  ingresosAlimentoKilosSemana: number;
  trasladosAlimentoKilosSemana: number;
  detalleDiario: ReporteTecnicoDiarioDto[];
}

export interface ReporteTecnicoLoteInfoDto {
  loteId: number;
  loteNombre: string;
  sublote?: string | null;
  raza?: string | null;
  linea?: string | null;
  etapa?: string | null;
  fechaEncaset?: string | null;
  numeroHembras?: number | null;
  galpon?: number | null;
  tecnico?: string | null;
  granjaNombre?: string | null;
  nucleoNombre?: string | null;
}

export interface ReporteTecnicoCompletoDto {
  informacionLote: ReporteTecnicoLoteInfoDto;
  datosDiarios: ReporteTecnicoDiarioDto[];
  datosSemanales: ReporteTecnicoSemanalDto[];
  esConsolidado: boolean;
  sublotesIncluidos: string[];
}

export interface GenerarReporteTecnicoRequestDto {
  loteId?: number | null;
  loteNombre?: string | null;
  sublote?: string | null;
  fechaInicio?: string | null;
  fechaFin?: string | null;
  incluirSemanales?: boolean;
  consolidarSublotes?: boolean;
}

@Injectable({ providedIn: 'root' })
export class ReporteTecnicoService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/ReporteTecnico`;

  /**
   * Genera reporte técnico diario para un sublote específico
   */
  generarReporteDiarioSublote(
    loteId: number,
    fechaInicio?: string,
    fechaFin?: string
  ): Observable<ReporteTecnicoCompletoDto> {
    let params = new HttpParams();
    if (fechaInicio) params = params.set('fechaInicio', fechaInicio);
    if (fechaFin) params = params.set('fechaFin', fechaFin);

    return this.http.get<ReporteTecnicoCompletoDto>(
      `${this.baseUrl}/diario/sublote/${loteId}`,
      { params }
    );
  }

  /**
   * Genera reporte técnico diario consolidado para un lote
   */
  generarReporteDiarioConsolidado(
    loteNombre: string,
    fechaInicio?: string,
    fechaFin?: string
  ): Observable<ReporteTecnicoCompletoDto> {
    let params = new HttpParams().set('loteNombre', loteNombre);
    if (fechaInicio) params = params.set('fechaInicio', fechaInicio);
    if (fechaFin) params = params.set('fechaFin', fechaFin);

    return this.http.get<ReporteTecnicoCompletoDto>(
      `${this.baseUrl}/diario/consolidado`,
      { params }
    );
  }

  /**
   * Genera reporte técnico semanal para un sublote específico
   */
  generarReporteSemanalSublote(
    loteId: number,
    semana?: number
  ): Observable<ReporteTecnicoCompletoDto> {
    let params = new HttpParams();
    if (semana !== undefined) params = params.set('semana', semana.toString());

    return this.http.get<ReporteTecnicoCompletoDto>(
      `${this.baseUrl}/semanal/sublote/${loteId}`,
      { params }
    );
  }

  /**
   * Genera reporte técnico semanal consolidado para un lote
   */
  generarReporteSemanalConsolidado(
    loteNombre: string,
    semana?: number
  ): Observable<ReporteTecnicoCompletoDto> {
    let params = new HttpParams().set('loteNombre', loteNombre);
    if (semana !== undefined) params = params.set('semana', semana.toString());

    return this.http.get<ReporteTecnicoCompletoDto>(
      `${this.baseUrl}/semanal/consolidado`,
      { params }
    );
  }

  /**
   * Genera reporte técnico según los parámetros de la solicitud
   */
  generarReporte(
    request: GenerarReporteTecnicoRequestDto
  ): Observable<ReporteTecnicoCompletoDto> {
    return this.http.post<ReporteTecnicoCompletoDto>(
      `${this.baseUrl}/generar`,
      request
    );
  }

  /**
   * Obtiene lista de sublotes disponibles para un lote base
   */
  obtenerSublotes(loteNombre: string): Observable<string[]> {
    const params = new HttpParams().set('loteNombre', loteNombre);
    return this.http.get<string[]>(`${this.baseUrl}/sublotes`, { params });
  }

  /**
   * Exporta reporte técnico diario a Excel
   */
  exportarExcelDiario(
    request: GenerarReporteTecnicoRequestDto
  ): Observable<Blob> {
    return this.http.post(
      `${this.baseUrl}/exportar/excel/diario`,
      request,
      { responseType: 'blob' }
    );
  }

  /**
   * Exporta reporte técnico semanal a Excel
   */
  exportarExcelSemanal(
    request: GenerarReporteTecnicoRequestDto
  ): Observable<Blob> {
    return this.http.post(
      `${this.baseUrl}/exportar/excel/semanal`,
      request,
      { responseType: 'blob' }
    );
  }
}


