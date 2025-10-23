// src/app/features/lote-levante/services/liquidacion-comparacion.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface LiquidacionTecnicaComparacionDto {
  // Datos del lote
  loteId: number;
  loteNombre: string;
  raza?: string;
  anoTablaGenetica?: number;
  lineaGeneticaId?: number;
  nombreGuiaGenetica?: string;
  
  // Métricas reales del lote
  porcentajeMortalidadHembras: number;
  porcentajeMortalidadMachos: number;
  consumoAcumuladoHembras: number;
  consumoAcumuladoMachos: number;
  pesoFinalHembras?: number;
  pesoFinalMachos?: number;
  uniformidadFinalHembras?: number;
  uniformidadFinalMachos?: number;
  
  // Valores esperados de la guía genética
  mortalidadEsperadaHembras?: number;
  mortalidadEsperadaMachos?: number;
  consumoAcumuladoEsperadoHembras?: number;
  consumoAcumuladoEsperadoMachos?: number;
  pesoEsperadoHembras?: number;
  pesoEsperadoMachos?: number;
  uniformidadEsperadaHembras?: number;
  uniformidadEsperadaMachos?: number;
  
  // Diferencias calculadas
  diferenciaMortalidadHembras?: number;
  diferenciaMortalidadMachos?: number;
  diferenciaConsumoHembras?: number;
  diferenciaConsumoMachos?: number;
  diferenciaPesoHembras?: number;
  diferenciaPesoMachos?: number;
  diferenciaUniformidadHembras?: number;
  diferenciaUniformidadMachos?: number;
  
  // Evaluación de cumplimiento
  cumpleMortalidadHembras: boolean;
  cumpleMortalidadMachos: boolean;
  cumpleConsumoHembras: boolean;
  cumpleConsumoMachos: boolean;
  cumplePesoHembras: boolean;
  cumplePesoMachos: boolean;
  cumpleUniformidadHembras: boolean;
  cumpleUniformidadMachos: boolean;
  
  // Resumen general
  totalParametrosEvaluados: number;
  parametrosCumplidos: number;
  porcentajeCumplimiento: number;
  estadoGeneral: string; // "Excelente", "Bueno", "Regular", "Deficiente"
  
  // Metadatos
  fechaCalculo: string;
  totalRegistrosSeguimiento: number;
  fechaUltimoSeguimiento?: string;
}

export interface ComparacionDetalladaDto {
  parametro: string;
  valorReal: number;
  valorEsperado?: number;
  diferencia?: number;
  tolerancia?: number;
  cumple: boolean;
  estado: string; // "Cumple", "Excede", "Por debajo"
}

export interface LiquidacionTecnicaComparacionCompletaDto {
  resumen: LiquidacionTecnicaComparacionDto;
  comparacionesDetalladas: ComparacionDetalladaDto[];
  seguimientos: any[]; // DetalleSeguimientoLiquidacionDto[]
  observaciones?: string;
}

@Injectable({
  providedIn: 'root'
})
export class LiquidacionComparacionService {
  private baseUrl = `${environment.apiUrl}/api/LiquidacionTecnicaComparacion`;

  constructor(private http: HttpClient) {}

  /**
   * Compara los datos del lote con la guía genética correspondiente
   */
  compararConGuiaGenetica(loteId: number, fechaHasta?: string): Observable<LiquidacionTecnicaComparacionDto> {
    let url = `${this.baseUrl}/lote/${loteId}`;
    if (fechaHasta) {
      url += `?fechaHasta=${fechaHasta}`;
    }
    return this.http.get<LiquidacionTecnicaComparacionDto>(url);
  }

  /**
   * Alias para compatibilidad con el componente
   */
  getComparacionBasica(loteId: number, fechaHasta?: Date): Observable<LiquidacionTecnicaComparacionDto> {
    const fechaHastaStr = fechaHasta ? fechaHasta.toISOString() : undefined;
    return this.compararConGuiaGenetica(loteId, fechaHastaStr);
  }

  /**
   * Obtiene la comparación completa con detalles
   */
  obtenerComparacionCompleta(loteId: number, fechaHasta?: string): Observable<LiquidacionTecnicaComparacionCompletaDto> {
    let url = `${this.baseUrl}/lote/${loteId}/completa`;
    if (fechaHasta) {
      url += `?fechaHasta=${fechaHasta}`;
    }
    return this.http.get<LiquidacionTecnicaComparacionCompletaDto>(url);
  }

  /**
   * Valida si un lote tiene guía genética configurada
   */
  validarGuiaGeneticaConfigurada(loteId: number): Observable<{ tieneGuia: boolean }> {
    return this.http.get<{ tieneGuia: boolean }>(`${this.baseUrl}/lote/${loteId}/validar-guia`);
  }

  /**
   * Obtiene las líneas genéticas disponibles desde ProduccionAvicolaRaw
   */
  obtenerLineasGeneticasDisponibles(raza?: string, ano?: number): Observable<any[]> {
    let url = `${this.baseUrl}/lineas-geneticas`;
    const params = new URLSearchParams();
    if (raza) params.append('raza', raza);
    if (ano) params.append('ano', ano.toString());
    
    if (params.toString()) {
      url += `?${params.toString()}`;
    }
    
    return this.http.get<any[]>(url);
  }
}
