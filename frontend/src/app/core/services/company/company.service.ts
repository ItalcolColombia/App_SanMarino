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
  logoDataUrl?: string | null;
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
  // Default GLOBAL de la empresa: ¿el alimento se maneja a nivel GALPÓN? (cada granja puede overridear)
  manejaAlimentoPorGalpon?: boolean;
  /** Flag por empresa: maneja códigos ERP avícolas (bodega / C.O. / instalación / centro de costo). */
  manejaCodigosErpAvicola?: boolean;
  /** Flag por empresa: clasifica los huevos por ÍTEM del catálogo (Primera/Pnc) en vez de las 11 columnas fijas. */
  clasificacionHuevoPorItems?: boolean;
  /** Flag por empresa: permite trasladar aves entre etapas (Levante → Producción) registrando cohorte con la edad de origen. */
  permiteTrasladoAvesCrossEtapa?: boolean;
  /** Días ANTES del encasetamiento cuyo alimento cuenta como «ingreso inicial del ciclo» en el reporte diario de engorde. Rango 0-30, default 10. */
  diasAlimentoPrevioEncaset?: number;
}

@Injectable({ providedIn: 'root' })
export class CompanyService extends BaseHttpService {
  private readonly baseUrl = `${environment.apiUrl}/Company`;

  /** Trae todas las empresas */
  getAll(): Observable<Company[]> {
    return this.get<Company[]>(this.baseUrl, { context: 'CompanyService.getAll' });
  }

  /** Trae una empresa por ID (incluye logo para edición) */
  getById(id: number): Observable<Company> {
    return this.get<Company>(`${this.baseUrl}/${id}`, { context: 'CompanyService.getById' });
  }

  /** Trae TODAS las empresas sin filtro para administración */
  // Ruta "global" (no "admin"): AWS WAF AdminProtection bloquea cualquier path con /admin.
  getAllForAdmin(): Observable<Company[]> {
    return this.get<Company[]>(`${this.baseUrl}/global`, { context: 'CompanyService.getAllForAdmin' });
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
