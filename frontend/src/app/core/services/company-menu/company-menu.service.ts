import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { BaseHttpService } from '../base-http.service';

/** Ítem de menú asignado a una empresa (árbol con estado habilitado). */
export interface CompanyMenuItem {
  id: number;
  label: string;
  icon?: string | null;
  route?: string | null;
  order: number;
  isEnabled: boolean;
  children: CompanyMenuItem[];
}

/** Request para asignar menús a una empresa. */
export interface SetCompanyMenusRequest {
  menuIds: number[];
  isEnabled?: boolean;
}

/** Ítem en la estructura de menú de la empresa (orden y padre). */
export interface CompanyMenuItemStructureDto {
  menuId: number;
  sortOrder: number;
  parentMenuId: number | null;
  isEnabled?: boolean;
}

/** Request para actualizar orden y jerarquía de menús de una empresa. */
export interface UpdateCompanyMenuStructureRequest {
  items: CompanyMenuItemStructureDto[];
}

@Injectable({ providedIn: 'root' })
export class CompanyMenuService extends BaseHttpService {
  private readonly baseUrl = `${environment.apiUrl}/Company`;

  /** Obtiene el árbol de menús asignados a la empresa. */
  getMenusForCompany(companyId: number): Observable<CompanyMenuItem[]> {
    return this.get<CompanyMenuItem[]>(`${this.baseUrl}/${companyId}/menus`, {
      context: 'CompanyMenuService.getMenusForCompany'
    });
  }

  /** Asigna o actualiza los menús de la empresa. */
  setCompanyMenus(companyId: number, request: SetCompanyMenusRequest): Observable<void> {
    const body = {
      menuIds: request.menuIds,
      isEnabled: request.isEnabled ?? true
    };
    return this.put<void>(`${this.baseUrl}/${companyId}/menus`, body, {
      context: 'CompanyMenuService.setCompanyMenus'
    });
  }

  /** Actualiza orden y jerarquía (padre/hijos) de los menús de la empresa. */
  updateCompanyMenuStructure(
    companyId: number,
    request: UpdateCompanyMenuStructureRequest
  ): Observable<void> {
    return this.put<void>(`${this.baseUrl}/${companyId}/menus/structure`, request, {
      context: 'CompanyMenuService.updateCompanyMenuStructure'
    });
  }
}
