import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import { LoteDto } from '../../lote/services/lote.service';

// ==================== INTERFACES ====================

// Liquidación Técnica Básica
export interface LiquidacionTecnicaDto {
  loteId: string;
  loteNombre: string;
  fechaEncaset?: Date;
  raza?: string;
  anoTablaGenetica?: number;
  hembrasEncasetadas: number;
  machosEncasetados: number;
  totalAvesEncasetadas: number;
  porcentajeMortalidadHembras: number;
  porcentajeMortalidadMachos: number;
  porcentajeSeleccionHembras: number;
  porcentajeSeleccionMachos: number;
  porcentajeErrorSexajeHembras: number;
  porcentajeErrorSexajeMachos: number;
  porcentajeRetiroTotalHembras: number;
  porcentajeRetiroTotalMachos: number;
  porcentajeRetiroTotalGeneral: number;
  porcentajeRetiroGuia?: number;
  consumoAlimentoRealGramos: number;
  consumoAlimentoGuiaGramos?: number;
  porcentajeDiferenciaConsumo?: number;
  pesoSemana25RealHembras?: number;
  pesoSemana25RealMachos?: number;
  pesoSemana25GuiaHembras?: number;
  porcentajeDiferenciaPesoHembras?: number;
  uniformidadRealHembras?: number;
  uniformidadRealMachos?: number;
  uniformidadGuiaHembras?: number;
  porcentajeDiferenciaUniformidadHembras?: number;
}

// Liquidación Técnica Completa
export interface LiquidacionTecnicaCompletaDto {
  resumen: LiquidacionTecnicaDto;
  detallesSeguimiento: SeguimientoLoteLevanteDto[];
  detallesGuiaGenetica: ProduccionAvicolaRawDto[];
}

// Request para cálculo
export interface LiquidacionTecnicaRequest {
  loteId: string;
  fechaHasta: Date;
}

// Seguimiento semanal (reutilizando del servicio existente)
export interface SeguimientoLoteLevanteDto {
  id: number;
  loteId: string;
  semana: number;
  fechaRegistro: Date;
  mortalidadHembras?: number;
  mortalidadMachos?: number;
  seleccionHembras?: number;
  seleccionMachos?: number;
  errorSexajeHembras?: number;
  errorSexajeMachos?: number;
  consumoAlimento?: number;
  pesoPromedioHembras?: number;
  pesoPromedioMachos?: number;
  uniformidadHembras?: number;
  uniformidadMachos?: number;
  observaciones?: string;
}

// Guía genética
export interface ProduccionAvicolaRawDto {
  id: number;
  anioGuia?: string;
  raza?: string;
  edad?: string;
  mortSemH?: string;
  retiroAcH?: string;
  mortSemM?: string;
  retiroAcM?: string;
  consAcH?: string;
  consAcM?: string;
  pesoH?: string;
  pesoM?: string;
  uniformidad?: string;
  // Campos adicionales según necesidad
  [key: string]: any;
}

@Injectable({
  providedIn: 'root'
})
export class LiquidacionTecnicaService {
  private baseUrl = `${environment.apiUrl}/LiquidacionTecnica`;

  constructor(private http: HttpClient) {}

  /**
   * Obtener liquidación técnica simple
   */
  getLiquidacionTecnica(loteId: string, fechaHasta: Date): Observable<LiquidacionTecnicaDto> {
    const params = new HttpParams().set('fechaHasta', fechaHasta.toISOString());
    return this.http.get<LiquidacionTecnicaDto>(`${this.baseUrl}/${loteId}`, { params });
  }

  /**
   * Obtener liquidación técnica completa con detalles
   */
  getLiquidacionCompleta(loteId: string, fechaHasta: Date): Observable<LiquidacionTecnicaCompletaDto> {
    const params = new HttpParams().set('fechaHasta', fechaHasta.toISOString());
    return this.http.get<LiquidacionTecnicaCompletaDto>(`${this.baseUrl}/${loteId}/completa`, { params });
  }

  /**
   * Calcular liquidación técnica
   */
  calcularLiquidacion(request: LiquidacionTecnicaRequest): Observable<LiquidacionTecnicaDto> {
    return this.http.post<LiquidacionTecnicaDto>(`${this.baseUrl}/calcular`, request);
  }

  /**
   * Validar si un lote existe y tiene datos para liquidación
   */
  validarLote(loteId: string): Observable<boolean> {
    return this.http.get<boolean>(`${this.baseUrl}/${loteId}/validar`);
  }

  /**
   * Validar múltiples lotes
   */
  validarMultiplesLotes(loteIds: string[]): Observable<{[key: string]: boolean}> {
    return this.http.post<{[key: string]: boolean}>(`${this.baseUrl}/validar-multiples`, loteIds);
  }

  /**
   * Obtener datos completos del lote para liquidación técnica
   * NOTA: Usa el servicio de lotes directamente en lugar de llamar al API
   */
  obtenerDatosCompletosLote(loteId: string): Observable<LoteDto> {
    // Obtener información del lote de la liquidación que ya tenemos
    // No necesitamos hacer una petición adicional
    return this.getLiquidacionTecnica(loteId, new Date()).pipe(
      map(liquidacion => {
        return {
          loteId: parseInt(liquidacion.loteId),
          loteNombre: liquidacion.loteNombre,
          fechaEncaset: liquidacion.fechaEncaset,
          raza: liquidacion.raza,
          anoTablaGenetica: liquidacion.anoTablaGenetica,
          hembrasL: liquidacion.hembrasEncasetadas,
          machosL: liquidacion.machosEncasetados,
          avesEncasetadas: liquidacion.totalAvesEncasetadas
        } as LoteDto;
      })
    );
  }
}
