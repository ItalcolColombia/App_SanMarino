// src/app/features/lesiones/services/lesion.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, Subject, tap } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  LesionDto,
  CreateLesionRequest,
  UpdateLesionRequest,
  LesionSearchRequest,
  LesionResumenDto,
  PagedResult,
  ModuloOrigenLesion
} from '../models/lesion.models';

/**
 * Servicio HTTP para gestión de lesiones de aves (módulo Panamá).
 * Consume los endpoints REST expuestos por LesionController.
 */
@Injectable({ providedIn: 'root' })
export class LesionService {
  private readonly baseUrl = `${environment.apiUrl}/lesiones`;
  private readonly http = inject(HttpClient);
  /** Subject para abrir el modal de creación de lesión desde otros componentes */
  readonly openCreate$ = new Subject<void>();
  /** Subject que emite cuando se debe refrescar el histórico/listados */
  readonly refresh$ = new Subject<void>();

  /**
   * Búsqueda paginada de lesiones. Cualquier filtro vacío/undefined se omite
   * de los query params para no enviar valores ruidosos al backend.
   */
  search(req: LesionSearchRequest = {}): Observable<PagedResult<LesionDto>> {
    return this.http.get<PagedResult<LesionDto>>(
      `${this.baseUrl}/search`,
      { params: this.toHttpParams(req) }
    );
  }

  /** Obtiene una lesión por su id. */
  getById(id: number): Observable<LesionDto> {
    return this.http.get<LesionDto>(`${this.baseUrl}/${id}`);
  }

  /** Crea una nueva lesión. */
  create(dto: CreateLesionRequest): Observable<LesionDto> {
    return this.http.post<LesionDto>(this.baseUrl, dto).pipe(
      tap(() => this.refresh$.next())
    );
  }

  /** Actualiza una lesión existente. */
  update(id: number, dto: UpdateLesionRequest): Observable<LesionDto> {
    return this.http.put<LesionDto>(`${this.baseUrl}/${id}`, dto).pipe(
      tap(() => this.refresh$.next())
    );
  }

  /** Elimina (soft o hard según backend) una lesión. */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`).pipe(
      tap(() => this.refresh$.next())
    );
  }

  /**
   * Resumen agregado por tipo de lesión. Acepta los mismos filtros base
   * (módulo, granja, lote, cliente) que la búsqueda paginada.
   */
  getResumen(filters: {
    moduloOrigen?: ModuloOrigenLesion;
    farmId?: number;
    loteId?: number;
    clienteId?: number;
    galponId?: string;
    loteReproductoraId?: string;
  } = {}): Observable<LesionResumenDto[]> {
    return this.http.get<LesionResumenDto[]>(
      `${this.baseUrl}/resumen`,
      { params: this.toHttpParams(filters) }
    );
  }

  /** Construye HttpParams a partir de un objeto, omitiendo valores vacíos. */
  private toHttpParams<T extends object>(obj: T): HttpParams {
    let params = new HttpParams();
    for (const [k, v] of Object.entries(obj)) {
      if (v !== undefined && v !== null && v !== '') {
        params = params.set(k, String(v));
      }
    }
    return params;
  }
}
