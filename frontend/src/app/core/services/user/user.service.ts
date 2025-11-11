// src/app/core/services/user/user.service.ts
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { BaseHttpService } from '../base-http.service';

export interface User {
  id?: string;
  firstName: string;
  lastName: string;
  surName?: string;
  email: string;
  phone?: string;
  telefono?: string;
  cedula?: string;
  ubicacion?: string;
  isActive: boolean;
  companyIds?: number[];
  companyNames?: string[];
  roles?: string[];
  roleIds?: number[];
  password?: string;
  emailSent?: boolean | null;
  emailQueueId?: number | null;
}

export interface UserListItem {
  id: string;
  firstName: string;
  lastName: string;
  surName?: string;
  email: string;
  phone?: string;
  telefono?: string;
  cedula?: string;
  ubicacion?: string;
  isActive: boolean;
  companyIds?: number[];
  companyNames?: string[];
  roles?: string[];
  roleIds?: number[];
  emailSent?: boolean | null;
  emailQueueId?: number | null;
}

export interface UserDto {
  id: string;
  firstName: string;
  lastName: string;
  surName?: string;
  email: string;
  phone?: string;
  telefono?: string;
  cedula?: string;
  ubicacion?: string;
  isActive: boolean;
  companyIds?: number[];
  companyNames?: string[];
  roles?: string[];
  roleIds?: number[];
}

export interface CreateUserDto {
  firstName: string;
  lastName: string;
  surName?: string;
  email: string;
  phone?: string;
  telefono?: string;
  cedula?: string;
  ubicacion?: string;
  password?: string;
  companyIds?: number[];
  roles?: string[];
  roleIds?: number[];
  isActive?: boolean;
}

export interface UpdateUserDto {
  id: string;
  firstName?: string;
  lastName?: string;
  surName?: string;
  email?: string;
  phone?: string;
  telefono?: string;
  cedula?: string;
  ubicacion?: string;
  isActive?: boolean;
  companyIds?: number[];
  roles?: string[];
  roleIds?: number[];
}

@Injectable({ providedIn: 'root' })
export class UserService extends BaseHttpService {
  private readonly baseUrl = `${environment.apiUrl}/Users`;

  /** Obtener todos los usuarios */
  getAll(): Observable<User[]> {
    return this.get<User[]>(this.baseUrl, { context: 'UserService.getAll' });
  }

  /** Obtener usuario por ID */
  getById(id: string): Observable<User> {
    return this.get<User>(`${this.baseUrl}/${id}`, { context: 'UserService.getById' });
  }

  /** Crear nuevo usuario */
  create(user: CreateUserDto): Observable<User> {
    return this.post<User>(this.baseUrl, user, { context: 'UserService.create' });
  }

  /** Actualizar usuario */
  update(id: string, user: UpdateUserDto): Observable<User> {
    return this.put<User>(`${this.baseUrl}/${id}`, user, { context: 'UserService.update' });
  }

  /** Eliminar usuario */
  delete(id: string): Observable<void> {
    return this.deleteRequest<void>(`${this.baseUrl}/${id}`, { context: 'UserService.delete' });
  }

  /** Buscar usuarios por empresa */
  getByCompany(companyId: number): Observable<User[]> {
    const params = this.createParams({ companyId });
    return this.get<User[]>(`${this.baseUrl}/by-company`, {
      params,
      context: 'UserService.getByCompany'
    });
  }

  /** Activar/Desactivar usuario */
  toggleActive(id: string): Observable<User> {
    return this.patch<User>(`${this.baseUrl}/${id}/toggle-active`, {}, {
      context: 'UserService.toggleActive'
    });
  }
}
