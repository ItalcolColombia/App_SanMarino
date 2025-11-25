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

