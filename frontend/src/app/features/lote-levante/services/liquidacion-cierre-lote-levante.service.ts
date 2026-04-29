import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface LiquidacionCierreLoteLevanteDto {
  lotePosturaLevanteId: number;
  loteNombre: string;
  raza?: string;
  anoTablaGenetica?: number;

  // Aves encasetadas
  hembrasEncasetadas?: number;
  machosEncasetados?: number;

  // Retiro acumulado hembras
  porcentajeMortalidadHembras: number;
  porcentajeSeleccionHembras: number;
  porcentajeErrorSexajeHembras: number;
  porcentajeRetiroAcumulado: number;
  porcentajeRetiroGuia?: number;

  // Consumo alimento
  consumoAlimentoRealGramos: number;
  consumoAlimentoGuiaGramos?: number;
  porcentajeDiferenciaConsumo?: number;

  // Peso semana 25
  pesoSemana25Real?: number;
  pesoSemana25Guia?: number;
  porcentajeDiferenciaPeso?: number;

  // Uniformidad
  uniformidadReal?: number;
  uniformidadGuia?: number;
  porcentajeDiferenciaUniformidad?: number;

  // Metadatos
  fechaCalculo: string;
  totalRegistrosSeguimiento: number;
  semanaUltimoRegistro?: number;
  tieneGuiaGenetica: boolean;
}

export interface LiquidacionCierreGuardadaDto {
  id: number;
  lotePosturaLevanteId: number;
  fechaCierre: string;
  datos: LiquidacionCierreLoteLevanteDto;
}

@Injectable({ providedIn: 'root' })
export class LiquidacionCierreLoteLevanteService {
  private readonly base = `${environment.apiUrl}/LiquidacionCierreLoteLevante`;

  constructor(private http: HttpClient) {}

  /** Calcula las variables sin guardar. */
  calcular(lotePosturaLevanteId: number): Observable<LiquidacionCierreLoteLevanteDto> {
    return this.http.get<LiquidacionCierreLoteLevanteDto>(
      `${this.base}/${lotePosturaLevanteId}/calcular`
    );
  }

  /** Guarda la liquidación de cierre en la BD. */
  guardar(lotePosturaLevanteId: number): Observable<LiquidacionCierreGuardadaDto> {
    return this.http.post<LiquidacionCierreGuardadaDto>(
      `${this.base}/${lotePosturaLevanteId}/guardar`,
      {}
    );
  }

  /** Obtiene la liquidación guardada (404 si no existe). */
  obtenerPorLote(lotePosturaLevanteId: number): Observable<LiquidacionCierreGuardadaDto> {
    return this.http.get<LiquidacionCierreGuardadaDto>(
      `${this.base}/${lotePosturaLevanteId}`
    );
  }
}
