import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { BaseHttpService } from '../base-http.service';

export interface CompanyPais {
  companyId: number;
  companyName: string;
  paisId: number;
  paisNombre: string;
  isDefault: boolean;
}

export interface AssignCompanyPaisRequest {
  companyId: number;
  paisId: number;
}

export interface AssignUserCompanyPaisRequest {
  userId: string; // Guid como string
  companyId: number;
  paisId: number;
  isDefault?: boolean;
}

@Injectable({ providedIn: 'root' })
export class CompanyPaisService extends BaseHttpService {
  private readonly baseUrl = `${environment.apiUrl}/CompanyPais`;

  /**
   * Obtiene todas las relaciones empresa-país
   */
  getAll(): Observable<CompanyPais[]> {
    return this.get<CompanyPais[]>(this.baseUrl, { context: 'CompanyPaisService.getAll' });
  }

  /**
   * Obtiene todas las empresas asignadas a un país
   */
  getCompaniesByPais(paisId: number): Observable<any[]> {
    return this.get<any[]>(`${this.baseUrl}/pais/${paisId}/companies`, {
      context: 'CompanyPaisService.getCompaniesByPais'
    });
  }

  /**
   * Obtiene todos los países asignados a una empresa
   */
  getPaisesByCompany(companyId: number): Observable<any[]> {
    return this.get<any[]>(`${this.baseUrl}/company/${companyId}/paises`, {
      context: 'CompanyPaisService.getPaisesByCompany'
    });
  }

  /**
   * Obtiene las combinaciones empresa-país de un usuario
   */
  getUserCompanyPais(userId: string): Observable<CompanyPais[]> {
    return this.get<CompanyPais[]>(`${this.baseUrl}/user/${userId}`, {
      context: 'CompanyPaisService.getUserCompanyPais'
    });
  }

  /**
   * Obtiene las combinaciones empresa-país del usuario actual
   */
  getCurrentUserCompanyPais(): Observable<CompanyPais[]> {
    return this.get<CompanyPais[]>(`${this.baseUrl}/user/current`, {
      context: 'CompanyPaisService.getCurrentUserCompanyPais'
    });
  }

  /**
   * Asigna una empresa a un país
   */
  assignCompanyToPais(request: AssignCompanyPaisRequest): Observable<CompanyPais> {
    return this.post<CompanyPais>(`${this.baseUrl}/assign`, request, {
      context: 'CompanyPaisService.assignCompanyToPais'
    });
  }

  /**
   * Remueve la asignación de una empresa a un país
   */
  removeCompanyFromPais(request: AssignCompanyPaisRequest): Observable<void> {
    // DELETE con body usando POST a un endpoint específico o usando options
    return this.post<void>(`${this.baseUrl}/remove`, request, {
      context: 'CompanyPaisService.removeCompanyFromPais'
    });
  }

  /**
   * Asigna un usuario a una empresa en un país específico
   */
  assignUserToCompanyPais(request: AssignUserCompanyPaisRequest): Observable<CompanyPais> {
    return this.post<CompanyPais>(`${this.baseUrl}/user/assign`, request, {
      context: 'CompanyPaisService.assignUserToCompanyPais'
    });
  }

  /**
   * Remueve la asignación de un usuario a una empresa-país
   */
  removeUserFromCompanyPais(request: AssignUserCompanyPaisRequest): Observable<void> {
    // DELETE con body usando POST a un endpoint específico
    return this.post<void>(`${this.baseUrl}/user/remove`, request, {
      context: 'CompanyPaisService.removeUserFromCompanyPais'
    });
  }

  /**
   * Valida que una empresa pertenece a un país
   */
  validateCompanyPais(request: AssignCompanyPaisRequest): Observable<{ isValid: boolean; companyId: number; paisId: number }> {
    return this.post<{ isValid: boolean; companyId: number; paisId: number }>(
      `${this.baseUrl}/validate`,
      request,
      { context: 'CompanyPaisService.validateCompanyPais' }
    );
  }
}

