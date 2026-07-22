// src/app/features/implementacion/services/implementacion.service.ts
// HTTP del módulo Implementación. La empresa/país activos viajan solos por headers del AuthInterceptor.
// Todas las peticiones llevan timeout defensivo: la UI nunca queda "pensando" indefinidamente.
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, timeout } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  ImplementacionConfirmarRequest,
  ImplementacionFirmarRequest,
  ImplementacionMiFirmaDto,
  ImplementacionMiTareaDto,
  ImplementacionParticipantesRequest,
  ImplementacionPlanCreateRequest,
  ImplementacionPlanDetalleDto,
  ImplementacionPlanDto,
  ImplementacionPlanUpdateRequest,
  ImplementacionRechazarRequest,
  ImplementacionRolAsignableDto,
  ImplementacionTareaDto,
  ImplementacionTareaRequest,
  ImplementacionUsuarioAsignableDto,
} from '../models/implementacion.models';

/** Tope defensivo por petición (ms): pasado esto la promesa rechaza y la página muestra error + Reintentar. */
const TIMEOUT_MS = 30_000;

@Injectable({ providedIn: 'root' })
export class ImplementacionService {
  private readonly base = `${environment.apiUrl}/Implementacion`;

  constructor(private http: HttpClient) {}

  private conTimeout<T>(obs: Observable<T>): Observable<T> {
    return obs.pipe(timeout({ first: TIMEOUT_MS }));
  }

  // Planes
  getPlanes(): Observable<ImplementacionPlanDto[]> {
    return this.conTimeout(this.http.get<ImplementacionPlanDto[]>(`${this.base}/planes`));
  }

  getPlanDetalle(id: number): Observable<ImplementacionPlanDetalleDto> {
    return this.conTimeout(this.http.get<ImplementacionPlanDetalleDto>(`${this.base}/planes/${id}`));
  }

  createPlan(req: ImplementacionPlanCreateRequest): Observable<ImplementacionPlanDto> {
    return this.conTimeout(this.http.post<ImplementacionPlanDto>(`${this.base}/planes`, req));
  }

  updatePlan(id: number, req: ImplementacionPlanUpdateRequest): Observable<ImplementacionPlanDto> {
    return this.conTimeout(this.http.put<ImplementacionPlanDto>(`${this.base}/planes/${id}`, req));
  }

  deletePlan(id: number): Observable<void> {
    return this.conTimeout(this.http.delete<void>(`${this.base}/planes/${id}`));
  }

  // Tareas (ítems de validación del cronograma)
  createTarea(planId: number, req: ImplementacionTareaRequest): Observable<ImplementacionTareaDto> {
    return this.conTimeout(this.http.post<ImplementacionTareaDto>(`${this.base}/planes/${planId}/tareas`, req));
  }

  updateTarea(tareaId: number, req: ImplementacionTareaRequest): Observable<ImplementacionTareaDto> {
    return this.conTimeout(this.http.put<ImplementacionTareaDto>(`${this.base}/tareas/${tareaId}`, req));
  }

  deleteTarea(tareaId: number): Observable<void> {
    return this.conTimeout(this.http.delete<void>(`${this.base}/tareas/${tareaId}`));
  }

  completarTarea(tareaId: number): Observable<ImplementacionTareaDto> {
    return this.conTimeout(this.http.post<ImplementacionTareaDto>(`${this.base}/tareas/${tareaId}/completar`, {}));
  }

  confirmarTarea(tareaId: number, req: ImplementacionConfirmarRequest): Observable<ImplementacionTareaDto> {
    return this.conTimeout(this.http.post<ImplementacionTareaDto>(`${this.base}/tareas/${tareaId}/confirmar`, req));
  }

  reabrirTarea(tareaId: number): Observable<ImplementacionTareaDto> {
    return this.conTimeout(this.http.post<ImplementacionTareaDto>(`${this.base}/tareas/${tareaId}/reabrir`, {}));
  }

  // Participantes y firmas
  setParticipantes(tareaId: number, req: ImplementacionParticipantesRequest): Observable<ImplementacionTareaDto> {
    return this.conTimeout(this.http.put<ImplementacionTareaDto>(`${this.base}/tareas/${tareaId}/participantes`, req));
  }

  firmarTarea(tareaId: number, req: ImplementacionFirmarRequest): Observable<ImplementacionMiFirmaDto> {
    return this.conTimeout(this.http.post<ImplementacionMiFirmaDto>(`${this.base}/tareas/${tareaId}/firmar`, req));
  }

  rechazarTarea(tareaId: number, req: ImplementacionRechazarRequest): Observable<ImplementacionMiFirmaDto> {
    return this.conTimeout(this.http.post<ImplementacionMiFirmaDto>(`${this.base}/tareas/${tareaId}/rechazar`, req));
  }

  getMisFirmas(): Observable<ImplementacionMiFirmaDto[]> {
    return this.conTimeout(this.http.get<ImplementacionMiFirmaDto[]>(`${this.base}/mis-firmas`));
  }

  // Consultas de apoyo
  getMisTareas(): Observable<ImplementacionMiTareaDto[]> {
    return this.conTimeout(this.http.get<ImplementacionMiTareaDto[]>(`${this.base}/mis-tareas`));
  }

  getUsuariosAsignables(): Observable<ImplementacionUsuarioAsignableDto[]> {
    return this.conTimeout(this.http.get<ImplementacionUsuarioAsignableDto[]>(`${this.base}/usuarios-asignables`));
  }

  getRolesAsignables(): Observable<ImplementacionRolAsignableDto[]> {
    return this.conTimeout(this.http.get<ImplementacionRolAsignableDto[]>(`${this.base}/roles-asignables`));
  }
}
