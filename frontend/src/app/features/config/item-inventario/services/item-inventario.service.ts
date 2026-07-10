// src/app/features/config/item-inventario/services/item-inventario.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../../environments/environment';

export interface ItemInventarioDto {
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

export interface ItemInventarioCreateRequest {
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

export interface ItemInventarioUpdateRequest {
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
export interface ItemInventarioCargaMasivaRow {
  grupo?: string | null;
  tipoInventarioCodigo?: string | null;
  descripcionTipoInventario?: string | null;
  tipoItem?: string | null;
  referencia?: string | null;
  descripcionItem?: string | null;
  concepto?: string | null;
  unidad?: string | null;
}

export interface ItemInventarioCargaMasivaResult {
  totalFilas: number;
  creados: number;
  actualizados: number;
  errores: number;
  mensajesError: string[];
}

@Injectable({ providedIn: 'root' })
export class ItemInventarioService {
  // Ruta neutra del backend (alias vigente: /api/item-inventario-ecuador). Módulo compartido EC/PA/CO.
  private readonly baseUrl = `${environment.apiUrl}/inventario/items`;

  constructor(private http: HttpClient) {}

  getAll(q?: string, tipoItem?: string, activo?: boolean): Observable<ItemInventarioDto[]> {
    let params = new HttpParams();
    if (q) params = params.set('q', q);
    if (tipoItem) params = params.set('tipoItem', tipoItem);
    if (activo !== undefined) params = params.set('activo', String(activo));
    return this.http.get<ItemInventarioDto[]>(this.baseUrl, { params });
  }

  getById(id: number): Observable<ItemInventarioDto> {
    return this.http.get<ItemInventarioDto>(`${this.baseUrl}/${id}`);
  }

  create(req: ItemInventarioCreateRequest): Observable<ItemInventarioDto> {
    return this.http.post<ItemInventarioDto>(this.baseUrl, req);
  }

  update(id: number, req: ItemInventarioUpdateRequest): Observable<ItemInventarioDto> {
    return this.http.put<ItemInventarioDto>(`${this.baseUrl}/${id}`, req);
  }

  delete(id: number, hard = false): Observable<void> {
    let params = new HttpParams();
    if (hard) params = params.set('hard', 'true');
    return this.http.delete<void>(`${this.baseUrl}/${id}`, { params });
  }

  cargaMasiva(filas: ItemInventarioCargaMasivaRow[]): Observable<ItemInventarioCargaMasivaResult> {
    return this.http.post<ItemInventarioCargaMasivaResult>(`${this.baseUrl}/carga-masiva`, filas);
  }

  cargaMasivaExcel(file: File): Observable<ItemInventarioCargaMasivaResult> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<ItemInventarioCargaMasivaResult>(`${this.baseUrl}/carga-masiva-excel`, form);
  }
}
