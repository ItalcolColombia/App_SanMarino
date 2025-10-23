// src/app/core/services/permission/permission.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface Permission {
  id: number;
  key: string;
  description?: string;
}
export interface CreatePermissionDto { key: string; description?: string; }
export interface UpdatePermissionDto { id: number; key: string; description?: string; }

@Injectable({ providedIn: 'root' })
export class PermissionService {
  private readonly baseUrl = `${environment.apiUrl}/Permission`;
  constructor(private http: HttpClient) {}

  getAll(): Observable<Permission[]> { return this.http.get<Permission[]>(this.baseUrl); }
  create(dto: CreatePermissionDto): Observable<Permission> { return this.http.post<Permission>(this.baseUrl, dto); }
  update(dto: UpdatePermissionDto): Observable<Permission> { return this.http.put<Permission>(`${this.baseUrl}/${dto.id}`, dto); }
  delete(id: number) { return this.http.delete<void>(`${this.baseUrl}/${id}`); }
}
