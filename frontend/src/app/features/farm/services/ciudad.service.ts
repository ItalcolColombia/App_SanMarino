// src/app/features/farm/services/ciudad.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface CiudadDto {
  municipioId: number;
  municipioNombre: string;
  departamentoId: number;
}

@Injectable({ providedIn: 'root' })
export class CiudadService {
  private readonly baseUrl = `${environment.apiUrl}/Municipio`;

  constructor(private http: HttpClient) {}

  getByDepartamentoId(departamentoId: number): Observable<CiudadDto[]> {
    const params = new HttpParams().set('departamentoId', departamentoId);
    return this.http.get<CiudadDto[]>(this.baseUrl, { params });
  }

  getById(id: number): Observable<CiudadDto> {
    return this.http.get<CiudadDto>(`${this.baseUrl}/${id}`);
  }

  getAll(): Observable<CiudadDto[]> {
    return this.http.get<CiudadDto[]>(this.baseUrl);
  }
}
