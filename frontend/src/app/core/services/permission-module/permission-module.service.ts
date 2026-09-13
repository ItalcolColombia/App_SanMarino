import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { BaseHttpService } from '../base-http.service';

/** Un módulo del catálogo con las keys de permiso que agrupa (M:N). */
export interface PermissionModule {
  id: number;
  key: string;
  nombre: string;
  descripcion?: string | null;
  orden: number;
  permissionKeys: string[];
  /** Empresas que lo tienen prendido (un módulo en uso no se puede borrar). */
  empresasConModulo: number;
}

export interface CreatePermissionModuleDto {
  key: string;
  nombre: string;
  descripcion?: string | null;
  orden: number;
}

export interface UpdatePermissionModuleDto {
  nombre: string;
  descripcion?: string | null;
  orden: number;
}

/** Un módulo visto desde una empresa. */
export interface CompanyPermissionModuleItem {
  moduleId: number;
  key: string;
  nombre: string;
  descripcion?: string | null;
  orden: number;
  isEnabled: boolean;
  totalPermisos: number;
  /** De los permisos del módulo, cuántos tiene prendidos la empresa (ajuste fino). */
  permisosHabilitados: number;
}

/** Efecto de un cambio sobre `company_permissions`. */
export interface CambioPermisos {
  empresasAfectadas: number;
  permisosPrendidos: number;
  permisosApagados: number;
}

/**
 * Módulos de permisos (`permission_modules`): catálogo y asignación por empresa. Todo cambio se
 * materializa en `company_permissions`, que es lo que manda en el login y en el modal de roles.
 */
@Injectable({ providedIn: 'root' })
export class PermissionModuleService extends BaseHttpService {
  private readonly baseUrl = `${environment.apiUrl}/PermissionModule`;

  getAll(): Observable<PermissionModule[]> {
    return this.get<PermissionModule[]>(this.baseUrl, { context: 'PermissionModuleService.getAll' });
  }

  create(dto: CreatePermissionModuleDto): Observable<PermissionModule> {
    return this.post<PermissionModule>(this.baseUrl, dto, { context: 'PermissionModuleService.create' });
  }

  update(id: number, dto: UpdatePermissionModuleDto): Observable<PermissionModule> {
    return this.put<PermissionModule>(`${this.baseUrl}/${id}`, dto, { context: 'PermissionModuleService.update' });
  }

  remove(id: number): Observable<void> {
    return this.deleteRequest<void>(`${this.baseUrl}/${id}`, { context: 'PermissionModuleService.remove' });
  }

  /** Reemplaza los permisos del módulo; el backend recalcula las empresas afectadas. */
  setPermissions(id: number, permissionIds: number[]): Observable<CambioPermisos> {
    return this.put<CambioPermisos>(`${this.baseUrl}/${id}/permissions`, { permissionIds }, {
      context: 'PermissionModuleService.setPermissions'
    });
  }

  getForCompany(companyId: number): Observable<CompanyPermissionModuleItem[]> {
    return this.get<CompanyPermissionModuleItem[]>(`${this.baseUrl}/company/${companyId}`, {
      context: 'PermissionModuleService.getForCompany'
    });
  }

  /** Fija los módulos prendidos de la empresa (los no enviados se apagan). */
  setForCompany(companyId: number, moduleIds: number[]): Observable<CambioPermisos> {
    return this.put<CambioPermisos>(`${this.baseUrl}/company/${companyId}`, { moduleIds }, {
      context: 'PermissionModuleService.setForCompany'
    });
  }
}
