// src/app/core/services/role/role.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface Role {
  id: number;
  name: string;
  permissions: string[];
  companyIds: number[];
  menuIds?: number[];
  isCompanyAdmin?: boolean; // Administrador de Empresa/País (solo Super Admin lo edita)
}

export interface CreateRoleDto {
  name: string;
  permissions: string[];
  companyIds: number[];
  menuIds?: number[]; // ⬅️ IMPORTANTE (replace total)
  isCompanyAdmin?: boolean; // solo Super Admin puede activarlo
}

export interface UpdateRoleDto {
  id: number;
  name: string;
  permissions: string[];
  companyIds: number[];
  menuIds?: number[]; // ⬅️ IMPORTANTE (replace total)
  isCompanyAdmin?: boolean; // null/undefined = no tocar; solo Super Admin lo cambia
}

@Injectable({ providedIn: 'root' })
export class RoleService {
  private readonly baseUrl = `${environment.apiUrl}/Roles`; // ⬅️ del controller

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 50): Observable<Role[]> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<Role[]>(this.baseUrl, { params });
  }

  create(dto: CreateRoleDto): Observable<Role> {
    return this.http.post<Role>(this.baseUrl, dto);
  }

  update(dto: UpdateRoleDto): Observable<Role> {
    return this.http.put<Role>(`${this.baseUrl}/${dto.id}`, dto);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ====== PERMISSIONS (tal cual en tu controller) ======
  assignPermissions(roleId: number, keys: string[]) {
    return this.http.post<Role>(`${this.baseUrl}/${roleId}/permissions/assign`, { keys });
  }
  unassignPermissions(roleId: number, keys: string[]) {
    return this.http.post<Role>(`${this.baseUrl}/${roleId}/permissions/unassign`, { keys });
  }
  replacePermissions(roleId: number, keys: string[]) {
    return this.http.put<Role>(`${this.baseUrl}/${roleId}/permissions`, { keys });
  }

  // ❌ Quita/NO uses assign/unassign/replace de MENÚS por endpoint,
  // porque tu controller no los expone. Usa update(dto) con menuIds.
}
