// frontend/src/app/features/indicador-ecuador/services/indicador-ecuador.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { AuditoriaLiquidacionResultado, AuditoriaScopeInput, AplicarCorreccionResultado } from '../models/auditoria-liquidacion.model';

export interface IndicadorEcuadorRequest {
  granjaId?: number | null;
  nucleoId?: string | null;
  galponId?: string | null;
  loteId?: number | null;
  fechaDesde?: string | null;
  fechaHasta?: string | null;
  tipoFiltroLotes?: 'cerrados' | 'aves_cero' | 'todos';
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
  fechaCierreLote: string | null;
  /** true si aves actuales = 0 (según reglas del backend) */
  loteCerrado: boolean;
  fechaAlistamiento?: string | null;
  // Mermas, ajuste y kilos a cliente (R1) — calculados por el backend.
  // null = Costos NO registró merma en el lote ⇒ el reporte muestra el campo VACÍO («—»).
  mermaUnidades?: number | null;
  mermaKilos?: number | null;
  mermaPorcentaje?: number | null;
  ajusteAves?: number | null;
  porcentajeAjuste?: number | null;
  produccionKiloEnPie?: number;
  totalKilosDespachadosCliente?: number | null;
  diasEngorde?: number;
  fechaLiquidacion?: string | null;
  avesSobrante?: number;
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

/** Respuesta: indicador lote padre (null si no está cerrado y se filtró por solo cerrados) + lista por reproductor. Incluye reproductores con 0 aves cuando aplica. */
export interface IndicadorPolloEngordePorLotePadreDto {
  indicadorLotePadre: IndicadorEcuadorDto | null;
  lotesReproductores: IndicadorReproductorDto[];
}

/** POST liquidacion-pollo-engorde-reporte: solo lote padre liquidado (sin reproductoras). */
export interface LiquidacionPolloEngordeReporteRequest {
  modo: 'UnLote' | 'Rango' | 'TodosLiquidados';
  loteAveEngordeId?: number | null;
  fechaDesde?: string | null;
  fechaHasta?: string | null;
  /** TodasLasGranjas | Granja | Nucleo (solo modo Rango) */
  alcance: string;
  granjaId?: number | null;
  nucleoId?: string | null;
  /** Modo UnLote sin lote: acota por galpón (opcional). */
  galponId?: string | null;
  /** Modo TodosLiquidados: prefijo YYCC del nombre del lote (ej: "2601"). Opcional. */
  loteCodigo?: string | null;
  /** "cerrados" (default) | "aves_cero" | "todos" */
  tipoFiltroLotes?: 'cerrados' | 'aves_cero' | 'todos';
}

export interface LiquidacionPolloEngordeItemDto {
  loteAveEngordeId: number;
  loteNombre: string;
  indicador: IndicadorEcuadorDto;
}

export interface LiquidacionPolloEngordeReporteDto {
  modo: string;
  items: LiquidacionPolloEngordeItemDto[];
}

/** ── Liquidación / Reporte de indicadores Panamá ──────────────────────────── */

/** Insumos digitados por el usuario al liquidar un lote en Panamá. */
export interface GuardarLiquidacionPanamaRequest {
  loteAveEngordeId: number;
  metrosCuadrados: number;
  avesFinalGranja: number;
  avesBeneficiada: number;
  produccionKiloPie: number;
  diasEngorde: number;
  diasEnGranja: number;
  registradoPorUserId?: string | null;
}

/** Bloque "liquidacion" del reporte Panamá (insumos + indicadores derivados por la fn). */
export interface LiquidacionPanamaDto {
  id: number;
  idUsuarioRegistro: string | null;
  idLote: number;
  metrosCuadrados: number;
  avesFinalGranja: number;
  produccionKiloPie: number;
  diasEngorde: number;
  diasEnGranja: number;
  avesBeneficiada: number;
  pesoPromedio: number;
  mortalidadPorc: number;
  seleccionPorc: number;
  porcMortalidadTotal: number;
  supervivencia: number;
  consumoAve: number;
  conversion: number;
  eficienciaAmericana: number;
  eeF: number;
  eefDos: number;
  avesMetrosCua: number;
  kilosMetrosCua: number;
  productividad: number;
  faltanteSobra: number;
}

export interface InfoProductivaPanamaDto {
  consumoAlimentoTotal: number;
  totalAvesSeleccion: number;
  totalAvesMuertas: number;
}

export interface ReporteIndicadoresPanamaDto {
  liquidacion: LiquidacionPanamaDto;
  infoProductiva: InfoProductivaPanamaDto;
  avesEncasetadas: number;
}

/** Reporte individual de un lote/galpón dentro de una corrida (Panamá). */
export interface ReporteCorridaPanamaItemDto {
  loteAveEngordeId: number;
  loteNombre: string;
  galponId: string | null;
  fechaEncaset: string | null;
  reporte: ReporteIndicadoresPanamaDto;
}

/** Lote/galpón de la corrida que aún no tiene liquidación registrada. */
export interface LoteCorridaPanamaResumenDto {
  loteAveEngordeId: number;
  loteNombre: string;
  galponId: string | null;
  fechaEncaset: string | null;
}

/**
 * Reporte de una CORRIDA completa (Panamá): el lote_nombre ES el número de corrida y se
 * repite en varios galpones de la granja. Un reporte por galpón + consolidado.
 */
export interface ReporteCorridaPanamaDto {
  corrida: string;
  granjaId: number;
  items: ReporteCorridaPanamaItemDto[];
  lotesSinLiquidacion: LoteCorridaPanamaResumenDto[];
  consolidado: ReporteIndicadoresPanamaDto | null;
}

@Injectable({ providedIn: 'root' })
export class IndicadorEcuadorService {
  private readonly baseUrl = `${environment.apiUrl}/IndicadorEcuador`;
  private readonly panamaUrl = `${environment.apiUrl}/ReporteIndicadorPanama`;

  constructor(private http: HttpClient) {}

  /** Guarda/actualiza los 6 insumos de liquidación Panamá del lote. */
  guardarLiquidacionPanama(req: GuardarLiquidacionPanamaRequest): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(`${this.panamaUrl}/liquidar`, req);
  }

  /** Reporte "RESULTADOS DE LIQUIDACIÓN" del lote (ejecuta fn_reporte_indicadores_panama). */
  getReporteIndicadoresPanama(loteAveEngordeId: number): Observable<ReporteIndicadoresPanamaDto> {
    return this.http.get<ReporteIndicadoresPanamaDto>(`${this.panamaUrl}/${loteAveEngordeId}`);
  }

  /** Reporte de una CORRIDA Panamá: todos los galpones de la corrida en la granja + consolidado. */
  getReporteCorridaPanama(
    granjaId: number,
    corrida: string,
    nucleoId?: string | null,
    galponId?: string | null
  ): Observable<ReporteCorridaPanamaDto> {
    let params = new HttpParams()
      .set('granjaId', String(granjaId))
      .set('corrida', corrida);
    if (nucleoId) params = params.set('nucleoId', nucleoId);
    if (galponId) params = params.set('galponId', galponId);
    return this.http.get<ReporteCorridaPanamaDto>(`${this.panamaUrl}/por-corrida`, { params });
  }

  calcularIndicadores(request: IndicadorEcuadorRequest): Observable<IndicadorEcuadorDto[]> {
    return this.http.post<IndicadorEcuadorDto[]>(`${this.baseUrl}/calcular`, request);
  }

  calcularConsolidado(request: IndicadorEcuadorRequest): Observable<IndicadorEcuadorConsolidadoDto> {
    return this.http.post<IndicadorEcuadorConsolidadoDto>(`${this.baseUrl}/consolidado`, request);
  }

  calcularLiquidacionPeriodo(request: LiquidacionPeriodoRequest): Observable<LiquidacionPeriodoDto> {
    return this.http.post<LiquidacionPeriodoDto>(`${this.baseUrl}/liquidacion-periodo`, request);
  }

  obtenerLotesCerrados(
    fechaDesde: string,
    fechaHasta: string,
    granjaId?: number,
    /** true = cerrados con fecha de cierre en el rango; false = encaset en el rango (incluye abiertos) */
    soloCerrados?: boolean
  ): Observable<IndicadorEcuadorDto[]> {
    let params = new HttpParams()
      .set('fechaDesde', fechaDesde)
      .set('fechaHasta', fechaHasta)
      .set('soloCerrados', String(soloCerrados ?? true));

    if (granjaId) {
      params = params.set('granjaId', granjaId.toString());
    }

    return this.http.get<IndicadorEcuadorDto[]>(`${this.baseUrl}/lotes-cerrados`, { params });
  }

  /** Indicadores de pollo engorde por lote padre (LoteAveEngorde) y sus lotes reproductores */
  indicadoresPolloEngordePorLotePadre(request: IndicadorPolloEngordePorLotePadreRequest): Observable<IndicadorPolloEngordePorLotePadreDto> {
    return this.http.post<IndicadorPolloEngordePorLotePadreDto>(`${this.baseUrl}/indicadores-pollo-engorde-por-lote-padre`, request);
  }

  /** Liquidación técnica: solo lotes padre liquidados (aves = 0), sin reproductoras. */
  liquidacionPolloEngordeReporte(request: LiquidacionPolloEngordeReporteRequest): Observable<LiquidacionPolloEngordeReporteDto> {
    return this.http.post<LiquidacionPolloEngordeReporteDto>(`${this.baseUrl}/liquidacion-pollo-engorde-reporte`, request);
  }

  /**
   * Verificador de liquidación: sube el Excel correcto (multipart) y devuelve el análisis armado
   * por la BD (reconciliación + hallazgos + simulación). El back solo parsea y delega en la fn.
   */
  auditarLiquidacion(scope: AuditoriaScopeInput, excel: File): Observable<AuditoriaLiquidacionResultado> {
    const fd = new FormData();
    fd.append('granjaId', String(scope.granjaId));
    if (scope.nucleoId) fd.append('nucleoId', scope.nucleoId);
    if (scope.loteCodigo) fd.append('loteCodigo', scope.loteCodigo);
    fd.append('excel', excel);
    return this.http.post<AuditoriaLiquidacionResultado>(`${this.baseUrl}/auditoria-liquidacion`, fd);
  }

  /**
   * Aplica la corrección sugerida (carga kgTotal en los despachos sin peso de la corrida).
   * Requiere permiso 'liquidacion.aplicar_correccion' (validado en el back → 403 si no).
   */
  aplicarCorreccionLiquidacion(scope: AuditoriaScopeInput, kgTotal: number): Observable<AplicarCorreccionResultado> {
    return this.http.post<AplicarCorreccionResultado>(`${this.baseUrl}/auditoria-liquidacion/aplicar`, {
      granjaId: scope.granjaId,
      nucleoId: scope.nucleoId,
      loteCodigo: scope.loteCodigo,
      kgTotal
    });
  }
}
