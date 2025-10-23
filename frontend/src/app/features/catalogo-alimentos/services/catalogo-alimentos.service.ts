// apps/features/catalogo-alimentos/services/catalogo-alimentos.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export type CatalogItemType =
  | 'alimento'
  | 'medicamento'
  | 'accesorio'
  | 'biologico'
  | 'consumible'
  | 'otro';

export interface CatalogItemDto {
  id?: number;
  codigo: string;
  nombre: string;
  metadata?: any;   // aquí vive type_item, especie, raza, genero y otros
  activo: boolean;
}

export interface CatalogItemCreateRequest {
  codigo: string;
  nombre: string;
  metadata?: any;
  activo: boolean;
}

export interface CatalogItemUpdateRequest {
  nombre: string;
  metadata?: any;
  activo: boolean;
}

@Injectable({ providedIn: 'root' })
export class CatalogoAlimentosService {
  private readonly baseUrl = `${environment.apiUrl}/catalogo-alimentos`; // mismo endpoint, compatible

  constructor(private http: HttpClient) {}

  list(q = '', page = 1, pageSize = 20): Observable<PagedResult<CatalogItemDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (q && q.trim().length > 0) params = params.set('q', q.trim());
    return this.http.get<PagedResult<CatalogItemDto>>(this.baseUrl, { params });
  }

  getById(id: number): Observable<CatalogItemDto> {
    return this.http.get<CatalogItemDto>(`${this.baseUrl}/${id}`);
  }

  create(dto: CatalogItemCreateRequest): Observable<CatalogItemDto> {
    return this.http.post<CatalogItemDto>(this.baseUrl, dto);
  }

  update(id: number, dto: CatalogItemUpdateRequest): Observable<CatalogItemDto> {
    return this.http.put<CatalogItemDto>(`${this.baseUrl}/${id}`, dto);
  }

  delete(id: number, hard = false): Observable<void> {
    let params = new HttpParams();
    if (hard) params = params.set('hard', true);
    return this.http.delete<void>(`${this.baseUrl}/${id}`, { params });
  }
}
