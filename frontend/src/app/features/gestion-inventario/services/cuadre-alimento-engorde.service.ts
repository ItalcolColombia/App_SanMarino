// src/app/features/gestion-inventario/services/cuadre-alimento-engorde.service.ts
//
// Los dos detectores del alimento de engorde:
//  · el CUADRE por galpón (saldo del ciclo activo == stock − movimientos posteriores), que existía en
//    el backend desde jul-2026 y no tenía una sola pantalla que lo mostrara;
//  · el señalamiento de la anomalía R2: lotes que se liquidaron dejando alimento en el galpón.
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

/** Veredicto del cuadre de un galpón. El API serializa el enum como número. */
export enum EstadoCuadreAlimento {
  Ok = 0,
  SaldoNegativo = 1,
  Descuadrado = 2
}

/** Veredicto de un lote liquidado con saldo de alimento. */
export enum EstadoAlimentoLiquidado {
  Trasladado = 0,
  PendienteEnGalpon = 1,
  SinRespaldoFisico = 2
}

export interface CuadreAlimentoEngordeFilaDto {
  companyId: number;
  empresa: string;
  granjaId: number;
  granja: string;
  nucleoId: string;
  galponId: string;
  loteAveEngordeId: number;
  loteNombre: string;
  estadoOperativoLote: string;
  ultimoSeguimiento: string;
  saldoTablaKg: number;
  movPostKg: number;
  stockKg: number;
  esperadoKg: number;
  descuadreKg: number;
  filasNegativas: number;
  estado: EstadoCuadreAlimento;
  detalle: string;
}

export interface CuadreAlimentoEngordeDto {
  totalGalpones: number;
  cuadran: number;
  descuadrados: number;
  conSaldoNegativo: number;
  kgErrorAbsoluto: number;
  galpones: CuadreAlimentoEngordeFilaDto[];
}

export interface AnomaliaAlimentoLiquidadoFilaDto {
  companyId: number;
  granjaId: number;
  granja: string;
  nucleoId: string;
  galponId: string;
  loteAveEngordeId: number;
  loteNombre: string;
  liquidadoAt: string;
  ultimoSeguimiento: string | null;
  saldoCongeladoKg: number;
  salidasPostKg: number;
  stockGalponKg: number;
  kgSinTrasladar: number;
  kgSinRespaldo: number;
  estado: EstadoAlimentoLiquidado;
  detalle: string;
  loteSiguienteId: number | null;
  loteSiguienteNombre: string | null;
  loteSiguienteEncaset: string | null;
}

export interface AnomaliaAlimentoLiquidadoDto {
  totalLiquidados: number;
  conSaldo: number;
  sinDatoCongelado: number;
  pendientesEnGalpon: number;
  sinRespaldoFisico: number;
  kgSinTrasladar: number;
  kgSinRespaldo: number;
  lotes: AnomaliaAlimentoLiquidadoFilaDto[];
}

@Injectable({ providedIn: 'root' })
export class CuadreAlimentoEngordeService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/CuadreAlimentoEngorde`;

  /** Cuadre de todos los galpones de la empresa activa. */
  obtenerCuadre(soloConProblemas = false): Observable<CuadreAlimentoEngordeDto> {
    const params = new HttpParams().set('soloConProblemas', soloConProblemas);
    return this.http.get<CuadreAlimentoEngordeDto>(this.api, { params });
  }

  /** Lotes ya liquidados que congelaron su liquidación con alimento en el galpón. */
  obtenerLiquidadosConAlimento(soloAnomalias = false): Observable<AnomaliaAlimentoLiquidadoDto> {
    const params = new HttpParams().set('soloAnomalias', soloAnomalias);
    return this.http.get<AnomaliaAlimentoLiquidadoDto>(`${this.api}/liquidados-con-alimento`, { params });
  }
}
