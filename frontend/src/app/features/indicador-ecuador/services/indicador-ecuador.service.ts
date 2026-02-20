// frontend/src/app/features/indicador-ecuador/services/indicador-ecuador.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface IndicadorEcuadorRequest {
  granjaId?: number | null;
  nucleoId?: string | null;
  galponId?: string | null;
  loteId?: number | null;
  fechaDesde?: string | null;
  fechaHasta?: string | null;
  soloLotesCerrados?: boolean;
  tipoLote?: string; // "Produccion", "Levante", "Reproductora", "Todos"
  pesoAjusteVariable?: number | null; // default 2.7 (Conv. Ajustada)
  divisorAjusteVariable?: number | null; // default 4.5
}

export interface IndicadorEcuadorDto {
  granjaId: number;
  granjaNombre: string;
  loteId: number | null;
  loteNombre: string | null;
  galponId: string | null;
  galponNombre: string | null;
  avesEncasetadas: number;
  avesSacrificadas: number;
  mortalidad: number;
  mortalidadPorcentaje: number;
  supervivenciaPorcentaje: number;
  consumoTotalAlimentoKg: number;
  consumoAveGramos: number;
  kgCarnePollos: number;
  pesoPromedioKilos: number;
  conversion: number;
  conversionAjustada2700: number;
  pesoAjusteVariable: number;
  divisorAjusteVariable: number;
  edadPromedio: number;
  metrosCuadrados: number;
  avesPorMetroCuadrado: number;
  kgPorMetroCuadrado: number;
  eficienciaAmericana: number;
  eficienciaEuropea: number;
  indiceProductividad: number;
  gananciaDia: number;
  fechaInicioLote: string | null;
  fechaCierreLote: string | null; // Fecha último despacho (cierre de lote)
  loteCerrado: boolean;
}

export interface IndicadorEcuadorConsolidadoDto {
  fechaCalculo: string;
  totalGranjas: number;
  totalLotes: number;
  totalLotesCerrados: number;
  totalAvesEncasetadas: number;
  totalAvesSacrificadas: number;
  totalMortalidad: number;
  promedioMortalidadPorcentaje: number;
  promedioSupervivenciaPorcentaje: number;
  totalConsumoAlimentoKg: number;
  promedioConsumoAveGramos: number;
  totalKgCarnePollos: number;
  promedioPesoKilos: number;
  promedioConversion: number;
  promedioConversionAjustada: number;
  promedioEdad: number;
  totalMetrosCuadrados: number;
  promedioAvesPorMetroCuadrado: number;
  promedioKgPorMetroCuadrado: number;
  promedioEficienciaAmericana: number;
  promedioEficienciaEuropea: number;
  promedioIndiceProductividad: number;
  promedioGananciaDia: number;
  indicadoresPorGranja: IndicadorEcuadorDto[];
}

export interface LiquidacionPeriodoRequest {
  fechaInicio: string;
  fechaFin: string;
  tipoPeriodo: string; // "Semanal" o "Mensual"
  granjaId?: number | null;
}

export interface LiquidacionPeriodoDto {
  fechaInicio: string;
  fechaFin: string;
  tipoPeriodo: string;
  totalGranjas: number;
  totalLotesCerrados: number;
  indicadores: IndicadorEcuadorDto[];
}

/** Request para indicadores de pollo engorde por lote padre y sus reproductores */
export interface IndicadorPolloEngordePorLotePadreRequest {
  loteAveEngordeId: number;
  fechaDesde?: string | null;
  fechaHasta?: string | null;
  soloLotesCerrados?: boolean;
  pesoAjusteVariable?: number | null;
  divisorAjusteVariable?: number | null;
}

/** Indicador de un lote reproductor */
export interface IndicadorReproductorDto {
  id: number;
  nombreLote: string;
  indicador: IndicadorEcuadorDto;
}

/** Respuesta: indicador lote padre + lista por reproductor */
export interface IndicadorPolloEngordePorLotePadreDto {
  indicadorLotePadre: IndicadorEcuadorDto;
  lotesReproductores: IndicadorReproductorDto[];
}

@Injectable({ providedIn: 'root' })
export class IndicadorEcuadorService {
  private readonly baseUrl = `${environment.apiUrl}/IndicadorEcuador`;

  constructor(private http: HttpClient) {}

  calcularIndicadores(request: IndicadorEcuadorRequest): Observable<IndicadorEcuadorDto[]> {
    return this.http.post<IndicadorEcuadorDto[]>(`${this.baseUrl}/calcular`, request);
  }

  calcularConsolidado(request: IndicadorEcuadorRequest): Observable<IndicadorEcuadorConsolidadoDto> {
    return this.http.post<IndicadorEcuadorConsolidadoDto>(`${this.baseUrl}/consolidado`, request);
  }

  calcularLiquidacionPeriodo(request: LiquidacionPeriodoRequest): Observable<LiquidacionPeriodoDto> {
    return this.http.post<LiquidacionPeriodoDto>(`${this.baseUrl}/liquidacion-periodo`, request);
  }

  obtenerLotesCerrados(fechaDesde: string, fechaHasta: string, granjaId?: number): Observable<IndicadorEcuadorDto[]> {
    let params = new HttpParams()
      .set('fechaDesde', fechaDesde)
      .set('fechaHasta', fechaHasta);
    
    if (granjaId) {
      params = params.set('granjaId', granjaId.toString());
    }

    return this.http.get<IndicadorEcuadorDto[]>(`${this.baseUrl}/lotes-cerrados`, { params });
  }

  /** Indicadores de pollo engorde por lote padre (LoteAveEngorde) y sus lotes reproductores */
  indicadoresPolloEngordePorLotePadre(request: IndicadorPolloEngordePorLotePadreRequest): Observable<IndicadorPolloEngordePorLotePadreDto> {
    return this.http.post<IndicadorPolloEngordePorLotePadreDto>(`${this.baseUrl}/indicadores-pollo-engorde-por-lote-padre`, request);
  }
}
