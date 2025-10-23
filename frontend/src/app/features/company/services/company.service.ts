// src/app/features/company/services/company.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { catchError, map, of, switchMap } from 'rxjs';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface CompanyDto {
  id: number;
  name: string;
}

@Injectable({ providedIn: 'root' })
export class CompanyService {
  private readonly base = `${environment.apiUrl}/Company`;

  constructor(private http: HttpClient) {}

  /** Intenta /Company, si falla prueba /Company/GetAll. Normaliza {id,name}. */
  getAll(): Observable<CompanyDto[]> {
    return this.http.get<any>(this.base).pipe(
      switchMap((res) => of(this.normalizeList(res))),
      catchError(() => this.http.get<any>(`${this.base}/GetAll`).pipe(
        map((res) => this.normalizeList(res))
      ))
    );
  }

  private normalizeList(input: any): CompanyDto[] {
    const list = Array.isArray(input)
      ? input
      : (Array.isArray(input?.data) ? input.data : []);

    return (list || [])
      .map((x: any) => ({
        id: Number(x?.id ?? x?.companyId ?? x?.CompanyId),
        name: String(x?.name ?? x?.nombre ?? `Empresa ${x?.id ?? x?.companyId ?? ''}`).trim()
      }))
      .filter((x: CompanyDto) => Number.isFinite(x.id) && x.name.length > 0)
      .sort((a: CompanyDto, b: CompanyDto) => a.id - b.id);
  }
}
