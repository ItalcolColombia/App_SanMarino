// src/app/features/implementacion/services/implementacion.service.ts
// HTTP del módulo Implementación. La empresa/país activos viajan solos por headers del AuthInterceptor.
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  ImplementacionConfirmarRequest,
  ImplementacionMiTareaDto,
  ImplementacionPlanCreateRequest,
  ImplementacionPlanDetalleDto,
  ImplementacionPlanDto,
  ImplementacionPlanUpdateRequest,
  ImplementacionRolAsignableDto,
  ImplementacionTareaDto,
  ImplementacionTareaRequest,
  ImplementacionUsuarioAsignableDto,
} from '../models/implementacion.models';

@Injectable({ providedIn: 'root' })
export class ImplementacionService {
  private readonly base = `${environment.apiUrl}/Implementacion`;

  constructor(private http: HttpClient) {}

  // Planes
  getPlanes(): Observable<ImplementacionPlanDto[]> {
    return this.http.get<ImplementacionPlanDto[]>(`${this.base}/planes`);
  }

  getPlanDetalle(id: number): Observable<ImplementacionPlanDetalleDto> {
    return this.http.get<ImplementacionPlanDetalleDto>(`${this.base}/planes/${id}`);
  }

  createPlan(req: ImplementacionPlanCreateRequest): Observable<ImplementacionPlanDto> {
    return this.http.post<ImplementacionPlanDto>(`${this.base}/planes`, req);
  }

  updatePlan(id: number, req: ImplementacionPlanUpdateRequest): Observable<ImplementacionPlanDto> {
    return this.http.put<ImplementacionPlanDto>(`${this.base}/planes/${id}`, req);
  }

  deletePlan(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/planes/${id}`);
  }

  // Tareas
  createTarea(planId: number, req: ImplementacionTareaRequest): Observable<ImplementacionTareaDto> {
    return this.http.post<ImplementacionTareaDto>(`${this.base}/planes/${planId}/tareas`, req);
  }

  updateTarea(tareaId: number, req: ImplementacionTareaRequest): Observable<ImplementacionTareaDto> {
    return this.http.put<ImplementacionTareaDto>(`${this.base}/tareas/${tareaId}`, req);
  }

  deleteTarea(tareaId: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/tareas/${tareaId}`);
  }

  completarTarea(tareaId: number): Observable<ImplementacionTareaDto> {
    return this.http.post<ImplementacionTareaDto>(`${this.base}/tareas/${tareaId}/completar`, {});
  }

  confirmarTarea(tareaId: number, req: ImplementacionConfirmarRequest): Observable<ImplementacionTareaDto> {
    return this.http.post<ImplementacionTareaDto>(`${this.base}/tareas/${tareaId}/confirmar`, req);
  }

  reabrirTarea(tareaId: number): Observable<ImplementacionTareaDto> {
    return this.http.post<ImplementacionTareaDto>(`${this.base}/tareas/${tareaId}/reabrir`, {});
  }

  // Consultas de apoyo
  getMisTareas(): Observable<ImplementacionMiTareaDto[]> {
    return this.http.get<ImplementacionMiTareaDto[]>(`${this.base}/mis-tareas`);
  }

  getUsuariosAsignables(): Observable<ImplementacionUsuarioAsignableDto[]> {
    return this.http.get<ImplementacionUsuarioAsignableDto[]>(`${this.base}/usuarios-asignables`);
  }

  getRolesAsignables(): Observable<ImplementacionRolAsignableDto[]> {
    return this.http.get<ImplementacionRolAsignableDto[]>(`${this.base}/roles-asignables`);
  }
}
