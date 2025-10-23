// src/app/features/galpon/services/galpon.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { GalponDetailDto, CreateGalponDto, UpdateGalponDto } from '../models/galpon.models';

@Injectable({ providedIn: 'root' })
export class GalponService {
  private readonly baseUrl = `${environment.apiUrl}/Galpon`;
  private readonly http = inject(HttpClient);

  constructor() {}

  /** Listado (detalle) */
  getAll(): Observable<GalponDetailDto[]> {
    return this.http.get<GalponDetailDto[]>(this.baseUrl);
  }

  /** Detalle por id */
  getById(id: string): Observable<GalponDetailDto> {
    return this.http.get<GalponDetailDto>(`${this.baseUrl}/${id}`);
  }

  /** Búsqueda paginada */
  search(params: {
    page?: number; pageSize?: number; search?: string;
    granjaId?: number; nucleoId?: string; tipoGalpon?: string;
    sortBy?: string; sortDesc?: boolean; soloActivos?: boolean;
  }): Observable<{ page: number; pageSize: number; total: number; items: GalponDetailDto[] }> {
    let p = new HttpParams();
    for (const [k, v] of Object.entries(params || {})) {
      if (v !== undefined && v !== null && v !== '') p = p.set(k, String(v));
    }
    return this.http.get<{ page: number; pageSize: number; total: number; items: GalponDetailDto[] }>(
      `${this.baseUrl}/search`, { params: p }
    );
  }

  /** Filtrar por granja+núcleo */
  getByGranjaAndNucleo(granjaId: number, nucleoId: string): Observable<GalponDetailDto[]> {
    return this.http.get<GalponDetailDto[]>(`${this.baseUrl}/granja/${granjaId}/nucleo/${nucleoId}`);
  }

  /** Crear */
  create(dto: CreateGalponDto): Observable<GalponDetailDto> {
    return this.http.post<GalponDetailDto>(this.baseUrl, dto);
  }

  /** Actualizar */
  update(dto: UpdateGalponDto): Observable<GalponDetailDto> {
    return this.http.put<GalponDetailDto>(`${this.baseUrl}/${dto.galponId}`, dto);
  }

  /** Borrar (soft) */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Borrar (hard) */
  hardDelete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}/hard`);
  }
}
