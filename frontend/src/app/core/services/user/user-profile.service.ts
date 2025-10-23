import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface UserDto {
  id: string;
  surName: string;
  firstName: string;
  cedula: string;
  telefono: string;
  ubicacion: string;
  roles: string[];
  companyIds: number[];
  farms: any[];
  isActive: boolean;
  isLocked: boolean;
  createdAt: string;
  lastLoginAt?: string;
}

export interface UpdateUserDto {
  surName?: string;
  firstName?: string;
  cedula?: string;
  telefono?: string;
  ubicacion?: string;
  isActive?: boolean;
  isLocked?: boolean;
  companyIds?: number[];
  roleIds?: number[];
  farmIds?: number[];
}

export interface ChangePasswordDto {
  currentPassword: string;
  newPassword: string;
}

@Injectable({ providedIn: 'root' })
export class UserProfileService {
  private http = inject(HttpClient);
  private baseUsersUrl = `${environment.apiUrl}/Users`;
  private baseAuthUrl  = `${environment.apiUrl}/Auth`;

  getById(userId: string): Observable<UserDto> {
    return this.http.get<UserDto>(`${this.baseUsersUrl}/${userId}`);
  }

  update(userId: string, dto: UpdateUserDto): Observable<UserDto> {
    return this.http.patch<UserDto>(`${this.baseUsersUrl}/${userId}`, dto);
  }

  changeMyPassword(userId: string, dto: ChangePasswordDto): Observable<void> {
    return this.http.patch<void>(`${this.baseUsersUrl}/${userId}/password`, dto);
  }
}


