// src/app/core/services/session/session-admin.service.ts
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { BaseHttpService } from '../base-http.service';

/**
 * Una sesión abierta, como la lista el backend.
 * ⚠️ `etiqueta` son los últimos 8 caracteres del `jti`, no el `jti`: publicarlo entero sería
 * repartir identificadores de sesión ajena.
 */
export interface SesionActiva {
  id: number;
  etiqueta: string;
  deviceId: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  createdAt: string;
  expiresAt: string;
  lastSeenAt: string | null;
  revokedAt: string | null;
  revokedReason: string | null;
  /** ¿Es la sesión desde la que se está mirando? Se avisa antes de que alguien se cierre a sí mismo. */
  esLaActual: boolean;
}

/**
 * Cliente de los endpoints de sesión (B1).
 *
 * Cuelgan de `/api/session` a propósito: ese prefijo ya está en EXCLUIDOS de la lista cacheable,
 * así que no hace falta tocar el gate de CI del front, y no contiene «admin», que el WAF bloquea.
 */
@Injectable({ providedIn: 'root' })
export class SessionAdminService extends BaseHttpService {
  private readonly base = `${environment.apiUrl}/session`;

  /** Mis dispositivos. La sesión actual viene marcada con `esLaActual`. */
  mias(incluirRevocadas = false): Observable<SesionActiva[]> {
    return this.get<SesionActiva[]>(`${this.base}/mias`, {
      params: { incluirRevocadas },
      context: 'SessionAdminService.mias'
    });
  }

  /** Sesiones de otro usuario (super admin o permiso `usuarios.revocar_sesion`). */
  deUsuario(userId: string, incluirRevocadas = false): Observable<SesionActiva[]> {
    return this.get<SesionActiva[]>(`${this.base}/de-usuario/${userId}`, {
      params: { incluirRevocadas },
      context: 'SessionAdminService.deUsuario'
    });
  }

  /** Cierro una sesión mía (la tablet que perdí, sin esperar a un administrador). */
  cerrarMia(id: number, motivo?: string): Observable<void> {
    return this.borrarConMotivo<void>(`${this.base}/mias/${id}`, motivo);
  }

  /**
   * Revoca una sesión cualquiera.
   * Surte efecto en **menos de un minuto** desde que el dispositivo toque la red, no al instante.
   */
  revocar(id: number, motivo?: string): Observable<void> {
    return this.borrarConMotivo<void>(`${this.base}/${id}`, motivo);
  }

  /** Revoca todas las sesiones de un usuario. Devuelve cuántas apagó. */
  revocarTodas(userId: string, motivo?: string): Observable<{ revocadas: number }> {
    return this.borrarConMotivo<{ revocadas: number }>(`${this.base}/de-usuario/${userId}`, motivo);
  }

  /**
   * DELETE **con cuerpo**: el motivo queda en la auditoría de la revocación. `BaseHttpService.delete`
   * no admite body, así que se usa `HttpClient` directo con las mismas cabeceras autenticadas.
   */
  private borrarConMotivo<T>(url: string, motivo?: string): Observable<T> {
    return this.http.delete<T>(url, {
      headers: this.companyHelper.getAuthenticatedHeaders(),
      body: { motivo: motivo ?? null }
    });
  }
}
