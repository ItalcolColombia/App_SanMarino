import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface LotePosturaBaseDto {
  lotePosturaBaseId: number;
  loteNombre: string;
  codigoErp: string | null;
  cantidadHembras: number;
  cantidadMachos: number;
  cantidadMixtas: number;
  companyId: number;
  createdByUserId: number;
  paisId: number | null;
  createdAt: string;
}

export interface CreateLotePosturaBaseDto {
  loteNombre: string;
  codigoErp: string | null;
  cantidadHembras: number;
  cantidadMachos: number;
  cantidadMixtas: number;
}

@Injectable({ providedIn: 'root' })
export class LotePosturaBaseService {
  private readonly baseUrl = '/api/LotePosturaBase';
  constructor(private http: HttpClient) {}

  getAll(): Observable<LotePosturaBaseDto[]> {
    return this.http.get<LotePosturaBaseDto[]>(this.baseUrl);
  }

  create(dto: CreateLotePosturaBaseDto): Observable<LotePosturaBaseDto> {
    return this.http.post<LotePosturaBaseDto>(this.baseUrl, dto);
  }
}

