// src/app/features/config/item-inventario-ecuador/services/item-inventario-ecuador.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../../environments/environment';

export interface ItemInventarioEcuadorDto {
  id: number;
  codigo: string;
  nombre: string;
  tipoItem: string;
  unidad: string;
  descripcion?: string | null;
  activo: boolean;
  grupo?: string | null;
  tipoInventarioCodigo?: string | null;
  descripcionTipoInventario?: string | null;
  referencia?: string | null;
  descripcionItem?: string | null;
  concepto?: string | null;
  companyId: number;
  paisId: number;
  createdAt: string;
  updatedAt: string;
}

export interface ItemInventarioEcuadorCreateRequest {
  codigo: string;
  nombre: string;
  tipoItem: string;
  unidad: string;
  descripcion?: string | null;
  activo: boolean;
  grupo?: string | null;
  tipoInventarioCodigo?: string | null;
  descripcionTipoInventario?: string | null;
  referencia?: string | null;
  descripcionItem?: string | null;
  concepto?: string | null;
}

export interface ItemInventarioEcuadorUpdateRequest {
  nombre: string;
  tipoItem: string;
  unidad: string;
  descripcion?: string | null;
  activo: boolean;
  grupo?: string | null;
  tipoInventarioCodigo?: string | null;
  descripcionTipoInventario?: string | null;
  referencia?: string | null;
  descripcionItem?: string | null;
  concepto?: string | null;
}

/** Fila para carga masiva. Columnas: GRUPO, TIPO DE INVENTARIO, Desc. tipo inventario, Tipo inventario, Referencia, Desc. item, Concepto, Unidad de medida */
export interface ItemInventarioEcuadorCargaMasivaRow {
  grupo?: string | null;
  tipoInventarioCodigo?: string | null;
  descripcionTipoInventario?: string | null;
  tipoItem?: string | null;
  referencia?: string | null;
  descripcionItem?: string | null;
  concepto?: string | null;
  unidad?: string | null;
}

export interface ItemInventarioEcuadorCargaMasivaResult {
  totalFilas: number;
  creados: number;
  actualizados: number;
  errores: number;
  mensajesError: string[];
}

@Injectable({ providedIn: 'root' })
export class ItemInventarioEcuadorService {
  private readonly baseUrl = `${environment.apiUrl}/item-inventario-ecuador`;

  constructor(private http: HttpClient) {}

  getAll(q?: string, tipoItem?: string, activo?: boolean): Observable<ItemInventarioEcuadorDto[]> {
    let params = new HttpParams();
    if (q) params = params.set('q', q);
    if (tipoItem) params = params.set('tipoItem', tipoItem);
    if (activo !== undefined) params = params.set('activo', String(activo));
    return this.http.get<ItemInventarioEcuadorDto[]>(this.baseUrl, { params });
  }

  getById(id: number): Observable<ItemInventarioEcuadorDto> {
    return this.http.get<ItemInventarioEcuadorDto>(`${this.baseUrl}/${id}`);
  }

  create(req: ItemInventarioEcuadorCreateRequest): Observable<ItemInventarioEcuadorDto> {
    return this.http.post<ItemInventarioEcuadorDto>(this.baseUrl, req);
  }

  update(id: number, req: ItemInventarioEcuadorUpdateRequest): Observable<ItemInventarioEcuadorDto> {
    return this.http.put<ItemInventarioEcuadorDto>(`${this.baseUrl}/${id}`, req);
  }

  delete(id: number, hard = false): Observable<void> {
    let params = new HttpParams();
    if (hard) params = params.set('hard', 'true');
    return this.http.delete<void>(`${this.baseUrl}/${id}`, { params });
  }

  cargaMasiva(filas: ItemInventarioEcuadorCargaMasivaRow[]): Observable<ItemInventarioEcuadorCargaMasivaResult> {
    return this.http.post<ItemInventarioEcuadorCargaMasivaResult>(`${this.baseUrl}/carga-masiva`, filas);
  }

  cargaMasivaExcel(file: File): Observable<ItemInventarioEcuadorCargaMasivaResult> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<ItemInventarioEcuadorCargaMasivaResult>(`${this.baseUrl}/carga-masiva-excel`, form);
  }
}
