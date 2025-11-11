// src/app/features/lote-produccion/services/produccion.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface ExisteProduccionLoteResponse {
  exists: boolean;
  produccionLoteId?: number;
}

export interface CrearProduccionLoteRequest {
  loteId: number;
  fechaInicio: string; // ISO date
  avesInicialesH: number;
  avesInicialesM: number;
  huevosIniciales: number;
  tipoNido: string; // Jansen, Manual, Vencomatic
  ciclo: string; // normal, 2 Replume, D: Depopulación
  nucleoP?: string; // Núcleo de Producción
}

export interface ProduccionLoteDetalleDto {
  id: number;
  loteId: number;
  fechaInicio: string; // ISO date
  avesInicialesH: number;
  avesInicialesM: number;
  huevosIniciales: number;
  tipoNido: string;
  ciclo: string;
  createdAt: string;
  updatedAt?: string;
}

export interface CrearSeguimientoRequest {
  produccionLoteId: number;
  fechaRegistro: string; // ISO date
  mortalidadH: number;
  mortalidadM: number;
  selH: number; // Selección hembras (retiradas)
  consKgH: number; // Consumo hembras (kg)
  consKgM: number; // Consumo machos (kg)
  huevosTotales: number;
  huevosIncubables: number;
  // Campos de Clasificadora de Huevos - (Limpio, Tratado) = HuevoInc +
  huevoLimpio?: number;
  huevoTratado?: number;
  // Campos de Clasificadora de Huevos - (Sucio, Deforme, Blanco, Doble Yema, Piso, Pequeño, Roto, Desecho, Otro) = Huevo Total
  huevoSucio?: number;
  huevoDeforme?: number;
  huevoBlanco?: number;
  huevoDobleYema?: number;
  huevoPiso?: number;
  huevoPequeno?: number;
  huevoRoto?: number;
  huevoDesecho?: number;
  huevoOtro?: number;
  tipoAlimento: string;
  pesoHuevo: number;
  etapa: number; // 1: semana 25-33, 2: 34-50, 3: >50
  observaciones?: string;
  // Campos de Pesaje Semanal (registro una vez por semana)
  pesoH?: number; // Peso promedio hembras (kg)
  pesoM?: number; // Peso promedio machos (kg)
  uniformidad?: number; // Uniformidad del lote (%)
  coeficienteVariacion?: number; // Coeficiente de variación (CV)
  observacionesPesaje?: string; // Observaciones específicas del pesaje
}

export interface SeguimientoItemDto {
  id: number;
  produccionLoteId: number;
  fechaRegistro: string; // ISO date
  mortalidadH: number;
  mortalidadM: number;
  selH: number;
  consKgH: number;
  consKgM: number;
  consumoKg: number; // Para compatibilidad (suma de consKgH + consKgM)
  huevosTotales: number;
  huevosIncubables: number;
  tipoAlimento: string;
  pesoHuevo: number;
  etapa: number;
  observaciones?: string;
  createdAt: string;
  updatedAt?: string;
  // Campos de Clasificadora de Huevos
  huevoLimpio?: number;
  huevoTratado?: number;
  huevoSucio?: number;
  huevoDeforme?: number;
  huevoBlanco?: number;
  huevoDobleYema?: number;
  huevoPiso?: number;
  huevoPequeno?: number;
  huevoRoto?: number;
  huevoDesecho?: number;
  huevoOtro?: number;
  // Campos de Pesaje Semanal
  pesoH?: number;
  pesoM?: number;
  uniformidad?: number;
  coeficienteVariacion?: number;
  observacionesPesaje?: string;
}

export interface ListaSeguimientoResponse {
  items: SeguimientoItemDto[];
  total: number;
}

export interface ListaSeguimientoQuery {
  loteId: number;
  desde?: string;
  hasta?: string;
  page?: number;
  size?: number;
}

@Injectable({
  providedIn: 'root'
})
export class ProduccionService {
  private readonly baseUrl = `${environment.apiUrl}/Produccion`;

  constructor(private http: HttpClient) {}

  /**
   * Verifica si existe un registro inicial de producción para un lote
   */
  existsProduccionLote(loteId: number): Observable<ExisteProduccionLoteResponse> {
    return this.http.get<ExisteProduccionLoteResponse>(`${this.baseUrl}/lotes/${loteId}/exists`);
  }

  /**
   * Crea un nuevo registro inicial de producción para un lote
   */
  crearProduccionLote(payload: CrearProduccionLoteRequest): Observable<number> {
    const headers = { 'Content-Type': 'application/json' };
    return this.http.post<number>(`${this.baseUrl}/lotes`, payload, { headers });
  }

  /**
   * Obtiene el detalle del registro inicial de producción de un lote
   */
  getProduccionLote(loteId: number): Observable<ProduccionLoteDetalleDto> {
    return this.http.get<ProduccionLoteDetalleDto>(`${this.baseUrl}/lotes/${loteId}`);
  }

  /**
   * Crea un nuevo seguimiento diario de producción
   */
  crearSeguimiento(payload: CrearSeguimientoRequest): Observable<number> {
    return this.http.post<number>(`${this.baseUrl}/seguimiento`, payload);
  }

  /**
   * Lista los seguimientos diarios de producción de un lote
   */
  listarSeguimiento(query: ListaSeguimientoQuery): Observable<ListaSeguimientoResponse> {
    let params = new HttpParams();

    params = params.set('loteId', query.loteId.toString());

    if (query.desde) {
      params = params.set('desde', query.desde);
    }

    if (query.hasta) {
      params = params.set('hasta', query.hasta);
    }

    if (query.page) {
      params = params.set('page', query.page.toString());
    }

    if (query.size) {
      params = params.set('size', query.size.toString());
    }

    return this.http.get<ListaSeguimientoResponse>(`${this.baseUrl}/seguimiento`, { params });
  }

  /**
   * Obtiene el detalle completo de un seguimiento por su ID
   */
  obtenerSeguimientoPorId(id: number): Observable<SeguimientoItemDto> {
    return this.http.get<SeguimientoItemDto>(`${this.baseUrl}/seguimiento/${id}`);
  }

  /**
   * Elimina un seguimiento diario de producción
   */
  eliminarSeguimiento(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/seguimiento/${id}`);
  }

  /**
   * Obtiene los lotes que tienen semana 26 o superior (para módulo de producción)
   * Solo incluye lotes que han alcanzado la semana 26 desde su fecha de encaset
   */
  obtenerLotesProduccion(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/lotes-produccion`);
  }

  /**
   * Calcula la liquidación técnica de producción para un lote
   * Organizado por etapas: 1 (25-33), 2 (34-50), 3 (>50)
   */
  calcularLiquidacionProduccion(loteId: number, fechaHasta?: string): Observable<LiquidacionTecnicaProduccionDto> {
    const request: LiquidacionTecnicaProduccionRequest = {
      loteId,
      fechaHasta: fechaHasta ? new Date(fechaHasta).toISOString() : undefined
    };
    return this.http.post<LiquidacionTecnicaProduccionDto>(`${this.baseUrl}/liquidacion-tecnica`, request);
  }

  /**
   * Verifica si un lote tiene datos válidos para liquidación técnica de producción
   */
  validarLoteParaLiquidacionProduccion(loteId: number): Observable<boolean> {
    return this.http.get<boolean>(`${this.baseUrl}/liquidacion-tecnica/validar/${loteId}`);
  }

  /**
   * Obtiene el resumen de una etapa específica
   */
  obtenerResumenEtapa(loteId: number, etapa: number): Observable<EtapaLiquidacionDto> {
    return this.http.get<EtapaLiquidacionDto>(`${this.baseUrl}/liquidacion-tecnica/etapa/${loteId}/${etapa}`);
  }

  // ================== INDICADORES SEMANALES ==================

  /**
   * Obtiene indicadores semanales de producción agrupados por semana
   * Compara con guía genética cuando está disponible
   */
  obtenerIndicadoresSemanales(request: IndicadoresProduccionRequest): Observable<IndicadoresProduccionResponse> {
    return this.http.post<IndicadoresProduccionResponse>(`${this.baseUrl}/indicadores-semanales`, request);
  }

  /**
   * Obtiene indicadores para una semana específica
   */
  obtenerIndicadorSemana(loteId: number, semana: number): Observable<IndicadorProduccionSemanalDto> {
    return this.http.get<IndicadorProduccionSemanalDto>(`${this.baseUrl}/indicadores-semanales/${loteId}/${semana}`);
  }
}

// ================== DTOs INDICADORES SEMANALES ==================

export interface IndicadorProduccionSemanalDto {
  semana: number;
  fechaInicioSemana: string;
  fechaFinSemana: string;
  totalRegistros: number;

  // Mortalidad
  mortalidadHembras: number;
  mortalidadMachos: number;
  porcentajeMortalidadHembras: number;
  porcentajeMortalidadMachos: number;
  mortalidadGuiaHembras: number;
  mortalidadGuiaMachos: number;
  diferenciaMortalidadHembras?: number | null;
  diferenciaMortalidadMachos?: number | null;

  // Selección
  seleccionHembras: number;
  porcentajeSeleccionHembras: number;

  // Consumo (kg)
  consumoKgHembras: number;
  consumoKgMachos: number;
  consumoTotalKg: number;
  consumoPromedioDiarioKg: number;
  consumoGuiaHembras?: number | null;
  consumoGuiaMachos?: number | null;
  diferenciaConsumoHembras?: number | null;
  diferenciaConsumoMachos?: number | null;

  // Producción de Huevos
  huevosTotales: number;
  huevosIncubables: number;
  promedioHuevosPorDia: number;
  eficienciaProduccion: number;
  huevosTotalesGuia?: number | null;
  huevosIncubablesGuia?: number | null;
  porcentajeProduccionGuia?: number | null;
  diferenciaHuevosTotales?: number | null;
  diferenciaHuevosIncubables?: number | null;
  diferenciaPorcentajeProduccion?: number | null;

  // Peso Huevo
  pesoHuevoPromedio?: number | null;
  pesoHuevoGuia?: number | null;
  diferenciaPesoHuevo?: number | null;

  // Peso Aves
  pesoPromedioHembras?: number | null;
  pesoPromedioMachos?: number | null;
  pesoGuiaHembras?: number | null;
  pesoGuiaMachos?: number | null;
  diferenciaPesoHembras?: number | null;
  diferenciaPesoMachos?: number | null;

  // Uniformidad
  uniformidadPromedio?: number | null;
  uniformidadGuia?: number | null;
  diferenciaUniformidad?: number | null;

  // Coeficiente de Variación
  coeficienteVariacionPromedio?: number | null;

  // Clasificadora de Huevos
  huevosLimpios: number;
  huevosTratados: number;
  huevosSucios: number;
  huevosDeformes: number;
  huevosBlancos: number;
  huevosDobleYema: number;
  huevosPiso: number;
  huevosPequenos: number;
  huevosRotos: number;
  huevosDesecho: number;
  huevosOtro: number;

  // Aves
  avesHembrasInicioSemana: number;
  avesMachosInicioSemana: number;
  avesHembrasFinSemana: number;
  avesMachosFinSemana: number;
}

export interface IndicadoresProduccionRequest {
  loteId: number;
  fechaDesde?: string | null;
  fechaHasta?: string | null;
  semanaDesde?: number | null;
  semanaHasta?: number | null;
}

export interface IndicadoresProduccionResponse {
  indicadores: IndicadorProduccionSemanalDto[];
  totalSemanas: number;
  semanaInicial: number;
  semanaFinal: number;
  tieneDatosGuiaGenetica: boolean;
}

// ==================== DTOs para Liquidación Técnica de Producción ====================

export interface LiquidacionTecnicaProduccionDto {
  loteId: string;
  loteNombre: string;
  fechaEncaset: string;
  raza?: string;
  anoTablaGenetica?: number;
  hembrasIniciales: number;
  machosIniciales: number;
  huevosIniciales: number;
  etapa1: EtapaLiquidacionDto;
  etapa2: EtapaLiquidacionDto;
  etapa3: EtapaLiquidacionDto;
  totales: MetricasAcumuladasProduccionDto;
  comparacionGuia?: ComparacionGuiaProduccionDto;
  fechaCalculo: string;
  totalRegistrosSeguimiento: number;
  fechaUltimoSeguimiento?: string;
  semanaActual: number;
}

export interface EtapaLiquidacionDto {
  etapa: number;
  nombre: string;
  semanaDesde: number;
  semanaHasta?: number;
  totalRegistros: number;
  mortalidadHembras: number;
  mortalidadMachos: number;
  porcentajeMortalidadHembras: number;
  porcentajeMortalidadMachos: number;
  seleccionHembras: number;
  porcentajeSeleccionHembras: number;
  consumoKgHembras: number;
  consumoKgMachos: number;
  consumoTotalKg: number;
  huevosTotales: number;
  huevosIncubables: number;
  promedioHuevosPorDia: number;
  eficienciaProduccion: number;
  pesoHembras?: number;
  pesoMachos?: number;
  uniformidad?: number;
  huevosLimpios: number;
  huevosTratados: number;
  huevosSucios: number;
  huevosDeformes: number;
  huevosBlancos: number;
  huevosDobleYema: number;
  huevosPiso: number;
  huevosPequenos: number;
  huevosRotos: number;
  huevosDesecho: number;
  huevosOtro: number;
  pesoPromedioHembras?: number;
  pesoPromedioMachos?: number;
  uniformidadPromedio?: number;
  coeficienteVariacionPromedio?: number;
}

export interface MetricasAcumuladasProduccionDto {
  totalMortalidadHembras: number;
  totalMortalidadMachos: number;
  porcentajeMortalidadAcumuladaHembras: number;
  porcentajeMortalidadAcumuladaMachos: number;
  totalSeleccionHembras: number;
  porcentajeSeleccionAcumuladaHembras: number;
  consumoTotalKgHembras: number;
  consumoTotalKgMachos: number;
  consumoTotalKg: number;
  consumoPromedioDiarioKg: number;
  totalHuevosTotales: number;
  totalHuevosIncubables: number;
  promedioHuevosPorDia: number;
  eficienciaProduccionTotal: number;
  avesHembrasActuales: number;
  avesMachosActuales: number;
  totalAvesActuales: number;
}

export interface ComparacionGuiaProduccionDto {
  // Consumo
  consumoGuiaHembras?: number;
  consumoGuiaMachos?: number;
  diferenciaConsumoHembras?: number;
  diferenciaConsumoMachos?: number;
  // Peso
  pesoGuiaHembras?: number;
  pesoGuiaMachos?: number;
  diferenciaPesoHembras?: number;
  diferenciaPesoMachos?: number;
  // Mortalidad
  mortalidadGuiaHembras?: number;
  mortalidadGuiaMachos?: number;
  diferenciaMortalidadHembras?: number;
  diferenciaMortalidadMachos?: number;
  // Uniformidad
  uniformidadGuia?: number;
  uniformidadReal?: number;
  diferenciaUniformidad?: number;
  // Producción de Huevos (Guía Genética)
  huevosTotalesGuia?: number;
  porcentajeProduccionGuia?: number;
  huevosIncubablesGuia?: number;
  pesoHuevoGuia?: number;
  masaHuevoGuia?: number;
  gramosHuevoTotalGuia?: number;
  gramosHuevoIncubableGuia?: number;
  aprovechamientoSemanalGuia?: number;
  aprovechamientoAcumuladoGuia?: number;
  // Producción de Huevos (Real)
  huevosTotalesReal?: number;
  porcentajeProduccionReal?: number;
  huevosIncubablesReal?: number;
  pesoHuevoReal?: number;
  eficienciaReal?: number;
  // Diferencias de Producción
  diferenciaHuevosTotales?: number;
  diferenciaPorcentajeProduccion?: number;
  diferenciaHuevosIncubables?: number;
  diferenciaPesoHuevo?: number;
  diferenciaMasaHuevo?: number;
  // Datos adicionales de guía genética
  nacimientoPorcentajeGuia?: number;
  pollitosAveAlojadaGuia?: number;
  gramosPollitoGuia?: number;
  apareoGuia?: number;
  kcalAveDiaHGuia?: number;
  kcalAveDiaMGuia?: number;
  // Retiro acumulado de guía
  retiroAcumuladoHembrasGuia?: number;
  retiroAcumuladoMachosGuia?: number;
}

export interface LiquidacionTecnicaProduccionRequest {
  loteId: number;
  fechaHasta?: string;
  etapaFiltro?: number;
}
