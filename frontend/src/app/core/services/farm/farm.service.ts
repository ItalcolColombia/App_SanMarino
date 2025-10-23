// src/app/core/services/farm/farm.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { HttpCompanyHelperService } from '../http-company-helper.service';

export interface Farm {
  id: number;
  name: string;
  companyId: number;
  address?: string;
  regionalId?: number;
  status?: string;
  departamentoId?: number;
  municipioId?: number;
}

export interface FarmDto {
  id: number;
  name: string;
  companyId: number;
  address?: string;
  regionalId?: number;
  status?: string;
  departamentoId?: number;
  municipioId?: number;
}

@Injectable({ providedIn: 'root' })
export class FarmService {
  private http = inject(HttpClient);
  private companyHelper = inject(HttpCompanyHelperService);
  private readonly baseUrl = `${environment.apiUrl}/Farm`;

  /** Obtener todas las granjas */
  getAll(): Observable<Farm[]> {
    this.companyHelper.logActiveCompany('FarmService.getAll');
    
    const headers = this.companyHelper.getAuthenticatedHeaders();
    
    return this.http.get<Farm[]>(this.baseUrl, { headers });
  }

  /** Obtener todas las granjas (alias para compatibilidad) */
  getAllFarms(): Observable<Farm[]> {
    return this.getAll();
  }

  /** Obtener granja por ID */
  getById(id: number): Observable<Farm> {
    this.companyHelper.logActiveCompany('FarmService.getById');
    
    const headers = this.companyHelper.getAuthenticatedHeaders();
    
    return this.http.get<Farm>(`${this.baseUrl}/${id}`, { headers });
  }

  /** Crear nueva granja */
  create(farm: Omit<Farm, 'id'>): Observable<Farm> {
    this.companyHelper.logActiveCompany('FarmService.create');
    
    const headers = this.companyHelper.getAuthenticatedHeaders();
    
    return this.http.post<Farm>(this.baseUrl, farm, { headers });
  }

  /** Actualizar granja */
  update(id: number, farm: Partial<Farm>): Observable<Farm> {
    this.companyHelper.logActiveCompany('FarmService.update');
    
    const headers = this.companyHelper.getAuthenticatedHeaders();
    
    return this.http.put<Farm>(`${this.baseUrl}/${id}`, farm, { headers });
  }

  /** Eliminar granja */
  delete(id: number): Observable<void> {
    this.companyHelper.logActiveCompany('FarmService.delete');
    
    const headers = this.companyHelper.getAuthenticatedHeaders();
    
    return this.http.delete<void>(`${this.baseUrl}/${id}`, { headers });
  }

  /** Búsqueda de granjas con parámetros */
  search(params: { name?: string; companyId?: number; status?: string }): Observable<Farm[]> {
    this.companyHelper.logActiveCompany('FarmService.search');
    
    const headers = this.companyHelper.getAuthenticatedHeaders();
    const searchParams = new URLSearchParams();
    
    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null) {
        searchParams.append(key, value.toString());
      }
    });
    
    const url = `${this.baseUrl}/search?${searchParams.toString()}`;
    
    return this.http.get<Farm[]>(url, { headers });
  }
}