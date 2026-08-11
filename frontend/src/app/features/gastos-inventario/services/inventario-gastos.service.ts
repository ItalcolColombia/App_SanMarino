import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  CreateInventarioGastoRequest,
  FilterDataResponse,
  InventarioGastoDto,
  InventarioGastoExistenciaDto,
  InventarioGastoExportRowDto,
  InventarioGastoItemStockDto,
  InventarioGastoListItemDto
} from '../models/inventario-gasto.model';

// Los tipos viven en `models/` (para que `funciones/` los use sin import circular) y se re-exportan
// acá: los imports que ya apuntaban al servicio siguen funcionando igual.
export type {
  CreateInventarioGastoRequest,
  EstadoGastoFiltro,
  FarmDto,
  FilterDataResponse,
  GalponLiteDto,
  InventarioGastoDetalleDto,
  InventarioGastoDto,
  InventarioGastoExistenciaDto,
  InventarioGastoExportRowDto,
  InventarioGastoItemStockDto,
  InventarioGastoLineaRequest,
  InventarioGastoLineaResumenDto,
  InventarioGastoListItemDto,
  LoteFilterItemDto,
  NucleoDto
} from '../models/inventario-gasto.model';

/** Filtros comunes del listado y del reporte. */
interface InventarioGastoQueryParams {
  farmId?: number;
  nucleoId?: string;
  galponId?: string;
  loteAveEngordeId?: number;
  loteBaseEngordeId?: number;
  fechaDesde?: string;
  fechaHasta?: string;
  concepto?: string;
  search?: string;
  estado?: string;
}

@Injectable({ providedIn: 'root' })
export class InventarioGastosService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  /** Arma los `HttpParams` comunes del listado/reporte (omite lo vacío). */
  private buildParams(params: InventarioGastoQueryParams): HttpParams {
    let httpParams = new HttpParams();
    if (params.farmId != null) httpParams = httpParams.set('farmId', params.farmId);
    if (params.nucleoId) httpParams = httpParams.set('nucleoId', params.nucleoId);
    if (params.galponId) httpParams = httpParams.set('galponId', params.galponId);
    if (params.loteAveEngordeId != null) httpParams = httpParams.set('loteAveEngordeId', params.loteAveEngordeId);
    if (params.loteBaseEngordeId != null) httpParams = httpParams.set('loteBaseEngordeId', params.loteBaseEngordeId);
    if (params.fechaDesde) httpParams = httpParams.set('fechaDesde', params.fechaDesde);
    if (params.fechaHasta) httpParams = httpParams.set('fechaHasta', params.fechaHasta);
    if (params.concepto) httpParams = httpParams.set('concepto', params.concepto);
    if (params.search) httpParams = httpParams.set('search', params.search);
    if (params.estado) httpParams = httpParams.set('estado', params.estado);
    return httpParams;
  }

  getFilterData(): Observable<FilterDataResponse> {
    return this.http.get<FilterDataResponse>(`${this.api}/inventario-gastos/filter-data`);
  }

  /** Sin farmId: todos los conceptos de la compañía. Con farmId: solo los que tienen ítems con stock en esa granja. */
  getConceptos(farmId?: number | null): Observable<string[]> {
    let httpParams = new HttpParams();
    if (farmId != null) httpParams = httpParams.set('farmId', farmId);
    return this.http.get<string[]>(`${this.api}/inventario-gastos/conceptos`, { params: httpParams });
  }

  getItems(params: { farmId: number; concepto: string }): Observable<InventarioGastoItemStockDto[]> {
    const httpParams = new HttpParams()
      .set('farmId', params.farmId)
      .set('concepto', params.concepto);
    return this.http.get<InventarioGastoItemStockDto[]>(`${this.api}/inventario-gastos/items`, { params: httpParams });
  }

  /** Listado de la tabla. Respeta `estado` (permite consultar el historial de eliminados). */
  search(params: InventarioGastoQueryParams = {}): Observable<InventarioGastoListItemDto[]> {
    return this.http.get<InventarioGastoListItemDto[]>(`${this.api}/inventario-gastos`, {
      params: this.buildParams(params)
    });
  }

  /**
   * Hoja "Consumos" del reporte: una fila por línea, con nombres de granja, núcleo y galpón.
   * El backend EXCLUYE siempre los gastos eliminados (su stock ya volvió al inventario).
   */
  export(params: InventarioGastoQueryParams = {}): Observable<InventarioGastoExportRowDto[]> {
    return this.http.get<InventarioGastoExportRowDto[]>(`${this.api}/inventario-gastos/export`, {
      params: this.buildParams(params)
    });
  }

  /**
   * Hoja "Existencias" del reporte: TODOS los ítems no-alimento del catálogo por granja (tengan o no
   * consumo) con su saldo actual y lo consumido en el rango.
   */
  existencias(params: {
    farmId?: number;
    fechaDesde?: string;
    fechaHasta?: string;
    concepto?: string;
  } = {}): Observable<InventarioGastoExistenciaDto[]> {
    return this.http.get<InventarioGastoExistenciaDto[]>(`${this.api}/inventario-gastos/existencias`, {
      params: this.buildParams(params)
    });
  }

  getById(id: number): Observable<InventarioGastoDto> {
    return this.http.get<InventarioGastoDto>(`${this.api}/inventario-gastos/${id}`);
  }

  create(payload: CreateInventarioGastoRequest): Observable<InventarioGastoDto> {
    return this.http.post<InventarioGastoDto>(`${this.api}/inventario-gastos`, payload);
  }

  delete(id: number, motivo?: string | null): Observable<{ ok: boolean }> {
    let httpParams = new HttpParams();
    if (motivo) httpParams = httpParams.set('motivo', motivo);
    return this.http.delete<{ ok: boolean }>(`${this.api}/inventario-gastos/${id}`, { params: httpParams });
  }
}
