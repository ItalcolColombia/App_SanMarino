// src/app/features/traslados-aves/services/traslados-aves.service.ts
// Traslados y ventas de aves y huevos (`/api/traslados`), cohortes y traslado de LOTE completo.
//
// El 3-sep-2026 este archivo tenia 747 lineas y mezclaba CUATRO dominios: inventario de aves,
// movimientos, historial/trazabilidad y traslados. Se partio por dominio:
//   · `InventarioAvesService`      -> /api/InventarioAves
//   · `HistorialInventarioService` -> /api/HistorialInventario
//   · este                          -> /api/traslados (+ cohortes y Lote/trasladar)
// y las 29 interfaces se movieron a `../models/`. Se RE-EXPORTAN desde aca, asi que los imports
// que ya existian (`from '.../services/traslados-aves.service'`) siguen compilando sin tocarse.
//
// De paso salieron 15 metodos sin un solo llamador (createMovimiento, getInventarioByLote,
// validarTraslado, procesarMovimiento, searchMovimientos, getLotesDisponibles, ...): eran el resto
// del modal de traslado del dashboard, que estaba muerto, y del formulario de huevos que se
// reemplazo por `ModalTrasladoHuevosComponent`.
import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import { manejarErrorHttp } from '../funciones/manejar-error-http.funcion';
import {
  CohortesLoteDto,
  CrearTrasladoAvesDto,
  DisponibilidadLoteDto,
  HistorialTrasladoLoteDto,
  MovimientoAvesDto,
  ResultadoMovimientoDto,
  TrasladoAvesDesdeSegDiarioDto,
  TrasladoAvesResultSegDto,
  TrasladoHuevosDto,
  TrasladoLoteRequest,
  TrasladoLoteResponse
} from '../models';

// Re-export de TODOS los tipos del modulo: los consumidores siguen importando de este archivo.
export * from '../models';

@Injectable({
  providedIn: 'root'
})
export class TrasladosAvesService {
  private movimientoUrl = `${environment.apiUrl}/MovimientoAves`;
  private trasladosUrl = `${environment.apiUrl}/traslados`;
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // =====================================================
  // DISPONIBILIDAD
  // =====================================================

  /** Disponibilidad del lote: informa el bloque `aves` siempre y `huevos` cuando hay LPP. */
  getDisponibilidadLote(loteId: string): Observable<DisponibilidadLoteDto> {
    return this.http.get<DisponibilidadLoteDto>(`${this.trasladosUrl}/lote/${loteId}/disponibilidad`)
      .pipe(catchError(this.handleError));
  }

  // =====================================================
  // TRASLADO / VENTA DE AVES  (Camino A)
  // =====================================================

  /** Traslado o venta de aves; la operacion la decide `TipoOperacion` en el DTO. */
  crearTrasladoAves(dto: CrearTrasladoAvesDto): Observable<MovimientoAvesDto> {
    return this.http.post<MovimientoAvesDto>(`${this.trasladosUrl}/aves`, dto)
      .pipe(catchError(this.handleError));
  }

  /** Movimientos de aves del lote (origen o destino). */
  getMovimientosAvesPorLote(loteId: number): Observable<MovimientoAvesDto[]> {
    return this.http.get<MovimientoAvesDto[]>(`${this.apiUrl}/MovimientoAves/lote/${loteId}`)
      .pipe(catchError(this.handleError));
  }

  /** Anula un movimiento: devuelve las aves al lote si ya se habia aplicado. */
  cancelarMovimiento(id: number, motivo: string): Observable<ResultadoMovimientoDto> {
    return this.http
      .post<ResultadoMovimientoDto>(`${this.movimientoUrl}/${id}/cancelar`, {
        motivoCancelacion: motivo,
        motivo
      })
      .pipe(catchError(this.handleError));
  }

  // =====================================================
  // TRASLADO / VENTA DE HUEVOS
  // =====================================================

  /** Traslados de huevos registrados para el lote. */
  getTrasladosHuevosPorLote(loteId: string): Observable<TrasladoHuevosDto[]> {
    return this.http.get<TrasladoHuevosDto[]>(`${this.trasladosUrl}/huevos/lote/${loteId}`)
      .pipe(catchError(this.handleError));
  }

  // =====================================================
  // TRASLADO DE LOTE COMPLETO  (reubicar un lote de granja)
  // =====================================================

  crearTrasladoLote(dto: TrasladoLoteRequest): Observable<TrasladoLoteResponse> {
    return this.http.post<TrasladoLoteResponse>(`${environment.apiUrl}/Lote/trasladar`, dto)
      .pipe(catchError(this.handleError));
  }

  getHistorialTrasladosLote(loteId: number): Observable<HistorialTrasladoLoteDto[]> {
    return this.http.get<HistorialTrasladoLoteDto[]>(`${environment.apiUrl}/Lote/${loteId}/historial-traslados`)
      .pipe(catchError(this.handleError));
  }

  // =====================================================
  // TRASLADO DESDE SEGUIMIENTO DIARIO  (Camino C)
  // =====================================================

  /**
   * Traslado de aves desde el seguimiento diario.
   *
   * OJO: `LoteOrigenId`/`LoteDestinoId` de este DTO son ids de ESPEJO
   * (`lote_postura_levante` / `lote_postura_produccion`), no de `lotes` — al reves que
   * `crearTrasladoAves`. Es el unico camino con gate de etapa por empresa
   * (`permite_traslado_aves_cross_etapa`).
   */
  ejecutarTrasladoDesdeSegDiario(dto: TrasladoAvesDesdeSegDiarioDto): Observable<TrasladoAvesResultSegDto> {
    return this.http.post<TrasladoAvesResultSegDto>(
      `${this.trasladosUrl}/aves-desde-seguimiento`, dto
    ).pipe(catchError(this.handleError));
  }

  // =====================================================
  // COHORTES (edades de las aves dentro de un lote)
  // =====================================================

  getCohortesLote(loteId: number): Observable<CohortesLoteDto> {
    return this.http.get<CohortesLoteDto>(
      `${this.trasladosUrl}/cohortes/${loteId}`
    ).pipe(catchError(this.handleError));
  }

  /** @param loteAveEngordeId ID de `lote_ave_engorde` (no el lote base de postura). */
  getCohortesLoteEngorde(loteAveEngordeId: number): Observable<CohortesLoteDto> {
    return this.http.get<CohortesLoteDto>(
      `${environment.apiUrl}/MovimientoPolloEngorde/cohortes/${loteAveEngordeId}`
    ).pipe(catchError(this.handleError));
  }

  private handleError(error: HttpErrorResponse): Observable<never> {
    return manejarErrorHttp(error, 'TrasladosAvesService');
  }
}
