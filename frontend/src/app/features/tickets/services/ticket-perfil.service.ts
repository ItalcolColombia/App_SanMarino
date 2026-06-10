// src/app/features/tickets/services/ticket-perfil.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface ResolutorItemRequest { tipo: string; paisId?: number | null; }
export interface UpsertTicketPerfilRequest { nivel: string; resolutores: ResolutorItemRequest[]; }
export interface UpsertTicketResolutorRolRequest { resolutores: ResolutorItemRequest[]; }

export interface ResolutorItemDto { id: number; tipo: string; paisId: number | null; activo: boolean; }
export interface TicketPerfilDto { userId: string; nivel: string; resolutores: ResolutorItemDto[]; }
export interface AsignableDto { userId: string; nombreCompleto: string; paisLabel: string | null; }
export interface TipoPermitidoDto { tipo: string; label: string; asignables: AsignableDto[]; }
export interface TicketResolutorRolDto { roleId: number; resolutores: ResolutorItemDto[]; }

@Injectable({ providedIn: 'root' })
export class TicketPerfilService {
  private readonly base = `${environment.apiUrl}/ticket-perfiles`;
  private readonly http = inject(HttpClient);

  getTiposPermitidos(): Observable<TipoPermitidoDto[]> {
    return this.http.get<TipoPermitidoDto[]>(`${this.base}/tipos-permitidos`);
  }

  getAsignables(tipo: string, paisId?: number): Observable<AsignableDto[]> {
    let params = new HttpParams().set('tipo', tipo);
    if (paisId != null) params = params.set('paisId', paisId);
    return this.http.get<AsignableDto[]>(`${this.base}/asignables`, { params });
  }

  // Perfil de usuario
  getPerfilUsuario(userId: string): Observable<TicketPerfilDto> {
    return this.http.get<TicketPerfilDto>(`${this.base}/usuario/${userId}`);
  }
  upsertPerfilUsuario(userId: string, req: UpsertTicketPerfilRequest): Observable<TicketPerfilDto> {
    return this.http.put<TicketPerfilDto>(`${this.base}/usuario/${userId}`, req);
  }

  // Perfil de rol
  getPerfilRol(roleId: number): Observable<TicketResolutorRolDto> {
    return this.http.get<TicketResolutorRolDto>(`${this.base}/rol/${roleId}`);
  }
  upsertPerfilRol(roleId: number, req: UpsertTicketResolutorRolRequest): Observable<TicketResolutorRolDto> {
    return this.http.put<TicketResolutorRolDto>(`${this.base}/rol/${roleId}`, req);
  }
  seedDesdeRol(userId: string, roleId: number): Observable<void> {
    return this.http.post<void>(`${this.base}/usuario/${userId}/seed-desde-rol/${roleId}`, {});
  }
}
