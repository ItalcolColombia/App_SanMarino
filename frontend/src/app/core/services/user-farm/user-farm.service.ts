// src/app/core/services/user-farm/user-farm.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface UserFarmDto {
  userId: string;
  farmId: number;
  userName: string;
  farmName: string;
  isAdmin: boolean;
  isDefault: boolean;
  createdAt: string;
  createdByUserId: string;
  /** true = acceso restringido a los grants de user_farm_scopes (núcleo/galpón/lote). */
  restrictLocations: boolean;
  /** Cantidad de grants configurados (0 con restrictLocations=true ⇒ no ve nada, fail-closed). */
  scopeCount: number;
}

export interface UserFarmLiteDto {
  farmId: number;
  farmName: string;
  isAdmin: boolean;
  isDefault: boolean;
  restrictLocations?: boolean;
}

// ── Alcance granular (núcleo/galpón/lote o global) por usuario-granja ──

export type UserFarmScopeLevel = 'nucleo' | 'galpon' | 'lote';

export interface UserFarmScopeItemDto {
  level: UserFarmScopeLevel;
  nucleoId: string | null;
  galponId: string | null;
  loteId: number | null;
  /** Nombre display (solo en GET; se ignora en PUT). */
  nombre?: string | null;
}

export interface UserFarmScopeConfigDto {
  userId: string;
  farmId: number;
  farmName: string;
  restrictLocations: boolean;
  items: UserFarmScopeItemDto[];
}

export interface UpdateUserFarmScopeDto {
  restrictLocations: boolean;
  items: UserFarmScopeItemDto[];
}

export interface FarmLocationLoteNodeDto {
  loteId: number;
  loteNombre: string;
  fase: string | null;
}

export interface FarmLocationGalponNodeDto {
  galponId: string;
  galponNombre: string;
  lotes: FarmLocationLoteNodeDto[];
}

export interface FarmLocationNucleoNodeDto {
  nucleoId: string;
  nucleoNombre: string;
  galpones: FarmLocationGalponNodeDto[];
  lotesSinGalpon: FarmLocationLoteNodeDto[];
}

export interface FarmLocationsTreeDto {
  farmId: number;
  farmName: string;
  nucleos: FarmLocationNucleoNodeDto[];
  lotesSinNucleo: FarmLocationLoteNodeDto[];
}

export interface UserFarmsResponseDto {
  userId: string;
  userName: string;
  farms: UserFarmDto[];
}

export interface FarmUsersResponseDto {
  farmId: number;
  farmName: string;
  users: UserFarmDto[];
}

export interface CreateUserFarmDto {
  UserId: string;
  FarmId: number;
  IsAdmin?: boolean;
  IsDefault?: boolean;
}

export interface UpdateUserFarmDto {
  isAdmin?: boolean;
  isDefault?: boolean;
}

export interface AssociateUserFarmsDto {
  userId: string;
  farmIds: number[];
  isAdmin?: boolean;
  isDefault?: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class UserFarmService {
  private readonly apiUrl = `${environment.apiUrl}/userfarm`;

  constructor(private http: HttpClient) {}

  // Obtener granjas de un usuario
  getUserFarms(userId: string): Observable<UserFarmsResponseDto> {
    return this.http.get<UserFarmsResponseDto>(`${this.apiUrl}/user/${userId}/farms`);
  }

  // Obtener usuarios de una granja
  getFarmUsers(farmId: number): Observable<FarmUsersResponseDto> {
    return this.http.get<FarmUsersResponseDto>(`${this.apiUrl}/farm/${farmId}/users`);
  }

  // Crear asociación usuario-granja (una sola granja)
  createUserFarm(dto: CreateUserFarmDto): Observable<UserFarmDto> {
     // Debug log
     // Debug log
    
    return this.http.post<UserFarmDto>(`${this.apiUrl}`, dto);
  }

  // Actualizar asociación usuario-granja
  updateUserFarm(userId: string, farmId: number, dto: UpdateUserFarmDto): Observable<UserFarmDto> {
    return this.http.put<UserFarmDto>(`${this.apiUrl}/user/${userId}/farm/${farmId}`, dto);
  }

  // Eliminar asociación usuario-granja
  deleteUserFarm(userId: string, farmId: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/user/${userId}/farm/${farmId}`);
  }

  // Asociar múltiples granjas a un usuario
  associateUserFarms(dto: AssociateUserFarmsDto): Observable<UserFarmDto[]> {
    return this.http.post<UserFarmDto[]>(`${this.apiUrl}/user/${dto.userId}/associate-farms`, dto);
  }

  // Reemplazar todas las granjas de un usuario
  replaceUserFarms(userId: string, farmIds: number[]): Observable<UserFarmDto[]> {
    return this.http.put<UserFarmDto[]>(`${this.apiUrl}/user/${userId}/replace-farms`, farmIds);
  }

  // Verificar acceso de usuario a granja
  hasUserAccessToFarm(userId: string, farmId: number): Observable<{ hasAccess: boolean }> {
    return this.http.get<{ hasAccess: boolean }>(`${this.apiUrl}/user/${userId}/farm/${farmId}/access`);
  }

  // Obtener granjas accesibles para un usuario
  getUserAccessibleFarms(userId: string): Observable<UserFarmLiteDto[]> {
    return this.http.get<UserFarmLiteDto[]>(`${this.apiUrl}/user/${userId}/accessible-farms`);
  }

  // ── Alcance granular (núcleo/galpón/lote o global) ──

  /** Configuración de alcance del usuario en una granja (flag + grants con nombres). */
  getUserFarmScope(userId: string, farmId: number): Observable<UserFarmScopeConfigDto> {
    return this.http.get<UserFarmScopeConfigDto>(`${this.apiUrl}/user/${userId}/farm/${farmId}/scope`);
  }

  /** Reemplazo transaccional del alcance (restrictLocations=false vuelve a acceso global). */
  updateUserFarmScope(userId: string, farmId: number, dto: UpdateUserFarmScopeDto): Observable<UserFarmScopeConfigDto> {
    return this.http.put<UserFarmScopeConfigDto>(`${this.apiUrl}/user/${userId}/farm/${farmId}/scope`, dto);
  }

  /** Árbol núcleos→galpones→lotes activos de la granja (para el modal de alcance). */
  getFarmLocationsTree(farmId: number): Observable<FarmLocationsTreeDto> {
    return this.http.get<FarmLocationsTreeDto>(`${this.apiUrl}/farm/${farmId}/locations-tree`);
  }
}
