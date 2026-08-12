import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { BaseHttpService } from '../base-http.service';

/**
 * Un permiso del catálogo global visto desde una empresa. El backend devuelve SIEMPRE el catálogo
 * completo: `isEnabled` dice si esta empresa lo tiene habilitado.
 */
export interface CompanyPermissionItem {
  id: number;
  key: string;
  description?: string | null;
  isEnabled: boolean;
  /** Cuántos roles de la empresa ya lo tienen asignado (para no apagar a ciegas algo en uso). */
  enUsoPorRoles: number;
}

/** Request para fijar los permisos habilitados de una empresa (reemplaza la configuración). */
export interface SetCompanyPermissionsRequest {
  permissionIds: number[];
}

/**
 * Configuración del eje permiso↔empresa (`company_permissions`).
 *
 * A diferencia de `company_menus`, esta configuración MANDA: filtra lo que se puede asignar a un rol
 * y se intersecta con los permisos efectivos del usuario en el login.
 */
@Injectable({ providedIn: 'root' })
export class CompanyPermissionService extends BaseHttpService {
  private readonly baseUrl = `${environment.apiUrl}/Company`;

  /** Catálogo completo con el estado (habilitado / no) para la empresa. */
  getPermissionsForCompany(companyId: number): Observable<CompanyPermissionItem[]> {
    return this.get<CompanyPermissionItem[]>(`${this.baseUrl}/${companyId}/permissions`, {
      context: 'CompanyPermissionService.getPermissionsForCompany'
    });
  }

  /** Fija los permisos habilitados de la empresa. */
  setPermissionsForCompany(
    companyId: number,
    request: SetCompanyPermissionsRequest
  ): Observable<void> {
    return this.put<void>(`${this.baseUrl}/${companyId}/permissions`, request, {
      context: 'CompanyPermissionService.setPermissionsForCompany'
    });
  }
}
