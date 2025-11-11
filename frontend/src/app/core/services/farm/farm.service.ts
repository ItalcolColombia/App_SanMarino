// src/app/core/services/farm/farm.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { tap, catchError } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import { HttpCompanyHelperService } from '../http-company-helper.service';
import { TokenStorageService } from '../../../core/auth/token-storage.service';
import { AuthSession } from '../../../core/auth/auth.models';

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
  private tokenStorage = inject(TokenStorageService);
  private readonly baseUrl = `${environment.apiUrl}/Farm`;

  /** Obtener todas las granjas filtradas por usuario */
  getAll(): Observable<Farm[]> {
    this.companyHelper.logActiveCompany('FarmService.getAll');

    // Obtener el ID del usuario de la sesión actual
    const session: AuthSession | null = this.tokenStorage.get();
    const userId = session?.user?.id;

    console.log('=== Core FarmService.getAll() Debug ===');
    console.log('Session completa:', session);
    console.log('User ID:', userId);

    const headers = this.companyHelper.getAuthenticatedHeaders();
    let params = new HttpParams();

    // Si hay userId, agregarlo como parámetro
    if (userId && userId.trim() !== '') {
      params = params.set('id_user_session', userId.trim());
      console.log('✅ Enviando id_user_session:', userId);
    } else {
      console.warn('⚠️ No hay userId disponible - devolviendo todas las granjas');
    }

    return this.http.get<Farm[]>(this.baseUrl, { headers, params }).pipe(
      tap(response => {
        console.log('✅ Respuesta del backend:', response);
        console.log('Cantidad de granjas recibidas:', response.length);
        if (response.length === 0 && userId) {
          console.warn('⚠️ No se recibieron granjas. Verificar que el usuario tenga granjas asignadas.');
        }
      }),
      catchError(error => {
        console.error('❌ Error en Core FarmService.getAll():', error);
        console.error('Error details:', error.error);
        console.error('Error status:', error.status);
        return throwError(() => error);
      })
    );
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
