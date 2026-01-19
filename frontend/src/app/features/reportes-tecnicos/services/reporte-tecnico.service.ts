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
  trasladosNumero: number;
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
  descarteTotalSemana: number;
  trasladosTotalSemana: number;
  errorSexajeTotalSemana: number;
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
  numeroMachos?: number | null; // Número inicial de machos
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

  /**
   * Genera reporte técnico completo de Levante con estructura Excel (25 semanas)
   * Incluye todos los campos calculados, manuales y de guía
   */
  generarReporteLevanteCompleto(
    loteId: number,
    consolidarSublotes: boolean = false
  ): Observable<ReporteTecnicoLevanteCompletoDto> {
    const params = new HttpParams().set('consolidarSublotes', consolidarSublotes.toString());
    return this.http.get<ReporteTecnicoLevanteCompletoDto>(
      `${this.baseUrl}/levante/completo/${loteId}`,
      { params }
    );
  }

  /**
   * Genera reporte técnico de Levante con estructura de tabs
   * Incluye datos diarios separados (machos y hembras) y datos semanales completos
   */
  generarReporteLevanteConTabs(
    loteId: number,
    fechaInicio?: string,
    fechaFin?: string,
    consolidarSublotes: boolean = false
  ): Observable<ReporteTecnicoLevanteConTabsDto> {
    let params = new HttpParams()
      .set('consolidarSublotes', consolidarSublotes.toString());
    
    if (fechaInicio) {
      params = params.set('fechaInicio', fechaInicio);
    }
    if (fechaFin) {
      params = params.set('fechaFin', fechaFin);
    }
    
    return this.http.get<ReporteTecnicoLevanteConTabsDto>(
      `${this.baseUrl}/levante/tabs/${loteId}`,
      { params }
    );
  }
}

// ========== DTOs para Reporte Técnico Completo de Levante (Estructura Excel) ==========

export interface ReporteTecnicoLevanteSemanalDto {
  // Identificación y datos manuales
  codGuia?: string | null;
  idLoteRAP?: string | null;
  regional?: string | null;
  granja?: string | null;
  lote?: string | null;
  raza?: string | null;
  anoG?: number | null;
  hembraIni: number;
  machoIni: number;
  traslado?: number | null;
  nucleoL?: string | null;
  anon?: number | null;
  edad: number;
  fecha: string;
  semAno: number;
  semana: number;

  // Datos hembras
  hembra: number;
  mortH: number;
  selH: number;
  errorH: number;
  consKgH: number;
  pesoH?: number | null;
  uniformH?: number | null;
  cvH?: number | null;
  kcalAlH?: number | null;
  protAlH?: number | null;

  // Datos machos
  saldoMacho: number;
  mortM: number;
  selM: number;
  errorM: number;
  consKgM: number;
  pesoM?: number | null;
  uniformM?: number | null;
  cvM?: number | null;
  kcalAlM?: number | null;
  protAlM?: number | null;

  // Cálculos de eficiencia y rendimiento
  kcalAveH?: number | null;
  protAveH?: number | null;
  kcalAveM?: number | null;
  protAveM?: number | null;
  relMH?: number | null;
  porcMortH?: number | null;
  porcMortHGUIA?: number | null;
  difMortH?: number | null;
  acMortH?: number | null;
  porcSelH?: number | null;
  acSelH?: number | null;
  porcErrH?: number | null;
  acErrH?: number | null;
  mseh?: number | null;
  retAcH?: number | null;
  porcRetiroH?: number | null;
  retiroHGUIA?: number | null;
  acConsH?: number | null;
  consAcGrH?: number | null;
  consAcGrHGUIA?: number | null;
  grAveDiaH?: number | null;
  grAveDiaGUIAH?: number | null;
  incrConsH?: number | null;
  incrConsHGUIA?: number | null;
  porcDifConsH?: number | null;
  pesoHGUIA?: number | null;
  porcDifPesoH?: number | null;
  unifHGUIA?: number | null;
  porcMortM?: number | null;
  porcMortMGUIA?: number | null;
  difMortM?: number | null;
  acMortM?: number | null;
  porcSelM?: number | null;
  acSelM?: number | null;
  porcErrM?: number | null;
  acErrM?: number | null;
  msem?: number | null;
  retAcM?: number | null;
  porcRetAcM?: number | null;
  retiroMGUIA?: number | null;
  acConsM?: number | null;
  consAcGrM?: number | null;
  consAcGrMGUIA?: number | null;
  grAveDiaM?: number | null;
  grAveDiaMGUIA?: number | null;
  incrConsM?: number | null;
  incrConsMGUIA?: number | null;
  difConsM?: number | null;
  pesoMGUIA?: number | null;
  porcDifPesoM?: number | null;
  unifMGUIA?: number | null;
  errSexAcH?: number | null;
  porcErrSxAcH?: number | null;
  errSexAcM?: number | null;
  porcErrSxAcM?: number | null;
  difConsAcH?: number | null;
  difConsAcM?: number | null;

  // Datos nutricionales y guía
  alimHGUIA?: string | null;
  kcalSemH?: number | null;
  kcalSemAcH?: number | null;
  kcalSemHGUIA?: number | null;
  kcalSemAcHGUIA?: number | null;
  protSemH?: number | null;
  protSemAcH?: number | null;
  protSemHGUIA?: number | null;
  protSemAcHGUIA?: number | null;
  alimMGUIA?: string | null;
  kcalSemM?: number | null;
  kcalSemAcM?: number | null;
  kcalSemMGUIA?: number | null;
  kcalSemAcMGUIA?: number | null;
  protSemM?: number | null;
  protSemAcM?: number | null;
  protSemMGUIA?: number | null;
  protSemAcMGUIA?: number | null;

  // Observaciones
  observaciones?: string | null;
}

export interface ReporteTecnicoLevanteCompletoDto {
  informacionLote: ReporteTecnicoLoteInfoDto;
  datosSemanales: ReporteTecnicoLevanteSemanalDto[];
  esConsolidado: boolean;
  sublotesIncluidos: string[];
}

// ========== DTOs para Reporte Técnico Diario Separado ==========

export interface ReporteTecnicoDiarioMachosDto {
  fecha: string;
  edadDias: number;
  edadSemanas: number;
  saldoMachos: number;
  mortalidadMachos: number;
  mortalidadMachosAcumulada: number;
  mortalidadMachosPorcentajeDiario: number;
  mortalidadMachosPorcentajeAcumulado: number;
  seleccionMachos: number;
  seleccionMachosAcumulada: number;
  seleccionMachosPorcentajeDiario: number;
  seleccionMachosPorcentajeAcumulado: number;
  trasladosMachos: number;
  trasladosMachosAcumulados: number;
  errorSexajeMachos: number;
  errorSexajeMachosAcumulado: number;
  errorSexajeMachosPorcentajeDiario: number;
  errorSexajeMachosPorcentajeAcumulado: number;
  consumoKgMachos: number;
  consumoKgMachosAcumulado: number;
  consumoGramosPorAveMachos: number;
  pesoPromedioMachos?: number | null;
  uniformidadMachos?: number | null;
  coeficienteVariacionMachos?: number | null;
  gananciaPesoMachos?: number | null;
  kcalAlMachos?: number | null;
  protAlMachos?: number | null;
  kcalAveMachos?: number | null;
  protAveMachos?: number | null;
  ingresosAlimentoKilos: number;
  trasladosAlimentoKilos: number;
  observaciones?: string | null;
}

export interface ReporteTecnicoDiarioHembrasDto {
  fecha: string;
  edadDias: number;
  edadSemanas: number;
  saldoHembras: number;
  mortalidadHembras: number;
  mortalidadHembrasAcumulada: number;
  mortalidadHembrasPorcentajeDiario: number;
  mortalidadHembrasPorcentajeAcumulado: number;
  seleccionHembras: number;
  seleccionHembrasAcumulada: number;
  seleccionHembrasPorcentajeDiario: number;
  seleccionHembrasPorcentajeAcumulado: number;
  trasladosHembras: number;
  trasladosHembrasAcumulados: number;
  errorSexajeHembras: number;
  errorSexajeHembrasAcumulado: number;
  errorSexajeHembrasPorcentajeDiario: number;
  errorSexajeHembrasPorcentajeAcumulado: number;
  consumoKgHembras: number;
  consumoKgHembrasAcumulado: number;
  consumoGramosPorAveHembras: number;
  pesoPromedioHembras?: number | null;
  uniformidadHembras?: number | null;
  coeficienteVariacionHembras?: number | null;
  gananciaPesoHembras?: number | null;
  kcalAlHembras?: number | null;
  protAlHembras?: number | null;
  kcalAveHembras?: number | null;
  protAveHembras?: number | null;
  ingresosAlimentoKilos: number;
  trasladosAlimentoKilos: number;
  observaciones?: string | null;
}

export interface ReporteTecnicoLevanteConTabsDto {
  informacionLote: ReporteTecnicoLoteInfoDto;
  datosDiariosMachos: ReporteTecnicoDiarioMachosDto[];
  datosDiariosHembras: ReporteTecnicoDiarioHembrasDto[];
  datosSemanales: ReporteTecnicoLevanteSemanalDto[];
  esConsolidado: boolean;
  sublotesIncluidos: string[];
}


