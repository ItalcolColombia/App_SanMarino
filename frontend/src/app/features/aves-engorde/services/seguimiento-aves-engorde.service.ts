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
  /** Merma ya registrada por Costos (para pre-poblar el modal). */
  mermaUnidades: number | null;
  mermaKilos: number | null;
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

// ── Tabla diaria precalculada (fn_seguimiento_diario_engorde) ────────────────

export interface SeguimientoDiarioTablaFilaDto {
  /**
   * ID del seguimiento diario. Es null cuando la fila representa un movimiento
   * de inventario o venta de aves que aún no tiene un seguimiento diario asociado.
   * En ese caso, los campos de mortalidad, consumo, peso, etc. son 0 / null y el
   * frontend debe ocultar acciones de edit/delete (ofrecer "Crear seguimiento aquí").
   */
  segId: number | null;
  fecha: string;
  edadDia: number;
  semana: number;
  mortalidadHembras: number;
  mortalidadMachos: number;
  selH: number;
  selM: number;
  errorSexajeHembras: number;
  errorSexajeMachos: number;
  totalMortSelDia: number;
  perdidasTotalesDia: number;
  consumoKgHembras: number;
  consumoKgMachos: number;
  consumoDiaKg: number;
  acumConsumoKg: number;
  saldoAves: number;
  pctPerdidasDia: number;
  saldoAlimentoKg: number;
  ingresoAlimentoKg: number;
  trasladoEntradaKg: number;
  trasladoSalidaKg: number;
  consumoBodegaKg: number;
  documento: string | null;
  despachoHembras: number;
  despachoMachos: number;
  despachoMixtas: number;
  /** Peso INDIVIDUAL real de la venta de este lote en la fecha (R3.5), no el global de factura. */
  despachoPesoNeto?: number;
  despachoPesoTara?: number;
  despachoPromedioPesoAve?: number;
  tipoAlimento: string | null;
  pesoPromHembras: number | null;
  pesoPromMachos: number | null;
  uniformidadHembras: number | null;
  uniformidadMachos: number | null;
  cvHembras: number | null;
  cvMachos: number | null;
  consumoAguaDiario: number | null;
  consumoAguaPh: number | null;
  consumoAguaOrp: number | null;
  consumoAguaTemperatura: number | null;
  observaciones: string | null;
  ciclo: string | null;
  metadata: string | null;
  itemsAdicionales: string | null;
  historicoConsumoAlimento: string | null;
  createdByUserId: string | null;
}

// ── DTOs Cuadrar Saldos ────────────────────────────────────────────────────
// (fuente unica en engorde-comun; se re-exportan para compatibilidad)
import {
  FilaExcelCuadrarSaldosDto,
  AccionCorreccionCuadrarSaldosDto,
  CuadrarSaldosValidarResponseDto,
  CuadrarSaldosAplicarResponseDto
} from '../../engorde-comun/models/cuadrar-saldos-engorde.models';
export * from '../../engorde-comun/models/cuadrar-saldos-engorde.models';


// ──────────────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class SeguimientoAvesEngordeService {
  private readonly baseUrl = `${environment.apiUrl}/SeguimientoAvesEngordeEcuador`;
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

  /** Tabla diaria calculada por fn_seguimiento_diario_engorde (backend recalcula saldo alimento antes). */
  getTablaDiaria(loteId: number): Observable<SeguimientoDiarioTablaFilaDto[]> {
    return this.http.get<SeguimientoDiarioTablaFilaDto[]>(
      `${this.baseUrl}/por-lote/${encodeURIComponent(loteId.toString())}/tabla-diaria`
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

  // Cuadrar saldos: las rutas viven SOLO en el controller SeguimientoAvesEngorde
  // (mismo esquema de datos: tabla compartida seguimiento_diario_aves_engorde).
  // Con baseUrl (SeguimientoAvesEngordeEcuador) daban 404.
  private readonly cuadrarUrl = `${environment.apiUrl}/SeguimientoAvesEngorde`;

  cuadrarSaldosValidar(
    loteId: number,
    filasExcel: FilaExcelCuadrarSaldosDto[]
  ): Observable<CuadrarSaldosValidarResponseDto> {
    return this.http.post<CuadrarSaldosValidarResponseDto>(
      `${this.cuadrarUrl}/por-lote/${encodeURIComponent(loteId.toString())}/cuadrar-saldos/validar`,
      { filasExcel }
    );
  }

  cuadrarSaldosAplicar(
    loteId: number,
    acciones: AccionCorreccionCuadrarSaldosDto[],
    filasExcel?: FilaExcelCuadrarSaldosDto[]
  ): Observable<CuadrarSaldosAplicarResponseDto> {
    return this.http.post<CuadrarSaldosAplicarResponseDto>(
      `${this.cuadrarUrl}/por-lote/${encodeURIComponent(loteId.toString())}/cuadrar-saldos/aplicar`,
      { acciones, filasExcel: filasExcel ?? [] }
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