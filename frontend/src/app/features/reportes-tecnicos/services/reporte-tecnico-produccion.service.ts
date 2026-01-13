// src/app/features/reportes-tecnicos/services/reporte-tecnico-produccion.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

// DTOs
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
  porcentajeIncubable: number;
  pesoHembra?: number | null;
  pesoMachos?: number | null;
  pesoHuevo: number;
  // Desglose de tipos de huevos
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
  // Porcentajes de tipos de huevos
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
  // Transferencias de huevos
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
  porcentajeIncubablePromedio: number;
  pesoHembraPromedio?: number | null;
  pesoMachosPromedio?: number | null;
  pesoHuevoPromedio: number;
  // Desglose de tipos de huevos semanal
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
  // Porcentajes promedio de tipos de huevos
  porcentajeLimpioPromedio?: number | null;
  porcentajeTratadoPromedio?: number | null;
  porcentajeSucioPromedio?: number | null;
  porcentajeDeformePromedio?: number | null;
  porcentajeBlancoPromedio?: number | null;
  porcentajeDobleYemaPromedio?: number | null;
  porcentajePisoPromedio?: number | null;
  porcentajePequenoPromedio?: number | null;
  porcentajeRotoPromedio?: number | null;
  porcentajeDesechoPromedio?: number | null;
  porcentajeOtroPromedio?: number | null;
  // Transferencias de huevos semanal
  huevosTrasladadosTotalSemanal: number;
  huevosTrasladadosLimpioSemanal: number;
  huevosTrasladadosTratadoSemanal: number;
  huevosTrasladadosSucioSemanal: number;
  huevosTrasladadosDeformeSemanal: number;
  huevosTrasladadosBlancoSemanal: number;
  huevosTrasladadosDobleYemaSemanal: number;
  huevosTrasladadosPisoSemanal: number;
  huevosTrasladadosPequenoSemanal: number;
  huevosTrasladadosRotoSemanal: number;
  huevosTrasladadosDesechoSemanal: number;
  huevosTrasladadosOtroSemanal: number;
  detalleDiario: ReporteTecnicoProduccionDiarioDto[];
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

export interface ReporteTecnicoProduccionCompletoDto {
  loteInfo: ReporteTecnicoProduccionLoteInfoDto;
  datosDiarios: ReporteTecnicoProduccionDiarioDto[];
  datosSemanales: ReporteTecnicoProduccionSemanalDto[];
}

export interface GenerarReporteTecnicoProduccionRequestDto {
  tipoReporte: string; // "diario" o "semanal"
  tipoConsolidacion: string; // "sublote" o "consolidado"
  loteId?: number | null;
  loteNombreBase?: string | null;
  fechaInicio?: string | null;
  fechaFin?: string | null;
  semana?: number | null;
}

@Injectable({ providedIn: 'root' })
export class ReporteTecnicoProduccionService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/ReporteTecnicoProduccion`;

  /**
   * Genera reporte técnico de producción
   */
  generarReporte(request: GenerarReporteTecnicoProduccionRequestDto): Observable<ReporteTecnicoProduccionCompletoDto> {
    return this.http.post<ReporteTecnicoProduccionCompletoDto>(`${this.baseUrl}/generar`, request);
  }

  /**
   * Obtiene la lista de sublotes para un lote base
   */
  obtenerSublotes(loteNombreBase: string): Observable<string[]> {
    const params = new HttpParams().set('loteNombreBase', loteNombreBase);
    return this.http.get<string[]>(`${this.baseUrl}/sublotes`, { params });
  }
}

