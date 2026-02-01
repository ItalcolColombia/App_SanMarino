// src/app/core/services/http-company-helper.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpHeaders } from '@angular/common/http';
import { TokenStorageService } from '../auth/token-storage.service';

/**
 * Helper centralizado para manejar headers de empresa activa en todas las peticiones HTTP
 */
@Injectable({
  providedIn: 'root'
})
export class HttpCompanyHelperService {
  private storage = inject(TokenStorageService);

  /**
   * Obtiene los headers estándar con empresa activa para todas las peticiones HTTP
   * @param additionalHeaders Headers adicionales opcionales
   * @returns HttpHeaders con empresa activa incluida
   */
  getHeadersWithActiveCompany(additionalHeaders: { [key: string]: string | string[] } = {}): HttpHeaders {
    const session = this.storage.get();
    const activeCompany = session?.activeCompany || '';
    const activeCompanyId = session?.activeCompanyId;
    const activePaisId = session?.activePaisId;
    const activePaisNombre = session?.activePaisNombre;

    // Headers base con empresa activa y país
    const baseHeaders: { [key: string]: string | string[] } = {
      'X-Active-Company': activeCompany,
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      ...additionalHeaders
    };

    // Agregar ID de empresa activa si está disponible
    if (activeCompanyId) {
      baseHeaders['X-Active-Company-Id'] = activeCompanyId.toString();
    }

    // Agregar ID de país activo si está disponible
    if (activePaisId) {
      baseHeaders['X-Active-Pais'] = activePaisId.toString();
    }

    // Agregar nombre del país activo si está disponible
    if (activePaisNombre) {
      baseHeaders['X-Active-Pais-Nombre'] = activePaisNombre;
    }

    return new HttpHeaders(baseHeaders);
  }

  /**
   * Obtiene headers para peticiones autenticadas con empresa activa
   * @param additionalHeaders Headers adicionales opcionales
   * @returns HttpHeaders con token y empresa activa
   */
  getAuthenticatedHeaders(additionalHeaders: { [key: string]: string | string[] } = {}): HttpHeaders {
    const session = this.storage.get();
    const token = session?.accessToken;
    const activeCompany = session?.activeCompany || '';
    const activeCompanyId = session?.activeCompanyId;
    const activePaisId = session?.activePaisId;
    const activePaisNombre = session?.activePaisNombre;

    if (!token) {
      console.warn('HttpCompanyHelper: No hay token disponible, usando headers sin autenticación');
      return this.getHeadersWithActiveCompany(additionalHeaders);
    }

    const authHeaders: { [key: string]: string | string[] } = {
      'Authorization': `Bearer ${token}`,
      'X-Active-Company': activeCompany,
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      ...additionalHeaders
    };

    // Agregar ID de empresa activa si está disponible
    if (activeCompanyId) {
      authHeaders['X-Active-Company-Id'] = activeCompanyId.toString();
    }

    // Agregar ID de país activo si está disponible
    if (activePaisId) {
      authHeaders['X-Active-Pais'] = activePaisId.toString();
    }

    // Agregar nombre del país activo si está disponible
    if (activePaisNombre) {
      authHeaders['X-Active-Pais-Nombre'] = activePaisNombre;
    }

    return new HttpHeaders(authHeaders);
  }

  /**
   * Obtiene solo el header de empresa activa y país
   * @returns Headers X-Active-Company, X-Active-Company-Id, X-Active-Pais y X-Active-Pais-Nombre
   */
  getActiveCompanyHeader(): { [key: string]: string } {
    const session = this.storage.get();
    const activeCompany = session?.activeCompany || '';
    const activeCompanyId = session?.activeCompanyId;
    const activePaisId = session?.activePaisId;
    const activePaisNombre = session?.activePaisNombre;

    const headers: { [key: string]: string } = {
      'X-Active-Company': activeCompany
    };

    // Agregar ID de empresa activa si está disponible
    if (activeCompanyId) {
      headers['X-Active-Company-Id'] = activeCompanyId.toString();
    }

    // Agregar ID de país activo si está disponible
    if (activePaisId) {
      headers['X-Active-Pais'] = activePaisId.toString();
    }

    // Agregar nombre del país activo si está disponible
    if (activePaisNombre) {
      headers['X-Active-Pais-Nombre'] = activePaisNombre;
    }

    return headers;
  }

  /**
   * Obtiene la empresa activa actual
   * @returns Nombre de la empresa activa o string vacío
   */
  getActiveCompany(): string {
    const session = this.storage.get();
    return session?.activeCompany || '';
  }

  /**
   * Verifica si hay una empresa activa configurada
   * @returns true si hay empresa activa, false en caso contrario
   */
  hasActiveCompany(): boolean {
    const activeCompany = this.getActiveCompany();
    return activeCompany.trim().length > 0;
  }

  /**
   * Debug: Log de información de empresa activa
   * @param context Contexto donde se está usando (ej: 'FarmService', 'UserService')
   */
  logActiveCompany(context: string): void {
    const activeCompany = this.getActiveCompany();
    console.log(`[${context}] Empresa activa:`, activeCompany || 'Ninguna');
  }
}
