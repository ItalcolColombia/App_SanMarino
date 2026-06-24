// frontend/src/app/features/informe-semanal-engorde/services/informe-semanal-engorde.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

/** Filtros del Informe Semanal Pollo de Engorde (Panamá). */
export interface InformeSemanalRequest {
  granjaIds?: number[] | null;   // null/vacío = todas las granjas
  nucleoId?: string | null;
  galponId?: string | null;
  loteId?: number | null;
  fechaDesde?: string | null;    // 'yyyy-MM-dd'
  fechaHasta?: string | null;
}

/** Una fila = (lote, semana de vida). Reales + Tabla (placeholder NULL). */
export interface InformeSemanalFila {
  granjaId: number;
  granjaNombre: string;
  nucleoId: string | null;
  galponId: string | null;
  galponNombre: string | null;
  loteAveEngordeId: number;
  loteNombre: string;
  fechaEncaset: string | null;
  semana: number;
  edadDiaFin: number;
  fechaInicioSemana: string | null;
  fechaFinSemana: string | null;
  avesEncasetadas: number;
  saldoInicioSemana: number;
  saldoFinSemana: number;
  mortNaturalUnid: number;
  seleccionUnid: number;
  ventasUnid: number;
  mortNaturalPct: number;
  seleccionPct: number;
  mortalidadTotalPct: number;
  consumoSemanaKg: number;
  consumoAcumKg: number;
  consumoRealGAve: number;
  pesoRealG: number | null;
  pesoAnteriorG: number | null;
  pesoLlegadaG: number | null;
  gananciaRealG: number | null;
  conversionReal: number | null;
  ventasKg: number;
  aguaMl: number;
  relacionAgua: number | null;
  // Tabla genética (placeholder — aún no conectado)
  consumoTablaG: number | null;
  pesoTablaG: number | null;
  gananciaTablaG: number | null;
  conversionTabla: number | null;
  mortalidadTablaPct: number | null;
  pctConsumo: number | null;
  pctPeso: number | null;
  pctConversion: number | null;
}

/** Fila CONSOLIDADO de la semana (AVES = suma; tasas/pesos = promedio). */
export interface InformeSemanalConsolidado {
  semana: number;
  cantidadLotes: number;
  avesTotales: number;
  consumoRealGAveProm: number;
  pesoRealGProm: number | null;
  gananciaRealGProm: number | null;
  conversionRealProm: number | null;
  mortNaturalPctProm: number;
  seleccionPctProm: number;
  mortalidadTotalPctProm: number;
  consumoSemanaKgTotal: number;
  ventasKgTotal: number;
  ventasUnidTotal: number;
  // Tabla genética (promedio entre lotes; null si ningún lote tiene guía configurada)
  consumoTablaGProm: number | null;
  pesoTablaGProm: number | null;
  gananciaTablaGProm: number | null;
  conversionTablaProm: number | null;
  mortalidadTablaPctProm: number | null;
  pctConsumoProm: number | null;
  pctPesoProm: number | null;
  pctConversionProm: number | null;
}

export interface InformeSemanalGrupoSemana {
  semana: number;
  filas: InformeSemanalFila[];
  consolidado: InformeSemanalConsolidado;
}

export interface InformeSemanalReporte {
  filtrosAplicados: InformeSemanalRequest;
  totalFilas: number;
  semanas: InformeSemanalGrupoSemana[];
}

@Injectable({ providedIn: 'root' })
export class InformeSemanalEngordeService {
  private readonly baseUrl = `${environment.apiUrl}/InformeSemanalPolloEngorde`;
  private http = inject(HttpClient);

  generar(request: InformeSemanalRequest): Observable<InformeSemanalReporte> {
    return this.http.post<InformeSemanalReporte>(`${this.baseUrl}/generar`, request);
  }
}
