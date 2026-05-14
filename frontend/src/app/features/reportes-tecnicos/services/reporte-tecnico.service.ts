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

  /**
   * Obtiene reporte técnico de producción navegando desde LotePosturaBase.
   * Lee desde produccion_diaria. Si lotePosturaProduccionId está presente, genera individual; si no, consolida.
   */
  obtenerReporteProduccion(
    request: ObtenerReporteProduccionRequestDto
  ): Observable<ReporteTecnicoProduccionCompletoDto> {
    return this.http.post<ReporteTecnicoProduccionCompletoDto>(
      `${environment.apiUrl}/ReporteTecnicoProduccion/obtener`,
      request
    );
  }

  /**
   * Obtiene reporte técnico de producción con sistema de TABs (Fase 4 — SOLO PRODUCCIÓN).
   * Retorna datos desglosados por galpón + consolidados (general), con valores guía STANDARD.
   */
  obtenerReporteProduccionTabs(
    request: ObtenerReporteProduccionRequestDto
  ): Observable<ReporteTecnicoProduccionTabsDto> {
    return this.http.post<ReporteTecnicoProduccionTabsDto>(
      `${environment.apiUrl}/ReporteTecnicoProduccion/obtener-tabs`,
      request
    );
  }

  /**
   * Obtiene el reporte de Levante navegando desde LotePosturaBase.
   * Usa el nuevo endpoint POST /levante/obtener con cruce genético Real vs Guía.
   * Para pasar LotePosturaBaseId=0, el backend lo resolverá desde LoteLevanteId.
   */
  obtenerReporteLevante(
    request: ObtenerReporteLevanteRequestDto
  ): Observable<ReporteTecnicoLevanteCompletoDto> {
    return this.http.post<ReporteTecnicoLevanteCompletoDto>(
      `${this.baseUrl}/levante/obtener`,
      request
    );
  }

  /**
   * Exporta todos los reportes de Levante a Excel (múltiples hojas)
   */
  exportarExcelCompletoLevante(
    loteId: number,
    fechaInicio?: string | null,
    fechaFin?: string | null,
    consolidarSublotes: boolean = false
  ): Observable<Blob> {
    let params = new HttpParams()
      .set('consolidarSublotes', consolidarSublotes.toString());

    if (fechaInicio) {
      params = params.set('fechaInicio', fechaInicio);
    }
    if (fechaFin) {
      params = params.set('fechaFin', fechaFin);
    }

    return this.http.get(
      `${this.baseUrl}/levante/exportar/excel/${loteId}`,
      {
        params,
        responseType: 'blob'
      }
    );
  }

  exportarExcelLevanteNuevo(
    reporte: ReporteTecnicoLevanteCompletoDto,
    meta: ExportarExcelMetaDto
  ): Observable<Blob> {
    return this.http.post(
      `${this.baseUrl}/levante/exportar-excel`,
      { reporte, meta },
      { responseType: 'blob' }
    );
  }

  exportarExcelProduccionTabs(
    reporte: ReporteTecnicoProduccionTabsDto,
    meta: ExportarExcelMetaDto
  ): Observable<Blob> {
    return this.http.post(
      `${environment.apiUrl}/ReporteTecnicoProduccion/exportar-excel-tabs`,
      { reporte, meta },
      { responseType: 'blob' }
    );
  }
}

// ========== DTOs Exportación Excel (Fase 5) ==========

export interface ExportarExcelMetaDto {
  etapa: string;
  loteBaseNombre: string;
  loteSubloteNombre?: string | null;
  granjaNombre?: string | null;
  nucleoNombre?: string | null;
  fechaInicio?: string | null;
  fechaFin?: string | null;
  totalAvesInicio?: number | null;
  periodicidad?: string | null;
}

// ========== DTOs para el nuevo endpoint POST /produccion/obtener ==========

export interface ObtenerReporteProduccionRequestDto {
  lotePosturaBaseId: number;
  lotePosturaProduccionId?: number | null;
  filtroPeriodicidad: 'Semanal' | 'Diario';
  fechaInicio?: string | null;
  fechaFin?: string | null;
}

export interface ReporteTecnicoProduccionLoteInfoDto {
  loteId: number;
  loteNombre: string;
  raza?: string | null;
  linea?: string | null;
  fechaInicioProduccion?: string | null;
  numeroHembrasIniciales?: number | null;
  numeroMachosIniciales?: number | null;
  galpon?: number | null;
  tecnico?: string | null;
  granjaNombre?: string | null;
  nucleoNombre?: string | null;
}

export interface ReporteTecnicoProduccionDiarioDto {
  dia: number;
  semana: number;
  fecha: string;
  mortalidadHembras: number;
  mortalidadMachos: number;
  seleccionHembras: number;
  seleccionMachos: number;
  ventasHembras: number;
  ventasMachos: number;
  trasladosHembras: number;
  trasladosMachos: number;
  saldoHembras: number;
  saldoMachos: number;
  huevosTotales: number;
  porcentajePostura: number;
  kilosAlimentoHembras: number;
  kilosAlimentoMachos: number;
  huevosEnviadosPlanta: number;
  porcentajeEnviadoPlanta: number;
  huevosIncubables: number;
  huevosCargados: number;
  porcentajeNacimientos?: number | null;
  ventaHuevo?: number | null;
  pollitosVendidos?: number | null;
  pesoHembra?: number | null;
  pesoMachos?: number | null;
  pesoHuevo: number;
  porcentajeGrasaCorporal?: number | null;
  huevoLimpio: number;
  huevoTratado: number;
  huevoSucio: number;
  huevoDeforme: number;
  huevoBlanco: number;
  huevoDobleYema: number;
  huevoPiso: number;
  huevoPequeno: number;
  huevoRoto: number;
  huevoDesecho: number;
  huevoOtro: number;
  porcentajeLimpio?: number | null;
  porcentajeTratado?: number | null;
  porcentajeSucio?: number | null;
  porcentajeDeforme?: number | null;
  porcentajeBlanco?: number | null;
  porcentajeDobleYema?: number | null;
  porcentajePiso?: number | null;
  porcentajePequeno?: number | null;
  porcentajeRoto?: number | null;
  porcentajeDesecho?: number | null;
  porcentajeOtro?: number | null;
  huevosTrasladadosTotal: number;
  huevosTrasladadosLimpio: number;
  huevosTrasladadosTratado: number;
  huevosTrasladadosSucio: number;
  huevosTrasladadosDeforme: number;
  huevosTrasladadosBlanco: number;
  huevosTrasladadosDobleYema: number;
  huevosTrasladadosPiso: number;
  huevosTrasladadosPequeno: number;
  huevosTrasladadosRoto: number;
  huevosTrasladadosDesecho: number;
  huevosTrasladadosOtro: number;
}

export interface ReporteTecnicoProduccionSemanalDto {
  semana: number;
  fechaInicioSemana: string;
  fechaFinSemana: string;
  edadInicioSemanas: number;
  edadFinSemanas: number;
  mortalidadHembrasSemanal: number;
  mortalidadMachosSemanal: number;
  seleccionHembrasSemanal: number;
  seleccionMachosSemanal: number;
  ventasHembrasSemanal: number;
  ventasMachosSemanal: number;
  trasladosHembrasSemanal: number;
  trasladosMachosSemanal: number;
  saldoInicioHembras: number;
  saldoFinHembras: number;
  saldoInicioMachos: number;
  saldoFinMachos: number;
  huevosTotalesSemanal: number;
  porcentajePosturaPromedio: number;
  kilosAlimentoHembrasSemanal: number;
  kilosAlimentoMachosSemanal: number;
  huevosEnviadosPlantaSemanal: number;
  porcentajeEnviadoPlantaPromedio: number;
  huevosIncubablesSemanal: number;
  huevosCargadosSemanal: number;
  porcentajeNacimientosPromedio?: number | null;
  ventaHuevoSemanal?: number | null;
  pollitosVendidosSemanal?: number | null;
  pesoHembraPromedio?: number | null;
  pesoMachosPromedio?: number | null;
  pesoHuevoPromedio: number;
  porcentajeGrasaCorporalPromedio?: number | null;
  huevoLimpioSemanal: number;
  huevoTratadoSemanal: number;
  huevoSucioSemanal: number;
  huevoDeformeSemanal: number;
  huevoBlancoSemanal: number;
  huevoDobleYemaSemanal: number;
  huevoPisoSemanal: number;
  huevoPequenoSemanal: number;
  huevoRotoSemanal: number;
  huevoDesechoSemanal: number;
  huevoOtroSemanal: number;
  detalleDiario: ReporteTecnicoProduccionDiarioDto[];
}

export interface ReporteTecnicoProduccionCompletoDto {
  loteInfo: ReporteTecnicoProduccionLoteInfoDto;
  datosDiarios: ReporteTecnicoProduccionDiarioDto[];
  datosSemanales: ReporteTecnicoProduccionSemanalDto[];
}

// ========== DTOs para el nuevo endpoint POST /levante/obtener ==========

export interface ObtenerReporteLevanteRequestDto {
  /** ID de lote_postura_base (raíz). Puede ser 0 si loteLevanteId está presente y el backend lo resuelve. */
  lotePosturaBaseId: number;
  /** ID de lote_postura_levante (opcional). Si se omite, se consolidan todos los lotes del base. */
  loteLevanteId?: number | null;
  /** "Semanal" (semanas 1-25 con cruce genético) | "Diario" (por fecha calendario) */
  filtroPeriodicidad: 'Semanal' | 'Diario';
  fechaInicio?: string | null;
  fechaFin?: string | null;
}

/** Fila de datos diarios del DTO ReporteTecnicoLevanteCompletoDto.DatosDiarios */
export interface ReporteTecnicoDiarioLevanteDto {
  fecha: string;
  edadDias: number;
  edadSemanas: number;
  saldoHembras: number;
  mortalidadHembras: number;
  mortalidadHembrasAcumulada: number;
  porcMortH: number;
  porcMortHAcumulado: number;
  selH: number;
  errorSexajeH: number;
  consumoKgH: number;
  consumoKgHAcumulado: number;
  pesoPromH?: number | null;
  uniformidadH?: number | null;
  cvH?: number | null;
  kcalAlH?: number | null;
  protAlH?: number | null;
  kcalAveH?: number | null;
  protAveH?: number | null;
  saldoMachos: number;
  mortalidadMachos: number;
  mortalidadMachosAcumulada: number;
  porcMortM: number;
  porcMortMAcumulado: number;
  selM: number;
  errorSexajeM: number;
  consumoKgM: number;
  consumoKgMAcumulado: number;
  pesoPromM?: number | null;
  uniformidadM?: number | null;
  cvM?: number | null;
  observaciones?: string | null;
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
  /** Datos diarios. Solo se puebla cuando FiltroPeriodicidad = "Diario". */
  datosDiarios: ReporteTecnicoDiarioLevanteDto[];
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
  descarteMachos: number;
  descarteMachosAcumulado: number;
  descarteMachosPorcentajeDiario: number;
  descarteMachosPorcentajeAcumulado: number;
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
  descarteHembras: number;
  descarteHembrasAcumulado: number;
  descarteHembrasPorcentajeDiario: number;
  descarteHembrasPorcentajeAcumulado: number;
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

// ========== DTOs Fase 4 — Sistema de TABs (SOLO PRODUCCIÓN) ==========

export interface ReporteDiarioGalponDto {
  lotePosturaProduccionId: number;
  galponId: string;
  galponNombre: string;
  loteNombre: string;
  fecha: string;
  semanaRelativa: number;
  edadDias: number;
  saldoHembras: number;
  saldoMachos: number;
  mortalidadHembras: number;
  mortalidadMachos: number;
  porcMortalidad: number;
  consKgH: number;
  consKgM: number;
  huevoTot: number;
  huevoInc: number;
  porcentajePostura: number;
  porcentajeIncubables: number;
  pesoHuevo: number;
  pesoH?: number | null;
  pesoM?: number | null;
  uniformidad?: number | null;
  htaa?: number | null;
  // GUIA
  porcentajePosturaGuia?: number | null;
  pesoHuevoGuia?: number | null;
  htaaGuia?: number | null;
  uniformidadGuia?: number | null;
  // Diferencias
  difPostura?: number | null;
  difPesoHuevo?: number | null;
  observaciones?: string | null;
}

export interface ReporteSemanalGalponDto {
  lotePosturaProduccionId: number;
  galponId: string;
  galponNombre: string;
  loteNombre: string;
  semana: number;
  fechaInicioSemana: string;
  fechaFinSemana: string;
  edadSemanas: number;
  saldoInicioHembras: number;
  saldoInicioMachos: number;
  saldoFinHembras: number;
  saldoFinMachos: number;
  mortalidadHembrasSemanal: number;
  mortalidadMachosSemanal: number;
  porcMortalidadSemanal: number;
  consKgHSemanal: number;
  consKgMSemanal: number;
  huevoTotSemanal: number;
  huevoIncSemanal: number;
  porcentajePosturaPromedio: number;
  porcentajeIncubablesPromedio: number;
  pesoHuevoPromedio: number;
  pesoHPromedio?: number | null;
  pesoMPromedio?: number | null;
  uniformidadPromedio?: number | null;
  htaaSemanal?: number | null;
  // GUIA
  porcentajePosturaGuia?: number | null;
  pesoHuevoGuia?: number | null;
  htaaGuia?: number | null;
  uniformidadGuia?: number | null;
  // Diferencias
  difPostura?: number | null;
  difPesoHuevo?: number | null;
}

export interface ReporteGeneralDiarioDto {
  fecha: string;
  semanaRelativa: number;
  edadDias: number;
  saldoTotalHembras: number;
  saldoTotalMachos: number;
  mortalidadTotalHembras: number;
  mortalidadTotalMachos: number;
  porcMortalidadPromedio: number;
  consKgHTotalKg: number;
  consKgMTotalKg: number;
  huevosTotTotal: number;
  huevosIncTotal: number;
  porcentajePosturaPromedio: number;
  pesoHuevoPromedio: number;
  uniformidadPromedio?: number | null;
  // GUIA
  porcentajePosturaGuia?: number | null;
  pesoHuevoGuia?: number | null;
  htaaGuia?: number | null;
  // Diferencia
  difPostura?: number | null;
}

export interface ReporteGeneralSemanalDto {
  semana: number;
  fechaInicioSemana: string;
  fechaFinSemana: string;
  edadSemanas: number;
  saldoInicioHembras: number;
  saldoInicioMachos: number;
  saldoFinHembras: number;
  saldoFinMachos: number;
  mortalidadTotalHembras: number;
  mortalidadTotalMachos: number;
  porcMortalidadSemanal: number;
  consKgHTotal: number;
  consKgMTotal: number;
  huevosTotTotal: number;
  huevosIncTotal: number;
  porcentajePosturaPromedio: number;
  pesoHuevoPromedio: number;
  pesoHPromedio?: number | null;
  pesoMPromedio?: number | null;
  uniformidadPromedio?: number | null;
  htaaSemanal?: number | null;
  // GUIA
  porcentajePosturaGuia?: number | null;
  pesoHuevoGuia?: number | null;
  htaaGuia?: number | null;
  uniformidadGuia?: number | null;
  // Diferencias
  difPostura?: number | null;
  difPesoHuevo?: number | null;
}

export interface ReporteTecnicoProduccionTabsDto {
  loteInfo: ReporteTecnicoProduccionLoteInfoDto;
  diariosGalpon: ReporteDiarioGalponDto[];
  semanalesGalpon: ReporteSemanalGalponDto[];
  diariosGeneral: ReporteGeneralDiarioDto[];
  semanalesGeneral: ReporteGeneralSemanalDto[];
}


