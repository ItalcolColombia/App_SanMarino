// src/app/features/tickets/services/ticket-perfil.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

/** EMPRESA = atiende solo esa empresa · GLOBAL = todas las empresas (solo el admin global). */
export type AlcanceResolutor = 'EMPRESA' | 'GLOBAL';

export interface ResolutorItemRequest { tipo: string; paisId?: number | null; alcance?: AlcanceResolutor | null; }
/** `companyId`: empresa donde se guarda; null = la del usuario/rol (la resuelve el backend). */
export interface UpsertTicketPerfilRequest { nivel: string; resolutores: ResolutorItemRequest[]; companyId?: number | null; }
export interface UpsertTicketResolutorRolRequest {
  resolutores: ResolutorItemRequest[];
  /** Qué puede ABRIR quien tenga el rol. null = no tocar; '' = el rol deja de definirlo. */
  nivelCreacion?: string | null;
  companyId?: number | null;
}

export interface ResolutorItemDto {
  id: number; tipo: string; paisId: number | null; activo: boolean;
  alcance?: AlcanceResolutor; companyId?: number;
}
export interface TicketPerfilDto {
  userId: string; nivel: string; resolutores: ResolutorItemDto[]; hasProfile?: boolean;
  companyId?: number; companyName?: string | null;
  /** Lo que le dan sus roles (NORMAL | IMPLEMENTADOR), o null si ninguno lo define. */
  nivelPorRol?: string | null;
  puedeElegirGlobal?: boolean;
}
export interface AsignableDto { userId: string; nombreCompleto: string; paisLabel: string | null; }
export interface TipoPermitidoDto { tipo: string; label: string; asignables: AsignableDto[]; }
export interface TicketResolutorRolDto {
  roleId: number; resolutores: ResolutorItemDto[];
  nivelCreacion?: string | null;
  companyId?: number; companyName?: string | null;
  puedeElegirGlobal?: boolean;
}

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

  // Perfil de usuario (nivel de apertura + resolutores)
  getPerfilUsuario(userId: string, companyId?: number | null): Observable<TicketPerfilDto> {
    const params = companyId != null ? new HttpParams().set('companyId', companyId) : undefined;
    return this.http.get<TicketPerfilDto>(`${this.base}/usuario/${userId}`, { params });
  }
  upsertPerfilUsuario(userId: string, req: UpsertTicketPerfilRequest): Observable<TicketPerfilDto> {
    return this.http.put<TicketPerfilDto>(`${this.base}/usuario/${userId}`, req);
  }

  // Configuración de rol (qué puede ABRIR + qué ATIENDE)
  getPerfilRol(roleId: number, companyId?: number | null): Observable<TicketResolutorRolDto> {
    const params = companyId != null ? new HttpParams().set('companyId', companyId) : undefined;
    return this.http.get<TicketResolutorRolDto>(`${this.base}/rol/${roleId}`, { params });
  }
  upsertPerfilRol(roleId: number, req: UpsertTicketResolutorRolRequest): Observable<TicketResolutorRolDto> {
    return this.http.put<TicketResolutorRolDto>(`${this.base}/rol/${roleId}`, req);
  }
}
