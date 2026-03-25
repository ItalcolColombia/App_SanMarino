import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface LoteFilterItemDto {
  loteId: number;
  loteNombre: string;
  granjaId: number;
  nucleoId: string | null;
  galponId: string | null;
  loteErp?: string | null;
}

export interface FarmDto {
  id: number;
  name: string;
  companyId: number;
}

export interface NucleoDto {
  nucleoId: string;
  granjaId: number;
  nucleoNombre: string;
  granjaNombre?: string;
}

export interface GalponLiteDto {
  galponId: string;
  galponNombre: string;
  nucleoId: string;
  granjaId: number;
}

export interface FilterDataResponse {
  farms: FarmDto[];
  nucleos: NucleoDto[];
  galpones: GalponLiteDto[];
  lotes: LoteFilterItemDto[];
}

export interface InventarioGastoItemStockDto {
  itemInventarioEcuadorId: number;
  codigo: string;
  nombre: string;
  tipoItem: string;
  unidad: string;
  concepto: string | null;
  stockCantidad: number;
}

export interface InventarioGastoLineaRequest {
  itemInventarioEcuadorId: number;
  cantidad: number;
}

export interface CreateInventarioGastoRequest {
  farmId: number;
  nucleoId: string | null;
  galponId: string | null;
  loteAveEngordeId: number | null;
  fecha: string; // yyyy-MM-dd
  observaciones?: string | null;
  concepto: string;
  lineas: InventarioGastoLineaRequest[];
}

export interface InventarioGastoListItemDto {
  id: number;
  fecha: string;
  farmId: number;
  granjaNombre: string | null;
  nucleoId: string | null;
  nucleoNombre: string | null;
  galponId: string | null;
  galponNombre: string | null;
  loteAveEngordeId: number | null;
  loteNombre: string | null;
  observaciones: string | null;
  estado: string;
  lineas: number;
  totalCantidad: number;
  unidad: string | null;
  createdAt: string;
  createdByUserId: string | null;
}

export interface InventarioGastoDetalleDto {
  id: number;
  itemInventarioEcuadorId: number;
  itemCodigo: string;
  itemNombre: string;
  itemType: string;
  concepto: string | null;
  cantidad: number;
  unidad: string;
  stockAntes: number | null;
  stockDespues: number | null;
}

export interface InventarioGastoExportRowDto {
  inventarioGastoId: number;
  fecha: string;
  estado: string;
  observacionesCabecera: string | null;
  farmId: number;
  granjaNombre: string;
  nucleoId: string | null;
  nucleoNombre: string | null;
  galponId: string | null;
  galponNombre: string | null;
  loteAveEngordeId: number | null;
  loteNombre: string | null;
  detalleId: number;
  itemInventarioEcuadorId: number;
  itemCodigo: string;
  itemNombre: string;
  itemTipo: string;
  conceptoLinea: string | null;
  cantidad: number;
  unidad: string;
  stockAntes: number | null;
  stockDespues: number | null;
  createdAt: string;
  createdByUserId: string | null;
  deletedAt: string | null;
  deletedByUserId: string | null;
}

export interface InventarioGastoDto {
  id: number;
  fecha: string;
  farmId: number;
  nucleoId: string | null;
  galponId: string | null;
  loteAveEngordeId: number | null;
  loteNombre: string | null;
  observaciones: string | null;
  estado: string;
  createdAt: string;
  createdByUserId: string | null;
  deletedAt: string | null;
  deletedByUserId: string | null;
  detalles: InventarioGastoDetalleDto[];
}

@Injectable({ providedIn: 'root' })
export class InventarioGastosService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getFilterData(): Observable<FilterDataResponse> {
    return this.http.get<FilterDataResponse>(`${this.api}/inventario-gastos/filter-data`);
  }

  getConceptos(): Observable<string[]> {
    return this.http.get<string[]>(`${this.api}/inventario-gastos/conceptos`);
  }

  getItems(params: { farmId: number; concepto: string }): Observable<InventarioGastoItemStockDto[]> {
    const httpParams = new HttpParams()
      .set('farmId', params.farmId)
      .set('concepto', params.concepto);
    return this.http.get<InventarioGastoItemStockDto[]>(`${this.api}/inventario-gastos/items`, { params: httpParams });
  }

  search(params: {
    farmId?: number;
    nucleoId?: string;
    galponId?: string;
    loteAveEngordeId?: number;
    fechaDesde?: string;
    fechaHasta?: string;
    concepto?: string;
    search?: string;
    estado?: string;
  } = {}): Observable<InventarioGastoListItemDto[]> {
    let httpParams = new HttpParams();
    if (params.farmId != null) httpParams = httpParams.set('farmId', params.farmId);
    if (params.nucleoId) httpParams = httpParams.set('nucleoId', params.nucleoId);
    if (params.galponId) httpParams = httpParams.set('galponId', params.galponId);
    if (params.loteAveEngordeId != null) httpParams = httpParams.set('loteAveEngordeId', params.loteAveEngordeId);
    if (params.fechaDesde) httpParams = httpParams.set('fechaDesde', params.fechaDesde);
    if (params.fechaHasta) httpParams = httpParams.set('fechaHasta', params.fechaHasta);
    if (params.concepto) httpParams = httpParams.set('concepto', params.concepto);
    if (params.search) httpParams = httpParams.set('search', params.search);
    if (params.estado) httpParams = httpParams.set('estado', params.estado);
    return this.http.get<InventarioGastoListItemDto[]>(`${this.api}/inventario-gastos`, { params: httpParams });
  }

  /** Una fila por línea de consumo; incluye nombres de granja, núcleo y galpón. */
  export(params: {
    farmId?: number;
    nucleoId?: string;
    galponId?: string;
    loteAveEngordeId?: number;
    fechaDesde?: string;
    fechaHasta?: string;
    concepto?: string;
    search?: string;
    estado?: string;
  } = {}): Observable<InventarioGastoExportRowDto[]> {
    let httpParams = new HttpParams();
    if (params.farmId != null) httpParams = httpParams.set('farmId', params.farmId);
    if (params.nucleoId) httpParams = httpParams.set('nucleoId', params.nucleoId);
    if (params.galponId) httpParams = httpParams.set('galponId', params.galponId);
    if (params.loteAveEngordeId != null) httpParams = httpParams.set('loteAveEngordeId', params.loteAveEngordeId);
    if (params.fechaDesde) httpParams = httpParams.set('fechaDesde', params.fechaDesde);
    if (params.fechaHasta) httpParams = httpParams.set('fechaHasta', params.fechaHasta);
    if (params.concepto) httpParams = httpParams.set('concepto', params.concepto);
    if (params.search) httpParams = httpParams.set('search', params.search);
    if (params.estado) httpParams = httpParams.set('estado', params.estado);
    return this.http.get<InventarioGastoExportRowDto[]>(`${this.api}/inventario-gastos/export`, { params: httpParams });
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

