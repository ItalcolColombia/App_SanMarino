import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import type { MovimientoPolloEngordeDto } from '../../movimientos-pollo-engorde/services/movimiento-pollo-engorde.service';

export type { MovimientoPolloEngordeDto };

// Reutilizamos los mismos DTOs que Levante (misma estructura de datos)
import type {
  SeguimientoLoteLevanteDto,
  CreateSeguimientoLoteLevanteDto,
  UpdateSeguimientoLoteLevanteDto,
  ResultadoLevanteResponse
} from '../../lote-levante/services/seguimiento-lote-levante.service';

export type {
  SeguimientoLoteLevanteDto,
  CreateSeguimientoLoteLevanteDto,
  UpdateSeguimientoLoteLevanteDto,
  ResultadoLevanteResponse
};

/**
 * Respuesta unificada de GET SeguimientoAvesEngorde/por-lote/{loteId}:
 * registros diarios + historial (inventario y ventas).
 */
export interface SeguimientoAvesEngordePorLoteResponseDto {
  seguimientos: SeguimientoLoteLevanteDto[];
  historicoUnificado: LoteRegistroHistoricoUnificadoDto[];
}

/** Fila de lote_registro_historico_unificado (también en por-lote unificado). */
export interface LiquidacionLoteEngordeResumenDto {
  loteAveEngordeId: number;
  loteNombre: string;
  estadoOperativoLote: string;
  hembrasInicio: number | null;
  machosInicio: number | null;
  mixtasInicio: number | null;
  totalAvesInicio: number;
  ventasTotalHembras: number;
  ventasTotalMachos: number;
  ventasTotalMixtas: number;
  /** Aves vivas actuales (inicio - bajas seguimiento - ventas). Debe ser 0 para permitir liquidación. */
  avesVivasActuales: number;
  movimientosVentaCount: number;
  saldoAlimentoKg: number | null;
}

export interface LoteRegistroHistoricoUnificadoDto {
  id: number;
  companyId: number;
  loteAveEngordeId: number | null;
  farmId: number;
  nucleoId: string | null;
  galponId: string | null;
  fechaOperacion: string;
  tipoEvento: string;
  origenTabla: string;
  origenId: number;
  movementTypeOriginal: string | null;
  itemInventarioEcuadorId: number | null;
  itemResumen: string | null;
  cantidadKg: number | null;
  unidad: string | null;
  cantidadHembras: number | null;
  cantidadMachos: number | null;
  cantidadMixtas: number | null;
  referencia: string | null;
  numeroDocumento: string | null;
  acumuladoEntradasAlimentoKg: number | null;
  anulado: boolean;
  createdAt: string;
}

// ── DTOs Cuadrar Saldos ────────────────────────────────────────────────────

export interface FilaExcelCuadrarSaldosDto {
  fecha: string;
  saldoAlimentoKg: number | null;
  ingresoAlimentoKg: number | null;
  trasladoEntradaKg: number | null;
  trasladoSalidaKg: number | null;
  documento: string | null;
  consumoKg: number | null;
  consumoAcumuladoKg: number | null;
}

export interface InconsistenciaCuadrarSaldosDto {
  fecha: string;
  tipo: string;
  descripcion: string;
  valorExcel: number | null;
  valorSistema: number | null;
  historicoId: number | null;
  documentoExcel: string | null;
  documentoSistema: string | null;
}

export interface AccionCorreccionCuadrarSaldosDto {
  tipoAccion: string;
  historicoId: number | null;
  nuevaFecha: string | null;
  fechaInsertar: string | null;
  tipoEvento: string | null;
  cantidadKg: number | null;
  documento: string | null;
  descripcion: string | null;
}

export interface CuadrarSaldosValidarResponseDto {
  loteId: number;
  filasExcel: number;
  inconsistenciasCount: number;
  inconsistencias: InconsistenciaCuadrarSaldosDto[];
  accionesSugeridas: AccionCorreccionCuadrarSaldosDto[];
}

export interface CuadrarSaldosAplicarResponseDto {
  loteId: number;
  fechasAjustadas: number;
  registrosAnulados: number;
  registrosInsertados: number;
  mensaje: string;
}

// ──────────────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class SeguimientoAvesEngordeService {
  private readonly baseUrl = `${environment.apiUrl}/SeguimientoAvesEngorde`;
  private readonly movUrl = `${environment.apiUrl}/MovimientoPolloEngorde`;

  constructor(private http: HttpClient) {}

  getById(id: number): Observable<SeguimientoLoteLevanteDto> {
    return this.http.get<SeguimientoLoteLevanteDto>(`${this.baseUrl}/${id}`);
  }

  /** Incluye seguimientos e historicoUnificado en una sola respuesta. */
  getByLoteId(loteId: number): Observable<SeguimientoAvesEngordePorLoteResponseDto> {
    return this.http.get<SeguimientoAvesEngordePorLoteResponseDto>(
      `${this.baseUrl}/por-lote/${encodeURIComponent(loteId.toString())}`
    );
  }

  getHistoricoUnificadoPorLote(loteId: number): Observable<LoteRegistroHistoricoUnificadoDto[]> {
    return this.http.get<LoteRegistroHistoricoUnificadoDto[]>(
      `${this.baseUrl}/por-lote/${encodeURIComponent(loteId.toString())}/historico-unificado`
    );
  }

  /** Resumen para liquidar lote (ventas, aves inicio, saldo alimento). */
  getResumenLiquidacion(loteId: number): Observable<LiquidacionLoteEngordeResumenDto> {
    return this.http.get<LiquidacionLoteEngordeResumenDto>(
      `${this.baseUrl}/por-lote/${encodeURIComponent(loteId.toString())}/resumen-liquidacion`
    );
  }

  /**
   * Ventas (Venta/Despacho/Retiro) del lote con información completa de peso individual y global.
   * Usa el endpoint MovimientoPolloEngorde/por-lote/{loteId}/ventas-con-peso.
   */
  getVentasConPeso(loteId: number): Observable<MovimientoPolloEngordeDto[]> {
    return this.http.get<MovimientoPolloEngordeDto[]>(
      `${this.movUrl}/por-lote/${encodeURIComponent(loteId.toString())}/ventas-con-peso`
    );
  }

  filter(params: { loteId?: string; desde?: string | Date; hasta?: string | Date }): Observable<SeguimientoLoteLevanteDto[]> {
    let hp = new HttpParams();
    if (params.loteId) hp = hp.set('loteId', params.loteId);
    if (params.desde) hp = hp.set('desde', this.toIso(params.desde));
    if (params.hasta) hp = hp.set('hasta', this.toIso(params.hasta));
    return this.http.get<SeguimientoLoteLevanteDto[]>(`${this.baseUrl}/filtro`, { params: hp });
  }

  create(dto: CreateSeguimientoLoteLevanteDto): Observable<SeguimientoLoteLevanteDto> {
    const body = { ...dto, tipoSeguimiento: 'engorde' as const };
    return this.http.post<SeguimientoLoteLevanteDto>(this.baseUrl, body);
  }

  update(dto: UpdateSeguimientoLoteLevanteDto): Observable<SeguimientoLoteLevanteDto> {
    const body = { ...dto, tipoSeguimiento: 'engorde' as const };
    return this.http.put<SeguimientoLoteLevanteDto>(`${this.baseUrl}/${dto.id}`, body);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  private toIso(d: string | Date): string {
    const dd = typeof d === 'string' ? new Date(d) : d;
    return dd.toISOString();
  }

  cuadrarSaldosValidar(
    loteId: number,
    filasExcel: FilaExcelCuadrarSaldosDto[]
  ): Observable<CuadrarSaldosValidarResponseDto> {
    return this.http.post<CuadrarSaldosValidarResponseDto>(
      `${this.baseUrl}/por-lote/${encodeURIComponent(loteId.toString())}/cuadrar-saldos/validar`,
      { filasExcel }
    );
  }

  cuadrarSaldosAplicar(
    loteId: number,
    acciones: AccionCorreccionCuadrarSaldosDto[]
  ): Observable<CuadrarSaldosAplicarResponseDto> {
    return this.http.post<CuadrarSaldosAplicarResponseDto>(
      `${this.baseUrl}/por-lote/${encodeURIComponent(loteId.toString())}/cuadrar-saldos/aplicar`,
      { acciones }
    );
  }

  getResultado(params: {
    loteId: number;
    desde?: string | Date;
    hasta?: string | Date;
    recalcular?: boolean;
  }): Observable<ResultadoLevanteResponse> {
    const { loteId } = params;
    let hp = new HttpParams().set('recalcular', String(params.recalcular ?? true));
    if (params.desde) hp = hp.set('desde', this.toIso(params.desde));
    if (params.hasta) hp = hp.set('hasta', this.toIso(params.hasta));
    const url = `${this.baseUrl}/por-lote/${encodeURIComponent(loteId)}/resultado`;
    return this.http.get<ResultadoLevanteResponse>(url, { params: hp });
  }
}
