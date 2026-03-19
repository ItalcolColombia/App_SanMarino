// src/app/features/gestion-inventario/services/gestion-inventario.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

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

export interface InventarioGestionFilterDataDto {
  farms: FarmDto[];
  nucleos: NucleoDto[];
  galpones: GalponLiteDto[];
}

export interface InventarioGestionStockDto {
  id: number;
  farmId: number;
  nucleoId: string | null;
  galponId: string | null;
  itemInventarioEcuadorId: number;
  itemCodigo: string;
  itemNombre: string;
  itemType: string;
  quantity: number;
  unit: string;
  granjaNombre?: string;
  nucleoNombre?: string;
  galponNombre?: string;
}

export interface InventarioGestionIngresoRequest {
  farmId: number;
  nucleoId: string | null;
  galponId: string | null;
  itemInventarioEcuadorId: number;
  quantity: number;
  unit: string;
  reference?: string | null;
  reason?: string | null;
  /** Origen para estado en histórico: "planta" | "granja" */
  origenTipo?: string | null;
}

export interface InventarioGestionTrasladoRequest {
  fromFarmId: number;
  fromNucleoId: string | null;
  fromGalponId: string | null;
  toFarmId: number;
  toNucleoId: string | null;
  toGalponId: string | null;
  itemInventarioEcuadorId: number;
  quantity: number;
  unit: string;
  reference?: string | null;
  reason?: string | null;
  /** Destino para estado en histórico: "granja" | "planta" */
  destinoTipo?: string | null;
}

/** Registro del histórico de movimientos. */
export interface InventarioGestionMovimientoDto {
  id: number;
  farmId: number;
  nucleoId: string | null;
  galponId: string | null;
  itemInventarioEcuadorId: number;
  itemCodigo: string;
  itemNombre: string;
  itemType: string;
  quantity: number;
  unit: string;
  movementType: string;
  estado: string | null;
  fromFarmId: number | null;
  fromNucleoId: string | null;
  fromGalponId: string | null;
  reference: string | null;
  reason: string | null;
  createdAt: string;
  granjaNombre?: string | null;
  nucleoNombre?: string | null;
  galponNombre?: string | null;
}

/** Ítem del catálogo item_inventario_ecuador (Config > Ítems inventario Ecuador). */
export interface ItemInventarioEcuadorDto {
  id: number;
  codigo: string;
  nombre: string;
  tipoItem: string;
  concepto?: string | null;
  unidad: string;
  descripcion?: string | null;
  activo: boolean;
}

@Injectable({ providedIn: 'root' })
export class GestionInventarioService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getFilterData(): Observable<InventarioGestionFilterDataDto> {
    return this.http.get<InventarioGestionFilterDataDto>(`${this.api}/inventario-gestion/filter-data`);
  }

  getStock(params: {
    farmId?: number;
    nucleoId?: string;
    galponId?: string;
    itemType?: string;
    search?: string;
  } = {}): Observable<InventarioGestionStockDto[]> {
    let httpParams = new HttpParams();
    if (params.farmId != null) httpParams = httpParams.set('farmId', params.farmId);
    if (params.nucleoId) httpParams = httpParams.set('nucleoId', params.nucleoId);
    if (params.galponId) httpParams = httpParams.set('galponId', params.galponId);
    if (params.itemType) httpParams = httpParams.set('itemType', params.itemType);
    if (params.search) httpParams = httpParams.set('search', params.search);
    return this.http.get<InventarioGestionStockDto[]>(`${this.api}/inventario-gestion/stock`, { params: httpParams });
  }

  registrarIngreso(payload: InventarioGestionIngresoRequest): Observable<InventarioGestionStockDto> {
    return this.http.post<InventarioGestionStockDto>(`${this.api}/inventario-gestion/ingreso`, payload);
  }

  registrarTraslado(payload: InventarioGestionTrasladoRequest): Observable<{ origen: InventarioGestionStockDto; destino: InventarioGestionStockDto }> {
    return this.http.post<{ origen: InventarioGestionStockDto; destino: InventarioGestionStockDto }>(`${this.api}/inventario-gestion/traslado`, payload);
  }

  /** Histórico de movimientos (entradas, salidas, traslados). */
  getMovimientos(params: {
    farmId?: number;
    fechaDesde?: string;
    fechaHasta?: string;
    estado?: string;
    movementType?: string;
  } = {}): Observable<InventarioGestionMovimientoDto[]> {
    let httpParams = new HttpParams();
    if (params.farmId != null) httpParams = httpParams.set('farmId', params.farmId);
    if (params.fechaDesde) httpParams = httpParams.set('fechaDesde', params.fechaDesde);
    if (params.fechaHasta) httpParams = httpParams.set('fechaHasta', params.fechaHasta);
    if (params.estado) httpParams = httpParams.set('estado', params.estado);
    if (params.movementType) httpParams = httpParams.set('movementType', params.movementType);
    return this.http.get<InventarioGestionMovimientoDto[]>(`${this.api}/inventario-gestion/movimientos`, { params: httpParams });
  }

  /** Ítems desde Config > Ítems inventario Ecuador (item_inventario_ecuador). */
  getItemsByType(tipoItem: string | null = null, search: string | null = null, activo = true): Observable<ItemInventarioEcuadorDto[]> {
    let httpParams = new HttpParams();
    if (tipoItem) httpParams = httpParams.set('tipoItem', tipoItem);
    if (search) httpParams = httpParams.set('q', search);
    if (activo !== undefined) httpParams = httpParams.set('activo', String(activo));
    return this.http.get<ItemInventarioEcuadorDto[]>(`${this.api}/item-inventario-ecuador`, { params: httpParams });
  }
}
