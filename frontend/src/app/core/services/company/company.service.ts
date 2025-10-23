// src/app/core/services/company/company.service.ts
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { BaseHttpService } from '../base-http.service';

export interface Company {
  id?: number;
  name: string;
  identifier: string;
  documentType: string;
  address?: string;
  phone?: string;
  email?: string;
  // antiguos (fallback visual)
  country?: string;
  state?: string;
  city?: string;
  // nuevos preferidos
  countryId?: number;
  departamentoId?: number;
  municipioId?: number;

  roleIds?: number[];
  visualPermissions?: string[];
  mobileAccess?: boolean;
}

@Injectable({ providedIn: 'root' })
export class CompanyService extends BaseHttpService {
  private readonly baseUrl = `${environment.apiUrl}/Company`;

  /** Trae todas las empresas */
  getAll(): Observable<Company[]> {
    return this.get<Company[]>(this.baseUrl, { context: 'CompanyService.getAll' });
  }

  /** Trae TODAS las empresas sin filtro para administración */
  getAllForAdmin(): Observable<Company[]> {
    return this.get<Company[]>(`${this.baseUrl}/admin`, { context: 'CompanyService.getAllForAdmin' });
  }

  /** Crea una nueva */
  create(c: Company): Observable<Company> {
    return this.post<Company>(this.baseUrl, c, { context: 'CompanyService.create' });
  }

  /** Actualiza existentes */
  update(c: Company): Observable<Company> {
    return this.put<Company>(`${this.baseUrl}/${c.id}`, c, { context: 'CompanyService.update' });
  }

  /** Elimina por id */
  delete(id: number): Observable<void> {
    return this.deleteRequest<void>(`${this.baseUrl}/${id}`, { context: 'CompanyService.delete' });
  }
}
