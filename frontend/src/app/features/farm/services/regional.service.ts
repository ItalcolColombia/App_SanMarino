// src/app/features/farm/services/regional.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';

export interface RegionalDto {
  regionalCia: number;
  regionalId: number;
  regionalNombre: string;
  regionalEstado: string;
  regionalCodigo: string;
}

@Injectable({ providedIn: 'root' })
export class RegionalService {
  private readonly baseUrl = `${environment.apiUrl}/Regional`;

  constructor(private http: HttpClient) {}

  /** Obtiene todas las regionales */
  getAll(): Observable<RegionalDto[]> {
    return this.http.get<RegionalDto[]>(this.baseUrl);
  }

  /** Obtiene regionales por compañía */
  getByCompany(companyId: number): Observable<RegionalDto[]> {
    return this.getAll().pipe(
      map(regionales => regionales.filter(r => r.regionalCia === companyId))
    );
  }

  /** Obtiene una regional por ID y compañía */
  getById(companyId: number, regionalId: number): Observable<RegionalDto> {
    return this.http.get<RegionalDto>(`${this.baseUrl}/${companyId}/${regionalId}`);
  }
}
