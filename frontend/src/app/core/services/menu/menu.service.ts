// src/app/core/services/menu/menu.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface MenuItem {
  id: number;
  key: string;
  label: string;
  icon?: string;
  route?: string;
  parentId?: number | null;
  sortOrder?: number | null;
  isGroup?: boolean;
  children?: MenuItem[]; // ⬅️ árbol
}

export interface CreateMenuDto extends Omit<MenuItem, 'id' | 'children'> {}
export interface UpdateMenuDto extends Omit<MenuItem, 'children'> { id: number; }

@Injectable({ providedIn: 'root' })
export class MenuService {
  private readonly base = `${environment.apiUrl}/Roles/menus`; // ⬅️ del controller

  constructor(private http: HttpClient) {}

  getTree(): Observable<MenuItem[]> {
    return this.http.get<MenuItem[]>(`${this.base}/tree`);
  }

  create(dto: CreateMenuDto): Observable<MenuItem> {
    return this.http.post<MenuItem>(this.base, dto);
  }

  update(dto: UpdateMenuDto): Observable<MenuItem> {
    return this.http.put<MenuItem>(`${this.base}/${dto.id}`, dto);
  }

  delete(id: number) {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
