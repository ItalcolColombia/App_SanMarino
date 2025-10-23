// src/app/core/services/pais/pais.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface PaisDto {
  paisId: number;
  paisNombre: string;
}

@Injectable({ providedIn: 'root' })
export class PaisService {
  private readonly baseUrl = `${environment.apiUrl}/Pais`;
  constructor(private http: HttpClient) {}
  getAll(): Observable<PaisDto[]> {
    return this.http.get<PaisDto[]>(this.baseUrl);
  }
}
