import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  ClienteDto,
  CreateClienteRequest,
  UpdateClienteRequest,
  ClienteSearchRequest,
  PagedResult
} from '../models/cliente.models';

@Injectable({ providedIn: 'root' })
export class ClienteService {
  private readonly baseUrl = `${environment.apiUrl}/clientes`;
  private readonly http = inject(HttpClient);

  getAll(): Observable<ClienteDto[]> {
    return this.http.get<ClienteDto[]>(this.baseUrl);
  }

  getById(id: number): Observable<ClienteDto> {
    return this.http.get<ClienteDto>(`${this.baseUrl}/${id}`);
  }

  search(req: ClienteSearchRequest = {}): Observable<PagedResult<ClienteDto>> {
    let params = new HttpParams();
    for (const [k, v] of Object.entries(req)) {
      if (v !== undefined && v !== null && v !== '') {
        params = params.set(k, String(v));
      }
    }
    return this.http.get<PagedResult<ClienteDto>>(`${this.baseUrl}/search`, { params });
  }

  create(dto: CreateClienteRequest): Observable<ClienteDto> {
    return this.http.post<ClienteDto>(this.baseUrl, dto);
  }

  update(id: number, dto: UpdateClienteRequest): Observable<ClienteDto> {
    return this.http.put<ClienteDto>(`${this.baseUrl}/${id}`, dto);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
